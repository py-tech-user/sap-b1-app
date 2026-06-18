using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Controllers;

[ApiController]
[Route("api/sap")]
public class SapB1Controller : ControllerBase
{
    private readonly ISapB1Service _sapService;
    private readonly ICurrentUserService _currentUserService;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SapB1Controller> _logger;
    private readonly IMemoryCache _cache;

    public SapB1Controller(
        ISapB1Service sapService,
        ICurrentUserService currentUserService,
        AppDbContext db,
        IConfiguration configuration,
        ILogger<SapB1Controller> logger,
        IMemoryCache cache)
    {
        _sapService = sapService;
        _currentUserService = currentUserService;
        _db = db;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
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
    [Authorize]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetClients(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
        => GetBusinessPartnersViaSqlAsync(page, pageSize, cancellationToken);


    [HttpPost("clients")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> CreateClient([FromBody] CreateSapClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardName))
            return BadRequest(SapError("La Raison sociale est obligatoire."));

        var nextCardCode = await GetNextBusinessPartnerCardCodeAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(nextCardCode))
            return BadRequest(SapError("Impossible de generer automatiquement le code partenaire."));

        request.CardCode = nextCardCode;
        _logger.LogInformation("Creation partenaire SAP: CardCode genere automatiquement = {CardCode}", request.CardCode);

        var payload = BuildBusinessPartnerPayload(request);
        _logger.LogInformation("Creation partenaire SAP: payload BusinessPartners envoye au Service Layer = {Payload}",
            JsonSerializer.Serialize(payload));

        return await CreateRawAsync("BusinessPartners", payload, cancellationToken);
    }


    [HttpGet("clients/series")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SapSeriesDto>>>> GetClientSeries(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return Ok(new ApiResponse<IReadOnlyList<SapSeriesDto>>(true, null, [], 0));
    }

    [HttpGet("items")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SapItemDto>>>> GetItems(
        [FromQuery] int? groupCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50000);
        var hasGroupFilter = groupCode.HasValue && groupCode.Value >= 0;
        var itemsCacheKey = hasGroupFilter
            ? $"sap:items:group:{groupCode!.Value}"
            : "sap:items:all";
        var pagedCacheKey = $"{itemsCacheKey}:p{page}:s{pageSize}";
        if (_cache.TryGetValue(pagedCacheKey, out ApiResponse<IReadOnlyList<SapItemDto>>? cachedPaged) && cachedPaged is not null)
            return Ok(cachedPaged);

        var configuredPriceList = await ResolveDefaultPriceListAsync(cancellationToken);

        var sql = @"
SELECT
    I.ItemCode,
    I.ItemName,
    ISNULL(I.ItmsGrpCod, 0) AS GroupCode,
    ISNULL(G.ItmsGrpNam, '') AS GroupName,
    ISNULL(PL_CFG.Price, ISNULL(I.AvgPrice, 0)) AS Price,
    ISNULL(PL_CFG.Currency, '') AS PriceCurrency,
    ISNULL(I.OnHand, 0) AS OnHand,
    ISNULL(I.PicturName, '') AS PicturName
FROM OITM I
LEFT JOIN OITB G ON G.ItmsGrpCod = I.ItmsGrpCod
LEFT JOIN ITM1 PL_CFG ON PL_CFG.ItemCode = I.ItemCode AND PL_CFG.PriceList = @priceList
WHERE (@groupCode IS NULL OR ISNULL(I.ItmsGrpCod, 0) = @groupCode)
ORDER BY I.ItemCode
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        var countSql = @"
SELECT COUNT(1)
FROM OITM I
WHERE (@groupCode IS NULL OR ISNULL(I.ItmsGrpCod, 0) = @groupCode);";

        var items = new List<SapItemDto>();
        int totalCount;
        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
            {
                return StatusCode(500, SapError("Connexion SQL SAP impossible pour le catalogue."));
            }

            await using (conn)
            {
                await using (var countCmd = new SqlCommand(countSql, conn))
                {
                    countCmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                    countCmd.Parameters.Add(new SqlParameter("@groupCode", SqlDbType.Int) { Value = hasGroupFilter ? groupCode!.Value : DBNull.Value });
                    totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
                }

                await using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                    cmd.Parameters.Add(new SqlParameter("@priceList", SqlDbType.Int) { Value = configuredPriceList > 0 ? configuredPriceList : 1 });
                    cmd.Parameters.Add(new SqlParameter("@groupCode", SqlDbType.Int) { Value = hasGroupFilter ? groupCode!.Value : DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = (page - 1) * pageSize });
                    cmd.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize });
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var itemName = reader["ItemName"]?.ToString() ?? string.Empty;
                        var picture = reader["PicturName"]?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(picture))
                        {
                            picture = GuessPictureFileNameFromItemName(itemName);
                        }
                        items.Add(new SapItemDto
                        {
                            ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                            ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                            GroupCode = reader["GroupCode"] is DBNull ? 0 : Convert.ToInt32(reader["GroupCode"]),
                            GroupName = reader["GroupName"]?.ToString() ?? string.Empty,
                            ImageUrl = BuildItemImageUrl(reader["ItemCode"]?.ToString() ?? string.Empty, picture),
                            Price = reader["Price"] is DBNull ? 0m : Convert.ToDecimal(reader["Price"]),
                            Currency = reader["PriceCurrency"]?.ToString() ?? string.Empty,
                            StockTotal = reader["OnHand"] is DBNull ? 0m : Convert.ToDecimal(reader["OnHand"]),
                            Warehouses = []
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur SQL lors du chargement du catalogue SAP.");
            return StatusCode(500, SapError("Erreur SQL lors du chargement du catalogue."));
        }

        var payload = new ApiResponse<IReadOnlyList<SapItemDto>>(true, null, items, totalCount);
        _cache.Set(pagedCacheKey, payload, TimeSpan.FromMinutes(2));
        return Ok(payload);
    }

