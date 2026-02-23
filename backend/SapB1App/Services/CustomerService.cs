using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext  _db;
    private readonly ISapB1Service _sap;

    public CustomerService(AppDbContext db, ISapB1Service sap)
    {
        _db  = db;
        _sap = sap;
    }

    // ── GET ALL (paged + search) ─────────────────────────────────────────────
    public async Task<PagedResult<CustomerDto>> GetAllAsync(
        int page, int pageSize, string? search, bool? isActive)
    {
        var query = _db.Customers
            .Include(c => c.Orders)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                c.CardCode.Contains(search) ||
                c.CardName.Contains(search) ||
                (c.Email != null && c.Email.Contains(search)));

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.CardName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<CustomerDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────
    public async Task<CustomerDto?> GetByIdAsync(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Id == id);

        return customer is null ? null : MapToDto(customer);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────
    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        var code = dto.CardCode.Trim().ToUpperInvariant();

        if (await _db.Customers.AnyAsync(c => c.CardCode == code))
            throw new InvalidOperationException(
                $"Le code client '{code}' existe déjà.");

        var customer = new Customer
        {
            CardCode    = code,
            CardName    = dto.CardName.Trim(),
            Phone       = dto.Phone,
            Email       = dto.Email?.Trim().ToLowerInvariant(),
            Address     = dto.Address,
            City        = dto.City,
            Country     = dto.Country,
            Currency    = dto.Currency,
            CreditLimit = dto.CreditLimit,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return MapToDto(customer);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────
    public async Task<CustomerDto?> UpdateAsync(int id, UpdateCustomerDto dto)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return null;

        customer.CardName    = dto.CardName.Trim();
        customer.Phone       = dto.Phone;
        customer.Email       = dto.Email?.Trim().ToLowerInvariant();
        customer.Address     = dto.Address;
        customer.City        = dto.City;
        customer.Country     = dto.Country;
        customer.CreditLimit = dto.CreditLimit;
        customer.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(customer);
    }

    // ── DELETE (soft) ────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return false;

        customer.IsActive  = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── SYNC TO SAP B1 ───────────────────────────────────────────────────────
    public async Task<CustomerDto?> SyncToSapAsync(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.Orders)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null) return null;

        await _sap.SyncCustomerAsync(id);

        // Simule un DocEntry retourné par SAP (remplacer par la vraie valeur)
        customer.SapDocNum = new Random().Next(1000, 9999);
        customer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapToDto(customer);
    }

    // ── MAPPER ───────────────────────────────────────────────────────────────
    private static CustomerDto MapToDto(Customer c) => new()
    {
        Id          = c.Id,
        CardCode    = c.CardCode,
        CardName    = c.CardName,
        Phone       = c.Phone,
        Email       = c.Email,
        Address     = c.Address,
        City        = c.City,
        Country     = c.Country,
        Currency    = c.Currency,
        CreditLimit = c.CreditLimit,
        IsActive    = c.IsActive,
        SyncedToSap = c.SapDocNum.HasValue,
        CreatedAt   = c.CreatedAt,
        OrderCount  = c.Orders?.Count ?? 0
    };
}
