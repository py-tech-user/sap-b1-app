using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class CreditNoteService(AppDbContext db) : ICreditNoteService
{
    public async Task<PagedResult<CreditNoteDto>> GetAllAsync(int page, int pageSize, string? search, int? invoiceId, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = db.CreditNotes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.DocNum.Contains(search) || x.Reason.Contains(search) || x.Invoice.DocNum.Contains(search));

        if (invoiceId.HasValue)
            query = query.Where(x => x.InvoiceId == invoiceId.Value);

        if (dateFrom.HasValue)
            query = query.Where(x => x.DocDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(x => x.DocDate <= dateTo.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var invoiceIds = items.Select(i => i.InvoiceId).Distinct().ToList();
        var invoices = await db.Invoices.AsNoTracking().Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        var mapped = items.Select(i => Map(i, invoices.GetValueOrDefault(i.InvoiceId))).ToList();
        return new PagedResult<CreditNoteDto>(mapped, total, page, pageSize);
    }

    public async Task<CreditNoteDto?> GetByIdAsync(int id)
    {
        var credit = await db.CreditNotes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (credit is null) return null;

        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == credit.InvoiceId);
        return Map(credit, invoice);
    }

    public async Task<CreditNoteDto> CreateAsync(CreateCreditNoteDto dto)
    {
        var invoice = await db.Invoices.FindAsync(dto.InvoiceId)
            ?? throw new InvalidOperationException("Facture introuvable.");

        if (dto.ReturnId.HasValue)
        {
            var ret = await db.Returns.FindAsync(dto.ReturnId.Value)
                ?? throw new InvalidOperationException("Retour introuvable.");

            if (ret.CreditNoteId.HasValue)
                throw new InvalidOperationException("Un avoir existe déjà pour ce retour.");
        }

        var credit = new CreditNote
        {
            DocNum = $"AV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            InvoiceId = dto.InvoiceId,
            ReturnId = dto.ReturnId,
            Amount = dto.Amount,
            Reason = dto.Reason,
            DocDate = dto.DocDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.CreditNotes.Add(credit);
        await db.SaveChangesAsync();

        if (dto.ReturnId.HasValue)
        {
            var ret = await db.Returns.FindAsync(dto.ReturnId.Value);
            if (ret is not null)
            {
                ret.CreditNoteId = credit.Id;
                ret.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        return (await GetByIdAsync(credit.Id))!;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var credit = await db.CreditNotes.FindAsync(id);
        if (credit is null) return false;

        var linkedReturn = await db.Returns.FirstOrDefaultAsync(r => r.CreditNoteId == id);
        if (linkedReturn is not null)
        {
            linkedReturn.CreditNoteId = null;
            linkedReturn.UpdatedAt = DateTime.UtcNow;
        }

        db.CreditNotes.Remove(credit);
        await db.SaveChangesAsync();
        return true;
    }

    private static CreditNoteDto Map(CreditNote c, Invoice? i) => new()
    {
        Id = c.Id,
        DocNum = c.DocNum,
        InvoiceId = c.InvoiceId,
        InvoiceDocNum = i?.DocNum ?? string.Empty,
        ReturnId = c.ReturnId,
        Amount = c.Amount,
        Reason = c.Reason,
        DocDate = c.DocDate,
        CreatedAt = c.CreatedAt
    };
}
