using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetAllAsync(
        int page, int pageSize, string? search, string? status, int? customerId);

    Task<OrderDto?> GetByIdAsync(int id);

    Task<OrderDto>  CreateAsync(CreateOrderDto dto);

    Task<OrderDto?> UpdateStatusAsync(int id, UpdateOrderStatusDto dto);

    Task<bool>      DeleteAsync(int id);

    Task<OrderDto?> SyncToSapAsync(int id);
}
