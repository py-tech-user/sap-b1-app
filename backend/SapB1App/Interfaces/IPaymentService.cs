using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IPaymentService
{
    Task<PagedResult<PaymentDto>> GetAllAsync(
        int page, int pageSize, string? search, int? customerId, int? orderId);

    Task<PaymentDto?> GetByIdAsync(int id);

    Task<PaymentDto> CreateAsync(CreatePaymentDto dto);

    Task<PaymentDto?> UpdateAsync(int id, UpdatePaymentDto dto);

    Task<bool> DeleteAsync(int id);

    Task<PaymentDto?> SyncToSapAsync(int id);
}
