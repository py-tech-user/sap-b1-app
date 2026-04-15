using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text.Json;
using SapB1App.DTOs;
using SapB1App.Interfaces;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/sap")]
public class SapB1Controller : ControllerBase
{
    private readonly ISapB1Service _sapService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SapB1Controller> _logger;

    public SapB1Controller(ISapB1Service sapService, IConfiguration configuration, ILogger<SapB1Controller> logger)
    {
        _sapService = sapService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        var (success, _, response, statusCode, errorMessage) = await _sapService.LoginServiceLayerWithSessionIdAsync(cancellationToken);
        if (!success)
            return StatusCode(statusCode, SapError(errorMessage, response));

        return Ok(new ApiResponse<JsonElement?>(true, "Connexion établie.", response));
    }

    [HttpGet("test")]
    [AllowAnonymous]
    public async Task<IActionResult> Test(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync("BusinessPartners?$top=1", cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        return Ok(new ApiResponse<object>(true, "Connexion OK", new { ok = true }));
    }

    [HttpGet("clients")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
        => GetBusinessPartnersViaServiceLayerAsync(page, pageSize, cancellationToken);

    [HttpGet("partners")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetPartners(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
        => GetBusinessPartnersViaServiceLayerAsync(page, pageSize, cancellationToken);

    [HttpPost("clients")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateClient([FromBody] CreateSapClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode) || string.IsNullOrWhiteSpace(request.CardName))
            return Task.FromResult<ActionResult<ApiResponse<object>>>(BadRequest(SapError("CardCode et CardName sont obligatoires.")));

        var payload = BuildBusinessPartnerPayload(request);

        return CreateRawAsync("BusinessPartners", payload, cancellationToken);
    }

    [HttpPost("partners")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreatePartner([FromBody] CreateSapClientRequest request, CancellationToken cancellationToken)
        => CreateClient(request, cancellationToken);

    [HttpGet("items")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SapItemDto>>>> GetItems(CancellationToken cancellationToken)
    {
        var result = await _sapService.ServiceLayerGetAsync("Items", cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        Dictionary<string, List<SapItemWarehouseDto>>? warehousesByItem = null;
        var warehousesResult = await _sapService.ServiceLayerGetAsync(
            "ItemWarehouseInfoCollection?$select=ItemCode,WarehouseCode,InStock",
            cancellationToken);

        if (warehousesResult.Success)
        {
            warehousesByItem = MapWarehousesByItem(warehousesResult.Response);
        }

        var items = MapItems(result.Response, warehousesByItem);
        return Ok(new ApiResponse<IReadOnlyList<SapItemDto>>(true, null, items, items.Count));
    }

    [HttpGet("orders")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetOrders(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetDocumentsViaSqlAsync("ORDR", openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpGet("orders/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> GetOrderByDocEntry(int docEntry, CancellationToken cancellationToken)
        => GetDocumentByDocEntryAsync("Orders", docEntry, cancellationToken);

    [HttpPost("orders")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateOrder([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateCommercialDocumentAsync("Orders", request, cancellationToken);

    [HttpPut("orders/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateOrder(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateCommercialDocumentByDocEntryAsync("Orders", docEntry, request, cancellationToken);

    [HttpDelete("orders/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteOrder(int docEntry, CancellationToken cancellationToken)
        => DeleteDocumentByDocEntryAsync("Orders", docEntry, cancellationToken, requireOpenStatus: true);

    [HttpPost("orders/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseOrder(int docEntry, CancellationToken cancellationToken)
        => CloseDocumentByDocEntryAsync("Orders", docEntry, cancellationToken);

    [HttpPost("orders/from-quote/{quoteDocEntry:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateOrderFromQuote(int quoteDocEntry, [FromBody] GenerateFromSourceRequest? request, CancellationToken cancellationToken)
    {
        var build = await BuildFromSourceDocumentAsync("Quotations", quoteDocEntry, request?.SelectedLineNums, cancellationToken);
        if (!build.Success || build.Request is null)
            return BadRequest(SapError(build.ErrorMessage));

        return await CreateCommercialDocumentAsync("Orders", build.Request, cancellationToken);
    }

    [HttpGet("bc")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetBonCommandes(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetOrders(openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpPost("bc")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateBonCommande([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateOrder(request, cancellationToken);

    [HttpPut("bc/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateBonCommande(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateOrder(docEntry, request, cancellationToken);

    [HttpPost("bc/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseBonCommande(int docEntry, CancellationToken cancellationToken)
        => CloseOrder(docEntry, cancellationToken);

    [HttpGet("delivery-notes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDeliveryNotes(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetDocumentsViaSqlAsync("ODLN", openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpGet("delivery-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> GetDeliveryNoteByDocEntry(int docEntry, CancellationToken cancellationToken)
        => GetDocumentByDocEntryAsync("DeliveryNotes", docEntry, cancellationToken);

    [HttpPost("delivery-notes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateDeliveryNote([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateCommercialDocumentAsync("DeliveryNotes", request, cancellationToken, defaultDocStatus: "bost_Open");

    [HttpDelete("delivery-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteDeliveryNote(int docEntry, CancellationToken cancellationToken)
        => Task.FromResult<ActionResult<ApiResponse<object>>>(BadRequest(SapError("Annulation autorisée uniquement pour les devis et bons de commande ouverts.")));

    [HttpPost("delivery-notes/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseDeliveryNote(int docEntry, CancellationToken cancellationToken)
        => CloseDocumentByDocEntryAsync("DeliveryNotes", docEntry, cancellationToken);

    [HttpPost("delivery-notes/from-order/{orderDocEntry:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateDeliveryNoteFromOrder(int orderDocEntry, [FromBody] GenerateFromSourceRequest? request, CancellationToken cancellationToken)
    {
        var build = await BuildFromSourceDocumentAsync("Orders", orderDocEntry, request?.SelectedLineNums, cancellationToken);
        if (!build.Success || build.Request is null)
            return BadRequest(SapError(build.ErrorMessage));

        return await CreateCommercialDocumentAsync("DeliveryNotes", build.Request, cancellationToken, defaultDocStatus: "bost_Open");
    }

    [HttpGet("bl")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetBonsLivraison(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetDeliveryNotes(openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpPost("bl")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateBonLivraison([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateDeliveryNote(request, cancellationToken);

    [HttpPost("bl/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseBonLivraison(int docEntry, CancellationToken cancellationToken)
        => CloseDeliveryNote(docEntry, cancellationToken);

    [HttpGet("quotes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetQuotes(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetDocumentsViaSqlAsync("OQUT", openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpGet("quotes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> GetQuoteByDocEntry(int docEntry, CancellationToken cancellationToken)
        => GetDocumentByDocEntryAsync("Quotations", docEntry, cancellationToken);

    [HttpPost("quotes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateQuote([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateCommercialDocumentAsync("Quotations", request, cancellationToken);

    [HttpPut("quotes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateQuote(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateCommercialDocumentByDocEntryAsync("Quotations", docEntry, request, cancellationToken);

    [HttpDelete("quotes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteQuote(int docEntry, CancellationToken cancellationToken)
        => DeleteDocumentByDocEntryAsync("Quotations", docEntry, cancellationToken, requireOpenStatus: true);

    [HttpPost("quotes/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseQuote(int docEntry, CancellationToken cancellationToken)
        => CloseDocumentByDocEntryAsync("Quotations", docEntry, cancellationToken);

    [HttpGet("devis")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDevis(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetQuotes(openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpPost("devis")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateDevis([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateQuote(request, cancellationToken);

    [HttpPut("devis/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateDevis(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateQuote(docEntry, request, cancellationToken);

    [HttpPost("devis/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseDevis(int docEntry, CancellationToken cancellationToken)
        => CloseQuote(docEntry, cancellationToken);

    [HttpGet("factures")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetInvoices(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[HYBRID-MODE][READ] Lecture des factures via SQL. Status={Status}, OpenOnly={OpenOnly}", status, openOnly);
        return GetDocumentsViaSqlAsync("OINV", openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);
    }

    [HttpGet("factures/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> GetInvoiceByDocEntry(int docEntry, CancellationToken cancellationToken)
        => GetDocumentByDocEntryAsync("Invoices", docEntry, cancellationToken);

    [HttpPost("factures")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateInvoice([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[HYBRID-MODE][WRITE] Création de facture via Service Layer. CardCode={CardCode}, DocDate={DocDate}", request.CardCode, request.DocDate?.ToString("yyyy-MM-dd"));
        return CreateCommercialDocumentAsync("Invoices", request, cancellationToken);
    }

    [HttpDelete("factures/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteInvoice(int docEntry, CancellationToken cancellationToken)
        => Task.FromResult<ActionResult<ApiResponse<object>>>(BadRequest(SapError("Annulation autorisée uniquement pour les devis et bons de commande ouverts.")));

    [HttpPost("factures/{invoiceDocEntry:int}/payments")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> RegisterInvoicePayment(
        int invoiceDocEntry,
        [FromBody] RegisterInvoicePaymentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[HYBRID-MODE][WRITE] Début de la création d'un paiement de facture. InvoiceDocEntry={InvoiceDocEntry}, CardCode={CardCode}", invoiceDocEntry, request.CardCode);

        if (invoiceDocEntry <= 0)
            return BadRequest(SapError("Invoice DocEntry invalide."));

        if (string.IsNullOrWhiteSpace(request.PaymentMethodCode))
            return BadRequest(SapError("PaymentMethodCode est obligatoire."));

        if (request.CashSum < 0 || request.CreditSum < 0)
            return BadRequest(SapError("CashSum et CreditSum doivent être >= 0."));

        var totalPaid = request.CashSum + request.CreditSum;
        if (totalPaid <= 0)
            return BadRequest(SapError("Au moins un montant de paiement doit être > 0."));

        var invoiceResult = await _sapService.ServiceLayerGetAsync(
            $"Invoices({invoiceDocEntry})?$select=DocEntry,CardCode,DocCurrency",
            cancellationToken);

        if (!invoiceResult.Success || invoiceResult.Response is null)
            return StatusCode(invoiceResult.StatusCode, SapError(invoiceResult.ErrorMessage ?? "Impossible de charger la facture.", invoiceResult.Response));

        var invoice = invoiceResult.Response.Value;
        var cardCode = string.IsNullOrWhiteSpace(request.CardCode) ? GetString(invoice, "CardCode") : request.CardCode;
        if (string.IsNullOrWhiteSpace(cardCode))
            return BadRequest(SapError("CardCode manquant pour l'encaissement."));

        var sapCashSum = totalPaid;
        var payload = new
        {
            CardCode = cardCode,
            DocDate = DateTime.Today.ToString("yyyy-MM-dd"),
            DocCurrency = GetString(invoice, "DocCurrency"),
            CashSum = sapCashSum,
            PaymentInvoices = new[]
            {
                new
                {
                    DocEntry = invoiceDocEntry,
                    SumApplied = totalPaid,
                    InvoiceType = "it_Invoice"
                }
            }
        };

        _logger.LogInformation(
            "[HYBRID-MODE][WRITE] Paiement de facture prêt pour SAP. InvoiceDocEntry={InvoiceDocEntry}, CardCode={CardCode}, PaymentMethodCode={PaymentMethodCode}, CashSum={CashSum}",
            invoiceDocEntry,
            cardCode,
            request.PaymentMethodCode,
            sapCashSum);

        var paymentResult = await _sapService.ServiceLayerPostAsync("IncomingPayments", payload, cancellationToken);
        if (!paymentResult.Success)
        {
            _logger.LogError("[HYBRID-MODE][WRITE-ERROR] Échec de la création du paiement. InvoiceDocEntry={InvoiceDocEntry}, Error={Error}", invoiceDocEntry, paymentResult.ErrorMessage);
            return StatusCode(paymentResult.StatusCode, SapError(paymentResult.ErrorMessage, paymentResult.Response));
        }

        _logger.LogInformation("[HYBRID-MODE][WRITE-SUCCESS] Paiement de facture créé avec succès. InvoiceDocEntry={InvoiceDocEntry}", invoiceDocEntry);

        var refreshedInvoice = await _sapService.ServiceLayerGetAsync(
            $"Invoices({invoiceDocEntry})?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,PaidToDate,DocumentStatus",
            cancellationToken);

        object? invoiceStatus = null;
        if (refreshedInvoice.Success && refreshedInvoice.Response.HasValue)
            invoiceStatus = NormalizeDocumentForFrontend(refreshedInvoice.Response.Value);

        return Ok(new ApiResponse<object>(true, "Encaissement enregistré.", new
        {
            payment = paymentResult.Response,
            invoice = invoiceStatus
        }));
    }

    [HttpGet("encaissement/clients")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EncaissementClientDto>>>> GetEncaissementClients(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ENCAISSEMENT] Loading customers for payment screen.");

        var result = await _sapService.ServiceLayerGetAsync(
            "BusinessPartners?$select=CardCode,CardName,Currency,CreditLimit&$filter=CardType eq 'cCustomer'",
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogError("[ENCAISSEMENT] Failed to load customers. Error={Error}", result.ErrorMessage);
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));
        }

        var clients = MapEncaissementClients(result.Response);
        _logger.LogInformation("[ENCAISSEMENT] Customers loaded. Count={Count}", clients.Count);
        return Ok(new ApiResponse<IReadOnlyList<EncaissementClientDto>>(true, null, clients, clients.Count));
    }

    [HttpGet("encaissement/clients/{cardCode}/open-invoices")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EncaissementInvoiceDto>>>> GetOpenInvoicesForClient(
        string cardCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
            return BadRequest(SapError("CardCode est obligatoire."));

        _logger.LogInformation("[ENCAISSEMENT] Loading open invoices for CardCode={CardCode}", cardCode);

        var sqlConnectionString = BuildSapSqlConnectionString();
        if (string.IsNullOrWhiteSpace(sqlConnectionString))
        {
            return StatusCode(500, SapError("Configuration SQL manquante pour les factures ouvertes."));
        }

        try
        {
            var sqlInvoices = new List<EncaissementInvoiceDto>();

            await using var conn = new SqlConnection(sqlConnectionString);
            await conn.OpenAsync(cancellationToken);

                const string sql = @"
SELECT DocEntry, DocNum, CardCode, CardName, DocDate, DocDueDate, DocCur, DocTotal, PaidToDate, DocTotalFC, PaidFC, DocStatus, CANCELED
FROM OINV
WHERE CardCode = @cardCode
  AND ISNULL(CANCELED, 'N') <> 'Y'
  AND (
        ISNULL(DocStatus, 'O') = 'O'
        OR (ISNULL(DocTotal, 0) - ISNULL(PaidToDate, 0)) > 0
        OR (ISNULL(DocTotalFC, 0) - ISNULL(PaidFC, 0)) > 0
      )
ORDER BY DocDate ASC, DocEntry ASC;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            cmd.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = cardCode });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                    var docTotal = reader["DocTotal"] is DBNull ? 0m : Convert.ToDecimal(reader["DocTotal"]);
                    var paidToDate = reader["PaidToDate"] is DBNull ? 0m : Convert.ToDecimal(reader["PaidToDate"]);
                    var localOpen = docTotal - paidToDate;
                    if (localOpen < 0) localOpen = 0;

                    var docTotalFc = reader["DocTotalFC"] is DBNull ? 0m : Convert.ToDecimal(reader["DocTotalFC"]);
                    var paidFc = reader["PaidFC"] is DBNull ? 0m : Convert.ToDecimal(reader["PaidFC"]);
                    var fcOpen = docTotalFc - paidFc;
                    if (fcOpen < 0) fcOpen = 0;

                    var openAmount = new[] { localOpen, fcOpen }.Max();
                    if (openAmount <= 0)
                        continue;

                    sqlInvoices.Add(new EncaissementInvoiceDto
                    {
                        DocEntry = Convert.ToInt32(reader["DocEntry"]),
                        DocNum = Convert.ToInt32(reader["DocNum"]),
                        CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                        CardName = reader["CardName"]?.ToString() ?? string.Empty,
                        DocDate = reader["DocDate"] is DateTime docDate ? docDate : null,
                        DocDueDate = reader["DocDueDate"] is DateTime dueDate ? dueDate : null,
                        DocCurrency = reader["DocCur"]?.ToString() ?? string.Empty,
                        DocTotal = docTotal,
                        PaidToDate = paidToDate,
                        OpenAmount = openAmount,
                        DocStatus = "O"
                    });

                    _logger.LogInformation(
                        "[ENCAISSEMENT][TRACE][OPEN_SQL_ROW] CardCode={CardCode}, DocEntry={DocEntry}, DocTotal={DocTotal}, PaidToDate={PaidToDate}, LocalOpen={LocalOpen}, FcOpen={FcOpen}, OpenAmount={OpenAmount}, DocStatus={DocStatus}, CANCELED={Canceled}",
                        cardCode,
                        Convert.ToInt32(reader["DocEntry"]),
                        docTotal,
                        paidToDate,
                        localOpen,
                        fcOpen,
                        openAmount,
                        reader["DocStatus"]?.ToString() ?? string.Empty,
                        reader["CANCELED"]?.ToString() ?? string.Empty);
            }

            _logger.LogInformation("[ENCAISSEMENT] Open invoices loaded from SQL. CardCode={CardCode}, Count={Count}", cardCode, sqlInvoices.Count);
            return Ok(new ApiResponse<IReadOnlyList<EncaissementInvoiceDto>>(true, null, sqlInvoices, sqlInvoices.Count));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ENCAISSEMENT] SQL open invoices failed for CardCode={CardCode}", cardCode);
            return StatusCode(500, SapError("Erreur lors du chargement des factures ouvertes."));
        }
    }

    [HttpPost("encaissement")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> RegisterEncaissement(
        [FromBody] RegisterEncaissementRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ENCAISSEMENT] Start payment registration. CardCode={CardCode}, InvoiceCount={InvoiceCount}", request.CardCode, request.Invoices.Count);

        if (string.IsNullOrWhiteSpace(request.CardCode))
            return BadRequest(SapError("CardCode est obligatoire."));

        if (string.IsNullOrWhiteSpace(request.PaymentMethodCode))
            return BadRequest(SapError("PaymentMethodCode est obligatoire."));

        if (request.CashSum < 0)
            return BadRequest(SapError("CashSum doit être >= 0."));

        if (request.Invoices.Count == 0)
            return BadRequest(SapError("Au moins une facture doit être sélectionnée."));

        if (request.Invoices.Any(i => i.DocEntry <= 0))
            return BadRequest(SapError("Chaque ligne d'encaissement doit contenir DocEntry > 0."));

        var selectedDocEntries = request.Invoices
            .Select(i => i.DocEntry)
            .Distinct()
            .ToList();

        var checkedInvoices = new List<(int DocEntry, DateTime? DocDate, string DocCurrency, decimal OpenAmount)>();

        foreach (var docEntry in selectedDocEntries)
        {
            _logger.LogInformation("[ENCAISSEMENT] Checking invoice before payment. CardCode={CardCode}, InvoiceDocEntry={InvoiceDocEntry}", request.CardCode, docEntry);

            var invoiceCheck = await _sapService.ServiceLayerGetAsync(
                $"Invoices({docEntry})",
                cancellationToken);

            if (!invoiceCheck.Success || invoiceCheck.Response is null)
            {
                _logger.LogError("[ENCAISSEMENT] Unable to check invoice. InvoiceDocEntry={InvoiceDocEntry}, Error={Error}", docEntry, invoiceCheck.ErrorMessage);
                return StatusCode(invoiceCheck.StatusCode, SapError(invoiceCheck.ErrorMessage ?? "Impossible de vérifier la facture.", invoiceCheck.Response));
            }

            var invoiceNode = invoiceCheck.Response.Value;
            var invoiceCardCode = GetString(invoiceNode, "CardCode");
            var invoiceStatus = GetString(invoiceNode, "DocumentStatus");
            if (string.IsNullOrWhiteSpace(invoiceStatus))
                invoiceStatus = GetString(invoiceNode, "DocStatus");

            if (!string.Equals(invoiceCardCode, request.CardCode, StringComparison.OrdinalIgnoreCase))
                return BadRequest(SapError($"La facture {docEntry} n'appartient pas au client {request.CardCode}."));

            var openAmount = ResolveOpenAmount(invoiceNode);
            if (openAmount <= 0)
                return BadRequest(SapError($"La facture {docEntry} est déjà soldée."));

            var beforeTrace = ReadInvoiceTrace(invoiceNode);
            _logger.LogInformation(
                "[ENCAISSEMENT][TRACE][BEFORE] DocEntry={DocEntry}, CardCode={CardCode}, Currency={Currency}, DocTotal={DocTotal}, PaidToDate={PaidToDate}, OpenBal={OpenBal}, OpenBalFC={OpenBalFC}, DocTotalFC={DocTotalFC}, PaidFC={PaidFC}, ComputedOpen={ComputedOpen}, RawStatus={RawStatus}, IsCancelled={IsCancelled}",
                docEntry,
                invoiceCardCode,
                beforeTrace.DocCurrency,
                beforeTrace.DocTotal,
                beforeTrace.PaidToDate,
                beforeTrace.OpenBal,
                beforeTrace.OpenBalFc,
                beforeTrace.DocTotalFc,
                beforeTrace.PaidFc,
                beforeTrace.OpenAmount,
                beforeTrace.RawStatus,
                beforeTrace.IsCancelled);

            checkedInvoices.Add((docEntry, GetDate(invoiceNode, "DocDate"), GetString(invoiceNode, "DocCurrency"), openAmount));
        }

        var orderedInvoices = checkedInvoices
            .OrderBy(i => i.DocDate ?? DateTime.MaxValue)
            .ThenBy(i => i.DocEntry)
            .ToList();

        var totalSelected = orderedInvoices.Sum(i => i.OpenAmount);
        var totalPaid = request.CashSum;

        if (totalPaid <= 0)
            return BadRequest(SapError("CashSum doit être supérieur à 0."));

        var requestedByDocEntry = request.Invoices
            .GroupBy(i => i.DocEntry)
            .ToDictionary(g => g.Key, g => Math.Max(0m, g.Sum(x => x.SumApplied)));

        var hasExplicitAllocations = requestedByDocEntry.Values.Any(v => v > 0);

        var amountToApply = Math.Min(totalPaid, totalSelected);

        var remainingToApply = amountToApply;
        decimal totalAppliedBuilt = 0m;
        var paymentInvoices = new List<object>();
        foreach (var invoice in orderedInvoices)
        {
            if (remainingToApply <= 0) break;

            var requested = requestedByDocEntry.TryGetValue(invoice.DocEntry, out var explicitAmount)
                ? explicitAmount
                : 0m;

            var targetForInvoice = hasExplicitAllocations
                ? Math.Min(invoice.OpenAmount, requested)
                : invoice.OpenAmount;

            var sumApplied = Math.Min(targetForInvoice, remainingToApply);
            remainingToApply -= sumApplied;

            if (sumApplied > 0)
            {
                totalAppliedBuilt += sumApplied;
                paymentInvoices.Add(new
                {
                    invoice.DocEntry,
                    SumApplied = sumApplied,
                    AppliedFC = sumApplied,
                    InvoiceType = "it_Invoice"
                });
            }
        }

        if (paymentInvoices.Count == 0)
            return BadRequest(SapError("Aucune somme n'a pu être affectée aux factures sélectionnées."));

        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = request.CardCode,
            ["DocDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["CashSum"] = totalAppliedBuilt,
            ["PaymentInvoices"] = paymentInvoices
        };

        _logger.LogInformation(
            "[ENCAISSEMENT] Posting payment to SAP. CardCode={CardCode}, PaymentMethodCode={PaymentMethodCode}, CashSum={CashSum}, TotalSelected={TotalSelected}",
            request.CardCode,
            request.PaymentMethodCode,
            totalAppliedBuilt,
            totalSelected);

        _logger.LogInformation("[ENCAISSEMENT][TRACE][PAYLOAD] {@Payload}", payload);

        var paymentResult = await _sapService.ServiceLayerPostAsync("IncomingPayments", payload, cancellationToken);
        if (!paymentResult.Success)
        {
            _logger.LogError("[ENCAISSEMENT] SAP payment registration failed. CardCode={CardCode}, Error={Error}", request.CardCode, paymentResult.ErrorMessage);
            return StatusCode(paymentResult.StatusCode, SapError(paymentResult.ErrorMessage, paymentResult.Response));
        }

        _logger.LogInformation("[ENCAISSEMENT] Payment registration succeeded. CardCode={CardCode}, InvoiceCount={InvoiceCount}", request.CardCode, request.Invoices.Count);

        var refreshedInvoices = new List<object>();
        foreach (var invoice in orderedInvoices)
        {
            var refreshed = await _sapService.ServiceLayerGetAsync(
                $"Invoices({invoice.DocEntry})?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,PaidToDate,DocumentStatus",
                cancellationToken);

            if (refreshed.Success && refreshed.Response.HasValue)
            {
                var afterTrace = ReadInvoiceTrace(refreshed.Response.Value);
                _logger.LogInformation(
                    "[ENCAISSEMENT][TRACE][AFTER] DocEntry={DocEntry}, Currency={Currency}, DocTotal={DocTotal}, PaidToDate={PaidToDate}, OpenBal={OpenBal}, OpenBalFC={OpenBalFC}, DocTotalFC={DocTotalFC}, PaidFC={PaidFC}, ComputedOpen={ComputedOpen}, RawStatus={RawStatus}, IsCancelled={IsCancelled}",
                    invoice.DocEntry,
                    afterTrace.DocCurrency,
                    afterTrace.DocTotal,
                    afterTrace.PaidToDate,
                    afterTrace.OpenBal,
                    afterTrace.OpenBalFc,
                    afterTrace.DocTotalFc,
                    afterTrace.PaidFc,
                    afterTrace.OpenAmount,
                    afterTrace.RawStatus,
                    afterTrace.IsCancelled);

                if (afterTrace.OpenAmount > 0 &&
                    (string.Equals(afterTrace.RawStatus, "C", StringComparison.OrdinalIgnoreCase) ||
                     afterTrace.RawStatus.Contains("close", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning(
                        "[ENCAISSEMENT][TRACE][ANOMALY] DocEntry={DocEntry} closed while computed open > 0. ComputedOpen={ComputedOpen}, RawStatus={RawStatus}",
                        invoice.DocEntry,
                        afterTrace.OpenAmount,
                        afterTrace.RawStatus);
                }

                var sqlTrace = await ReadInvoiceSqlTraceAsync(invoice.DocEntry, cancellationToken);
                if (sqlTrace.Found)
                {
                    _logger.LogInformation(
                        "[ENCAISSEMENT][TRACE][AFTER_SQL] DocEntry={DocEntry}, DocCur={DocCur}, DocTotal={DocTotal}, PaidToDate={PaidToDate}, OpenBal={OpenBal}, OpenBalFC={OpenBalFC}, DocStatus={DocStatus}, CANCELED={Canceled}",
                        invoice.DocEntry,
                        sqlTrace.DocCur,
                        sqlTrace.DocTotal,
                        sqlTrace.PaidToDate,
                        sqlTrace.OpenBal,
                        sqlTrace.OpenBalFc,
                        sqlTrace.DocStatus,
                        sqlTrace.Canceled);
                }

                refreshedInvoices.Add(NormalizeDocumentForFrontend(refreshed.Response.Value));
            }
        }

        return Ok(new ApiResponse<object>(true, "Encaissement enregistré.", new
        {
            payment = paymentResult.Response,
            invoices = refreshedInvoices,
            totalSelected,
            cashSumApplied = totalAppliedBuilt
        }));
    }

    [HttpPost("factures/from-delivery-note/{deliveryDocEntry:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> CreateInvoiceFromDeliveryNote(int deliveryDocEntry, [FromBody] GenerateFromSourceRequest? request, CancellationToken cancellationToken)
    {
        var build = await BuildFromSourceDocumentAsync("DeliveryNotes", deliveryDocEntry, request?.SelectedLineNums, cancellationToken);
        if (!build.Success || build.Request is null)
            return BadRequest(SapError(build.ErrorMessage));

        return await CreateCommercialDocumentAsync("Invoices", build.Request, cancellationToken);
    }

    [HttpGet("credit-notes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetCreditNotes(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetDocumentsViaSqlAsync("ORIN", openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpGet("credit-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> GetCreditNoteByDocEntry(int docEntry, CancellationToken cancellationToken)
        => GetDocumentByDocEntryAsync("CreditNotes", docEntry, cancellationToken);

    [HttpPost("credit-notes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateCreditNote([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateCommercialDocumentAsync("CreditNotes", request, cancellationToken);

    [HttpDelete("credit-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteCreditNote(int docEntry, CancellationToken cancellationToken)
        => Task.FromResult<ActionResult<ApiResponse<object>>>(BadRequest(SapError("Annulation autorisée uniquement pour les devis et bons de commande ouverts.")));

    [HttpGet("returns")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetReturns(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
        => GetDocumentsViaSqlAsync("ORDN", openOnly, page, pageSize, search, customer, status, dateFrom, dateTo, cancellationToken);

    [HttpGet("returns/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> GetReturnByDocEntry(int docEntry, CancellationToken cancellationToken)
        => GetDocumentByDocEntryAsync("Returns", docEntry, cancellationToken);

    [HttpPost("returns")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CreateReturn([FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => CreateCommercialDocumentAsync("Returns", request, cancellationToken);

    [HttpDelete("returns/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteReturn(int docEntry, CancellationToken cancellationToken)
        => Task.FromResult<ActionResult<ApiResponse<object>>>(BadRequest(SapError("Annulation autorisée uniquement pour les devis et bons de commande ouverts.")));

    private async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDocumentsAsync(
        string relativeUrl,
        CancellationToken cancellationToken,
        bool isBusinessPartner = false)
    {
        var result = await _sapService.ServiceLayerGetAsync(relativeUrl, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        var items = isBusinessPartner ? MapBusinessPartners(result.Response) : MapDocuments(result.Response);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, items.Count));
    }

    private async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetBusinessPartnersViaServiceLayerAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var result = await _sapService.ServiceLayerGetAsync(
            "BusinessPartners?$select=CardCode,CardName,Phone1,Cellular,E_Mail,Currency,CreditLimit,CardType,GroupCode,Country,City,Address&$orderby=CardCode desc&$top=2000",
            cancellationToken);

        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        var allItems = MapBusinessPartners(result.Response);
        var totalCount = allItems.Count;
        var pagedItems = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, pagedItems, totalCount));
    }

    private async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetFacturesFromServiceLayerAsync(
        bool openOnly,
        int page,
        int pageSize,
        string? search,
        string? customer,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var relativeUrl =
            "Invoices?$select=DocEntry,DocNum,CardCode,CardName,DocTotal,PaidToDate,DocumentStatus,DocDate&$orderby=DocEntry desc&$top=1000&$skip=0";

        try
        {
            var result = await _sapService.ServiceLayerGetAsync(relativeUrl, cancellationToken);
            if (!result.Success)
            {
                _logger.LogWarning("[FACTURES-SL] Requête liste factures avec projection échouée. Fallback requête brute sans filtres/projection. Error={Error}", result.ErrorMessage);
                var fallbackUrl = "Invoices?$top=1000&$skip=0";
                result = await _sapService.ServiceLayerGetAsync(fallbackUrl, cancellationToken);
            }

            if (!result.Success)
                return StatusCode(result.StatusCode, SapError($"Service Layer SAP inaccessible: {result.ErrorMessage}", result.Response));

            var items = new List<DocumentViewDto>();
            if (result.Response.HasValue &&
                result.Response.Value.ValueKind == JsonValueKind.Object &&
                result.Response.Value.TryGetProperty("value", out var values) &&
                values.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in values.EnumerateArray())
                {
                    var rawStatus = GetRawDocumentStatus(node);
                    var total = GetDecimal(node, "DocTotal");
                    var paidToDate = GetDecimal(node, "PaidToDate");
                    var computedStatus = IsCancelled(node)
                        ? "Cancelled"
                        : string.IsNullOrWhiteSpace(rawStatus)
                            ? (ResolveOpenAmount(node) > 0 ? "Open" : "Closed")
                            : NormalizeDocumentStatus(rawStatus, node);

                    items.Add(new DocumentViewDto
                    {
                        DocEntry = GetInt(node, "DocEntry"),
                        DocNum = GetInt(node, "DocNum"),
                        CardCode = GetString(node, "CardCode"),
                        CardName = GetString(node, "CardName"),
                        Total = total,
                        PaidToDate = paidToDate,
                        Date = GetDate(node, "DocDate"),
                        DocStatus = rawStatus,
                        DocumentStatus = rawStatus,
                        IsCancelled = IsCancelled(node),
                        Status = computedStatus
                    });
                }
            }

            var filteredItems = ApplyDocumentFilters(items, openOnly, search, customer, status, dateFrom, dateTo);
            var totalCount = filteredItems.Count;
            var pagedItems = filteredItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, pagedItems, totalCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FACTURES-SL] Erreur d'accès Service Layer pour la liste des factures.");
            return StatusCode(503, SapError("Service Layer SAP inaccessible. Veuillez réessayer."));
        }
    }

    /// <summary>
    /// Mode hybride : lecture SQL + écriture Service Layer
    /// 
    /// INVOICES (OINV):
    /// - Lecture : exclusivement via SQL (table OINV) - toutes les factures (Open + Closed) pour la performance
    /// - Pas de fallback Service Layer si SQL échoue - erreur explicite retournée
    /// - Écriture (création, suppression, paiements) : via Service Layer uniquement
    /// 
    /// AUTRES DOCUMENTS (ORDR, ODLN, OQUT, ORIN, ORDN):
    /// - Lecture : via SQL en priorité, fallback Service Layer si SQL échoue
    /// - Écriture : via Service Layer
    /// </summary>
    private async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDocumentsViaSqlAsync(
        string tableName,
        bool openOnly,
        int page,
        int pageSize,
        string? search,
        string? customer,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var isInvoiceTable = string.Equals(tableName, "OINV", StringComparison.OrdinalIgnoreCase);
        var normalizedSearch = (search ?? string.Empty).Trim();
        var normalizedCustomer = (customer ?? string.Empty).Trim();
        var normalizedStatus = NormalizeDocumentStatusFilter(status);
        var useSqlReadForDocuments = bool.TryParse(_configuration["SapB1:UseSqlReadForDocuments"], out var sqlReadEnabled) && sqlReadEnabled;
        if (!useSqlReadForDocuments)
        {
            _logger.LogError("[HYBRID-MODE] Lecture SQL désactivée pour la table {TableName}.", tableName);
            return StatusCode(503, SapError("Lecture SQL indisponible pour les documents."));
        }

        var connectionString = BuildSapSqlConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogError("[HYBRID-MODE] Configuration SQL incomplète pour {TableName}.", tableName);
            return StatusCode(500, SapError("Configuration SQL manquante. Impossible de charger les documents."));
        }

        var offset = (page - 1) * pageSize;
        var selectColumns = isInvoiceTable
            ? "DocEntry, DocNum, CardCode, CardName, DocTotal, PaidToDate, DocTotalFC, PaidFC, DocDate, DocStatus, CANCELED"
            : "DocEntry, DocNum, CardCode, CardName, DocTotal, DocDate, DocStatus, CANCELED";

        var openCondition = isInvoiceTable
            ? "(ISNULL(CANCELED,'N') <> 'Y' AND (ISNULL(DocStatus,'O') = 'O' OR (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0 OR (ISNULL(DocTotalFC,0) - ISNULL(PaidFC,0)) > 0))"
            : "(ISNULL(CANCELED,'N') <> 'Y' AND ISNULL(DocStatus,'O') = 'O')";

        var closedCondition = isInvoiceTable
            ? "(ISNULL(CANCELED,'N') <> 'Y' AND (ISNULL(DocStatus,'') = 'C' OR ((ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) <= 0 AND (ISNULL(DocTotalFC,0) - ISNULL(PaidFC,0)) <= 0)))"
            : "(ISNULL(CANCELED,'N') <> 'Y' AND ISNULL(DocStatus,'C') = 'C')";

        var sql = $@"
;WITH Filtered AS
(
    SELECT {selectColumns},
           ROW_NUMBER() OVER (ORDER BY DocEntry DESC) AS RowNum
    FROM {tableName}
    WHERE (@openOnly = 0 OR {openCondition})
      AND (@search = '' OR CardCode LIKE @searchLike OR CardName LIKE @searchLike OR CAST(DocNum AS NVARCHAR(50)) LIKE @searchLike)
      AND (@customer = '' OR CardCode LIKE @customerLike OR CardName LIKE @customerLike)
      AND (
            @status = ''
            OR (@status = 'open' AND {openCondition})
            OR (@status = 'closed' AND {closedCondition})
            OR (@status = 'cancelled' AND ISNULL(CANCELED,'N') = 'Y')
          )
      AND (@dateFrom IS NULL OR DocDate >= @dateFrom)
      AND (@dateTo IS NULL OR DocDate < DATEADD(DAY, 1, @dateTo))
)
SELECT {selectColumns}
FROM Filtered
WHERE RowNum BETWEEN @rowStart AND @rowEnd
ORDER BY RowNum;";

        var countSql = $@"
SELECT COUNT(1)
FROM {tableName}
WHERE (@openOnly = 0 OR {openCondition})
  AND (@search = '' OR CardCode LIKE @searchLike OR CardName LIKE @searchLike OR CAST(DocNum AS NVARCHAR(50)) LIKE @searchLike)
  AND (@customer = '' OR CardCode LIKE @customerLike OR CardName LIKE @customerLike)
  AND (
        @status = ''
        OR (@status = 'open' AND {openCondition})
        OR (@status = 'closed' AND {closedCondition})
        OR (@status = 'cancelled' AND ISNULL(CANCELED,'N') = 'Y')
      )
  AND (@dateFrom IS NULL OR DocDate >= @dateFrom)
  AND (@dateTo IS NULL OR DocDate < DATEADD(DAY, 1, @dateTo));";

        var items = new List<DocumentViewDto>();
        var totalCount = 0;
        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
            {
                _logger.LogError("[HYBRID-MODE] Ouverture de connexion SQL impossible pour la table {TableName}.", tableName);
                return StatusCode(500, SapError("Connexion SQL impossible."));
            }
            await using (conn)
            {

                await using (var countCmd = new SqlCommand(countSql, conn))
                {
                    countCmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                    countCmd.Parameters.Add(new SqlParameter("@openOnly", SqlDbType.Bit) { Value = openOnly });
                    countCmd.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 200) { Value = normalizedSearch });
                    countCmd.Parameters.Add(new SqlParameter("@searchLike", SqlDbType.NVarChar, 210) { Value = $"%{normalizedSearch}%" });
                    countCmd.Parameters.Add(new SqlParameter("@customer", SqlDbType.NVarChar, 200) { Value = normalizedCustomer });
                    countCmd.Parameters.Add(new SqlParameter("@customerLike", SqlDbType.NVarChar, 210) { Value = $"%{normalizedCustomer}%" });
                    countCmd.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20) { Value = normalizedStatus });
                    countCmd.Parameters.Add(new SqlParameter("@dateFrom", SqlDbType.DateTime) { Value = dateFrom?.Date ?? (object)DBNull.Value });
                    countCmd.Parameters.Add(new SqlParameter("@dateTo", SqlDbType.DateTime) { Value = dateTo?.Date ?? (object)DBNull.Value });
                    var countObj = await countCmd.ExecuteScalarAsync(cancellationToken);
                    totalCount = countObj is null || countObj == DBNull.Value ? 0 : Convert.ToInt32(countObj);
                }

                await using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                cmd.Parameters.Add(new SqlParameter("@openOnly", SqlDbType.Bit) { Value = openOnly });
                cmd.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 200) { Value = normalizedSearch });
                cmd.Parameters.Add(new SqlParameter("@searchLike", SqlDbType.NVarChar, 210) { Value = $"%{normalizedSearch}%" });
                cmd.Parameters.Add(new SqlParameter("@customer", SqlDbType.NVarChar, 200) { Value = normalizedCustomer });
                cmd.Parameters.Add(new SqlParameter("@customerLike", SqlDbType.NVarChar, 210) { Value = $"%{normalizedCustomer}%" });
                cmd.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20) { Value = normalizedStatus });
                cmd.Parameters.Add(new SqlParameter("@dateFrom", SqlDbType.DateTime) { Value = dateFrom?.Date ?? (object)DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@dateTo", SqlDbType.DateTime) { Value = dateTo?.Date ?? (object)DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@rowStart", SqlDbType.Int) { Value = offset + 1 });
                cmd.Parameters.Add(new SqlParameter("@rowEnd", SqlDbType.Int) { Value = offset + pageSize });

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var rawStatus = reader["DocStatus"]?.ToString() ?? string.Empty;
                    var canceled = reader["CANCELED"]?.ToString() ?? string.Empty;
                    var isCancelled = string.Equals(canceled, "Y", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(canceled, "tYES", StringComparison.OrdinalIgnoreCase);

                    var docTotal = reader["DocTotal"] is DBNull ? 0m : Convert.ToDecimal(reader["DocTotal"]);
                    var paidToDate = isInvoiceTable
                        ? (reader["PaidToDate"] is DBNull ? 0m : Convert.ToDecimal(reader["PaidToDate"]))
                        : 0m;
                    var docTotalFc = isInvoiceTable
                        ? (reader["DocTotalFC"] is DBNull ? 0m : Convert.ToDecimal(reader["DocTotalFC"]))
                        : 0m;
                    var paidFc = isInvoiceTable
                        ? (reader["PaidFC"] is DBNull ? 0m : Convert.ToDecimal(reader["PaidFC"]))
                        : 0m;
                    var localOpenBalance = isInvoiceTable ? (docTotal - paidToDate) : 0m;
                    var foreignOpenBalance = isInvoiceTable ? (docTotalFc - paidFc) : 0m;
                    var hasOpenBalance = false;
                    if (isInvoiceTable)
                    {
                        var isRawClosed = string.Equals(rawStatus, "C", StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(rawStatus, "Closed", StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(rawStatus, "bost_Close", StringComparison.OrdinalIgnoreCase);

                        hasOpenBalance = !isRawClosed && (
                            string.Equals(rawStatus, "O", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(rawStatus, "Open", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(rawStatus, "bost_Open", StringComparison.OrdinalIgnoreCase)
                            || localOpenBalance > 0.0001m
                            || foreignOpenBalance > 0.0001m
                        );
                    }

                    var normalizedRowStatus = isCancelled
                        ? "Cancelled"
                        : isInvoiceTable
                            ? (hasOpenBalance ? "Open" : "Closed")
                            : (string.Equals(rawStatus, "O", StringComparison.OrdinalIgnoreCase) ? "Open" : "Closed");

                    items.Add(new DocumentViewDto
                    {
                        DocEntry = Convert.ToInt32(reader["DocEntry"]),
                        DocNum = Convert.ToInt32(reader["DocNum"]),
                        CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                        CardName = reader["CardName"]?.ToString() ?? string.Empty,
                        Total = docTotal,
                        Date = reader["DocDate"] is DateTime date ? date : null,
                        Status = normalizedRowStatus,
                        DocStatus = rawStatus,
                        DocumentStatus = rawStatus,
                        IsCancelled = isCancelled
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HYBRID-MODE] Erreur SQL lors du chargement de la table {TableName}. Exception: {Exception}", tableName, ex.Message);
            return StatusCode(500, SapError("Erreur lors du chargement des documents."));
        }

        if (totalCount == 0)
        {
            _logger.LogInformation("[HYBRID-MODE] Aucune donnée SQL trouvée pour {TableName}. Search={Search}, Customer={Customer}, Status={Status}", tableName, normalizedSearch, normalizedCustomer, normalizedStatus);
            return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, 0));
        }

        _logger.LogInformation("[HYBRID-MODE] Factures chargées depuis SQL OINV avec succès. Count={Count}, TotalCount={TotalCount}, Page={Page}, PageSize={PageSize}", items.Count, totalCount, page, pageSize);
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, totalCount));
    }

    private async Task<IReadOnlyList<DocumentViewDto>> RefreshInvoicesFromDocEntryAsync(
        IReadOnlyList<DocumentViewDto> invoices,
        CancellationToken cancellationToken)
    {
        var tasks = invoices.Select(async invoice =>
        {
            if (invoice.DocEntry <= 0)
                return invoice;

            var detail = await _sapService.ServiceLayerGetAsync(
                $"Invoices({invoice.DocEntry})?$select=DocEntry,DocStatus,DocumentStatus,DocTotal,PaidToDate,DocTotalFC,PaidFC,OpenBal,OpenBalFC",
                cancellationToken);

            if (!detail.Success || !detail.Response.HasValue)
                return invoice;

            var node = detail.Response.Value;
            var rawStatus = GetRawDocumentStatus(node);
            var isCancelled = IsCancelled(node);
            var hasOpenBalance = ResolveOpenAmount(node) > 0;
            var normalizedStatus = isCancelled
                ? "Cancelled"
                : hasOpenBalance ? "Open" : NormalizeDocumentStatus(rawStatus, node);

            invoice.DocStatus = rawStatus;
            invoice.DocumentStatus = rawStatus;
            invoice.IsCancelled = isCancelled;
            invoice.Status = normalizedStatus;
            return invoice;
        });

        return await Task.WhenAll(tasks);
    }

    private async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetDocumentsFromServiceLayerFallbackAsync(
        string serviceLayerEntity,
        bool openOnly,
        int page,
        int pageSize,
        string? search,
        string? customer,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        var relativeUrl = string.Equals(serviceLayerEntity, "Invoices", StringComparison.OrdinalIgnoreCase)
            ? "Invoices?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,PaidToDate,DocumentStatus&$orderby=DocEntry desc&$top=1000&$skip=0"
            : $"{serviceLayerEntity}?$select=DocEntry,DocNum,CardCode,CardName,DocDate,DocDueDate,DocTotal,DocumentStatus&$orderby=DocEntry desc&$top=1000&$skip=0";

        var result = await _sapService.ServiceLayerGetAsync(relativeUrl, cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        var items = string.Equals(serviceLayerEntity, "Invoices", StringComparison.OrdinalIgnoreCase)
            ? MapInvoiceDocuments(result.Response)
            : MapDocuments(result.Response);
        var filteredItems = ApplyDocumentFilters(items, openOnly, search, customer, status, dateFrom, dateTo);
        var totalCount = filteredItems.Count;
        var pagedItems = filteredItems
            .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 200))
            .Take(Math.Clamp(pageSize, 1, 200))
            .ToList();

        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, pagedItems, totalCount));
    }

    private static List<DocumentViewDto> ApplyDocumentFilters(
        IReadOnlyList<DocumentViewDto> items,
        bool openOnly,
        string? search,
        string? customer,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var normalizedSearch = (search ?? string.Empty).Trim();
        var normalizedCustomer = (customer ?? string.Empty).Trim();
        var normalizedStatus = NormalizeDocumentStatusFilter(status);

        var filtered = items.AsEnumerable();

        if (openOnly)
            filtered = filtered.Where(x => IsOpenStatusFilterValue(x.Status));

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            filtered = filtered.Where(x =>
                x.DocNum.ToString().Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(x.CardCode) && x.CardCode.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.CardName) && x.CardName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(normalizedCustomer))
        {
            filtered = filtered.Where(x =>
                (!string.IsNullOrWhiteSpace(x.CardCode) && x.CardCode.Contains(normalizedCustomer, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(x.CardName) && x.CardName.Contains(normalizedCustomer, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            filtered = normalizedStatus switch
            {
                "open" => filtered.Where(x => IsOpenStatusFilterValue(x.Status)),
                "closed" => filtered.Where(x => !IsOpenStatusFilterValue(x.Status) && !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)),
                "cancelled" => filtered.Where(x => string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)),
                _ => filtered
            };
        }

        if (dateFrom.HasValue)
        {
            var startDate = dateFrom.Value.Date;
            filtered = filtered.Where(x => x.Date.HasValue && x.Date.Value.Date >= startDate);
        }

        if (dateTo.HasValue)
        {
            var endDate = dateTo.Value.Date;
            filtered = filtered.Where(x => x.Date.HasValue && x.Date.Value.Date <= endDate);
        }

        return filtered.OrderByDescending(x => x.DocEntry).ToList();
    }

    private static bool IsOpenStatusFilterValue(string? status)
    {
        var s = (status ?? string.Empty).Trim().ToLowerInvariant();
        var compact = s.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);

        return s is "open" or "o" or "en attente"
               || compact is "bostopen" or "enattente" or "unpaid" or "partiallypaid" or "partialpaid" or "overdue"
               || (compact.Contains("open") && !compact.Contains("close"));
    }

    private static string NormalizeDocumentStatusFilter(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "open" or "o" or "bost_open" => "open",
            "closed" or "close" or "c" or "bost_close" => "closed",
            "cancelled" or "canceled" or "cancel" => "cancelled",
            _ => string.Empty
        };
    }

    private string BuildSapSqlConnectionString()
    {
        var server = _configuration["SapB1:SqlServer"];
        if (string.IsNullOrWhiteSpace(server))
            server = _configuration["SapB1:Server"];

        var sqlInstance = _configuration["SapB1:SqlInstance"];
        var sqlPort = _configuration["SapB1:SqlPort"];
        var appConn = _configuration.GetConnectionString("DefaultConnection");
        string? appDataSource = null;
        if (!string.IsNullOrWhiteSpace(appConn))
        {
            try
            {
                var appBuilder = new SqlConnectionStringBuilder(appConn);
                appDataSource = appBuilder.DataSource;
            }
            catch
            {
            }
        }

        var hasExplicitInstanceOrPort = !string.IsNullOrWhiteSpace(sqlInstance) || !string.IsNullOrWhiteSpace(sqlPort) ||
                                       (!string.IsNullOrWhiteSpace(server) && (server.Contains('\\') || server.Contains(',')));

        if (!hasExplicitInstanceOrPort &&
            !string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(appDataSource))
        {
            var normalizedServer = server.Trim().ToLowerInvariant();
            if (normalizedServer is "localhost" or "." or "(local)" &&
                (appDataSource.Contains('\\') || appDataSource.Contains(',')))
            {
                server = appDataSource;
            }
        }

        if (!string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(sqlInstance) &&
            !server.Contains('\\') &&
            !server.Contains(','))
        {
            server = $"{server}\\{sqlInstance}";
        }

        if (!string.IsNullOrWhiteSpace(server) &&
            !string.IsNullOrWhiteSpace(sqlPort) &&
            !server.Contains(',') &&
            !string.Equals(sqlPort.Trim(), "1433", StringComparison.OrdinalIgnoreCase))
        {
            server = $"{server},{sqlPort}";
        }

        if (string.IsNullOrWhiteSpace(server))
        {
            server = appDataSource;
        }

        var database = _configuration["SapB1:SqlCompanyDB"];
        if (string.IsNullOrWhiteSpace(database))
            database = _configuration["SapB1:CompanyDB"];
        if (string.IsNullOrWhiteSpace(database))
            database = _configuration["SapB1ServiceLayer:CompanyDB"];
        var dbUser = _configuration["SapB1:DbUserName"];
        if (string.IsNullOrWhiteSpace(dbUser))
            dbUser = _configuration["SapB1:UserName"];

        var dbPassword = _configuration["SapB1:DbPassword"];
        if (string.IsNullOrWhiteSpace(dbPassword))
            dbPassword = _configuration["SapB1:Password"];
        var useTrusted = bool.TryParse(_configuration["SapB1:UseTrusted"], out var trusted) && trusted;
        var useSqlAuth = !string.IsNullOrWhiteSpace(dbUser);
        var useIntegratedSecurity = useTrusted && !useSqlAuth;

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            return string.Empty;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            TrustServerCertificate = true,
            Encrypt = false,
            IntegratedSecurity = useIntegratedSecurity,
            ConnectTimeout = 5
        };

        if (useSqlAuth)
        {
            builder.UserID = dbUser;
            builder.Password = dbPassword;
        }

        _logger.LogInformation("[HYBRID-MODE] SQL SAP target resolved. DataSource={DataSource}, Database={Database}, AuthMode={AuthMode}", builder.DataSource, builder.InitialCatalog, builder.IntegratedSecurity ? "IntegratedSecurity" : "SqlAuth");

        return builder.ConnectionString;
    }

    private async Task<SqlConnection?> OpenSapSqlConnectionAsync(CancellationToken cancellationToken)
    {
        var baseConnectionString = BuildSapSqlConnectionString();
        if (string.IsNullOrWhiteSpace(baseConnectionString))
            return null;

        var baseBuilder = new SqlConnectionStringBuilder(baseConnectionString);
        var baseDataSource = baseBuilder.DataSource?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseDataSource))
            return null;

        var candidates = new List<string> { baseDataSource };

        if (!baseDataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            candidates.Add($"tcp:{baseDataSource}");
        else
            candidates.Add(baseDataSource[4..]);

        var noPrefix = baseDataSource.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)
            ? baseDataSource[4..]
            : baseDataSource;

        if (!noPrefix.Contains(','))
        {
            var configuredPort = (_configuration["SapB1:SqlPort"] ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(configuredPort))
            {
                candidates.Add($"{noPrefix},{configuredPort}");
                candidates.Add($"tcp:{noPrefix},{configuredPort}");
            }
        }
        else
        {
            var hostOnly = noPrefix.Split(',')[0];
            if (!string.IsNullOrWhiteSpace(hostOnly))
            {
                candidates.Add(hostOnly);
                candidates.Add($"tcp:{hostOnly}");
            }
        }

        var host = noPrefix.Split(',')[0].Trim();
        if (!string.IsNullOrWhiteSpace(host) && !host.Contains('\\'))
            candidates.Add($@"np:\\{host}\pipe\sql\query");

        foreach (var dataSource in candidates
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                DataSource = dataSource
            };

            var conn = new SqlConnection(builder.ConnectionString);
            try
            {
                await conn.OpenAsync(cancellationToken);
                _logger.LogInformation("[HYBRID-MODE] Connexion SQL ouverte via DataSource={DataSource}", dataSource);
                return conn;
            }
            catch (Exception ex)
            {
                await conn.DisposeAsync();
                _logger.LogWarning("[HYBRID-MODE] Tentative SQL échouée via DataSource={DataSource}. Error={Error}", dataSource, ex.Message);
            }
        }

        return null;
    }

    private int GetSapSqlCommandTimeoutSeconds()
    {
        var raw = _configuration["SapB1:SqlCommandTimeoutSeconds"];
        if (!int.TryParse(raw, out var timeout))
            timeout = 30;

        return Math.Clamp(timeout, 3, 120);
    }

    private static string BuildServiceLayerListUrl(string entity, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        return $"{entity}?$top={pageSize}&$skip={skip}";
    }

    private static string ResolveServiceLayerEntity(string tableName)
        => tableName.ToUpperInvariant() switch
        {
            "ORDR" => "Orders",
            "ODLN" => "DeliveryNotes",
            "OQUT" => "Quotations",
            "OINV" => "Invoices",
            "ORIN" => "CreditNotes",
            "ORDN" => "Returns",
            _ => "Orders"
        };

    private static bool TryResolveSqlDocumentTables(string sapEntity, out string headerTable, out string lineTable)
    {
        switch ((sapEntity ?? string.Empty).Trim())
        {
            case "Orders":
                headerTable = "ORDR";
                lineTable = "RDR1";
                return true;
            case "DeliveryNotes":
                headerTable = "ODLN";
                lineTable = "DLN1";
                return true;
            case "Quotations":
                headerTable = "OQUT";
                lineTable = "QUT1";
                return true;
            case "Invoices":
                headerTable = "OINV";
                lineTable = "INV1";
                return true;
            case "CreditNotes":
                headerTable = "ORIN";
                lineTable = "RIN1";
                return true;
            case "Returns":
                headerTable = "ORDN";
                lineTable = "RDN1";
                return true;
            default:
                headerTable = string.Empty;
                lineTable = string.Empty;
                return false;
        }
    }

    private async Task<ActionResult<ApiResponse<object>>?> GetDocumentByDocEntryViaSqlAsync(
        string sapEntity,
        int docEntry,
        CancellationToken cancellationToken)
    {
        if (!TryResolveSqlDocumentTables(sapEntity, out var headerTable, out var lineTable))
            return null;

        var connectionString = BuildSapSqlConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var isInvoice = string.Equals(headerTable, "OINV", StringComparison.OrdinalIgnoreCase);

        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
                return null;
            await using (conn)
            {

                var headerSql = isInvoice
                    ? $@"SELECT TOP 1 DocEntry, DocNum, CardCode, CardName, DocDate, DocDueDate, DocTotal, PaidToDate, DocStatus, CANCELED, Comments, DocCur
FROM {headerTable}
WHERE DocEntry = @docEntry;"
                    : $@"SELECT TOP 1 DocEntry, DocNum, CardCode, CardName, DocDate, DocDueDate, DocTotal, DocStatus, CANCELED, Comments, DocCur
FROM {headerTable}
WHERE DocEntry = @docEntry;";

            await using var headerCmd = new SqlCommand(headerSql, conn);
            headerCmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            headerCmd.Parameters.Add(new SqlParameter("@docEntry", SqlDbType.Int) { Value = docEntry });

            await using var headerReader = await headerCmd.ExecuteReaderAsync(cancellationToken);
            if (!await headerReader.ReadAsync(cancellationToken))
                return Ok(new ApiResponse<object>(true, null, null));

            var header = new Dictionary<string, object?>
            {
                ["DocEntry"] = Convert.ToInt32(headerReader["DocEntry"]),
                ["DocNum"] = Convert.ToInt32(headerReader["DocNum"]),
                ["CardCode"] = headerReader["CardCode"]?.ToString() ?? string.Empty,
                ["CardName"] = headerReader["CardName"]?.ToString() ?? string.Empty,
                ["DocDate"] = headerReader["DocDate"] is DateTime docDate ? docDate : null,
                ["DocDueDate"] = headerReader["DocDueDate"] is DateTime dueDate ? dueDate : null,
                ["DocTotal"] = headerReader["DocTotal"] is DBNull ? 0m : Convert.ToDecimal(headerReader["DocTotal"]),
                ["PaidToDate"] = isInvoice && headerReader["PaidToDate"] is not DBNull ? Convert.ToDecimal(headerReader["PaidToDate"]) : 0m,
                ["DocStatus"] = headerReader["DocStatus"]?.ToString() ?? string.Empty,
                ["DocumentStatus"] = headerReader["DocStatus"]?.ToString() ?? string.Empty,
                ["CANCELED"] = headerReader["CANCELED"]?.ToString() ?? string.Empty,
                ["Comments"] = headerReader["Comments"]?.ToString() ?? string.Empty,
                ["DocCurrency"] = headerReader["DocCur"]?.ToString() ?? string.Empty
            };

            await headerReader.CloseAsync();

            var lines = new List<Dictionary<string, object?>>();
            var lineSql = $@"SELECT LineNum, ItemCode, Dscription, Quantity, Price, DiscPrcnt, VatPrcnt, WhsCode, LineStatus, LineTotal
FROM {lineTable}
WHERE DocEntry = @docEntry
ORDER BY LineNum ASC;";

            await using var lineCmd = new SqlCommand(lineSql, conn);
            lineCmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            lineCmd.Parameters.Add(new SqlParameter("@docEntry", SqlDbType.Int) { Value = docEntry });

            await using var lineReader = await lineCmd.ExecuteReaderAsync(cancellationToken);
            while (await lineReader.ReadAsync(cancellationToken))
            {
                var unitPrice = lineReader["Price"] is DBNull ? 0m : Convert.ToDecimal(lineReader["Price"]);

                lines.Add(new Dictionary<string, object?>
                {
                    ["LineNum"] = lineReader["LineNum"] is DBNull ? null : Convert.ToInt32(lineReader["LineNum"]),
                    ["ItemCode"] = lineReader["ItemCode"]?.ToString() ?? string.Empty,
                    ["ItemName"] = lineReader["Dscription"]?.ToString() ?? string.Empty,
                    ["Dscription"] = lineReader["Dscription"]?.ToString() ?? string.Empty,
                    ["Quantity"] = lineReader["Quantity"] is DBNull ? 0m : Convert.ToDecimal(lineReader["Quantity"]),
                    ["UnitPrice"] = unitPrice,
                    ["Price"] = unitPrice,
                    ["DiscountPercent"] = lineReader["DiscPrcnt"] is DBNull ? 0m : Convert.ToDecimal(lineReader["DiscPrcnt"]),
                    ["VatPercent"] = lineReader["VatPrcnt"] is DBNull ? 0m : Convert.ToDecimal(lineReader["VatPrcnt"]),
                    ["WarehouseCode"] = lineReader["WhsCode"]?.ToString() ?? string.Empty,
                    ["LineStatus"] = lineReader["LineStatus"]?.ToString() ?? string.Empty,
                    ["LineTotal"] = lineReader["LineTotal"] is DBNull ? 0m : Convert.ToDecimal(lineReader["LineTotal"])
                });
            }

            header["DocumentLines"] = lines;

                var normalized = NormalizeDocumentForFrontend(JsonSerializer.SerializeToElement(header));
                return Ok(new ApiResponse<object>(true, null, normalized));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HYBRID-MODE][READ] Lecture SQL détail échouée. Entity={Entity}, DocEntry={DocEntry}", sapEntity, docEntry);
            return null;
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> GetDocumentByDocEntryAsync(
        string sapEntity,
        int docEntry,
        CancellationToken cancellationToken)
    {
        if (docEntry <= 0)
            return BadRequest(SapError("DocEntry invalide."));

        var useSqlReadForDocuments = bool.TryParse(_configuration["SapB1:UseSqlReadForDocuments"], out var sqlReadEnabled) && sqlReadEnabled;
        if (!useSqlReadForDocuments)
        {
            var directResult = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
            if (!directResult.Success)
                return StatusCode(directResult.StatusCode, SapError(directResult.ErrorMessage, directResult.Response));

            if (!directResult.Response.HasValue)
                return Ok(new ApiResponse<object>(true, null, null));

            return Ok(new ApiResponse<object>(true, null, NormalizeDocumentForFrontend(directResult.Response.Value)));
        }

        var sqlResult = await GetDocumentByDocEntryViaSqlAsync(sapEntity, docEntry, cancellationToken);
        if (sqlResult is not null)
            return sqlResult;

        _logger.LogError("[HYBRID-MODE] Lecture détail SQL impossible pour Entity={Entity}, DocEntry={DocEntry}.", sapEntity, docEntry);
        return StatusCode(500, SapError("Erreur lors du chargement du document."));
    }

    private async Task<ActionResult<ApiResponse<object>>> DeleteDocumentByDocEntryAsync(
        string sapEntity,
        int docEntry,
        CancellationToken cancellationToken,
        bool requireOpenStatus = false)
    {
        if (docEntry <= 0)
            return BadRequest(SapError("DocEntry invalide."));

        if (requireOpenStatus)
        {
            var current = await _sapService.ServiceLayerGetAsync(
                $"{sapEntity}({docEntry})?$select=DocEntry,DocumentStatus,DocStatus&$expand=DocumentLines($select=LineNum,LineStatus)",
                cancellationToken);
            if (!current.Success || !current.Response.HasValue)
                return StatusCode(current.StatusCode, SapError(current.ErrorMessage ?? "Impossible de vérifier le statut du document.", current.Response));

            var rawStatus = GetRawDocumentStatus(current.Response.Value);
            if (!IsOpenStatusFilterValue(rawStatus))
                return BadRequest(SapError("Annulation refusée: seul un devis/BC en statut Open peut être annulé."));

            if (HasClosedDocumentLines(current.Response.Value))
                return BadRequest(SapError("Annulation refusée: document avec au moins une ligne fermée."));

            var cancellationAttempts = new List<Func<Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)>>>
            {
                () => _sapService.ServiceLayerPostAsync($"{sapEntity}({docEntry})/Cancel", new { }, cancellationToken),
                () => _sapService.ServiceLayerPostAsync($"{sapEntity}({docEntry})/Close", new { }, cancellationToken),
                () => _sapService.ServiceLayerPatchAsync($"{sapEntity}({docEntry})", new
                {
                    DocumentStatus = "bost_Close",
                    Status = "closed"
                }, cancellationToken)
            };

            (bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage) cancelResult = default;
            foreach (var attempt in cancellationAttempts)
            {
                cancelResult = await attempt();
                if (cancelResult.Success)
                {
                    var refreshed = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
                    var responseData = refreshed.Success && refreshed.Response.HasValue
                        ? NormalizeDocumentForFrontend(refreshed.Response.Value)
                        : null;

                    return Ok(new ApiResponse<object>(true, "Annulation réussie.", responseData));
                }
            }

            return StatusCode(cancelResult.StatusCode, SapError(cancelResult.ErrorMessage ?? "Annulation non supportée pour cet objet SAP.", cancelResult.Response));
        }

        var result = await _sapService.ServiceLayerDeleteAsync($"{sapEntity}({docEntry})", cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        return Ok(new ApiResponse<object>(true, "Suppression réussie.", result.Response));
    }

    private static bool HasClosedDocumentLines(JsonElement document)
    {
        if (!document.TryGetProperty("DocumentLines", out var lines) || lines.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var line in lines.EnumerateArray())
        {
            var status = GetStringAny(line, "LineStatus");
            if (IsClosedLineStatus(status))
                return true;
        }

        return false;
    }

    private async Task<ActionResult<ApiResponse<object>>> CloseDocumentByDocEntryAsync(
        string sapEntity,
        int docEntry,
        CancellationToken cancellationToken)
    {
        if (docEntry <= 0)
            return BadRequest(SapError("DocEntry invalide."));

        if (!string.Equals(sapEntity, "Orders", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sapEntity, "Quotations", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sapEntity, "DeliveryNotes", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(SapError("Clôture autorisée uniquement pour Devis/BC/BL."));
        }

        var current = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})?$select=DocEntry,DocumentStatus", cancellationToken);
        if (!current.Success || !current.Response.HasValue)
            return StatusCode(current.StatusCode, SapError(current.ErrorMessage ?? "Impossible de vérifier le statut du document.", current.Response));

        var rawStatus = GetRawDocumentStatus(current.Response.Value);
        if (!IsOpenStatusFilterValue(rawStatus))
            return BadRequest(SapError("Clôture impossible: le document est déjà fermé."));

        var close = await _sapService.ServiceLayerPostAsync($"{sapEntity}({docEntry})/Close", new { }, cancellationToken);
        if (!close.Success)
        {
            var fallback = await _sapService.ServiceLayerPatchAsync($"{sapEntity}({docEntry})", new
            {
                DocumentStatus = "bost_Close",
                Status = "closed"
            }, cancellationToken);

            if (!fallback.Success)
                return StatusCode(fallback.StatusCode, SapError(fallback.ErrorMessage ?? close.ErrorMessage, fallback.Response ?? close.Response));
        }

        var refreshed = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
        var responseData = refreshed.Success && refreshed.Response.HasValue
            ? NormalizeDocumentForFrontend(refreshed.Response.Value)
            : null;

        return Ok(new ApiResponse<object>(true, "Clôture réussie.", responseData));
    }

    private async Task<ActionResult<ApiResponse<object>>> UpdateCommercialDocumentByDocEntryAsync(
        string sapEntity,
        int docEntry,
        CreateSapDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (docEntry <= 0)
            return BadRequest(SapError("DocEntry invalide."));

        if (!string.Equals(sapEntity, "Orders", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sapEntity, "Quotations", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(SapError("Modification autorisée uniquement pour les devis et bons de commande."));
        }

        var validationError = ValidateDocumentRequest(request);
        if (validationError is not null)
            return BadRequest(SapError(validationError));

        var current = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
        if (!current.Success || !current.Response.HasValue)
            return StatusCode(current.StatusCode, SapError(current.ErrorMessage ?? "Impossible de charger le document à modifier.", current.Response));

        var currentDoc = current.Response.Value;
        var rawStatus = GetRawDocumentStatus(currentDoc);
        if (!IsOpenStatusFilterValue(rawStatus))
            return BadRequest(SapError("Modification refusée: seul un devis/BC en statut Open peut être modifié."));

        if (!ValidateClosedLinesNotModified(currentDoc, request.DocumentLines, out var closedLineError))
            return BadRequest(SapError(closedLineError));

        var docCurrency = GetString(currentDoc, "DocCurrency");
        if (string.IsNullOrWhiteSpace(docCurrency))
        {
            var currencyResult = await _sapService.ServiceLayerGetAsync(
                $"BusinessPartners('{EscapeODataString(request.CardCode)}')?$select=Currency",
                cancellationToken);

            if (!currencyResult.Success || currencyResult.Response is null)
                return StatusCode(currencyResult.StatusCode, SapError(currencyResult.ErrorMessage ?? "Impossible de récupérer la devise du client.", currencyResult.Response));

            docCurrency = GetString(currencyResult.Response.Value, "Currency");
        }

        if (string.IsNullOrWhiteSpace(docCurrency))
            return BadRequest(SapError("Devise client introuvable pour ce CardCode."));

        var documentDate = request.DocDate ?? GetDate(currentDoc, "DocDate") ?? DateTime.Today;
        var (resolvedDocRate, _) = await ResolveDocRateAsync(docCurrency, documentDate, cancellationToken);

        var payload = BuildDocumentPayload(sapEntity, request, docCurrency, resolvedDocRate, defaultDocStatus: null);
        var update = await _sapService.ServiceLayerPatchAsync($"{sapEntity}({docEntry})", payload, cancellationToken);
        if (!update.Success)
            return StatusCode(update.StatusCode, SapError(update.ErrorMessage, update.Response));

        var refreshed = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
        var responseData = refreshed.Success && refreshed.Response.HasValue
            ? NormalizeDocumentForFrontend(refreshed.Response.Value)
            : null;

        return Ok(new ApiResponse<object>(true, "Mise à jour réussie.", responseData));
    }

    private static bool ValidateClosedLinesNotModified(JsonElement currentDocument, IReadOnlyList<CreateSapDocumentLineRequest> incomingLines, out string error)
    {
        error = string.Empty;

        if (!currentDocument.TryGetProperty("DocumentLines", out var sourceLines) || sourceLines.ValueKind != JsonValueKind.Array)
            return true;

        var incoming = incomingLines?.ToList() ?? [];
        var incomingByLineNum = incoming
            .Where(l => l.LineNum.HasValue)
            .ToDictionary(l => l.LineNum!.Value, l => l);

        var index = 0;
        foreach (var sourceLine in sourceLines.EnumerateArray())
        {
            var sourceLineNum = GetNullableInt(sourceLine, "LineNum");
            var sourceStatus = GetStringAny(sourceLine, "LineStatus");
            var isClosed = IsClosedLineStatus(sourceStatus);

            if (!isClosed)
            {
                index++;
                continue;
            }

            CreateSapDocumentLineRequest? candidate = null;
            if (sourceLineNum.HasValue && incomingByLineNum.TryGetValue(sourceLineNum.Value, out var byLineNum))
            {
                candidate = byLineNum;
            }
            else if (index < incoming.Count)
            {
                candidate = incoming[index];
            }

            if (candidate is null)
            {
                error = $"Ligne fermée #{sourceLineNum ?? index}: suppression impossible.";
                return false;
            }

            if (HasClosedLineChanged(sourceLine, candidate))
            {
                error = $"Ligne fermée #{sourceLineNum ?? index}: modification interdite.";
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool HasClosedLineChanged(JsonElement sourceLine, CreateSapDocumentLineRequest incoming)
    {
        var sourceItemCode = GetString(sourceLine, "ItemCode");
        var sourceWarehouse = GetString(sourceLine, "WarehouseCode");
        var sourceQuantity = GetDecimal(sourceLine, "Quantity");
        var sourcePrice = GetDecimal(sourceLine, "UnitPrice");
        if (sourcePrice <= 0)
            sourcePrice = GetDecimal(sourceLine, "Price");

        var sourceDiscount = GetDecimal(sourceLine, "DiscountPercent");
        var sourceVat = GetDecimal(sourceLine, "VatPercent");
        if (sourceVat <= 0)
            sourceVat = GetDecimal(sourceLine, "TaxPercent");

        var incomingItemCode = (incoming.ItemCode ?? string.Empty).Trim();
        var incomingWarehouse = (incoming.WarehouseCode ?? string.Empty).Trim();
        var incomingPrice = GetLinePrice(incoming);

        return !string.Equals(sourceItemCode, incomingItemCode, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(sourceWarehouse, incomingWarehouse, StringComparison.OrdinalIgnoreCase)
               || !AreDecimalValuesEquivalent(sourceQuantity, incoming.Quantity)
               || !AreDecimalValuesEquivalent(sourcePrice, incomingPrice)
               || !AreDecimalValuesEquivalent(sourceDiscount, incoming.DiscountPercent ?? 0)
               || !AreDecimalValuesEquivalent(sourceVat, incoming.VatPercent ?? 0);
    }

    private static bool IsClosedLineStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim().ToLowerInvariant();
        var compact = raw.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return raw is "c" or "closed" or "close"
               || compact is "bostclose" or "bostclosed"
               || compact.Contains("close");
    }

    private static bool AreDecimalValuesEquivalent(decimal left, decimal right)
        => Math.Abs(left - right) <= 0.0001m;

    private async Task<ActionResult<ApiResponse<object>>> CreateCommercialDocumentAsync(
        string sapEntity,
        CreateSapDocumentRequest request,
        CancellationToken cancellationToken,
        string? defaultDocStatus = null)
    {
        var validationError = ValidateDocumentRequest(request);
        if (validationError is not null)
            return BadRequest(SapError(validationError));

        var currencyResult = await _sapService.ServiceLayerGetAsync(
            $"BusinessPartners('{EscapeODataString(request.CardCode)}')?$select=Currency",
            cancellationToken);

        if (!currencyResult.Success || currencyResult.Response is null)
            return StatusCode(currencyResult.StatusCode, SapError(currencyResult.ErrorMessage ?? "Impossible de récupérer la devise du client.", currencyResult.Response));

        var docCurrency = GetString(currencyResult.Response.Value, "Currency");
        if (string.IsNullOrWhiteSpace(docCurrency))
            return BadRequest(SapError("Devise client introuvable pour ce CardCode."));

        var documentDate = request.DocDate ?? DateTime.Today;
        var (resolvedDocRate, rateSource) = await ResolveDocRateAsync(docCurrency, documentDate, cancellationToken);

        if (!resolvedDocRate.HasValue)
        {
            return BadRequest(SapError($"Taux de change introuvable pour la devise {docCurrency} à la date {documentDate:yyyy-MM-dd}"));
        }

        _logger.LogInformation(
            "SAP document creation {Entity} - CardCode={CardCode}, DocCurrency={DocCurrency}, DocDate={DocDate}, DocRate={DocRate}, RateSource={RateSource}",
            sapEntity,
            request.CardCode,
            docCurrency,
            documentDate.ToString("yyyy-MM-dd"),
            resolvedDocRate,
            rateSource);

        var payload = BuildDocumentPayload(sapEntity, request, docCurrency, resolvedDocRate, defaultDocStatus);
        return await CreateRawAsync(sapEntity, payload, cancellationToken);
    }

    private async Task<ActionResult<ApiResponse<object>>> CreateRawAsync(string sapEntity, object payload, CancellationToken cancellationToken)
    {
        var isInvoice = string.Equals(sapEntity, "Invoices", StringComparison.OrdinalIgnoreCase);

        var result = await _sapService.ServiceLayerPostAsync(sapEntity, payload, cancellationToken);
        if (!result.Success)
        {
            if (isInvoice)
                _logger.LogError("[HYBRID-MODE][WRITE-ERROR] Échec de la création de facture. Entity={Entity}, ErrorMessage={ErrorMessage}", sapEntity, result.ErrorMessage);

            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));
        }

        var docEntry = result.Response.HasValue ? GetInt(result.Response.Value, "DocEntry") : 0;
        if (docEntry > 0)
        {
            var createdDoc = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
            if (createdDoc.Success && createdDoc.Response.HasValue)
            {
                if (isInvoice)
                    _logger.LogInformation("[HYBRID-MODE][WRITE-SUCCESS] Facture créée avec succès. DocEntry={DocEntry}", docEntry);

                return StatusCode(result.StatusCode,
                    new ApiResponse<object>(true, "Création réussie.", NormalizeDocumentForFrontend(createdDoc.Response.Value)));
            }
        }

        var fallbackData = result.Response.HasValue
            ? NormalizeDocumentForFrontend(result.Response.Value)
            : null;

        if (isInvoice && docEntry > 0)
            _logger.LogWarning("[HYBRID-MODE][WRITE-SUCCESS-PARTIAL] Facture créée mais récupération post-création échouée. DocEntry={DocEntry}", docEntry);

        return StatusCode(result.StatusCode, new ApiResponse<object>(true, "Création réussie.", fallbackData));
    }

    private async Task<(bool Success, CreateSapDocumentRequest? Request, string? ErrorMessage)> BuildFromSourceDocumentAsync(
        string sourceEntity,
        int sourceDocEntry,
        IReadOnlyCollection<int>? selectedLineNums,
        CancellationToken cancellationToken)
    {
        var sourceResult = await _sapService.ServiceLayerGetAsync(
            $"{sourceEntity}({sourceDocEntry})",
            cancellationToken);

        if (!sourceResult.Success || sourceResult.Response is null)
            return (false, null, sourceResult.ErrorMessage ?? "Impossible de charger le document source.");

        var source = sourceResult.Response.Value;
        var cardCode = GetString(source, "CardCode");
        if (string.IsNullOrWhiteSpace(cardCode))
            return (false, null, "Impossible de générer: CardCode manquant dans le document source.");

        if (!source.TryGetProperty("DocumentLines", out var sourceLines) || sourceLines.ValueKind != JsonValueKind.Array)
            return (false, null, "Impossible de générer: DocumentLines manquant dans le document source.");

        var selected = (selectedLineNums ?? [])
            .Where(n => n >= 0)
            .Distinct()
            .ToHashSet();

        var lines = new List<CreateSapDocumentLineRequest>();
        var lineIndex = 0;
        foreach (var line in sourceLines.EnumerateArray())
        {
            lineIndex++;
            var lineNum = GetNullableInt(line, "LineNum") ?? (lineIndex - 1);
            if (selected.Count > 0 && !selected.Contains(lineNum))
                continue;

            var itemCode = GetString(line, "ItemCode");
            if (string.IsNullOrWhiteSpace(itemCode))
                return (false, null, $"Impossible de générer: ligne {lineIndex}, ItemCode manquant.");

            var quantity = GetDecimal(line, "Quantity");
            if (quantity <= 0)
                return (false, null, $"Impossible de générer: ligne {lineIndex}, Quantity invalide.");

            var warehouseCode = GetString(line, "WarehouseCode");
            if (string.IsNullOrWhiteSpace(warehouseCode))
                return (false, null, $"Impossible de générer: ligne {lineIndex}, WarehouseCode manquant.");

            var unitPrice = GetDecimal(line, "UnitPrice");
            if (unitPrice <= 0)
                unitPrice = GetDecimal(line, "Price");

            var discountPercent = GetDecimal(line, "DiscountPercent");
            if (discountPercent <= 0)
                discountPercent = GetDecimal(line, "DiscPrcnt");

            var vatPercent = GetDecimal(line, "VatPercent");
            if (vatPercent <= 0)
                vatPercent = GetDecimal(line, "TaxPercent");

            if (unitPrice <= 0)
                return (false, null, $"Impossible de générer: ligne {lineIndex}, UnitPrice/Price invalide.");

            lines.Add(new CreateSapDocumentLineRequest
            {
                LineNum = lineNum,
                ItemCode = itemCode,
                Quantity = quantity,
                WarehouseCode = warehouseCode,
                UnitPrice = unitPrice,
                Price = unitPrice,
                DiscountPercent = discountPercent > 0 ? discountPercent : null,
                VatPercent = vatPercent > 0 ? vatPercent : null,
                BaseType = ResolveDocObjectCode(sourceEntity),
                BaseEntry = sourceDocEntry,
                BaseLine = lineNum
            });
        }

        if (lines.Count == 0)
            return (false, null, "Impossible de générer: aucune ligne valide dans le document source.");

        var request = new CreateSapDocumentRequest
        {
            CardCode = cardCode,
            DocDate = GetDate(source, "DocDate") ?? DateTime.Today,
            DocDueDate = GetDate(source, "DocDueDate") ?? GetDate(source, "DocDate") ?? DateTime.Today,
            RequiredDate = GetDate(source, "RequriedDate"),
            Comments = GetString(source, "Comments"),
            SalesPersonCode = GetNullableInt(source, "SalesPersonCode"),
            DocType = GetString(source, "DocType"),
            UserSign = GetNullableInt(source, "UserSign"),
            DocumentLines = lines
        };

        return (true, request, null);
    }

    private static string? ValidateDocumentRequest(CreateSapDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CardCode))
            return "CardCode est obligatoire.";

        if (request.DocumentLines.Count == 0)
            return "DocumentLines est obligatoire.";

        if (!request.DocDueDate.HasValue && !request.RequiredDate.HasValue)
            return "DocDueDate ou RequiredDate est obligatoire.";

        if (request.DocumentLines.Any(l =>
                string.IsNullOrWhiteSpace(l.ItemCode) ||
                l.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(l.WarehouseCode) ||
                GetLinePrice(l) <= 0))
        {
            return "Chaque ligne doit contenir ItemCode, Quantity > 0, WarehouseCode et Price/UnitPrice.";
        }

        return null;
    }

    private Dictionary<string, object?> BuildDocumentPayload(string sapEntity, CreateSapDocumentRequest request, string docCurrency, decimal? resolvedDocRate, string? defaultDocStatus)
    {
        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = request.CardCode,
            ["DocDate"] = (request.DocDate ?? DateTime.Today).ToString("yyyy-MM-dd"),
            ["DocDueDate"] = (request.DocDueDate ?? request.RequiredDate ?? DateTime.Today).ToString("yyyy-MM-dd"),
            ["DocCurrency"] = docCurrency,
            ["Comments"] = request.Comments,
            ["DocObjectCode"] = ResolveDocObjectCode(sapEntity),
            ["DocumentLines"] = request.DocumentLines.Select(x =>
            {
                var linePayload = new Dictionary<string, object?>
                {
                    ["ItemCode"] = x.ItemCode,
                    ["Quantity"] = x.Quantity,
                    ["WarehouseCode"] = x.WarehouseCode,
                    ["Price"] = GetLinePrice(x),
                    ["UnitPrice"] = GetLinePrice(x)
                };

                if (x.DiscountPercent.HasValue)
                    linePayload["DiscountPercent"] = x.DiscountPercent.Value;

                if (x.VatPercent.HasValue)
                    linePayload["VatPercent"] = x.VatPercent.Value;

                if (x.LineNum.HasValue)
                    linePayload["LineNum"] = x.LineNum.Value;

                if (!string.IsNullOrWhiteSpace(x.BaseType))
                    linePayload["BaseType"] = x.BaseType;

                if (x.BaseEntry.HasValue)
                    linePayload["BaseEntry"] = x.BaseEntry.Value;

                if (x.BaseLine.HasValue)
                    linePayload["BaseLine"] = x.BaseLine.Value;

                if (!string.IsNullOrWhiteSpace(x.LineStatus))
                    linePayload["LineStatus"] = x.LineStatus;

                return linePayload;
            }).ToList()
        };

        if (request.RequiredDate.HasValue)
            payload["RequriedDate"] = request.RequiredDate.Value.ToString("yyyy-MM-dd");

        if (request.SalesPersonCode.HasValue)
            payload["SalesPersonCode"] = request.SalesPersonCode.Value;

        if (request.Series.HasValue)
            payload["Series"] = request.Series.Value;

        if (!string.IsNullOrWhiteSpace(request.DocObjectCode))
            payload["DocObjectCode"] = request.DocObjectCode;

        if (!string.IsNullOrWhiteSpace(request.DocType))
            payload["DocType"] = request.DocType;

        if (resolvedDocRate.HasValue)
            payload["DocRate"] = resolvedDocRate.Value;

        if (request.UserSign.HasValue)
            payload["UserSign"] = request.UserSign.Value;

        if (!string.IsNullOrWhiteSpace(request.DocStatus))
            payload["DocStatus"] = request.DocStatus;
        else if (!string.IsNullOrWhiteSpace(defaultDocStatus))
            payload["DocStatus"] = defaultDocStatus;

        return payload;
    }

    private static Dictionary<string, object?> BuildBusinessPartnerPayload(CreateSapClientRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = request.CardCode,
            ["CardName"] = request.CardName,
            ["CardType"] = ResolveBusinessPartnerType(request),
            ["Currency"] = NormalizeBusinessPartnerCurrency(request.Currency)
        };

        if (request.CreditLimit.HasValue)
            payload["CreditLimit"] = request.CreditLimit.Value;

        if (!string.IsNullOrWhiteSpace(request.Phone1))
            payload["Phone1"] = request.Phone1;

        if (!string.IsNullOrWhiteSpace(request.Cellular))
            payload["Cellular"] = request.Cellular;

        if (!string.IsNullOrWhiteSpace(request.EmailAddress))
            payload["EmailAddress"] = request.EmailAddress;

        if (int.TryParse(request.GroupCode, out var groupCode) && groupCode > 0)
            payload["GroupCode"] = groupCode;

        if (!string.IsNullOrWhiteSpace(request.DebitorAccount))
            payload["DebitorAccount"] = request.DebitorAccount;

        if (!string.IsNullOrWhiteSpace(request.PeymentMethodCode))
            payload["PeymentMethodCode"] = request.PeymentMethodCode;

        if (!string.IsNullOrWhiteSpace(request.Country) ||
            !string.IsNullOrWhiteSpace(request.City) ||
            !string.IsNullOrWhiteSpace(request.Address))
        {
            payload["BPAddresses"] = new[]
            {
                new
                {
                    AddressName = "Main",
                    AddressType = "bo_BillTo",
                    Country = request.Country,
                    City = request.City,
                    Street = request.Address
                }
            };
        }

        if (!string.IsNullOrWhiteSpace(request.ContactPerson))
        {
            payload["ContactEmployees"] = new[]
            {
                new
                {
                    Name = request.ContactPerson
                }
            };
        }

        return payload;
    }

    private static string ResolveBusinessPartnerType(CreateSapClientRequest request)
    {
        var raw = string.IsNullOrWhiteSpace(request.CardType)
            ? request.PartnerType
            : request.CardType;

        var normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized is "clead" or "clid" or "lead" or "prospect")
            return "cLid";

        if (normalized is "csupplier" or "supplier" or "vendor" or "fournisseur")
            return "cSupplier";

        return "cCustomer";
    }

    private static string NormalizeBusinessPartnerCurrency(string? currency)
    {
        var raw = (currency ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "EUR";

        var normalized = raw.ToLowerInvariant();
        if (normalized is "toutesdevises" or "allcurrencies" or "all-currencies" or "##")
            return "##";

        return raw;
    }

    private static string NormalizeBusinessPartnerTypeForDisplay(string? cardType)
    {
        var raw = (cardType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var normalized = raw.ToLowerInvariant();
        return normalized switch
        {
            "clid" or "lead" or "prospect" or "l" => "Prospect",
            "ccustomer" or "customer" or "client" or "c" => "Client",
            "csupplier" or "supplier" or "vendor" or "fournisseur" or "s" => "Fournisseur",
            _ => raw
        };
    }

    private static string ResolveDocObjectCode(string sapEntity)
        => sapEntity switch
        {
            "Orders" => "17",
            "DeliveryNotes" => "15",
            "Invoices" => "13",
            "Quotations" => "23",
            "CreditNotes" => "14",
            "Returns" => "16",
            _ => "17"
        };

    private static decimal GetLinePrice(CreateSapDocumentLineRequest line)
        => line.Price ?? line.UnitPrice;

    private static string EscapeODataString(string value)
        => value.Replace("'", "''");

    private async Task<(decimal? Rate, string Source)> ResolveDocRateAsync(string docCurrency, DateTime docDate, CancellationToken cancellationToken)
    {
        var localCurrency = _configuration["SapB1ServiceLayer:LocalCurrency"];
        if (!string.IsNullOrWhiteSpace(localCurrency) &&
            string.Equals(localCurrency, docCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return (1m, "local-currency-fallback");
        }

        var escapedCurrency = EscapeODataString(docCurrency);
        var date = docDate.ToString("yyyy-MM-dd");

        var b1Function = await _sapService.ServiceLayerPostAsync(
            "SBOBobService_GetCurrencyRate",
            new
            {
                Currency = docCurrency,
                Date = date
            },
            cancellationToken);

        var b1Rate = ExtractRateFromObject(b1Function.Response);
        if (b1Function.Success && b1Rate.HasValue)
            return (b1Rate, "SBOBobService_GetCurrencyRate(yyyy-MM-dd)");

        var b1FunctionCompactDate = await _sapService.ServiceLayerPostAsync(
            "SBOBobService_GetCurrencyRate",
            new
            {
                Currency = docCurrency,
                Date = docDate.ToString("yyyyMMdd")
            },
            cancellationToken);

        b1Rate = ExtractRateFromObject(b1FunctionCompactDate.Response);
        if (b1FunctionCompactDate.Success && b1Rate.HasValue)
            return (b1Rate, "SBOBobService_GetCurrencyRate(yyyyMMdd)");

        var b1FunctionRateDate = await _sapService.ServiceLayerPostAsync(
            "SBOBobService_GetCurrencyRate",
            new
            {
                Currency = docCurrency,
                RateDate = date
            },
            cancellationToken);

        b1Rate = ExtractRateFromObject(b1FunctionRateDate.Response);
        if (b1FunctionRateDate.Success && b1Rate.HasValue)
            return (b1Rate, "SBOBobService_GetCurrencyRate(RateDate)");

        var byDate = await _sapService.ServiceLayerGetAsync(
            $"ExchangeRates?$filter=Currency eq '{escapedCurrency}' and RateDate eq '{date}'&$top=1",
            cancellationToken);

        var byDateRate = ExtractRate(byDate.Response);
        if (byDate.Success && byDateRate.HasValue)
            return (byDateRate, "ExchangeRates(date)");

        var fallback = await _sapService.ServiceLayerGetAsync(
            $"ExchangeRates?$filter=Currency eq '{escapedCurrency}'&$orderby=RateDate desc&$top=1",
            cancellationToken);

        var fallbackRate = ExtractRate(fallback.Response);
        if (fallbackRate.HasValue)
            return (fallbackRate, "ExchangeRates(latest)");

        if (string.Equals(docCurrency, "MAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(docCurrency, "EUR", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Aucun taux trouvé pour {Currency} le {Date}. Fallback DocRate=1.",
                docCurrency,
                docDate.ToString("yyyy-MM-dd"));
            return (1m, "eur-mad-fallback");
        }

        return (null, "not-found");
    }

    private static decimal? ExtractRate(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = values.EnumerateArray().FirstOrDefault();
        var rate = GetDecimal(first, "Rate");
        if (rate > 0) return rate;

        rate = GetDecimal(first, "ExchangeRate");
        return rate > 0 ? rate : null;
    }

    private static decimal? ExtractRateFromObject(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object)
            return null;

        var rate = GetDecimal(response.Value, "Rate");
        if (rate > 0) return rate;

        rate = GetDecimal(response.Value, "ExchangeRate");
        if (rate > 0) return rate;

        rate = GetDecimal(response.Value, "Value");
        return rate > 0 ? rate : null;
    }

    private static IReadOnlyList<DocumentViewDto> MapBusinessPartners(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(node =>
        {
            var creditLimit = GetDecimal(node, "CreditLimit");
            if (creditLimit <= 0)
                creditLimit = GetDecimal(node, "CreditLine");

            if (creditLimit <= 0)
                creditLimit = GetDecimal(node, "MaxCommitment");

            return new DocumentViewDto
            {
                Code = GetString(node, "CardCode"),
                Name = GetString(node, "CardName"),
                CardCode = GetString(node, "CardCode"),
                CardName = GetString(node, "CardName"),
                Phone1 = GetString(node, "Phone1"),
                Cellular = GetString(node, "Cellular"),
                EmailAddress = GetString(node, "EmailAddress"),
                Currency = GetString(node, "Currency"),
                CreditLimit = creditLimit,
                Total = creditLimit,
                CardType = NormalizeBusinessPartnerTypeForDisplay(GetStringAny(node, "CardType")),
                GroupCode = GetStringAny(node, "GroupCode"),
                Country = GetString(node, "Country"),
                City = GetString(node, "City"),
                Address = GetString(node, "Address"),
                ContactPerson = GetString(node, "ContactPerson"),
                OpenOrdersBalance = GetDecimal(node, "OpenOrdersBalance"),
                DebitorAccount = GetString(node, "DebitorAccount"),
                PeymentMethodCode = GetString(node, "PeymentMethodCode")
            };
        }).ToList();
    }

    private static IReadOnlyList<EncaissementClientDto> MapEncaissementClients(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(node =>
        {
            var creditLimit = GetDecimal(node, "CreditLimit");
            if (creditLimit <= 0)
                creditLimit = GetDecimal(node, "CreditLine");

            return new EncaissementClientDto
            {
                CardCode = GetString(node, "CardCode"),
                CardName = GetString(node, "CardName"),
                Currency = GetString(node, "Currency"),
                CreditLimit = creditLimit
            };
        }).ToList();
    }

    private static IReadOnlyList<EncaissementInvoiceDto> MapOpenInvoices(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(node =>
        {
            var docTotal = GetDecimal(node, "DocTotal");
            var paidToDate = GetDecimal(node, "PaidToDate");
            var openAmount = ResolveOpenAmount(node);

            var status = GetString(node, "DocumentStatus");
            if (string.IsNullOrWhiteSpace(status))
                status = GetString(node, "DocStatus");

            return new EncaissementInvoiceDto
            {
                DocEntry = GetInt(node, "DocEntry"),
                DocNum = GetInt(node, "DocNum"),
                CardCode = GetString(node, "CardCode"),
                CardName = GetString(node, "CardName"),
                DocDate = GetDate(node, "DocDate"),
                DocDueDate = GetDate(node, "DocDueDate"),
                DocCurrency = GetString(node, "DocCurrency"),
                DocTotal = docTotal,
                PaidToDate = paidToDate,
                OpenAmount = openAmount,
                DocStatus = openAmount > 0 ? "O" : status
            };
        }).ToList();
    }

    private static decimal ResolveOpenAmount(JsonElement invoiceNode)
    {
        var openSum = GetDecimal(invoiceNode, "OpenSum");
        if (openSum > 0)
            return openSum;

        var openBal = GetDecimal(invoiceNode, "OpenBal");
        if (openBal > 0)
            return openBal;

        var openBalFc = GetDecimal(invoiceNode, "OpenBalFC");
        if (openBalFc > 0)
            return openBalFc;

        var openBalance = GetDecimal(invoiceNode, "OpenBalance");
        if (openBalance > 0)
            return openBalance;

        var docTotal = GetDecimal(invoiceNode, "DocTotal");
        var paidToDate = GetDecimal(invoiceNode, "PaidToDate");
        var computed = docTotal - paidToDate;
        if (computed > 0)
            return computed;

        var docTotalFc = GetDecimal(invoiceNode, "DocTotalFC");
        var paidFc = GetDecimal(invoiceNode, "PaidFC");
        var computedFc = docTotalFc - paidFc;
        if (computedFc > 0)
            return computedFc;

        var status = GetString(invoiceNode, "DocumentStatus");
        if (string.IsNullOrWhiteSpace(status))
            status = GetStringAny(invoiceNode, "DocumentStatus");

        var normalizedStatus = status.Trim().ToLowerInvariant();
        if (normalizedStatus is "o" or "open" or "bost_open" or "bo_open" || normalizedStatus.Contains("open"))
            return 0.01m;

        return 0m;
    }

    private static (decimal OpenAmount, decimal DocTotal, decimal PaidToDate, decimal OpenBal, decimal OpenBalFc, decimal DocTotalFc, decimal PaidFc, string RawStatus, bool IsCancelled, string DocCurrency) ReadInvoiceTrace(JsonElement invoiceNode)
    {
        var docTotal = GetDecimal(invoiceNode, "DocTotal");
        var paidToDate = GetDecimal(invoiceNode, "PaidToDate");
        var openBal = GetDecimal(invoiceNode, "OpenBal");
        var openBalFc = GetDecimal(invoiceNode, "OpenBalFC");
        var docTotalFc = GetDecimal(invoiceNode, "DocTotalFC");
        var paidFc = GetDecimal(invoiceNode, "PaidFC");

        return (
            ResolveOpenAmount(invoiceNode),
            docTotal,
            paidToDate,
            openBal,
            openBalFc,
            docTotalFc,
            paidFc,
            GetRawDocumentStatus(invoiceNode),
            IsCancelled(invoiceNode),
            GetString(invoiceNode, "DocCurrency"));
    }

    private async Task<(bool Found, decimal DocTotal, decimal PaidToDate, decimal OpenBal, decimal OpenBalFc, string DocStatus, string Canceled, string DocCur)> ReadInvoiceSqlTraceAsync(int docEntry, CancellationToken cancellationToken)
    {
        var connectionString = BuildSapSqlConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString) || docEntry <= 0)
            return (false, 0m, 0m, 0m, 0m, string.Empty, string.Empty, string.Empty);

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            const string sql = @"
SELECT TOP 1 DocTotal, PaidToDate, OpenBal, OpenBalFC, DocStatus, CANCELED, DocCur
FROM OINV
WHERE DocEntry = @docEntry;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            cmd.Parameters.Add(new SqlParameter("@docEntry", SqlDbType.Int) { Value = docEntry });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return (false, 0m, 0m, 0m, 0m, string.Empty, string.Empty, string.Empty);

            var docTotal = reader["DocTotal"] is DBNull ? 0m : Convert.ToDecimal(reader["DocTotal"]);
            var paidToDate = reader["PaidToDate"] is DBNull ? 0m : Convert.ToDecimal(reader["PaidToDate"]);
            var openBal = reader["OpenBal"] is DBNull ? 0m : Convert.ToDecimal(reader["OpenBal"]);
            var openBalFc = reader["OpenBalFC"] is DBNull ? 0m : Convert.ToDecimal(reader["OpenBalFC"]);
            var docStatus = reader["DocStatus"]?.ToString() ?? string.Empty;
            var canceled = reader["CANCELED"]?.ToString() ?? string.Empty;
            var docCur = reader["DocCur"]?.ToString() ?? string.Empty;

            return (true, docTotal, paidToDate, openBal, openBalFc, docStatus, canceled, docCur);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ENCAISSEMENT][TRACE] SQL trace failed for DocEntry={DocEntry}", docEntry);
            return (false, 0m, 0m, 0m, 0m, string.Empty, string.Empty, string.Empty);
        }
    }

    private static IReadOnlyList<SapItemDto> MapItems(
        JsonElement? response,
        Dictionary<string, List<SapItemWarehouseDto>>? warehousesByItem = null)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(node => MapItem(node, warehousesByItem)).ToList();
    }

    private static SapItemDto MapItem(JsonElement node, Dictionary<string, List<SapItemWarehouseDto>>? warehousesByItem)
    {
        var warehouses = new List<SapItemWarehouseDto>();
        var itemCode = GetString(node, "ItemCode");

        if (warehousesByItem is not null &&
            !string.IsNullOrWhiteSpace(itemCode) &&
            warehousesByItem.TryGetValue(itemCode, out var mappedWarehouses))
        {
            warehouses = mappedWarehouses;
        }

        if (node.TryGetProperty("ItemWarehouseInfoCollection", out var warehouseArray) &&
            warehouseArray.ValueKind == JsonValueKind.Array)
        {
            warehouses = warehouseArray.EnumerateArray()
                .Select(w => new SapItemWarehouseDto
                {
                    WarehouseCode = GetString(w, "WarehouseCode"),
                    InStock = GetDecimal(w, "InStock")
                })
                .ToList();
        }

        decimal price = 0m;
        string currency = string.Empty;
        if (node.TryGetProperty("ItemPrices", out var itemPrices) &&
            itemPrices.ValueKind == JsonValueKind.Array)
        {
            var firstPrice = itemPrices.EnumerateArray().FirstOrDefault();
            price = GetDecimal(firstPrice, "Price");
            currency = GetString(firstPrice, "Currency");
        }

        if (price <= 0m)
        {
            price = GetDecimal(node, "AvgPrice");
        }

        return new SapItemDto
        {
            ItemCode = itemCode,
            ItemName = GetString(node, "ItemName"),
            Price = price,
            Currency = currency,
            StockTotal = warehouses.Count > 0 ? warehouses.Sum(x => x.InStock) : GetDecimal(node, "OnHand"),
            Warehouses = warehouses
        };
    }

    private static Dictionary<string, List<SapItemWarehouseDto>> MapWarehousesByItem(JsonElement? response)
    {
        var map = new Dictionary<string, List<SapItemWarehouseDto>>(StringComparer.OrdinalIgnoreCase);

        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var node in values.EnumerateArray())
        {
            var itemCode = GetString(node, "ItemCode");
            if (string.IsNullOrWhiteSpace(itemCode))
                continue;

            var warehouse = new SapItemWarehouseDto
            {
                WarehouseCode = GetString(node, "WarehouseCode"),
                InStock = GetDecimal(node, "InStock")
            };

            if (!map.TryGetValue(itemCode, out var list))
            {
                list = [];
                map[itemCode] = list;
            }

            list.Add(warehouse);
        }

        return map;
    }

    private static IReadOnlyList<DocumentViewDto> MapDocuments(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(node =>
        {
            var rawStatus = GetRawDocumentStatus(node);
            var normalizedStatus = NormalizeDocumentStatus(rawStatus, node);

            return new DocumentViewDto
            {
                DocEntry = GetInt(node, "DocEntry"),
                DocNum = GetInt(node, "DocNum"),
                CardCode = GetString(node, "CardCode"),
                CardName = GetString(node, "CardName"),
                Date = GetDate(node, "DocDate"),
                Total = GetDecimal(node, "DocTotal"),
                Status = normalizedStatus,
                DocStatus = rawStatus,
                DocumentStatus = rawStatus,
                IsCancelled = IsCancelled(node)
            };
        }).ToList();
    }

    private static IReadOnlyList<DocumentViewDto> MapInvoiceDocuments(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object ||
            !response.Value.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return values.EnumerateArray().Select(node =>
        {
            var rawStatus = GetRawDocumentStatus(node);
            var isCancelled = IsCancelled(node);
            var hasOpenBalance = ResolveOpenAmount(node) > 0;
            var normalizedStatus = isCancelled
                ? "Cancelled"
                : hasOpenBalance ? "Open" : NormalizeDocumentStatus(rawStatus, node);

            return new DocumentViewDto
            {
                DocEntry = GetInt(node, "DocEntry"),
                DocNum = GetInt(node, "DocNum"),
                CardCode = GetString(node, "CardCode"),
                CardName = GetString(node, "CardName"),
                Date = GetDate(node, "DocDate"),
                Total = GetDecimal(node, "DocTotal"),
                Status = normalizedStatus,
                DocStatus = rawStatus,
                DocumentStatus = rawStatus,
                IsCancelled = isCancelled
            };
        }).ToList();
    }

    private static string GetRawDocumentStatus(JsonElement node)
    {
        var status = GetString(node, "DocumentStatus");
        if (!string.IsNullOrWhiteSpace(status)) return status;

        status = GetString(node, "DocStatus");
        if (!string.IsNullOrWhiteSpace(status)) return status;

        status = GetStringAny(node, "DocumentStatus");
        if (!string.IsNullOrWhiteSpace(status)) return status;

        return GetStringAny(node, "DocStatus");
    }

    private static string NormalizeDocumentStatus(string rawStatus, JsonElement node)
    {
        if (IsCancelled(node)) return "Cancelled";

        var status = (rawStatus ?? string.Empty).Trim();
        var lower = status.ToLowerInvariant();

        if (lower is "o" or "open" or "bost_open" or "bo_open" or "0") return "Open";
        if (lower is "c" or "closed" or "close" or "bost_close" or "bo_close" or "1") return "Closed";

        if (lower.Contains("open")) return "Open";
        if (lower.Contains("close")) return "Closed";
        if (lower.Contains("cancel")) return "Cancelled";

        return string.IsNullOrWhiteSpace(status) ? "Open" : status;
    }

    private static bool IsCancelled(JsonElement node)
    {
        var cancelled = GetStringAny(node, "CANCELED");
        if (string.IsNullOrWhiteSpace(cancelled))
            cancelled = GetStringAny(node, "Canceled");
        if (string.IsNullOrWhiteSpace(cancelled))
            cancelled = GetStringAny(node, "Cancelled");
        if (string.IsNullOrWhiteSpace(cancelled))
            cancelled = GetStringAny(node, "CancelStatus");

        if (string.IsNullOrWhiteSpace(cancelled))
            return false;

        var normalized = cancelled.Trim().ToLowerInvariant();
        return normalized is "tyes" or "yes" or "y" or "true" or "cancelled" or "canceled";
    }

    private static object NormalizeDocumentForFrontend(JsonElement source)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(source.GetRawText())
            ?? new Dictionary<string, object?>();

        var rawStatus = GetRawDocumentStatus(source);
        var normalizedStatus = NormalizeDocumentStatus(rawStatus, source);

        data["status"] = normalizedStatus;
        data["Status"] = normalizedStatus;
        data["DocStatus"] = rawStatus;
        data["DocumentStatus"] = rawStatus;
        data["IsCancelled"] = IsCancelled(source);

        return data;
    }

    private static string GetString(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string GetStringAny(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static decimal GetDecimal(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return 0m;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedInvariant)) return parsedInvariant;
                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsedCurrent)) return parsedCurrent;

                var normalized = raw.Replace(',', '.');
                if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedNormalized)) return parsedNormalized;
            }
        }
        return 0m;
    }

    private static int GetInt(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0;
    }

    private static int? GetNullableInt(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }

    private static DateTime? GetDate(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var date)) return date;
        return null;
    }

    private static object SapError(string? error, JsonElement? sapResponse = null)
        => new
        {
            success = false,
            message = "Erreur SAP",
            error = error ?? "Erreur inconnue",
            sapResponse
        };
}

public class CreateSapClientRequest
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public decimal? CreditLimit { get; set; }
    public string Phone1 { get; set; } = string.Empty;
    public string Cellular { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string DebitorAccount { get; set; } = string.Empty;
    public string PeymentMethodCode { get; set; } = string.Empty;
}

public class RegisterInvoicePaymentRequest
{
    public string CardCode { get; set; } = string.Empty;
    public string PaymentMethodCode { get; set; } = string.Empty;
    public decimal CashSum { get; set; }
    public decimal CreditSum { get; set; }
}

public class RegisterEncaissementRequest
{
    public string CardCode { get; set; } = string.Empty;
    public string PaymentMethodCode { get; set; } = string.Empty;
    public decimal CashSum { get; set; }
    public decimal CreditSum { get; set; }
    public List<RegisterEncaissementInvoiceRequest> Invoices { get; set; } = [];
}

public class RegisterEncaissementInvoiceRequest
{
    public int DocEntry { get; set; }
    public decimal SumApplied { get; set; }
}

public class GenerateFromSourceRequest
{
    public List<int> SelectedLineNums { get; set; } = [];
}

public class CreateSapDocumentRequest
{
    public string CardCode { get; set; } = string.Empty;
    public DateTime? DocDate { get; set; }
    public DateTime? DocDueDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Comments { get; set; }
    public int? SalesPersonCode { get; set; }
    public int? Series { get; set; }
    public string? DocObjectCode { get; set; }
    public string? DocType { get; set; }
    public decimal? DocRate { get; set; }
    public int? UserSign { get; set; }
    public string? DocStatus { get; set; }
    public List<CreateSapDocumentLineRequest> DocumentLines { get; set; } = [];
}

public class EncaissementClientDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
}

public class EncaissementInvoiceDto
{
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public DateTime? DocDate { get; set; }
    public DateTime? DocDueDate { get; set; }
    public string DocCurrency { get; set; } = string.Empty;
    public decimal DocTotal { get; set; }
    public decimal PaidToDate { get; set; }
    public decimal OpenAmount { get; set; }
    public string DocStatus { get; set; } = string.Empty;
}

public class CreateSapDocumentLineRequest
{
    public int? LineNum { get; set; }
    public string? LineStatus { get; set; }
    public string? BaseType { get; set; }
    public int? BaseEntry { get; set; }
    public int? BaseLine { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal? Price { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? VatPercent { get; set; }
}

public class DocumentViewDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone1 { get; set; } = string.Empty;
    public string Cellular { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public string CardType { get; set; } = string.Empty;
    public string GroupCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public decimal OpenOrdersBalance { get; set; }
    public string DebitorAccount { get; set; } = string.Empty;
    public string PeymentMethodCode { get; set; } = string.Empty;
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal Total { get; set; }
    public decimal PaidToDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DocStatus { get; set; } = string.Empty;
    public string DocumentStatus { get; set; } = string.Empty;
    public bool IsCancelled { get; set; }
}

public class SapItemDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal StockTotal { get; set; }
    public List<SapItemWarehouseDto> Warehouses { get; set; } = [];
}

public class SapItemWarehouseDto
{
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal InStock { get; set; }
}
