using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class InvoiceService(AppDbContext db) : IInvoiceService
{
    public async Task<PagedResult<InvoiceDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = db.Invoices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.DocNum.Contains(search) || x.Customer.CardName.Contains(search) || (x.DeliveryNote != null && x.DeliveryNote.DocNum.Contains(search)));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var statusEnum))
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
        var deliveryIds = items.Where(i => i.DeliveryNoteId.HasValue).Select(i => i.DeliveryNoteId!.Value).Distinct().ToList();

        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var deliveryNotes = await db.DeliveryNotes.AsNoTracking().Where(d => deliveryIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var lines = await db.InvoiceLines.AsNoTracking().Include(l => l.Product).Where(l => ids.Contains(l.InvoiceId)).ToListAsync();
        var credits = await db.CreditNotes.AsNoTracking().Where(c => ids.Contains(c.InvoiceId)).ToListAsync();
        var lineMap = lines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.AsEnumerable());
        var creditMap = credits.GroupBy(c => c.InvoiceId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        var mapped = items.Select(i => Map(i, customers.GetValueOrDefault(i.CustomerId), i.DeliveryNoteId.HasValue ? deliveryNotes.GetValueOrDefault(i.DeliveryNoteId.Value) : null, lineMap.GetValueOrDefault(i.Id), creditMap.GetValueOrDefault(i.Id))).ToList();
        return new PagedResult<InvoiceDto>(mapped, total, page, pageSize);
    }

    public async Task<InvoiceDto?> GetByIdAsync(int id)
    {
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (invoice is null) return null;

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == invoice.CustomerId);
        var delivery = await db.DeliveryNotes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == invoice.DeliveryNoteId);
        var lines = await db.InvoiceLines.AsNoTracking().Include(l => l.Product).Where(l => l.InvoiceId == id).ToListAsync();
        var credits = await db.CreditNotes.AsNoTracking().Where(c => c.InvoiceId == id).ToListAsync();
        return Map(invoice, customer, delivery, lines, credits);
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto)
    {
        if (dto.DeliveryNoteId.HasValue)
        {
            var delivery = await db.DeliveryNotes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dto.DeliveryNoteId.Value)
                ?? throw new InvalidOperationException("BL introuvable.");

            if (delivery.CustomerId != dto.CustomerId)
                throw new InvalidOperationException("Le client de la facture doit correspondre au BL.");
        }

        var invoice = new Invoice
        {
            CustomerId = dto.CustomerId,
            CardCode = (await db.Customers.AsNoTracking().Where(c => c.Id == dto.CustomerId).Select(c => c.CardCode).FirstAsync()),
            DeliveryNoteId = dto.DeliveryNoteId,
            DocNum = $"FAC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            DocDate = DateTime.UtcNow,
            DueDate = dto.DueDate,
            Currency = dto.Currency,
            Status = InvoiceStatus.Unpaid,
            BaseType = dto.DeliveryNoteId.HasValue ? DocumentBaseType.Delivery : null,
            BaseEntry = dto.DeliveryNoteId,
            BaseLine = null,
            Comments = dto.Comments,
            CreatedAt = DateTime.UtcNow
        };

        var productCodes = await db.Products.AsNoTracking().Where(p => dto.Lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ItemCode);
        AddLines(invoice, dto.Lines, productCodes, null);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return (await GetByIdAsync(invoice.Id))!;
    }

    public async Task<InvoiceDto> CreateFromDeliveryNoteAsync(int deliveryNoteId)
    {
        var delivery = await db.DeliveryNotes.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == deliveryNoteId)
            ?? throw new InvalidOperationException("BL introuvable.");

        if (delivery.Lines.Count == 0)
            throw new InvalidOperationException("Impossible de générer une facture sans lignes.");

        var customerCardCode = await db.Customers.AsNoTracking().Where(c => c.Id == delivery.CustomerId).Select(c => c.CardCode).FirstAsync();

        var invoice = new Invoice
        {
            CustomerId = delivery.CustomerId,
            CardCode = customerCardCode,
            DeliveryNoteId = delivery.Id,
            DocNum = $"FAC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            DocDate = DateTime.UtcNow,
            Currency = "EUR",
            Status = InvoiceStatus.Unpaid,
            BaseType = DocumentBaseType.Delivery,
            BaseEntry = delivery.Id,
            BaseLine = null,
            Comments = $"Générée depuis BL {delivery.DocNum}",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var sourceLine in delivery.Lines.OrderBy(l => l.LineNum))
        {
            invoice.Lines.Add(new InvoiceLine
            {
                ProductId = sourceLine.ProductId,
                ItemCode = sourceLine.ItemCode,
                LineNum = sourceLine.LineNum,
                Quantity = sourceLine.Quantity,
                Price = sourceLine.Price,
                UnitPrice = sourceLine.UnitPrice,
                VatPct = sourceLine.VatPct,
                LineTotal = sourceLine.LineTotal,
                BaseEntry = delivery.Id,
                BaseLine = sourceLine.LineNum
            });
        }

        invoice.VatTotal = Math.Round(invoice.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 4);
        invoice.DocTotal = Math.Round(invoice.Lines.Sum(l => l.LineTotal), 4);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return (await GetByIdAsync(invoice.Id))!;
    }

    public async Task<InvoiceDto?> UpdateAsync(int id, CreateInvoiceDto dto)
    {
        var invoice = await db.Invoices.AsTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (invoice is null) return null;
        if (invoice.Status != InvoiceStatus.Unpaid)
            throw new InvalidOperationException("Seule une facture non payée peut être modifiée.");

        invoice.CustomerId = dto.CustomerId;
        invoice.CardCode = await db.Customers.AsNoTracking().Where(c => c.Id == dto.CustomerId).Select(c => c.CardCode).FirstAsync();
        invoice.DeliveryNoteId = dto.DeliveryNoteId;
        invoice.DueDate = dto.DueDate;
        invoice.Currency = dto.Currency;
        invoice.Comments = dto.Comments;
        invoice.UpdatedAt = DateTime.UtcNow;

        db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();
        var productCodes = await db.Products.AsNoTracking().Where(p => dto.Lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ItemCode);
        AddLines(invoice, dto.Lines, productCodes, null);

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<InvoiceDto?> UpdateStatusAsync(int id, UpdateInvoiceStatusDto dto)
    {
        var invoice = await db.Invoices.AsTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null) return null;

        var statusRaw = dto.Status?.Trim().ToLowerInvariant();
        var statusParsed = Enum.TryParse<InvoiceStatus>(dto.Status, true, out var parsed)
            ? parsed
            : statusRaw switch
            {
                "non paye" or "non payé" => InvoiceStatus.Unpaid,
                "paye" or "payé" => InvoiceStatus.Paid,
                _ => (InvoiceStatus?)null
            };

        if (!statusParsed.HasValue)
            throw new InvalidOperationException("Statut de facture invalide.");

        var newStatus = statusParsed.Value;

        if (!DocumentStatusTransitions.CanTransition(invoice.Status, newStatus))
            throw new InvalidOperationException($"Transition non autorisée: {invoice.Status} -> {newStatus}");

        invoice.Status = newStatus;
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var invoice = await db.Invoices.FindAsync(id);
        if (invoice is null) return false;
        if (invoice.Status != InvoiceStatus.Unpaid)
            throw new InvalidOperationException("Seule une facture non payée peut être supprimée.");

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync();
        return true;
    }

    private static void AddLines(Invoice invoice, IEnumerable<CreateInvoiceLineDto> lines, IReadOnlyDictionary<int, string> productCodes, int? baseEntry)
    {
        var index = 1;
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice * (1 + line.VatPct / 100);
            productCodes.TryGetValue(line.ProductId, out var itemCode);

            invoice.Lines.Add(new InvoiceLine
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

        invoice.VatTotal = Math.Round(invoice.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 4);
        invoice.DocTotal = Math.Round(invoice.Lines.Sum(l => l.LineTotal), 4);
    }

    private static InvoiceDto Map(Invoice i, Customer? c, DeliveryNote? d, IEnumerable<InvoiceLine>? lines, IEnumerable<CreditNote>? credits) => new()
    {
        Id = i.Id,
        DocEntry = i.Id,
        DocNum = i.DocNum,
        CardCode = i.CardCode,
        CustomerId = i.CustomerId,
        CustomerName = c?.CardName ?? string.Empty,
        CustomerCode = c?.CardCode ?? string.Empty,
        DeliveryNoteId = i.DeliveryNoteId,
        DeliveryDocNum = d?.DocNum ?? string.Empty,
        DocDate = i.DocDate,
        DueDate = i.DueDate,
        Status = i.Status.ToString(),
        DocTotal = i.DocTotal,
        VatTotal = i.VatTotal,
        BaseType = i.BaseType?.ToString(),
        BaseEntry = i.BaseEntry,
        BaseLine = i.BaseLine,
        Currency = i.Currency,
        Comments = i.Comments,
        CreatedAt = i.CreatedAt,
        Lines = lines?.Select(l => new InvoiceLineDto
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
        }).ToList() ?? [],
        CreditNotes = credits?.Select(cn => new CreditNoteSummaryDto
        {
            Id = cn.Id,
            DocNum = cn.DocNum,
            Amount = cn.Amount,
            Reason = cn.Reason,
            DocDate = cn.DocDate
        }).ToList() ?? []
    };
}
