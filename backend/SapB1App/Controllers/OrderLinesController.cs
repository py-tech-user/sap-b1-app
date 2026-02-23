using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/orders/{orderId:int}/lines")]
// [Authorize] // DÉSACTIVÉ pour le développement
public class OrderLinesController : ControllerBase
{
    private readonly IOrderLineService _service;

    public OrderLinesController(IOrderLineService service)
    {
        _service = service;
    }

    /// <summary>Liste des lignes d'une commande.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<OrderLineDto>>>> GetByOrderId(int orderId)
    {
        var lines = await _service.GetByOrderIdAsync(orderId);
        return Ok(new ApiResponse<IEnumerable<OrderLineDto>>(
            true, null, lines, lines.Count()));
    }

    /// <summary>Récupérer une ligne par son ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<OrderLineDto>>> GetById(int orderId, int id)
    {
        var line = await _service.GetByIdAsync(id);

        if (line is null)
            return NotFound(new ApiResponse<OrderLineDto>(
                false, $"Ligne ID {id} introuvable.", null));

        return Ok(new ApiResponse<OrderLineDto>(true, null, line));
    }

    /// <summary>Ajouter une ligne à une commande.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderLineDto>>> Create(
        int orderId,
        [FromBody] CreateOrderLineDto? dto)
    {
        if (dto is null)
            return BadRequest(new ApiResponse<OrderLineDto>(
                false, "Les données de la ligne sont requises.", null));

        try
        {
            var line = await _service.CreateAsync(orderId, dto);

            return CreatedAtAction(
                nameof(GetById),
                new { orderId, id = line.Id },
                new ApiResponse<OrderLineDto>(true, "Ligne créée avec succès.", line));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<OrderLineDto>(false, ex.Message, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<OrderLineDto>(
                false, $"Erreur lors de la création de la ligne : {ex.Message}", null));
        }
    }

    /// <summary>Mettre à jour une ligne de commande.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<OrderLineDto>>> Update(
        int orderId,
        int id,
        [FromBody] UpdateOrderLineDto dto)
    {
        var line = await _service.UpdateAsync(id, dto);

        if (line is null)
            return NotFound(new ApiResponse<OrderLineDto>(
                false, $"Ligne ID {id} introuvable.", null));

        return Ok(new ApiResponse<OrderLineDto>(true, "Ligne mise à jour.", line));
    }

    /// <summary>Supprimer une ligne de commande.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int orderId, int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound(new ApiResponse<object>(
                false, $"Ligne ID {id} introuvable.", null));

        return Ok(new ApiResponse<object>(true, "Ligne supprimée.", null));
    }
}
