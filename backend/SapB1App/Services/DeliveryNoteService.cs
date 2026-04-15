using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class DeliveryNoteService(AppDbContext db) : IDeliveryNoteService
{
    public async Task<PagedResult<DeliveryNoteDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = db.DeliveryNotes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.DocNum.Contains(search) || x.Customer.CardName.Contains(search) || (x.Order != null && x.Order.DocNum.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeliveryNoteStatus>(status, true, out var statusEnum))
            query = query.Where(x => x.Status == statusEnum);

        if (customerId.HasValue)
            query = query.Where(x => x.CustomerId == customerId.Value);

        if (dateFrom.HasValue)
            query = query.Where(x => x.DocDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.DocDate <= dateTo.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var ids = items.Select(i => i.Id).ToList();
        var customerIds = items.Select(i => i.CustomerId).Distinct().ToList();
        var orderIds = items.Where(i => i.OrderId.HasValue).Select(i => i.OrderId!.Value).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var orders = await db.Orders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
        var lines = await db.DeliveryNoteLines.AsNoTracking().Include(l => l.Product).Where(l => ids.Contains(l.DeliveryNoteId)).ToListAsync();
        var lineMap = lines.GroupBy(l => l.DeliveryNoteId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        var mapped = items.Select(i => Map(i, customers.GetValueOrDefault(i.CustomerId), i.OrderId.HasValue ? orders.GetValueOrDefault(i.OrderId.Value) : null, lineMap.GetValueOrDefault(i.Id))).ToList();
        return new PagedResult<DeliveryNoteDto>(mapped, total, page, pageSize);
    }

    public async Task<DeliveryNoteDto?> GetByIdAsync(int id)
    {
        var note = await db.DeliveryNotes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (note is null) return null;

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == note.CustomerId);
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == note.OrderId);
        var lines = await db.DeliveryNoteLines.AsNoTracking().Include(l => l.Product).Where(l => l.DeliveryNoteId == id).ToListAsync();

        return Map(note, customer, order, lines);
    }

    public async Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto dto)
    {
        if (dto.OrderId.HasValue)
        {
            var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == dto.OrderId.Value)
                ?? throw new InvalidOperationException("BC introuvable.");

            if (order.CustomerId != dto.CustomerId)
                throw new InvalidOperationException("Le client du BL doit correspondre au BC.");
        }

        var note = new DeliveryNote
        {
            CustomerId = dto.CustomerId,
            CardCode = (await db.Customers.AsNoTracking().Where(c => c.Id == dto.CustomerId).Select(c => c.CardCode).FirstAsync()),
            OrderId = dto.OrderId,
            DocNum = $"BL-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            DocDate = DateTime.UtcNow,
            DeliveryDate = dto.DeliveryDate,
            Status = DeliveryNoteStatus.InProgress,
            BaseType = dto.OrderId.HasValue ? DocumentBaseType.SalesOrder : null,
            BaseEntry = dto.OrderId,
            BaseLine = null,
            Signature = dto.Signature,
            Comments = dto.Comments,
            CreatedAt = DateTime.UtcNow
        };

        var productCodes = await db.Products.AsNoTracking().Where(p => dto.Lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ItemCode);
        AddLines(note, dto.Lines, productCodes, null);
        db.DeliveryNotes.Add(note);
        await db.SaveChangesAsync();

        return (await GetByIdAsync(note.Id))!;
    }

    public async Task<DeliveryNoteDto> CreateFromOrderAsync(int orderId)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new InvalidOperationException("BC introuvable.");

        if (order.Lines.Count == 0)
            throw new InvalidOperationException("Impossible de générer un BL sans lignes.");

        var customerCardCode = await db.Customers.AsNoTracking().Where(c => c.Id == order.CustomerId).Select(c => c.CardCode).FirstAsync();

        var note = new DeliveryNote
        {
            CustomerId = order.CustomerId,
            CardCode = customerCardCode,
            OrderId = order.Id,
            DocNum = $"BL-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            DocDate = DateTime.UtcNow,
            DeliveryDate = order.DeliveryDate,
            Status = DeliveryNoteStatus.InProgress,
            BaseType = DocumentBaseType.SalesOrder,
            BaseEntry = order.Id,
            BaseLine = null,
            Comments = $"Généré depuis BC {order.DocNum}",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var sourceLine in order.Lines.OrderBy(l => l.LineNum))
        {
            note.Lines.Add(new DeliveryNoteLine
            {
                ProductId = sourceLine.ProductId,
                OrderLineId = sourceLine.Id,
                ItemCode = sourceLine.ItemCode,
                LineNum = sourceLine.LineNum,
                Quantity = sourceLine.Quantity,
                Price = sourceLine.Price,
                UnitPrice = sourceLine.UnitPrice,
                VatPct = sourceLine.VatPct,
                LineTotal = sourceLine.LineTotal,
                BaseEntry = order.Id,
                BaseLine = sourceLine.LineNum
            });
        }

        note.VatTotal = Math.Round(note.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 4);
        note.DocTotal = Math.Round(note.Lines.Sum(l => l.LineTotal), 4);

        db.DeliveryNotes.Add(note);
        await db.SaveChangesAsync();
        return (await GetByIdAsync(note.Id))!;
    }

    public async Task<DeliveryNoteDto?> UpdateAsync(int id, CreateDeliveryNoteDto dto)
    {
        var note = await db.DeliveryNotes.AsTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (note is null) return null;
        if (note.Status != DeliveryNoteStatus.InProgress)
            throw new InvalidOperationException("Seul un BL en cours peut être modifié.");

        note.CustomerId = dto.CustomerId;
        note.CardCode = await db.Customers.AsNoTracking().Where(c => c.Id == dto.CustomerId).Select(c => c.CardCode).FirstAsync();
        note.OrderId = dto.OrderId;
        note.DeliveryDate = dto.DeliveryDate;
        note.Comments = dto.Comments;
        note.Signature = dto.Signature;
        note.UpdatedAt = DateTime.UtcNow;

        db.DeliveryNoteLines.RemoveRange(note.Lines);
        note.Lines.Clear();
        var productCodes = await db.Products.AsNoTracking().Where(p => dto.Lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ItemCode);
        AddLines(note, dto.Lines, productCodes, null);

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<DeliveryNoteDto?> UpdateStatusAsync(int id, UpdateDeliveryNoteStatusDto dto)
    {
        var note = await db.DeliveryNotes.AsTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (note is null) return null;

        var statusRaw = dto.Status?.Trim().ToLowerInvariant();
        var statusParsed = Enum.TryParse<DeliveryNoteStatus>(dto.Status, true, out var parsed)
            ? parsed
            : statusRaw switch
            {
                "en cours" => DeliveryNoteStatus.InProgress,
                "livre" or "livré" => DeliveryNoteStatus.Delivered,
                _ => (DeliveryNoteStatus?)null
            };

        if (!statusParsed.HasValue)
            throw new InvalidOperationException("Statut BL invalide.");

        var newStatus = statusParsed.Value;

        if (!DocumentStatusTransitions.CanTransition(note.Status, newStatus))
            throw new InvalidOperationException($"Transition non autorisée: {note.Status} -> {newStatus}");

        note.Status = newStatus;
        note.Signature = dto.Signature ?? note.Signature;
        note.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var note = await db.DeliveryNotes.FindAsync(id);
        if (note is null) return false;
        if (note.Status != DeliveryNoteStatus.InProgress)
            throw new InvalidOperationException("Seul un BL en cours peut être supprimé.");

        db.DeliveryNotes.Remove(note);
        await db.SaveChangesAsync();
        return true;
    }

    private static void AddLines(DeliveryNote note, IEnumerable<CreateDeliveryNoteLineDto> lines, IReadOnlyDictionary<int, string> productCodes, int? baseEntry)
    {
        var index = 1;
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice * (1 + line.VatPct / 100);
            productCodes.TryGetValue(line.ProductId, out var itemCode);

            note.Lines.Add(new DeliveryNoteLine
            {
                ProductId = line.ProductId,
                ItemCode = itemCode ?? string.Empty,
                LineNum = index++,
                Quantity = line.Quantity,
                Price = line.UnitPrice,
                UnitPrice = line.UnitPrice,
                VatPct = line.VatPct,
                LineTotal = Math.Round(lineTotal, 4),
                BaseEntry = baseEntry,
                BaseLine = null
            });
        }

        note.VatTotal = Math.Round(note.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 4);
        note.DocTotal = Math.Round(note.Lines.Sum(l => l.LineTotal), 4);
    }

    private static DeliveryNoteDto Map(DeliveryNote dn, Customer? c, Order? o, IEnumerable<DeliveryNoteLine>? lines) => new()
    {
        Id = dn.Id,
        DocEntry = dn.Id,
        DocNum = dn.DocNum,
        CardCode = dn.CardCode,
        CustomerId = dn.CustomerId,
        CustomerName = c?.CardName ?? string.Empty,
        CustomerCode = c?.CardCode ?? string.Empty,
        OrderId = dn.OrderId,
        OrderDocNum = o?.DocNum ?? string.Empty,
        DocDate = dn.DocDate,
        DeliveryDate = dn.DeliveryDate,
        Status = dn.Status.ToString(),
        Signature = dn.Signature,
        DocTotal = dn.DocTotal,
        VatTotal = dn.VatTotal,
        BaseType = dn.BaseType?.ToString(),
        BaseEntry = dn.BaseEntry,
        BaseLine = dn.BaseLine,
        Comments = dn.Comments,
        CreatedAt = dn.CreatedAt,
        Lines = lines?.Select(l => new DeliveryNoteLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ItemCode = l.ItemCode,
            ItemName = l.Product?.ItemName ?? string.Empty,
            Quantity = l.Quantity,
            Price = l.Price,
            UnitPrice = l.UnitPrice,
            VatPct = l.VatPct,
            LineTotal = l.LineTotal,
            BaseEntry = l.BaseEntry,
            BaseLine = l.BaseLine
        }).ToList() ?? []
    };
}
