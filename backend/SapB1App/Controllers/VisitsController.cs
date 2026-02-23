using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles
public class VisitsController : ControllerBase
{
    private readonly IVisitService _service;

    public VisitsController(IVisitService service)
    {
        _service = service;
    }

    /// <summary>Liste paginée des visites avec filtres optionnels.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<VisitDto>>>> GetAll(
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 20,
        [FromQuery] string? search     = null,
        [FromQuery] string? status     = null,
        [FromQuery] int?    customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, customerId);
        return Ok(new ApiResponse<PagedResult<VisitDto>>(
            true, null, result, result.TotalCount));
    }

    /// <summary>Récupérer une visite par son ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> GetById(int id)
    {
        var visit = await _service.GetByIdAsync(id);

        if (visit is null)
            return NotFound(new ApiResponse<VisitDto>(
                false, $"Visite ID {id} introuvable.", null));

        return Ok(new ApiResponse<VisitDto>(true, null, visit));
    }

    /// <summary>Créer une nouvelle visite.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Create(
        [FromBody] CreateVisitDto? dto)
    {
        if (dto is null)
            return BadRequest(new ApiResponse<VisitDto>(
                false, "Les données de la visite sont requises.", null));

        try
        {
            var visit = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = visit.Id },
                new ApiResponse<VisitDto>(true, "Visite créée avec succès.", visit));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<VisitDto>(false, ex.Message, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<VisitDto>(
                false, $"Erreur lors de la création de la visite : {ex.Message}", null));
        }
    }

    /// <summary>Mettre à jour une visite.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> Update(
        int id, [FromBody] UpdateVisitDto dto)
    {
        var visit = await _service.UpdateAsync(id, dto);

        if (visit is null)
            return NotFound(new ApiResponse<VisitDto>(
                false, $"Visite ID {id} introuvable.", null));

        return Ok(new ApiResponse<VisitDto>(true, "Visite mise à jour.", visit));
    }

    /// <summary>Supprimer une visite. Rôle Admin ou Manager requis.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound(new ApiResponse<object>(
                false, $"Visite ID {id} introuvable.", null));

        return Ok(new ApiResponse<object>(true, "Visite supprimée.", null));
    }

    /// <summary>Synchroniser une visite vers SAP Business One. Rôle Admin ou Manager requis.</summary>
    [HttpPost("{id:int}/sync-sap")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<VisitDto>>> SyncToSap(int id)
    {
        var visit = await _service.SyncToSapAsync(id);

        if (visit is null)
            return NotFound(new ApiResponse<VisitDto>(
                false, $"Visite ID {id} introuvable.", null));

        return Ok(new ApiResponse<VisitDto>(
            true, "Visite synchronisée avec SAP Business One.", visit));
    }
}
