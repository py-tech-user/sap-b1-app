using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IOrderLineService
{
    Task<IEnumerable<OrderLineDto>> GetByOrderIdAsync(int orderId);

    Task<OrderLineDto?> GetByIdAsync(int id);

    Task<OrderLineDto> CreateAsync(int orderId, CreateOrderLineDto dto);

    Task<OrderLineDto?> UpdateAsync(int id, UpdateOrderLineDto dto);

    Task<bool> DeleteAsync(int id);
}
