using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface ITrackingService
{
    // ── Position en temps réel ─────────────────────────────────────────────
    
    /// <summary>
    /// Enregistre une nouvelle position GPS pour un utilisateur.
    /// </summary>
    Task<LocationTrackDto> RecordPositionAsync(int userId, CreateLocationTrackDto dto);
    
    /// <summary>
    /// Enregistre plusieurs positions en batch (pour sync différée).
    /// </summary>
    Task<int> RecordBatchPositionsAsync(int userId, BatchLocationTrackDto dto);
    
    /// <summary>
    /// Récupère la dernière position connue d'un utilisateur.
    /// </summary>
    Task<UserLivePositionDto?> GetUserLivePositionAsync(int userId);
    
    /// <summary>
    /// Récupère les positions en temps réel de tous les commerciaux.
    /// </summary>
    Task<List<UserLivePositionDto>> GetAllLivePositionsAsync();
    
    // ── Historique des déplacements ────────────────────────────────────────
    
    /// <summary>
    /// Récupère l'historique des positions d'un utilisateur pour une date.
    /// </summary>
    Task<UserTrackHistoryDto> GetUserTrackHistoryAsync(int userId, DateOnly date);
    
    /// <summary>
    /// Récupère l'historique des positions entre deux dates.
    /// </summary>
    Task<List<LocationTrackDto>> GetTrackHistoryAsync(
        int userId, DateTime from, DateTime to, int? maxPoints = null);
    
    // ── Check-in / Check-out ───────────────────────────────────────────────
    
    /// <summary>
    /// Effectue un check-in pour une visite.
    /// </summary>
    Task<VisitDto> CheckInAsync(int userId, CheckInDto dto);
    
    /// <summary>
    /// Effectue un check-out pour une visite.
    /// </summary>
    Task<VisitDto> CheckOutAsync(int userId, CheckOutDto dto);
    
    /// <summary>
    /// Récupère les checkpoints d'une visite.
    /// </summary>
    Task<List<CheckPointDto>> GetVisitCheckPointsAsync(int visitId);
    
    // ── Distance et statistiques ───────────────────────────────────────────
    
    /// <summary>
    /// Calcule la distance entre deux points GPS (formule Haversine).
    /// </summary>
    DistanceResultDto CalculateDistance(DistanceRequestDto request);
    
    /// <summary>
    /// Calcule la distance totale parcourue par un utilisateur sur une période.
    /// </summary>
    Task<double> CalculateTotalDistanceAsync(int userId, DateTime from, DateTime to);
    
    /// <summary>
    /// Récupère les statistiques de tracking d'un utilisateur.
    /// </summary>
    Task<UserTrackingStatsDto> GetUserTrackingStatsAsync(int userId);

    /// <summary>
    /// Récupère les statistiques de tracking de tous les utilisateurs.
    /// </summary>
    Task<List<UserTrackingStatsDto>> GetAllUsersTrackingStatsAsync();

    /// <summary>
    /// Récupère le résumé journalier d'un utilisateur.
    /// </summary>
    Task<DailyTrackSummaryDto?> GetDailySummaryAsync(int userId, DateOnly date);
    
    /// <summary>
    /// Met à jour ou crée le résumé journalier (appelé automatiquement).
    /// </summary>
    Task UpdateDailySummaryAsync(int userId, DateOnly date);
}
