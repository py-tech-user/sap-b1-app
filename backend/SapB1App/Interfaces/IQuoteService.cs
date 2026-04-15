using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IQuoteService
{
    Task<PagedResult<QuoteDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId, DateTime? dateFrom, DateTime? dateTo);
    Task<QuoteDto?> GetByIdAsync(int id);
    Task<QuoteDto> CreateAsync(CreateQuoteDto dto);
    Task<QuoteDto?> UpdateAsync(int id, CreateQuoteDto dto);
    Task<QuoteDto?> UpdateStatusAsync(int id, UpdateQuoteStatusDto dto);
    Task<bool> DeleteAsync(int id);
}