    [HttpGet("item-groups")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetItemGroups(CancellationToken cancellationToken)
    {
        const string itemGroupsCacheKey = "sap:item-groups:all";
        if (_cache.TryGetValue(itemGroupsCacheKey, out List<object>? cachedGroups) && cachedGroups is not null)
            return Ok(new ApiResponse<IReadOnlyList<object>>(true, null, cachedGroups, cachedGroups.Count));

        const string sql = @"
SELECT
    ISNULL(B.ItmsGrpCod, 0) AS GroupCode,
    ISNULL(B.ItmsGrpNam, '') AS GroupName,
    COUNT(1) AS ItemsCount
FROM OITM I
LEFT JOIN OITB B ON B.ItmsGrpCod = I.ItmsGrpCod
GROUP BY B.ItmsGrpCod, B.ItmsGrpNam
ORDER BY B.ItmsGrpNam, B.ItmsGrpCod;";

        var groups = new List<object>();
        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
                return StatusCode(500, SapError("Connexion SQL SAP impossible pour les groupes d articles."));

            await using (conn)
            await using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    groups.Add(new
                    {
                        groupCode = reader["GroupCode"] is DBNull ? 0 : Convert.ToInt32(reader["GroupCode"]),
                        groupName = reader["GroupName"]?.ToString() ?? string.Empty,
                        itemsCount = reader["ItemsCount"] is DBNull ? 0 : Convert.ToInt32(reader["ItemsCount"])
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur SQL lors du chargement des groupes d articles SAP.");
            return StatusCode(500, SapError("Erreur SQL lors du chargement des groupes d articles."));
        }

        _cache.Set(itemGroupsCacheKey, groups, TimeSpan.FromMinutes(5));
        return Ok(new ApiResponse<IReadOnlyList<object>>(true, null, groups, groups.Count));
    }

    [HttpGet("item-images/{*fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetItemImage(string fileName, CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(safeFileName))
            return BadRequest(SapError("Nom de fichier image invalide."));

        var roots = await GetItemPictureRootsAsync(cancellationToken);
        foreach (var root in roots)
        {
            string rootFullPath;
            try
            {
                rootFullPath = Path.GetFullPath(root);
            }
            catch
            {
                continue;
            }

            foreach (var candidateFileName in BuildImageFileNameCandidates(safeFileName))
            {
                string imageFullPath;
                try
                {
                    imageFullPath = Path.GetFullPath(Path.Combine(rootFullPath, candidateFileName));
                }
                catch
                {
                    continue;
                }

                if (!imageFullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!System.IO.File.Exists(imageFullPath))
                continue;

                return PhysicalFile(imageFullPath, GetImageContentType(imageFullPath));
            }
        }

        _logger.LogWarning("Image article introuvable. File={FileName}; Roots={Roots}",
            safeFileName, string.Join(" | ", roots));
        return NotFound(SapError($"Image introuvable: {safeFileName}"));
    }

    [HttpGet("item-images/by-item/{itemCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetItemImageByItemCode(string itemCode, CancellationToken cancellationToken)
    {
        var safeItemCode = (itemCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeItemCode))
            return BadRequest(SapError("ItemCode image invalide."));

        // Images: priorité Service Layer ItemImages, puis fallback local SQL/fichier.
        var (sessionOk, sessionId, _, _, sessionError) = await _sapService.LoginServiceLayerWithSessionIdAsync(cancellationToken);
        if (sessionOk && !string.IsNullOrWhiteSpace(sessionId))
        {
            var slUrl = _configuration["SapB1ServiceLayer:ServiceLayerUrl"]?.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(slUrl))
            {
                try
                {
                    var handler = new HttpClientHandler();
                    if (bool.TryParse(_configuration["SapB1ServiceLayer:IgnoreSslErrors"], out var ignoreSsl) && ignoreSsl)
                    {
                        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }

                    using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{slUrl}/ItemImages('{Uri.EscapeDataString(safeItemCode)}')/$value");
                    request.Headers.Add("Cookie", $"B1SESSION={sessionId}");
                    using var response = await client.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                        return File(bytes, contentType);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ItemImages Service Layer indisponible pour ItemCode={ItemCode}", safeItemCode);
                }
            }
        }
        else
        {
            _logger.LogWarning("Session Service Layer indisponible pour image item {ItemCode}. Error={Error}", safeItemCode, sessionError);
        }

        var pictureName = await ResolvePictureNameByItemCodeAsync(safeItemCode, cancellationToken);
        if (!string.IsNullOrWhiteSpace(pictureName))
            return await GetItemImage(pictureName, cancellationToken);

        return NotFound(SapError($"Image introuvable pour item: {safeItemCode}"));
    }

    [HttpGet("orders")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetOrders(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100000,
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
        [FromQuery] int pageSize = 100000,
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
        [FromQuery] int pageSize = 100000,
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

    [HttpPut("delivery-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateDeliveryNote(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateCommercialDocumentByDocEntryAsync("DeliveryNotes", docEntry, request, cancellationToken);

    [HttpDelete("delivery-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteDeliveryNote(int docEntry, CancellationToken cancellationToken)
        => DeleteDocumentByDocEntryAsync("DeliveryNotes", docEntry, cancellationToken, requireOpenStatus: true);

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
        [FromQuery] int pageSize = 100000,
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

    [HttpPut("bl/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateBonLivraison(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateDeliveryNote(docEntry, request, cancellationToken);

    [HttpPost("bl/{docEntry:int}/close")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> CloseBonLivraison(int docEntry, CancellationToken cancellationToken)
        => CloseDeliveryNote(docEntry, cancellationToken);

    [HttpGet("quotes")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetQuotes(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100000,
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
        [FromQuery] int pageSize = 100000,
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
        [FromQuery] int pageSize = 100000,
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
        => DeleteDocumentByDocEntryAsync("Invoices", docEntry, cancellationToken, requireOpenStatus: true);

    [HttpPut("factures/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateInvoice(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateCommercialDocumentByDocEntryAsync("Invoices", docEntry, request, cancellationToken);

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

        var invoiceOpenAmount = ResolveOpenAmount(invoice);
        var amountAppliedToInvoice = Math.Min(totalPaid, invoiceOpenAmount);
        var walletCreditAdded = Math.Max(0m, totalPaid - amountAppliedToInvoice);

        if (walletCreditAdded > 0)
        {
            await AddWalletCreditAsync(cardCode, walletCreditAdded, cancellationToken);
            await CreateSapAdvanceOnAccountAsync(cardCode, walletCreditAdded, cancellationToken);
        }

        if (amountAppliedToInvoice <= 0)
        {
            var walletOnlyBalance = await GetWalletBalanceAsync(cardCode, cancellationToken);
            return Ok(new ApiResponse<object>(true, "Encaissement enregistre sur solde client.", new
            {
                payment = (object?)null,
                invoice = (object?)null,
                walletCreditAdded,
                walletApplied = 0m,
                walletRemaining = walletOnlyBalance
            }));
        }

        var sapCashSum = amountAppliedToInvoice;
        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = cardCode,
            ["DocDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
            ["DocCurrency"] = GetString(invoice, "DocCurrency"),
            ["CashSum"] = sapCashSum, // On utilise CashSum car c'est le mode le plus simple pour solder via Service Layer sans config bancaire complexe
            ["PaymentInvoices"] = new[]
            {
                new
                {
                    DocEntry = invoiceDocEntry,
                    SumApplied = amountAppliedToInvoice,
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

        var walletBalance = await GetWalletBalanceAsync(cardCode, cancellationToken);
        return Ok(new ApiResponse<object>(true, "Encaissement enregistré.", new
        {
            payment = paymentResult.Response,
            invoice = invoiceStatus,
            walletCreditAdded,
            walletApplied = 0m,
            walletRemaining = walletBalance
        }));
    }

    [HttpGet("encaissement/clients")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EncaissementClientDto>>>> GetEncaissementClients(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ENCAISSEMENT] Loading customers for payment screen.");

        var isAdmin = _currentUserService.IsAdmin();
        var salesPersonCode = _currentUserService.GetSapSalesPersonCode();
        if (!isAdmin && salesPersonCode <= 0)
            return Forbid();

        var clients = new List<EncaissementClientDto>();
        var nextUrl = "BusinessPartners?$select=CardCode,CardName,Currency,CreditLimit,SalesPersonCode&$filter=CardType eq 'cCustomer'";
        if (!isAdmin)
            nextUrl += $" and SalesPersonCode eq {salesPersonCode}";
        nextUrl += "&$orderby=CardCode asc&$top=2000";
        var guard = 0;

        while (!string.IsNullOrWhiteSpace(nextUrl) && guard++ < 100)
        {
            var result = await _sapService.ServiceLayerGetAsync(nextUrl, cancellationToken);
            if (!result.Success)
            {
                _logger.LogError("[ENCAISSEMENT] Failed to load customers. Error={Error}", result.ErrorMessage);
                return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));
            }

            clients.AddRange(MapEncaissementClients(result.Response));
            nextUrl = ExtractServiceLayerNextLink(result.Response);
        }

        clients = clients
            .Where(c => !string.IsNullOrWhiteSpace(c.CardCode))
            .GroupBy(c => c.CardCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.CardCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

            var walletMap = await GetWalletBalancesAsync(clients.Select(c => c.CardCode), cancellationToken);
        foreach (var client in clients)
        {
            if (walletMap.TryGetValue(client.CardCode, out var wallet))
                client.AdvanceBalance = wallet;
        }

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
            var isAdmin = _currentUserService.IsAdmin();
            var salesPersonCode = _currentUserService.GetSapSalesPersonCode();
            if (!isAdmin && salesPersonCode <= 0)
            {
                return Ok(new ApiResponse<IReadOnlyList<EncaissementInvoiceDto>>(true, null, [], 0));
            }

            var sqlInvoices = new List<EncaissementInvoiceDto>();

            await using var conn = new SqlConnection(sqlConnectionString);
            await conn.OpenAsync(cancellationToken);

                const string sql = @"
SELECT DocEntry, DocNum, CardCode, CardName, DocDate, DocDueDate, DocCur, DocTotal, PaidToDate, DocTotalFC, PaidFC, DocStatus, CANCELED
FROM OINV
WHERE CardCode = @cardCode
  AND (@isAdmin = 1 OR (@salesPersonCode > 0 AND SlpCode = @salesPersonCode))
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
            cmd.Parameters.Add(new SqlParameter("@isAdmin", SqlDbType.Bit) { Value = isAdmin });
            cmd.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = salesPersonCode });

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

    [HttpGet("encaissement/clients/{cardCode}/balance")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetEncaissementClientBalance(
        string cardCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
            return BadRequest(SapError("CardCode est obligatoire."));

        var normalized = cardCode.Trim();
        var balance = await GetWalletBalanceAsync(normalized, cancellationToken);
        return Ok(new ApiResponse<object>(true, null, new
        {
            cardCode = normalized,
            advanceBalance = balance
        }));
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
            return BadRequest(SapError("Le montant Cash doit être >= 0."));

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
        var freshPaid = Math.Max(0m, request.CashSum) + Math.Max(0m, request.CreditSum);
        var walletBefore = request.UseAdvance
            ? await GetWalletBalanceAsync(request.CardCode, cancellationToken)
            : 0m;
        var totalAvailable = freshPaid + walletBefore;
        var amountToApply = Math.Min(totalAvailable, totalSelected);

        var remainingToApply = amountToApply;
        decimal totalAppliedBuilt = 0m;
        var paymentInvoices = new List<object>();
        foreach (var invoice in orderedInvoices)
        {
            if (remainingToApply <= 0) break;

            var sumApplied = Math.Min(invoice.OpenAmount, remainingToApply);
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

        var walletApplied = Math.Max(0m, amountToApply - freshPaid);
        var walletCreditAdded = Math.Max(0m, freshPaid - amountToApply);

        _logger.LogInformation(
            "[ENCAISSEMENT] Posting payment to SAP. CardCode={CardCode}, PaymentMethodCode={PaymentMethodCode}, FreshPaid={FreshPaid}, WalletBefore={WalletBefore}, AmountToApply={AmountToApply}, TotalSelected={TotalSelected}",
            request.CardCode,
            request.PaymentMethodCode,
            freshPaid,
            walletBefore,
            amountToApply,
            totalSelected);

        JsonElement? paymentResponse = null;
        if (amountToApply > 0)
        {
            var sapPayload = new Dictionary<string, object?>
            {
                ["CardCode"] = request.CardCode,
                ["DocDate"] = DateTime.Today.ToString("yyyy-MM-dd"),
                // SAP recoit uniquement le montant applique aux factures.
                ["CashSum"] = amountToApply,
                ["PaymentInvoices"] = paymentInvoices
            };
            _logger.LogInformation("[ENCAISSEMENT][TRACE][PAYLOAD] {@Payload}", sapPayload);
            
            var paymentResult = await _sapService.ServiceLayerPostAsync("IncomingPayments", sapPayload, cancellationToken);
            if (!paymentResult.Success)
            {
                _logger.LogError("[ENCAISSEMENT] SAP payment registration failed. CardCode={CardCode}, Error={Error}", request.CardCode, paymentResult.ErrorMessage);
                return StatusCode(paymentResult.StatusCode, SapError(paymentResult.ErrorMessage, paymentResult.Response));
            }

            if (walletApplied > 0)
                await ConsumeWalletCreditAsync(request.CardCode, walletApplied, cancellationToken);

            if (walletCreditAdded > 0)
                await AddWalletCreditAsync(request.CardCode, walletCreditAdded, cancellationToken);

            paymentResponse = paymentResult.Response;
            _logger.LogInformation("[ENCAISSEMENT] Payment registration succeeded. CardCode={CardCode}, InvoiceCount={InvoiceCount}", request.CardCode, request.Invoices.Count);
        }


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
            payment = paymentResponse,
            invoices = refreshedInvoices,
            totalSelected,
            cashSumApplied = totalAppliedBuilt,
            walletBefore,
            walletApplied,
            walletCreditAdded,
            walletRemaining = await GetWalletBalanceAsync(request.CardCode, cancellationToken)
        }));
    }

    private async Task<Dictionary<string, decimal>> GetSapCustomerAdvanceBalancesAsync(IEnumerable<string> cardCodes, CancellationToken cancellationToken)
    {
        var normalized = cardCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (normalized.Count == 0)
            return result;

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return result;

        await using (conn)
        {
            var inSql = string.Join(",", normalized.Select((_, i) => $"@p{i}"));
            var sql = $@"
SELECT CardCode, ISNULL(Balance, 0) AS Balance
FROM OCRD
WHERE CardCode IN ({inSql});";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            for (var i = 0; i < normalized.Count; i++)
                cmd.Parameters.AddWithValue($"@p{i}", normalized[i]);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader["CardCode"]?.ToString() ?? string.Empty;
                var balance = reader["Balance"] is DBNull ? 0m : Convert.ToDecimal(reader["Balance"]);
                // Sur un client, un solde negatif signifie un avoir/avance disponible.
                result[code] = balance < 0 ? Math.Abs(balance) : 0m;
            }
        }

        return result;
    }

    private async Task<decimal> GetSapCustomerAdvanceBalanceAsync(string cardCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode))
            return 0m;
        var map = await GetSapCustomerAdvanceBalancesAsync(new[] { cardCode }, cancellationToken);
        return map.TryGetValue(cardCode.Trim(), out var value) ? value : 0m;
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
        [FromQuery] int pageSize = 100000,
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

    [HttpPut("credit-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateCreditNote(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateCommercialDocumentByDocEntryAsync("CreditNotes", docEntry, request, cancellationToken);

    [HttpDelete("credit-notes/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteCreditNote(int docEntry, CancellationToken cancellationToken)
        => DeleteDocumentByDocEntryAsync("CreditNotes", docEntry, cancellationToken, requireOpenStatus: true);

    [HttpGet("returns")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetReturns(
        [FromQuery] bool openOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100000,
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

    [HttpPut("returns/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> UpdateReturn(int docEntry, [FromBody] CreateSapDocumentRequest request, CancellationToken cancellationToken)
        => UpdateCommercialDocumentByDocEntryAsync("Returns", docEntry, request, cancellationToken);

    [HttpDelete("returns/{docEntry:int}")]
    [AllowAnonymous]
    public Task<ActionResult<ApiResponse<object>>> DeleteReturn(int docEntry, CancellationToken cancellationToken)
        => DeleteDocumentByDocEntryAsync("Returns", docEntry, cancellationToken, requireOpenStatus: true);

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
        pageSize = Math.Clamp(pageSize, 1, 500);
        var allItems = new List<DocumentViewDto>();
        var isAdmin = _currentUserService.IsAdmin();
        var salesPersonCode = _currentUserService.GetSapSalesPersonCode();
        if (!isAdmin && salesPersonCode <= 0)
            return Forbid();

        var queryBase = "BusinessPartners?$select=CardCode,CardName,Phone1,Cellular,EmailAddress,Currency,CreditLimit,CardType,GroupCode,Country,City,Address,SalesPersonCode";
        var nextUrl = $"{queryBase}&$orderby=CardCode desc&$top=10000";
        var guard = 0;

        while (!string.IsNullOrWhiteSpace(nextUrl) && guard++ < 1000)
        {
            var result = await _sapService.ServiceLayerGetAsync(nextUrl, cancellationToken);
            if (!result.Success)
                return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

            allItems.AddRange(MapBusinessPartners(result.Response));
            nextUrl = ExtractServiceLayerNextLink(result.Response);
        }

        if (!isAdmin)
        {
            allItems = allItems
                .Where(x => x.SalesPersonCode == salesPersonCode)
                .ToList();
        }

        var totalCount = allItems.Count;
        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, allItems, totalCount));
    }


    private async Task<ActionResult<ApiResponse<IReadOnlyList<DocumentViewDto>>>> GetBusinessPartnersViaSqlAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 10000);
        var isAdmin = _currentUserService.IsAdmin();
        var salesPersonCode = _currentUserService.GetSapSalesPersonCode();
        if (!isAdmin && salesPersonCode <= 0)
            return Forbid();

        var cacheKey = $"sap:partners-sql:{isAdmin}:{(isAdmin ? 0 : salesPersonCode)}";
        if (!_cache.TryGetValue(cacheKey, out List<DocumentViewDto>? allItems) || allItems is null)
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
                return StatusCode(500, SapError("Connexion SQL impossible."));

            await using (conn)
            {
                const string sql = @"
SELECT
  CardCode,
  CardName,
  ISNULL(Phone1, '') AS Phone1,
  ISNULL(Cellular, '') AS Cellular,
  ISNULL(E_Mail, '') AS EmailAddress,
  ISNULL(Currency, '') AS Currency,
  ISNULL(CreditLine, 0) AS CreditLimit,
  ISNULL(CardType, '') AS CardType,
  ISNULL(GroupCode, 0) AS GroupCode,
  ISNULL(Country, '') AS Country,
  ISNULL(City, '') AS City,
  ISNULL(Address, '') AS Address,
  ISNULL(SlpCode, 0) AS SalesPersonCode,
  ISNULL(CntctPrsn, '') AS ContactPerson,
  ISNULL(OrdersBal, 0) AS OpenOrdersBalance,
  ISNULL(DebPayAcct, '') AS DebitorAccount,
  ISNULL(PymCode, '') AS PeymentMethodCode
FROM OCRD
WHERE CardType = 'C'
  AND (@isAdmin = 1 OR SlpCode = @salesPersonCode)
ORDER BY CardName, CardCode;";

                allItems = new List<DocumentViewDto>();
                await using var cmd = new SqlCommand(sql, conn);
                cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                cmd.Parameters.Add(new SqlParameter("@isAdmin", SqlDbType.Bit) { Value = isAdmin });
                cmd.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = salesPersonCode });

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var cardCode = reader["CardCode"]?.ToString() ?? string.Empty;
                    var cardName = reader["CardName"]?.ToString() ?? string.Empty;
                    var cardType = reader["CardType"]?.ToString() ?? string.Empty;
                    var creditLimit = reader["CreditLimit"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["CreditLimit"]);
                    allItems.Add(new DocumentViewDto
                    {
                        Code = cardCode,
                        Name = cardName,
                        CardCode = cardCode,
                        CardName = cardName,
                        Phone1 = reader["Phone1"]?.ToString() ?? string.Empty,
                        Cellular = reader["Cellular"]?.ToString() ?? string.Empty,
                        EmailAddress = reader["EmailAddress"]?.ToString() ?? string.Empty,
                        Currency = reader["Currency"]?.ToString() ?? string.Empty,
                        CreditLimit = creditLimit,
                        Total = creditLimit,
                        CardType = NormalizeBusinessPartnerTypeForDisplay(cardType),
                        GroupCode = reader["GroupCode"]?.ToString() ?? string.Empty,
                        Country = reader["Country"]?.ToString() ?? string.Empty,
                        City = reader["City"]?.ToString() ?? string.Empty,
                        Address = reader["Address"]?.ToString() ?? string.Empty,
                        SalesPersonCode = reader["SalesPersonCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SalesPersonCode"]),
                        ContactPerson = reader["ContactPerson"]?.ToString() ?? string.Empty,
                        OpenOrdersBalance = reader["OpenOrdersBalance"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["OpenOrdersBalance"]),
                        DebitorAccount = reader["DebitorAccount"]?.ToString() ?? string.Empty,
                        PeymentMethodCode = reader["PeymentMethodCode"]?.ToString() ?? string.Empty
                    });
                }
            }

            _cache.Set(cacheKey, allItems, TimeSpan.FromSeconds(120));
        }

        var totalCount = allItems.Count;
        var items = allItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new ApiResponse<IReadOnlyList<DocumentViewDto>>(true, null, items, totalCount));
    }

    private static string? ExtractServiceLayerNextLink(JsonElement? response)
    {
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Object)
            return null;

        if (!response.Value.TryGetProperty("odata.nextLink", out var nextLinkNode) &&
            !response.Value.TryGetProperty("@odata.nextLink", out nextLinkNode))
            return null;

        var raw = nextLinkNode.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Convert absolute Service Layer URL to relative URL expected by SapB1Service.
        var marker = "/b1s/v1/";
        var idx = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return raw[(idx + marker.Length)..];

        marker = "/b1s/v2/";
        idx = raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return raw[(idx + marker.Length)..];

        return raw.TrimStart('/');
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
    /// - Lecture : exclusivement via SQL (aucun fallback Service Layer)
    /// - Écriture : via Service Layer
    /// </summary>
    [HttpGet("reporting/commercial")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CommercialReportingResponseDto>>> GetCommercialReporting(
        [FromQuery] string periodType = "month",
        [FromQuery] string? month = null,
        [FromQuery] int? quarter = null,
        [FromQuery] int? year = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? salesPersonCode = null,
        [FromQuery] string? cardCode = null,
        [FromQuery] bool includeRecentDocuments = true,
        [FromQuery] bool includeTeamPerformance = true,
        CancellationToken cancellationToken = default)
    {
        var (periodStart, periodEnd, periodLabel) = ResolveReportingPeriod(periodType, month, quarter, year, startDate, endDate);
        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPerson = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin ? salesPersonCode : currentSalesPerson;

        if (!isAdmin && scopedSalesPersonCode <= 0)
            return Forbid();

        var cacheKey = $"reporting:commercial:v2:{periodType}:{periodStart:yyyyMMdd}:{periodEnd:yyyyMMdd}:{scopedSalesPersonCode?.ToString() ?? "none"}:{isAdmin}:card:{(cardCode ?? string.Empty).Trim().ToLowerInvariant()}:docs:{includeRecentDocuments}:team:{includeTeamPerformance}";
        if (_cache.TryGetValue(cacheKey, out CommercialReportingResponseDto? cached) && cached is not null)
            return Ok(new ApiResponse<CommercialReportingResponseDto>(true, null, cached));
        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        var response = new CommercialReportingResponseDto
        {
            Mode = isAdmin ? "Admin" : "Commercial",
            PeriodLabel = periodLabel,
            SelectedSalesPersonCode = scopedSalesPersonCode > 0 ? scopedSalesPersonCode : null
        };

        List<CommercialSalesPersonPerformanceDto> allTeamPerformances = new();
        await using (conn)
        {
            response.Kpis = await LoadReportingKpisAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, cancellationToken, !includeTeamPerformance && !includeRecentDocuments);
            if (includeTeamPerformance)
            {
                response.TeamPerformances = await LoadReportingTeamPerformanceAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, cancellationToken);
                response.TopClients = await LoadTopClientsAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, 5, cancellationToken);
            }
            if (includeRecentDocuments)
                response.RecentDocuments = await LoadReportingRecentDocumentsAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, cancellationToken);
            if (isAdmin && includeTeamPerformance)
            {
                allTeamPerformances = scopedSalesPersonCode.HasValue && scopedSalesPersonCode.Value > 0
                    ? await LoadReportingTeamPerformanceAsync(conn, periodStart, periodEnd, null, cardCode, cancellationToken)
                    : response.TeamPerformances;
            }
        }

        response.TeamMembers = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Role == Roles.Commercial)
            .Select(u => new CommercialSalesPersonInfoDto
            {
                SalesPersonCode = u.SapSalesPersonCode,
                SalesPersonName = u.FullName,
                Role = u.Role
            })
            .OrderBy(u => u.SalesPersonName)
            .ToListAsync(cancellationToken);

        var commercialSalesPersonCodes = response.TeamMembers
            .Select(m => m.SalesPersonCode)
            .ToHashSet();
        response.TeamPerformances = response.TeamPerformances
            .Where(t => commercialSalesPersonCodes.Contains(t.SalesPersonCode))
            .ToList();
        allTeamPerformances = allTeamPerformances
            .Where(t => commercialSalesPersonCodes.Contains(t.SalesPersonCode))
            .ToList();

        var namesByCode = response.TeamMembers.ToDictionary(k => k.SalesPersonCode, v => v.SalesPersonName);
        foreach (var item in response.TeamPerformances)
        {
            if (namesByCode.TryGetValue(item.SalesPersonCode, out var fullName))
                item.SalesPersonName = fullName;
        }

        if (isAdmin)
        {
            foreach (var item in allTeamPerformances)
            {
                if (namesByCode.TryGetValue(item.SalesPersonCode, out var fullName))
                    item.SalesPersonName = fullName;
            }
        }

        if (!isAdmin)
        {
            response.TeamPerformances = response.TeamPerformances
                .Where(t => t.SalesPersonCode == scopedSalesPersonCode)
                .ToList();
            response.TeamMembers = response.TeamMembers
                .Where(t => t.SalesPersonCode == scopedSalesPersonCode)
                .ToList();
        }

        response.TopSalesPerson = (isAdmin ? allTeamPerformances : response.TeamPerformances)
            .OrderByDescending(t => t.OrdersAmount)
            .ThenByDescending(t => t.InvoicesAmount)
            .FirstOrDefault();
        response.SelectedSalesPersonName = response.TeamMembers
            .FirstOrDefault(m => m.SalesPersonCode == response.SelectedSalesPersonCode)?.SalesPersonName;
        response.InactiveSalesPersons = response.TeamMembers
            .Where(member => !response.TeamPerformances.Any(t => t.SalesPersonCode == member.SalesPersonCode && (t.QuotesCount + t.OrdersCount + t.InvoicesCount) > 0))
            .ToList();

        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(45));
        return Ok(new ApiResponse<CommercialReportingResponseDto>(true, null, response));
    }

    [HttpPut("reporting/monthly-target")]
    [Authorize(Policy = Policies.ManagerOrAdmin)]
    public async Task<ActionResult<ApiResponse<MonthlyTargetResponseDto>>> UpdateMonthlyTarget(
        [FromBody] MonthlyTargetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var targetSalesPersonCode = request.SalesPersonCode.GetValueOrDefault(0);
        var monthlyTarget = Math.Max(0m, request.MonthlyTarget);

        await EnsureSalesTargetsTableAsync(cancellationToken);

        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(cancellationToken);
        const string sql = @"
MERGE dbo.SalesTargets AS target
USING (SELECT @salesPersonCode AS SalesPersonCode, @monthlyTarget AS MonthlyTarget) AS source
ON target.SalesPersonCode = source.SalesPersonCode
WHEN MATCHED THEN UPDATE SET MonthlyTarget = source.MonthlyTarget, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (SalesPersonCode, MonthlyTarget, UpdatedAt) VALUES (source.SalesPersonCode, source.MonthlyTarget, SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = targetSalesPersonCode });
        cmd.Parameters.Add(new SqlParameter("@monthlyTarget", SqlDbType.Decimal) { Precision = 18, Scale = 4, Value = monthlyTarget });
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return Ok(new ApiResponse<MonthlyTargetResponseDto>(true, "Objectif CA enregistré.", new MonthlyTargetResponseDto
        {
            MonthlyTarget = monthlyTarget,
            SalesPersonCode = targetSalesPersonCode > 0 ? targetSalesPersonCode : null
        }));
    }

    [HttpGet("reporting/partners-activity")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<CommercialPartnerActivityDto>>>> GetPartnersActivity(
        [FromQuery] string? month = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? salesPersonCode = null,
        [FromQuery] string activity = "all",
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        DateTime periodStart;
        DateTime periodEnd;
        if (startDate.HasValue && endDate.HasValue)
        {
            periodStart = startDate.Value;
            periodEnd = endDate.Value;
            if (periodEnd <= periodStart)
                periodEnd = periodStart.AddHours(1);
        }
        else
        {
            periodStart = DateTime.Today;
            if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
                periodStart = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
            else
                periodStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            periodEnd = periodStart.AddMonths(1);
        }
        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPerson = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin ? salesPersonCode : currentSalesPerson;

        if (!isAdmin && scopedSalesPersonCode <= 0)
            return Forbid();

        var normalizedSearch = (search ?? string.Empty).Trim().ToLowerInvariant();
        var cacheKey = $"reporting:partners-activity:{periodStart:yyyyMMddHHmm}:{periodEnd:yyyyMMddHHmm}:{scopedSalesPersonCode?.ToString() ?? "none"}:{activity}:{normalizedSearch}";
        if (_cache.TryGetValue(cacheKey, out List<CommercialPartnerActivityDto>? cached) && cached is not null)
            return Ok(new ApiResponse<List<CommercialPartnerActivityDto>>(true, null, cached, cached.Count));

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        await using (conn)
        {
            var rows = await LoadPartnersActivityAsync(
                conn,
                periodStart,
                periodEnd,
                scopedSalesPersonCode,
                activity,
                search,
                cancellationToken);

            _cache.Set(cacheKey, rows, TimeSpan.FromSeconds(30));
            return Ok(new ApiResponse<List<CommercialPartnerActivityDto>>(true, null, rows, rows.Count));
        }
    }

    [HttpGet("reporting/partner-debts")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<PartnerDebtDto>>>> GetPartnerDebts(
        [FromQuery] int? salesPersonCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? commercialSearch = null,
        [FromQuery] string? cardCode = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPerson = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin ? salesPersonCode : currentSalesPerson;

        if (!isAdmin && scopedSalesPersonCode <= 0)
            return Forbid();

        var normalizedSearch = NormalizeReportingSearch(search);
        var normalizedCommercialSearch = NormalizeReportingSearch(commercialSearch);
        var normalizedCardCode = NormalizeReportingSearch(cardCode);
        var cacheKey = $"reporting:partner-debts:{isAdmin}:{scopedSalesPersonCode?.ToString() ?? "none"}";
        if (_cache.TryGetValue(cacheKey, out List<PartnerDebtDto>? cached) && cached is not null)
        {
            var filteredCached = FilterPartnerDebts(cached, normalizedSearch, normalizedCommercialSearch, normalizedCardCode, isAdmin);
            var totalCount = filteredCached.Count;
            var paged = filteredCached
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Ok(new ApiResponse<List<PartnerDebtDto>>(true, null, paged, totalCount));
        }

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        await using (conn)
        {
            const string sql = @"
SELECT
  BP.CardCode,
  MAX(BP.CardName) AS CardName,
  ISNULL(MAX(BP.SlpCode), 0) AS SlpCode,
  ISNULL(SUM(
    CASE
      WHEN ISNULL(I.CANCELED, 'N') <> 'Y'
           AND (ISNULL(I.DocTotal, 0) - ISNULL(I.PaidToDate, 0)) > 0.0001
      THEN (ISNULL(I.DocTotal, 0) - ISNULL(I.PaidToDate, 0))
      ELSE 0
    END
  ), 0) AS PartnerOwesCompanyAmount
FROM OCRD BP
LEFT JOIN OINV I
  ON I.CardCode = BP.CardCode
  AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
WHERE BP.CardType = 'C'
  AND (@applyScope = 0 OR BP.SlpCode = @salesPersonCode)
GROUP BY BP.CardCode
ORDER BY MAX(BP.CardName), BP.CardCode;";

            var rows = new List<PartnerDebtDto>();
            await using var cmd = new SqlCommand(sql, conn);
            AddReportingScopeParameters(cmd, scopedSalesPersonCode);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new PartnerDebtDto
                {
                    CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                    CardName = reader["CardName"]?.ToString() ?? string.Empty,
                    SalesPersonCode = reader["SlpCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SlpCode"]),
                    PartnerOwesCompanyAmount = Convert.ToDecimal(reader["PartnerOwesCompanyAmount"]),
                    CompanyOwesPartnerAmount = 0m
                });
            }

            var walletMap = await GetWalletBalancesAsync(rows.Select(r => r.CardCode), cancellationToken);
            foreach (var row in rows)
            {
                if (walletMap.TryGetValue(row.CardCode, out var walletCredit))
                    row.CompanyOwesPartnerAmount = walletCredit;
                row.Balance = row.PartnerOwesCompanyAmount - row.CompanyOwesPartnerAmount;
            }

            var userNamesByCode = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new { u.SapSalesPersonCode, u.FullName })
                .ToDictionaryAsync(x => x.SapSalesPersonCode, x => x.FullName, cancellationToken);

            foreach (var row in rows)
            {
                if (userNamesByCode.TryGetValue(row.SalesPersonCode, out var fullName))
                    row.SalesPersonName = fullName;
            }

            rows = rows
                .GroupBy(r => r.CardCode?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Where(r => Math.Abs(r.Balance) > 0.0001m)
                .OrderByDescending(r => Math.Abs(r.Balance))
                .ThenByDescending(r => r.Balance)
                .ThenBy(r => r.CardName)
                .ThenBy(r => r.CardCode)
                .ToList();

            _cache.Set(cacheKey, rows, TimeSpan.FromSeconds(120));

            var filteredRows = FilterPartnerDebts(rows, normalizedSearch, normalizedCommercialSearch, normalizedCardCode, isAdmin);
            var totalCount = filteredRows.Count;
            var paged = filteredRows
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return Ok(new ApiResponse<List<PartnerDebtDto>>(true, null, paged, totalCount));
        }
    }

    [HttpGet("reporting/evolution")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<ReportingEvolutionDto>>> GetReportingEvolution(
        [FromQuery] int months = 6,
        [FromQuery] string periodType = "",
        [FromQuery] string? month = null,
        [FromQuery] int? quarter = null,
        [FromQuery] int? year = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? salesPersonCode = null,
        [FromQuery] string? cardCode = null,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 24);
        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPerson = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin ? salesPersonCode : currentSalesPerson;

        if (!isAdmin && scopedSalesPersonCode <= 0)
            return Forbid();

        DateTime queryStart;
        DateTime queryEnd;
        if (!string.IsNullOrWhiteSpace(periodType))
        {
            var resolved = ResolveReportingPeriod(periodType, month, quarter, year, startDate, endDate);
            queryStart = resolved.Start;
            queryEnd = resolved.End;
        }
        else
        {
            var today = DateTime.Today;
            queryStart = new DateTime(today.Year, today.Month, 1).AddMonths(-months + 1);
            queryEnd = new DateTime(today.Year, today.Month, 1).AddMonths(1);
        }

        var normalizedEvolutionPeriodType = (periodType ?? string.Empty).Trim().ToLowerInvariant();
        var evolutionDays = (queryEnd.Date - queryStart.Date).TotalDays;
        var groupEvolutionByDay = normalizedEvolutionPeriodType == "week" || normalizedEvolutionPeriodType == "month" || (normalizedEvolutionPeriodType == "custom" && evolutionDays <= 62);
        var periodKeyLength = groupEvolutionByDay ? 10 : 7;

        var cacheKey = $"reporting:evolution:{queryStart:yyyyMMdd}:{queryEnd:yyyyMMdd}:{scopedSalesPersonCode?.ToString() ?? "none"}:card:{(cardCode ?? string.Empty).Trim().ToLowerInvariant()}:granularity:{(groupEvolutionByDay ? "day" : "month")}";
        if (_cache.TryGetValue(cacheKey, out ReportingEvolutionDto? cached) && cached is not null)
            return Ok(new ApiResponse<ReportingEvolutionDto>(true, null, cached));

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        await using (conn)
        {
            var result = new ReportingEvolutionDto();
            var points = new List<ReportingEvolutionPointDto>();

            var sql = $@"
SELECT PeriodKey, Revenue, PendingAmount FROM (
  SELECT CONVERT(char({periodKeyLength}), I.DocDate, 120) AS PeriodKey,
    ISNULL(SUM(I.DocTotal),0) - ISNULL((
      SELECT SUM(ISNULL(ORIN.DocTotal,0))
      FROM ORIN
      WHERE CONVERT(char({periodKeyLength}), ORIN.DocDate, 120) = CONVERT(char({periodKeyLength}), I.DocDate, 120)
        AND ISNULL(ORIN.CANCELED,'N') <> 'Y'
        AND (@applyCard = 0 OR ORIN.CardCode = @cardCode)
        AND (@applyScope = 0 OR ORIN.SlpCode = @salesPersonCode)
    ),0) AS Revenue,
    0 AS PendingAmount
  FROM OINV I
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
  GROUP BY CONVERT(char({periodKeyLength}), I.DocDate, 120)
  UNION ALL
  SELECT CONVERT(char({periodKeyLength}), O.DocDate, 120) AS PeriodKey, 0 AS Revenue,
    ISNULL(SUM(CASE WHEN ISNULL(O.DocStatus,'O') = 'O' AND ISNULL(O.CANCELED,'N') <> 'Y' THEN O.DocTotal ELSE 0 END),0) AS PendingAmount
  FROM ORDR O
  WHERE O.DocDate >= @dateFrom AND O.DocDate < @dateTo
    AND ISNULL(O.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR O.CardCode = @cardCode)
    AND (@applyScope = 0 OR O.SlpCode = @salesPersonCode)
  GROUP BY CONVERT(char({periodKeyLength}), O.DocDate, 120)
  UNION ALL
  SELECT CONVERT(char({periodKeyLength}), D.DocDate, 120) AS PeriodKey, 0 AS Revenue,
    ISNULL(SUM(CASE WHEN ISNULL(D.DocStatus,'O') = 'O' AND ISNULL(D.CANCELED,'N') <> 'Y' THEN D.DocTotal ELSE 0 END),0) AS PendingAmount
  FROM ODLN D
  WHERE D.DocDate >= @dateFrom AND D.DocDate < @dateTo
    AND ISNULL(D.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR D.CardCode = @cardCode)
    AND (@applyScope = 0 OR D.SlpCode = @salesPersonCode)
  GROUP BY CONVERT(char({periodKeyLength}), D.DocDate, 120)
) src
ORDER BY PeriodKey;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@dateFrom", SqlDbType.DateTime) { Value = queryStart.Date });
            cmd.Parameters.Add(new SqlParameter("@dateTo", SqlDbType.DateTime) { Value = queryEnd.Date });
            AddReportingScopeParameters(cmd, scopedSalesPersonCode);
            AddReportingPartnerParameters(cmd, cardCode);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var periodData = new Dictionary<string, (decimal Revenue, decimal Pending)>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = reader["PeriodKey"]?.ToString() ?? string.Empty;
                var rev = Convert.ToDecimal(reader["Revenue"]);
                var pend = Convert.ToDecimal(reader["PendingAmount"]);
                if (periodData.TryGetValue(key, out var existing))
                    periodData[key] = (existing.Revenue + rev, existing.Pending + pend);
                else
                    periodData[key] = (rev, pend);
            }

            if (groupEvolutionByDay)
            {
                for (var cursor = queryStart.Date; cursor < queryEnd.Date; cursor = cursor.AddDays(1))
                {
                    var key = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (periodData.TryGetValue(key, out var data))
                        points.Add(new ReportingEvolutionPointDto { MonthKey = key, Revenue = data.Revenue, PendingRevenue = data.Pending });
                    else
                        points.Add(new ReportingEvolutionPointDto { MonthKey = key, Revenue = 0, PendingRevenue = 0 });
                }
            }
            else
            {
                for (var cursor = new DateTime(queryStart.Year, queryStart.Month, 1); cursor < queryEnd; cursor = cursor.AddMonths(1))
                {
                    var key = $"{cursor.Year:D4}-{cursor.Month:D2}";
                    if (periodData.TryGetValue(key, out var data))
                        points.Add(new ReportingEvolutionPointDto { MonthKey = key, Revenue = data.Revenue, PendingRevenue = data.Pending });
                    else
                        points.Add(new ReportingEvolutionPointDto { MonthKey = key, Revenue = 0, PendingRevenue = 0 });
                }
            }

            result.Points = points;
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(120));
            return Ok(new ApiResponse<ReportingEvolutionDto>(true, null, result));
        }
    }

    private static List<PartnerDebtDto> FilterPartnerDebts(
        IEnumerable<PartnerDebtDto> rows,
        string normalizedSearch,
        string normalizedCommercialSearch,
        string normalizedCardCode,
        bool allowCommercialSearch)
    {
        return rows
            .Where(row =>
            {
                var matchesSelectedPartner = string.IsNullOrWhiteSpace(normalizedCardCode)
                    || NormalizeReportingSearch(row.CardCode) == normalizedCardCode;

                var matchesPartner = matchesSelectedPartner && (string.IsNullOrWhiteSpace(normalizedSearch)
                    || NormalizeReportingSearch(row.CardCode).Contains(normalizedSearch)
                    || NormalizeReportingSearch(row.CardName).Contains(normalizedSearch));

                var matchesCommercial = !allowCommercialSearch
                    || string.IsNullOrWhiteSpace(normalizedCommercialSearch)
                    || NormalizeReportingSearch(row.SalesPersonName).Contains(normalizedCommercialSearch)
                    || NormalizeReportingSearch(row.SalesPersonCode.ToString(CultureInfo.InvariantCulture)).Contains(normalizedCommercialSearch);

                return matchesPartner && matchesCommercial;
            })
            .ToList();
    }

    private static string NormalizeReportingSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray();
        return new string(chars).Normalize(NormalizationForm.FormC);
    }

