using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Service Bons de commande fournisseur
// ═══════════════════════════════════════════════════════════════════════════

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly AppDbContext _db;

    public PurchaseOrderService(AppDbContext db) => _db = db;

    public async Task<PagedResult<PurchaseOrderDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? supplierId)
    {
        var query = _db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(po => po.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PurchaseOrderStatus>(status, true, out var poStatus))
            query = query.Where(po => po.Status == poStatus);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(po => po.DocNum.Contains(search) || po.Supplier.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(po => po.DocDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(po => MapToDto(po))
            .ToListAsync();

        return new PagedResult<PurchaseOrderDto>(items, totalCount, page, pageSize);
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(p => p.Id == id);

        return po is null ? null : MapToDto(po);
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto dto)
    {
        var supplier = await _db.Suppliers.FindAsync(dto.SupplierId)
            ?? throw new InvalidOperationException($"Supplier {dto.SupplierId} not found.");

        var docNum = await GenerateDocNumAsync();

        var po = new PurchaseOrder
        {
            DocNum = docNum,
            SupplierId = dto.SupplierId,
            Status = PurchaseOrderStatus.Draft,
            ExpectedDate = dto.ExpectedDate,
            Currency = dto.Currency,
            Reference = dto.Reference,
            Comments = dto.Comments
        };

        foreach (var lineDto in dto.Lines)
        {
            var product = await _db.Products.FindAsync(lineDto.ProductId)
                ?? throw new InvalidOperationException($"Product {lineDto.ProductId} not found.");

            var unitPrice = lineDto.UnitPrice > 0 ? lineDto.UnitPrice : product.Price;
            var lineTotal = lineDto.Quantity * unitPrice;

            po.Lines.Add(new PurchaseOrderLine
            {
                ProductId = lineDto.ProductId,
                Quantity = lineDto.Quantity,
                UnitPrice = unitPrice,
                VatPct = lineDto.VatPct,
                LineTotal = lineTotal,
                ExpectedDate = dto.ExpectedDate
            });
        }

        po.DocTotal = po.Lines.Sum(l => l.LineTotal);
        po.VatTotal = po.Lines.Sum(l => l.LineTotal * l.VatPct / 100);

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(po.Id) ?? throw new InvalidOperationException("Failed to create purchase order.");
    }

    public async Task<PurchaseOrderDto?> UpdateAsync(int id, UpdatePurchaseOrderDto dto)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<PurchaseOrderStatus>(dto.Status, true, out var status))
            po.Status = status;

        po.ExpectedDate = dto.ExpectedDate ?? po.ExpectedDate;
        po.Reference = dto.Reference ?? po.Reference;
        po.Comments = dto.Comments ?? po.Comments;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<PurchaseOrderDto?> SendAsync(int id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null || po.Status != PurchaseOrderStatus.Draft) return null;

        po.Status = PurchaseOrderStatus.Sent;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<PurchaseOrderDto?> ConfirmAsync(int id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null || po.Status != PurchaseOrderStatus.Sent) return null;

        po.Status = PurchaseOrderStatus.Confirmed;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);
        if (po is null || po.Status != PurchaseOrderStatus.Draft) return false;

        _db.PurchaseOrders.Remove(po);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateDocNumAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.PurchaseOrders.CountAsync(po => po.DocDate.Year == year) + 1;
        return $"PO-{year}-{count:D4}";
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder po) => new()
    {
        Id = po.Id,
        DocNum = po.DocNum,
        SupplierId = po.SupplierId,
        SupplierName = po.Supplier?.CardName ?? "",
        SupplierCode = po.Supplier?.CardCode ?? "",
        Status = po.Status.ToString(),
        DocDate = po.DocDate,
        ExpectedDate = po.ExpectedDate,
        ReceivedDate = po.ReceivedDate,
        DocTotal = po.DocTotal,
        VatTotal = po.VatTotal,
        Currency = po.Currency,
        Reference = po.Reference,
        Comments = po.Comments,
        SyncedToSap = po.SyncedToSap,
        CreatedAt = po.CreatedAt,
        Lines = po.Lines.Select(l => new PurchaseOrderLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ItemCode = l.Product?.ItemCode ?? "",
            ItemName = l.Product?.ItemName ?? "",
            Quantity = l.Quantity,
            ReceivedQty = l.ReceivedQty,
            UnitPrice = l.UnitPrice,
            VatPct = l.VatPct,
            LineTotal = l.LineTotal,
            ExpectedDate = l.ExpectedDate
        }).ToList()
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Avoirs
// ═══════════════════════════════════════════════════════════════════════════

