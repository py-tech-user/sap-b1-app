using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public PaymentService(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<PagedResult<PaymentDto>> GetAllAsync(
        int page, int pageSize, string? search, int? customerId, int? orderId)
    {
        var query = _db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Order)
            .AsQueryable();

        // Filter by customer
        if (customerId.HasValue)
            query = query.Where(p => p.CustomerId == customerId.Value);

        // Filter by order
        if (orderId.HasValue)
            query = query.Where(p => p.OrderId == orderId.Value);

        // Search by customer name or reference
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Customer.CardName.Contains(search) ||
                (p.Reference != null && p.Reference.Contains(search)));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentDto
            {
                Id            = p.Id,
                CustomerId    = p.CustomerId,
                CustomerName  = p.Customer.CardName,
                CustomerCode  = p.Customer.CardCode,
                OrderId       = p.OrderId,
                OrderDocNum   = p.Order != null ? p.Order.DocNum : null,
                Amount        = p.Amount,
                PaymentDate   = p.PaymentDate,
                PaymentMethod = p.PaymentMethod.ToString(),
                Reference     = p.Reference,
                Comments      = p.Comments,
                SyncedToSap   = p.SyncedToSap,
                CreatedAt     = p.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<PaymentDto>(items, totalCount, page, pageSize);
    }

    public async Task<PaymentDto?> GetByIdAsync(int id)
    {
        var payment = await _db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
            return null;

        return new PaymentDto
        {
            Id            = payment.Id,
            CustomerId    = payment.CustomerId,
            CustomerName  = payment.Customer.CardName,
            CustomerCode  = payment.Customer.CardCode,
            OrderId       = payment.OrderId,
            OrderDocNum   = payment.Order?.DocNum,
            Amount        = payment.Amount,
            PaymentDate   = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Reference     = payment.Reference,
            Comments      = payment.Comments,
            SyncedToSap   = payment.SyncedToSap,
            CreatedAt     = payment.CreatedAt
        };
    }

    public async Task<PaymentDto> CreateAsync(CreatePaymentDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Customer ID {dto.CustomerId} not found.");

        Order? order = null;
        if (dto.OrderId.HasValue)
        {
            order = await _db.Orders.FindAsync(dto.OrderId.Value)
                ?? throw new InvalidOperationException($"Order ID {dto.OrderId.Value} not found.");
        }

        if (!Enum.TryParse<PaymentMethod>(dto.PaymentMethod, true, out var paymentMethod))
            paymentMethod = Models.PaymentMethod.Cash;

        var payment = new Payment
        {
            CustomerId    = dto.CustomerId,
            OrderId       = dto.OrderId,
            Amount        = dto.Amount,
            PaymentDate   = dto.PaymentDate,
            PaymentMethod = paymentMethod,
            Reference     = dto.Reference,
            Comments      = dto.Comments
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        return new PaymentDto
        {
            Id            = payment.Id,
            CustomerId    = payment.CustomerId,
            CustomerName  = customer.CardName,
            CustomerCode  = customer.CardCode,
            OrderId       = payment.OrderId,
            OrderDocNum   = order?.DocNum,
            Amount        = payment.Amount,
            PaymentDate   = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Reference     = payment.Reference,
            Comments      = payment.Comments,
            SyncedToSap   = payment.SyncedToSap,
            CreatedAt     = payment.CreatedAt
        };
    }

    public async Task<PaymentDto?> UpdateAsync(int id, UpdatePaymentDto dto)
    {
        var payment = await _db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
            return null;

        if (Enum.TryParse<PaymentMethod>(dto.PaymentMethod, true, out var paymentMethod))
            payment.PaymentMethod = paymentMethod;

        payment.Amount      = dto.Amount;
        payment.PaymentDate = dto.PaymentDate;
        payment.Reference   = dto.Reference;
        payment.Comments    = dto.Comments;
        payment.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new PaymentDto
        {
            Id            = payment.Id,
            CustomerId    = payment.CustomerId,
            CustomerName  = payment.Customer.CardName,
            CustomerCode  = payment.Customer.CardCode,
            OrderId       = payment.OrderId,
            OrderDocNum   = payment.Order?.DocNum,
            Amount        = payment.Amount,
            PaymentDate   = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Reference     = payment.Reference,
            Comments      = payment.Comments,
            SyncedToSap   = payment.SyncedToSap,
            CreatedAt     = payment.CreatedAt
        };
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var payment = await _db.Payments.FindAsync(id);

        if (payment is null)
            return false;

        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<PaymentDto?> SyncToSapAsync(int id)
    {
        var payment = await _db.Payments
            .Include(p => p.Customer)
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null)
            return null;

        // TODO: Implement SAP B1 sync logic here
        payment.SyncedToSap = true;
        payment.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new PaymentDto
        {
            Id            = payment.Id,
            CustomerId    = payment.CustomerId,
            CustomerName  = payment.Customer.CardName,
            CustomerCode  = payment.Customer.CardCode,
            OrderId       = payment.OrderId,
            OrderDocNum   = payment.Order?.DocNum,
            Amount        = payment.Amount,
            PaymentDate   = payment.PaymentDate,
            PaymentMethod = payment.PaymentMethod.ToString(),
            Reference     = payment.Reference,
            Comments      = payment.Comments,
            SyncedToSap   = payment.SyncedToSap,
            CreatedAt     = payment.CreatedAt
        };
    }
}
