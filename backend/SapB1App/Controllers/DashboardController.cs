using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Retourne les KPIs et données du tableau de bord.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> Get()
    {
        var totalCustomers = await _db.Customers
            .CountAsync(c => c.IsActive);

        var totalOrders = await _db.Orders.CountAsync();

        var totalRevenue = await _db.Orders
            .SumAsync(o => (decimal?)o.DocTotal) ?? 0;

        var pendingOrders = await _db.Orders
            .CountAsync(o =>
                o.Status == OrderStatus.Draft ||
                o.Status == OrderStatus.Confirmed);

        var lowStockProducts = await _db.Products
            .CountAsync(p => p.IsActive && p.Stock < 10);

        var recentOrders = await _db.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new RecentOrderDto
            {
                Id           = o.Id,
                DocNum       = o.DocNum,
                CustomerName = o.Customer.CardName,
                Total        = o.DocTotal,
                Status       = o.Status.ToString(),
                Date         = o.DocDate
            })
            .ToListAsync();

        var topProducts = await _db.OrderLines
            .Include(l => l.Product)
            .GroupBy(l => new
            {
                l.ProductId,
                l.Product.ItemCode,
                l.Product.ItemName
            })
            .Select(g => new TopProductDto
            {
                ItemCode      = g.Key.ItemCode,
                ItemName      = g.Key.ItemName,
                TotalQuantity = g.Sum(l => l.Quantity),
                TotalRevenue  = g.Sum(l => l.LineTotal)
            })
            .OrderByDescending(p => p.TotalRevenue)
            .Take(5)
            .ToListAsync();

        var dashboard = new DashboardDto
        {
            TotalCustomers   = totalCustomers,
            TotalOrders      = totalOrders,
            TotalRevenue     = totalRevenue,
            PendingOrders    = pendingOrders,
            LowStockProducts = lowStockProducts,
            RecentOrders     = recentOrders,
            TopProducts      = topProducts
        };

        return Ok(new ApiResponse<DashboardDto>(true, null, dashboard));
    }
}
