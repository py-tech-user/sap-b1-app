using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

/// <summary>
/// Couche d'intégration SAP Business One via DI API (SAPbobsCOM.dll).
/// 
/// ⚠️ IMPORTANT: Pour utiliser la DI API réelle, vous devez:
/// 1. Copier SAPbobsCOM.dll depuis "C:\Program Files (x86)\SAP\SAP Business One DI API\" vers le dossier "Lib\"
/// 2. Décommenter le code avec #if SAP_DI_API et compiler avec la constante SAP_DI_API définie
/// 
/// Cette version utilise un mode MOCK pour le développement sans SAP.
/// </summary>
public class SapB1Service : ISapB1Service
{
    private static readonly SemaphoreSlim SessionLock = new(1, 1);
    private static string? _cachedSessionId;
    private static DateTime _cachedSessionExpiresAtUtc = DateTime.MinValue;

    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<SapB1Service> _logger;

    private bool _mockConnected;
    private bool _disposed;

    public bool IsConnected => _mockConnected;

    public SapB1Service(
        IConfiguration config,
        AppDbContext db,
        ILogger<SapB1Service> logger)
    {
        _config = config;
        _db = db;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MODE MOCK (Développement sans SAP B1 DI API)
    // ══════════════════════════════════════════════════════════════════════════

    public bool Connect()
    {
        _logger.LogWarning("⚠️ SAP B1 DI API - MODE MOCK ACTIVÉ (pas de connexion réelle)");
        _logger.LogInformation("Config SAP: Server={Server}, DB={CompanyDB}, User={UserName}",
            _config["SapB1:Server"],
            _config["SapB1:CompanyDB"],
            _config["SapB1:UserName"]);

        // Simuler une connexion réussie
        _mockConnected = true;
        _logger.LogInformation("✅ MOCK: Connexion SAP B1 simulée avec succès");
        return true;
    }

    public void Disconnect()
    {
        _mockConnected = false;
        _logger.LogInformation("MOCK: Déconnexion SAP B1 simulée");
    }

    public (bool Success, string? ErrorMessage) CreateBusinessPartner(string cardCode, string cardName)
    {
        _logger.LogWarning("⚠️ MODE MOCK: Création BusinessPartner simulée");

        if (!_mockConnected)
        {
            Connect();
        }

        // Simuler la création
        _logger.LogInformation("✅ MOCK: BusinessPartner {CardCode} - {CardName} créé (simulation)",
            cardCode, cardName);

        return (true, null);

        /* ══════════════════════════════════════════════════════════════════════
         * CODE RÉEL AVEC SAP DI API (Décommentez quand SAPbobsCOM.dll est disponible)
         * ══════════════════════════════════════════════════════════════════════
         * 
         * try
         * {
         *     if (!IsConnected && !Connect())
         *     {
         *         return (false, "Impossible de se connecter à SAP B1");
         *     }
         * 
         *     var bp = (SAPbobsCOM.BusinessPartners)_company!.GetBusinessObject(
         *         SAPbobsCOM.BoObjectTypes.oBusinessPartners);
         * 
         *     bp.CardCode = cardCode;
         *     bp.CardName = cardName;
         *     bp.CardType = SAPbobsCOM.BoCardTypes.cCustomer;
         *     bp.Currency = "EUR";
         * 
         *     int result = bp.Add();
         *     System.Runtime.InteropServices.Marshal.ReleaseComObject(bp);
         * 
         *     if (result != 0)
         *     {
         *         _company.GetLastError(out int errCode, out string errMsg);
         *         return (false, $"[{errCode}] {errMsg}");
         *     }
         * 
         *     return (true, null);
         * }
         * catch (System.Runtime.InteropServices.COMException ex)
         * {
         *     return (false, $"Erreur COM: {ex.Message}");
         * }
         * ══════════════════════════════════════════════════════════════════════ */
    }

    public async Task<bool> SyncCustomerAsync(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return false;

        _logger.LogInformation("MOCK: Syncing customer {CardCode} to SAP B1...", customer.CardCode);

        var (success, _) = CreateBusinessPartner(customer.CardCode, customer.CardName);
        return success;
    }

    public async Task<bool> SyncOrderAsync(int orderId)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null) return false;

