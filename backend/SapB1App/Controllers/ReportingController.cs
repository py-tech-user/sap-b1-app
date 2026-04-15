using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using System.Text.Json;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly ISapB1Service _sapService;
    private readonly ILogger<ReportingController> _logger;

    public ReportingController(IReportingService reportingService, ISapB1Service sapService, ILogger<ReportingController> logger)
    {
        _reportingService = reportingService;
        _sapService = sapService;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdvancedDashboardDto>>> GetDashboard()
    {
        try
        {
            var data = await BuildSapDashboardAsync();
            return Ok(new ApiResponse<AdvancedDashboardDto>(true, null, data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Dashboard loading failed.");
            return StatusCode(500, new ApiResponse<AdvancedDashboardDto>(false, "Impossible de charger le reporting.", null));
        }
    }

    private async Task<AdvancedDashboardDto> BuildSapDashboardAsync()
    {
        var customersResult = await _sapService.ServiceLayerGetAsync(
            "BusinessPartners?$select=CardCode&$filter=CardType eq 'cCustomer'",
            HttpContext.RequestAborted);

        var invoicesResult = await _sapService.ServiceLayerGetAsync(
            "Invoices?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,PaidToDate,DocumentStatus&$top=500",
            HttpContext.RequestAborted);

        var itemsResult = await _sapService.ServiceLayerGetAsync(
            "Items?$select=ItemCode,ItemName,OnHand,AvgPrice&$top=50",
            HttpContext.RequestAborted);

        var customerRows = ExtractArray(customersResult.Response);
        var invoiceRows = ExtractArray(invoicesResult.Response);
        var itemRows = ExtractArray(itemsResult.Response);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        var invoiceData = invoiceRows.Select(x => new
        {
            DocEntry = GetInt(x, "DocEntry"),
            DocNum = GetInt(x, "DocNum"),
            CardCode = GetString(x, "CardCode"),
            CardName = GetString(x, "CardName"),
            DocDate = GetDate(x, "DocDate"),
            DocDueDate = GetDate(x, "DocDueDate"),
            DocTotal = GetDecimal(x, "DocTotal"),
            PaidToDate = GetDecimal(x, "PaidToDate"),
            Status = GetString(x, "DocumentStatus")
        }).ToList();

        var totalRevenue = invoiceData.Sum(x => x.DocTotal);
        var revenueThisMonth = invoiceData.Where(x => x.DocDate.HasValue && x.DocDate.Value.Date >= startOfMonth).Sum(x => x.DocTotal);

        var pendingPayments = invoiceData
            .Where(x => string.Equals(x.Status, "bost_Open", StringComparison.OrdinalIgnoreCase) && x.DocTotal > x.PaidToDate)
            .Select(x => new PendingPaymentDto
            {
                OrderId = x.DocEntry,
                DocNum = x.DocNum > 0 ? x.DocNum.ToString() : x.DocEntry.ToString(),
                CustomerId = 0,
                CustomerCode = x.CardCode,
                CustomerName = x.CardName,
                OrderTotal = x.DocTotal,
                PaidAmount = x.PaidToDate,
                RemainingAmount = Math.Max(0, x.DocTotal - x.PaidToDate),
                OrderDate = x.DocDate ?? now,
                DaysOverdue = x.DocDate.HasValue ? Math.Max(0, (now.Date - x.DocDate.Value.Date).Days) : 0,
                Status = x.Status
            })
            .OrderByDescending(x => x.RemainingAmount)
            .Take(10)
            .ToList();

        var topCustomers = invoiceData
            .GroupBy(x => new { x.CardCode, x.CardName })
            .Select(g => new TopCustomerDto
            {
                CustomerId = 0,
                CardCode = g.Key.CardCode,
                CardName = g.Key.CardName,
                TotalRevenue = g.Sum(x => x.DocTotal),
                OrderCount = g.Count(),
                AvgOrderValue = g.Count() > 0 ? g.Sum(x => x.DocTotal) / g.Count() : 0,
                LastOrderDate = g.Max(x => x.DocDate)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(10)
            .ToList();

        var topProducts = itemRows
            .Select(x => new TopProductDto
            {
                ProductId = 0,
                ItemCode = GetString(x, "ItemCode"),
                ItemName = GetString(x, "ItemName"),
                TotalQuantity = (int)GetDecimal(x, "OnHand"),
                TotalRevenue = GetDecimal(x, "AvgPrice") * GetDecimal(x, "OnHand"),
                OrderCount = 0
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(10)
            .ToList();

        var recentOrders = invoiceData
            .OrderByDescending(x => x.DocDate)
            .Take(10)
            .Select(x => new RecentOrderDto
            {
                Id = x.DocEntry,
                DocNum = x.DocNum > 0 ? x.DocNum.ToString() : x.DocEntry.ToString(),
                CustomerName = x.CardName,
                Total = x.DocTotal,
                Status = x.Status,
                Date = x.DocDate ?? now
            })
            .ToList();

        var revenueByMonth = invoiceData
            .Where(x => x.DocDate.HasValue)
            .GroupBy(x => x.DocDate!.Value.ToString("yyyy-MM"))
            .OrderBy(x => x.Key)
            .TakeLast(12)
            .Select(g => new RevenueEvolutionDto
            {
                Period = g.Key,
                Revenue = g.Sum(x => x.DocTotal),
                OrderCount = g.Count(),
                AvgOrderValue = g.Count() > 0 ? g.Sum(x => x.DocTotal) / g.Count() : 0
            })
            .ToList();

        var lateOrders = invoiceData
            .Where(x => x.DocDueDate.HasValue && x.DocDueDate.Value.Date < now.Date && string.Equals(x.Status, "bost_Open", StringComparison.OrdinalIgnoreCase))
            .Select(x => new LateOrderDto
            {
                OrderId = x.DocEntry,
                DocNum = x.DocNum > 0 ? x.DocNum.ToString() : x.DocEntry.ToString(),
                CustomerId = 0,
                CustomerCode = x.CardCode,
                CustomerName = x.CardName,
                Total = x.DocTotal,
                OrderDate = x.DocDate ?? now,
                DeliveryDate = x.DocDueDate,
                ExpectedDate = x.DocDueDate ?? now,
                DaysLate = x.DocDueDate.HasValue ? (now.Date - x.DocDueDate.Value.Date).Days : 0,
                Status = x.Status
            })
            .OrderByDescending(x => x.DaysLate)
            .Take(10)
            .ToList();

        return new AdvancedDashboardDto
        {
            TotalCustomers = customerRows.Count,
            ActiveCustomers = customerRows.Count,
            TotalOrders = invoiceData.Count,
            TotalRevenue = totalRevenue,
            RevenueThisMonth = revenueThisMonth,
            GrowthPercent = 0,
            PendingOrdersCount = invoiceData.Count(x => string.Equals(x.Status, "bost_Open", StringComparison.OrdinalIgnoreCase)),
            LateOrdersCount = lateOrders.Count,
            PendingPaymentsAmount = pendingPayments.Sum(x => x.RemainingAmount),
            TopCustomers = topCustomers,
            TopProducts = topProducts,
            RevenueEvolution = revenueByMonth,
            RecentOrders = recentOrders,
            LateOrders = lateOrders,
            PendingPayments = pendingPayments
        };
    }

    private static List<JsonElement> ExtractArray(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(x => x).ToList();
    }

    private static string GetString(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static decimal GetDecimal(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return 0m;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0m;
    }

    private static int GetInt(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0;
    }

    private static DateTime? GetDate(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var date)) return date;
        return null;
    }

    [HttpGet("kpis")]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetKpis()
    {
        try
        {
            var dashboard = await BuildSapDashboardAsync();
            var data = new DashboardDto
            {
                TotalCustomers = dashboard.TotalCustomers,
                TotalOrders = dashboard.TotalOrders,
                TotalRevenue = dashboard.TotalRevenue,
                PendingOrders = dashboard.PendingOrdersCount,
                LowStockProducts = 0,
                RecentOrders = dashboard.RecentOrders,
                TopProducts = dashboard.TopProducts
            };
            return Ok(new ApiResponse<DashboardDto>(true, null, data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] KPI loading failed.");
            return StatusCode(500, new ApiResponse<DashboardDto>(false, "Impossible de charger les KPIs.", null));
        }
    }

    [HttpGet("top-customers")]
    public async Task<ActionResult<ApiResponse<List<TopCustomerDto>>>> GetTopCustomers([FromQuery] int limit = 10)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var dashboard = await BuildSapDashboardAsync();
            var data = dashboard.TopCustomers.Take(safeLimit).ToList();
            return Ok(new ApiResponse<List<TopCustomerDto>>(true, null, data, data.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Top customers loading failed.");
            return StatusCode(500, new ApiResponse<List<TopCustomerDto>>(false, "Impossible de charger les top clients.", null));
        }
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<ApiResponse<List<TopProductDto>>>> GetTopProducts([FromQuery] int limit = 10)
    {
        try
        {
            var safeLimit = Math.Clamp(limit, 1, 100);
            var dashboard = await BuildSapDashboardAsync();
            var data = dashboard.TopProducts.Take(safeLimit).ToList();
            return Ok(new ApiResponse<List<TopProductDto>>(true, null, data, data.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Top products loading failed.");
            return StatusCode(500, new ApiResponse<List<TopProductDto>>(false, "Impossible de charger les top produits.", null));
        }
    }

    [HttpGet("revenue/monthly")]
    public async Task<ActionResult<ApiResponse<List<RevenueEvolutionDto>>>> GetRevenueMonthly([FromQuery] int months = 12)
    {
        try
        {
            var safeMonths = Math.Clamp(months, 1, 60);
            var dashboard = await BuildSapDashboardAsync();
            var data = dashboard.RevenueEvolution.TakeLast(safeMonths).ToList();
            return Ok(new ApiResponse<List<RevenueEvolutionDto>>(true, null, data, data.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Monthly revenue loading failed.");
            return StatusCode(500, new ApiResponse<List<RevenueEvolutionDto>>(false, "Impossible de charger l'évolution mensuelle.", null));
        }
    }

    [HttpGet("revenue/daily")]
    public async Task<ActionResult<ApiResponse<List<RevenueEvolutionDto>>>> GetRevenueDaily([FromQuery] int days = 30)
    {
        try
        {
            var safeDays = Math.Clamp(days, 1, 365);
            var invoicesResult = await _sapService.ServiceLayerGetAsync(
                "Invoices?$select=DocDate,DocTotal&$top=1000",
                HttpContext.RequestAborted);
            var rows = ExtractArray(invoicesResult.Response);
            var grouped = rows
                .Select(x => new { Date = GetDate(x, "DocDate"), Total = GetDecimal(x, "DocTotal") })
                .Where(x => x.Date.HasValue && x.Date.Value.Date >= DateTime.UtcNow.Date.AddDays(-safeDays + 1))
                .GroupBy(x => x.Date!.Value.ToString("yyyy-MM-dd"))
                .OrderBy(g => g.Key)
                .Select(g => new RevenueEvolutionDto
                {
                    Period = g.Key,
                    Revenue = g.Sum(x => x.Total),
                    OrderCount = g.Count(),
                    AvgOrderValue = g.Count() > 0 ? g.Sum(x => x.Total) / g.Count() : 0
                })
                .ToList();

            return Ok(new ApiResponse<List<RevenueEvolutionDto>>(true, null, grouped, grouped.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Daily revenue loading failed.");
            return StatusCode(500, new ApiResponse<List<RevenueEvolutionDto>>(false, "Impossible de charger l'évolution journalière.", null));
        }
    }

    [HttpGet("pending-payments")]
    public async Task<ActionResult<ApiResponse<List<PendingPaymentDto>>>> GetPendingPayments()
    {
        try
        {
            var dashboard = await BuildSapDashboardAsync();
            var data = dashboard.PendingPayments;
            return Ok(new ApiResponse<List<PendingPaymentDto>>(true, null, data, data.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Pending payments loading failed.");
            return StatusCode(500, new ApiResponse<List<PendingPaymentDto>>(false, "Impossible de charger les paiements en attente.", null));
        }
    }

    [HttpGet("late-orders")]
    public async Task<ActionResult<ApiResponse<List<LateOrderDto>>>> GetLateOrders([FromQuery] int daysThreshold = 7)
    {
        try
        {
            var safeThreshold = Math.Clamp(daysThreshold, 1, 365);
            var dashboard = await BuildSapDashboardAsync();
            var data = dashboard.LateOrders.Where(x => x.DaysLate >= safeThreshold).ToList();
            return Ok(new ApiResponse<List<LateOrderDto>>(true, null, data, data.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[REPORTING] Late orders loading failed.");
            return StatusCode(500, new ApiResponse<List<LateOrderDto>>(false, "Impossible de charger les commandes en retard.", null));
        }
    }
}
