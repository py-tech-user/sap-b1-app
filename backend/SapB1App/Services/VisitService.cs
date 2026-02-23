using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class VisitService : IVisitService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public VisitService(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<PagedResult<VisitDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId)
    {
        var query = _db.Visits
            .Include(v => v.Customer)
            .Include(v => v.User)
            .AsQueryable();

        // Filter by customer
        if (customerId.HasValue)
            query = query.Where(v => v.CustomerId == customerId.Value);

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VisitStatus>(status, true, out var visitStatus))
            query = query.Where(v => v.Status == visitStatus);

        // Search by customer name or comments
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v =>
                v.Customer.CardName.Contains(search) ||
                (v.Comments != null && v.Comments.Contains(search)));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VisitDto
            {
                Id                = v.Id,
                CustomerId        = v.CustomerId,
                CustomerName      = v.Customer.CardName,
                CustomerCode      = v.Customer.CardCode,
                UserId            = v.UserId,
                UserName          = v.User != null ? v.User.Username : null,
                Date              = v.Date,
                Status            = v.Status.ToString(),
                Comments          = v.Comments,
                Latitude          = v.Latitude,
                Longitude         = v.Longitude,
                CheckInAt         = v.CheckInAt,
                CheckInLatitude   = v.CheckInLatitude,
                CheckInLongitude  = v.CheckInLongitude,
                CheckOutAt        = v.CheckOutAt,
                CheckOutLatitude  = v.CheckOutLatitude,
                CheckOutLongitude = v.CheckOutLongitude,
                DistanceKm        = v.DistanceKm,
                DurationMins      = v.CheckInAt.HasValue && v.CheckOutAt.HasValue 
                    ? (v.CheckOutAt.Value - v.CheckInAt.Value).TotalMinutes : null,
                SyncedToSap       = v.SyncedToSap,
                CreatedAt         = v.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<VisitDto>(items, totalCount, page, pageSize);
    }

    public async Task<VisitDto?> GetByIdAsync(int id)
    {
        var visit = await _db.Visits
            .Include(v => v.Customer)
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (visit is null)
            return null;

        return MapToDto(visit);
    }

    public async Task<VisitDto> CreateAsync(CreateVisitDto dto)
    {
        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new InvalidOperationException($"Customer ID {dto.CustomerId} not found.");

        if (!Enum.TryParse<VisitStatus>(dto.Status, true, out var status))
            status = VisitStatus.Planned;

        var visit = new Visit
        {
            CustomerId = dto.CustomerId,
            UserId     = dto.UserId,
            Date       = dto.Date,
            Status     = status,
            Comments   = dto.Comments,
            Latitude   = dto.Latitude,
            Longitude  = dto.Longitude
        };

        _db.Visits.Add(visit);
        await _db.SaveChangesAsync();

        visit.Customer = customer;
        return MapToDto(visit);
    }

    public async Task<VisitDto?> UpdateAsync(int id, UpdateVisitDto dto)
    {
        var visit = await _db.Visits
            .Include(v => v.Customer)
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (visit is null)
            return null;

        if (Enum.TryParse<VisitStatus>(dto.Status, true, out var status))
            visit.Status = status;

        visit.UserId    = dto.UserId ?? visit.UserId;
        visit.Date      = dto.Date;
        visit.Comments  = dto.Comments;
        visit.Latitude  = dto.Latitude;
        visit.Longitude = dto.Longitude;
        visit.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return MapToDto(visit);
    }

    private static VisitDto MapToDto(Visit visit) => new()
    {
        Id                = visit.Id,
        CustomerId        = visit.CustomerId,
        CustomerName      = visit.Customer?.CardName ?? string.Empty,
        CustomerCode      = visit.Customer?.CardCode ?? string.Empty,
        UserId            = visit.UserId,
        UserName          = visit.User?.Username,
        Date              = visit.Date,
        Status            = visit.Status.ToString(),
        Comments          = visit.Comments,
        Latitude          = visit.Latitude,
        Longitude         = visit.Longitude,
        CheckInAt         = visit.CheckInAt,
        CheckInLatitude   = visit.CheckInLatitude,
        CheckInLongitude  = visit.CheckInLongitude,
        CheckOutAt        = visit.CheckOutAt,
        CheckOutLatitude  = visit.CheckOutLatitude,
        CheckOutLongitude = visit.CheckOutLongitude,
        DistanceKm        = visit.DistanceKm,
        DurationMins      = visit.CheckInAt.HasValue && visit.CheckOutAt.HasValue 
            ? (visit.CheckOutAt.Value - visit.CheckInAt.Value).TotalMinutes : null,
        SyncedToSap       = visit.SyncedToSap,
        CreatedAt         = visit.CreatedAt
    };

    public async Task<bool> DeleteAsync(int id)
    {
        var visit = await _db.Visits.FindAsync(id);

        if (visit is null)
            return false;

        _db.Visits.Remove(visit);
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<VisitDto?> SyncToSapAsync(int id)
    {
        var visit = await _db.Visits
            .Include(v => v.Customer)
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (visit is null)
            return null;

        // TODO: Implement SAP B1 sync logic here
        visit.SyncedToSap = true;
        visit.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return MapToDto(visit);
    }
}
