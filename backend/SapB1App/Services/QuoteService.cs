using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class QuoteService(AppDbContext db) : IQuoteService
{
    public async Task<PagedResult<QuoteDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo)
    {
        var query = db.Quotes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(q => q.DocNum.Contains(search) || q.Customer.CardName.Contains(search));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<QuoteStatus>(status, true, out var statusEnum))
            query = query.Where(q => q.Status == statusEnum);

        if (customerId.HasValue)
            query = query.Where(q => q.CustomerId == customerId.Value);

        if (dateFrom.HasValue)
            query = query.Where(q => q.DocDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(q => q.DocDate <= dateTo.Value);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(q => q.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var ids = items.Select(i => i.Id).ToList();
        var customerIds = items.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await db.Customers.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var lines = await db.QuoteLines.AsNoTracking().Include(l => l.Product).Where(l => ids.Contains(l.QuoteId)).ToListAsync();
        var lineMap = lines.GroupBy(l => l.QuoteId).ToDictionary(g => g.Key, g => g.AsEnumerable());

        var mapped = items.Select(i => Map(i, customers.GetValueOrDefault(i.CustomerId), lineMap.GetValueOrDefault(i.Id))).ToList();
        return new PagedResult<QuoteDto>(mapped, total, page, pageSize);
    }

    public async Task<QuoteDto?> GetByIdAsync(int id)
    {
        var quote = await db.Quotes.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return null;

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == quote.CustomerId);
        var lines = await db.QuoteLines.AsNoTracking().Include(l => l.Product).Where(l => l.QuoteId == id).ToListAsync();
        return Map(quote, customer, lines);
    }

    public async Task<QuoteDto> CreateAsync(CreateQuoteDto dto)
    {
        _ = await db.Customers.FindAsync(dto.CustomerId) ?? throw new InvalidOperationException("Client introuvable.");

        var quote = new Quote
        {
            CustomerId = dto.CustomerId,
            CardCode = (await db.Customers.AsNoTracking().Where(c => c.Id == dto.CustomerId).Select(c => c.CardCode).FirstAsync()),
            DocNum = $"DEV-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Currency = dto.Currency,
            Comments = dto.Comments,
            Status = QuoteStatus.Pending,
            BaseType = null,
            BaseEntry = null,
            BaseLine = null,
            DocDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var productCodes = await db.Products.AsNoTracking().Where(p => dto.Lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ItemCode);
        AddLines(quote, dto.Lines, productCodes);
        db.Quotes.Add(quote);
        await db.SaveChangesAsync();
        return (await GetByIdAsync(quote.Id))!;
    }

    public async Task<QuoteDto?> UpdateAsync(int id, CreateQuoteDto dto)
    {
        var quote = await db.Quotes.AsTracking().Include(q => q.Lines).FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return null;
        if (quote.Status != QuoteStatus.Pending)
            throw new InvalidOperationException("Seul un devis en attente peut être modifié.");

        quote.CustomerId = dto.CustomerId;
        quote.CardCode = await db.Customers.AsNoTracking().Where(c => c.Id == dto.CustomerId).Select(c => c.CardCode).FirstAsync();
        quote.Currency = dto.Currency;
        quote.Comments = dto.Comments;
        quote.UpdatedAt = DateTime.UtcNow;

        db.QuoteLines.RemoveRange(quote.Lines);
        quote.Lines.Clear();
        var productCodes = await db.Products.AsNoTracking().Where(p => dto.Lines.Select(l => l.ProductId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ItemCode);
        AddLines(quote, dto.Lines, productCodes);

        await db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<QuoteDto?> UpdateStatusAsync(int id, UpdateQuoteStatusDto dto)
    {
        var quote = await db.Quotes.AsTracking().FirstOrDefaultAsync(q => q.Id == id);
        if (quote is null) return null;

        var statusRaw = dto.Status?.Trim().ToLowerInvariant();
        var statusParsed = Enum.TryParse<QuoteStatus>(dto.Status, true, out var parsed)
            ? parsed
            : statusRaw switch
            {
                "en attente" => QuoteStatus.Pending,
                "accepte" or "accepté" => QuoteStatus.Accepted,
                "refuse" or "refusé" => QuoteStatus.Rejected,
                _ => (QuoteStatus?)null
            };

        if (!statusParsed.HasValue)
            throw new InvalidOperationException("Statut de devis invalide.");

        var newStatus = statusParsed.Value;

        if (!DocumentStatusTransitions.CanTransition(quote.Status, newStatus))
            throw new InvalidOperationException($"Transition non autorisée: {quote.Status} -> {newStatus}");

        quote.Status = newStatus;
        quote.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var quote = await db.Quotes.FindAsync(id);
        if (quote is null) return false;
        if (quote.Status != QuoteStatus.Pending)
            throw new InvalidOperationException("Seul un devis en attente peut être supprimé.");

        db.Quotes.Remove(quote);
        await db.SaveChangesAsync();
        return true;
    }

    private static void AddLines(Quote quote, IEnumerable<CreateQuoteLineDto> lines, IReadOnlyDictionary<int, string> productCodes)
    {
        var index = 1;
        foreach (var line in lines)
        {
            var lineTotal = line.Quantity * line.UnitPrice * (1 + line.VatPct / 100);
            productCodes.TryGetValue(line.ProductId, out var itemCode);

            quote.Lines.Add(new QuoteLine
            {
                ProductId = line.ProductId,
                ItemCode = itemCode ?? string.Empty,
                LineNum = index++,
                Quantity = line.Quantity,
                Price = line.UnitPrice,
                UnitPrice = line.UnitPrice,
                VatPct = line.VatPct,
                LineTotal = Math.Round(lineTotal, 4),
                BaseEntry = null,
                BaseLine = null
            });
        }

        quote.VatTotal = Math.Round(quote.Lines.Sum(l => l.LineTotal - (l.Quantity * l.UnitPrice)), 4);
        quote.DocTotal = Math.Round(quote.Lines.Sum(l => l.LineTotal), 4);
    }

    private static QuoteDto Map(Quote q, Customer? c, IEnumerable<QuoteLine>? lines) => new()
    {
        Id = q.Id,
        DocEntry = q.Id,
        DocNum = q.DocNum,
        CardCode = q.CardCode,
        CustomerId = q.CustomerId,
        CustomerName = c?.CardName ?? string.Empty,
        CustomerCode = c?.CardCode ?? string.Empty,
        DocDate = q.DocDate,
        Status = q.Status.ToString(),
        DocTotal = q.DocTotal,
        VatTotal = q.VatTotal,
        BaseType = q.BaseType?.ToString(),
        BaseEntry = q.BaseEntry,
        BaseLine = q.BaseLine,
        Currency = q.Currency,
        Comments = q.Comments,
        CreatedAt = q.CreatedAt,
        Lines = lines?.Select(l => new QuoteLineDto
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
