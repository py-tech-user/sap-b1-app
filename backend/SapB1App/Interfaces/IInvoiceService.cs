using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IInvoiceService
{
    Task<PagedResult<InvoiceDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo);
    Task<InvoiceDto?> GetByIdAsync(int id);
    Task<InvoiceDto> CreateAsync(CreateInvoiceDto dto);
    Task<InvoiceDto> CreateFromDeliveryNoteAsync(int deliveryNoteId);
    Task<InvoiceDto?> UpdateAsync(int id, CreateInvoiceDto dto);
    Task<InvoiceDto?> UpdateStatusAsync(int id, UpdateInvoiceStatusDto dto);
    Task<bool> DeleteAsync(int id);
}
