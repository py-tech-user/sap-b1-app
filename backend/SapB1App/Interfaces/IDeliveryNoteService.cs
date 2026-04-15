using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IDeliveryNoteService
{
    Task<PagedResult<DeliveryNoteDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo);
    Task<DeliveryNoteDto?> GetByIdAsync(int id);
    Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto dto);
    Task<DeliveryNoteDto> CreateFromOrderAsync(int orderId);
    Task<DeliveryNoteDto?> UpdateAsync(int id, CreateDeliveryNoteDto dto);
    Task<DeliveryNoteDto?> UpdateStatusAsync(int id, UpdateDeliveryNoteStatusDto dto);
    Task<bool> DeleteAsync(int id);
}
