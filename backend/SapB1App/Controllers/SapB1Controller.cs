using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SapB1App.DTOs;
using SapB1App.Interfaces;

namespace SapB1App.Controllers;

/// <summary>
/// Contrôleur pour les opérations SAP Business One via DI API.
/// </summary>
[ApiController]
[Route("api")]
public class SapB1Controller : ControllerBase
{
    private readonly ISapB1Service _sapService;
    private readonly ILogger<SapB1Controller> _logger;

    public SapB1Controller(ISapB1Service sapService, ILogger<SapB1Controller> logger)
    {
        _sapService = sapService;
        _logger = logger;
    }

    /// <summary>
    /// Teste la connexion à SAP Business One.
    /// </summary>
    [HttpGet("sap/test-connection")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> TestConnection()
    {
        try
        {
            var connected = await _sapService.TestConnectionAsync();

            if (connected)
            {
                return Ok(new ApiResponse<object>(true, "Connexion SAP B1 réussie!", new
                {
                    Status = "Connected",
                    Timestamp = DateTime.UtcNow
                }));
            }

            return BadRequest(new ApiResponse<object>(false, "Impossible de se connecter à SAP B1.", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SAP connection");
            return StatusCode(500, new ApiResponse<object>(false, ex.Message, null));
        }
    }

    [HttpGet("sap/clients")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ClientViewDto>>>> GetClients(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "BusinessPartners?$select=CardCode,CardName,Currency,CreditLimit",
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<ClientViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));
        }

        var items = MapBusinessPartners(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<ClientViewDto>>(true, null, items, items.Count));
    }

    [HttpGet("sap/partners")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<ClientViewDto>>>> GetPartners(CancellationToken cancellationToken)
        => GetClients(cancellationToken);

    [HttpPost("sap/clients")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ClientViewDto>>> CreateClient([FromBody] CreateSapClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || string.IsNullOrWhiteSpace(request.CardName))
        {
            return BadRequest(SapError("Code et nom client obligatoires."));
        }

        var payload = new
        {
            request.CardCode,
            request.CardName,
            request.Currency,
            CardType = "cCustomer"
        };

        var result = await _sapService.ServiceLayerPostAsync("BusinessPartners", payload, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage));
        }

        return StatusCode(result.StatusCode, new ApiResponse<ClientViewDto>(true, "Création réussie.", MapBusinessPartner(result.Response)));
    }

