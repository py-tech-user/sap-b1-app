using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class OrderService(AppDbContext db, ISapB1Service sapService) : IOrderService
{
    public async Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId)
    {
        var query = db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o =>
                o.DocNum.Contains(search) ||
                o.Customer.CardName.Contains(search));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var statusEnum))
            query = query.Where(o => o.Status == statusEnum);

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto>(items, total, page, pageSize);
    }

    public async Task<OrderDto?> GetByIdAsync(int id)
    {
        var order = await db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order is null ? null : MapToDto(order);
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto)
    {
        var customer = await db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Client ID {dto.CustomerId} introuvable.");

        // Build order number: CMD-YYYYMMDD-XXXX
        var docNum = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";

        var order = new Order
        {
            DocNum       = docNum,
            CustomerId   = dto.CustomerId,
            DocDate      = DateTime.UtcNow,
            DeliveryDate = dto.DeliveryDate,
            Status       = OrderStatus.Draft,
            Currency     = dto.Currency,
            Comments     = dto.Comments,
            CreatedAt    = DateTime.UtcNow
        };

        // Add lines
        var lineNum = 1;
        foreach (var lineDto in dto.Lines)
        {
            var product = await db.Products.FindAsync(lineDto.ProductId)
                ?? throw new InvalidOperationException($"Produit ID {lineDto.ProductId} introuvable.");

            var lineTotal = lineDto.Quantity * lineDto.UnitPrice * (1 + lineDto.VatPct / 100);

            order.Lines.Add(new OrderLine
            {
                ProductId = lineDto.ProductId,
                LineNum   = lineNum++,
                Quantity  = lineDto.Quantity,
                UnitPrice = lineDto.UnitPrice,
                VatPct    = lineDto.VatPct,
                LineTotal = Math.Round(lineTotal, 2)
            });
        }

        // Calculate totals
        order.VatTotal = Math.Round(order.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 2);
        order.DocTotal = Math.Round(order.Lines.Sum(l => l.LineTotal), 2);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Reload with includes
        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<OrderDto?> UpdateStatusAsync(int id, UpdateOrderStatusDto dto)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return null;

        if (!Enum.TryParse<OrderStatus>(dto.Status, out var newStatus))
            throw new InvalidOperationException($"Statut invalide: {dto.Status}");

        order.Status    = newStatus;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return false;

        if (order.Status != OrderStatus.Draft)
            throw new InvalidOperationException("Seules les commandes en brouillon peuvent être supprimées.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<OrderDto?> SyncToSapAsync(int id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null) return null;

        await sapService.SyncOrderAsync(id);

        order.SyncedToSap = true;
        order.SapDocNum   = new Random().Next(10000, 99999);
        order.UpdatedAt   = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    private static OrderDto MapToDto(Order o) => new()
    {
        Id            = o.Id,
        DocNum        = o.DocNum,
        CustomerId    = o.CustomerId,
        CustomerName  = o.Customer?.CardName ?? string.Empty,
        CustomerCode  = o.Customer?.CardCode ?? string.Empty,
        DocDate       = o.DocDate,
        DeliveryDate  = o.DeliveryDate,
        Status        = o.Status.ToString(),
        DocTotal      = o.DocTotal,
        VatTotal      = o.VatTotal,
        Currency      = o.Currency,
        Comments      = o.Comments,
        SyncedToSap   = o.SyncedToSap,
        SapDocNum     = o.SapDocNum,
        CreatedAt     = o.CreatedAt,
        Lines         = o.Lines?.Select(l => new OrderLineDto
        {
            Id        = l.Id,
            ProductId = l.ProductId,
            ItemCode  = l.Product?.ItemCode ?? string.Empty,
            ItemName  = l.Product?.ItemName ?? string.Empty,
            Quantity  = l.Quantity,
            UnitPrice = l.UnitPrice,
            VatPct    = l.VatPct,
            LineTotal = l.LineTotal
        }).ToList() ?? []
    };
}
