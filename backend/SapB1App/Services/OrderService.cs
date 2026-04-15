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
            .AsNoTracking()
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

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Charger les customers séparément (optimisation)
        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customers = await db.Customers
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        // Charger les lignes de commande séparément
        var orderIds = orders.Select(o => o.Id).ToList();
        var lines = await db.OrderLines
            .AsNoTracking()
            .Include(l => l.Product)
            .Where(l => orderIds.Contains(l.OrderId))
            .ToListAsync();

        var linesByOrderId = lines
            .GroupBy(l => l.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = orders.Select(o => MapToDto(
            o, 
            customers.GetValueOrDefault(o.CustomerId),
            linesByOrderId.GetValueOrDefault(o.Id, new List<OrderLine>())
        )).ToList();

        return new PagedResult<OrderDto>(items, total, page, pageSize);
    }

    public async Task<OrderDto?> GetByIdAsync(int id)
    {
        var order = await db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return null;

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == order.CustomerId);

        var lines = await db.OrderLines
            .AsNoTracking()
            .Include(l => l.Product)
            .Where(l => l.OrderId == id)
            .ToListAsync();

        return MapToDto(order, customer, lines);
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
            CardCode     = customer.CardCode,
            CustomerId   = dto.CustomerId,
            DocDate      = DateTime.UtcNow,
            DeliveryDate = dto.DeliveryDate,
            Status       = OrderStatus.Draft,
            BaseType     = null,
            BaseEntry    = null,
            BaseLine     = null,
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
                ItemCode  = product.ItemCode,
                LineNum   = lineNum++,
                Quantity  = lineDto.Quantity,
                Price     = lineDto.UnitPrice,
                UnitPrice = lineDto.UnitPrice,
                VatPct    = lineDto.VatPct,
                LineTotal = Math.Round(lineTotal, 2),
                BaseEntry = null,
                BaseLine  = null,
                Product   = product
            });
        }

        // Calculate totals
        order.VatTotal = Math.Round(order.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 2);
        order.DocTotal = Math.Round(order.Lines.Sum(l => l.LineTotal), 2);

        var sapResult = await sapService.CreateSalesOrderAsync(order, customer, order.Lines);
        if (!sapResult.Success)
        {
            throw new InvalidOperationException(sapResult.ErrorMessage ?? "Erreur Service Layer lors de la création de la commande.");
        }

        if (sapResult.Response is { } response &&
            response.TryGetProperty("DocEntry", out var docEntry) &&
            docEntry.TryGetInt32(out var sapDocNum))
        {
            order.SapDocNum = sapDocNum;
            order.SyncedToSap = true;
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Reload with includes
        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<OrderDto> CreateFromQuoteAsync(int quoteId)
    {
        var quote = await db.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == quoteId)
            ?? throw new InvalidOperationException($"Devis ID {quoteId} introuvable.");

        if (quote.Lines.Count == 0)
            throw new InvalidOperationException("Le devis ne contient aucune ligne.");

        var order = new Order
        {
            DocNum = $"CMD-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            CardCode = quote.CardCode,
            CustomerId = quote.CustomerId,
            DocDate = DateTime.UtcNow,
            Status = OrderStatus.Draft,
            BaseType = DocumentBaseType.Quotation,
            BaseEntry = quote.Id,
            BaseLine = null,
            Currency = quote.Currency,
            Comments = $"Généré depuis devis {quote.DocNum}",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var sourceLine in quote.Lines.OrderBy(l => l.LineNum))
        {
            order.Lines.Add(new OrderLine
            {
                ProductId = sourceLine.ProductId,
                ItemCode = sourceLine.ItemCode,
                LineNum = sourceLine.LineNum,
                Quantity = sourceLine.Quantity,
                Price = sourceLine.Price,
                UnitPrice = sourceLine.UnitPrice,
                VatPct = sourceLine.VatPct,
                LineTotal = sourceLine.LineTotal,
                BaseEntry = quote.Id,
                BaseLine = sourceLine.LineNum
            });
        }

        order.VatTotal = Math.Round(order.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 2);
        order.DocTotal = Math.Round(order.Lines.Sum(l => l.LineTotal), 2);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<OrderDto?> UpdateStatusAsync(int id, UpdateOrderStatusDto dto)
    {
        var order = await db.Orders.AsTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order is null) return null;

        if (!Enum.TryParse<OrderStatus>(dto.Status, out var newStatus))
            throw new InvalidOperationException($"Statut invalide: {dto.Status}");

        var canTransition = order.Status switch
        {
            OrderStatus.Draft => newStatus is OrderStatus.Confirmed or OrderStatus.Cancelled,
            OrderStatus.Confirmed => newStatus is OrderStatus.Shipped or OrderStatus.Cancelled,
            OrderStatus.Shipped => newStatus == OrderStatus.Delivered,
            OrderStatus.Delivered => false,
            OrderStatus.Cancelled => false,
            _ => false
        };

        if (!canTransition)
            throw new InvalidOperationException($"Transition non autorisée: {order.Status} -> {newStatus}");

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

    private static OrderDto MapToDto(Order o, Customer? c = null, IEnumerable<OrderLine> lines = null) => new()
    {
        Id            = o.Id,
        DocEntry      = o.Id,
        DocNum        = o.DocNum,
        CardCode      = o.CardCode,
        CustomerId    = o.CustomerId,
        CustomerName  = c?.CardName ?? string.Empty,
        CustomerCode  = c?.CardCode ?? string.Empty,
        DocDate       = o.DocDate,
        DeliveryDate  = o.DeliveryDate,
        Status        = o.Status.ToString(),
        DocTotal      = o.DocTotal,
        VatTotal      = o.VatTotal,
        BaseType      = o.BaseType?.ToString(),
        BaseEntry     = o.BaseEntry,
        BaseLine      = o.BaseLine,
        Currency      = o.Currency,
        Comments      = o.Comments,
        SyncedToSap   = o.SyncedToSap,
        SapDocNum     = o.SapDocNum,
        CreatedAt     = o.CreatedAt,
        Lines         = lines?.Select(l => new OrderLineDto
        {
            Id        = l.Id,
            ProductId = l.ProductId,
            ItemCode  = l.ItemCode,
            ItemName  = l.Product?.ItemName ?? string.Empty,
            Quantity  = l.Quantity,
            Price     = l.Price,
            UnitPrice = l.UnitPrice,
            VatPct    = l.VatPct,
            LineTotal = l.LineTotal,
            BaseEntry = l.BaseEntry,
            BaseLine  = l.BaseLine
        }).ToList() ?? []
    };
}
