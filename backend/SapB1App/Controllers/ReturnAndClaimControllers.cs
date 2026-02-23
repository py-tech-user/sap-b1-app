using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AllRoles)]  // Tous les rôles peuvent gérer les retours
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _service;

    public ReturnsController(IReturnService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ReturnDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, customerId);
        return Ok(new ApiResponse<PagedResult<ReturnDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<ReturnDto>(false, "Retour non trouvé.", null));
        return Ok(new ApiResponse<ReturnDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> Create([FromBody] CreateReturnDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<ReturnDto>(true, "Retour créé avec succès.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<ReturnDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> Update(int id, [FromBody] UpdateReturnDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<ReturnDto>(false, "Retour non trouvé.", null));
        return Ok(new ApiResponse<ReturnDto>(true, "Retour mis à jour.", result));
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]  // Seuls Manager/Admin peuvent approuver
    public async Task<ActionResult<ApiResponse<ReturnDto>>> Approve(int id, [FromBody] ApproveReturnDto dto)
    {
        var approverId = int.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "1");
        var result = await _service.ApproveAsync(id, approverId, dto);
        if (result is null)
            return NotFound(new ApiResponse<ReturnDto>(false, "Retour non trouvé.", null));
        return Ok(new ApiResponse<ReturnDto>(true, dto.Approved ? "Retour approuvé." : "Retour rejeté.", result));
    }

    [HttpPost("{id:int}/receive")]
    public async Task<ActionResult<ApiResponse<ReturnDto>>> Receive(int id)
    {
        var result = await _service.ReceiveAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<ReturnDto>(false, "Impossible de réceptionner ce retour.", null));
        return Ok(new ApiResponse<ReturnDto>(true, "Marchandise réceptionnée.", result));
    }

    [HttpPost("{id:int}/process")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]  // Seuls Manager/Admin peuvent traiter
    public async Task<ActionResult<ApiResponse<ReturnDto>>> Process(int id)
    {
        var result = await _service.ProcessAsync(id);
        if (result is null)
            return BadRequest(new ApiResponse<ReturnDto>(false, "Impossible de traiter ce retour.", null));
        return Ok(new ApiResponse<ReturnDto>(true, "Retour traité et avoir généré.", result));
    }


    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return BadRequest(new ApiResponse<bool>(false, "Impossible de supprimer ce retour.", false));
        return Ok(new ApiResponse<bool>(true, "Retour supprimé.", true));
    }
}

[ApiController]
[Route("api/[controller]")]
public class ClaimsController : ControllerBase
{
    private readonly IClaimService _service;

