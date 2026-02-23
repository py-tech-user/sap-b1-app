namespace SapB1App.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Location Track DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class LocationTrackDto
{
    public int      Id         { get; set; }
    public int      UserId     { get; set; }
    public string   UserName   { get; set; } = string.Empty;
    public double   Latitude   { get; set; }
    public double   Longitude  { get; set; }
    public double?  Accuracy   { get; set; }
    public double?  Speed      { get; set; }
    public double?  Heading    { get; set; }
    public double?  Altitude   { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class CreateLocationTrackDto
{
    public double   Latitude   { get; set; }
    public double   Longitude  { get; set; }
    public double?  Accuracy   { get; set; }
    public double?  Speed      { get; set; }
    public double?  Heading    { get; set; }
    public double?  Altitude   { get; set; }
    public DateTime? RecordedAt { get; set; }  // Si null, utilise DateTime.UtcNow
}

/// <summary>
/// Envoi batch de plusieurs positions (pour sync différée).
/// </summary>
public class BatchLocationTrackDto
{
    public List<CreateLocationTrackDto> Positions { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// Check-in / Check-out DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class CheckInDto
{
    public int     VisitId   { get; set; }
    public double  Latitude  { get; set; }
    public double  Longitude { get; set; }
    public double? Accuracy  { get; set; }
    public string? Notes     { get; set; }
}

public class CheckOutDto
{
    public int     VisitId   { get; set; }
    public double  Latitude  { get; set; }
    public double  Longitude { get; set; }
    public double? Accuracy  { get; set; }
    public string? Notes     { get; set; }
}

public class CheckPointDto
{
    public int      Id        { get; set; }
    public int      VisitId   { get; set; }
    public int      UserId    { get; set; }
    public string   UserName  { get; set; } = string.Empty;
    public string   Type      { get; set; } = string.Empty;  // "CheckIn" ou "CheckOut"
    public double   Latitude  { get; set; }
    public double   Longitude { get; set; }
    public double?  Accuracy  { get; set; }
    public string?  Address   { get; set; }
    public string?  Notes     { get; set; }
    public DateTime Timestamp { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Position en temps réel DTOs
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Position actuelle d'un commercial (dernière position connue).
/// </summary>
public class UserLivePositionDto
{
    public int      UserId        { get; set; }
    public string   UserName      { get; set; } = string.Empty;
    public string   UserFullName  { get; set; } = string.Empty;
    public double   Latitude      { get; set; }
    public double   Longitude     { get; set; }
    public double?  Speed         { get; set; }
    public double?  Heading       { get; set; }
    public DateTime LastUpdate    { get; set; }
    public bool     IsOnline      { get; set; }  // True si dernière position < 5 min
    public string?  CurrentStatus { get; set; }  // "En visite", "En déplacement", "Inactif"
}

// ═══════════════════════════════════════════════════════════════════════════
// Historique et statistiques DTOs
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Trajet d'un commercial (liste de points pour affichage sur carte).
/// </summary>
public class UserTrackHistoryDto
{
    public int      UserId          { get; set; }
    public string   UserName        { get; set; } = string.Empty;
    public DateOnly Date            { get; set; }
    public double   TotalDistanceKm { get; set; }
    public TimeSpan Duration        { get; set; }
    public int      VisitsCount     { get; set; }
    public List<TrackPointDto> Points { get; set; } = new();
}

public class TrackPointDto
{
    public double   Latitude   { get; set; }
    public double   Longitude  { get; set; }
    public DateTime Timestamp  { get; set; }
    public double?  Speed      { get; set; }
}

/// <summary>
/// Résumé journalier pour dashboard.
/// </summary>
public class DailyTrackSummaryDto
{
    public int      UserId          { get; set; }
    public string   UserName        { get; set; } = string.Empty;
    public DateOnly Date            { get; set; }
    public double   TotalDistanceKm { get; set; }
    public int      PointsCount     { get; set; }
    public int      VisitsCount     { get; set; }
    public TimeSpan? TotalDuration  { get; set; }
}

/// <summary>
/// Statistiques de suivi pour un commercial.
/// </summary>
public class UserTrackingStatsDto
{
    public int    UserId                 { get; set; }
    public string UserName               { get; set; } = string.Empty;
    public double TotalDistanceKmToday   { get; set; }
    public double TotalDistanceKmWeek    { get; set; }
    public double TotalDistanceKmMonth   { get; set; }
    public int    VisitsCompletedToday   { get; set; }
    public int    VisitsCompletedWeek    { get; set; }
    public int    VisitsCompletedMonth   { get; set; }
    public double AvgDistancePerVisit    { get; set; }
    public double AvgVisitDurationMins   { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Distance calculation DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class DistanceRequestDto
{
    public double FromLatitude  { get; set; }
    public double FromLongitude { get; set; }
    public double ToLatitude    { get; set; }
    public double ToLongitude   { get; set; }
}

public class DistanceResultDto
{
    public double DistanceKm    { get; set; }
    public double DistanceMeters { get; set; }
    public string FormattedDistance { get; set; } = string.Empty;  // "2.5 km" ou "500 m"
}
