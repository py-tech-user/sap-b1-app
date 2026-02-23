using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.Interfaces;

namespace SapB1App.Services;

/// <summary>
/// Couche d'intégration SAP Business One via Service Layer REST API.
/// Les méthodes PostAsync sont préparées — remplacez le commentaire
/// par l'appel réel une fois l'URL Service Layer configurée.
/// </summary>
public class SapB1Service : ISapB1Service
{
    private readonly HttpClient    _http;
    private readonly IConfiguration _config;
    private readonly AppDbContext  _db;
    private readonly ILogger<SapB1Service> _logger;

    // SAP B1 session cookie (valide ~30 min)
    private string? _sessionId;

    public SapB1Service(
        HttpClient http,
        IConfiguration config,
        AppDbContext db,
        ILogger<SapB1Service> logger)
    {
        _http   = http;
        _config = config;
        _db     = db;
        _logger = logger;

        // Désactiver la vérification SSL pour environnements de dev SAP
        // (en prod, utilisez un certificat valide)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
    }

    // ── Login SAP B1 Service Layer ───────────────────────────────────────────
    private async Task<bool> LoginAsync()
    {
        try
        {
            var baseUrl   = _config["SapB1:ServiceLayerUrl"];
            var companyDB = _config["SapB1:CompanyDB"];
            var userName  = _config["SapB1:UserName"];
            var password  = _config["SapB1:Password"];

            var payload = JsonSerializer.Serialize(new
            {
                CompanyDB = companyDB,
                UserName  = userName,
                Password  = password
            });

            var response = await _http.PostAsync(
                $"{baseUrl}/Login",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SAP B1 Login failed: {Status}", response.StatusCode);
                return false;
            }

            // Récupérer le cookie de session SAP B1
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                _sessionId = cookies.FirstOrDefault();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SAP B1 Login error");
            return false;
        }
    }

    // ── Sync Customer → SAP B1 BusinessPartner ──────────────────────────────
    public async Task<bool> SyncCustomerAsync(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null) return false;

        _logger.LogInformation(
            "Syncing customer {CardCode} to SAP B1...", customer.CardCode);

        /* ── Payload SAP B1 ─────────────────────────────────────────────────
         * Décommentez et adaptez quand votre Service Layer est disponible :
         *
         * var loggedIn = await LoginAsync();
         * if (!loggedIn) return false;
         *
         * var payload = JsonSerializer.Serialize(new {
         *     CardCode     = customer.CardCode,
         *     CardName     = customer.CardName,
         *     CardType     = "cCustomer",
         *     Phone1       = customer.Phone,
         *     EmailAddress = customer.Email,
         *     Currency     = customer.Currency,
         *     BPAddresses  = new[] { new {
         *         AddressName = "Facturation",
         *         Street      = customer.Address,
         *         City        = customer.City,
         *         Country     = customer.Country
         *     }}
         * });
         *
         * var request = new HttpRequestMessage(HttpMethod.Post,
         *     $"{_config["SapB1:ServiceLayerUrl"]}/BusinessPartners");
         * request.Headers.Add("Cookie", _sessionId);
         * request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
         *
         * var response = await _http.SendAsync(request);
         * return response.IsSuccessStatusCode;
         * ──────────────────────────────────────────────────────────────────── */

        // Stub : retourne true tant que le Service Layer n'est pas configuré
        await Task.Delay(100); // simule latence réseau
        return true;
    }

    // ── Sync Order → SAP B1 Sales Order ─────────────────────────────────────
    public async Task<bool> SyncOrderAsync(int orderId)
    {
        var order = await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null) return false;

        _logger.LogInformation(
            "Syncing order {DocNum} to SAP B1...", order.DocNum);

        /* ── Payload SAP B1 Sales Order ────────────────────────────────────
         * var sapOrder = new {
         *     CardCode      = order.Customer.CardCode,
         *     DocDate       = order.DocDate.ToString("yyyy-MM-dd"),
         *     DocDueDate    = order.DeliveryDate?.ToString("yyyy-MM-dd"),
         *     Comments      = order.Comments,
         *     DocumentLines = order.Lines.Select(l => new {
         *         ItemCode  = l.Product.ItemCode,
         *         Quantity  = (double)l.Quantity,
         *         UnitPrice = (double)l.UnitPrice,
         *         TaxCode   = "TVA20"
         *     }).ToArray()
         * };
         * ──────────────────────────────────────────────────────────────────── */

        await Task.Delay(100);
        return true;
    }

    // ── Test Connection ──────────────────────────────────────────────────────
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var baseUrl  = _config["SapB1:ServiceLayerUrl"];
            var response = await _http.GetAsync($"{baseUrl}/CompanyService/GetAdminInfo");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SAP B1 connection test failed");
            return false;
        }
    }
}
