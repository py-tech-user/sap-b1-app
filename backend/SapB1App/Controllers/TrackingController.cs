using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;
using System.Security.Claims;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles
public class TrackingController : ControllerBase
{
    private readonly ITrackingService _service;

    public TrackingController(ITrackingService service)
    {
        _service = service;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Position en temps réel
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Enregistre la position GPS actuelle du commercial connecté.
    /// </summary>
    [HttpPost("position")]
    public async Task<ActionResult<ApiResponse<LocationTrackDto>>> RecordPosition(
        [FromBody] CreateLocationTrackDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _service.RecordPositionAsync(userId, dto);
        return Ok(new ApiResponse<LocationTrackDto>(true, "Position enregistrée.", result));
    }

    /// <summary>
    /// Enregistre plusieurs positions en batch (pour sync différée/offline).
    /// </summary>
    [HttpPost("positions/batch")]
    public async Task<ActionResult<ApiResponse<int>>> RecordBatchPositions(
        [FromBody] BatchLocationTrackDto dto)
    {
        var userId = GetCurrentUserId();
        var count = await _service.RecordBatchPositionsAsync(userId, dto);
        return Ok(new ApiResponse<int>(true, $"{count} position(s) enregistrée(s).", count));
    }

    /// <summary>
    /// Récupère la position en temps réel d'un commercial.
    /// </summary>
    [HttpGet("live/{userId:int}")]
    public async Task<ActionResult<ApiResponse<UserLivePositionDto>>> GetUserLivePosition(int userId)
    {
        var result = await _service.GetUserLivePositionAsync(userId);

        if (result is null)
            return NotFound(new ApiResponse<UserLivePositionDto>(
                false, "Aucune position trouvée pour cet utilisateur.", null));

        return Ok(new ApiResponse<UserLivePositionDto>(true, null, result));
    }

    /// <summary>
    /// Récupère les positions en temps réel de tous les commerciaux.
    /// </summary>
    [HttpGet("live")]
    public async Task<ActionResult<ApiResponse<List<UserLivePositionDto>>>> GetAllLivePositions()
    {
        var result = await _service.GetAllLivePositionsAsync();
        return Ok(new ApiResponse<List<UserLivePositionDto>>(
            true, null, result, result.Count));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Historique des déplacements
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère l'historique complet des déplacements d'un commercial pour une date.
    /// </summary>
    [HttpGet("history/{userId:int}/{date}")]
    public async Task<ActionResult<ApiResponse<UserTrackHistoryDto>>> GetUserTrackHistory(
        int userId, DateOnly date)
    {
        var result = await _service.GetUserTrackHistoryAsync(userId, date);
        return Ok(new ApiResponse<UserTrackHistoryDto>(true, null, result));
    }

    /// <summary>
    /// Récupère l'historique des positions entre deux dates.
    /// </summary>
    [HttpGet("history/{userId:int}")]
    public async Task<ActionResult<ApiResponse<List<LocationTrackDto>>>> GetTrackHistory(
        int userId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? maxPoints = null)
    {
        var result = await _service.GetTrackHistoryAsync(userId, from, to, maxPoints);
        return Ok(new ApiResponse<List<LocationTrackDto>>(
            true, null, result, result.Count));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Check-in / Check-out
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Effectue un check-in pour une visite client.
    /// </summary>
    [HttpPost("checkin")]
    [HttpPost("check-in")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> CheckIn([FromBody] CheckInDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.CheckInAsync(userId, dto);
            return Ok(new ApiResponse<VisitDto>(true, "Check-in effectué avec succès.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<VisitDto>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Effectue un check-out pour une visite client.
    /// </summary>
    [HttpPost("checkout")]
    [HttpPost("check-out")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> CheckOut([FromBody] CheckOutDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _service.CheckOutAsync(userId, dto);
            return Ok(new ApiResponse<VisitDto>(true, "Check-out effectué avec succès.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<VisitDto>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Récupère les checkpoints (check-in/out) d'une visite.
    /// </summary>
    [HttpGet("visits/{visitId:int}/checkpoints")]
    public async Task<ActionResult<ApiResponse<List<CheckPointDto>>>> GetVisitCheckPoints(int visitId)
    {
        var result = await _service.GetVisitCheckPointsAsync(visitId);
        return Ok(new ApiResponse<List<CheckPointDto>>(true, null, result, result.Count));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Distance et statistiques
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calcule la distance entre deux points GPS.
    /// </summary>
    [HttpPost("distance")]
    public ActionResult<ApiResponse<DistanceResultDto>> CalculateDistance(
        [FromBody] DistanceRequestDto request)
    {
        var result = _service.CalculateDistance(request);
        return Ok(new ApiResponse<DistanceResultDto>(true, null, result));
    }

    /// <summary>
    /// Récupère les statistiques de tracking d'un commercial.
    /// </summary>
    [HttpGet("stats/{userId:int}")]
    public async Task<ActionResult<ApiResponse<UserTrackingStatsDto>>> GetUserStats(int userId)
    {
        var result = await _service.GetUserTrackingStatsAsync(userId);
        return Ok(new ApiResponse<UserTrackingStatsDto>(true, null, result));
    }

    /// <summary>
    /// Récupère les statistiques du commercial connecté.
    /// </summary>
    [HttpGet("stats/me")]
    public async Task<ActionResult<ApiResponse<UserTrackingStatsDto>>> GetMyStats()
    {
        var userId = GetCurrentUserId();
        var result = await _service.GetUserTrackingStatsAsync(userId);
        return Ok(new ApiResponse<UserTrackingStatsDto>(true, null, result));
    }

    /// <summary>
    /// Récupère le résumé journalier d'un commercial.
    /// </summary>
    [HttpGet("summary/{userId:int}/{date}")]
    public async Task<ActionResult<ApiResponse<DailyTrackSummaryDto>>> GetDailySummary(
        int userId, DateOnly date)
    {
        var result = await _service.GetDailySummaryAsync(userId, date);

        if (result is null)
            return NotFound(new ApiResponse<DailyTrackSummaryDto>(
                false, "Aucun résumé pour cette date.", null));

        return Ok(new ApiResponse<DailyTrackSummaryDto>(true, null, result));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Méthodes privées
    // ═══════════════════════════════════════════════════════════════════════

    private int GetCurrentUserId()
    {
        // En mode développement sans JWT, utiliser un ID par défaut
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 1;
    }
}
