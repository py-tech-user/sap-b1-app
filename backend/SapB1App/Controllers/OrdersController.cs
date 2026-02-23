using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;

    public OrdersController(IOrderService service)
    {
        _service = service;
    }

    /// <summary>Liste paginée des commandes avec filtres optionnels.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetAll(
        [FromQuery] int    page       = 1,
        [FromQuery] int    pageSize   = 20,
        [FromQuery] string? search    = null,
        [FromQuery] string? status    = null,
        [FromQuery] int?   customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, customerId);
        return Ok(new ApiResponse<PagedResult<OrderDto>>(
            true, null, result, result.TotalCount));
    }

    /// <summary>Récupérer une commande avec ses lignes par son ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetById(int id)
    {
        var order = await _service.GetByIdAsync(id);

        if (order is null)
            return NotFound(new ApiResponse<OrderDto>(
                false, $"Commande ID {id} introuvable.", null));

        return Ok(new ApiResponse<OrderDto>(true, null, order));
    }

    /// <summary>Créer une nouvelle commande avec ses lignes.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Create(
        [FromBody] CreateOrderDto? dto)
    {
        if (dto is null)
            return BadRequest(new ApiResponse<OrderDto>(
                false, "Les données de la commande sont requises.", null));

        try
        {
            var order = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = order.Id },
                new ApiResponse<OrderDto>(true, "Commande créée avec succès.", order));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<OrderDto>(false, ex.Message, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<OrderDto>(
                false, $"Erreur lors de la création de la commande : {ex.Message}", null));
        }
    }

    /// <summary>Mettre à jour le statut d'une commande (Draft → Confirmed → Shipped → Delivered).</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateStatus(
        int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var order = await _service.UpdateStatusAsync(id, dto);

        if (order is null)
            return NotFound(new ApiResponse<OrderDto>(
                false, $"Commande ID {id} introuvable.", null));

        return Ok(new ApiResponse<OrderDto>(true, "Statut mis à jour.", order));
    }

    /// <summary>Supprimer une commande (uniquement si statut = Draft). Rôle Admin requis.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound(new ApiResponse<object>(
                false, $"Commande ID {id} introuvable.", null));

        return Ok(new ApiResponse<object>(true, "Commande supprimée.", null));
    }

    /// <summary>Synchroniser une commande vers SAP Business One. Rôle Admin ou Manager requis.</summary>
    [HttpPost("{id:int}/sync-sap")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> SyncToSap(int id)
    {
        var order = await _service.SyncToSapAsync(id);

        if (order is null)
            return NotFound(new ApiResponse<OrderDto>(
                false, $"Commande ID {id} introuvable.", null));

        return Ok(new ApiResponse<OrderDto>(
            true, "Commande synchronisée avec SAP Business One.", order));
    }
}
