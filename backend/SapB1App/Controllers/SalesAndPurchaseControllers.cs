using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/delivery-notes")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles
public class DeliveryNotesController : ControllerBase
{
    private readonly IDeliveryNoteService _service;

    public DeliveryNotesController(IDeliveryNoteService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DeliveryNoteDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, customerId);
        return Ok(new ApiResponse<PagedResult<DeliveryNoteDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<DeliveryNoteDto>(false, "Bon de livraison non trouvé.", null));
        return Ok(new ApiResponse<DeliveryNoteDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDto>>> Create([FromBody] CreateDeliveryNoteDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<DeliveryNoteDto>(true, "Bon de livraison créé.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<DeliveryNoteDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDto>>> Update(int id, [FromBody] UpdateDeliveryNoteDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<DeliveryNoteDto>(false, "Bon de livraison non trouvé.", null));
        return Ok(new ApiResponse<DeliveryNoteDto>(true, "Bon de livraison mis à jour.", result));
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDto>>> Confirm(int id)
    {
        var result = await _service.ConfirmAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<DeliveryNoteDto>(false, "Impossible de confirmer ce BL.", null));
        return Ok(new ApiResponse<DeliveryNoteDto>(true, "Bon de livraison confirmé.", result));
    }

    [HttpPost("{id:int}/ship")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDto>>> Ship(int id, [FromQuery] string? trackingNumber = null)
    {
        var result = await _service.ShipAsync(id, trackingNumber);
        if (result is null)
            return BadRequest(new ApiResponse<DeliveryNoteDto>(false, "Impossible d'expédier ce BL.", null));
        return Ok(new ApiResponse<DeliveryNoteDto>(true, "Livraison expédiée.", result));
    }

    [HttpPost("{id:int}/deliver")]
    public async Task<ActionResult<ApiResponse<DeliveryNoteDto>>> Deliver(int id, [FromQuery] string? receivedBy = null)
    {
        var result = await _service.DeliverAsync(id, receivedBy);
        if (result is null)
            return BadRequest(new ApiResponse<DeliveryNoteDto>(false, "Impossible de marquer comme livré.", null));
        return Ok(new ApiResponse<DeliveryNoteDto>(true, "Livraison effectuée.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return BadRequest(new ApiResponse<bool>(false, "Impossible de supprimer ce BL.", false));
        return Ok(new ApiResponse<bool>(true, "Bon de livraison supprimé.", true));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.ManagerOrAdmin)]  // Manager ou Admin pour les fournisseurs
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _service;

    public SuppliersController(ISupplierService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search);
        return Ok(new ApiResponse<PagedResult<SupplierDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<SupplierDto>(false, "Fournisseur non trouvé.", null));
        return Ok(new ApiResponse<SupplierDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create([FromBody] CreateSupplierDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<SupplierDto>(true, "Fournisseur créé.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<SupplierDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Update(int id, [FromBody] UpdateSupplierDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<SupplierDto>(false, "Fournisseur non trouvé.", null));
        return Ok(new ApiResponse<SupplierDto>(true, "Fournisseur mis à jour.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return NotFound(new ApiResponse<bool>(false, "Fournisseur non trouvé.", false));
        return Ok(new ApiResponse<bool>(true, "Fournisseur supprimé.", true));
    }
}

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Policy = Policies.ManagerOrAdmin)]  // Manager ou Admin pour les achats
public class PurchaseOrdersController : ControllerBase
{
    private readonly IPurchaseOrderService _service;

    public PurchaseOrdersController(IPurchaseOrderService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseOrderDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? supplierId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, supplierId);
        return Ok(new ApiResponse<PagedResult<PurchaseOrderDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<PurchaseOrderDto>(false, "Bon de commande non trouvé.", null));
        return Ok(new ApiResponse<PurchaseOrderDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Create([FromBody] CreatePurchaseOrderDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<PurchaseOrderDto>(true, "Bon de commande créé.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<PurchaseOrderDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Update(int id, [FromBody] UpdatePurchaseOrderDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<PurchaseOrderDto>(false, "Bon de commande non trouvé.", null));
        return Ok(new ApiResponse<PurchaseOrderDto>(true, "Bon de commande mis à jour.", result));
    }

    [HttpPost("{id:int}/send")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Send(int id)
    {
        var result = await _service.SendAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<PurchaseOrderDto>(false, "Impossible d'envoyer ce BC.", null));
        return Ok(new ApiResponse<PurchaseOrderDto>(true, "Bon de commande envoyé.", result));
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Confirm(int id)
    {
        var result = await _service.ConfirmAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<PurchaseOrderDto>(false, "Impossible de confirmer ce BC.", null));
        return Ok(new ApiResponse<PurchaseOrderDto>(true, "Bon de commande confirmé.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return BadRequest(new ApiResponse<bool>(false, "Impossible de supprimer ce BC.", false));
        return Ok(new ApiResponse<bool>(true, "Bon de commande supprimé.", true));
    }
}

[ApiController]
[Route("api/credit-notes")]
[Authorize(Policy = Policies.ManagerOrAdmin)]  // Manager ou Admin pour les avoirs
public class CreditNotesController : ControllerBase
{
    private readonly ICreditNoteService _service;

    public CreditNotesController(ICreditNoteService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CreditNoteDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, customerId);
        return Ok(new ApiResponse<PagedResult<CreditNoteDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<CreditNoteDto>(false, "Avoir non trouvé.", null));
        return Ok(new ApiResponse<CreditNoteDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> Create([FromBody] CreateCreditNoteDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<CreditNoteDto>(true, "Avoir créé.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<CreditNoteDto>(false, ex.Message, null));
        }
    }

    [HttpPost("from-return/{returnId:int}")]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> CreateFromReturn(int returnId)
    {
        try
        {
            var result = await _service.CreateFromReturnAsync(returnId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<CreditNoteDto>(true, "Avoir créé depuis le retour.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<CreditNoteDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> Update(int id, [FromBody] UpdateCreditNoteDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<CreditNoteDto>(false, "Avoir non trouvé.", null));
        return Ok(new ApiResponse<CreditNoteDto>(true, "Avoir mis à jour.", result));
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> Confirm(int id)
    {
        var result = await _service.ConfirmAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<CreditNoteDto>(false, "Impossible de confirmer cet avoir.", null));
        return Ok(new ApiResponse<CreditNoteDto>(true, "Avoir confirmé.", result));
    }

    [HttpPost("{id:int}/apply")]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> Apply(int id, [FromQuery] int? invoiceId = null)
    {
        var result = await _service.ApplyAsync(id, invoiceId);
        if (result is null)
            return BadRequest(new ApiResponse<CreditNoteDto>(false, "Impossible d'appliquer cet avoir.", null));
        return Ok(new ApiResponse<CreditNoteDto>(true, "Avoir appliqué.", result));
    }

    [HttpPost("{id:int}/refund")]
    public async Task<ActionResult<ApiResponse<CreditNoteDto>>> Refund(int id)
    {
        var result = await _service.RefundAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<CreditNoteDto>(false, "Impossible de rembourser.", null));
        return Ok(new ApiResponse<CreditNoteDto>(true, "Remboursement effectué.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return BadRequest(new ApiResponse<bool>(false, "Impossible de supprimer cet avoir.", false));
        return Ok(new ApiResponse<bool>(true, "Avoir supprimé.", true));
    }
}

[ApiController]
[Route("api/goods-receipts")]
[Authorize(Policy = Policies.ManagerOrAdmin)]  // Manager ou Admin pour les réceptions
public class GoodsReceiptsController : ControllerBase
{
    private readonly IGoodsReceiptService _service;

    public GoodsReceiptsController(IGoodsReceiptService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<GoodsReceiptDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? supplierId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, supplierId);
        return Ok(new ApiResponse<PagedResult<GoodsReceiptDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<GoodsReceiptDto>(false, "Réception non trouvée.", null));
        return Ok(new ApiResponse<GoodsReceiptDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> Create([FromBody] CreateGoodsReceiptDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<GoodsReceiptDto>(true, "Réception créée.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<GoodsReceiptDto>(false, ex.Message, null));
        }
    }

    [HttpPost("from-purchase-order/{purchaseOrderId:int}")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> CreateFromPurchaseOrder(
        int purchaseOrderId, [FromBody] List<CreateGoodsReceiptLineDto> lines)
    {
        try
        {
            var result = await _service.CreateFromPurchaseOrderAsync(purchaseOrderId, lines);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<GoodsReceiptDto>(true, "Réception créée depuis BC.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<GoodsReceiptDto>(false, ex.Message, null));
        }
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<GoodsReceiptDto>>> Confirm(int id)
    {
        var result = await _service.ConfirmAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<GoodsReceiptDto>(false, "Impossible de confirmer.", null));
        return Ok(new ApiResponse<GoodsReceiptDto>(true, "Réception confirmée et stock mis à jour.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return BadRequest(new ApiResponse<bool>(false, "Impossible de supprimer.", false));
        return Ok(new ApiResponse<bool>(true, "Réception supprimée.", true));
    }
}