    public ClaimsController(IClaimService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ClaimDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] int? customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, priority, customerId);
        return Ok(new ApiResponse<PagedResult<ClaimDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<ClaimDto>(false, "Réclamation non trouvée.", null));
        return Ok(new ApiResponse<ClaimDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> Create([FromBody] CreateClaimDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<ClaimDto>(true, "Réclamation créée avec succès.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<ClaimDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> Update(int id, [FromBody] UpdateClaimDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<ClaimDto>(false, "Réclamation non trouvée.", null));
        return Ok(new ApiResponse<ClaimDto>(true, "Réclamation mise à jour.", result));
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> AddComment(int id, [FromBody] AddClaimCommentDto dto)
    {
        var userId = 1; // TODO: Get from JWT
        var result = await _service.AddCommentAsync(id, userId, dto);
        if (result is null)
            return NotFound(new ApiResponse<ClaimDto>(false, "Réclamation non trouvée.", null));
        return Ok(new ApiResponse<ClaimDto>(true, "Commentaire ajouté.", result));
    }

    [HttpPost("{id:int}/assign/{userId:int}")]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> Assign(int id, int userId)
    {
        var result = await _service.AssignAsync(id, userId);
        if (result is null)
            return NotFound(new ApiResponse<ClaimDto>(false, "Réclamation non trouvée.", null));
        return Ok(new ApiResponse<ClaimDto>(true, "Réclamation assignée.", result));
    }

    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> Resolve(int id, [FromBody] string resolution)
    {
        var result = await _service.ResolveAsync(id, resolution);
        if (result is null)
            return NotFound(new ApiResponse<ClaimDto>(false, "Réclamation non trouvée.", null));
        return Ok(new ApiResponse<ClaimDto>(true, "Réclamation résolue.", result));
    }

    [HttpPost("{id:int}/close")]
    public async Task<ActionResult<ApiResponse<ClaimDto>>> Close(int id)
    {
        var result = await _service.CloseAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<ClaimDto>(false, "Réclamation non trouvée.", null));
        return Ok(new ApiResponse<ClaimDto>(true, "Réclamation clôturée.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return NotFound(new ApiResponse<bool>(false, "Réclamation non trouvée.", false));
        return Ok(new ApiResponse<bool>(true, "Réclamation supprimée.", true));
    }
}

[ApiController]
[Route("api/service-tickets")]
public class ServiceTicketsController : ControllerBase
{
    private readonly IServiceTicketService _service;

    public ServiceTicketsController(IServiceTicketService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ServiceTicketDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? customerId = null)
    {
        var result = await _service.GetAllAsync(page, pageSize, search, status, customerId);
        return Ok(new ApiResponse<PagedResult<ServiceTicketDto>>(true, null, result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result is null)
            return NotFound(new ApiResponse<ServiceTicketDto>(false, "Ticket SAV non trouvé.", null));
        return Ok(new ApiResponse<ServiceTicketDto>(true, null, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> Create([FromBody] CreateServiceTicketDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                new ApiResponse<ServiceTicketDto>(true, "Ticket SAV créé.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<ServiceTicketDto>(false, ex.Message, null));
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> Update(int id, [FromBody] UpdateServiceTicketDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        if (result is null)
            return NotFound(new ApiResponse<ServiceTicketDto>(false, "Ticket SAV non trouvé.", null));
        return Ok(new ApiResponse<ServiceTicketDto>(true, "Ticket SAV mis à jour.", result));
    }

    [HttpPost("{id:int}/parts")]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> AddPart(int id, [FromBody] AddServicePartDto dto)
    {
        try
        {
            var result = await _service.AddPartAsync(id, dto);
            if (result is null)
                return NotFound(new ApiResponse<ServiceTicketDto>(false, "Ticket SAV non trouvé.", null));
            return Ok(new ApiResponse<ServiceTicketDto>(true, "Pièce ajoutée.", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<ServiceTicketDto>(false, ex.Message, null));
        }
    }

    [HttpDelete("{id:int}/parts/{partId:int}")]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> RemovePart(int id, int partId)
    {
        var result = await _service.RemovePartAsync(id, partId);
        if (result is null)
            return NotFound(new ApiResponse<ServiceTicketDto>(false, "Ticket ou pièce non trouvé.", null));
        return Ok(new ApiResponse<ServiceTicketDto>(true, "Pièce retirée.", result));
    }

    [HttpPost("{id:int}/schedule")]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> Schedule(
        int id, [FromQuery] DateTime scheduledDate, [FromQuery] int? technicianId = null)
    {
        var result = await _service.ScheduleAsync(id, scheduledDate, technicianId);
        if (result is null)
            return NotFound(new ApiResponse<ServiceTicketDto>(false, "Ticket SAV non trouvé.", null));
        return Ok(new ApiResponse<ServiceTicketDto>(true, "Intervention planifiée.", result));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<ServiceTicketDto>>> Complete(int id, [FromBody] string resolution)
    {
        var result = await _service.CompleteAsync(id, resolution);
        if (result is null)
            return NotFound(new ApiResponse<ServiceTicketDto>(false, "Ticket SAV non trouvé.", null));
        return Ok(new ApiResponse<ServiceTicketDto>(true, "Intervention terminée.", result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result)
            return NotFound(new ApiResponse<bool>(false, "Ticket SAV non trouvé.", false));
        return Ok(new ApiResponse<bool>(true, "Ticket SAV supprimé.", true));
    }
}
