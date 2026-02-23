using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.ManagerOrAdmin)]  // Manager ou Admin uniquement
public class ReportingController : ControllerBase
{
    private readonly IReportingService _service;

    public ReportingController(IReportingService service)
    {
        _service = service;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dashboard principal
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère le tableau de bord avancé complet avec tous les KPIs et rapports.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdvancedDashboardDto>>> GetAdvancedDashboard(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var filter = new ReportFilterDto { StartDate = startDate, EndDate = endDate };
        var result = await _service.GetAdvancedDashboardAsync(filter);
        return Ok(new ApiResponse<AdvancedDashboardDto>(true, null, result));
    }

    /// <summary>
    /// Récupère les KPIs simplifiés du jour.
    /// </summary>
    [HttpGet("kpis")]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDailyKPIs()
    {
        var result = await _service.GetDailyKPIsAsync();
        return Ok(new ApiResponse<DashboardDto>(true, null, result));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Top 10 Clients
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère les top clients par chiffre d'affaires.
    /// </summary>
    [HttpGet("top-customers")]
    public async Task<ActionResult<ApiResponse<List<TopCustomerDto>>>> GetTopCustomers(
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _service.GetTopCustomersAsync(limit, startDate, endDate);
        return Ok(new ApiResponse<List<TopCustomerDto>>(true, null, result, result.Count));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Top Articles vendus
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère les articles les plus vendus.
    /// </summary>
    [HttpGet("top-products")]
    public async Task<ActionResult<ApiResponse<List<TopProductDto>>>> GetTopProducts(
        [FromQuery] int limit = 10,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _service.GetTopProductsAsync(limit, startDate, endDate);
        return Ok(new ApiResponse<List<TopProductDto>>(true, null, result, result.Count));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Évolution du chiffre d'affaires
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère l'évolution du chiffre d'affaires.
    /// </summary>
    [HttpGet("revenue")]
    public async Task<ActionResult<ApiResponse<RevenueReportDto>>> GetRevenueEvolution(
        [FromQuery] string period = "month",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var result = await _service.GetRevenueEvolutionAsync(period, startDate, endDate);
        return Ok(new ApiResponse<RevenueReportDto>(true, null, result));
    }

    /// <summary>
    /// Récupère uniquement l'évolution mensuelle du CA.
    /// </summary>
    [HttpGet("revenue/monthly")]
    public async Task<ActionResult<ApiResponse<List<RevenueEvolutionDto>>>> GetMonthlyRevenue(
        [FromQuery] int months = 12)
    {
        var result = await _service.GetRevenueEvolutionAsync("month", 
            DateTime.UtcNow.AddMonths(-months), DateTime.UtcNow);
        return Ok(new ApiResponse<List<RevenueEvolutionDto>>(
            true, null, result.MonthlyEvolution, result.MonthlyEvolution.Count));
    }

    /// <summary>
    /// Récupère uniquement l'évolution journalière du CA.
    /// </summary>
    [HttpGet("revenue/daily")]
    public async Task<ActionResult<ApiResponse<List<RevenueEvolutionDto>>>> GetDailyRevenue(
        [FromQuery] int days = 30)
    {
        var result = await _service.GetRevenueEvolutionAsync("day", 
            DateTime.UtcNow.AddDays(-days), DateTime.UtcNow);
        return Ok(new ApiResponse<List<RevenueEvolutionDto>>(
            true, null, result.DailyEvolution, result.DailyEvolution.Count));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Encaissements en attente
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère les commandes avec paiements en attente.
    /// </summary>
    [HttpGet("pending-payments")]
    public async Task<ActionResult<ApiResponse<PendingPaymentsReportDto>>> GetPendingPayments(
        [FromQuery] int? customerId = null)
    {
        var result = await _service.GetPendingPaymentsAsync(customerId);
        return Ok(new ApiResponse<PendingPaymentsReportDto>(true, null, result));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Commandes en retard
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Récupère les commandes en retard de livraison.
    /// </summary>
    [HttpGet("late-orders")]
    public async Task<ActionResult<ApiResponse<LateOrdersReportDto>>> GetLateOrders(
        [FromQuery] int? daysThreshold = null)
    {
        var result = await _service.GetLateOrdersAsync(daysThreshold);
        return Ok(new ApiResponse<LateOrdersReportDto>(true, null, result));
    }
}
