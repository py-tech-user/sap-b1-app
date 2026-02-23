using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db)
    {
        _db = db;
    }

    // ── GET ALL ──────────────────────────────────────────────────────────────
    public async Task<PagedResult<ProductDto>> GetAllAsync(
        int page, int pageSize, string? search, string? category)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.ItemCode.Contains(search) ||
                p.ItemName.Contains(search));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        var total = await query.CountAsync();

        var items = await query
            .Where(p => p.IsActive)
            .OrderBy(p => p.ItemName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ProductDto>
        {
            Items      = items.Select(MapToDto),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────
    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var p = await _db.Products.FindAsync(id);
        return p is null ? null : MapToDto(p);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────
    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var code = dto.ItemCode.Trim().ToUpperInvariant();

        if (await _db.Products.AnyAsync(p => p.ItemCode == code))
            throw new InvalidOperationException(
                $"Le code article '{code}' existe déjà.");

        var product = new Product
        {
            ItemCode    = code,
            ItemName    = dto.ItemName.Trim(),
            Description = dto.Description,
            Price       = dto.Price,
            Category    = dto.Category,
            Stock       = dto.Stock,
            Unit        = dto.Unit ?? "Pcs",
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return MapToDto(product);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────
    public async Task<ProductDto?> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return null;

        product.ItemName    = dto.ItemName.Trim();
        product.Description = dto.Description;
        product.Price       = dto.Price;
        product.Category    = dto.Category;
        product.Stock       = dto.Stock;
        product.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapToDto(product);
    }

    // ── DELETE (soft) ────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null) return false;

        product.IsActive  = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    // ── MAPPER ───────────────────────────────────────────────────────────────
    private static ProductDto MapToDto(Product p) => new()
    {
        Id          = p.Id,
        ItemCode    = p.ItemCode,
        ItemName    = p.ItemName,
        Description = p.Description,
        Price       = p.Price,
        Category    = p.Category,
        Stock       = p.Stock,
        Unit        = p.Unit,
        IsActive    = p.IsActive,
        CreatedAt   = p.CreatedAt
    };
}
