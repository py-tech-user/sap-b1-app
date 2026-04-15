using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface ICustomerService
{
    CustomerOptionsDto GetOptions();

    Task<PagedResult<CustomerDto>> GetAllAsync(
        int page, int pageSize, string? search, string? partnerType);

    Task<CustomerDto?> GetByIdAsync(int id);

    Task<CustomerDto>  CreateAsync(CreateCustomerDto dto);

    Task<CustomerDto?> UpdateAsync(int id, UpdateCustomerDto dto);

    Task<bool>         DeleteAsync(int id);

    Task<CustomerDto?> SyncToSapAsync(int id);
}
