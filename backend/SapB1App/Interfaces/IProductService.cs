using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetAllAsync(
        int page, int pageSize, string? search, string? category);

    Task<ProductDto?> GetByIdAsync(int id);

    Task<ProductDto>  CreateAsync(CreateProductDto dto);

    Task<ProductDto?> UpdateAsync(int id, UpdateProductDto dto);

    Task<bool>        DeleteAsync(int id);
}