    [HttpGet("reporting/quotes-to-relaunch")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<QuoteToRelaunchDto>>>> GetQuotesToRelaunch(
        [FromQuery] int minDays = 7,
        [FromQuery] int? salesPersonCode = null,
        [FromQuery] string? cardCode = null,
        CancellationToken cancellationToken = default)
    {
        minDays = Math.Max(0, minDays);
        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPerson = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin ? salesPersonCode : currentSalesPerson;

        if (!isAdmin && scopedSalesPersonCode <= 0)
            return Forbid();

        var cacheKey = $"reporting:quotes-to-relaunch:{minDays}:{scopedSalesPersonCode?.ToString() ?? "none"}";
        if (_cache.TryGetValue(cacheKey, out List<QuoteToRelaunchDto>? cached) && cached is not null)
            return Ok(new ApiResponse<List<QuoteToRelaunchDto>>(true, null, cached, cached.Count));

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        await using (conn)
        {
            var sql = $@"
SELECT Q.DocEntry, Q.DocNum, Q.CardCode, Q.CardName, Q.DocTotal, Q.DocDate, Q.SlpCode,
  DATEDIFF(day, Q.DocDate, GETDATE()) AS DaysSince
FROM OQUT Q
WHERE Q.DocStatus = 'O'
  AND ISNULL(Q.CANCELED,'N') <> 'Y'
  AND (@minDays <= 0 OR Q.DocDate < DATEADD(day, -@minDays, CAST(GETDATE() AS DATE)))
  AND NOT EXISTS (SELECT 1 FROM RDR1 R WHERE R.BaseType = 23 AND R.BaseEntry = Q.DocEntry)
  AND (@applyScope = 0 OR Q.SlpCode = @salesPersonCode)
ORDER BY Q.DocDate ASC;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@minDays", minDays);
            AddReportingScopeParameters(cmd, scopedSalesPersonCode);

            var namesByCode = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.SapSalesPersonCode > 0)
                .ToDictionaryAsync(u => u.SapSalesPersonCode, u => u.FullName, cancellationToken);

            var rows = new List<QuoteToRelaunchDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var slpCode = reader["SlpCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SlpCode"]);
                rows.Add(new QuoteToRelaunchDto
                {
                    DocEntry = Convert.ToInt32(reader["DocEntry"]),
                    DocNum = Convert.ToInt32(reader["DocNum"]),
                    CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                    CardName = reader["CardName"]?.ToString() ?? string.Empty,
                    Total = Convert.ToDecimal(reader["DocTotal"]),
                    DocDate = Convert.ToDateTime(reader["DocDate"]),
                    DaysSinceQuote = Convert.ToInt32(reader["DaysSince"]),
                    SalesPersonCode = slpCode,
                    SalesPersonName = namesByCode.TryGetValue(slpCode, out var nm) ? nm : $"#{slpCode}"
                });
            }

            _cache.Set(cacheKey, rows, TimeSpan.FromSeconds(60));
            return Ok(new ApiResponse<List<QuoteToRelaunchDto>>(true, null, rows, rows.Count));
        }
    }

    [HttpGet("reporting/admin-dashboard")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AdminDashboardDto>>> GetAdminDashboard(
        [FromQuery] string? month = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAdmin())
            return Forbid();

        DateTime periodStart, periodEnd;
        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
        {
            periodStart = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
            periodEnd = periodStart.AddMonths(1);
        }
        else
        {
            periodStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            periodEnd = periodStart.AddMonths(1);
        }

        var cacheKey = $"reporting:admin-dashboard:{periodStart:yyyyMMdd}";
        if (_cache.TryGetValue(cacheKey, out AdminDashboardDto? cached) && cached is not null)
            return Ok(new ApiResponse<AdminDashboardDto>(true, null, cached));

        var response = new AdminDashboardDto();
        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        await using (conn)
        {
            // 1. Top 5 partenaires par CA
            var topPartnersSql = @"
SELECT TOP 5 I.CardCode, MAX(BP.CardName) AS CardName,
  ISNULL(SUM(I.DocTotal),0) AS Revenue,
  ISNULL(MAX(BP.SlpCode),0) AS SlpCode
FROM OINV I
INNER JOIN OCRD BP ON BP.CardCode = I.CardCode
WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
  AND ISNULL(I.CANCELED,'N') <> 'Y'
GROUP BY I.CardCode
ORDER BY Revenue DESC;";
            await using (var cmd = new SqlCommand(topPartnersSql, conn))
            {
                AddReportingPeriodParameters(cmd, periodStart, periodEnd);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    response.TopPartners.Add(new AdminTopPartnerDto
                    {
                        CardCode = r["CardCode"]?.ToString() ?? string.Empty,
                        CardName = r["CardName"]?.ToString() ?? string.Empty,
                        Revenue = Convert.ToDecimal(r["Revenue"])
                    });
                }
            }

            // 2. Top 5 produits
            var topProductsSql = @"
SELECT TOP 5 INV1.ItemCode, ISNULL(MAX(OITM.ItemName),'') AS ItemName,
  ISNULL(SUM(INV1.Quantity),0) AS QuantitySold,
  ISNULL(SUM(INV1.LineTotal),0) AS Revenue
FROM INV1
INNER JOIN OINV ON OINV.DocEntry = INV1.DocEntry
LEFT JOIN OITM ON OITM.ItemCode = INV1.ItemCode
WHERE OINV.DocDate >= @dateFrom AND OINV.DocDate < @dateTo
  AND ISNULL(OINV.CANCELED,'N') <> 'Y'
GROUP BY INV1.ItemCode
ORDER BY Revenue DESC;";
            await using (var cmd = new SqlCommand(topProductsSql, conn))
            {
                AddReportingPeriodParameters(cmd, periodStart, periodEnd);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    response.TopProducts.Add(new AdminTopProductDto
                    {
                        ItemCode = r["ItemCode"]?.ToString() ?? string.Empty,
                        ItemName = r["ItemName"]?.ToString() ?? string.Empty,
                        QuantitySold = Convert.ToDecimal(r["QuantitySold"]),
                        Revenue = Convert.ToDecimal(r["Revenue"])
                    });
                }
            }

            // 3. CA mensuel 12 mois + courbe par commercial
            var monthlySql = @"
SELECT Yr, Mo, SlpCode, SUM(Revenue) AS Revenue, SUM(Pending) AS PendingRevenue FROM (
  SELECT YEAR(I.DocDate) AS Yr, MONTH(I.DocDate) AS Mo,
    I.SlpCode, ISNULL(SUM(I.DocTotal),0) AS Revenue, 0 AS Pending
  FROM OINV I
  WHERE I.DocDate >= DATEADD(year, -1, @dateTo) AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
  GROUP BY YEAR(I.DocDate), MONTH(I.DocDate), I.SlpCode
  UNION ALL
  SELECT YEAR(O.DocDate), MONTH(O.DocDate),
    O.SlpCode, 0,
    ISNULL(SUM(CASE WHEN ISNULL(O.DocStatus,'O') = 'O' AND ISNULL(O.CANCELED,'N') <> 'Y' THEN O.DocTotal ELSE 0 END),0)
  FROM ORDR O
  WHERE O.DocDate >= DATEADD(year, -1, @dateTo) AND O.DocDate < @dateTo
    AND ISNULL(O.CANCELED,'N') <> 'Y'
  GROUP BY YEAR(O.DocDate), MONTH(O.DocDate), O.SlpCode
  UNION ALL
  SELECT YEAR(D.DocDate), MONTH(D.DocDate),
    D.SlpCode, 0,
    ISNULL(SUM(CASE WHEN ISNULL(D.DocStatus,'O') = 'O' AND ISNULL(D.CANCELED,'N') <> 'Y' THEN D.DocTotal ELSE 0 END),0)
  FROM ODLN D
  WHERE D.DocDate >= DATEADD(year, -1, @dateTo) AND D.DocDate < @dateTo
    AND ISNULL(D.CANCELED,'N') <> 'Y'
  GROUP BY YEAR(D.DocDate), MONTH(D.DocDate), D.SlpCode
) src
GROUP BY Yr, Mo, SlpCode
ORDER BY Yr, Mo, SlpCode;";
            await using (var cmd = new SqlCommand(monthlySql, conn))
            {
                cmd.Parameters.Add(new SqlParameter("@dateFrom", SqlDbType.DateTime) { Value = DateTime.Today.AddYears(-1) });
                cmd.Parameters.Add(new SqlParameter("@dateTo", SqlDbType.DateTime) { Value = DateTime.Today.AddDays(1) });
                var salesPersonNames = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.SapSalesPersonCode > 0)
                    .ToDictionaryAsync(u => u.SapSalesPersonCode, u => u.FullName, cancellationToken);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var slpCode = r["SlpCode"] == DBNull.Value ? 0 : Convert.ToInt32(r["SlpCode"]);
                    response.MonthlyRevenue.Add(new AdminMonthlyRevenueDto
                    {
                        MonthKey = $"{r["Yr"]}-{Convert.ToInt32(r["Mo"]):D2}",
                        Revenue = Convert.ToDecimal(r["Revenue"]),
                        PendingRevenue = Convert.ToDecimal(r["PendingRevenue"]),
                        SalesPersonName = salesPersonNames.TryGetValue(slpCode, out var nm) ? nm : $"#{slpCode}"
                    });
                }
            }

            // 4. Pipeline total (devis ouverts)
            var pipelineSql = @"
SELECT ISNULL(SUM(DocTotal),0) AS PipelineAmount
FROM OQUT WHERE DocStatus = 'O' AND ISNULL(CANCELED,'N') <> 'Y';";
            await using (var cmd = new SqlCommand(pipelineSql, conn))
            await using (var r = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                if (await r.ReadAsync(cancellationToken))
                    response.TotalPipelineAmount = Convert.ToDecimal(r["PipelineAmount"]);
            }

            // 5. Factures en retard globales
            var overdueSql = @"
SELECT COUNT(1) AS Cnt, ISNULL(SUM(ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)),0) AS Amt
FROM OINV
WHERE DocDueDate < @today AND ISNULL(CANCELED,'N') <> 'Y'
  AND (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001;";
            await using (var cmd = new SqlCommand(overdueSql, conn))
            {
                cmd.Parameters.AddWithValue("@today", DateTime.Today);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await r.ReadAsync(cancellationToken))
                {
                    response.GlobalOverdueInvoicesCount = Convert.ToInt32(r["Cnt"]);
                    response.GlobalOverdueInvoicesAmount = Convert.ToDecimal(r["Amt"]);
                }
            }

            // 6. Résumé par commercial
            var commercialSummarySql = @"
SELECT I.SlpCode,
  COUNT(DISTINCT I.DocEntry) AS InvoicesCount,
  ISNULL(SUM(I.DocTotal),0) AS InvoicesAmount,
  ISNULL(SUM(I.PaidToDate),0) AS CollectedAmount,
  COUNT(DISTINCT Q.DocEntry) AS QuotesCount,
  COUNT(DISTINCT CASE WHEN QR.DocEntry IS NOT NULL THEN Q.DocEntry END) AS ConvertedQuotes,
  COUNT(DISTINCT CASE WHEN I.DocDueDate < @today2 AND (ISNULL(I.DocTotal,0) - ISNULL(I.PaidToDate,0)) > 0.0001 THEN I.DocEntry END) AS OverdueCnt,
  ISNULL(SUM(CASE WHEN I.DocDueDate < @today2 AND (ISNULL(I.DocTotal,0) - ISNULL(I.PaidToDate,0)) > 0.0001 THEN ISNULL(I.DocTotal,0) - ISNULL(I.PaidToDate,0) ELSE 0 END),0) AS OverdueAmt
FROM OINV I
LEFT JOIN OQUT Q ON Q.DocDate >= @dateFrom AND Q.DocDate < @dateTo AND Q.SlpCode = I.SlpCode
LEFT JOIN (SELECT DISTINCT RDR1.BaseEntry AS DocEntry FROM RDR1 WHERE RDR1.BaseType = 23) QR ON QR.DocEntry = Q.DocEntry
WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
  AND ISNULL(I.CANCELED,'N') <> 'Y'
GROUP BY I.SlpCode
ORDER BY InvoicesAmount DESC;";
            await using (var cmd2 = new SqlCommand(commercialSummarySql, conn))
            {
                AddReportingPeriodParameters(cmd2, periodStart, periodEnd);
                cmd2.Parameters.AddWithValue("@today2", DateTime.Today);
                var namesDict = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.SapSalesPersonCode > 0)
                    .ToDictionaryAsync(u => u.SapSalesPersonCode, u => u.FullName, cancellationToken);
                await using var r = await cmd2.ExecuteReaderAsync(cancellationToken);
                while (await r.ReadAsync(cancellationToken))
                {
                    var slpCode = r["SlpCode"] == DBNull.Value ? 0 : Convert.ToInt32(r["SlpCode"]);
                    var invCount = Convert.ToInt32(r["InvoicesCount"]);
                    var quotesCount2 = Convert.ToInt32(r["QuotesCount"]);
                    var converted = Convert.ToInt32(r["ConvertedQuotes"]);
                    response.CommercialSummaries.Add(new AdminCommercialSummaryDto
                    {
                        SalesPersonCode = slpCode,
                        SalesPersonName = namesDict.TryGetValue(slpCode, out var nm) ? nm : $"#{slpCode}",
                        Revenue = Convert.ToDecimal(r["InvoicesAmount"]),
                        QuotesCount = quotesCount2,
                        QuoteToOrderRate = quotesCount2 <= 0 ? 0 : Math.Round((decimal)converted * 100m / quotesCount2, 2),
                        CollectedRevenue = Convert.ToDecimal(r["CollectedAmount"]),
                        OverdueInvoicesCount = Convert.ToInt32(r["OverdueCnt"]),
                        OverdueInvoicesAmount = Convert.ToDecimal(r["OverdueAmt"])
                    });
                }
            }
        }

        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(120));
        return Ok(new ApiResponse<AdminDashboardDto>(true, null, response));
    }

    [HttpGet("reporting/advanced")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AdvancedReportingResponseDto>>> GetAdvancedReporting(
        [FromQuery] string periodType = "month",
        [FromQuery] string? month = null,
        [FromQuery] int? quarter = null,
        [FromQuery] int? year = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int? salesPersonCode = null,
        [FromQuery] string? itemCode = null,
        [FromQuery] string? cardCode = null,
        [FromQuery] int detailsLimit = 10,
        CancellationToken cancellationToken = default)
    {
        detailsLimit = Math.Clamp(detailsLimit, 10, 200);

        var (periodStart, periodEnd, periodLabel) = ResolveReportingPeriod(periodType, month, quarter, year, startDate, endDate);
        var previousStart = periodStart.AddMonths(-1);
        var previousEnd = periodStart;

        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPerson = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin ? salesPersonCode : currentSalesPerson;
        if (!isAdmin && scopedSalesPersonCode <= 0) return Forbid();

        var cacheKey = $"reporting:advanced:v2:{periodType}:{periodStart:yyyyMMdd}:{periodEnd:yyyyMMdd}:{scopedSalesPersonCode?.ToString() ?? "none"}:{(itemCode ?? string.Empty).Trim().ToLowerInvariant()}:{(cardCode ?? string.Empty).Trim().ToLowerInvariant()}:{detailsLimit}:{isAdmin}";
        if (_cache.TryGetValue(cacheKey, out AdvancedReportingResponseDto? cached) && cached is not null)
            return Ok(new ApiResponse<AdvancedReportingResponseDto>(true, null, cached));

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return StatusCode(500, SapError("Connexion SQL impossible."));

        await using (conn)
        {
            var response = new AdvancedReportingResponseDto
            {
                Mode = isAdmin ? "Admin" : "Commercial",
                PeriodType = periodType,
                PeriodLabel = periodLabel,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                SelectedSalesPersonCode = scopedSalesPersonCode > 0 ? scopedSalesPersonCode : null
            };

            response.TeamMembers = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Role == Roles.Commercial)
                .Select(u => new CommercialSalesPersonInfoDto
                {
                    SalesPersonCode = u.SapSalesPersonCode,
                SalesPersonName = u.FullName,
                Role = u.Role
            })
                .OrderBy(u => u.SalesPersonName)
                .ToListAsync(cancellationToken);

            if (!isAdmin)
            {
                response.TeamMembers = response.TeamMembers
                    .Where(x => x.SalesPersonCode == scopedSalesPersonCode)
                    .ToList();
            }

            response.SelectedSalesPersonName = response.TeamMembers
                .FirstOrDefault(x => x.SalesPersonCode == response.SelectedSalesPersonCode)?.SalesPersonName;

            var hasSelectedCommercial = !isAdmin || (scopedSalesPersonCode.HasValue && scopedSalesPersonCode.Value > 0);
            var hasSelectedPartner = !string.IsNullOrWhiteSpace(cardCode);
            if (isAdmin && !hasSelectedCommercial && !hasSelectedPartner)
            {
                _cache.Set(cacheKey, response, TimeSpan.FromSeconds(120));
                return Ok(new ApiResponse<AdvancedReportingResponseDto>(true, null, response));
            }

            if (!hasSelectedCommercial && hasSelectedPartner)
            {
                response.PartnerReport = await LoadPartnerFocusedReportAsync(conn, periodType, periodStart, periodEnd, null, cardCode, detailsLimit, cancellationToken);
                _cache.Set(cacheKey, response, TimeSpan.FromSeconds(60));
                return Ok(new ApiResponse<AdvancedReportingResponseDto>(true, null, response));
            }
            response.Kpis = await LoadReportingKpisAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, cancellationToken);
            response.PreviousKpis = await LoadReportingKpisAsync(conn, previousStart, previousEnd, scopedSalesPersonCode, cardCode, cancellationToken);
            response.MonthlyRevenue = await LoadMonthlyRevenueAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cancellationToken);
            response.MonthlyRevenuePreviousYear = await LoadMonthlyRevenueAsync(conn, periodStart.AddYears(-1), periodEnd.AddYears(-1), scopedSalesPersonCode, cancellationToken);
            response.TopProducts = await LoadTopProductsAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, itemCode, cardCode, detailsLimit, cancellationToken);
            response.TopClients = await LoadTopClientsAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, detailsLimit, cancellationToken);
            response.TopPartners = await LoadTopPartnersAsync(conn, periodStart, periodEnd, scopedSalesPersonCode, cardCode, detailsLimit, cancellationToken);
            response.UnpaidItems = await LoadUnpaidItemsAsync(conn, scopedSalesPersonCode, detailsLimit, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cardCode))
                response.PartnerReport = await LoadPartnerFocusedReportAsync(conn, periodType, periodStart, periodEnd, scopedSalesPersonCode, cardCode, detailsLimit, cancellationToken);
            // Les tableaux détaillés ont été retirés du frontend reporting:
            // on évite ces requêtes SQL coûteuses pour réduire le temps de chargement.
            response.ProductDetails = new List<ReportingProductDetailDto>();
            response.ClientDetails = new List<ReportingClientDetailDto>();
            response.PartnerDetails = new List<ReportingPartnerDetailDto>();
            response.TopCommercials = await LoadReportingTeamPerformanceAsync(conn, periodStart, periodEnd, isAdmin ? null : scopedSalesPersonCode, cardCode, cancellationToken);
            var commercialCodes = response.TeamMembers.Select(x => x.SalesPersonCode).ToHashSet();
            response.TopCommercials = response.TopCommercials
                .Where(x => commercialCodes.Contains(x.SalesPersonCode))
                .OrderByDescending(x => x.NetRevenue)
                .Take(10)
                .ToList();

            var namesByCode = response.TeamMembers.ToDictionary(x => x.SalesPersonCode, x => x.SalesPersonName);
            foreach (var row in response.TopCommercials)
            {
                if (namesByCode.TryGetValue(row.SalesPersonCode, out var fullName))
                    row.SalesPersonName = fullName;
            }
            foreach (var row in response.UnpaidItems)
            {
                if (namesByCode.TryGetValue(row.SalesPersonCode, out var fullName))
                    row.SalesPersonName = fullName;
            }

            if (!isAdmin)
            {
                response.TeamMembers = response.TeamMembers
                    .Where(x => x.SalesPersonCode == scopedSalesPersonCode)
                    .ToList();
            }

            response.SelectedSalesPersonName = response.TeamMembers
                .FirstOrDefault(x => x.SalesPersonCode == response.SelectedSalesPersonCode)?.SalesPersonName;

            _cache.Set(cacheKey, response, TimeSpan.FromSeconds(30));
            return Ok(new ApiResponse<AdvancedReportingResponseDto>(true, null, response));
        }
    }

    private async Task<ReportingPartnerFocusedReportDto?> LoadPartnerFocusedReportAsync(
        SqlConnection conn,
        string periodType,
        DateTime periodStart,
        DateTime periodEnd,
        int? salesPersonCode,
        string? cardCode,
        int limit,
        CancellationToken cancellationToken)
    {
        var selectedCardCode = (cardCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(selectedCardCode))
            return null;

        var report = new ReportingPartnerFocusedReportDto { CardCode = selectedCardCode };

        const string summarySql = @"
SELECT TOP 1
  C.CardCode,
  C.CardName,
  ISNULL(C.SlpCode, 0) AS SalesPersonCode,
  ISNULL(S.SlpName, '') AS SalesPersonName,
  ISNULL(Inv.Debit, 0) AS Debit,
  ISNULL(Inv.Paid, 0) + ISNULL(Cn.CreditNotes, 0) AS Credit,
  ISNULL(Inv.Debit, 0) - (ISNULL(Inv.Paid, 0) + ISNULL(Cn.CreditNotes, 0)) AS Balance
FROM OCRD C
LEFT JOIN OSLP S ON S.SlpCode = C.SlpCode
OUTER APPLY (
  SELECT ISNULL(SUM(ISNULL(I.DocTotal, 0)), 0) AS Debit,
         ISNULL(SUM(ISNULL(I.PaidToDate, 0)), 0) AS Paid
  FROM OINV I
  WHERE I.CardCode = C.CardCode
    AND ISNULL(I.CANCELED, 'N') <> 'Y'
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
) Inv
OUTER APPLY (
  SELECT ISNULL(SUM(ISNULL(N.DocTotal, 0)), 0) AS CreditNotes
  FROM ORIN N
  WHERE N.CardCode = C.CardCode
    AND ISNULL(N.CANCELED, 'N') <> 'Y'
    AND (@applyScope = 0 OR N.SlpCode = @salesPersonCode)
) Cn
WHERE C.CardCode = @cardCode
  AND C.CardType = 'C'
  AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode);";

        await using (var cmd = new SqlCommand(summarySql, conn))
        {
            AddReportingPeriodParameters(cmd, periodStart, periodEnd);
            AddReportingScopeParameters(cmd, salesPersonCode);
            cmd.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = selectedCardCode });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            report.CardCode = reader["CardCode"]?.ToString() ?? selectedCardCode;
            report.CardName = reader["CardName"]?.ToString() ?? string.Empty;
            report.SalesPersonCode = Convert.ToInt32(reader["SalesPersonCode"]);
            report.SalesPersonName = reader["SalesPersonName"]?.ToString() ?? string.Empty;
            report.FinancialSummary = new ReportingPartnerFinancialSummaryDto
            {
                Debit = Convert.ToDecimal(reader["Debit"]),
                Credit = Convert.ToDecimal(reader["Credit"]),
                Balance = Convert.ToDecimal(reader["Balance"])
            };
        }

        var topLimit = Math.Clamp(limit, 10, 200);
        var documentsSql = $@"
