using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Service Bons de livraison
// ═══════════════════════════════════════════════════════════════════════════

public class DeliveryNoteService : IDeliveryNoteService
{
    private readonly AppDbContext _db;

    public DeliveryNoteService(AppDbContext db) => _db = db;

    public async Task<PagedResult<DeliveryNoteDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId)
    {
        var query = _db.DeliveryNotes
            .Include(dn => dn.Customer)
            .Include(dn => dn.Order)
            .Include(dn => dn.Lines).ThenInclude(l => l.Product)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(dn => dn.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeliveryStatus>(status, true, out var deliveryStatus))
            query = query.Where(dn => dn.Status == deliveryStatus);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(dn => dn.DocNum.Contains(search) || dn.Customer.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(dn => dn.DocDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(dn => MapToDto(dn))
            .ToListAsync();

        return new PagedResult<DeliveryNoteDto>(items, totalCount, page, pageSize);
    }

    public async Task<DeliveryNoteDto?> GetByIdAsync(int id)
    {
        var dn = await _db.DeliveryNotes
            .Include(d => d.Customer)
            .Include(d => d.Order)
            .Include(d => d.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(d => d.Id == id);

        return dn is null ? null : MapToDto(dn);
    }

    public async Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto dto)
    {
        var order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == dto.OrderId)
            ?? throw new InvalidOperationException($"Order {dto.OrderId} not found.");

        var docNum = await GenerateDocNumAsync();

        var dn = new DeliveryNote
        {
            DocNum = docNum,
            CustomerId = order.CustomerId,
            OrderId = dto.OrderId,
            Status = DeliveryStatus.Draft,
            DeliveryAddress = dto.DeliveryAddress ?? order.Customer.Address,
            ContactName = dto.ContactName,
            ContactPhone = dto.ContactPhone,
            Carrier = dto.Carrier,
            Comments = dto.Comments
        };

        foreach (var lineDto in dto.Lines)
        {
            var product = await _db.Products.FindAsync(lineDto.ProductId)
                ?? throw new InvalidOperationException($"Product {lineDto.ProductId} not found.");

            dn.Lines.Add(new DeliveryNoteLine
            {
                ProductId = lineDto.ProductId,
                OrderLineId = lineDto.OrderLineId,
                OrderedQty = lineDto.OrderedQty,
                DeliveredQty = lineDto.DeliveredQty,
                BatchNumber = lineDto.BatchNumber,
                SerialNumber = lineDto.SerialNumber
            });
        }

        _db.DeliveryNotes.Add(dn);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(dn.Id) ?? throw new InvalidOperationException("Failed to create delivery note.");
    }

    public async Task<DeliveryNoteDto?> UpdateAsync(int id, UpdateDeliveryNoteDto dto)
    {
        var dn = await _db.DeliveryNotes.FindAsync(id);
        if (dn is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<DeliveryStatus>(dto.Status, true, out var status))
            dn.Status = status;

        dn.DeliveryDate = dto.DeliveryDate ?? dn.DeliveryDate;
        dn.TrackingNumber = dto.TrackingNumber ?? dn.TrackingNumber;
        dn.ReceivedBy = dto.ReceivedBy ?? dn.ReceivedBy;
        dn.Comments = dto.Comments ?? dn.Comments;
        dn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<DeliveryNoteDto?> ConfirmAsync(int id)
    {
        var dn = await _db.DeliveryNotes.Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == id);
        if (dn is null || dn.Status != DeliveryStatus.Draft) return null;

        // Déduire du stock
        foreach (var line in dn.Lines)
        {
            var product = await _db.Products.FindAsync(line.ProductId);
            if (product != null)
                product.Stock -= (int)line.DeliveredQty;
        }

        dn.Status = DeliveryStatus.Confirmed;
        dn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<DeliveryNoteDto?> ShipAsync(int id, string? trackingNumber)
    {
        var dn = await _db.DeliveryNotes.FindAsync(id);
        if (dn is null || dn.Status != DeliveryStatus.Confirmed) return null;

        dn.Status = DeliveryStatus.InTransit;
        dn.TrackingNumber = trackingNumber;
        dn.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<DeliveryNoteDto?> DeliverAsync(int id, string? receivedBy)
    {
        var dn = await _db.DeliveryNotes.FindAsync(id);
        if (dn is null) return null;

        dn.Status = DeliveryStatus.Delivered;
        dn.DeliveryDate = DateTime.UtcNow;
        dn.ReceivedBy = receivedBy;
        dn.UpdatedAt = DateTime.UtcNow;

        // Mettre à jour le statut de la commande
        var order = await _db.Orders.FindAsync(dn.OrderId);
        if (order != null)
            order.Status = OrderStatus.Delivered;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var dn = await _db.DeliveryNotes.FindAsync(id);
        if (dn is null || dn.Status != DeliveryStatus.Draft) return false;

        _db.DeliveryNotes.Remove(dn);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateDocNumAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.DeliveryNotes.CountAsync(dn => dn.DocDate.Year == year) + 1;
        return $"BL-{year}-{count:D4}";
    }

    private static DeliveryNoteDto MapToDto(DeliveryNote dn) => new()
    {
        Id = dn.Id,
        DocNum = dn.DocNum,
        CustomerId = dn.CustomerId,
        CustomerName = dn.Customer?.CardName ?? "",
        CustomerCode = dn.Customer?.CardCode ?? "",
        OrderId = dn.OrderId,
        OrderDocNum = dn.Order?.DocNum ?? "",
        Status = dn.Status.ToString(),
        DocDate = dn.DocDate,
        DeliveryDate = dn.DeliveryDate,
        DeliveryAddress = dn.DeliveryAddress,
        ContactName = dn.ContactName,
        ContactPhone = dn.ContactPhone,
        TrackingNumber = dn.TrackingNumber,
        Carrier = dn.Carrier,
        TotalWeight = dn.TotalWeight,
        PackageCount = dn.PackageCount,
        Comments = dn.Comments,
        ReceivedBy = dn.ReceivedBy,
        SyncedToSap = dn.SyncedToSap,
        CreatedAt = dn.CreatedAt,
        Lines = dn.Lines.Select(l => new DeliveryNoteLineDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ItemCode = l.Product?.ItemCode ?? "",
            ItemName = l.Product?.ItemName ?? "",
            OrderedQty = l.OrderedQty,
            DeliveredQty = l.DeliveredQty,
            BatchNumber = l.BatchNumber,
            SerialNumber = l.SerialNumber
        }).ToList()
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Fournisseurs
// ═══════════════════════════════════════════════════════════════════════════

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _db;

    public SupplierService(AppDbContext db) => _db = db;

    public async Task<PagedResult<SupplierDto>> GetAllAsync(int page, int pageSize, string? search)
    {
        var query = _db.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.CardCode.Contains(search) || s.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.CardName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                CardCode = s.CardCode,
                CardName = s.CardName,
                Address = s.Address,
                City = s.City,
                Country = s.Country,
                Phone = s.Phone,
                Email = s.Email,
                TaxId = s.TaxId,
                Currency = s.Currency,
                PaymentTerms = s.PaymentTerms,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<SupplierDto>(items, totalCount, page, pageSize);
    }

    public async Task<SupplierDto?> GetByIdAsync(int id)
    {
        var s = await _db.Suppliers.FindAsync(id);
        if (s is null) return null;

        return new SupplierDto
        {
            Id = s.Id,
            CardCode = s.CardCode,
            CardName = s.CardName,
            Address = s.Address,
            City = s.City,
            Country = s.Country,
            Phone = s.Phone,
            Email = s.Email,
            TaxId = s.TaxId,
            Currency = s.Currency,
            PaymentTerms = s.PaymentTerms,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt
        };
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierDto dto)
    {
        if (await _db.Suppliers.AnyAsync(s => s.CardCode == dto.CardCode))
            throw new InvalidOperationException($"Supplier with code {dto.CardCode} already exists.");

        var supplier = new Supplier
        {
            CardCode = dto.CardCode,
            CardName = dto.CardName,
            Address = dto.Address,
            City = dto.City,
            Country = dto.Country,
            Phone = dto.Phone,
            Email = dto.Email,
            TaxId = dto.TaxId,
            Currency = dto.Currency,
            PaymentTerms = dto.PaymentTerms
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(supplier.Id) ?? throw new InvalidOperationException("Failed to create supplier.");
    }

    public async Task<SupplierDto?> UpdateAsync(int id, UpdateSupplierDto dto)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return null;

        supplier.CardName = dto.CardName ?? supplier.CardName;
        supplier.Address = dto.Address ?? supplier.Address;
        supplier.City = dto.City ?? supplier.City;
        supplier.Country = dto.Country ?? supplier.Country;
        supplier.Phone = dto.Phone ?? supplier.Phone;
        supplier.Email = dto.Email ?? supplier.Email;
        supplier.TaxId = dto.TaxId ?? supplier.TaxId;
        supplier.PaymentTerms = dto.PaymentTerms ?? supplier.PaymentTerms;
        supplier.IsActive = dto.IsActive ?? supplier.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier is null) return false;

        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync();
        return true;
    }
}
