using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IReturnService
{
    Task<PagedResult<ReturnDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo);
    Task<ReturnDto?> GetByIdAsync(int id);
    Task<ReturnDto> CreateAsync(CreateReturnDto dto);
    Task<ReturnDto?> UpdateAsync(int id, CreateReturnDto dto);
    Task<ReturnDto?> UpdateStatusAsync(int id, UpdateReturnStatusDto dto);
    Task<bool> DeleteAsync(int id);
}
