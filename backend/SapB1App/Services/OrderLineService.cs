using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class OrderLineService : IOrderLineService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public OrderLineService(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<IEnumerable<OrderLineDto>> GetByOrderIdAsync(int orderId)
    {
        var lines = await _db.OrderLines
            .Include(ol => ol.Product)
            .Where(ol => ol.OrderId == orderId)
            .OrderBy(ol => ol.LineNum)
            .ToListAsync();

        return _mapper.Map<IEnumerable<OrderLineDto>>(lines);
    }

    public async Task<OrderLineDto?> GetByIdAsync(int id)
    {
        var line = await _db.OrderLines
            .Include(ol => ol.Product)
            .FirstOrDefaultAsync(ol => ol.Id == id);

        return line is null ? null : _mapper.Map<OrderLineDto>(line);
    }

    public async Task<OrderLineDto> CreateAsync(int orderId, CreateOrderLineDto dto)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new InvalidOperationException($"Order ID {orderId} not found.");

        var product = await _db.Products.FindAsync(dto.ProductId)
            ?? throw new InvalidOperationException($"Product ID {dto.ProductId} not found.");

        var lineNum = order.Lines.Count > 0 ? order.Lines.Max(l => l.LineNum) + 1 : 1;
        var lineTotal = dto.Quantity * dto.UnitPrice * (1 + dto.VatPct / 100);

        var line = new OrderLine
        {
            OrderId   = orderId,
            ProductId = dto.ProductId,
            LineNum   = lineNum,
            Quantity  = dto.Quantity,
            UnitPrice = dto.UnitPrice,
            VatPct    = dto.VatPct,
            LineTotal = lineTotal
        };

        _db.OrderLines.Add(line);

        // Update order totals
        order.DocTotal += lineTotal;
        order.VatTotal += dto.Quantity * dto.UnitPrice * (dto.VatPct / 100);

        await _db.SaveChangesAsync();

        // Reload with Product for mapping
        await _db.Entry(line).Reference(l => l.Product).LoadAsync();

        return _mapper.Map<OrderLineDto>(line);
    }

    public async Task<OrderLineDto?> UpdateAsync(int id, UpdateOrderLineDto dto)
    {
        var line = await _db.OrderLines
            .Include(ol => ol.Order)
            .Include(ol => ol.Product)
            .FirstOrDefaultAsync(ol => ol.Id == id);

        if (line is null)
            return null;

        // Recalculate totals
        var oldLineTotal = line.LineTotal;
        var oldVatAmount = line.Quantity * line.UnitPrice * (line.VatPct / 100);

        line.Quantity  = dto.Quantity;
        line.UnitPrice = dto.UnitPrice;
        line.VatPct    = dto.VatPct;
        line.LineTotal = dto.Quantity * dto.UnitPrice * (1 + dto.VatPct / 100);

        var newVatAmount = dto.Quantity * dto.UnitPrice * (dto.VatPct / 100);

        // Update order totals
        line.Order.DocTotal = line.Order.DocTotal - oldLineTotal + line.LineTotal;
        line.Order.VatTotal = line.Order.VatTotal - oldVatAmount + newVatAmount;

        await _db.SaveChangesAsync();

        return _mapper.Map<OrderLineDto>(line);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var line = await _db.OrderLines
            .Include(ol => ol.Order)
            .FirstOrDefaultAsync(ol => ol.Id == id);

        if (line is null)
            return false;

        // Update order totals
        line.Order.DocTotal -= line.LineTotal;
        line.Order.VatTotal -= line.Quantity * line.UnitPrice * (line.VatPct / 100);

        _db.OrderLines.Remove(line);
        await _db.SaveChangesAsync();

        return true;
    }
}
