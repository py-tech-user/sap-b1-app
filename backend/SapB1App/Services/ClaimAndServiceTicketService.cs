using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

// ═══════════════════════════════════════════════════════════════════════════
// Service Réclamations
// ═══════════════════════════════════════════════════════════════════════════

public class ClaimService : IClaimService
{
    private readonly AppDbContext _db;

    public ClaimService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ClaimDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, string? priority, int? customerId)
    {
        var query = _db.Claims
            .Include(c => c.Customer)
            .Include(c => c.Order)
            .Include(c => c.Product)
            .Include(c => c.AssignedUser)
            .Include(c => c.Comments).ThenInclude(cc => cc.User)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(c => c.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ClaimStatus>(status, true, out var claimStatus))
            query = query.Where(c => c.Status == claimStatus);

        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<ClaimPriority>(priority, true, out var claimPriority))
            query = query.Where(c => c.Priority == claimPriority);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ClaimNumber.Contains(search) || c.Subject.Contains(search) || c.Customer.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.OpenDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return new PagedResult<ClaimDto>(items, totalCount, page, pageSize);
    }

    public async Task<ClaimDto?> GetByIdAsync(int id)
    {
        var claim = await _db.Claims
            .Include(c => c.Customer)
            .Include(c => c.Order)
            .Include(c => c.Product)
            .Include(c => c.AssignedUser)
            .Include(c => c.Comments).ThenInclude(cc => cc.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        return claim is null ? null : MapToDto(claim);
    }

    public async Task<ClaimDto> CreateAsync(CreateClaimDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Customer {dto.CustomerId} not found.");

        var claimNumber = await GenerateClaimNumberAsync();

        Enum.TryParse<ClaimType>(dto.Type, true, out var claimType);
        Enum.TryParse<ClaimPriority>(dto.Priority, true, out var priority);

        var claim = new Claim
        {
            ClaimNumber = claimNumber,
            CustomerId = dto.CustomerId,
            OrderId = dto.OrderId,
            ProductId = dto.ProductId,
            Type = claimType,
            Priority = priority,
            Status = ClaimStatus.Open,
            Subject = dto.Subject,
            Description = dto.Description,
            AssignedTo = dto.AssignedTo
        };

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(claim.Id) ?? throw new InvalidOperationException("Failed to create claim.");
    }

    public async Task<ClaimDto?> UpdateAsync(int id, UpdateClaimDto dto)
    {
        var claim = await _db.Claims.FindAsync(id);
        if (claim is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<ClaimStatus>(dto.Status, true, out var status))
            claim.Status = status;

        if (!string.IsNullOrWhiteSpace(dto.Priority) && Enum.TryParse<ClaimPriority>(dto.Priority, true, out var priority))
            claim.Priority = priority;

        claim.Resolution = dto.Resolution ?? claim.Resolution;
        claim.AssignedTo = dto.AssignedTo ?? claim.AssignedTo;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ClaimDto?> AddCommentAsync(int id, int userId, AddClaimCommentDto dto)
    {
        var claim = await _db.Claims.FindAsync(id);
        if (claim is null) return null;

        var comment = new ClaimComment
        {
            ClaimId = id,
            UserId = userId,
            Comment = dto.Comment,
            IsInternal = dto.IsInternal
        };

        _db.ClaimComments.Add(comment);
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ClaimDto?> AssignAsync(int id, int userId)
    {
        var claim = await _db.Claims.FindAsync(id);
        if (claim is null) return null;

        claim.AssignedTo = userId;
        claim.Status = ClaimStatus.InProgress;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ClaimDto?> ResolveAsync(int id, string resolution)
    {
        var claim = await _db.Claims.FindAsync(id);
        if (claim is null) return null;

        claim.Status = ClaimStatus.Resolved;
        claim.Resolution = resolution;
        claim.ResolvedDate = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ClaimDto?> CloseAsync(int id)
    {
        var claim = await _db.Claims.FindAsync(id);
        if (claim is null) return null;

        claim.Status = ClaimStatus.Closed;
        claim.ClosedDate = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var claim = await _db.Claims.FindAsync(id);
        if (claim is null) return false;

        _db.Claims.Remove(claim);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateClaimNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.Claims.CountAsync(c => c.OpenDate.Year == year) + 1;
        return $"CLM-{year}-{count:D4}";
    }

    private static ClaimDto MapToDto(Claim c) => new()
    {
        Id = c.Id,
        ClaimNumber = c.ClaimNumber,
        CustomerId = c.CustomerId,
        CustomerName = c.Customer?.CardName ?? "",
        CustomerCode = c.Customer?.CardCode ?? "",
        OrderId = c.OrderId,
        OrderDocNum = c.Order?.DocNum,
        ProductId = c.ProductId,
        ProductName = c.Product?.ItemName,
        Type = c.Type.ToString(),
        Priority = c.Priority.ToString(),
        Status = c.Status.ToString(),
        Subject = c.Subject,
        Description = c.Description,
        Resolution = c.Resolution,
        OpenDate = c.OpenDate,
        ResolvedDate = c.ResolvedDate,
        ClosedDate = c.ClosedDate,
        AssignedTo = c.AssignedTo,
        AssignedToName = c.AssignedUser?.FullName,
        ReturnId = c.ReturnId,
        CreditNoteId = c.CreditNoteId,
        ServiceTicketId = c.ServiceTicketId,
        CreatedAt = c.CreatedAt,
        Comments = c.Comments.Select(cc => new ClaimCommentDto
        {
            Id = cc.Id,
            ClaimId = cc.ClaimId,
            UserId = cc.UserId,
            UserName = cc.User?.Username ?? "",
            Comment = cc.Comment,
            IsInternal = cc.IsInternal,
            CreatedAt = cc.CreatedAt
        }).ToList()
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// Service SAV
// ═══════════════════════════════════════════════════════════════════════════

public class ServiceTicketService : IServiceTicketService
{
    private readonly AppDbContext _db;

    public ServiceTicketService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ServiceTicketDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId)
    {
        var query = _db.ServiceTickets
            .Include(st => st.Customer)
            .Include(st => st.Product)
            .Include(st => st.Technician)
            .Include(st => st.Parts).ThenInclude(p => p.Product)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(st => st.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ServiceTicketStatus>(status, true, out var ticketStatus))
            query = query.Where(st => st.Status == ticketStatus);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(st => st.TicketNumber.Contains(search) || st.Customer.CardName.Contains(search));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(st => st.OpenDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(st => MapToDto(st))
            .ToListAsync();

        return new PagedResult<ServiceTicketDto>(items, totalCount, page, pageSize);
    }

    public async Task<ServiceTicketDto?> GetByIdAsync(int id)
    {
        var ticket = await _db.ServiceTickets
            .Include(st => st.Customer)
            .Include(st => st.Product)
            .Include(st => st.Technician)
            .Include(st => st.Parts).ThenInclude(p => p.Product)
            .FirstOrDefaultAsync(st => st.Id == id);

        return ticket is null ? null : MapToDto(ticket);
    }

    public async Task<ServiceTicketDto> CreateAsync(CreateServiceTicketDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Customer {dto.CustomerId} not found.");

        var ticketNumber = await GenerateTicketNumberAsync();

        Enum.TryParse<ServiceType>(dto.Type, true, out var serviceType);
        Enum.TryParse<ClaimPriority>(dto.Priority, true, out var priority);

        var ticket = new ServiceTicket
        {
            TicketNumber = ticketNumber,
            CustomerId = dto.CustomerId,
            ProductId = dto.ProductId,
            SerialNumber = dto.SerialNumber,
            Type = serviceType,
            Priority = priority,
            Status = ServiceTicketStatus.Open,
            Description = dto.Description,
            AssignedTo = dto.AssignedTo,
            ClaimId = dto.ClaimId,
            UnderWarranty = dto.UnderWarranty
        };

        _db.ServiceTickets.Add(ticket);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(ticket.Id) ?? throw new InvalidOperationException("Failed to create service ticket.");
    }

    public async Task<ServiceTicketDto?> UpdateAsync(int id, UpdateServiceTicketDto dto)
    {
        var ticket = await _db.ServiceTickets.FindAsync(id);
        if (ticket is null) return null;

        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<ServiceTicketStatus>(dto.Status, true, out var status))
            ticket.Status = status;

        if (!string.IsNullOrWhiteSpace(dto.Priority) && Enum.TryParse<ClaimPriority>(dto.Priority, true, out var priority))
            ticket.Priority = priority;

        ticket.Diagnosis = dto.Diagnosis ?? ticket.Diagnosis;
        ticket.Resolution = dto.Resolution ?? ticket.Resolution;
        ticket.ScheduledDate = dto.ScheduledDate ?? ticket.ScheduledDate;
        ticket.AssignedTo = dto.AssignedTo ?? ticket.AssignedTo;
        ticket.LaborCost = dto.LaborCost ?? ticket.LaborCost;
        ticket.UpdatedAt = DateTime.UtcNow;

        ticket.TotalCost = ticket.LaborCost + ticket.PartsCost;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ServiceTicketDto?> AddPartAsync(int id, AddServicePartDto dto)
    {
        var ticket = await _db.ServiceTickets.Include(t => t.Parts).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return null;

        var product = await _db.Products.FindAsync(dto.ProductId)
            ?? throw new InvalidOperationException($"Product {dto.ProductId} not found.");

        var unitPrice = dto.UnitPrice > 0 ? dto.UnitPrice : product.Price;
        var part = new ServicePart
        {
            ServiceTicketId = id,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            UnitPrice = unitPrice,
            LineTotal = dto.Quantity * unitPrice
        };

        ticket.Parts.Add(part);
        ticket.PartsCost = ticket.Parts.Sum(p => p.LineTotal);
        ticket.TotalCost = ticket.LaborCost + ticket.PartsCost;
        ticket.UpdatedAt = DateTime.UtcNow;

        // Déduire du stock
        product.Stock -= (int)dto.Quantity;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ServiceTicketDto?> RemovePartAsync(int id, int partId)
    {
        var ticket = await _db.ServiceTickets.Include(t => t.Parts).FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return null;

        var part = ticket.Parts.FirstOrDefault(p => p.Id == partId);
        if (part is null) return null;

        // Remettre en stock
        var product = await _db.Products.FindAsync(part.ProductId);
        if (product != null)
            product.Stock += (int)part.Quantity;

        ticket.Parts.Remove(part);
        ticket.PartsCost = ticket.Parts.Sum(p => p.LineTotal);
        ticket.TotalCost = ticket.LaborCost + ticket.PartsCost;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ServiceTicketDto?> ScheduleAsync(int id, DateTime scheduledDate, int? technicianId)
    {
        var ticket = await _db.ServiceTickets.FindAsync(id);
        if (ticket is null) return null;

        ticket.Status = ServiceTicketStatus.Scheduled;
        ticket.ScheduledDate = scheduledDate;
        ticket.AssignedTo = technicianId ?? ticket.AssignedTo;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ServiceTicketDto?> CompleteAsync(int id, string resolution)
    {
        var ticket = await _db.ServiceTickets.FindAsync(id);
        if (ticket is null) return null;

        ticket.Status = ServiceTicketStatus.Completed;
        ticket.Resolution = resolution;
        ticket.CompletedDate = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var ticket = await _db.ServiceTickets.FindAsync(id);
        if (ticket is null) return false;

        _db.ServiceTickets.Remove(ticket);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<string> GenerateTicketNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await _db.ServiceTickets.CountAsync(st => st.OpenDate.Year == year) + 1;
        return $"SAV-{year}-{count:D4}";
    }

    private static ServiceTicketDto MapToDto(ServiceTicket st) => new()
    {
        Id = st.Id,
        TicketNumber = st.TicketNumber,
        CustomerId = st.CustomerId,
        CustomerName = st.Customer?.CardName ?? "",
        CustomerCode = st.Customer?.CardCode ?? "",
        ProductId = st.ProductId,
        ProductName = st.Product?.ItemName,
        SerialNumber = st.SerialNumber,
        Type = st.Type.ToString(),
        Status = st.Status.ToString(),
        Priority = st.Priority.ToString(),
        Description = st.Description,
        Diagnosis = st.Diagnosis,
        Resolution = st.Resolution,
        OpenDate = st.OpenDate,
        ScheduledDate = st.ScheduledDate,
        CompletedDate = st.CompletedDate,
        AssignedTo = st.AssignedTo,
        TechnicianName = st.Technician?.FullName,
        LaborCost = st.LaborCost,
        PartsCost = st.PartsCost,
        TotalCost = st.TotalCost,
        UnderWarranty = st.UnderWarranty,
        ClaimId = st.ClaimId,
        CreatedAt = st.CreatedAt,
        Parts = st.Parts.Select(p => new ServicePartDto
        {
            Id = p.Id,
            ProductId = p.ProductId,
            ItemCode = p.Product?.ItemCode ?? "",
            ItemName = p.Product?.ItemName ?? "",
            Quantity = p.Quantity,
            UnitPrice = p.UnitPrice,
            LineTotal = p.LineTotal
        }).ToList()
    };
}
