using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Service Retours
// ═══════════════════════════════════════════════════════════════════════════

public class ReturnService : IReturnService
{
    private readonly AppDbContext _db;

    public ReturnService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ReturnDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId)
    {
        var query = _db.Returns
            .Include(r => r.Customer)
            .Include(r => r.Order)
            .Include(r => r.Approver)
            .Include(r => r.CreditNote)
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(r => r.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReturnStatus>(status, true, out var returnStatus))
            query = query.Where(r => r.Status == returnStatus);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.ReturnNumber.Contains(search) || r.Customer.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.RequestDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return new PagedResult<ReturnDto>(items, totalCount, page, pageSize);
    }

    public async Task<ReturnDto?> GetByIdAsync(int id)
    {
        var ret = await _db.Returns
            .Include(r => r.Customer)
            .Include(r => r.Order)
            .Include(r => r.Approver)
            .Include(r => r.CreditNote)
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(r => r.Id == id);

        return ret is null ? null : MapToDto(ret);
    }

    public async Task<ReturnDto> CreateAsync(CreateReturnDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Customer {dto.CustomerId} not found.");

        var returnNumber = await GenerateReturnNumberAsync();

        if (!Enum.TryParse<ReturnReason>(dto.Reason, true, out var reason))
            reason = ReturnReason.Defective;

        var ret = new Return
        {
            ReturnNumber = returnNumber,
            CustomerId = dto.CustomerId,
            OrderId = dto.OrderId,
            DeliveryNoteId = dto.DeliveryNoteId,
            Reason = reason,
            ReasonDetails = dto.ReasonDetails,
            Comments = dto.Comments,
            Status = ReturnStatus.Pending
        };

        foreach (var lineDto in dto.Lines)
        {
            var product = await _db.Products.FindAsync(lineDto.ProductId)
                ?? throw new InvalidOperationException($"Product {lineDto.ProductId} not found.");

            var unitPrice = lineDto.UnitPrice > 0 ? lineDto.UnitPrice : product.Price;
            ret.Lines.Add(new ReturnLine
            {
                ProductId = lineDto.ProductId,
                Quantity = lineDto.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineDto.Quantity * unitPrice,
                Condition = lineDto.Condition,
                Comments = lineDto.Comments
            });
        }

        ret.TotalAmount = ret.Lines.Sum(l => l.LineTotal);

        _db.Returns.Add(ret);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(ret.Id) ?? throw new InvalidOperationException("Failed to create return.");
    }

    public async Task<ReturnDto?> UpdateAsync(int id, UpdateReturnDto dto)
    {
        var ret = await _db.Returns.FindAsync(id);
        if (ret is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<ReturnStatus>(dto.Status, true, out var status))
            ret.Status = status;

        if (!string.IsNullOrWhiteSpace(dto.Reason) && Enum.TryParse<ReturnReason>(dto.Reason, true, out var reason))
            ret.Reason = reason;

        ret.ReasonDetails = dto.ReasonDetails ?? ret.ReasonDetails;
        ret.Comments = dto.Comments ?? ret.Comments;
        ret.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ReturnDto?> ApproveAsync(int id, int approverId, ApproveReturnDto dto)
    {
        var ret = await _db.Returns.FindAsync(id);
        if (ret is null) return null;

        ret.Status = dto.Approved ? ReturnStatus.Approved : ReturnStatus.Rejected;
        ret.ApprovedBy = approverId;
        ret.ApprovalDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Comments))
            ret.Comments = (ret.Comments ?? "") + "\n[Approval] " + dto.Comments;
        ret.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ReturnDto?> ReceiveAsync(int id)
    {
        var ret = await _db.Returns.FindAsync(id);
        if (ret is null || ret.Status != ReturnStatus.Approved) return null;

        ret.Status = ReturnStatus.Received;
        ret.ReceivedDate = DateTime.UtcNow;
        ret.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ReturnDto?> ProcessAsync(int id)
    {
        var ret = await _db.Returns
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (ret is null || ret.Status != ReturnStatus.Received) return null;

        // Créer l'avoir automatiquement
        var creditNote = new CreditNote
        {
            DocNum = await GenerateCreditNoteNumberAsync(),
            CustomerId = ret.CustomerId,
            OrderId = ret.OrderId,
            ReturnId = ret.Id,
            Reason = CreditNoteReason.Return,
            Status = CreditNoteStatus.Confirmed,
            DocDate = DateTime.UtcNow,
            Currency = "EUR",
            Comments = $"Avoir généré depuis retour {ret.ReturnNumber}"
        };

        foreach (var line in ret.Lines)
        {
            creditNote.Lines.Add(new CreditNoteLine
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                VatPct = 20,
                LineTotal = line.LineTotal
            });
        }

        creditNote.DocTotal = creditNote.Lines.Sum(l => l.LineTotal);
        creditNote.VatTotal = creditNote.DocTotal * 0.2m;

        _db.CreditNotes.Add(creditNote);

        ret.Status = ReturnStatus.Processed;
        ret.CreditNoteId = creditNote.Id;
        ret.UpdatedAt = DateTime.UtcNow;

        // Remettre les produits en stock
        foreach (var line in ret.Lines)
        {
            var product = await _db.Products.FindAsync(line.ProductId);
            if (product != null)
                product.Stock += (int)line.Quantity;
        }

        await _db.SaveChangesAsync();

        ret.CreditNoteId = creditNote.Id;
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ret = await _db.Returns.FindAsync(id);
        if (ret is null || ret.Status != ReturnStatus.Pending) return false;

        _db.Returns.Remove(ret);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateReturnNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Returns.CountAsync(r => r.RequestDate.Year == year) + 1;
        return $"RET-{year}-{count:D4}";
    }

    private async Task<string> GenerateCreditNoteNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.CreditNotes.CountAsync(cn => cn.DocDate.Year == year) + 1;
        return $"AV-{year}-{count:D4}";
    }

    private static ReturnDto MapToDto(Return r) => new()
    {
        Id = r.Id,
        ReturnNumber = r.ReturnNumber,
        CustomerId = r.CustomerId,
        CustomerName = r.Customer?.CardName ?? "",
        CustomerCode = r.Customer?.CardCode ?? "",
        OrderId = r.OrderId,
        OrderDocNum = r.Order?.DocNum,
        DeliveryNoteId = r.DeliveryNoteId,
        Status = r.Status.ToString(),
        Reason = r.Reason.ToString(),
        ReasonDetails = r.ReasonDetails,
        RequestDate = r.RequestDate,
        ApprovalDate = r.ApprovalDate,
        ReceivedDate = r.ReceivedDate,
        ApprovedBy = r.ApprovedBy,
        ApproverName = r.Approver?.FullName,
        TotalAmount = r.TotalAmount,
        Comments = r.Comments,
        CreditNoteId = r.CreditNoteId,
        CreditNoteNum = r.CreditNote?.DocNum,
        SyncedToSap = r.SyncedToSap,
        CreatedAt = r.CreatedAt,
        Lines = r.Lines.Select(l => new ReturnLineDto
        {
            Id = l.Id,
            ReturnId = l.ReturnId,
            ProductId = l.ProductId,
            ItemCode = l.Product?.ItemCode ?? "",
            ItemName = l.Product?.ItemName ?? "",
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            Condition = l.Condition,
            Comments = l.Comments
        }).ToList()
    };
}
