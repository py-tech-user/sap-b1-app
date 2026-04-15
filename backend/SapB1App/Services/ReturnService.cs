using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class ReturnService(AppDbContext db, ICreditNoteService creditNoteService) : IReturnService
{
    public async Task<PagedResult<ReturnDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = db.Returns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.ReturnNumber.Contains(search) || x.Customer.CardName.Contains(search) || x.Reason.Contains(search));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReturnStatus>(status, true, out var statusEnum))
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
        var deliveryIds = items.Select(i => i.DeliveryNoteId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var deliveryNotes = await db.DeliveryNotes.AsNoTracking().Where(d => deliveryIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var lines = await db.ReturnLines.AsNoTracking().Include(l => l.Product).Where(l => ids.Contains(l.ReturnId)).ToListAsync();
        var lineMap = lines.GroupBy(l => l.ReturnId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        var mapped = items.Select(i => Map(i, customers.GetValueOrDefault(i.CustomerId), deliveryNotes.GetValueOrDefault(i.DeliveryNoteId), lineMap.GetValueOrDefault(i.Id))).ToList();
        return new PagedResult<ReturnDto>(mapped, total, page, pageSize);
    }

    public async Task<ReturnDto?> GetByIdAsync(int id)
    {
        var ret = await db.Returns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (ret is null) return null;

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ret.CustomerId);
        var delivery = await db.DeliveryNotes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == ret.DeliveryNoteId);
        var lines = await db.ReturnLines.AsNoTracking().Include(l => l.Product).Where(l => l.ReturnId == id).ToListAsync();

        return Map(ret, customer, delivery, lines);
    }

    public async Task<ReturnDto> CreateAsync(CreateReturnDto dto)
    {
        var delivery = await db.DeliveryNotes.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dto.DeliveryNoteId)
            ?? throw new InvalidOperationException("BL introuvable.");

        if (delivery.CustomerId != dto.CustomerId)
            throw new InvalidOperationException("Le client du retour doit correspondre au BL.");

        var ret = new Return
        {
            CustomerId = dto.CustomerId,
            DeliveryNoteId = dto.DeliveryNoteId,
            ReturnNumber = $"RET-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Status = ReturnStatus.Pending,
            Reason = dto.Reason,
            DocDate = dto.DocDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        AddLines(ret, dto.Lines);
        db.Returns.Add(ret);
        await db.SaveChangesAsync();

        return (await GetByIdAsync(ret.Id))!;
    }

    public async Task<ReturnDto?> UpdateAsync(int id, CreateReturnDto dto)
    {
        var ret = await db.Returns.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (ret is null) return null;
        if (ret.Status != ReturnStatus.Pending)
            throw new InvalidOperationException("Seul un retour en attente peut être modifié.");

        ret.CustomerId = dto.CustomerId;
        ret.DeliveryNoteId = dto.DeliveryNoteId;
        ret.Reason = dto.Reason;
        ret.DocDate = dto.DocDate ?? ret.DocDate;
        ret.UpdatedAt = DateTime.UtcNow;

        db.ReturnLines.RemoveRange(ret.Lines);
        ret.Lines.Clear();
        AddLines(ret, dto.Lines);

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ReturnDto?> UpdateStatusAsync(int id, UpdateReturnStatusDto dto)
    {
        var ret = await db.Returns.FindAsync(id);
        if (ret is null) return null;

        if (!Enum.TryParse<ReturnStatus>(dto.Status, true, out var newStatus))
            throw new InvalidOperationException("Statut de retour invalide.");

        if (!DocumentStatusTransitions.CanTransition(ret.Status, newStatus))
            throw new InvalidOperationException($"Transition non autorisée: {ret.Status} -> {newStatus}");

        ret.Status = newStatus;
        ret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        if (newStatus == ReturnStatus.Validated && !ret.CreditNoteId.HasValue)
        {
            var invoiceId = await db.Invoices
                .Where(i => i.DeliveryNoteId == ret.DeliveryNoteId)
                .Select(i => i.Id)
                .FirstOrDefaultAsync();

            if (invoiceId != 0)
            {
                var amount = await db.ReturnLines
                    .Where(l => l.ReturnId == ret.Id)
                    .SumAsync(l => l.LineTotal);

                var credit = await creditNoteService.CreateAsync(new CreateCreditNoteDto
                {
                    InvoiceId = invoiceId,
                    ReturnId = ret.Id,
                    Amount = amount,
                    Reason = $"Avoir généré depuis retour {ret.ReturnNumber}",
                    DocDate = DateTime.UtcNow
                });

                ret.CreditNoteId = credit.Id;
                ret.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ret = await db.Returns.FindAsync(id);
        if (ret is null) return false;
        if (ret.Status != ReturnStatus.Pending)
            throw new InvalidOperationException("Seul un retour en attente peut être supprimé.");

        db.Returns.Remove(ret);
        await db.SaveChangesAsync();
        return true;
    }

    private static void AddLines(Return ret, IEnumerable<CreateReturnLineDto> lines)
    {
        var index = 1;
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice * (1 + line.VatPct / 100);
            ret.Lines.Add(new ReturnLine
            {
                ProductId = line.ProductId,
                LineNum = index++,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatPct = line.VatPct,
                LineTotal = Math.Round(lineTotal, 4)
            });
        }
    }

    private static ReturnDto Map(Return r, Customer? c, DeliveryNote? d, IEnumerable<ReturnLine>? lines) => new()
    {
        Id = r.Id,
        ReturnNumber = r.ReturnNumber,
        CustomerId = r.CustomerId,
        CustomerName = c?.CardName ?? string.Empty,
        CustomerCode = c?.CardCode ?? string.Empty,
        DeliveryNoteId = r.DeliveryNoteId,
        DeliveryDocNum = d?.DocNum ?? string.Empty,
        Status = r.Status.ToString(),
        Reason = r.Reason,
        DocDate = r.DocDate,
        CreatedAt = r.CreatedAt,
        CreditNoteId = r.CreditNoteId,
        Lines = lines?.Select(l => new ReturnLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ItemCode = l.Product?.ItemCode ?? string.Empty,
            ItemName = l.Product?.ItemName ?? string.Empty,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            VatPct = l.VatPct,
            LineTotal = l.LineTotal
        }).ToList() ?? []
    };
}