public class CreditNoteService : ICreditNoteService
{
    private readonly AppDbContext _db;

    public CreditNoteService(AppDbContext db) => _db = db;

    public async Task<PagedResult<CreditNoteDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId)
    {
        var query = _db.CreditNotes
            .Include(cn => cn.Customer)
            .Include(cn => cn.Order)
            .Include(cn => cn.Return)
            .Include(cn => cn.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(cn => cn.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CreditNoteStatus>(status, true, out var cnStatus))
            query = query.Where(cn => cn.Status == cnStatus);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(cn => cn.DocNum.Contains(search) || cn.Customer.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(cn => cn.DocDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cn => MapToDto(cn))
            .ToListAsync();

        return new PagedResult<CreditNoteDto>(items, totalCount, page, pageSize);
    }

    public async Task<CreditNoteDto?> GetByIdAsync(int id)
    {
        var cn = await _db.CreditNotes
            .Include(c => c.Customer)
            .Include(c => c.Order)
            .Include(c => c.Return)
            .Include(c => c.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(c => c.Id == id);

        return cn is null ? null : MapToDto(cn);
    }

    public async Task<CreditNoteDto> CreateAsync(CreateCreditNoteDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Customer {dto.CustomerId} not found.");

        var docNum = await GenerateDocNumAsync();

        Enum.TryParse<CreditNoteReason>(dto.Reason, true, out var reason);

        var cn = new CreditNote
        {
            DocNum = docNum,
            CustomerId = dto.CustomerId,
            OrderId = dto.OrderId,
            ReturnId = dto.ReturnId,
            Reason = reason,
            Status = CreditNoteStatus.Draft,
            Currency = dto.Currency,
            Comments = dto.Comments
        };

        foreach (var lineDto in dto.Lines)
        {
            var product = await _db.Products.FindAsync(lineDto.ProductId)
                ?? throw new InvalidOperationException($"Product {lineDto.ProductId} not found.");

            var unitPrice = lineDto.UnitPrice > 0 ? lineDto.UnitPrice : product.Price;
            var lineTotal = lineDto.Quantity * unitPrice;

            cn.Lines.Add(new CreditNoteLine
            {
                ProductId = lineDto.ProductId,
                Quantity = lineDto.Quantity,
                UnitPrice = unitPrice,
                VatPct = lineDto.VatPct,
                LineTotal = lineTotal
            });
        }

        cn.DocTotal = cn.Lines.Sum(l => l.LineTotal);
        cn.VatTotal = cn.Lines.Sum(l => l.LineTotal * l.VatPct / 100);

        _db.CreditNotes.Add(cn);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(cn.Id) ?? throw new InvalidOperationException("Failed to create credit note.");
    }

    public async Task<CreditNoteDto?> UpdateAsync(int id, UpdateCreditNoteDto dto)
    {
        var cn = await _db.CreditNotes.FindAsync(id);
        if (cn is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<CreditNoteStatus>(dto.Status, true, out var status))
            cn.Status = status;

        cn.Comments = dto.Comments ?? cn.Comments;
        cn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<CreditNoteDto?> ConfirmAsync(int id)
    {
        var cn = await _db.CreditNotes.FindAsync(id);
        if (cn is null || cn.Status != CreditNoteStatus.Draft) return null;

        cn.Status = CreditNoteStatus.Confirmed;
        cn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<CreditNoteDto?> ApplyAsync(int id, int? invoiceId)
    {
        var cn = await _db.CreditNotes.FindAsync(id);
        if (cn is null || cn.Status != CreditNoteStatus.Confirmed) return null;

        cn.Status = CreditNoteStatus.Applied;
        cn.AppliedDate = DateTime.UtcNow;
        cn.AppliedToInvoiceId = invoiceId;
        cn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<CreditNoteDto?> RefundAsync(int id)
    {
        var cn = await _db.CreditNotes.FindAsync(id);
        if (cn is null || cn.Status != CreditNoteStatus.Confirmed) return null;

        cn.Status = CreditNoteStatus.Refunded;
        cn.AppliedDate = DateTime.UtcNow;
        cn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var cn = await _db.CreditNotes.FindAsync(id);
        if (cn is null || cn.Status != CreditNoteStatus.Draft) return false;

        _db.CreditNotes.Remove(cn);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<CreditNoteDto> CreateFromReturnAsync(int returnId)
    {
        var ret = await _db.Returns
            .Include(r => r.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(r => r.Id == returnId)
            ?? throw new InvalidOperationException($"Return {returnId} not found.");

        var dto = new CreateCreditNoteDto
        {
            CustomerId = ret.CustomerId,
            OrderId = ret.OrderId,
            ReturnId = returnId,
            Reason = "Return",
            Comments = $"Avoir généré depuis retour {ret.ReturnNumber}",
            Lines = ret.Lines.Select(l => new CreateCreditNoteLineDto
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                VatPct = 20
            }).ToList()
        };

        return await CreateAsync(dto);
    }

    private async Task<string> GenerateDocNumAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.CreditNotes.CountAsync(cn => cn.DocDate.Year == year) + 1;
        return $"AV-{year}-{count:D4}";
    }

    private static CreditNoteDto MapToDto(CreditNote cn) => new()
    {
        Id = cn.Id,
        DocNum = cn.DocNum,
        CustomerId = cn.CustomerId,
        CustomerName = cn.Customer?.CardName ?? "",
        CustomerCode = cn.Customer?.CardCode ?? "",
        OrderId = cn.OrderId,
        OrderDocNum = cn.Order?.DocNum,
        ReturnId = cn.ReturnId,
        ReturnNumber = cn.Return?.ReturnNumber,
        Status = cn.Status.ToString(),
        Reason = cn.Reason.ToString(),
        DocDate = cn.DocDate,
        DocTotal = cn.DocTotal,
        VatTotal = cn.VatTotal,
        Currency = cn.Currency,
        Comments = cn.Comments,
        AppliedDate = cn.AppliedDate,
        SyncedToSap = cn.SyncedToSap,
        CreatedAt = cn.CreatedAt,
        Lines = cn.Lines.Select(l => new CreditNoteLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ItemCode = l.Product?.ItemCode ?? "",
            ItemName = l.Product?.ItemName ?? "",
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            VatPct = l.VatPct,
            LineTotal = l.LineTotal
        }).ToList()
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Réception marchandise
// ═══════════════════════════════════════════════════════════════════════════

public class GoodsReceiptService : IGoodsReceiptService
{
    private readonly AppDbContext _db;

    public GoodsReceiptService(AppDbContext db) => _db = db;

    public async Task<PagedResult<GoodsReceiptDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? supplierId)
    {
        var query = _db.GoodsReceipts
            .Include(gr => gr.Supplier)
            .Include(gr => gr.PurchaseOrder)
            .Include(gr => gr.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(gr => gr.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GoodsReceiptStatus>(status, true, out var grStatus))
            query = query.Where(gr => gr.Status == grStatus);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(gr => gr.DocNum.Contains(search) || gr.Supplier.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(gr => gr.DocDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(gr => MapToDto(gr))
            .ToListAsync();

        return new PagedResult<GoodsReceiptDto>(items, totalCount, page, pageSize);
    }

    public async Task<GoodsReceiptDto?> GetByIdAsync(int id)
    {
        var gr = await _db.GoodsReceipts
            .Include(g => g.Supplier)
            .Include(g => g.PurchaseOrder)
            .Include(g => g.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(g => g.Id == id);

        return gr is null ? null : MapToDto(gr);
    }

    public async Task<GoodsReceiptDto> CreateAsync(CreateGoodsReceiptDto dto)
    {
        var supplier = await _db.Suppliers.FindAsync(dto.SupplierId)
            ?? throw new InvalidOperationException($"Supplier {dto.SupplierId} not found.");

        var docNum = await GenerateDocNumAsync();

        var gr = new GoodsReceipt
        {
            DocNum = docNum,
            SupplierId = dto.SupplierId,
            PurchaseOrderId = dto.PurchaseOrderId,
            Status = GoodsReceiptStatus.Draft,
            DeliveryNoteRef = dto.DeliveryNoteRef,
            Comments = dto.Comments
        };

        foreach (var lineDto in dto.Lines)
        {
            var product = await _db.Products.FindAsync(lineDto.ProductId)
                ?? throw new InvalidOperationException($"Product {lineDto.ProductId} not found.");

            var unitPrice = lineDto.UnitPrice > 0 ? lineDto.UnitPrice : product.Price;

            gr.Lines.Add(new GoodsReceiptLine
            {
                ProductId = lineDto.ProductId,
                Quantity = lineDto.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineDto.Quantity * unitPrice,
                BatchNumber = lineDto.BatchNumber,
                SerialNumber = lineDto.SerialNumber,
                Location = lineDto.Location
            });
        }

        _db.GoodsReceipts.Add(gr);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(gr.Id) ?? throw new InvalidOperationException("Failed to create goods receipt.");
    }

    public async Task<GoodsReceiptDto?> ConfirmAsync(int id)
    {
        var gr = await _db.GoodsReceipts.Include(g => g.Lines).FirstOrDefaultAsync(g => g.Id == id);
        if (gr is null || gr.Status != GoodsReceiptStatus.Draft) return null;

        // Ajouter au stock
        foreach (var line in gr.Lines)
        {
            var product = await _db.Products.FindAsync(line.ProductId);
            if (product != null)
                product.Stock += (int)line.Quantity;
        }

        // Mettre à jour le BC si lié
        if (gr.PurchaseOrderId.HasValue)
        {
            var po = await _db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == gr.PurchaseOrderId);
            if (po != null)
            {
                foreach (var grLine in gr.Lines)
                {
                    var poLine = po.Lines.FirstOrDefault(l => l.ProductId == grLine.ProductId);
                    if (poLine != null)
                        poLine.ReceivedQty += grLine.Quantity;
                }

                var allReceived = po.Lines.All(l => l.ReceivedQty >= l.Quantity);
                po.Status = allReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
                if (allReceived)
                    po.ReceivedDate = DateTime.UtcNow;
            }
        }

        gr.Status = GoodsReceiptStatus.Confirmed;
        gr.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var gr = await _db.GoodsReceipts.FindAsync(id);
        if (gr is null || gr.Status != GoodsReceiptStatus.Draft) return false;

        _db.GoodsReceipts.Remove(gr);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<GoodsReceiptDto> CreateFromPurchaseOrderAsync(int purchaseOrderId, List<CreateGoodsReceiptLineDto> lines)
    {
        var po = await _db.PurchaseOrders.FindAsync(purchaseOrderId)
            ?? throw new InvalidOperationException($"Purchase order {purchaseOrderId} not found.");

        var dto = new CreateGoodsReceiptDto
        {
            SupplierId = po.SupplierId,
            PurchaseOrderId = purchaseOrderId,
            DeliveryNoteRef = po.Reference,
            Comments = $"Réception depuis BC {po.DocNum}",
            Lines = lines
        };

        return await CreateAsync(dto);
    }

    private async Task<string> GenerateDocNumAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.GoodsReceipts.CountAsync(gr => gr.DocDate.Year == year) + 1;
        return $"GR-{year}-{count:D4}";
    }

    private static GoodsReceiptDto MapToDto(GoodsReceipt gr) => new()
    {
        Id = gr.Id,
        DocNum = gr.DocNum,
        SupplierId = gr.SupplierId,
        SupplierName = gr.Supplier?.CardName ?? "",
        SupplierCode = gr.Supplier?.CardCode ?? "",
        PurchaseOrderId = gr.PurchaseOrderId,
        PurchaseOrderNum = gr.PurchaseOrder?.DocNum,
        Status = gr.Status.ToString(),
        DocDate = gr.DocDate,
        DeliveryNoteRef = gr.DeliveryNoteRef,
        Comments = gr.Comments,
        SyncedToSap = gr.SyncedToSap,
        CreatedAt = gr.CreatedAt,
        Lines = gr.Lines.Select(l => new GoodsReceiptLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ItemCode = l.Product?.ItemCode ?? "",
            ItemName = l.Product?.ItemName ?? "",
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            LineTotal = l.LineTotal,
            BatchNumber = l.BatchNumber,
            SerialNumber = l.SerialNumber,
            Location = l.Location
        }).ToList()
    };
}