    [HttpPost("sap/partners")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<ClientViewDto>>> CreatePartner([FromBody] CreateSapClientRequest request, CancellationToken cancellationToken)
        => CreateClient(request, cancellationToken);

    [HttpGet("sap/test")]
    [AllowAnonymous]
    public async Task<IActionResult> TestSap(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync("BusinessPartners?$top=1", cancellationToken);

        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = "Connexion échouée", error = result.ErrorMessage });
        }

        return Ok(new { message = "Connexion OK" });
    }

    [HttpGet("sap/orders")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetOrders(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "Orders?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<DocumentViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));

        var items = MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    [HttpPost("sap/orders")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateOrder([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || request.DocumentLines.Count == 0)
            return BadRequest(SapError("Client et lignes sont obligatoires."));

        if (request.DocumentLines.Any(l =>
                string.IsNullOrWhiteSpace(l.ItemCode) ||
                l.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(l.WarehouseCode) ||
                GetLinePrice(l) <= 0))
            return BadRequest(SapError("Chaque ligne doit contenir ItemCode, Quantity > 0, WarehouseCode et Price."));

        if (!request.DocDueDate.HasValue && !request.RequiredDate.HasValue)
            return BadRequest(SapError("DocDueDate ou RequiredDate est obligatoire."));

        var currencyResult = await _sapService.ServiceLayerGetAsync(
            $"BusinessPartners('{EscapeODataString(request.CardCode)}')?$select=Currency",
            cancellationToken);

        if (!currencyResult.Success || currencyResult.Response is null)
            return StatusCode(currencyResult.StatusCode, SapError(currencyResult.ErrorMessage ?? "Impossible de récupérer la devise du client."));

        var docCurrency = GetString(currencyResult.Response.Value, "Currency");
        if (string.IsNullOrWhiteSpace(docCurrency))
            return BadRequest(SapError("Devise client introuvable pour ce CardCode."));

        var payload = BuildOrderPayload(request, docCurrency);
        var result = await _sapService.ServiceLayerPostAsync("Orders", payload, cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage));

        return StatusCode(result.StatusCode, new ApiResponse<object>(true, "Commande créée avec succès.", result.Response));
    }

    [HttpGet("sap/bc")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetBonCommandes(CancellationToken cancellationToken)
        => GetOrders(cancellationToken);

    [HttpPost("sap/bc")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateBonCommande([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
        => CreateOrder(request, cancellationToken);

    [HttpGet("sap/delivery-notes")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDeliveryNotes(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "DeliveryNotes?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<DocumentViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));

        var items = MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    [HttpPost("sap/delivery-notes")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateDeliveryNote([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || request.DocumentLines.Count == 0)
            return BadRequest(SapError("Client et lignes sont obligatoires."));

        var payload = BuildDocumentPayload(request);
        var result = await _sapService.ServiceLayerPostAsync("DeliveryNotes", payload, cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage));

        return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(true, "Création réussie.", MapDocument(result.Response)));
    }

    [HttpGet("sap/bl")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetBonsLivraison(CancellationToken cancellationToken)
        => GetDeliveryNotes(cancellationToken);

    [HttpPost("sap/bl")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateBonLivraison([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
        => CreateDeliveryNote(request, cancellationToken);

    [HttpGet("sap/quotes")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetQuotes(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "Quotations?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<DocumentViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));

        var items = MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    [HttpPost("sap/quotes")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateQuote([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || request.DocumentLines.Count == 0)
            return BadRequest(new ApiResponse<DocumentViewDto>(false, "Client et lignes sont obligatoires.", null));

        var payload = BuildDocumentPayload(request);
        var result = await _sapService.ServiceLayerPostAsync("Quotations", payload, cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(false, result.ErrorMessage ?? "Erreur de création.", null));

        return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(true, "Création réussie.", MapDocument(result.Response)));
    }

    [HttpGet("sap/devis")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDevis(CancellationToken cancellationToken)
        => GetQuotes(cancellationToken);

    [HttpPost("sap/devis")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateDevis([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
        => CreateQuote(request, cancellationToken);

    [HttpGet("sap/invoices")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetInvoices(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "Invoices?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<DocumentViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));

        var items = MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    [HttpPost("sap/invoices")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateInvoice([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || request.DocumentLines.Count == 0)
            return BadRequest(SapError("Client et lignes sont obligatoires."));

        var payload = BuildDocumentPayload(request);
        var result = await _sapService.ServiceLayerPostAsync("Invoices", payload, cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage));

        return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(true, "Création réussie.", MapDocument(result.Response)));
    }

    [HttpPost("sap/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginSap(CancellationToken cancellationToken)
    {
        var (success, _, _, _, errorMessage) = await _sapService.LoginServiceLayerWithSessionIdAsync(cancellationToken);
        if (!success)
        {
            return StatusCode(401, SapError(errorMessage));
        }

        return Ok(new { success = true, message = "Connexion établie." });
    }

    [HttpGet("sap/factures")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetFactures(CancellationToken cancellationToken)
        => GetInvoices(cancellationToken);

    [HttpPost("sap/factures")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateFacture([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
        => CreateInvoice(request, cancellationToken);

    [HttpGet("sap/credit-notes")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetCreditNotes(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "CreditNotes?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<DocumentViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));

        var items = MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    [HttpPost("sap/credit-notes")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateCreditNote([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || request.DocumentLines.Count == 0)
            return BadRequest(new ApiResponse<DocumentViewDto>(false, "Client et lignes sont obligatoires.", null));

        var payload = BuildDocumentPayload(request);
        var result = await _sapService.ServiceLayerPostAsync("CreditNotes", payload, cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(false, result.ErrorMessage ?? "Erreur de création.", null));

        return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(true, "Création réussie.", MapDocument(result.Response)));
    }

    [HttpGet("sap/returns")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetReturns(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync(
            "Returns?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocTotal,DocStatus",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<IReadOnlyList<DocumentViewDto>>(false, result.ErrorMessage ?? "Erreur de chargement.", null));

        var items = MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    [HttpPost("sap/returns")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<DocumentViewDto>>> CreateReturn([FromBody] CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || request.DocumentLines.Count == 0)
            return BadRequest(new ApiResponse<DocumentViewDto>(false, "Client et lignes sont obligatoires.", null));

        var payload = BuildDocumentPayload(request);
        var result = await _sapService.ServiceLayerPostAsync("Returns", payload, cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(false, result.ErrorMessage ?? "Erreur de création.", null));

        return StatusCode(result.StatusCode, new ApiResponse<DocumentViewDto>(true, "Création réussie.", MapDocument(result.Response)));
    }

    /// <summary>
    /// Test complet Service Layer : login + client + commande.
    /// </summary>
    [HttpPost("sap/service-layer/test-full")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> TestServiceLayerFull(CancellationToken cancellationToken)
    {
        try
        {
            var (success, loginResponse, customerResponse, orderResponse, statusCode, errorMessage) =
                await _sapService.RunFullServiceLayerTestAsync(cancellationToken);

            var message = success
                ? "Test Service Layer complet réussi."
                : errorMessage ?? "Erreur lors du test Service Layer.";

            return StatusCode(statusCode, new ApiResponse<object>(success, message, new
            {
                Login = loginResponse,
                Customer = customerResponse,
                Order = orderResponse
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running full Service Layer test");
            return StatusCode(500, new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Connexion Service Layer et récupération du SessionId.
    /// </summary>
    [HttpPost("sap/service-layer/login-session")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> LoginServiceLayerSession(CancellationToken cancellationToken)
    {
        try
        {
            var (success, sessionId, response, statusCode, errorMessage) = await _sapService.LoginServiceLayerWithSessionIdAsync(cancellationToken);
            var message = success
                ? "Connexion Service Layer réussie."
                : errorMessage ?? "Erreur lors de la connexion au Service Layer.";

            return StatusCode(statusCode, new ApiResponse<object>(success, message, new
            {
                SessionId = sessionId,
                Response = response
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging into SAP Service Layer (SessionId)");
            return StatusCode(500, new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Crée un client fictif dans SAP B1 via Service Layer.
    /// </summary>
    [HttpPost("sap/service-layer/test-customer")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateTestCustomer(CancellationToken cancellationToken)
    {
        try
        {
            var (success, response, statusCode, errorMessage) = await _sapService.CreateTestCustomerAsync(cancellationToken);
            var message = success
                ? "Client test créé via Service Layer."
                : errorMessage ?? "Erreur lors de la création du client test.";

            return StatusCode(statusCode, new ApiResponse<object>(success, message, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test customer via Service Layer");
            return StatusCode(500, new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Crée une commande fictive dans SAP B1 via Service Layer.
    /// </summary>
    [HttpPost("sap/service-layer/test-order")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateTestOrder(CancellationToken cancellationToken)
    {
        try
        {
            var (success, response, statusCode, errorMessage) = await _sapService.CreateTestOrderAsync(cancellationToken);
            var message = success
                ? "Commande test créée via Service Layer."
                : errorMessage ?? "Erreur lors de la création de la commande test.";

            return StatusCode(statusCode, new ApiResponse<object>(success, message, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test order via Service Layer");
            return StatusCode(500, new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>
    /// Connexion au Service Layer SAP B1 pour récupérer un SessionId.
    /// </summary>
    [HttpPost("sap/service-layer/login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> LoginServiceLayer(CancellationToken cancellationToken)
    {
        try
        {
            var (success, response, statusCode, errorMessage) = await _sapService.LoginServiceLayerAsync(cancellationToken);

            var message = success
                ? "Connexion Service Layer réussie."
                : errorMessage ?? "Erreur lors de la connexion au Service Layer.";

            return StatusCode(statusCode, new ApiResponse<object>(success, message, response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging into SAP Service Layer");
            return StatusCode(500, new ApiResponse<object>(false, ex.Message, null));
        }
    }

    private static object BuildDocumentPayload(CreateDocumentRequest request)
    {
        return new
        {
            request.CardCode,
            DocDate = request.DocDate ?? DateTime.Today,
            DocDueDate = request.DocDueDate ?? DateTime.Today,
            Comments = request.Comments,
            DocCurrency = request.Currency,
            DocumentLines = request.DocumentLines.Select(x => new
            {
                x.ItemCode,
                x.Quantity,
                x.UnitPrice,
                x.WarehouseCode
            })
        };
    }

    private static object BuildOrderPayload(CreateDocumentRequest request, string docCurrency)
    {
        return new
        {
            request.CardCode,
            DocDate = (request.DocDate ?? DateTime.Today).ToString("yyyy-MM-dd"),
            DocDueDate = (request.DocDueDate ?? request.RequiredDate ?? DateTime.Today).ToString("yyyy-MM-dd"),
            RequriedDate = request.RequiredDate?.ToString("yyyy-MM-dd"),
            request.SalesPersonCode,
            request.Series,
            DocObjectCode = string.IsNullOrWhiteSpace(request.DocObjectCode) ? "17" : request.DocObjectCode,
            DocType = string.IsNullOrWhiteSpace(request.DocType) ? "dDocument_Items" : request.DocType,
            DocCurrency = docCurrency,
            request.DocRate,
            request.UserSign,
            request.Comments,
            DocumentLines = request.DocumentLines.Select(x => new
            {
                x.ItemCode,
                x.Quantity,
                x.WarehouseCode,
                Price = GetLinePrice(x),
                UnitPrice = GetLinePrice(x)
            })
        };
    }

    private static decimal GetLinePrice(CreateDocumentLineRequest line)
        => line.Price ?? line.UnitPrice;

    private static string EscapeODataString(string value)
        => value.Replace("'", "''");

    private static IReadOnlyList<ClientViewDto> MapBusinessPartners(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Select(MapBusinessPartnerElement)
            .ToList();
    }

    private static ClientViewDto MapBusinessPartner(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object)
            return new ClientViewDto();

        return MapBusinessPartnerElement(response.Value);
    }

    private static ClientViewDto MapBusinessPartnerElement(JsonElement node)
    {
        return new ClientViewDto
        {
            Code = GetString(node, "CardCode"),
            Name = GetString(node, "CardName"),
            Currency = GetString(node, "Currency"),
            CreditLimit = GetDecimal(node, "CreditLimit")
        };
    }

    private static IReadOnlyList<DocumentViewDto> MapDocuments(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray()
            .Select(MapDocumentElement)
            .ToList();
    }

    private static DocumentViewDto MapDocument(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object)
            return new DocumentViewDto();

        return MapDocumentElement(response.Value);
    }

    private static DocumentViewDto MapDocumentElement(JsonElement node)
    {
        return new DocumentViewDto
        {
            DocEntry = GetInt(node, "DocEntry"),
            DocNum = GetInt(node, "DocNum"),
            CardCode = GetString(node, "CardCode"),
            CardName = GetString(node, "CardName"),
            Date = GetDate(node, "DocDate"),
            Total = GetDecimal(node, "DocTotal"),
            Status = MapStatus(GetString(node, "DocStatus"))
        };
    }

    private static string GetString(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static decimal GetDecimal(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return 0m;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0m;
    }

    private static int GetInt(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0;
    }

    private static DateTime? GetDate(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var date)) return date;
        return null;
    }

    private static string MapStatus(string status)
        => status switch
        {
            "O" => "Open",
            "C" => "Closed",
            _ => status
        };

    private static object SapError(string? error)
        => new { success = false, message = "Erreur SAP", error = error ?? "Erreur inconnue" };
}

public class CreateSapClientRequest
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
}

public class CreateDocumentRequest
{
    public string CardCode { get; set; } = string.Empty;
    public DateTime? DocDate { get; set; }
    public DateTime? DocDueDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? Comments { get; set; }
    public int? SalesPersonCode { get; set; }
    public int? Series { get; set; }
    public string? DocObjectCode { get; set; }
    public string? DocType { get; set; }
    public decimal? DocRate { get; set; }
    public int? UserSign { get; set; }
    public List<CreateDocumentLineRequest> DocumentLines { get; set; } = new();
}

public class CreateDocumentLineRequest
{
    public string ItemCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal? Price { get; set; }
}

public class ClientViewDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
}

public class DocumentViewDto
{
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}
