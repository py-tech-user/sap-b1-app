using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _service;

    public PaymentsController(IPaymentService service)
    {
        _service = service;
    }

    /// <summary>Liste paginée des paiements avec filtres optionnels.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PaymentDto>>>> GetAll(
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 20,
        [FromQuery] string? search     = null,
        [FromQuery] int?    customerId = null,
        [FromQuery] int?    orderId    = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, customerId, orderId);
        return Ok(new ApiResponse<PagedResult<PaymentDto>>(
            true, null, result, result.TotalCount));
    }

    /// <summary>Récupérer un paiement par son ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetById(int id)
    {
        var payment = await _service.GetByIdAsync(id);

        if (payment is null)
            return NotFound(new ApiResponse<PaymentDto>(
                false, $"Paiement ID {id} introuvable.", null));

        return Ok(new ApiResponse<PaymentDto>(true, null, payment));
    }

    /// <summary>Créer un nouveau paiement.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Create(
        [FromBody] CreatePaymentDto? dto)
    {
        if (dto is null)
            return BadRequest(new ApiResponse<PaymentDto>(
                false, "Les données du paiement sont requises.", null));

        try
        {
            var payment = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = payment.Id },
                new ApiResponse<PaymentDto>(true, "Paiement créé avec succès.", payment));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<PaymentDto>(false, ex.Message, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<PaymentDto>(
                false, $"Erreur lors de la création du paiement : {ex.Message}", null));
        }
    }

    /// <summary>Mettre à jour un paiement.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Update(
        int id, [FromBody] UpdatePaymentDto dto)
    {
        var payment = await _service.UpdateAsync(id, dto);

        if (payment is null)
            return NotFound(new ApiResponse<PaymentDto>(
                false, $"Paiement ID {id} introuvable.", null));

        return Ok(new ApiResponse<PaymentDto>(true, "Paiement mis à jour.", payment));
    }

    /// <summary>Supprimer un paiement. Rôle Admin ou Manager requis.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound(new ApiResponse<object>(
                false, $"Paiement ID {id} introuvable.", null));

        return Ok(new ApiResponse<object>(true, "Paiement supprimé.", null));
    }

    /// <summary>Synchroniser un paiement vers SAP Business One. Rôle Admin ou Manager requis.</summary>
    [HttpPost("{id:int}/sync-sap")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> SyncToSap(int id)
    {
        var payment = await _service.SyncToSapAsync(id);

        if (payment is null)
            return NotFound(new ApiResponse<PaymentDto>(
                false, $"Paiement ID {id} introuvable.", null));

        return Ok(new ApiResponse<PaymentDto>(
            true, "Paiement synchronisé avec SAP Business One.", payment));
    }
}
