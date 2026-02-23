using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;
using System.Text.Json;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles peuvent accéder aux clients
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
    }

    /// <summary>Test endpoint - sans authentification.</summary>
    [HttpGet("test")]
    [AllowAnonymous]
    public IActionResult Test()
    {
        Console.WriteLine("=== TEST ENDPOINT ATTEINT ===");
        return Ok(new ApiResponse<string>(true, "Backend fonctionne!", "OK"));
    }

    /// <summary>Test POST - sans authentification.</summary>
    [HttpPost("test")]
    [AllowAnonymous]
    public IActionResult TestPost([FromBody] object? data)
    {
        Console.WriteLine("=== TEST POST ATTEINT ===");
        Console.WriteLine("Data reçue: " + JsonSerializer.Serialize(data));
        return Ok(new ApiResponse<object>(true, "POST reçu!", data));
    }

    /// <summary>Liste paginée des clients avec recherche optionnelle.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> GetAll(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 20,
        [FromQuery] string? search  = null,
        [FromQuery] bool?  isActive = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, isActive);
        return Ok(new ApiResponse<PagedResult<CustomerDto>>(
            true, null, result, result.TotalCount));
    }

    /// <summary>Récupérer un client par son ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetById(int id)
    {
        var customer = await _service.GetByIdAsync(id);

        if (customer is null)
            return NotFound(new ApiResponse<CustomerDto>(
                false, $"Client ID {id} introuvable.", null));

        return Ok(new ApiResponse<CustomerDto>(true, null, customer));
    }

    /// <summary>Créer un nouveau client.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create(
        [FromBody] CreateCustomerDto? dto)
    {
        Console.WriteLine("POST reçu : " + JsonSerializer.Serialize(dto));
        if (dto is null)
            return BadRequest(new ApiResponse<CustomerDto>(
                false, "Les données du client sont requises.", null));

        try
        {
            var customer = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = customer.Id },
                new ApiResponse<CustomerDto>(true, "Client créé avec succès.", customer));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<CustomerDto>(
                false, ex.Message, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<CustomerDto>(
                false, $"Erreur lors de la création du client : {ex.Message}", null));
        }
    }

    /// <summary>Mettre à jour les informations d'un client.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Update(
        int id, [FromBody] UpdateCustomerDto dto)
    {
        var customer = await _service.UpdateAsync(id, dto);

        if (customer is null)
            return NotFound(new ApiResponse<CustomerDto>(
                false, $"Client ID {id} introuvable.", null));

        return Ok(new ApiResponse<CustomerDto>(true, "Client mis à jour.", customer));
    }

    /// <summary>Désactiver un client (soft delete). Rôle Admin ou Manager requis.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound(new ApiResponse<object>(
                false, $"Client ID {id} introuvable.", null));

        return Ok(new ApiResponse<object>(true, "Client désactivé.", null));
    }

    /// <summary>Synchroniser un client vers SAP Business One. Rôle Admin ou Manager requis.</summary>
    [HttpPost("{id:int}/sync-sap")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> SyncToSap(int id)
    {
        var customer = await _service.SyncToSapAsync(id);

        if (customer is null)
            return NotFound(new ApiResponse<CustomerDto>(
                false, $"Client ID {id} introuvable.", null));

        return Ok(new ApiResponse<CustomerDto>(
            true, "Client synchronisé avec SAP Business One.", customer));
    }
}