        _logger.LogInformation("MOCK: Syncing order {DocNum} to SAP B1...", order.DocNum);

        // Simulation
        await Task.Delay(100);
        return true;
    }

    public Task<bool> TestConnectionAsync()
    {
        var connected = Connect();
        return Task.FromResult(connected);
    }

    public async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> LoginServiceLayerAsync(CancellationToken cancellationToken = default)
    {
        var login = await EnsureServiceLayerSessionAsync(forceRefresh: true, cancellationToken);
        return (login.Success, login.Response, login.StatusCode, login.ErrorMessage);
    }

    public async Task<(bool Success, string? SessionId, JsonElement? Response, int StatusCode, string? ErrorMessage)> LoginServiceLayerWithSessionIdAsync(CancellationToken cancellationToken = default)
    {
        var login = await EnsureServiceLayerSessionAsync(forceRefresh: true, cancellationToken);
        return (login.Success, _cachedSessionId, login.Response, login.StatusCode, login.ErrorMessage);
    }

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerGetAsync(
        string relativeUrl, CancellationToken cancellationToken = default)
        => ExecuteServiceLayerRequestAsync(HttpMethod.Get, relativeUrl, null, cancellationToken);

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerPostAsync(
        string relativeUrl, object? payload, CancellationToken cancellationToken = default)
        => ExecuteServiceLayerRequestAsync(HttpMethod.Post, relativeUrl, payload, cancellationToken);

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerPatchAsync(
        string relativeUrl, object? payload, CancellationToken cancellationToken = default)
        => ExecuteServiceLayerRequestAsync(HttpMethod.Patch, relativeUrl, payload, cancellationToken);

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerDeleteAsync(
        string relativeUrl, CancellationToken cancellationToken = default)
        => ExecuteServiceLayerRequestAsync(HttpMethod.Delete, relativeUrl, null, cancellationToken);

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateBusinessPartnerAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var cardType = customer.PartnerType == PartnerType.Prospect ? "cLead" : "cCustomer";

        var payload = new Dictionary<string, object?>
        {
            ["CardCode"] = customer.CardCode,
            ["CardName"] = customer.CardName,
            ["CardType"] = cardType,
            ["Currency"] = customer.Currency == CurrencyType.ToutesDevises ? null : customer.Currency.ToString(),
            ["FederalTaxID"] = customer.FederalTaxId,
            ["ForeignName"] = customer.ForeignName
        };

        return SendServiceLayerRequestAsync(HttpMethod.Post, "BusinessPartners", payload, cancellationToken);
    }

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateSalesOrderAsync(Order order, Customer customer, IEnumerable<OrderLine> lines, CancellationToken cancellationToken = default)
    {
        var documentLines = lines.Select(line =>
        {
            var itemCode = line.Product?.ItemCode;
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                throw new InvalidOperationException($"ItemCode manquant pour le produit ID {line.ProductId}.");
            }

            return new
            {
                ItemCode = itemCode,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice
            };
        }).ToList();

        var payload = new
        {
            CardCode = customer.CardCode,
            DocDate = order.DocDate,
            DocDueDate = order.DeliveryDate,
            DocCurrency = order.Currency,
            Comments = order.Comments,
            DocumentLines = documentLines
        };

        return SendServiceLayerRequestAsync(HttpMethod.Post, "Orders", payload, cancellationToken);
    }

    public Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateTestCustomerAsync(CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            CardCode = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CardName = "Client Test Service Layer",
            PartnerType = PartnerType.Client,
            Currency = CurrencyType.EUR
        };

        return CreateBusinessPartnerAsync(customer, cancellationToken);
    }

    public async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateTestOrderAsync(CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            CardCode = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CardName = "Client Test Service Layer",
            PartnerType = PartnerType.Client,
            Currency = CurrencyType.EUR
        };

        var customerResult = await CreateBusinessPartnerAsync(customer, cancellationToken);
        if (!customerResult.Success)
        {
            return customerResult;
        }

        var product = await _db.Products.AsNoTracking().OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return (false, null, (int)HttpStatusCode.BadRequest, "Aucun produit disponible pour créer une commande test.");
        }

        var order = new Order
        {
            DocNum = $"TEST-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DocDate = DateTime.UtcNow,
            DeliveryDate = DateTime.UtcNow.AddDays(7),
            Currency = "EUR",
            Comments = "Commande test Service Layer"
        };

        var line = new OrderLine
        {
            ProductId = product.Id,
            LineNum = 1,
            Quantity = 1,
            UnitPrice = product.Price > 0 ? product.Price : 1,
            VatPct = 0,
            LineTotal = product.Price > 0 ? product.Price : 1,
            Product = product
        };

        order.Lines.Add(line);
        order.DocTotal = line.LineTotal;
        order.VatTotal = 0;

        return await CreateSalesOrderAsync(order, customer, order.Lines, cancellationToken);
    }

    public async Task<(bool Success, JsonElement? LoginResponse, JsonElement? CustomerResponse, JsonElement? OrderResponse, int StatusCode, string? ErrorMessage)> RunFullServiceLayerTestAsync(CancellationToken cancellationToken = default)
    {
        var (serviceLayerUrl, companyDb, userName, password, ignoreSslErrors, configError) = GetServiceLayerSettings();
        if (configError is not null)
        {
            return (false, null, null, null, (int)HttpStatusCode.BadRequest, configError);
        }

        var cookieContainer = new CookieContainer();
        using var httpClient = CreateServiceLayerClient(serviceLayerUrl!, cookieContainer, ignoreSslErrors);

        var loginResult = await ExecuteLoginAsync(httpClient, companyDb!, userName!, password!, cancellationToken);
        if (!loginResult.Success)
        {
            return (false, loginResult.Response, null, null, loginResult.StatusCode, loginResult.ErrorMessage);
        }

        var customerPayload = new
        {
            CardCode = "TEST001",
            CardName = "Test Client",
            CardType = "C"
        };

        var customerResult = await SendServiceLayerRequestWithClientAsync(httpClient, HttpMethod.Post, "BusinessPartners", customerPayload, cancellationToken);
        if (!customerResult.Success)
        {
            return (false, loginResult.Response, customerResult.Response, null, customerResult.StatusCode, customerResult.ErrorMessage);
        }

        var orderPayload = new
        {
            CardCode = "TEST001",
            DocDate = DateTime.UtcNow,
            DocDueDate = DateTime.UtcNow.AddDays(7),
            DocumentLines = new[]
            {
                new
                {
                    ItemCode = "A00001",
                    Quantity = 1
                }
            }
        };

        var orderResult = await SendServiceLayerRequestWithClientAsync(httpClient, HttpMethod.Post, "Orders", orderPayload, cancellationToken);
        if (!orderResult.Success)
        {
            return (false, loginResult.Response, customerResult.Response, orderResult.Response, orderResult.StatusCode, orderResult.ErrorMessage);
        }

        return (true, loginResult.Response, customerResult.Response, orderResult.Response, (int)HttpStatusCode.OK, null);
    }

    private (string? ServiceLayerUrl, string? CompanyDb, string? UserName, string? Password, bool IgnoreSslErrors, string? Error) GetServiceLayerSettings()
    {
        var serviceLayerUrl = _config["SapB1ServiceLayer:ServiceLayerUrl"];
        var companyDb = _config["SapB1ServiceLayer:CompanyDB"];
        var userName = _config["SapB1ServiceLayer:SAPUser"];
        var password = _config["SapB1ServiceLayer:SAPPassword"];
        var ignoreSslErrors = bool.TryParse(_config["SapB1ServiceLayer:IgnoreSslErrors"], out var ignoreSsl) && ignoreSsl;

        if (string.IsNullOrWhiteSpace(serviceLayerUrl) ||
            string.IsNullOrWhiteSpace(companyDb) ||
            string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(password))
        {
            return (null, null, null, null, ignoreSslErrors, "Configuration Service Layer incomplète.");
        }

        return (serviceLayerUrl.TrimEnd('/'), companyDb, userName, password, ignoreSslErrors, null);
    }

    private HttpClient CreateServiceLayerClient(string serviceLayerUrl, CookieContainer cookieContainer, bool ignoreSslErrors)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            UseCookies = true
        };

        if (ignoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(serviceLayerUrl.EndsWith("/", StringComparison.Ordinal) ? serviceLayerUrl : serviceLayerUrl + "/")
        };

        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return httpClient;
    }

    private async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ExecuteLoginAsync(HttpClient httpClient, string companyDb, string userName, string password, CancellationToken cancellationToken)
    {
        var payload = new
        {
            CompanyDB = companyDb,
            UserName = userName,
            Password = password
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.PostAsync("Login", content, cancellationToken);
            return await ParseServiceLayerResponseAsync(response, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Service Layer login timeout");
            return (false, null, (int)HttpStatusCode.RequestTimeout, "Timeout lors de la connexion au Service Layer.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Layer login failed");
            return (false, null, (int)HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> SendServiceLayerRequestWithClientAsync(HttpClient httpClient, HttpMethod method, string relativeUrl, object? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (payload is not null)
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ParseServiceLayerResponseAsync(response, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Service Layer request timeout");
            return (false, null, (int)HttpStatusCode.RequestTimeout, "Timeout lors de l'appel Service Layer.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Layer request failed");
            return (false, null, (int)HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> SendServiceLayerRequestAsync(HttpMethod method, string relativeUrl, object? payload, CancellationToken cancellationToken)
        => ExecuteServiceLayerRequestAsync(method, relativeUrl, payload, cancellationToken);

    private async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ExecuteServiceLayerRequestAsync(
        HttpMethod method,
        string relativeUrl,
        object? payload,
        CancellationToken cancellationToken)
    {
        var (serviceLayerUrl, _, _, _, ignoreSslErrors, configError) = GetServiceLayerSettings();
        if (configError is not null)
        {
            return (false, null, (int)HttpStatusCode.BadRequest, configError);
        }

        var sessionResult = await EnsureServiceLayerSessionAsync(forceRefresh: false, cancellationToken);
        if (!sessionResult.Success || string.IsNullOrWhiteSpace(_cachedSessionId))
        {
            return (false, sessionResult.Response, sessionResult.StatusCode, sessionResult.ErrorMessage ?? "Session Service Layer indisponible.");
        }

        var response = await SendWithSessionAsync(
            serviceLayerUrl!, ignoreSslErrors, method, relativeUrl, payload, _cachedSessionId, cancellationToken);

        if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
        {
            var relogin = await EnsureServiceLayerSessionAsync(forceRefresh: true, cancellationToken);
            if (!relogin.Success || string.IsNullOrWhiteSpace(_cachedSessionId))
            {
                return (false, relogin.Response, relogin.StatusCode, relogin.ErrorMessage ?? "Reconnexion Service Layer échouée.");
            }

            response = await SendWithSessionAsync(
                serviceLayerUrl!, ignoreSslErrors, method, relativeUrl, payload, _cachedSessionId, cancellationToken);
        }

        return response;
    }

    private async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> EnsureServiceLayerSessionAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(_cachedSessionId) &&
            DateTime.UtcNow < _cachedSessionExpiresAtUtc)
        {
            return (true, null, (int)HttpStatusCode.OK, null);
        }

        await SessionLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh &&
                !string.IsNullOrWhiteSpace(_cachedSessionId) &&
                DateTime.UtcNow < _cachedSessionExpiresAtUtc)
            {
                return (true, null, (int)HttpStatusCode.OK, null);
            }

            var (serviceLayerUrl, companyDb, userName, password, ignoreSslErrors, configError) = GetServiceLayerSettings();
            if (configError is not null)
            {
                return (false, null, (int)HttpStatusCode.BadRequest, configError);
            }

            var cookieContainer = new CookieContainer();
            using var httpClient = CreateServiceLayerClient(serviceLayerUrl!, cookieContainer, ignoreSslErrors);
            var loginResult = await ExecuteLoginAsync(httpClient, companyDb!, userName!, password!, cancellationToken);
            if (!loginResult.Success || loginResult.Response is null)
            {
                return loginResult;
            }

            if (loginResult.Response.Value.ValueKind != JsonValueKind.Object ||
                !loginResult.Response.Value.TryGetProperty("SessionId", out var sessionIdProp))
            {
                return (false, loginResult.Response, (int)HttpStatusCode.InternalServerError, "SessionId introuvable dans la réponse Service Layer.");
            }

            var sessionId = sessionIdProp.GetString();
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return (false, loginResult.Response, (int)HttpStatusCode.InternalServerError, "SessionId vide dans la réponse Service Layer.");
            }

            var timeoutMinutes = 30;
            if (loginResult.Response.Value.TryGetProperty("SessionTimeout", out var timeoutProp) &&
                timeoutProp.TryGetInt32(out var parsedTimeout) &&
                parsedTimeout > 0)
            {
                timeoutMinutes = parsedTimeout;
            }

            _cachedSessionId = sessionId;
            _cachedSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, timeoutMinutes - 1));

            return (true, loginResult.Response, loginResult.StatusCode, null);
        }
        finally
        {
            SessionLock.Release();
        }
    }

    private async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> SendWithSessionAsync(
        string serviceLayerUrl,
        bool ignoreSslErrors,
        HttpMethod method,
        string relativeUrl,
        object? payload,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var cookieContainer = new CookieContainer();
        using var httpClient = CreateServiceLayerClient(serviceLayerUrl, cookieContainer, ignoreSslErrors);

        using var request = new HttpRequestMessage(method, relativeUrl.TrimStart('/'));
        request.Headers.Add("Cookie", $"B1SESSION={sessionId}");

        if (payload is not null)
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ParseServiceLayerResponseAsync(response, cancellationToken);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Service Layer request timeout");
            return (false, null, (int)HttpStatusCode.RequestTimeout, "Timeout lors de l'appel Service Layer.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service Layer request failed");
            return (false, null, (int)HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private async Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ParseServiceLayerResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonElement? responseBody = null;
        if (!string.IsNullOrWhiteSpace(responseJson))
        {
            using var doc = JsonDocument.Parse(responseJson);
            responseBody = doc.RootElement.Clone();
        }

        if (response.IsSuccessStatusCode)
        {
            return (true, responseBody, (int)response.StatusCode, null);
        }

        string? error = response.ReasonPhrase;
        if (responseBody is { ValueKind: JsonValueKind.Object } body &&
            body.TryGetProperty("error", out var errorNode) &&
            errorNode.ValueKind == JsonValueKind.Object &&
            errorNode.TryGetProperty("message", out var messageNode) &&
            messageNode.ValueKind == JsonValueKind.Object &&
            messageNode.TryGetProperty("value", out var valueNode) &&
            valueNode.ValueKind == JsonValueKind.String)
        {
            error = valueNode.GetString();
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            error = "Erreur Service Layer";
        }

        _logger.LogError("Service Layer HTTP {StatusCode}: {Error}. Response: {Response}",
            (int)response.StatusCode,
            error,
            string.IsNullOrWhiteSpace(responseJson) ? "<empty>" : responseJson);

        return (false, responseBody, (int)response.StatusCode, error);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disconnect();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~SapB1Service()
    {
        Dispose();
    }
}