SELECT TOP ({topLimit}) * FROM (
  SELECT 'Devis' AS Type, DocEntry, DocNum, DocDate, ISNULL(DocTotal,0) AS Total, ISNULL(DocStatus,'') AS RawStatus
  FROM OQUT WHERE CardCode = @cardCode AND DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  UNION ALL
  SELECT 'Commande', DocEntry, DocNum, DocDate, ISNULL(DocTotal,0), ISNULL(DocStatus,'')
  FROM ORDR WHERE CardCode = @cardCode AND DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  UNION ALL
  SELECT 'Bon de livraison', DocEntry, DocNum, DocDate, ISNULL(DocTotal,0), ISNULL(DocStatus,'')
  FROM ODLN WHERE CardCode = @cardCode AND DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  UNION ALL
  SELECT 'Facture', DocEntry, DocNum, DocDate, ISNULL(DocTotal,0), ISNULL(DocStatus,'')
  FROM OINV WHERE CardCode = @cardCode AND DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
) D
ORDER BY DocDate DESC, DocNum DESC;";

        await using (var cmd = new SqlCommand(documentsSql, conn))
        {
            AddReportingPeriodParameters(cmd, periodStart, periodEnd);
            AddReportingScopeParameters(cmd, salesPersonCode);
            cmd.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = selectedCardCode });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var rawStatus = reader["RawStatus"]?.ToString() ?? string.Empty;
                report.Documents.Add(new ReportingPartnerDocumentDto
                {
                    Type = reader["Type"]?.ToString() ?? string.Empty,
                    DocEntry = Convert.ToInt32(reader["DocEntry"]),
                    DocNum = Convert.ToInt32(reader["DocNum"]),
                    DocDate = reader["DocDate"] is DateTime d ? d : null,
                    Total = Convert.ToDecimal(reader["Total"]),
                    Status = rawStatus == "O" ? "Ouvert" : rawStatus == "C" ? "Cloture" : rawStatus
                });
            }
        }

        var productsSql = $@"
SELECT TOP ({topLimit})
  L.ItemCode,
  MAX(ISNULL(L.Dscription, '')) AS ItemName,
  SUM(ISNULL(L.Quantity, 0)) AS QuantitySold,
  SUM(ISNULL(L.LineTotal, 0)) AS Revenue,
  COUNT(1) AS SalesCount,
  1 AS ClientsCount,
  COUNT(DISTINCT I.SlpCode) AS SalesPeopleCount,
  MAX(I.CardName) AS MainClientName
FROM OINV I
INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
WHERE I.CardCode = @cardCode
  AND I.DocDate >= @dateFrom AND I.DocDate < @dateTo
  AND ISNULL(I.CANCELED, 'N') <> 'Y'
  AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
GROUP BY L.ItemCode
ORDER BY SUM(ISNULL(L.LineTotal, 0)) DESC, SUM(ISNULL(L.Quantity, 0)) DESC;";

        await using (var cmd = new SqlCommand(productsSql, conn))
        {
            AddReportingPeriodParameters(cmd, periodStart, periodEnd);
            AddReportingScopeParameters(cmd, salesPersonCode);
            cmd.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = selectedCardCode });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                report.TopPurchasedProducts.Add(new ReportingTopProductDto
                {
                    ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                    ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                    QuantitySold = Convert.ToDecimal(reader["QuantitySold"]),
                    Revenue = Convert.ToDecimal(reader["Revenue"]),
                    SalesCount = Convert.ToInt32(reader["SalesCount"]),
                    ClientsCount = Convert.ToInt32(reader["ClientsCount"]),
                    SalesPeopleCount = Convert.ToInt32(reader["SalesPeopleCount"]),
                    MainClientName = reader["MainClientName"]?.ToString() ?? string.Empty
                });
            }
        }

        const string categorySql = @"
SELECT
  CONVERT(nvarchar(50), ISNULL(G.ItmsGrpCod, 0)) AS CategoryCode,
  ISNULL(G.ItmsGrpNam, 'Sans categorie') AS CategoryName,
  SUM(ISNULL(L.Quantity, 0)) AS QuantitySold,
  SUM(ISNULL(L.LineTotal, 0)) AS Revenue
FROM OINV I
INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
LEFT JOIN OITM M ON M.ItemCode = L.ItemCode
LEFT JOIN OITB G ON G.ItmsGrpCod = M.ItmsGrpCod
WHERE I.CardCode = @cardCode
  AND I.DocDate >= @dateFrom AND I.DocDate < @dateTo
  AND ISNULL(I.CANCELED, 'N') <> 'Y'
  AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
GROUP BY ISNULL(G.ItmsGrpCod, 0), ISNULL(G.ItmsGrpNam, 'Sans categorie')
ORDER BY SUM(ISNULL(L.LineTotal, 0)) DESC;";

        await using (var cmd = new SqlCommand(categorySql, conn))
        {
            AddReportingPeriodParameters(cmd, periodStart, periodEnd);
            AddReportingScopeParameters(cmd, salesPersonCode);
            cmd.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = selectedCardCode });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                report.CategoryShares.Add(new ReportingCategoryShareDto
                {
                    CategoryCode = reader["CategoryCode"]?.ToString() ?? string.Empty,
                    CategoryName = reader["CategoryName"]?.ToString() ?? string.Empty,
                    QuantitySold = Convert.ToDecimal(reader["QuantitySold"]),
                    Revenue = Convert.ToDecimal(reader["Revenue"])
                });
            }
        }

        var normalizedPeriodType = (periodType ?? "month").Trim().ToLowerInvariant();
        var periodDays = Math.Max(1, (periodEnd.Date - periodStart.Date).Days);
        var groupByDay = normalizedPeriodType == "week" || normalizedPeriodType == "month" || (normalizedPeriodType == "custom" && periodDays <= 31);
        var chartStart = groupByDay ? periodStart.Date : new DateTime(periodStart.Year, periodStart.Month, 1);
        var chartEnd = groupByDay
            ? periodEnd.Date
            : new DateTime(periodEnd.AddDays(-1).Year, periodEnd.AddDays(-1).Month, 1).AddMonths(1);
        var chartKeyExpression = groupByDay ? "CONVERT(char(10), DocDate, 120)" : "CONVERT(char(7), DocDate, 120)";
        var yearlySql = $@"
SELECT {chartKeyExpression} AS MonthKey, ISNULL(SUM(ISNULL(DocTotal, 0)), 0) AS Revenue
FROM OINV
WHERE CardCode = @cardCode
  AND DocDate >= @chartFrom AND DocDate < @chartTo
  AND ISNULL(CANCELED, 'N') <> 'Y'
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
GROUP BY {chartKeyExpression};";

        var yearly = new Dictionary<string, decimal>();
        await using (var cmd = new SqlCommand(yearlySql, conn))
        {
            AddReportingScopeParameters(cmd, salesPersonCode);
            cmd.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = selectedCardCode });
            cmd.Parameters.Add(new SqlParameter("@chartFrom", SqlDbType.DateTime) { Value = chartStart });
            cmd.Parameters.Add(new SqlParameter("@chartTo", SqlDbType.DateTime) { Value = chartEnd });
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yearly[reader["MonthKey"]?.ToString() ?? string.Empty] = Convert.ToDecimal(reader["Revenue"]);
            }
        }

        for (var cursor = chartStart; cursor < chartEnd; cursor = groupByDay ? cursor.AddDays(1) : cursor.AddMonths(1))
        {
            var key = cursor.ToString(groupByDay ? "yyyy-MM-dd" : "yyyy-MM", CultureInfo.InvariantCulture);
            report.YearlyRevenue.Add(new ReportingMonthlyRevenuePointDto
            {
                MonthKey = key,
                Revenue = yearly.TryGetValue(key, out var amount) ? amount : 0m
            });
        }

        return report;
    }
    private static (DateTime Start, DateTime End, string Label) ResolveReportingPeriod(
        string periodType,
        string? month,
        int? quarter,
        int? year,
        DateTime? startDate,
        DateTime? endDate)
    {
        var now = DateTime.Today;
        var normalized = (periodType ?? "month").Trim().ToLowerInvariant();

        if (normalized == "custom" && startDate.HasValue && endDate.HasValue)
        {
            var s = startDate.Value.Date;
            var e = endDate.Value.Date.AddDays(1);
            if (e <= s) e = s.AddDays(1);
            return (s, e, $"{s:dd/MM/yyyy} - {e.AddDays(-1):dd/MM/yyyy}");
        }

        if (normalized == "week")
        {
            var s = startDate?.Date ?? now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            var e = endDate?.Date.AddDays(1) ?? s.AddDays(7);
            if (e <= s) e = s.AddDays(7);
            return (s, e, $"Semaine du {s:dd/MM/yyyy} au {e.AddDays(-1):dd/MM/yyyy}");
        }

        if (normalized == "year")
        {
            var y = year.GetValueOrDefault(now.Year);
            var s = new DateTime(y, 1, 1);
            return (s, s.AddYears(1), $"Annee {y}");
        }

        if (normalized == "quarter")
        {
            var y = year.GetValueOrDefault(now.Year);
            var q = Math.Min(4, Math.Max(1, quarter.GetValueOrDefault(((now.Month - 1) / 3) + 1)));
            var m = ((q - 1) * 3) + 1;
            var s = new DateTime(y, m, 1);
            return (s, s.AddMonths(3), $"T{q} {y}");
        }

        if (!string.IsNullOrWhiteSpace(month) &&
            DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedMonth))
        {
            var s = new DateTime(parsedMonth.Year, parsedMonth.Month, 1);
            return (s, s.AddMonths(1), s.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")));
        }

        var start = new DateTime(now.Year, now.Month, 1);
        return (start, start.AddMonths(1), start.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")));
    }

    private async Task<List<ReportingMonthlyRevenuePointDto>> LoadMonthlyRevenueAsync(
        SqlConnection conn,
        DateTime start,
        DateTime end,
        int? salesPersonCode,
        CancellationToken cancellationToken)
    {
        var groupByDay = (end.Date - start.Date).TotalDays <= 31;
        var keyExpression = groupByDay ? "CONVERT(char(10), DocDate, 120)" : "CONVERT(char(7), DocDate, 120)";
        var sql = $@"
SELECT {keyExpression} AS MonthKey,
       ISNULL(SUM(ISNULL(DocTotal,0)),0) AS Revenue
FROM OINV
WHERE DocDate >= @dateFrom AND DocDate < @dateTo
  AND ISNULL(CANCELED,'N') <> 'Y'
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
GROUP BY {keyExpression}
ORDER BY MonthKey;";

        var result = new List<ReportingMonthlyRevenuePointDto>();
        var byKey = new Dictionary<string, decimal>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            byKey[reader["MonthKey"]?.ToString() ?? string.Empty] = Convert.ToDecimal(reader["Revenue"]);
        }

        var cursor = groupByDay ? start.Date : new DateTime(start.Year, start.Month, 1);
        var stop = groupByDay ? end.Date : new DateTime(end.AddDays(-1).Year, end.AddDays(-1).Month, 1).AddMonths(1);
        for (; cursor < stop; cursor = groupByDay ? cursor.AddDays(1) : cursor.AddMonths(1))
        {
            var key = cursor.ToString(groupByDay ? "yyyy-MM-dd" : "yyyy-MM", CultureInfo.InvariantCulture);
            result.Add(new ReportingMonthlyRevenuePointDto
            {
                MonthKey = key,
                Revenue = byKey.TryGetValue(key, out var amount) ? amount : 0m
            });
        }

        return result;
    }

    private async Task<List<ReportingTopProductDto>> LoadTopProductsAsync(
        SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? itemCode, string? cardCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
WITH Base AS (
  SELECT L.ItemCode, ISNULL(L.Dscription,'') AS ItemName, ISNULL(L.Quantity,0) AS Qty, ISNULL(L.LineTotal,0) AS Revenue, I.CardCode, I.SlpCode
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
    AND (@itemCode = '' OR L.ItemCode = @itemCode)
), Agg AS (
  SELECT ItemCode, MAX(ItemName) AS ItemName,
         SUM(Qty) AS QuantitySold,
         SUM(Revenue) AS Revenue,
         COUNT(1) AS SalesCount,
         COUNT(DISTINCT CardCode) AS ClientsCount,
         COUNT(DISTINCT SlpCode) AS SalesPeopleCount
  FROM Base
  GROUP BY ItemCode
)
SELECT TOP ({limit})
  A.ItemCode, A.ItemName, A.QuantitySold, A.Revenue, A.SalesCount, A.ClientsCount, A.SalesPeopleCount,
  ISNULL(MC.CardName, '') AS MainClientName
FROM Agg A
OUTER APPLY (
  SELECT TOP 1 B.CardCode, C.CardName, SUM(B.Revenue) AS ClientRevenue
  FROM Base B
  LEFT JOIN OCRD C ON C.CardCode = B.CardCode
  WHERE B.ItemCode = A.ItemCode
  GROUP BY B.CardCode, C.CardName
  ORDER BY SUM(B.Revenue) DESC
) MC
ORDER BY A.Revenue DESC, A.QuantitySold DESC;";

        var result = new List<ReportingTopProductDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        cmd.Parameters.Add(new SqlParameter("@itemCode", SqlDbType.NVarChar, 100) { Value = (itemCode ?? string.Empty).Trim() });
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReportingTopProductDto
            {
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                QuantitySold = Convert.ToDecimal(reader["QuantitySold"]),
                Revenue = Convert.ToDecimal(reader["Revenue"]),
                SalesCount = Convert.ToInt32(reader["SalesCount"]),
                ClientsCount = Convert.ToInt32(reader["ClientsCount"]),
                SalesPeopleCount = Convert.ToInt32(reader["SalesPeopleCount"]),
                MainClientName = reader["MainClientName"]?.ToString() ?? string.Empty
            });
        }
        return result;
    }

    private async Task<List<ReportingTopClientDto>> LoadTopClientsAsync(
        SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? cardCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
WITH Base AS (
  SELECT I.CardCode, I.CardName, I.SlpCode, ISNULL(I.DocTotal,0) AS Total, ISNULL(I.PaidToDate,0) AS Paid
  FROM OINV I
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
    AND (@cardCode = '' OR I.CardCode = @cardCode)
), L AS (
  SELECT I.CardCode, COUNT(DISTINCT L.ItemCode) AS ProductsCount
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
    AND (@cardCode = '' OR I.CardCode = @cardCode)
  GROUP BY I.CardCode
)
SELECT TOP ({limit})
  B.CardCode,
  MAX(B.CardName) AS CardName,
  SUM(B.Total) AS Revenue,
  SUM(B.Paid) AS PaidAmount,
  SUM(B.Total - B.Paid) AS PendingAmount,
  COUNT(1) AS ContractsCount,
  ISNULL(MAX(L.ProductsCount), 0) AS ProductsCount,
  ISNULL(MAX(S.SlpName), '') AS MainSalesPersonName
FROM Base B
LEFT JOIN OSLP S ON S.SlpCode = B.SlpCode
LEFT JOIN L ON L.CardCode = B.CardCode
GROUP BY B.CardCode
ORDER BY SUM(B.Total) DESC;";

        var result = new List<ReportingTopClientDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReportingTopClientDto
            {
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                Revenue = Convert.ToDecimal(reader["Revenue"]),
                PaidAmount = Convert.ToDecimal(reader["PaidAmount"]),
                PendingAmount = Convert.ToDecimal(reader["PendingAmount"]),
                ContractsCount = Convert.ToInt32(reader["ContractsCount"]),
                ProductsCount = Convert.ToInt32(reader["ProductsCount"]),
                MainSalesPersonName = reader["MainSalesPersonName"]?.ToString() ?? string.Empty
            });
        }
        return result;
    }

    private async Task<List<ReportingTopPartnerDto>> LoadTopPartnersAsync(
        SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? cardCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
WITH Q AS (
  SELECT CardCode, COUNT(1) AS QuotesCount
  FROM OQUT
  WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY CardCode
), P AS (
  SELECT I.CardCode, COUNT(DISTINCT L.ItemCode) AS ProductsCount, COUNT(DISTINCT I.SlpCode) AS SalesPeopleCount, SUM(ISNULL(L.LineTotal,0)) AS Revenue
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
  GROUP BY I.CardCode
)
SELECT TOP ({limit})
  C.CardCode AS PartnerCode,
  C.CardName AS PartnerName,
  ISNULL(P.Revenue,0) AS Revenue,
  ISNULL(Q.QuotesCount,0) AS QuotesCount,
  ISNULL(P.ProductsCount,0) AS ProductsCount,
  ISNULL(P.SalesPeopleCount,0) AS SalesPeopleCount
FROM OCRD C
LEFT JOIN P ON P.CardCode = C.CardCode
LEFT JOIN Q ON Q.CardCode = C.CardCode
WHERE C.CardType = 'C'
  AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode)
ORDER BY ISNULL(P.Revenue,0) DESC, ISNULL(Q.QuotesCount,0) DESC;";

        var result = new List<ReportingTopPartnerDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReportingTopPartnerDto
            {
                PartnerCode = reader["PartnerCode"]?.ToString() ?? string.Empty,
                PartnerName = reader["PartnerName"]?.ToString() ?? string.Empty,
                Revenue = Convert.ToDecimal(reader["Revenue"]),
                QuotesCount = Convert.ToInt32(reader["QuotesCount"]),
                ProductsCount = Convert.ToInt32(reader["ProductsCount"]),
                SalesPeopleCount = Convert.ToInt32(reader["SalesPeopleCount"])
            });
        }
        return result;
    }

    private async Task<List<ReportingUnpaidDto>> LoadUnpaidItemsAsync(
        SqlConnection conn, int? salesPersonCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT TOP ({limit})
  I.CardCode, I.CardName, ISNULL(L.ItemCode,'') AS ItemCode, ISNULL(L.Dscription,'') AS ItemName,
  (ISNULL(I.DocTotal,0) - ISNULL(I.PaidToDate,0)) AS DueAmount,
  ISNULL(I.SlpCode,0) AS SalesPersonCode,
  ISNULL(I.DocDueDate, I.DocDate) AS DueDate
FROM OINV I
LEFT JOIN INV1 L ON L.DocEntry = I.DocEntry AND L.LineNum = 0
WHERE ISNULL(I.CANCELED,'N') <> 'Y'
  AND (ISNULL(I.DocTotal,0) - ISNULL(I.PaidToDate,0)) > 0.0001
  AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
ORDER BY ISNULL(I.DocDate, ISNULL(I.DocDueDate, GETDATE())) DESC, DueAmount DESC;";

        var result = new List<ReportingUnpaidDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingScopeParameters(cmd, salesPersonCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dueDate = reader["DueDate"] is DateTime dd ? dd.Date : DateTime.Today;
            result.Add(new ReportingUnpaidDto
            {
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                DueAmount = Convert.ToDecimal(reader["DueAmount"]),
                SalesPersonCode = Convert.ToInt32(reader["SalesPersonCode"]),
                DueDate = dueDate,
                OverdueDays = (DateTime.Today - dueDate).Days
            });
        }
        return result;
    }

    private async Task<List<ReportingProductDetailDto>> LoadProductDetailsAsync(
        SqlConnection conn, DateTime start, DateTime end, DateTime previousStart, DateTime previousEnd, int? salesPersonCode, string? itemCode, string? cardCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
WITH Cur AS (
  SELECT L.ItemCode, MAX(ISNULL(L.Dscription,'')) AS ItemName, SUM(ISNULL(L.Quantity,0)) AS Qty, SUM(ISNULL(L.LineTotal,0)) AS Revenue,
         COUNT(DISTINCT I.CardCode) AS ClientsCount
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
    AND (@itemCode = '' OR L.ItemCode = @itemCode)
  GROUP BY L.ItemCode
), Prev AS (
  SELECT L.ItemCode, SUM(ISNULL(L.LineTotal,0)) AS Revenue
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @prevFrom AND I.DocDate < @prevTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
    AND (@itemCode = '' OR L.ItemCode = @itemCode)
  GROUP BY L.ItemCode
)
SELECT TOP ({limit}) C.ItemCode, C.ItemName, C.Qty, C.Revenue, C.ClientsCount,
       ISNULL(TopClient.CardName,'') AS MainClientName,
       CASE WHEN ISNULL(P.Revenue,0) <= 0 THEN 100
            ELSE ((C.Revenue - P.Revenue) * 100.0 / NULLIF(P.Revenue,0)) END AS TrendPercent
FROM Cur C
LEFT JOIN Prev P ON P.ItemCode = C.ItemCode
OUTER APPLY (
  SELECT TOP 1 I.CardName, SUM(ISNULL(L.LineTotal,0)) AS Amount
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND L.ItemCode = C.ItemCode
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
  GROUP BY I.CardName
  ORDER BY SUM(ISNULL(L.LineTotal,0)) DESC
) TopClient
ORDER BY C.Revenue DESC;";

        var result = new List<ReportingProductDetailDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        cmd.Parameters.Add(new SqlParameter("@prevFrom", SqlDbType.DateTime) { Value = previousStart.Date });
        cmd.Parameters.Add(new SqlParameter("@prevTo", SqlDbType.DateTime) { Value = previousEnd.Date });
        cmd.Parameters.Add(new SqlParameter("@itemCode", SqlDbType.NVarChar, 100) { Value = (itemCode ?? string.Empty).Trim() });
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReportingProductDetailDto
            {
                ItemCode = reader["ItemCode"]?.ToString() ?? string.Empty,
                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                QuantitySold = Convert.ToDecimal(reader["Qty"]),
                Revenue = Convert.ToDecimal(reader["Revenue"]),
                ClientsCount = Convert.ToInt32(reader["ClientsCount"]),
                MainClientName = reader["MainClientName"]?.ToString() ?? string.Empty,
                TrendPercent = Convert.ToDecimal(reader["TrendPercent"])
            });
        }
        return result;
    }

    private async Task<List<ReportingClientDetailDto>> LoadClientDetailsAsync(
        SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? cardCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
WITH Base AS (
  SELECT I.CardCode, I.CardName, ISNULL(I.DocTotal,0) AS Total, ISNULL(I.PaidToDate,0) AS Paid
  FROM OINV I
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND (@applyCard = 0 OR I.CardCode = @cardCode)
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
    AND (@cardCode = '' OR I.CardCode = @cardCode)
)
SELECT TOP ({limit})
  B.CardCode, MAX(B.CardName) AS CardName,
  SUM(B.Total) AS Revenue,
  SUM(B.Paid) AS PaidAmount,
  SUM(B.Total - B.Paid) AS PendingAmount,
  COUNT(1) AS ContractsCount,
  ISNULL(MAX(P.ProductsCount),0) AS ProductsCount,
  ISNULL(MAX(F.ItemName), '') AS FavoriteItemName
FROM Base B
OUTER APPLY (
  SELECT COUNT(DISTINCT L.ItemCode) AS ProductsCount
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND I.CardCode = B.CardCode
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
) P
OUTER APPLY (
  SELECT TOP 1 ISNULL(L.Dscription, L.ItemCode) AS ItemName, SUM(ISNULL(L.LineTotal,0)) AS Revenue
  FROM OINV I
  INNER JOIN INV1 L ON L.DocEntry = I.DocEntry
  WHERE I.DocDate >= @dateFrom AND I.DocDate < @dateTo
    AND ISNULL(I.CANCELED,'N') <> 'Y'
    AND I.CardCode = B.CardCode
    AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
  GROUP BY ISNULL(L.Dscription, L.ItemCode)
  ORDER BY SUM(ISNULL(L.LineTotal,0)) DESC
) F
GROUP BY B.CardCode
ORDER BY SUM(B.Total) DESC;";

        var result = new List<ReportingClientDetailDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var revenue = Convert.ToDecimal(reader["Revenue"]);
            var paid = Convert.ToDecimal(reader["PaidAmount"]);
            result.Add(new ReportingClientDetailDto
            {
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                Revenue = revenue,
                PaidAmount = paid,
                PendingAmount = Convert.ToDecimal(reader["PendingAmount"]),
                PaymentRate = revenue <= 0 ? 0 : Math.Round((paid * 100m) / revenue, 2),
                ContractsCount = Convert.ToInt32(reader["ContractsCount"]),
                ProductsCount = Convert.ToInt32(reader["ProductsCount"]),
                FavoriteItemName = reader["FavoriteItemName"]?.ToString() ?? string.Empty
            });
        }
        return result;
    }

    private async Task<List<ReportingPartnerDetailDto>> LoadPartnerDetailsAsync(
        SqlConnection conn, DateTime start, DateTime end, DateTime previousStart, DateTime previousEnd, int? salesPersonCode, int limit, CancellationToken cancellationToken)
    {
        var sql = $@"
WITH Cur AS (
  SELECT C.CardCode, C.CardName,
         ISNULL(SUM(I.DocTotal),0) AS Revenue,
         (SELECT COUNT(1) FROM OQUT Q WHERE Q.CardCode = C.CardCode AND Q.DocDate >= @dateFrom AND Q.DocDate < @dateTo AND (@applyScope = 0 OR Q.SlpCode = @salesPersonCode)) AS QuotesCount,
         (SELECT COUNT(DISTINCT L.ItemCode) FROM OINV I2 INNER JOIN INV1 L ON L.DocEntry = I2.DocEntry WHERE I2.CardCode = C.CardCode AND I2.DocDate >= @dateFrom AND I2.DocDate < @dateTo AND ISNULL(I2.CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR I2.SlpCode = @salesPersonCode)) AS ProductsCount,
         (SELECT COUNT(DISTINCT I3.SlpCode) FROM OINV I3 WHERE I3.CardCode = C.CardCode AND I3.DocDate >= @dateFrom AND I3.DocDate < @dateTo AND ISNULL(I3.CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR I3.SlpCode = @salesPersonCode)) AS SalesPeopleCount
  FROM OCRD C
  LEFT JOIN OINV I ON I.CardCode = C.CardCode AND I.DocDate >= @dateFrom AND I.DocDate < @dateTo AND ISNULL(I.CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
  WHERE C.CardType = 'C'
    AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode)
  GROUP BY C.CardCode, C.CardName
), Prev AS (
  SELECT C.CardCode, ISNULL(SUM(I.DocTotal),0) AS Revenue
  FROM OCRD C
  LEFT JOIN OINV I ON I.CardCode = C.CardCode AND I.DocDate >= @prevFrom AND I.DocDate < @prevTo AND ISNULL(I.CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)
  WHERE C.CardType = 'C'
    AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode)
  GROUP BY C.CardCode
)
SELECT TOP ({limit})
  C.CardCode, C.CardName, C.Revenue, C.QuotesCount, C.ProductsCount, C.SalesPeopleCount,
  CASE WHEN ISNULL(P.Revenue,0) <= 0 THEN 100 ELSE ((C.Revenue - P.Revenue) * 100.0 / NULLIF(P.Revenue,0)) END AS TrendPercent,
  CASE WHEN C.QuotesCount <= 0 THEN 0 ELSE (C.Revenue * 100.0 / NULLIF(C.QuotesCount,0)) END AS RoiPercent
