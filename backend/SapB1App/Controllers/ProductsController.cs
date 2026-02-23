using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    /// <summary>Liste paginée des articles avec recherche et filtre catégorie.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductDto>>>> GetAll(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 20,
        [FromQuery] string? search  = null,
        [FromQuery] string? category = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, category);
        return Ok(new ApiResponse<PagedResult<ProductDto>>(
            true, null, result, result.TotalCount));
    }

    /// <summary>Récupérer un article par son ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> GetById(int id)
    {
        var product = await _service.GetByIdAsync(id);

        if (product is null)
            return NotFound(new ApiResponse<ProductDto>(
                false, $"Article ID {id} introuvable.", null));

        return Ok(new ApiResponse<ProductDto>(true, null, product));
    }

    /// <summary>Créer un nouvel article. Rôle Admin ou Manager requis.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create(
        [FromBody] CreateProductDto? dto)
    {
        if (dto is null)
            return BadRequest(new ApiResponse<ProductDto>(
                false, "Les données de l'article sont requises.", null));

        try
        {
            var product = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                new ApiResponse<ProductDto>(true, "Article créé avec succès.", product));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<ProductDto>(false, ex.Message, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<ProductDto>(
                false, $"Erreur lors de la création de l'article : {ex.Message}", null));
        }
    }

    /// <summary>Mettre à jour un article. Rôle Admin ou Manager requis.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Update(
        int id, [FromBody] UpdateProductDto dto)
    {
        var product = await _service.UpdateAsync(id, dto);

        if (product is null)
            return NotFound(new ApiResponse<ProductDto>(
                false, $"Article ID {id} introuvable.", null));

        return Ok(new ApiResponse<ProductDto>(true, "Article mis à jour.", product));
    }

    /// <summary>Désactiver un article (soft delete). Rôle Admin requis.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound(new ApiResponse<object>(
                false, $"Article ID {id} introuvable.", null));

        return Ok(new ApiResponse<object>(true, "Article désactivé.", null));
    }
}
