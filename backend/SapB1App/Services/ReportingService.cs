using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _db;

    public ReportingService(AppDbContext db)
    {
        _db = db;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dashboard principal
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<AdvancedDashboardDto> GetAdvancedDashboardAsync(ReportFilterDto? filter = null)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfLastMonth = startOfMonth.AddMonths(-1);

        // KPIs de base
        var totalCustomers = await _db.Customers.CountAsync();
        var activeCustomers = await _db.Customers.CountAsync(c => c.PartnerType == PartnerType.Client);
        var totalOrders = await _db.Orders.CountAsync();
        var totalRevenue = await _db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => o.DocTotal);

        var revenueThisMonth = await _db.Orders
            .Where(o => o.DocDate >= startOfMonth && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => o.DocTotal);

        var revenueLastMonth = await _db.Orders
            .Where(o => o.DocDate >= startOfLastMonth && o.DocDate < startOfMonth && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => o.DocTotal);

        var growthPercent = revenueLastMonth > 0 
            ? ((revenueThisMonth - revenueLastMonth) / revenueLastMonth) * 100 
            : 0;

        // Alertes
        var pendingOrdersCount = await _db.Orders
            .CountAsync(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Draft);
        
        var lateOrdersCount = await _db.Orders
            .CountAsync(o => o.DeliveryDate.HasValue && o.DeliveryDate < now && 
                           o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);

        var pendingPaymentsAmount = await GetPendingPaymentsTotalAsync();

        // Données détaillées
        var topCustomers = await GetTopCustomersAsync(10);
        var topProducts = await GetTopProductsAsync(10);
        var revenueEvolution = await GetMonthlyEvolutionAsync(12);
        var recentOrders = await GetRecentOrdersAsync(10);
        var lateOrders = (await GetLateOrdersAsync()).LateOrders.Take(10).ToList();
        var pendingPayments = (await GetPendingPaymentsAsync()).PendingPayments.Take(10).ToList();

        return new AdvancedDashboardDto
        {
            TotalCustomers = totalCustomers,
            ActiveCustomers = activeCustomers,
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            RevenueThisMonth = revenueThisMonth,
            GrowthPercent = growthPercent,
            PendingOrdersCount = pendingOrdersCount,
            LateOrdersCount = lateOrdersCount,
            PendingPaymentsAmount = pendingPaymentsAmount,
            TopCustomers = topCustomers,
            TopProducts = topProducts,
            RevenueEvolution = revenueEvolution,
            RecentOrders = recentOrders,
            LateOrders = lateOrders,
            PendingPayments = pendingPayments
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Top 10 Clients
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<TopCustomerDto>> GetTopCustomersAsync(
        int limit = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status != OrderStatus.Cancelled);

        if (startDate.HasValue)
            query = query.Where(o => o.DocDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(o => o.DocDate <= endDate.Value);

        var customerStats = await query
            .GroupBy(o => new { o.CustomerId, o.Customer.CardCode, o.Customer.CardName, o.Customer.City })
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.CardCode,
                g.Key.CardName,
                g.Key.City,
                TotalRevenue = g.Sum(o => o.DocTotal),
                OrderCount = g.Count(),
                LastOrderDate = g.Max(o => o.DocDate)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(limit)
            .ToListAsync();

        // Récupérer les visites pour chaque client
        var customerIds = customerStats.Select(c => c.CustomerId).ToList();
        var visitCounts = await _db.Visits
            .Where(v => customerIds.Contains(v.CustomerId) && v.Status == VisitStatus.Completed)
            .GroupBy(v => v.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Count);

        return customerStats.Select(c => new TopCustomerDto
        {
            CustomerId = c.CustomerId,
            CardCode = c.CardCode,
            CardName = c.CardName,
            City = c.City,
            TotalRevenue = c.TotalRevenue,
            OrderCount = c.OrderCount,
            VisitCount = visitCounts.GetValueOrDefault(c.CustomerId, 0),
            AvgOrderValue = c.OrderCount > 0 ? c.TotalRevenue / c.OrderCount : 0,
            LastOrderDate = c.LastOrderDate
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Top Articles vendus
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<TopProductDto>> GetTopProductsAsync(
        int limit = 10, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _db.OrderLines
            .Include(ol => ol.Order)
            .Include(ol => ol.Product)
            .Where(ol => ol.Order.Status != OrderStatus.Cancelled);

        if (startDate.HasValue)
            query = query.Where(ol => ol.Order.DocDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(ol => ol.Order.DocDate <= endDate.Value);

        return await query
            .GroupBy(ol => new { ol.ProductId, ol.Product.ItemCode, ol.Product.ItemName })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ItemCode = g.Key.ItemCode,
                ItemName = g.Key.ItemName,
                TotalQuantity = (int)g.Sum(ol => ol.Quantity),
                TotalRevenue = g.Sum(ol => ol.LineTotal),
                OrderCount = g.Select(ol => ol.OrderId).Distinct().Count()
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(limit)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Évolution du chiffre d'affaires
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<RevenueReportDto> GetRevenueEvolutionAsync(
        string period = "month", DateTime? startDate = null, DateTime? endDate = null)
    {
        var now = DateTime.UtcNow;
        startDate ??= now.AddMonths(-12);
        endDate ??= now;

        var orders = await _db.Orders
            .Where(o => o.DocDate >= startDate && o.DocDate <= endDate && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.DocDate, o.DocTotal })
            .ToListAsync();

        var totalRevenue = orders.Sum(o => o.DocTotal);
        
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var startOfLastMonth = startOfMonth.AddMonths(-1);
        
        var revenueThisMonth = orders.Where(o => o.DocDate >= startOfMonth).Sum(o => o.DocTotal);
        var revenueLastMonth = orders.Where(o => o.DocDate >= startOfLastMonth && o.DocDate < startOfMonth).Sum(o => o.DocTotal);
        var growthPercent = revenueLastMonth > 0 
            ? ((revenueThisMonth - revenueLastMonth) / revenueLastMonth) * 100 
            : 0;

        var daysInPeriod = (endDate.Value - startDate.Value).Days;
        var avgDailyRevenue = daysInPeriod > 0 ? totalRevenue / daysInPeriod : 0;

        // Évolution mensuelle
        var monthlyEvolution = await GetMonthlyEvolutionAsync(12);

        // Évolution journalière (30 derniers jours)
        var dailyEvolution = await GetDailyEvolutionAsync(30);

        return new RevenueReportDto
        {
            TotalRevenue = totalRevenue,
            RevenueThisMonth = revenueThisMonth,
            RevenueLastMonth = revenueLastMonth,
            GrowthPercent = growthPercent,
            AvgDailyRevenue = avgDailyRevenue,
            MonthlyEvolution = monthlyEvolution,
            DailyEvolution = dailyEvolution
        };
    }

    private async Task<List<RevenueEvolutionDto>> GetMonthlyEvolutionAsync(int months)
    {
        var now = DateTime.UtcNow;
        var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-months + 1);

        var orders = await _db.Orders
            .Where(o => o.DocDate >= startDate && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.DocDate, o.DocTotal })
            .ToListAsync();

        var result = new List<RevenueEvolutionDto>();
        decimal? previousRevenue = null;

        for (int i = 0; i < months; i++)
        {
            var monthStart = startDate.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);
            var monthOrders = orders.Where(o => o.DocDate >= monthStart && o.DocDate < monthEnd).ToList();
            
            var revenue = monthOrders.Sum(o => o.DocTotal);
            var orderCount = monthOrders.Count;
            var avgOrderValue = orderCount > 0 ? revenue / orderCount : 0;
            
            decimal? growth = previousRevenue.HasValue && previousRevenue > 0
                ? ((revenue - previousRevenue.Value) / previousRevenue.Value) * 100
                : null;

            result.Add(new RevenueEvolutionDto
            {
                Period = monthStart.ToString("yyyy-MM"),
                Revenue = revenue,
                OrderCount = orderCount,
                AvgOrderValue = avgOrderValue,
                GrowthPercent = growth
            });

            previousRevenue = revenue;
        }

        return result;
    }

    private async Task<List<RevenueEvolutionDto>> GetDailyEvolutionAsync(int days)
    {
        var now = DateTime.UtcNow.Date;
        var startDate = now.AddDays(-days + 1);

        var orders = await _db.Orders
            .Where(o => o.DocDate >= startDate && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.DocDate, o.DocTotal })
            .ToListAsync();

        var result = new List<RevenueEvolutionDto>();
        decimal? previousRevenue = null;

        for (int i = 0; i < days; i++)
        {
            var day = startDate.AddDays(i);
            var dayOrders = orders.Where(o => o.DocDate.Date == day).ToList();
            
            var revenue = dayOrders.Sum(o => o.DocTotal);
            var orderCount = dayOrders.Count;
            var avgOrderValue = orderCount > 0 ? revenue / orderCount : 0;
            
            decimal? growth = previousRevenue.HasValue && previousRevenue > 0
                ? ((revenue - previousRevenue.Value) / previousRevenue.Value) * 100
                : null;

            result.Add(new RevenueEvolutionDto
            {
                Period = day.ToString("yyyy-MM-dd"),
                Revenue = revenue,
                OrderCount = orderCount,
                AvgOrderValue = avgOrderValue,
                GrowthPercent = growth
            });

            previousRevenue = revenue;
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Encaissements en attente
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PendingPaymentsReportDto> GetPendingPaymentsAsync(int? customerId = null)
    {
        var now = DateTime.UtcNow;

        // Commandes livrées ou confirmées
        var ordersQuery = _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped);

        if (customerId.HasValue)
            ordersQuery = ordersQuery.Where(o => o.CustomerId == customerId.Value);

        var orders = await ordersQuery.ToListAsync();
        var orderIds = orders.Select(o => o.Id).ToList();

        // Paiements effectués pour ces commandes
        var payments = await _db.Payments
            .Where(p => p.OrderId.HasValue && orderIds.Contains(p.OrderId.Value))
            .GroupBy(p => p.OrderId)
            .Select(g => new { OrderId = g.Key, TotalPaid = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.OrderId!.Value, x => x.TotalPaid);

        var pendingPayments = new List<PendingPaymentDto>();

        foreach (var order in orders)
        {
            var paidAmount = payments.GetValueOrDefault(order.Id, 0);
            var remaining = order.DocTotal - paidAmount;

            if (remaining > 0)
            {
                var daysOverdue = (int)(now - order.DocDate).TotalDays;
                pendingPayments.Add(new PendingPaymentDto
                {
                    OrderId = order.Id,
                    DocNum = order.DocNum,
                    CustomerId = order.CustomerId,
                    CustomerName = order.Customer.CardName,
                    CustomerCode = order.Customer.CardCode,
                    OrderTotal = order.DocTotal,
                    PaidAmount = paidAmount,
                    RemainingAmount = remaining,
                    OrderDate = order.DocDate,
                    DaysOverdue = daysOverdue > 30 ? daysOverdue - 30 : 0,  // Considérer en retard après 30 jours
                    Status = order.Status.ToString()
                });
            }
        }

        var sortedPayments = pendingPayments.OrderByDescending(p => p.RemainingAmount).ToList();
        var overduePayments = sortedPayments.Where(p => p.DaysOverdue > 0).ToList();

        return new PendingPaymentsReportDto
        {
            TotalPending = sortedPayments.Sum(p => p.RemainingAmount),
            TotalOrdersCount = sortedPayments.Count,
            OverdueAmount = overduePayments.Sum(p => p.RemainingAmount),
            OverdueCount = overduePayments.Count,
            PendingPayments = sortedPayments
        };
    }

    private async Task<decimal> GetPendingPaymentsTotalAsync()
    {
        var report = await GetPendingPaymentsAsync();
        return report.TotalPending;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Commandes en retard
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<LateOrdersReportDto> GetLateOrdersAsync(int? daysThreshold = null)
    {
        var now = DateTime.UtcNow;

        var query = _db.Orders
            .Include(o => o.Customer)
            .Where(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);

        var orders = await query.ToListAsync();

        var lateOrders = orders
            .Where(o => 
            {
                // Si date de livraison définie et dépassée
                if (o.DeliveryDate.HasValue && o.DeliveryDate < now)
                    return true;
                
                // Si pas de date de livraison, considérer en retard après 7 jours (paramétrable)
                var threshold = daysThreshold ?? 7;
                if (!o.DeliveryDate.HasValue && (now - o.DocDate).TotalDays > threshold)
                    return true;
                
                return false;
            })
            .Select(o => 
            {
                var expectedDate = o.DeliveryDate ?? o.DocDate.AddDays(daysThreshold ?? 7);
                var daysLate = (int)(now - expectedDate).TotalDays;
                
                return new LateOrderDto
                {
                    OrderId = o.Id,
                    DocNum = o.DocNum,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.CardName,
                    CustomerCode = o.Customer.CardCode,
                    Total = o.DocTotal,
                    OrderDate = o.DocDate,
                    DeliveryDate = o.DeliveryDate,
                    ExpectedDate = expectedDate,
                    DaysLate = daysLate > 0 ? daysLate : 0,
                    Status = o.Status.ToString()
                };
            })
            .Where(o => o.DaysLate > 0)
            .OrderByDescending(o => o.DaysLate)
            .ToList();

        return new LateOrdersReportDto
        {
            TotalLateOrders = lateOrders.Count,
            TotalLateAmount = lateOrders.Sum(o => o.Total),
            AvgDaysLate = lateOrders.Count > 0 ? (int)lateOrders.Average(o => o.DaysLate) : 0,
            LateOrders = lateOrders
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KPIs du jour
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<DashboardDto> GetDailyKPIsAsync()
    {
        var today = DateTime.UtcNow.Date;

        var totalCustomers = await _db.Customers.CountAsync();
        var totalOrders = await _db.Orders.CountAsync();
        var totalRevenue = await _db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => o.DocTotal);

        var pendingOrders = await _db.Orders
            .CountAsync(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Draft);

        var lowStockProducts = await _db.Products
            .CountAsync(p => p.Stock < 10 && p.IsActive);

        var recentOrders = await GetRecentOrdersAsync(5);
        var topProducts = await GetTopProductsAsync(5);

        return new DashboardDto
        {
            TotalCustomers = totalCustomers,
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            PendingOrders = pendingOrders,
            LowStockProducts = lowStockProducts,
            RecentOrders = recentOrders,
            TopProducts = topProducts
        };
    }

    private async Task<List<RecentOrderDto>> GetRecentOrdersAsync(int limit)
    {
        return await _db.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .Select(o => new RecentOrderDto
            {
                Id = o.Id,
                DocNum = o.DocNum,
                CustomerName = o.Customer.CardName,
                Total = o.DocTotal,
                Status = o.Status.ToString(),
                Date = o.DocDate
            })
            .ToListAsync();
    }
}