FROM Cur C
LEFT JOIN Prev P ON P.CardCode = C.CardCode
ORDER BY C.Revenue DESC;";

        var result = new List<ReportingPartnerDetailDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        cmd.Parameters.Add(new SqlParameter("@prevFrom", SqlDbType.DateTime) { Value = previousStart.Date });
        cmd.Parameters.Add(new SqlParameter("@prevTo", SqlDbType.DateTime) { Value = previousEnd.Date });
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ReportingPartnerDetailDto
            {
                PartnerCode = reader["CardCode"]?.ToString() ?? string.Empty,
                PartnerName = reader["CardName"]?.ToString() ?? string.Empty,
                Revenue = Convert.ToDecimal(reader["Revenue"]),
                QuotesCount = Convert.ToInt32(reader["QuotesCount"]),
                ProductsCount = Convert.ToInt32(reader["ProductsCount"]),
                SalesPeopleCount = Convert.ToInt32(reader["SalesPeopleCount"]),
                TrendPercent = Convert.ToDecimal(reader["TrendPercent"]),
                RoiPercent = Convert.ToDecimal(reader["RoiPercent"])
            });
        }
        return result;
    }

    private static void AddReportingScopeParameters(SqlCommand command, int? salesPersonCode)
    {
        command.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = salesPersonCode ?? 0 });
        command.Parameters.Add(new SqlParameter("@applyScope", SqlDbType.Bit) { Value = salesPersonCode.HasValue && salesPersonCode.Value > 0 });
    }

    private static void AddReportingPartnerParameters(SqlCommand command, string? cardCode)
    {
        var selectedCardCode = (cardCode ?? string.Empty).Trim();
        command.Parameters.Add(new SqlParameter("@cardCode", SqlDbType.NVarChar, 50) { Value = selectedCardCode });
        command.Parameters.Add(new SqlParameter("@applyCard", SqlDbType.Bit) { Value = !string.IsNullOrWhiteSpace(selectedCardCode) });
    }

    private static string ApplyReportingCardCodeFilter(string sql)
    {
        return sql
            .Replace("FROM OQUT WHERE", "FROM OQUT WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ORDR WHERE", "FROM ORDR WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ODLN WHERE", "FROM ODLN WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM OINV WHERE", "FROM OINV WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ORIN WHERE", "FROM ORIN WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ORDN WHERE", "FROM ORDN WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM OQUT\r\n  WHERE", "FROM OQUT\r\n  WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ORDR\r\n  WHERE", "FROM ORDR\r\n  WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ODLN\r\n  WHERE", "FROM ODLN\r\n  WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM OINV\r\n  WHERE", "FROM OINV\r\n  WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("FROM ORIN\r\n  WHERE", "FROM ORIN\r\n  WHERE (@applyCard = 0 OR CardCode = @cardCode) AND")
            .Replace("WHERE Q.DocDate", "WHERE (@applyCard = 0 OR Q.CardCode = @cardCode) AND Q.DocDate")
            .Replace("WHERE O.DocDate", "WHERE (@applyCard = 0 OR O.CardCode = @cardCode) AND O.DocDate")
            .Replace("WHERE D.DocDate", "WHERE (@applyCard = 0 OR D.CardCode = @cardCode) AND D.DocDate")
            .Replace("WHERE I.DocDate", "WHERE (@applyCard = 0 OR I.CardCode = @cardCode) AND I.DocDate")
            .Replace("WHERE P.DocDate", "WHERE (@applyCard = 0 OR I.CardCode = @cardCode) AND P.DocDate")
            .Replace("WHERE C.CardType = 'C'", "WHERE C.CardType = 'C' AND (@applyCard = 0 OR C.CardCode = @cardCode)");
    }

    private static void AddReportingPeriodParameters(SqlCommand command, DateTime start, DateTime end)
    {
        command.Parameters.Add(new SqlParameter("@dateFrom", SqlDbType.DateTime) { Value = start.Date });
        command.Parameters.Add(new SqlParameter("@dateTo", SqlDbType.DateTime) { Value = end.Date });
    }

    private async Task<CommercialReportingKpiDto> LoadReportingKpisAsync(SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? cardCode, CancellationToken cancellationToken, bool lightweight = false)
    {
        var result = new CommercialReportingKpiDto();

        async Task<T> ExecAsync<T>(string label, string sql, Func<SqlDataReader, T> map)
        {
            try
            {
                await using var cmd2 = new SqlCommand(ApplyReportingCardCodeFilter(sql), conn);
                AddReportingPeriodParameters(cmd2, start, end);
                AddReportingScopeParameters(cmd2, salesPersonCode);
                AddReportingPartnerParameters(cmd2, cardCode);
                await using var r2 = await cmd2.ExecuteReaderAsync(cancellationToken);
                if (await r2.ReadAsync(cancellationToken)) return map(r2);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reporting KPI sub-query failed: {Label}", label);
            }
            return default!;
        }

        result.QuotesCount = await ExecAsync("QuotesCount",
            "SELECT COUNT(1) FROM OQUT WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.QuotesAmount = await ExecAsync("QuotesAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM OQUT WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        result.OrdersCount = await ExecAsync("OrdersCount",
            "SELECT COUNT(1) FROM ORDR WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.OrdersAmount = await ExecAsync("OrdersAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM ORDR WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        result.DeliveryNotesCount = await ExecAsync("DeliveryNotesCount",
            "SELECT COUNT(1) FROM ODLN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.DeliveryNotesAmount = await ExecAsync("DeliveryNotesAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM ODLN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        result.InvoicesCount = await ExecAsync("InvoicesCount",
            "SELECT COUNT(1) FROM OINV WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.InvoicesAmount = await ExecAsync("InvoicesAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM OINV WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        result.CreditNotesCount = await ExecAsync("CreditNotesCount",
            "SELECT COUNT(1) FROM ORIN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.CreditNotesAmount = await ExecAsync("CreditNotesAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM ORIN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        result.ReturnsCount = await ExecAsync("ReturnsCount",
            "SELECT COUNT(1) FROM ORDN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.ReturnsAmount = await ExecAsync("ReturnsAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM ORDN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        var pendingRevenueOrders = await ExecAsync("PendingRevenueOrders",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM ORDR WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND ISNULL(DocStatus,'O') = 'O' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        var pendingRevenueDeliveries = await ExecAsync("PendingRevenueDeliveries",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM ODLN WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND ISNULL(DocStatus,'O') = 'O' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));
        result.PendingRevenue = pendingRevenueOrders + pendingRevenueDeliveries;

        if (!lightweight)
        {
            result.ActivePartnersCount = await ExecAsync("ActivePartnersCount",
            @"SELECT COUNT(1) FROM OCRD C
              WHERE C.CardType = 'C' AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode)
                AND (
                  EXISTS (SELECT 1 FROM OQUT Q WHERE Q.CardCode = C.CardCode AND Q.DocDate >= @dateFrom AND Q.DocDate < @dateTo AND ISNULL(Q.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR Q.SlpCode = @salesPersonCode))
                  OR EXISTS (SELECT 1 FROM ORDR O WHERE O.CardCode = C.CardCode AND O.DocDate >= @dateFrom AND O.DocDate < @dateTo AND ISNULL(O.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR O.SlpCode = @salesPersonCode))
                  OR EXISTS (SELECT 1 FROM ODLN D WHERE D.CardCode = C.CardCode AND D.DocDate >= @dateFrom AND D.DocDate < @dateTo AND ISNULL(D.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR D.SlpCode = @salesPersonCode))
                  OR EXISTS (SELECT 1 FROM OINV I WHERE I.CardCode = C.CardCode AND I.DocDate >= @dateFrom AND I.DocDate < @dateTo AND ISNULL(I.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode))
                )",
            r => Convert.ToInt32(r[0]));
        result.InactivePartnersCount = await ExecAsync("InactivePartnersCount",
            @"SELECT COUNT(1) FROM OCRD C
              WHERE C.CardType = 'C' AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode)
                AND NOT (
                  EXISTS (SELECT 1 FROM OQUT Q WHERE Q.CardCode = C.CardCode AND Q.DocDate >= @dateFrom AND Q.DocDate < @dateTo AND ISNULL(Q.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR Q.SlpCode = @salesPersonCode))
                  OR EXISTS (SELECT 1 FROM ORDR O WHERE O.CardCode = C.CardCode AND O.DocDate >= @dateFrom AND O.DocDate < @dateTo AND ISNULL(O.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR O.SlpCode = @salesPersonCode))
                  OR EXISTS (SELECT 1 FROM ODLN D WHERE D.CardCode = C.CardCode AND D.DocDate >= @dateFrom AND D.DocDate < @dateTo AND ISNULL(D.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR D.SlpCode = @salesPersonCode))
                  OR EXISTS (SELECT 1 FROM OINV I WHERE I.CardCode = C.CardCode AND I.DocDate >= @dateFrom AND I.DocDate < @dateTo AND ISNULL(I.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode))
                )",
            r => Convert.ToInt32(r[0]));

        result.UnpaidInvoicesCount = await ExecAsync("UnpaidInvoicesCount",
            "SELECT COUNT(1) FROM OINV WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001 AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.UnpaidInvoicesAmount = await ExecAsync("UnpaidInvoicesAmount",
            "SELECT ISNULL(SUM(ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)), 0) FROM OINV WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001 AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        result.QuoteValidationDays = await ExecAsync("QuoteValidationDays",
            "SELECT ISNULL(AVG(DATEDIFF(day, Q.DocDate, O.DocDate)), 0) FROM OQUT Q INNER JOIN RDR1 R ON R.BaseEntry = Q.DocEntry AND R.BaseType = 23 INNER JOIN ORDR O ON O.DocEntry = R.DocEntry WHERE O.DocDate >= @dateFrom AND O.DocDate < @dateTo AND ISNULL(Q.CANCELED, 'N') <> 'Y' AND ISNULL(O.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR O.SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        result.OverdueInvoicesCount = await ExecAsync("OverdueInvoicesCount",
            "SELECT COUNT(1) FROM OINV WHERE DocDueDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001 AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));
        result.OverdueInvoicesAmount = await ExecAsync("OverdueInvoicesAmount",
            "SELECT ISNULL(SUM(ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)), 0) FROM OINV WHERE DocDueDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001 AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        }

        // Nouveaux KPIs (peuvent echouer si certaines tables SAP n'existent pas)
        result.CollectedRevenue = await ExecAsync("CollectedRevenue",
            "SELECT ISNULL(SUM(ISNULL(R2.SumApplied, 0)), 0) FROM ORCT P INNER JOIN RCT2 R2 ON R2.DocNum = P.DocEntry AND ISNULL(R2.InvType, 13) = 13 INNER JOIN OINV I ON I.DocEntry = R2.DocEntry WHERE P.DocDate >= @dateFrom AND P.DocDate < @dateTo AND ISNULL(P.Canceled, 'N') <> 'Y' AND ISNULL(I.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        if (!lightweight)
        {
            result.PaymentRate = await ExecAsync("PaymentRate",
            "SELECT CASE WHEN COUNT(1) = 0 THEN 0 ELSE CAST(SUM(CASE WHEN ISNULL(DocTotal,0) - ISNULL(PaidToDate,0) <= 0.001 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(1) * 100 END FROM OINV WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        result.NewActivePartnersCount = await ExecAsync("NewActivePartnersCount",
            "SELECT COUNT(1) FROM OCRD C WHERE C.CardType = 'C' AND C.CreateDate >= @dateFrom AND C.CreateDate < @dateTo AND (@applyScope = 0 OR C.SlpCode = @salesPersonCode)",
            r => Convert.ToInt32(r[0]));

        result.OpenPipelineAmount = await ExecAsync("OpenPipelineAmount",
            "SELECT ISNULL(SUM(DocTotal), 0) FROM OQUT WHERE DocStatus = 'O' AND ISNULL(CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        // Dso
        result.Dso = await ExecAsync("Dso",
            "SELECT ISNULL(AVG(DATEDIFF(day, I.DocDate, P.DocDate)), 0) FROM OINV I INNER JOIN RCT2 R2 ON R2.DocEntry = I.DocEntry AND ISNULL(R2.InvType, 13) = 13 INNER JOIN ORCT P ON P.DocEntry = R2.DocNum WHERE P.DocDate >= @dateFrom AND P.DocDate < @dateTo AND ISNULL(P.Canceled, 'N') <> 'Y' AND ISNULL(I.CANCELED, 'N') <> 'Y' AND (@applyScope = 0 OR I.SlpCode = @salesPersonCode)",
            r => Convert.ToDecimal(r[0]));

        }

        // Conversion rates via sous-requetes separees
        var convertedQuotes = await ExecAsync("ConvertedQuotesCount",
            "SELECT COUNT(DISTINCT Q.DocEntry) FROM OQUT Q WHERE Q.DocDate >= @dateFrom AND Q.DocDate < @dateTo AND (@applyScope = 0 OR Q.SlpCode = @salesPersonCode) AND EXISTS (SELECT 1 FROM RDR1 WHERE RDR1.BaseType = 23 AND RDR1.BaseEntry = Q.DocEntry)",
            r => Convert.ToInt32(r[0]));
        var convertedOrders = await ExecAsync("ConvertedOrdersCount",
            "SELECT COUNT(DISTINCT O.DocEntry) FROM ORDR O WHERE O.DocDate >= @dateFrom AND O.DocDate < @dateTo AND (@applyScope = 0 OR O.SlpCode = @salesPersonCode) AND EXISTS (SELECT 1 FROM DLN1 WHERE DLN1.BaseType = 17 AND DLN1.BaseEntry = O.DocEntry)",
            r => Convert.ToInt32(r[0]));
        var convertedDeliveries = await ExecAsync("ConvertedDeliveryCount",
            "SELECT COUNT(DISTINCT D.DocEntry) FROM ODLN D WHERE D.DocDate >= @dateFrom AND D.DocDate < @dateTo AND (@applyScope = 0 OR D.SlpCode = @salesPersonCode) AND EXISTS (SELECT 1 FROM INV1 WHERE INV1.BaseType = 15 AND INV1.BaseEntry = D.DocEntry)",
            r => Convert.ToInt32(r[0]));

        // Calculs derives
        var quotesCount = result.QuotesCount;
        var ordersCount = result.OrdersCount;
        var deliveryCount = result.DeliveryNotesCount;
        var invoicesAmount = result.InvoicesAmount;
        var creditNotesAmount = result.CreditNotesAmount;
        var quotesAmount = result.QuotesAmount;
        var collectedRevenue = result.CollectedRevenue;
        var quoteValidationDays = result.QuoteValidationDays;
        var dso = result.Dso;

        result.QuoteToOrderRate = quotesCount <= 0 ? 0 : Math.Round((decimal)convertedQuotes * 100m / quotesCount, 2);
        result.OrderToDeliveryRate = ordersCount <= 0 ? 0 : Math.Round((decimal)convertedOrders * 100m / ordersCount, 2);
        result.DeliveryToInvoiceRate = deliveryCount <= 0 ? 0 : Math.Round((decimal)convertedDeliveries * 100m / deliveryCount, 2);
        result.ConversionRate = result.QuoteToOrderRate;
        result.NetRevenue = invoicesAmount - creditNotesAmount;
        result.AverageQuoteAmount = quotesCount <= 0 ? 0 : Math.Round(quotesAmount / quotesCount, 2);
        result.SalesCycleDays = Math.Round(quoteValidationDays + dso, 1);

        var monthlyTarget = await GetMonthlyTargetAsync(salesPersonCode, cancellationToken);
        var targetCommercialCount = await ResolveTargetCommercialCountAsync(salesPersonCode, cancellationToken);
        var periodTarget = Math.Round(monthlyTarget * targetCommercialCount * ResolveTargetMultiplier(start, end), 2);
        result.MonthlyTarget = monthlyTarget;
        result.PeriodTarget = periodTarget;
        result.TargetAchievementRate = periodTarget > 0 ? Math.Round(collectedRevenue / periodTarget * 100, 2) : 0;

        return result;
    }

    private static decimal ResolveTargetMultiplier(DateTime start, DateTime end)
    {
        var s = start.Date;
        var e = end.Date;
        if (e <= s) return 1m;

        var wholeMonths = CountWholeMonths(s, e);
        if (wholeMonths > 0)
            return wholeMonths;

        var days = (decimal)(e - s).TotalDays;
        return Math.Round(days / 30.4375m, 4);
    }

    private static int CountWholeMonths(DateTime start, DateTime end)
    {
        var cursor = start;
        var months = 0;
        while (cursor.Day == 1 && cursor.AddMonths(1) <= end)
        {
            cursor = cursor.AddMonths(1);
            months++;
        }
        return cursor == end ? months : 0;
    }

    private async Task EnsureSalesTargetsTableAsync(CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(cancellationToken);
        const string sql = @"
IF OBJECT_ID(N'dbo.SalesTargets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesTargets
    (
        SalesPersonCode INT NOT NULL CONSTRAINT PK_SalesTargets PRIMARY KEY,
        MonthlyTarget DECIMAL(18,4) NOT NULL CONSTRAINT DF_SalesTargets_MonthlyTarget DEFAULT(0),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_SalesTargets_UpdatedAt DEFAULT(SYSUTCDATETIME())
    );
END";
        await using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ResolveTargetCommercialCountAsync(int? salesPersonCode, CancellationToken cancellationToken)
    {
        if (salesPersonCode.HasValue && salesPersonCode.Value > 0)
            return 1;

        var count = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.SapSalesPersonCode > 0 && u.Role == Roles.Commercial)
            .Select(u => u.SapSalesPersonCode)
            .Distinct()
            .CountAsync(cancellationToken);

        return Math.Max(1, count);
    }
    private async Task<decimal> GetMonthlyTargetAsync(int? salesPersonCode, CancellationToken cancellationToken)
    {
        await EnsureSalesTargetsTableAsync(cancellationToken);
        await using var conn = new SqlConnection(_db.Database.GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP 1 MonthlyTarget
FROM dbo.SalesTargets
WHERE SalesPersonCode = @salesPersonCode
   OR (@salesPersonCode > 0 AND SalesPersonCode = 0)
ORDER BY CASE WHEN SalesPersonCode = @salesPersonCode THEN 0 ELSE 1 END;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = salesPersonCode.GetValueOrDefault(0) });
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0m : Convert.ToDecimal(value);
    }

    private async Task<List<CommercialSalesPersonPerformanceDto>> LoadReportingTeamPerformanceAsync(SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? cardCode, CancellationToken cancellationToken)
    {
        var sql = @"
WITH Q AS (
  SELECT SlpCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM OQUT
  WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), O AS (
  SELECT SlpCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ORDR
  WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), D AS (
  SELECT SlpCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ODLN
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), I AS (
  SELECT SlpCode,
         COUNT(1) AS Cnt,
         ISNULL(SUM(DocTotal),0) AS Amt,
         SUM(CASE WHEN (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001 AND ISNULL(CANCELED,'N') <> 'Y' THEN 1 ELSE 0 END) AS UnpaidCnt,
         ISNULL(SUM(CASE WHEN (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) > 0.0001 AND ISNULL(CANCELED,'N') <> 'Y' THEN (ISNULL(DocTotal,0) - ISNULL(PaidToDate,0)) ELSE 0 END),0) AS UnpaidAmt
  FROM OINV
  WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), C AS (
  SELECT SlpCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ORIN
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), PO AS (
  SELECT SlpCode, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ORDR
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND ISNULL(DocStatus,'O') = 'O' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), PD AS (
  SELECT SlpCode, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ODLN
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo AND ISNULL(CANCELED,'N') <> 'Y' AND ISNULL(DocStatus,'O') = 'O' AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY SlpCode
), CQ AS (
  SELECT Q.SlpCode, COUNT(DISTINCT Q.DocEntry) AS Cnt
  FROM OQUT Q
  INNER JOIN RDR1 ON RDR1.BaseType = 23 AND RDR1.BaseEntry = Q.DocEntry
  WHERE Q.DocDate >= @dateFrom AND Q.DocDate < @dateTo AND (@applyScope = 0 OR Q.SlpCode = @salesPersonCode)
  GROUP BY Q.SlpCode
), CO AS (
  SELECT O.SlpCode, COUNT(DISTINCT O.DocEntry) AS Cnt
  FROM ORDR O
  INNER JOIN DLN1 ON DLN1.BaseType = 17 AND DLN1.BaseEntry = O.DocEntry
  WHERE O.DocDate >= @dateFrom AND O.DocDate < @dateTo AND (@applyScope = 0 OR O.SlpCode = @salesPersonCode)
  GROUP BY O.SlpCode
), CD AS (
  SELECT D.SlpCode, COUNT(DISTINCT D.DocEntry) AS Cnt
  FROM ODLN D
  INNER JOIN INV1 ON INV1.BaseType = 15 AND INV1.BaseEntry = D.DocEntry
  WHERE D.DocDate >= @dateFrom AND D.DocDate < @dateTo AND (@applyScope = 0 OR D.SlpCode = @salesPersonCode)
  GROUP BY D.SlpCode
)
SELECT
  COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode, C.SlpCode, PO.SlpCode, PD.SlpCode, CQ.SlpCode, CO.SlpCode, CD.SlpCode) AS SlpCode,
  ISNULL(Q.Cnt,0) AS QuotesCount,
  ISNULL(Q.Amt,0) AS QuotesAmount,
  ISNULL(O.Cnt,0) AS OrdersCount,
  ISNULL(O.Amt,0) AS OrdersAmount,
  ISNULL(D.Cnt,0) AS DeliveryNotesCount,
  ISNULL(D.Amt,0) AS DeliveryNotesAmount,
  ISNULL(I.Cnt,0) AS InvoicesCount,
  ISNULL(I.Amt,0) AS InvoicesAmount,
  ISNULL(C.Cnt,0) AS CreditNotesCount,
  ISNULL(C.Amt,0) AS CreditNotesAmount,
  ISNULL(PO.Amt,0) + ISNULL(PD.Amt,0) AS PendingRevenue,
  ISNULL(I.UnpaidCnt,0) AS UnpaidInvoicesCount,
  ISNULL(I.UnpaidAmt,0) AS UnpaidInvoicesAmount,
  ISNULL(CQ.Cnt,0) AS ConvertedQuotesCount,
  ISNULL(CO.Cnt,0) AS ConvertedOrdersCount,
  ISNULL(CD.Cnt,0) AS ConvertedDeliveryCount
FROM Q
FULL OUTER JOIN O ON O.SlpCode = Q.SlpCode
FULL OUTER JOIN D ON D.SlpCode = COALESCE(Q.SlpCode, O.SlpCode)
FULL OUTER JOIN I ON I.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode)
FULL OUTER JOIN C ON C.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode)
FULL OUTER JOIN PO ON PO.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode, C.SlpCode)
FULL OUTER JOIN PD ON PD.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode, C.SlpCode, PO.SlpCode)
FULL OUTER JOIN CQ ON CQ.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode, C.SlpCode, PO.SlpCode, PD.SlpCode)
FULL OUTER JOIN CO ON CO.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode, C.SlpCode, PO.SlpCode, PD.SlpCode, CQ.SlpCode)
FULL OUTER JOIN CD ON CD.SlpCode = COALESCE(Q.SlpCode, O.SlpCode, D.SlpCode, I.SlpCode, C.SlpCode, PO.SlpCode, PD.SlpCode, CQ.SlpCode, CO.SlpCode)
ORDER BY SlpCode;";

        var result = new List<CommercialSalesPersonPerformanceDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var quotesCount = Convert.ToInt32(reader["QuotesCount"]);
            var ordersCount = Convert.ToInt32(reader["OrdersCount"]);
            var deliveryCount = Convert.ToInt32(reader["DeliveryNotesCount"]);
            var invoiceCount = Convert.ToInt32(reader["InvoicesCount"]);
            var convertedQuotes = Convert.ToInt32(reader["ConvertedQuotesCount"]);
            var convertedOrders = Convert.ToInt32(reader["ConvertedOrdersCount"]);
            var convertedDeliveries = Convert.ToInt32(reader["ConvertedDeliveryCount"]);
            var quoteToOrder = quotesCount <= 0 ? 0 : Math.Round((decimal)convertedQuotes * 100m / quotesCount, 2);
            var orderToDelivery = ordersCount <= 0 ? 0 : Math.Round((decimal)convertedOrders * 100m / ordersCount, 2);
            var deliveryToInvoice = deliveryCount <= 0 ? 0 : Math.Round((decimal)convertedDeliveries * 100m / deliveryCount, 2);
            var invoicesAmount = Convert.ToDecimal(reader["InvoicesAmount"]);
            var creditNotesAmount = Convert.ToDecimal(reader["CreditNotesAmount"]);
            result.Add(new CommercialSalesPersonPerformanceDto
            {
                SalesPersonCode = reader["SlpCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SlpCode"]),
                SalesPersonName = string.Empty,
                QuotesCount = quotesCount,
                QuotesAmount = Convert.ToDecimal(reader["QuotesAmount"]),
                OrdersCount = ordersCount,
                OrdersAmount = Convert.ToDecimal(reader["OrdersAmount"]),
                DeliveryNotesCount = deliveryCount,
                DeliveryNotesAmount = Convert.ToDecimal(reader["DeliveryNotesAmount"]),
                InvoicesCount = invoiceCount,
                InvoicesAmount = invoicesAmount,
                CreditNotesCount = Convert.ToInt32(reader["CreditNotesCount"]),
                CreditNotesAmount = creditNotesAmount,
                NetRevenue = invoicesAmount - creditNotesAmount,
                PendingRevenue = Convert.ToDecimal(reader["PendingRevenue"]),
                UnpaidInvoicesCount = Convert.ToInt32(reader["UnpaidInvoicesCount"]),
                UnpaidInvoicesAmount = Convert.ToDecimal(reader["UnpaidInvoicesAmount"]),
                QuoteToOrderRate = quoteToOrder,
                OrderToDeliveryRate = orderToDelivery,
                DeliveryToInvoiceRate = deliveryToInvoice,
                ConversionRate = quoteToOrder
            });
        }

        return result;
    }

    private async Task<List<CommercialRecentDocumentDto>> LoadReportingRecentDocumentsAsync(SqlConnection conn, DateTime start, DateTime end, int? salesPersonCode, string? cardCode, CancellationToken cancellationToken)
    {
        var sql = @"
SELECT TOP 200 SourceType, DocEntry, DocNum, CardCode, CardName, DocTotal, DocDate, SlpCode, DocStatusLabel
FROM (
    SELECT 'Devis' AS SourceType, DocEntry, DocNum, CardCode, CardName, DocTotal, DocDate, SlpCode,
           CASE WHEN ISNULL(CANCELED,'N') = 'Y' THEN 'Annule' WHEN ISNULL(DocStatus,'O') = 'C' THEN 'Cloture' ELSE 'En attente' END AS DocStatusLabel
    FROM OQUT
    UNION ALL
    SELECT 'Commande' AS SourceType, DocEntry, DocNum, CardCode, CardName, DocTotal, DocDate, SlpCode,
           CASE WHEN ISNULL(CANCELED,'N') = 'Y' THEN 'Annule' WHEN ISNULL(DocStatus,'O') = 'C' THEN 'Cloture' ELSE 'En attente' END AS DocStatusLabel
    FROM ORDR
    UNION ALL
    SELECT 'Bon de livraison' AS SourceType, DocEntry, DocNum, CardCode, CardName, DocTotal, DocDate, SlpCode,
           CASE WHEN ISNULL(CANCELED,'N') = 'Y' THEN 'Annule' WHEN ISNULL(DocStatus,'O') = 'C' THEN 'Cloture' ELSE 'En attente' END AS DocStatusLabel
    FROM ODLN
    UNION ALL
    SELECT 'Facture' AS SourceType, DocEntry, DocNum, CardCode, CardName, DocTotal, DocDate, SlpCode,
           CASE WHEN ISNULL(CANCELED,'N') = 'Y' THEN 'Annule' WHEN ISNULL(DocStatus,'O') = 'C' THEN 'Cloture' ELSE 'En attente' END AS DocStatusLabel
    FROM OINV
) T
WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
ORDER BY DocDate DESC, DocEntry DESC;";

        var result = new List<CommercialRecentDocumentDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        AddReportingPartnerParameters(cmd, cardCode);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CommercialRecentDocumentDto
            {
                Type = reader["SourceType"]?.ToString() ?? string.Empty,
                DocEntry = Convert.ToInt32(reader["DocEntry"]),
                DocNum = Convert.ToInt32(reader["DocNum"]),
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                Total = Convert.ToDecimal(reader["DocTotal"]),
                Date = reader["DocDate"] is DateTime d ? d : null,
                SalesPersonCode = reader["SlpCode"] == DBNull.Value ? 0 : Convert.ToInt32(reader["SlpCode"]),
                Status = reader["DocStatusLabel"]?.ToString() ?? "En attente"
            });
        }

        return result;
    }

    private async Task<List<CommercialPartnerActivityDto>> LoadPartnersActivityAsync(
        SqlConnection conn,
        DateTime start,
        DateTime end,
        int? salesPersonCode,
        string activity,
        string? search,
        CancellationToken cancellationToken)
    {
        var sql = @"
WITH Q AS (
  SELECT CardCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM OQUT
  WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY CardCode
), O AS (
  SELECT CardCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ORDR
  WHERE DocDate >= @dateFrom
  AND DocDate < @dateTo
  AND (@applyCard = 0 OR CardCode = @cardCode)
  AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY CardCode
), D AS (
  SELECT CardCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ODLN
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo
    AND ISNULL(CANCELED,'N') <> 'Y'
    AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY CardCode
), I AS (
  SELECT CardCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM OINV
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo
    AND ISNULL(CANCELED,'N') <> 'Y'
    AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY CardCode
), C AS (
  SELECT CardCode, COUNT(1) AS Cnt, ISNULL(SUM(DocTotal),0) AS Amt
  FROM ORIN
  WHERE DocDate >= @dateFrom AND DocDate < @dateTo
    AND ISNULL(CANCELED,'N') <> 'Y'
    AND (@applyScope = 0 OR SlpCode = @salesPersonCode)
  GROUP BY CardCode
)
SELECT
  BP.CardCode,
  BP.CardName,
  ISNULL(BP.SlpCode, 0) AS SlpCode,
  ISNULL(Q.Cnt,0) AS QuotesCount,
  ISNULL(Q.Amt,0) AS QuotesAmount,
  ISNULL(O.Cnt,0) AS OrdersCount,
  ISNULL(O.Amt,0) AS OrdersAmount,
  ISNULL(D.Cnt,0) AS DeliveryNotesCount,
  ISNULL(D.Amt,0) AS DeliveryNotesAmount,
  ISNULL(I.Cnt,0) AS InvoicesCount,
  ISNULL(I.Amt,0) AS InvoicesAmount,
  ISNULL(C.Cnt,0) AS CreditNotesCount,
  ISNULL(C.Amt,0) AS CreditNotesAmount
FROM OCRD BP
LEFT JOIN Q ON Q.CardCode = BP.CardCode
LEFT JOIN O ON O.CardCode = BP.CardCode
LEFT JOIN D ON D.CardCode = BP.CardCode
LEFT JOIN I ON I.CardCode = BP.CardCode
LEFT JOIN C ON C.CardCode = BP.CardCode
WHERE BP.CardType = 'C'
  AND (@applyScope = 0 OR BP.SlpCode = @salesPersonCode)
  AND (@search = '' OR BP.CardCode LIKE @searchLike OR BP.CardName LIKE @searchLike)
ORDER BY BP.CardName, BP.CardCode;";

        var result = new List<CommercialPartnerActivityDto>();
        await using var cmd = new SqlCommand(sql, conn);
        AddReportingPeriodParameters(cmd, start, end);
        AddReportingScopeParameters(cmd, salesPersonCode);
        var normalizedSearch = (search ?? string.Empty).Trim();
        cmd.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 200) { Value = normalizedSearch });
        cmd.Parameters.Add(new SqlParameter("@searchLike", SqlDbType.NVarChar, 210) { Value = $"%{normalizedSearch}%" });

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var invoicesCount = Convert.ToInt32(reader["InvoicesCount"]);
            var row = new CommercialPartnerActivityDto
            {
                CardCode = reader["CardCode"]?.ToString() ?? string.Empty,
                CardName = reader["CardName"]?.ToString() ?? string.Empty,
                SalesPersonCode = Convert.ToInt32(reader["SlpCode"]),
                QuotesCount = Convert.ToInt32(reader["QuotesCount"]),
                QuotesAmount = Convert.ToDecimal(reader["QuotesAmount"]),
                OrdersCount = Convert.ToInt32(reader["OrdersCount"]),
                OrdersAmount = Convert.ToDecimal(reader["OrdersAmount"]),
                DeliveryNotesCount = Convert.ToInt32(reader["DeliveryNotesCount"]),
                DeliveryNotesAmount = Convert.ToDecimal(reader["DeliveryNotesAmount"]),
                InvoicesCount = invoicesCount,
                InvoicesAmount = Convert.ToDecimal(reader["InvoicesAmount"]),
                CreditNotesCount = Convert.ToInt32(reader["CreditNotesCount"]),
                CreditNotesAmount = Convert.ToDecimal(reader["CreditNotesAmount"])
            };
            row.NetRevenue = row.InvoicesAmount - row.CreditNotesAmount;
            row.IsActive = invoicesCount > 0;
            result.Add(row);
        }

        var mode = (activity ?? "all").Trim().ToLowerInvariant();
        if (mode == "active")
            return result.Where(r => r.IsActive).ToList();
        if (mode == "inactive")
            return result.Where(r => !r.IsActive).ToList();
        return result;
    }

    private static string ResolveDocumentListSortOrder(string? sortBy, string? sortDirection, bool isInvoiceTable)
    {
        var direction = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "DESC";

        var column = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "number" => "H.DocNum",
            "partner" => "H.CardName",
            "date" => "H.DocDate",
            "status" => "CASE WHEN ISNULL(H.CANCELED, 'N') = 'Y' THEN 2 WHEN ISNULL(H.DocStatus, 'O') = 'O' THEN 0 ELSE 1 END",
            "total" => "ISNULL(H.DocTotal, 0)",
            "paid" when isInvoiceTable => "ISNULL(H.PaidToDate, 0)",
            "open" when isInvoiceTable => "ISNULL(H.DocTotal, 0) - ISNULL(H.PaidToDate, 0)",
            _ => "H.DocEntry"
        };

        return $"{column} {direction}, H.DocEntry DESC";
    }
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
        pageSize = Math.Max(1, pageSize);
        var isInvoiceTable = string.Equals(tableName, "OINV", StringComparison.OrdinalIgnoreCase);
        var normalizedSearch = (search ?? string.Empty).Trim();
        var normalizedCustomer = (customer ?? string.Empty).Trim();
        var normalizedStatus = NormalizeDocumentStatusFilter(status);
        var sortBy = Request.Query["sortBy"].FirstOrDefault();
        var sortDirection = Request.Query["sortDirection"].FirstOrDefault();
        var sortOrderExpression = ResolveDocumentListSortOrder(sortBy, sortDirection, isInvoiceTable);
        var applySalesScope = ShouldApplySalesScopeBySalesPerson(tableName);
        var isAdmin = _currentUserService.IsAdmin();
        var salesPersonCode = _currentUserService.GetSapSalesPersonCode();
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
        var salesScopeCondition = applySalesScope
            ? @"      AND (
                    @isAdmin = 1
                    OR (
                        @salesPersonCode > 0
                        AND (
                          ISNULL(SlpCode, 0) = @salesPersonCode
                          OR EXISTS (
                            SELECT 1
                            FROM OCRD BP
                            WHERE BP.CardCode = H.CardCode
                              AND ISNULL(BP.SlpCode, 0) = @salesPersonCode
                          )
                        )
                    )
                  )"
            : string.Empty;

        var sql = $@"
