using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface ICreditNoteService
{
    Task<PagedResult<CreditNoteDto>> GetAllAsync(int page, int pageSize, string? search, int? invoiceId, DateTime? dateFrom, DateTime? dateTo);
    Task<CreditNoteDto?> GetByIdAsync(int id);
    Task<CreditNoteDto> CreateAsync(CreateCreditNoteDto dto);
    Task<bool> DeleteAsync(int id);
}
