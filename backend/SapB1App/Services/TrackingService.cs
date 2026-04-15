using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class TrackingService : ITrackingService
{
    private readonly AppDbContext _db;
    private const double EarthRadiusKm = 6371.0;
    private const int OnlineThresholdMinutes = 5;

    public TrackingService(AppDbContext db)
    {
        _db = db;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Position en temps réel
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<LocationTrackDto> RecordPositionAsync(int userId, CreateLocationTrackDto dto)
    {
        var track = new LocationTrack
        {
            UserId     = userId,
            Latitude   = dto.Latitude,
            Longitude  = dto.Longitude,
            Accuracy   = dto.Accuracy,
            Speed      = dto.Speed,
            Heading    = dto.Heading,
            Altitude   = dto.Altitude,
            RecordedAt = dto.RecordedAt ?? DateTime.UtcNow
        };

        _db.LocationTracks.Add(track);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);

        // Note: Daily summary update will be done separately to avoid concurrency issues

        return new LocationTrackDto
        {
            Id         = track.Id,
            UserId     = track.UserId,
            UserName   = user?.Username ?? "Unknown",
            Latitude   = track.Latitude,
            Longitude  = track.Longitude,
            Accuracy   = track.Accuracy,
            Speed      = track.Speed,
            Heading    = track.Heading,
            Altitude   = track.Altitude,
            RecordedAt = track.RecordedAt
        };
    }

    public async Task<int> RecordBatchPositionsAsync(int userId, BatchLocationTrackDto dto)
    {
        if (dto.Positions.Count == 0) return 0;

        var tracks = dto.Positions.Select(p => new LocationTrack
        {
            UserId     = userId,
            Latitude   = p.Latitude,
            Longitude  = p.Longitude,
            Accuracy   = p.Accuracy,
            Speed      = p.Speed,
            Heading    = p.Heading,
            Altitude   = p.Altitude,
            RecordedAt = p.RecordedAt ?? DateTime.UtcNow
        }).ToList();

        _db.LocationTracks.AddRange(tracks);
        await _db.SaveChangesAsync();

        // Mise à jour des résumés pour chaque jour concerné
        var dates = tracks.Select(t => DateOnly.FromDateTime(t.RecordedAt)).Distinct();
        foreach (var date in dates)
        {
            await UpdateDailySummaryAsync(userId, date);
        }

        return tracks.Count;
    }

    public async Task<UserLivePositionDto?> GetUserLivePositionAsync(int userId)
    {
        var lastTrack = await _db.LocationTracks
            .Where(lt => lt.UserId == userId)
            .OrderByDescending(lt => lt.RecordedAt)
            .Include(lt => lt.User)
            .FirstOrDefaultAsync();

        if (lastTrack == null) return null;

        var isOnline = (DateTime.UtcNow - lastTrack.RecordedAt).TotalMinutes < OnlineThresholdMinutes;
        var currentVisit = await _db.Visits
            .Where(v => v.UserId == userId && v.Status == VisitStatus.InProgress)
            .FirstOrDefaultAsync();

        return new UserLivePositionDto
        {
            UserId       = lastTrack.UserId,
            UserName     = lastTrack.User.Username,
            UserFullName = lastTrack.User.FullName,
            Latitude     = lastTrack.Latitude,
            Longitude    = lastTrack.Longitude,
            Speed        = lastTrack.Speed,
            Heading      = lastTrack.Heading,
            LastUpdate   = lastTrack.RecordedAt,
            IsOnline     = isOnline,
            CurrentStatus = currentVisit != null ? "En visite" : (isOnline ? "En déplacement" : "Inactif")
        };
    }

    public async Task<List<UserLivePositionDto>> GetAllLivePositionsAsync()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Récupérer tous les utilisateurs avec leur dernière position
        var userIds = await _db.LocationTracks
            .Where(lt => lt.RecordedAt > cutoff)
            .Select(lt => lt.UserId)
            .Distinct()
            .ToListAsync();

        var result = new List<UserLivePositionDto>();

        foreach (var userId in userIds)
        {
            var position = await GetUserLivePositionAsync(userId);
            if (position != null)
                result.Add(position);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Historique des déplacements
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<UserTrackHistoryDto> GetUserTrackHistoryAsync(int userId, DateOnly date)
    {
        var user = await _db.Users.FindAsync(userId);
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var tracks = await _db.LocationTracks
            .Where(lt => lt.UserId == userId && lt.RecordedAt >= startOfDay && lt.RecordedAt <= endOfDay)
            .OrderBy(lt => lt.RecordedAt)
            .ToListAsync();

        var visitsCount = await _db.Visits
            .Where(v => v.UserId == userId && v.Date.Date == startOfDay.Date && v.Status == VisitStatus.Completed)
            .CountAsync();

        var totalDistance = CalculateTotalDistanceFromTracks(tracks);
        var duration = tracks.Count >= 2 
            ? tracks.Last().RecordedAt - tracks.First().RecordedAt 
            : TimeSpan.Zero;

        return new UserTrackHistoryDto
        {
            UserId          = userId,
            UserName        = user?.Username ?? "Unknown",
            Date            = date,
            TotalDistanceKm = totalDistance,
            Duration        = duration,
            VisitsCount     = visitsCount,
            Points          = tracks.Select(t => new TrackPointDto
            {
                Latitude  = t.Latitude,
                Longitude = t.Longitude,
                Timestamp = t.RecordedAt,
                Speed     = t.Speed
            }).ToList()
        };
    }

    public async Task<List<LocationTrackDto>> GetTrackHistoryAsync(
        int userId, DateTime from, DateTime to, int? maxPoints = null)
    {
        var query = _db.LocationTracks
            .Where(lt => lt.UserId == userId && lt.RecordedAt >= from && lt.RecordedAt <= to)
            .OrderBy(lt => lt.RecordedAt)
            .Include(lt => lt.User);

        var tracks = maxPoints.HasValue 
            ? await query.Take(maxPoints.Value).ToListAsync()
            : await query.ToListAsync();

        return tracks.Select(t => new LocationTrackDto
        {
            Id         = t.Id,
            UserId     = t.UserId,
            UserName   = t.User.Username,
            Latitude   = t.Latitude,
            Longitude  = t.Longitude,
            Accuracy   = t.Accuracy,
            Speed      = t.Speed,
            Heading    = t.Heading,
            Altitude   = t.Altitude,
            RecordedAt = t.RecordedAt
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Check-in / Check-out
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<VisitDto> CheckInAsync(int userId, CheckInDto dto)
    {
        var visit = await _db.Visits
            .Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == dto.VisitId);

        if (visit == null)
            throw new InvalidOperationException($"Visite ID {dto.VisitId} introuvable.");

        if (visit.Status != VisitStatus.Planned)
            throw new InvalidOperationException("Seule une visite planifiée peut être check-in.");

        // Mise à jour de la visite
        visit.Status           = VisitStatus.InProgress;
        visit.UserId           = userId;
        visit.CheckInAt        = DateTime.UtcNow;
        visit.CheckInLatitude  = dto.Latitude;
        visit.CheckInLongitude = dto.Longitude;
        visit.UpdatedAt        = DateTime.UtcNow;

        // Création du checkpoint
        var checkPoint = new VisitCheckPoint
        {
            VisitId   = visit.Id,
            UserId    = userId,
            Type      = CheckPointType.CheckIn,
            Latitude  = dto.Latitude,
            Longitude = dto.Longitude,
            Accuracy  = dto.Accuracy,
            Notes     = dto.Notes,
            Timestamp = DateTime.UtcNow
        };
        _db.VisitCheckPoints.Add(checkPoint);

        // Enregistrer aussi comme position
        var track = new LocationTrack
        {
            UserId     = userId,
            Latitude   = dto.Latitude,
            Longitude  = dto.Longitude,
            Accuracy   = dto.Accuracy,
            RecordedAt = DateTime.UtcNow
        };
        _db.LocationTracks.Add(track);

        await _db.SaveChangesAsync();

        return MapToVisitDto(visit);
    }

    public async Task<VisitDto> CheckOutAsync(int userId, CheckOutDto dto)
    {
        var visit = await _db.Visits
            .Include(v => v.Customer)
            .FirstOrDefaultAsync(v => v.Id == dto.VisitId);

        if (visit == null)
            throw new InvalidOperationException($"Visite ID {dto.VisitId} introuvable.");

        if (visit.Status != VisitStatus.InProgress)
            throw new InvalidOperationException("Seule une visite en cours peut être check-out.");

        // Calcul de la distance parcourue pendant la visite
        double? distanceKm = null;
        if (visit.CheckInLatitude.HasValue && visit.CheckInLongitude.HasValue)
        {
            distanceKm = CalculateHaversineDistance(
                visit.CheckInLatitude.Value, visit.CheckInLongitude.Value,
                dto.Latitude, dto.Longitude);
        }

        // Mise à jour de la visite
        visit.Status            = VisitStatus.Completed;
        visit.CheckOutAt        = DateTime.UtcNow;
        visit.CheckOutLatitude  = dto.Latitude;
        visit.CheckOutLongitude = dto.Longitude;
        visit.DistanceKm        = distanceKm;
        visit.UpdatedAt         = DateTime.UtcNow;

        // Création du checkpoint
        var checkPoint = new VisitCheckPoint
        {
            VisitId   = visit.Id,
            UserId    = userId,
            Type      = CheckPointType.CheckOut,
            Latitude  = dto.Latitude,
            Longitude = dto.Longitude,
            Accuracy  = dto.Accuracy,
            Notes     = dto.Notes,
            Timestamp = DateTime.UtcNow
        };
        _db.VisitCheckPoints.Add(checkPoint);

        // Enregistrer aussi comme position
        var track = new LocationTrack
        {
            UserId     = userId,
            Latitude   = dto.Latitude,
            Longitude  = dto.Longitude,
            Accuracy   = dto.Accuracy,
            RecordedAt = DateTime.UtcNow
        };
        _db.LocationTracks.Add(track);

        await _db.SaveChangesAsync();

        // Mise à jour du résumé journalier
        await UpdateDailySummaryAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow));

        return MapToVisitDto(visit);
    }

    public async Task<List<CheckPointDto>> GetVisitCheckPointsAsync(int visitId)
    {
        var checkPoints = await _db.VisitCheckPoints
            .Where(cp => cp.VisitId == visitId)
            .Include(cp => cp.User)
            .OrderBy(cp => cp.Timestamp)
            .ToListAsync();

        return checkPoints.Select(cp => new CheckPointDto
        {
            Id        = cp.Id,
            VisitId   = cp.VisitId,
            UserId    = cp.UserId,
            UserName  = cp.User.Username,
            Type      = cp.Type.ToString(),
            Latitude  = cp.Latitude,
            Longitude = cp.Longitude,
            Accuracy  = cp.Accuracy,
            Address   = cp.Address,
            Notes     = cp.Notes,
            Timestamp = cp.Timestamp
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Distance et statistiques
    // ═══════════════════════════════════════════════════════════════════════

    public DistanceResultDto CalculateDistance(DistanceRequestDto request)
    {
        var distanceKm = CalculateHaversineDistance(
            request.FromLatitude, request.FromLongitude,
            request.ToLatitude, request.ToLongitude);

        var distanceMeters = distanceKm * 1000;
        var formatted = distanceKm >= 1 
            ? $"{distanceKm:F1} km" 
            : $"{distanceMeters:F0} m";

        return new DistanceResultDto
        {
            DistanceKm        = distanceKm,
            DistanceMeters    = distanceMeters,
            FormattedDistance = formatted
        };
    }

    public async Task<double> CalculateTotalDistanceAsync(int userId, DateTime from, DateTime to)
    {
        var tracks = await _db.LocationTracks
            .Where(lt => lt.UserId == userId && lt.RecordedAt >= from && lt.RecordedAt <= to)
            .OrderBy(lt => lt.RecordedAt)
            .ToListAsync();

        return CalculateTotalDistanceFromTracks(tracks);
    }

    public async Task<UserTrackingStatsDto> GetUserTrackingStatsAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        var today = DateTime.UtcNow.Date;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1); // Lundi
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        var stats = new UserTrackingStatsDto
        {
            UserId   = userId,
            UserName = user?.Username ?? "Unknown"
        };

        // Distance aujourd'hui
        stats.TotalDistanceKmToday = await CalculateTotalDistanceAsync(userId, today, today.AddDays(1));

        // Distance cette semaine
        stats.TotalDistanceKmWeek = await CalculateTotalDistanceAsync(userId, startOfWeek, today.AddDays(1));

        // Distance ce mois
        stats.TotalDistanceKmMonth = await CalculateTotalDistanceAsync(userId, startOfMonth, today.AddDays(1));

        // Visites complétées
        stats.VisitsCompletedToday = await _db.Visits
            .CountAsync(v => v.UserId == userId && v.CheckOutAt != null && v.CheckOutAt.Value.Date == today);

        stats.VisitsCompletedWeek = await _db.Visits
            .CountAsync(v => v.UserId == userId && v.CheckOutAt != null && v.CheckOutAt.Value >= startOfWeek);

        stats.VisitsCompletedMonth = await _db.Visits
            .CountAsync(v => v.UserId == userId && v.CheckOutAt != null && v.CheckOutAt.Value >= startOfMonth);

        // Moyennes
        if (stats.VisitsCompletedMonth > 0)
        {
            stats.AvgDistancePerVisit = stats.TotalDistanceKmMonth / stats.VisitsCompletedMonth;

            var completedVisits = await _db.Visits
                .Where(v => v.UserId == userId && v.CheckInAt != null && v.CheckOutAt != null && v.CheckOutAt.Value >= startOfMonth)
                .ToListAsync();

            if (completedVisits.Any())
            {
                stats.AvgVisitDurationMins = completedVisits
                    .Average(v => (v.CheckOutAt!.Value - v.CheckInAt!.Value).TotalMinutes);
            }
        }

        return stats;
    }

    public async Task<List<UserTrackingStatsDto>> GetAllUsersTrackingStatsAsync()
    {
        // Récupérer tous les utilisateurs actifs (commerciaux)
        var users = await _db.Users
            .Where(u => u.IsActive && (u.Role == "Commercial" || u.Role == "Manager" || u.Role == "Admin"))
            .ToListAsync();

        var statsList = new List<UserTrackingStatsDto>();

        foreach (var user in users)
        {
            var stats = await GetUserTrackingStatsAsync(user.Id);
            // Ajouter les champs manquants pour le frontend
            stats.TotalVisits = stats.VisitsCompletedMonth;
            stats.CompletedVisits = stats.VisitsCompletedMonth;
            stats.TotalDistanceKm = stats.TotalDistanceKmMonth;
            statsList.Add(stats);
        }

        return statsList;
    }

    public async Task<DailyTrackSummaryDto?> GetDailySummaryAsync(int userId, DateOnly date)
    {
        var summary = await _db.DailyTrackSummaries
            .Include(dts => dts.User)
            .FirstOrDefaultAsync(dts => dts.UserId == userId && dts.Date == date);

        if (summary == null) return null;

        return new DailyTrackSummaryDto
        {
            UserId          = summary.UserId,
            UserName        = summary.User.Username,
            Date            = summary.Date,
            TotalDistanceKm = summary.TotalDistanceKm,
            PointsCount     = summary.PointsCount,
            VisitsCount     = summary.VisitsCount,
            TotalDuration   = summary.TotalDuration
        };
    }

    public async Task UpdateDailySummaryAsync(int userId, DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var tracks = await _db.LocationTracks
            .Where(lt => lt.UserId == userId && lt.RecordedAt >= startOfDay && lt.RecordedAt <= endOfDay)
            .OrderBy(lt => lt.RecordedAt)
            .ToListAsync();

        var visitsCount = await _db.Visits
            .CountAsync(v => v.UserId == userId && v.CheckOutAt != null && 
                           v.CheckOutAt.Value >= startOfDay && v.CheckOutAt.Value <= endOfDay);

        var totalDistance = CalculateTotalDistanceFromTracks(tracks);
        var duration = tracks.Count >= 2 
            ? tracks.Last().RecordedAt - tracks.First().RecordedAt 
            : (TimeSpan?)null;

        var existing = await _db.DailyTrackSummaries
            .FirstOrDefaultAsync(dts => dts.UserId == userId && dts.Date == date);

        if (existing != null)
        {
            existing.TotalDistanceKm = totalDistance;
            existing.PointsCount     = tracks.Count;
            existing.VisitsCount     = visitsCount;
            existing.TotalDuration   = duration;
            existing.UpdatedAt       = DateTime.UtcNow;
        }
        else
        {
            _db.DailyTrackSummaries.Add(new DailyTrackSummary
            {
                UserId          = userId,
                Date            = date,
                TotalDistanceKm = totalDistance,
                PointsCount     = tracks.Count,
                VisitsCount     = visitsCount,
                TotalDuration   = duration
            });
        }

        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Méthodes privées
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calcule la distance entre deux points GPS avec la formule Haversine.
    /// </summary>
    private static double CalculateHaversineDistance(
        double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Calcule la distance totale à partir d'une liste de positions ordonnées.
    /// </summary>
    private static double CalculateTotalDistanceFromTracks(List<LocationTrack> tracks)
    {
        if (tracks.Count < 2) return 0;

        double total = 0;
        for (int i = 1; i < tracks.Count; i++)
        {
            total += CalculateHaversineDistance(
                tracks[i - 1].Latitude, tracks[i - 1].Longitude,
                tracks[i].Latitude, tracks[i].Longitude);
        }
        return total;
    }

    private static VisitDto MapToVisitDto(Visit visit)
    {
        return new VisitDto
        {
            Id           = visit.Id,
            CustomerId   = visit.CustomerId,
            CustomerName = visit.Customer?.CardName ?? string.Empty,
            CustomerCode = visit.Customer?.CardCode ?? string.Empty,
            Date         = visit.Date,
            Status       = visit.Status.ToString(),
            Comments     = visit.Comments,
            Latitude     = visit.Latitude,
            Longitude    = visit.Longitude,
            SyncedToSap  = visit.SyncedToSap,
            CreatedAt    = visit.CreatedAt
        };
    }
}
