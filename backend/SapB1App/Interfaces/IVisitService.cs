using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IVisitService
{
    Task<PagedResult<VisitDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId);

    Task<VisitDto?> GetByIdAsync(int id);

    Task<VisitDto> CreateAsync(CreateVisitDto dto);

    Task<VisitDto?> UpdateAsync(int id, UpdateVisitDto dto);

    Task<bool> DeleteAsync(int id);

    Task<VisitDto?> SyncToSapAsync(int id);
}