;WITH Filtered AS
(
    SELECT {selectColumns},
           ROW_NUMBER() OVER (ORDER BY {sortOrderExpression}) AS RowNum
    FROM {tableName} H
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
{salesScopeCondition}
)
SELECT {selectColumns}
FROM Filtered
WHERE RowNum BETWEEN @rowStart AND @rowEnd
ORDER BY RowNum;";

        var countSql = $@"
SELECT COUNT(1)
FROM {tableName} H
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
{salesScopeCondition};";

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
                    if (applySalesScope)
                    {
                        countCmd.Parameters.Add(new SqlParameter("@isAdmin", SqlDbType.Bit) { Value = isAdmin });
                        countCmd.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = salesPersonCode });
                    }
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
                if (applySalesScope)
                {
                    cmd.Parameters.Add(new SqlParameter("@isAdmin", SqlDbType.Bit) { Value = isAdmin });
                    cmd.Parameters.Add(new SqlParameter("@salesPersonCode", SqlDbType.Int) { Value = salesPersonCode });
                }
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
            !server.Contains('\\'))
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
            ConnectTimeout = GetSapSqlConnectTimeoutSeconds(),
            ConnectRetryCount = 0
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

        const string workingDataSourceCacheKey = "sap:sql:working-datasource";
        var candidates = new List<string>();
        if (_cache.TryGetValue(workingDataSourceCacheKey, out string? cachedDataSource) &&
            !string.IsNullOrWhiteSpace(cachedDataSource))
        {
            candidates.Add(cachedDataSource);
        }

        candidates.Add(baseDataSource);

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
                _cache.Set(workingDataSourceCacheKey, dataSource, TimeSpan.FromHours(1));
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
            return 0;

        return timeout <= 0 ? 0 : Math.Clamp(timeout, 3, 120);
    }

    private int GetSapSqlConnectTimeoutSeconds()
    {
        var raw = _configuration["SapB1:SqlConnectTimeoutSeconds"];
        if (!int.TryParse(raw, out var timeout))
            timeout = 15;

        return Math.Clamp(timeout, 5, 60);
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
                    ? $@"SELECT TOP 1 H.DocEntry, H.DocNum, H.CardCode, H.CardName, H.DocDate, H.DocDueDate, H.DocTotal, H.PaidToDate, H.DocStatus, H.CANCELED, H.Comments, H.DocCur, H.SlpCode,
       ISNULL(BP.SlpCode, 0) AS PartnerSlpCode
FROM {headerTable} H
LEFT JOIN OCRD BP ON BP.CardCode = H.CardCode
WHERE H.DocEntry = @docEntry;"
                    : $@"SELECT TOP 1 H.DocEntry, H.DocNum, H.CardCode, H.CardName, H.DocDate, H.DocDueDate, H.DocTotal, H.DocStatus, H.CANCELED, H.Comments, H.DocCur, H.SlpCode,
       ISNULL(BP.SlpCode, 0) AS PartnerSlpCode
FROM {headerTable} H
LEFT JOIN OCRD BP ON BP.CardCode = H.CardCode
WHERE H.DocEntry = @docEntry;";

            await using var headerCmd = new SqlCommand(headerSql, conn);
            headerCmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            headerCmd.Parameters.Add(new SqlParameter("@docEntry", SqlDbType.Int) { Value = docEntry });

            await using var headerReader = await headerCmd.ExecuteReaderAsync(cancellationToken);
            if (!await headerReader.ReadAsync(cancellationToken))
                return Ok(new ApiResponse<object>(true, null, null));

            var salesPersonCode = headerReader["SlpCode"] is DBNull ? 0 : Convert.ToInt32(headerReader["SlpCode"]);
            var partnerSalesPersonCode = headerReader["PartnerSlpCode"] is DBNull ? 0 : Convert.ToInt32(headerReader["PartnerSlpCode"]);
            var isAdmin = _currentUserService.IsAdmin();
            var currentSalesPersonCode = _currentUserService.GetSapSalesPersonCode();
            if (!isAdmin)
            {
                if (currentSalesPersonCode <= 0)
                    return Forbid();
                if (salesPersonCode != currentSalesPersonCode && partnerSalesPersonCode != currentSalesPersonCode)
                    return Forbid();
            }
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
                ["DocCurrency"] = headerReader["DocCur"]?.ToString() ?? string.Empty,
                ["SalesPersonCode"] = salesPersonCode > 0 ? salesPersonCode : null
            };

            await headerReader.CloseAsync();
            if (salesPersonCode > 0)
            {
                var mappedUser = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && u.SapSalesPersonCode == salesPersonCode)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(mappedUser))
                    header["SalesPersonName"] = mappedUser;
            }

            var lines = new List<Dictionary<string, object?>>();
            var lineSql = $@"SELECT LineNum, ItemCode, Dscription, Quantity, Price, DiscPrcnt, VatPrcnt, WhsCode, LineStatus, LineTotal,
       BaseType, BaseEntry, BaseLine
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
                    ["LineTotal"] = lineReader["LineTotal"] is DBNull ? 0m : Convert.ToDecimal(lineReader["LineTotal"]),
                    ["BaseType"] = lineReader["BaseType"] is DBNull ? null : lineReader["BaseType"],
                    ["BaseEntry"] = lineReader["BaseEntry"] is DBNull ? null : Convert.ToInt32(lineReader["BaseEntry"]),
                    ["BaseLine"] = lineReader["BaseLine"] is DBNull ? null : Convert.ToInt32(lineReader["BaseLine"])
                });
                var lt = lineReader["LineTotal"] is DBNull ? 0m : Convert.ToDecimal(lineReader["LineTotal"]);
                var vp = lineReader["VatPrcnt"] is DBNull ? 0m : Convert.ToDecimal(lineReader["VatPrcnt"]);
                var vatAmt = Math.Round(lt * vp / 100m, 2);
                lines[^1]["subtotalHt"] = lt;
                lines[^1]["vatAmount"] = vatAmt;
                lines[^1]["totalTtc"] = lt + vatAmt;
            }
            await lineReader.CloseAsync();

            header["DocumentLines"] = lines;
            var (sourceDocument, linkedDocuments) = BuildRelationsFromSqlLines(lines, docEntry);
            var generatedByBase = await FindGeneratedDocumentsFromSqlAsync(conn, sapEntity, docEntry, cancellationToken);
            foreach (var generated in generatedByBase)
            {
                var alreadyExists = linkedDocuments.Any(x =>
                    string.Equals(x.TryGetValue("type", out var t) ? t?.ToString() : null, generated.Type, StringComparison.OrdinalIgnoreCase)
                    && GetIntFromObject(x.TryGetValue("id", out var i) ? i : null) == generated.Id);
                if (alreadyExists) continue;

                linkedDocuments.Add(new Dictionary<string, object?>
                {
                    ["type"] = generated.Type,
                    ["id"] = generated.Id,
                    ["docNum"] = generated.DocNum
                });
            }
            if (sourceDocument is not null)
                header["sourceDocument"] = sourceDocument;
            if (linkedDocuments.Count > 0)
                header["linkedDocuments"] = linkedDocuments;

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

    private static (Dictionary<string, object?>? Source, List<Dictionary<string, object?>> Linked) BuildRelationsFromSqlLines(
        IReadOnlyList<Dictionary<string, object?>> lines,
        int currentDocEntry)
    {
        Dictionary<string, object?>? source = null;
        var linked = new List<Dictionary<string, object?>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var baseEntry = GetIntFromObject(line.TryGetValue("BaseEntry", out var be) ? be : null);
            var baseType = ResolveLinkedTypeFromObjectCode(line.TryGetValue("BaseType", out var bt) ? bt : null);
            if (source is null && baseEntry > 0 && !string.IsNullOrWhiteSpace(baseType))
            {
                source = new Dictionary<string, object?>
                {
                    ["type"] = baseType,
                    ["id"] = baseEntry
                };
            }

            var targetEntry = GetIntFromObject(line.TryGetValue("TrgetEntry", out var te) ? te : null);
            var targetType = ResolveLinkedTypeFromObjectCode(line.TryGetValue("TargetType", out var tt) ? tt : null);
            if (targetEntry <= 0 || string.IsNullOrWhiteSpace(targetType) || targetEntry == currentDocEntry)
                continue;

            var key = $"{targetType}:{targetEntry}";
            if (!seen.Add(key))
                continue;

            linked.Add(new Dictionary<string, object?>
            {
                ["type"] = targetType,
                ["id"] = targetEntry
            });
        }

        return (source, linked);
    }

    private static int GetIntFromObject(object? value)
    {
        if (value is null || value is DBNull)
            return 0;
        if (value is int i)
            return i;
        if (value is long l)
            return l is > int.MaxValue or < int.MinValue ? 0 : (int)l;
        if (value is decimal d)
            return d is > int.MaxValue or < int.MinValue ? 0 : (int)d;
        if (int.TryParse(value.ToString(), out var parsed))
            return parsed;
        return 0;
    }

    private static decimal GetDecimalFromRow(Dictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var val) && val is not null && val is not DBNull)
            {
                if (val is decimal d) return d;
                if (val is double db) return (decimal)db;
                if (val is float f) return (decimal)f;
                if (val is int i) return i;
                if (val is long l) return l;
                if (decimal.TryParse(val.ToString(), out var parsed)) return parsed;
            }
        }
        return 0m;
    }

    private static string ResolveLinkedTypeFromObjectCode(object? value)
    {
        var raw = value?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        if (int.TryParse(raw, out var code))
        {
            return code switch
            {
                23 => "quote",
                17 => "order",
                15 => "deliverynote",
                13 => "invoice",
                14 => "creditnote",
                16 => "return",
                _ => string.Empty
            };
        }

        var normalized = raw.ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        if (normalized.Contains("quote") || normalized.Contains("devis")) return "quote";
        if (normalized.Contains("order") || normalized.Contains("commande")) return "order";
        if (normalized.Contains("deliverynote") || normalized.Contains("bonlivraison")) return "deliverynote";
        if (normalized.Contains("invoice") || normalized.Contains("facture")) return "invoice";
        if (normalized.Contains("creditnote") || normalized.Contains("avoir")) return "creditnote";
        if (normalized.Contains("return") || normalized.Contains("retour")) return "return";
        return string.Empty;
    }

    private async Task<List<(string Type, int Id, int DocNum)>> FindGeneratedDocumentsFromSqlAsync(
        SqlConnection conn,
        string sourceEntity,
        int sourceDocEntry,
        CancellationToken cancellationToken)
    {
        var result = new List<(string Type, int Id, int DocNum)>();

        var baseTypeRaw = ResolveDocObjectCode(sourceEntity);
        if (!int.TryParse(baseTypeRaw, out var baseType) || sourceDocEntry <= 0)
            return result;

        var targets = new (string HeaderTable, string LineTable, string Type)[]
        {
            ("OQUT", "QUT1", "quote"),
            ("ORDR", "RDR1", "order"),
            ("ODLN", "DLN1", "deliverynote"),
            ("OINV", "INV1", "invoice"),
            ("ORIN", "RIN1", "creditnote"),
            ("ORDN", "RDN1", "return")
        };

        foreach (var target in targets)
        {
            var sql = $@"
SELECT DISTINCT H.DocEntry, H.DocNum
FROM {target.HeaderTable} H
INNER JOIN {target.LineTable} L ON L.DocEntry = H.DocEntry
WHERE L.BaseType = @baseType
  AND L.BaseEntry = @baseEntry
  AND H.DocEntry <> @baseEntry;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            cmd.Parameters.Add(new SqlParameter("@baseType", SqlDbType.Int) { Value = baseType });
            cmd.Parameters.Add(new SqlParameter("@baseEntry", SqlDbType.Int) { Value = sourceDocEntry });

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var docEntry = reader["DocEntry"] is DBNull ? 0 : Convert.ToInt32(reader["DocEntry"]);
                if (docEntry <= 0)
                    continue;

                var docNum = reader["DocNum"] is DBNull ? 0 : Convert.ToInt32(reader["DocNum"]);
                result.Add((target.Type, docEntry, docNum));
            }
        }

        return result;
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
                $"{sapEntity}({docEntry})",
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
                    await ReopenSourceLinesConditionallyAfterCancellationAsync(current.Response.Value, docEntry, cancellationToken);

                    var refreshed = await _sapService.ServiceLayerGetAsync($"{sapEntity}({docEntry})", cancellationToken);
                    var responseData = refreshed.Success && refreshed.Response.HasValue
                        ? NormalizeDocumentForFrontend(refreshed.Response.Value)
                        : null;

                    return Ok(new ApiResponse<object>(true, "Annulation réussie.", responseData));
                }
            }

            return StatusCode(cancelResult.StatusCode, SapError(cancelResult.ErrorMessage ?? "Annulation non supportée pour cet objet SAP.", cancelResult.Response));
        }

        await ReopenSourceLinesConditionallyAfterCancellationAsync(sapEntity, docEntry, cancellationToken);

        var result = await _sapService.ServiceLayerDeleteAsync($"{sapEntity}({docEntry})", cancellationToken);
        if (!result.Success)
            return StatusCode(result.StatusCode, SapError(result.ErrorMessage, result.Response));

        return Ok(new ApiResponse<object>(true, "Suppression réussie.", result.Response));
    }

    private async Task ReopenSourceLinesConditionallyAfterCancellationAsync(
        string cancelledEntity,
        int cancelledDocEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            var cancelled = await _sapService.ServiceLayerGetAsync($"{cancelledEntity}({cancelledDocEntry})", cancellationToken);
            if (!cancelled.Success || !cancelled.Response.HasValue)
                return;

            await ReopenSourceLinesConditionallyAfterCancellationAsync(cancelled.Response.Value, cancelledDocEntry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CANCEL-REOPEN] Echec de la reouverture conditionnelle apres annulation.");
        }
    }

    private async Task ReopenSourceLinesConditionallyAfterCancellationAsync(
        JsonElement cancelledDocument,
        int cancelledDocEntry,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!cancelledDocument.TryGetProperty("DocumentLines", out var docLines) || docLines.ValueKind != JsonValueKind.Array)
                return;

            var baseRefs = docLines
                .EnumerateArray()
                .Select(l => new
                {
                    BaseType = GetStringAny(l, "BaseType"),
                    BaseEntry = GetNullableInt(l, "BaseEntry"),
                    BaseLine = GetNullableInt(l, "BaseLine")
                })
                .Where(x => x.BaseEntry.HasValue && x.BaseEntry.Value > 0 && x.BaseLine.HasValue && x.BaseLine.Value >= 0)
                .Select(x => new
                {
                    BaseTypeKey = ResolveLinkedTypeFromObjectCode(x.BaseType),
                    BaseEntry = x.BaseEntry!.Value,
                    BaseLine = x.BaseLine!.Value
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.BaseTypeKey))
                .GroupBy(x => new { x.BaseTypeKey, x.BaseEntry })
                .ToList();

            foreach (var source in baseRefs)
            {
                var sourceEntity = SourceTypeKeyToSapEntity(source.Key.BaseTypeKey);
                if (string.IsNullOrWhiteSpace(sourceEntity))
                    continue;

                var baseLines = source.Select(x => x.BaseLine).Distinct().ToList();
                var reopenableLines = await ResolveReopenableSourceLinesAsync(
                    sourceEntity,
                    source.Key.BaseEntry,
                    baseLines,
                    cancelledDocEntry,
                    cancellationToken);

                if (reopenableLines.Count == 0)
                    continue;

                // Reopen document first so eligible source lines can be re-used in next flow steps.
                var reopenDoc = await _sapService.ServiceLayerPostAsync($"{sourceEntity}({source.Key.BaseEntry})/Reopen", new { }, cancellationToken);
                if (!reopenDoc.Success)
                {
                    // Fallback: some SAP setups reject Reopen action but accept status patch.
                    await _sapService.ServiceLayerPatchAsync($"{sourceEntity}({source.Key.BaseEntry})", new
                    {
                        DocumentStatus = "bost_Open",
                        DocStatus = "O",
                        Status = "open"
                    }, cancellationToken);
                }

                var patchPayload = new
                {
                    DocumentLines = reopenableLines
                        .Select(lineNum => new Dictionary<string, object?>
                        {
                            ["LineNum"] = lineNum,
                            ["LineStatus"] = "bost_Open"
                        })
                        .ToList()
                };

                var patch = await _sapService.ServiceLayerPatchAsync($"{sourceEntity}({source.Key.BaseEntry})", patchPayload, cancellationToken);
                if (!patch.Success)
                {
                    _logger.LogWarning(
                        "[CANCEL-REOPEN] Reouverture partielle impossible. Source={SourceEntity} DocEntry={SourceDocEntry} Lines={Lines}. Error={Error}",
                        sourceEntity,
                        source.Key.BaseEntry,
                        string.Join(",", reopenableLines),
                        patch.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CANCEL-REOPEN] Echec de la reouverture conditionnelle apres annulation.");
        }
    }

    private async Task<List<int>> ResolveReopenableSourceLinesAsync(
        string sourceEntity,
        int sourceDocEntry,
        IReadOnlyCollection<int> sourceLineNums,
        int cancelledDocEntry,
        CancellationToken cancellationToken)
    {
        var result = new List<int>();
        if (sourceDocEntry <= 0 || sourceLineNums.Count == 0)
            return result;

        if (!int.TryParse(ResolveDocObjectCode(sourceEntity), out var sourceObjectType))
            return result;

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return result;

        await using (conn)
        {
            var candidates = sourceLineNums.Distinct().ToList();
            var inSql = string.Join(",", candidates.Select((_, i) => $"@line{i}"));

            var sql = $@"
SELECT L.BaseLine, COUNT(1) AS ActiveCount
FROM (
    SELECT L.BaseLine
    FROM RDR1 L INNER JOIN ORDR H ON H.DocEntry = L.DocEntry
    WHERE L.BaseType = @baseType AND L.BaseEntry = @baseEntry AND L.BaseLine IN ({inSql})
      AND ISNULL(H.CANCELED, 'N') = 'N'
      AND H.DocEntry <> @cancelledDocEntry
    UNION ALL
    SELECT L.BaseLine
    FROM DLN1 L INNER JOIN ODLN H ON H.DocEntry = L.DocEntry
    WHERE L.BaseType = @baseType AND L.BaseEntry = @baseEntry AND L.BaseLine IN ({inSql})
      AND ISNULL(H.CANCELED, 'N') = 'N'
      AND H.DocEntry <> @cancelledDocEntry
    UNION ALL
    SELECT L.BaseLine
    FROM INV1 L INNER JOIN OINV H ON H.DocEntry = L.DocEntry
    WHERE L.BaseType = @baseType AND L.BaseEntry = @baseEntry AND L.BaseLine IN ({inSql})
      AND ISNULL(H.CANCELED, 'N') = 'N'
      AND H.DocEntry <> @cancelledDocEntry
    UNION ALL
    SELECT L.BaseLine
    FROM RIN1 L INNER JOIN ORIN H ON H.DocEntry = L.DocEntry
    WHERE L.BaseType = @baseType AND L.BaseEntry = @baseEntry AND L.BaseLine IN ({inSql})
      AND ISNULL(H.CANCELED, 'N') = 'N'
      AND H.DocEntry <> @cancelledDocEntry
    UNION ALL
    SELECT L.BaseLine
    FROM RDN1 L INNER JOIN ORDN H ON H.DocEntry = L.DocEntry
    WHERE L.BaseType = @baseType AND L.BaseEntry = @baseEntry AND L.BaseLine IN ({inSql})
      AND ISNULL(H.CANCELED, 'N') = 'N'
      AND H.DocEntry <> @cancelledDocEntry
    UNION ALL
    SELECT L.BaseLine
    FROM QUT1 L INNER JOIN OQUT H ON H.DocEntry = L.DocEntry
    WHERE L.BaseType = @baseType AND L.BaseEntry = @baseEntry AND L.BaseLine IN ({inSql})
      AND ISNULL(H.CANCELED, 'N') = 'N'
      AND H.DocEntry <> @cancelledDocEntry
) L
GROUP BY L.BaseLine;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            cmd.Parameters.Add(new SqlParameter("@baseType", SqlDbType.Int) { Value = sourceObjectType });
            cmd.Parameters.Add(new SqlParameter("@baseEntry", SqlDbType.Int) { Value = sourceDocEntry });
            cmd.Parameters.Add(new SqlParameter("@cancelledDocEntry", SqlDbType.Int) { Value = cancelledDocEntry });
            for (var i = 0; i < candidates.Count; i++)
                cmd.Parameters.Add(new SqlParameter($"@line{i}", SqlDbType.Int) { Value = candidates[i] });

            var busy = new HashSet<int>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var lineNum = reader["BaseLine"] is DBNull ? -1 : Convert.ToInt32(reader["BaseLine"]);
                var activeCount = reader["ActiveCount"] is DBNull ? 0 : Convert.ToInt32(reader["ActiveCount"]);
                if (lineNum >= 0 && activeCount > 0)
                    busy.Add(lineNum);
            }

            result.AddRange(candidates.Where(line => !busy.Contains(line)));
        }

        return result;
    }

    private static string SourceTypeKeyToSapEntity(string typeKey) => typeKey.ToLowerInvariant() switch
    {
        "quote" => "Quotations",
        "order" => "Orders",
        "deliverynote" => "DeliveryNotes",
        "invoice" => "Invoices",
        "creditnote" => "CreditNotes",
        "return" => "Returns",
        _ => string.Empty
    };

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
            !string.Equals(sapEntity, "Quotations", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sapEntity, "DeliveryNotes", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sapEntity, "Invoices", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sapEntity, "Returns", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sapEntity, "CreditNotes", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(SapError("Modification non autorisee pour ce type de document."));
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
            return BadRequest(SapError("Modification refusee: seul un document en statut Open peut etre modifie."));

        request.DocumentLines = EnsureClosedLinesPresent(currentDoc, request.DocumentLines);

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

        var responseData = BuildUpdatedDocumentResponse(sapEntity, docEntry, request, currentDoc, docCurrency);

        return Ok(new ApiResponse<object>(true, "Mise � jour r�ussie.", responseData));
    }

    private static object BuildUpdatedDocumentResponse(
        string sapEntity,
        int docEntry,
        CreateSapDocumentRequest request,
        JsonElement currentDocument,
        string docCurrency)
    {
        var docDate = request.DocDate ?? GetDate(currentDocument, "DocDate") ?? DateTime.Today;
        var dueDate = request.DocDueDate ?? request.RequiredDate ?? GetDate(currentDocument, "DocDueDate") ?? docDate;
        var docNum = GetNullableInt(currentDocument, "DocNum") ?? docEntry;
        var cardName = GetStringAny(currentDocument, "CardName");
        var rawStatus = GetRawDocumentStatus(currentDocument);
        var status = NormalizeDocumentStatus(rawStatus, currentDocument);

        var lines = request.DocumentLines.Select((line, index) =>
        {
            var quantity = line.Quantity;
            var price = GetLinePrice(line);
            var discount = line.DiscountPercent.GetValueOrDefault(0m);
            var lineTotal = Math.Round(quantity * price * (1m - discount / 100m), 2);
            var vatPercent = line.VatPercent.GetValueOrDefault(0m);
            var vatAmount = Math.Round(lineTotal * vatPercent / 100m, 2);

            return new Dictionary<string, object?>
            {
                ["LineNum"] = line.LineNum ?? index,
                ["ItemCode"] = line.ItemCode,
                ["WarehouseCode"] = line.WarehouseCode,
                ["Quantity"] = quantity,
                ["Price"] = price,
                ["UnitPrice"] = price,
                ["DiscountPercent"] = discount,
                ["VatPercent"] = vatPercent,
                ["LineTotal"] = lineTotal,
                ["subtotalHt"] = lineTotal,
                ["vatAmount"] = vatAmount,
                ["totalTtc"] = lineTotal + vatAmount,
                ["LineStatus"] = string.IsNullOrWhiteSpace(line.LineStatus) ? "Open" : line.LineStatus,
                ["BaseType"] = line.BaseType,
                ["BaseEntry"] = line.BaseEntry,
                ["BaseLine"] = line.BaseLine
            };
        }).ToList();

        var totalHt = lines.Sum(line => Convert.ToDecimal(line["subtotalHt"] ?? 0m, CultureInfo.InvariantCulture));
        var totalTtc = lines.Sum(line => Convert.ToDecimal(line["totalTtc"] ?? 0m, CultureInfo.InvariantCulture));

        return new Dictionary<string, object?>
        {
            ["DocEntry"] = docEntry,
            ["docEntry"] = docEntry,
            ["Id"] = docEntry,
            ["id"] = docEntry,
            ["DocNum"] = docNum,
            ["docNum"] = docNum,
            ["DocObjectCode"] = ResolveDocObjectCode(sapEntity),
            ["CardCode"] = request.CardCode,
            ["cardCode"] = request.CardCode,
            ["CardName"] = cardName,
            ["cardName"] = cardName,
            ["DocDate"] = docDate.ToString("yyyy-MM-dd"),
            ["DocDueDate"] = dueDate.ToString("yyyy-MM-dd"),
            ["DocCurrency"] = docCurrency,
            ["Comments"] = request.Comments,
            ["DocumentStatus"] = rawStatus,
            ["DocStatus"] = rawStatus,
            ["status"] = status,
            ["Status"] = status,
            ["DocTotal"] = totalTtc,
            ["docTotal"] = totalTtc,
            ["LineTotal"] = totalHt,
            ["DocumentLines"] = lines,
            ["lines"] = lines,
            ["SalesPersonCode"] = request.SalesPersonCode ?? GetNullableInt(currentDocument, "SalesPersonCode")
        };
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

        foreach (var sourceLine in sourceLines.EnumerateArray())
        {
            var sourceLineNum = GetNullableInt(sourceLine, "LineNum");
            var sourceStatus = GetStringAny(sourceLine, "LineStatus");
            var isClosed = IsClosedLineStatus(sourceStatus);

            if (!isClosed)
                continue;

            if (!sourceLineNum.HasValue || !incomingByLineNum.TryGetValue(sourceLineNum.Value, out var candidate))
            {
                error = $"Ligne fermee #{sourceLineNum ?? -1}: suppression impossible.";
                return false;
            }

            if (HasClosedLineChanged(sourceLine, candidate))
            {
                error = $"Ligne fermee #{sourceLineNum ?? -1}: modification interdite.";
                return false;
            }
        }

        return true;
    }

    private static List<CreateSapDocumentLineRequest> EnsureClosedLinesPresent(JsonElement currentDocument, IReadOnlyList<CreateSapDocumentLineRequest>? incomingLines)
    {
        var result = incomingLines?.ToList() ?? [];
        static string FirstString(JsonElement node, params string[] names)
        {
            foreach (var name in names)
            {
                var value = GetStringAny(node, name);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        if (!currentDocument.TryGetProperty("DocumentLines", out var sourceLines) || sourceLines.ValueKind != JsonValueKind.Array)
            return result;

        var incomingIndexByLineNum = result
            .Select((line, index) => new { line, index })
            .Where(x => x.line.LineNum.HasValue)
            .ToDictionary(x => x.line.LineNum!.Value, x => x.index);

        foreach (var sourceLine in sourceLines.EnumerateArray())
        {
            var sourceLineNum = GetNullableInt(sourceLine, "LineNum");
            if (!sourceLineNum.HasValue)
                continue;

            var sourceStatus = GetStringAny(sourceLine, "LineStatus");
            if (!IsClosedLineStatus(sourceStatus))
                continue;

            var frozenClosedLine = new CreateSapDocumentLineRequest
            {
                LineNum = sourceLineNum.Value,
                LineStatus = sourceStatus,
                ItemCode = FirstString(sourceLine, "ItemCode", "itemCode"),
                Quantity = GetDecimal(sourceLine, "Quantity"),
                WarehouseCode = FirstString(sourceLine, "WarehouseCode", "WhsCode", "warehouseCode", "whsCode"),
                UnitPrice = GetDecimal(sourceLine, "UnitPrice") > 0 ? GetDecimal(sourceLine, "UnitPrice") : GetDecimal(sourceLine, "Price"),
                Price = GetDecimal(sourceLine, "Price") > 0 ? GetDecimal(sourceLine, "Price") : GetDecimal(sourceLine, "UnitPrice"),
                DiscountPercent = GetDecimal(sourceLine, "DiscountPercent"),
                VatPercent = GetDecimal(sourceLine, "VatPercent") > 0 ? GetDecimal(sourceLine, "VatPercent") : GetDecimal(sourceLine, "TaxPercent"),
                BaseType = FirstString(sourceLine, "BaseType", "baseType"),
                BaseEntry = GetNullableInt(sourceLine, "BaseEntry"),
                BaseLine = GetNullableInt(sourceLine, "BaseLine")
            };

            if (incomingIndexByLineNum.TryGetValue(sourceLineNum.Value, out var existingIndex))
            {
                // Always freeze closed lines to source values so only open lines can be effectively modified.
                result[existingIndex] = frozenClosedLine;
                continue;
            }

            result.Add(frozenClosedLine);
            incomingIndexByLineNum[sourceLineNum.Value] = result.Count - 1;
        }

        return result;
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

        var generatedQuantityError = await ValidateGeneratedDocumentQuantitiesAsync(sapEntity, request, cancellationToken);
        if (generatedQuantityError is not null)
            return BadRequest(SapError(generatedQuantityError));

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
                    new ApiResponse<object>(true, "Creation reussie.", NormalizeDocumentForFrontend(createdDoc.Response.Value)));
            }
        }

        var fallbackData = result.Response.HasValue
            ? NormalizeDocumentForFrontend(result.Response.Value)
            : null;

        if (isInvoice && docEntry > 0)
            _logger.LogWarning("[HYBRID-MODE][WRITE-SUCCESS-PARTIAL] Facture créée mais récupération post-création échouée. DocEntry={DocEntry}", docEntry);

        return StatusCode(result.StatusCode, new ApiResponse<object>(true, "Creation reussie.", fallbackData));
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
            return (false, null, "Impossible de generer: CardCode manquant dans le document source.");

        if (!source.TryGetProperty("DocumentLines", out var sourceLines) || sourceLines.ValueKind != JsonValueKind.Array)
            return (false, null, "Impossible de generer: DocumentLines manquant dans le document source.");

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
                return (false, null, $"Impossible de generer: ligne {lineIndex}, ItemCode manquant.");

            var quantity = GetDecimal(line, "Quantity");
            if (quantity <= 0)
                return (false, null, $"Impossible de generer: ligne {lineIndex}, Quantity invalide.");

            var warehouseCode = GetString(line, "WarehouseCode");
            if (string.IsNullOrWhiteSpace(warehouseCode))
                return (false, null, $"Impossible de generer: ligne {lineIndex}, WarehouseCode manquant.");

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
                return (false, null, $"Impossible de generer: ligne {lineIndex}, UnitPrice/Price invalide.");

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
            return (false, null, "Impossible de generer: aucune ligne valide dans le document source.");

        var request = new CreateSapDocumentRequest
        {
            CardCode = cardCode,
            DocDate = GetDate(source, "DocDate") ?? DateTime.Today,
            DocDueDate = GetDate(source, "DocDueDate") ?? GetDate(source, "DocDate") ?? DateTime.Today,
            RequiredDate = GetDate(source, "RequriedDate"),
            Comments = string.Empty,
            SalesPersonCode = GetNullableInt(source, "SalesPersonCode"),
            DocType = GetString(source, "DocType"),
            UserSign = GetNullableInt(source, "UserSign"),
            DocumentLines = lines
        };

        return (true, request, null);
    }

    private async Task<string?> ValidateGeneratedDocumentQuantitiesAsync(
        string sapEntity,
        CreateSapDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(sapEntity, "DeliveryNotes", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sapEntity, "Invoices", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var line in request.DocumentLines)
        {
            if (!line.BaseEntry.HasValue || !line.BaseLine.HasValue || string.IsNullOrWhiteSpace(line.BaseType))
                continue;

            var sourceEntity = ResolveEntityFromBaseType(line.BaseType);
            if (string.IsNullOrWhiteSpace(sourceEntity))
                continue;

            var sourceQuantity = await GetSourceDocumentLineQuantityAsync(
                sourceEntity,
                line.BaseEntry.Value,
                line.BaseLine.Value,
                cancellationToken);

            if (!sourceQuantity.HasValue)
                continue;

            if (sourceQuantity.Value > 0 && line.Quantity > sourceQuantity.Value + 0.0001m)
                return "La quantite du document cible ne peut pas depasser la quantite du document source.";
        }

        return null;
    }
    private async Task<decimal?> GetSourceDocumentLineQuantityAsync(
        string sourceEntity,
        int sourceDocEntry,
        int sourceLineNum,
        CancellationToken cancellationToken)
    {
        var lineTable = SourceEntityToLineTable(sourceEntity);
        if (string.IsNullOrWhiteSpace(lineTable))
            return null;

        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return null;

        await using (conn)
        await using (var cmd = new SqlCommand($@"
SELECT Quantity
FROM {lineTable}
WHERE DocEntry = @docEntry AND LineNum = @lineNum;", conn))
        {
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            cmd.Parameters.Add(new SqlParameter("@docEntry", SqlDbType.Int) { Value = sourceDocEntry });
            cmd.Parameters.Add(new SqlParameter("@lineNum", SqlDbType.Int) { Value = sourceLineNum });

            var value = await cmd.ExecuteScalarAsync(cancellationToken);
            if (value is null || value is DBNull)
                return null;

            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }
    }

    private static string? SourceEntityToLineTable(string sourceEntity) => sourceEntity switch
    {
        "Quotations" => "QUT1",
        "Orders" => "RDR1",
        "DeliveryNotes" => "DLN1",
        "Invoices" => "INV1",
        "CreditNotes" => "RIN1",
        "Returns" => "RDN1",
        _ => null
    };

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

        if (!string.IsNullOrWhiteSpace(request.DocStatus))
            payload["DocStatus"] = request.DocStatus;
        else if (!string.IsNullOrWhiteSpace(defaultDocStatus))
            payload["DocStatus"] = defaultDocStatus;

        return payload;
    }

    private Dictionary<string, object?> BuildBusinessPartnerPayload(CreateSapClientRequest request)
    {
        var isAdmin = _currentUserService.IsAdmin();
        var currentSalesPersonCode = _currentUserService.GetSapSalesPersonCode();
        var scopedSalesPersonCode = isAdmin
            ? request.SalesPersonCode
            : currentSalesPersonCode;

        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = request.CardCode,
            ["CardName"] = request.CardName,
            ["CardType"] = ResolveBusinessPartnerType(request),
            ["Currency"] = NormalizeBusinessPartnerCurrency(request.Currency)
        };

        if (scopedSalesPersonCode.HasValue && scopedSalesPersonCode.Value > 0)
            payload["SalesPersonCode"] = scopedSalesPersonCode.Value;

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

    private async Task<string?> GetNextBusinessPartnerCardCodeAsync(CancellationToken cancellationToken)
    {
        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn is null)
            return null;

        await using (conn)
        await using (var cmd = new SqlCommand(@"
SELECT CardCode
FROM OCRD
WHERE CardCode LIKE 'C[0-9][0-9][0-9][0-9][0-9][0-9]'
ORDER BY CardCode ASC;", conn))
        {
            cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
            var usedNumbers = new HashSet<int>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var rawCode = reader["CardCode"]?.ToString()?.Trim() ?? string.Empty;
                if (rawCode.Length == 7 &&
                    rawCode.StartsWith("C", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(rawCode[1..], out var parsedNumber) &&
                    parsedNumber > 0)
                {
                    usedNumbers.Add(parsedNumber);
                }
            }

            for (var candidate = 1; candidate <= 999999; candidate++)
            {
                if (!usedNumbers.Contains(candidate))
                    return $"C{candidate:000000}";
            }
        }

        return null;
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

    private static string ResolveEntityFromBaseType(string? baseType)
    {
        var raw = (baseType ?? string.Empty).Trim();
        return raw switch
        {
            "23" => "Quotations",
            "17" => "Orders",
            "15" => "DeliveryNotes",
            "13" => "Invoices",
            "14" => "CreditNotes",
            "16" => "Returns",
            _ when raw.Equals("Quotations", StringComparison.OrdinalIgnoreCase) => "Quotations",
            _ when raw.Equals("Orders", StringComparison.OrdinalIgnoreCase) => "Orders",
            _ when raw.Equals("DeliveryNotes", StringComparison.OrdinalIgnoreCase) => "DeliveryNotes",
            _ when raw.Equals("Invoices", StringComparison.OrdinalIgnoreCase) => "Invoices",
            _ => string.Empty
        };
    }
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
        var cacheKey = $"sap:doc-rate:{docCurrency.Trim().ToUpperInvariant()}:{date}";
        if (_cache.TryGetValue(cacheKey, out decimal cachedRate) && cachedRate > 0m)
            return (cachedRate, "cache");

        // Prefer ExchangeRates collection queries only.
        // This avoids Service Layer variants that require extra Date parameters
        // or expose unsupported resource paths on some SAP environments.
        var rateByDate = await _sapService.ServiceLayerGetAsync(
            $"ExchangeRates?$filter=Currency eq '{escapedCurrency}' and RateDate eq '{date}'&$top=1",
            cancellationToken);
        var parsedRateByDate = ExtractRate(rateByDate.Response);
        if (rateByDate.Success && parsedRateByDate.HasValue)         {             _cache.Set(cacheKey, parsedRateByDate.Value, TimeSpan.FromHours(6));             return (parsedRateByDate, "ExchangeRates(date)");         }

        var latestRateResult = await _sapService.ServiceLayerGetAsync(
            $"ExchangeRates?$filter=Currency eq '{escapedCurrency}'&$orderby=RateDate desc&$top=1",
            cancellationToken);
        var parsedLatestRate = ExtractRate(latestRateResult.Response);
        if (parsedLatestRate.HasValue)         {             _cache.Set(cacheKey, parsedLatestRate.Value, TimeSpan.FromHours(6));             return (parsedLatestRate, "ExchangeRates(latest)");         }

        var b1Function = await _sapService.ServiceLayerPostAsync(
            "SBOBobService_GetCurrencyRate",
            new
            {
                Currency = docCurrency,
                Date = date
            },
            cancellationToken);

        var b1Rate = ExtractRateFromObject(b1Function.Response);
        if (b1Function.Success && b1Rate.HasValue)         {             _cache.Set(cacheKey, b1Rate.Value, TimeSpan.FromHours(6));             return (b1Rate, "SBOBobService_GetCurrencyRate(yyyy-MM-dd)");         }

        var b1FunctionCompactDate = await _sapService.ServiceLayerPostAsync(
            "SBOBobService_GetCurrencyRate",
            new
            {
                Currency = docCurrency,
                Date = docDate.ToString("yyyyMMdd")
            },
            cancellationToken);

        b1Rate = ExtractRateFromObject(b1FunctionCompactDate.Response);
        if (b1FunctionCompactDate.Success && b1Rate.HasValue)         {             _cache.Set(cacheKey, b1Rate.Value, TimeSpan.FromHours(6));             return (b1Rate, "SBOBobService_GetCurrencyRate(yyyyMMdd)");         }

        var b1FunctionRateDate = await _sapService.ServiceLayerPostAsync(
            "SBOBobService_GetCurrencyRate",
            new
            {
                Currency = docCurrency,
                RateDate = date
            },
            cancellationToken);

        b1Rate = ExtractRateFromObject(b1FunctionRateDate.Response);
        if (b1FunctionRateDate.Success && b1Rate.HasValue)         {             _cache.Set(cacheKey, b1Rate.Value, TimeSpan.FromHours(6));             return (b1Rate, "SBOBobService_GetCurrencyRate(RateDate)");         }


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
                SalesPersonCode = GetInt(node, "SalesPersonCode"),
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

        var imageUrl = GetStringAny(node, "ImageUrl");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "ImageURL");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "PictureUrl");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "PhotoUrl");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "Picture");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "PicturName");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "U_ImageUrl");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "U_Image");
        if (string.IsNullOrWhiteSpace(imageUrl)) imageUrl = GetStringAny(node, "U_Photo");

        return new SapItemDto
        {
            ItemCode = itemCode,
            ItemName = GetString(node, "ItemName"),
            ImageUrl = NormalizeItemImageUrl(imageUrl),
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

    private static bool ShouldApplySalesScopeBySalesPerson(string tableName)
        => tableName.ToUpperInvariant() is "ORDR" or "OINV" or "OQUT" or "ODLN" or "ORIN" or "ORDN";

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
        var isCancelled = IsCancelled(source);
        data["IsCancelled"] = isCancelled;

        if (source.TryGetProperty("DocumentLines", out var linesProp) && linesProp.ValueKind == JsonValueKind.Array)
        {
            var normalizedLines = new List<Dictionary<string, object?>>();
            foreach (var line in linesProp.EnumerateArray())
            {
                var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(line.GetRawText())
                    ?? new Dictionary<string, object?>();

                if (isCancelled)
                {
                    row["LineStatus"] = "annuler";
                    row["Status"] = "annuler";
                }

                if (!row.ContainsKey("subtotalHt"))
                {
                    var lt = GetDecimalFromRow(row, "LineTotal", "lineTotal");
                    var vp = GetDecimalFromRow(row, "VatPercent", "vatPercent", "VatPrcnt", "vatPrcnt");
                    var vatAmt = vp > 0 ? Math.Round(lt * vp / 100m, 2) : GetDecimalFromRow(row, "VatAmount", "vatAmount");
                    row["subtotalHt"] = lt;
                    row["vatAmount"] = vatAmt;
                    row["totalTtc"] = lt + vatAmt;
                }

                normalizedLines.Add(row);
            }
            data["DocumentLines"] = normalizedLines;
        }

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

    private static string NormalizeItemImageUrl(string rawImageUrl)
    {
        if (string.IsNullOrWhiteSpace(rawImageUrl))
            return string.Empty;

        var value = rawImageUrl.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var normalized = value.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        // Nettoyage de caractères parasites possibles dans certains exports SAP
        fileName = fileName.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        return $"/api/sap/item-images/{Uri.EscapeDataString(fileName)}";
    }

    private string BuildItemImageUrl(string itemCode, string rawPictureName)
    {
        var safeItemCode = (itemCode ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(safeItemCode))
            return $"/api/sap/item-images/by-item/{Uri.EscapeDataString(safeItemCode)}";

        return NormalizeItemImageUrl(rawPictureName);
    }

    private async Task<int> ResolveDefaultPriceListAsync(CancellationToken cancellationToken)
    {
        if (int.TryParse(_configuration["SapB1:DefaultPriceList"], out var configured) && configured > 0)
            return configured;

        var configuredName = (_configuration["SapB1:DefaultPriceListName"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(configuredName))
            return 1;

        var cacheKey = $"sap:default-price-list:{configuredName.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out int cachedPriceList) && cachedPriceList > 0)
            return cachedPriceList;

        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
                return 1;

            await using (conn)
            await using (var cmd = new SqlCommand("SELECT TOP 1 ListNum FROM OPLN WHERE ListName = @name", conn))
            {
                cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                cmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 100) { Value = configuredName });
                var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                if (obj is not null && obj != DBNull.Value)
                {
                    var parsed = Convert.ToInt32(obj);
                    if (parsed > 0)
                    {
                        _cache.Set(cacheKey, parsed, TimeSpan.FromHours(6));
                        return parsed;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de résoudre DefaultPriceListName={Name}", configuredName);
        }

        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
                throw new InvalidOperationException("Connexion SQL SAP indisponible pour résoudre la PriceList.");

            await using (conn)
            await using (var cmd = new SqlCommand(@"
SELECT TOP 1 ListNum
FROM OPLN
WHERE ISNULL(ListName, '') <> ''
  AND ListName NOT LIKE '%Last Evaluated%'
  AND ListName NOT LIKE '%Last Purchase%'
ORDER BY ListName ASC, ListNum ASC", conn))
            {
                cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                if (obj is not null && obj != DBNull.Value)
                {
                    var parsed = Convert.ToInt32(obj);
                    if (parsed > 0)
                    {
                        _cache.Set(cacheKey, parsed, TimeSpan.FromHours(6));
                        return parsed;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible de résoudre une PriceList SAP par défaut.");
            throw;
        }

        throw new InvalidOperationException("Aucune PriceList SAP exploitable trouvée.");
    }

    private async Task<string> ResolvePictureNameByItemCodeAsync(string itemCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
            return string.Empty;

        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is null)
                return string.Empty;

            await using (conn)
            await using (var cmd = new SqlCommand("SELECT TOP 1 ISNULL(PicturName,'') FROM OITM WHERE ItemCode = @itemCode", conn))
            {
                cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                cmd.Parameters.Add(new SqlParameter("@itemCode", SqlDbType.NVarChar, 50) { Value = itemCode });
                var obj = await cmd.ExecuteScalarAsync(cancellationToken);
                return obj?.ToString()?.Trim() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetImageContentType(string imagePath)
    {
        var extension = Path.GetExtension(imagePath)?.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    private async Task<List<string>> GetItemPictureRootsAsync(CancellationToken cancellationToken)
    {
        var roots = new List<string>();
        var rawConfiguredRoots = _configuration["SapB1:AttachmentsPicturesPath"] ?? string.Empty;
        var configuredRoots = rawConfiguredRoots
            .Split([';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var configuredRoot in configuredRoots)
            roots.Add(configuredRoot);

        // Source SAP native pour le dossier images
        try
        {
            var conn = await OpenSapSqlConnectionAsync(cancellationToken);
            if (conn is not null)
            {
                await using (conn)
                await using (var cmd = new SqlCommand("SELECT TOP 1 BitmapPath FROM OADP WHERE ISNULL(BitmapPath,'') <> ''", conn))
                {
                    cmd.CommandTimeout = GetSapSqlCommandTimeoutSeconds();
                    var bitmapPath = (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(bitmapPath))
                    {
                        var normalized = bitmapPath.Trim('"', '\'').Replace('/', '\\');
                        var folder = Path.GetDirectoryName(normalized);
                        roots.Add(string.IsNullOrWhiteSpace(folder) ? normalized : folder);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lecture OADP.BitmapPath impossible.");
        }

        // Fallbacks utiles si la config n'est pas alignée avec le serveur SAP.
        roots.Add(@"C:\Images\");
        roots.Add(@"C:\Users\stg1\Pictures\");
        roots.Add(@"C:\Program Files (x86)\SAP\SAP Business One\Pictures\");
        roots.Add(@"C:\Program Files\SAP\SAP Business One\Pictures\");
        roots.Add(@"C:\SAP\Pictures\");

        return roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> BuildImageFileNameCandidates(string fileName)
    {
        yield return fileName;

        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(ext))
            yield break;

        yield return fileName + ".jpg";
        yield return fileName + ".jpeg";
        yield return fileName + ".png";
        yield return fileName + ".bmp";
        yield return fileName + ".gif";
        yield return fileName + ".webp";
    }

    private static string GuessPictureFileNameFromItemName(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return string.Empty;

        var compact = new string(itemName.Where(c => char.IsLetterOrDigit(c)).ToArray());
        if (string.IsNullOrWhiteSpace(compact))
            return string.Empty;

        return compact + ".jpg";
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

    private async Task<ActionResult<ApiResponse<object>>> CreateInvoiceWithWalletAsync(CreateSapDocumentRequest request, CancellationToken cancellationToken)
    {
        var created = await CreateCommercialDocumentAsync("Invoices", request, cancellationToken);
        if (created.Result is not ObjectResult objectResult)
            return created;

        if (objectResult.Value is not ApiResponse<object> createdResponse || createdResponse.Data is null)
            return created;

        try
        {
            var node = JsonSerializer.SerializeToElement(createdResponse.Data);
            var invoiceDocEntry = GetInt(node, "docEntry");
            if (invoiceDocEntry <= 0)
                invoiceDocEntry = GetInt(node, "DocEntry");
            var cardCode = request.CardCode?.Trim();
            if (invoiceDocEntry <= 0 || string.IsNullOrWhiteSpace(cardCode))
                return created;

            var walletBefore = await GetWalletBalanceAsync(cardCode, cancellationToken);
            if (walletBefore <= 0)
                return created;

            var invoiceResult = await _sapService.ServiceLayerGetAsync($"Invoices({invoiceDocEntry})", cancellationToken);
            if (!invoiceResult.Success || !invoiceResult.Response.HasValue)
                return created;

            var openAmount = ResolveOpenAmount(invoiceResult.Response.Value);
            var walletApplied = Math.Min(walletBefore, openAmount);
            if (walletApplied <= 0)
                return created;

            await ConsumeWalletCreditAsync(cardCode, walletApplied, cancellationToken);

            var payload = new
            {
                CardCode = cardCode,
                DocDate = DateTime.Today.ToString("yyyy-MM-dd"),
                CashSum = walletApplied,
                PaymentInvoices = new[]
                {
                    new
                    {
                        DocEntry = invoiceDocEntry,
                        SumApplied = walletApplied,
                        InvoiceType = "it_Invoice"
                    }
                }
            };

            var paymentResult = await _sapService.ServiceLayerPostAsync("IncomingPayments", payload, cancellationToken);
            if (!paymentResult.Success)
            {
                await AddWalletCreditAsync(cardCode, walletApplied, cancellationToken);
                return created;
            }

            var merged = new
            {
                created = createdResponse.Data,
                walletApplied,
                walletRemaining = await GetWalletBalanceAsync(cardCode, cancellationToken)
            };
            return StatusCode(objectResult.StatusCode ?? 200, new ApiResponse<object>(true, "Facture creee et solde client applique.", merged));
        }
        catch
        {
            return created;
        }
    }

    private async Task EnsureWalletTableAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
IF OBJECT_ID(N'dbo.CustomerWalletBalances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWalletBalances
    (
        CardCode NVARCHAR(50) NOT NULL PRIMARY KEY,
        Balance DECIMAL(19,4) NOT NULL CONSTRAINT DF_CustomerWalletBalances_Balance DEFAULT(0),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_CustomerWalletBalances_UpdatedAt DEFAULT(SYSUTCDATETIME())
    );
END";
        await _db.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<Dictionary<string, decimal>> GetWalletBalancesAsync(IEnumerable<string> cardCodes, CancellationToken cancellationToken)
    {
        await EnsureWalletTableAsync(cancellationToken);
        var normalized = cardCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.Count == 0) return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        await using var conn = OpenAppSqlConnection();
        await conn.OpenAsync(cancellationToken);
        var inSql = string.Join(",", normalized.Select((_, i) => $"@p{i}"));
        var sql = $"SELECT CardCode, Balance FROM dbo.CustomerWalletBalances WHERE CardCode IN ({inSql});";
        await using var cmd = new SqlCommand(sql, conn);
        for (var i = 0; i < normalized.Count; i++)
            cmd.Parameters.AddWithValue($"@p{i}", normalized[i]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader["CardCode"]?.ToString() ?? string.Empty] = reader["Balance"] is DBNull ? 0m : Convert.ToDecimal(reader["Balance"]);
        return result;
    }

    private async Task<decimal> GetWalletBalanceAsync(string cardCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode)) return 0m;
        var map = await GetWalletBalancesAsync(new[] { cardCode }, cancellationToken);
        return map.TryGetValue(cardCode.Trim(), out var balance) ? Math.Max(0m, balance) : 0m;
    }

    private async Task AddWalletCreditAsync(string cardCode, decimal amount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode) || amount <= 0) return;
        await EnsureWalletTableAsync(cancellationToken);
        await using var conn = OpenAppSqlConnection();
        await conn.OpenAsync(cancellationToken);
        const string sql = @"
MERGE dbo.CustomerWalletBalances AS target
USING (SELECT @cardCode AS CardCode) AS source
ON target.CardCode = source.CardCode
WHEN MATCHED THEN UPDATE SET Balance = Balance + @amount, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CardCode, Balance, UpdatedAt) VALUES (@cardCode, @amount, SYSUTCDATETIME());";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cardCode", cardCode.Trim());
        cmd.Parameters.AddWithValue("@amount", amount);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ConsumeWalletCreditAsync(string cardCode, decimal amount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode) || amount <= 0) return;
        await EnsureWalletTableAsync(cancellationToken);
        await using var conn = OpenAppSqlConnection();
        await conn.OpenAsync(cancellationToken);
        const string sql = @"
UPDATE dbo.CustomerWalletBalances
SET Balance = CASE WHEN Balance >= @amount THEN Balance - @amount ELSE 0 END,
    UpdatedAt = SYSUTCDATETIME()
WHERE CardCode = @cardCode;";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@cardCode", cardCode.Trim());
        cmd.Parameters.AddWithValue("@amount", amount);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqlConnection OpenAppSqlConnection()
    {
        var conn = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("DefaultConnection est manquante.");
        return new SqlConnection(conn);
    }

    private async Task CreateSapAdvanceOnAccountAsync(string cardCode, decimal amount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cardCode) || amount <= 0) return;

        var payload = new
        {
            CardCode = cardCode,
            DocDate = DateTime.Today.ToString("yyyy-MM-dd"),
            CashSum = amount
        };

        var result = await _sapService.ServiceLayerPostAsync("IncomingPayments", payload, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning("Echec creation avance SAP (on account). CardCode={CardCode}, Amount={Amount}, Error={Error}",
                cardCode, amount, result.ErrorMessage);
        }
    }

    private async Task<int> GetTransIdFromInvoiceAsync(int docEntry, CancellationToken cancellationToken)
    {
        var conn = await OpenSapSqlConnectionAsync(cancellationToken);
        if (conn == null) return 0;
        await using (conn)
        {
            var sql = "SELECT TransId FROM OINV WHERE DocEntry = @de";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@de", docEntry);
            var res = await cmd.ExecuteScalarAsync(cancellationToken);
            return res != null ? Convert.ToInt32(res) : 0;
        }
    }

    private static object SapError(string? error, JsonElement? sapResponse = null)
    {
        var msg = error ?? "Erreur inconnue";
        
        // TRADUCTION SYSTÉMATIQUE ET EXHAUSTIVE
        if (msg.Contains("Invalid session", StringComparison.OrdinalIgnoreCase)) msg = "Session SAP expirée. Veuillez vous reconnecter.";
        if (msg.Contains("Insufficient permission", StringComparison.OrdinalIgnoreCase)) msg = "Permissions SAP insuffisantes.";
        if (msg.Contains("Confirmation amount must be greater than zero", StringComparison.OrdinalIgnoreCase)) msg = "Le montant du paiement doit être supérieur à zéro. Veuillez saisir un montant cash.";
        if (msg.Contains("Account for bank transfer has not been defined", StringComparison.OrdinalIgnoreCase)) msg = "Configuration SAP manquante : Le compte de virement bancaire n'est pas défini.";
        if (msg.Contains("Internal error", StringComparison.OrdinalIgnoreCase)) msg = "Erreur interne SAP.";
        if (msg.Contains("Object not found", StringComparison.OrdinalIgnoreCase)) msg = "Objet SAP introuvable.";
        if (msg.Contains("Database connection failed", StringComparison.OrdinalIgnoreCase)) msg = "Échec de connexion à la base de données SAP.";
        if (msg.Contains("Already exists", StringComparison.OrdinalIgnoreCase)) msg = "Cet enregistrement existe d�j� dans SAP.";
        if (msg.Contains("No matching records found", StringComparison.OrdinalIgnoreCase)) msg = "Aucun enregistrement correspondant trouv� dans SAP (Erreur ODBC -2028).";
        if (msg.Contains("ODBC", StringComparison.OrdinalIgnoreCase)) msg = "Erreur de base de donn�es SAP (ODBC).";
        if (msg.Contains("Service Layer", StringComparison.OrdinalIgnoreCase)) msg = "Erreur de communication avec le Service Layer SAP.";

        return new { success = false, message = "Erreur SAP", error = msg, sapResponse };
    }
}
public class CreateSapClientRequest
{
    public string CardCode { get; set; } = string.Empty;
    public bool Automatic { get; set; }
    public int? Series { get; set; }
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
    public int? SalesPersonCode { get; set; }
}

public class SapSeriesDto
{
    public int Series { get; set; }
    public string SeriesName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int NextNumber { get; set; }
    public int LastNum { get; set; }
    public string Locked { get; set; } = "N";
    public bool IsDefault { get; set; }
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
    public bool UseAdvance { get; set; }
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
    public decimal AdvanceBalance { get; set; }
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
    public int SalesPersonCode { get; set; }
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
    public int GroupCode { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
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














