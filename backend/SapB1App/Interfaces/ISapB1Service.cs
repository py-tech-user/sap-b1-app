namespace SapB1App.Interfaces;

using System.Text.Json;
using SapB1App.Models;

/// <summary>
/// Contrat pour l'intégration SAP Business One via DI API (SAPbobsCOM.dll).
/// </summary>
public interface ISapB1Service : IDisposable
{
    /// <summary>Connecte à SAP Business One via DI API.</summary>
    bool Connect();

    /// <summary>Déconnecte de SAP Business One.</summary>
    void Disconnect();

    /// <summary>Vérifie si la connexion SAP est active.</summary>
    bool IsConnected { get; }

    /// <summary>Crée un BusinessPartner (client) dans SAP B1.</summary>
    /// <param name="cardCode">Code du partenaire</param>
    /// <param name="cardName">Nom du partenaire</param>
    /// <returns>Tuple (success, errorMessage)</returns>
    (bool Success, string? ErrorMessage) CreateBusinessPartner(string cardCode, string cardName);

    /// <summary>Synchronise un client vers SAP B1 (BusinessPartner).</summary>
    Task<bool> SyncCustomerAsync(int customerId);

    /// <summary>Synchronise une commande vers SAP B1 (Sales Order).</summary>
    Task<bool> SyncOrderAsync(int orderId);

    /// <summary>Teste la connexion au Service Layer SAP B1.</summary>
    Task<bool> TestConnectionAsync();

    /// <summary>Se connecte au Service Layer SAP B1 et retourne la réponse JSON complète.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> LoginServiceLayerAsync(CancellationToken cancellationToken = default);

    /// <summary>Se connecte au Service Layer et retourne le SessionId.</summary>
    Task<(bool Success, string? SessionId, JsonElement? Response, int StatusCode, string? ErrorMessage)> LoginServiceLayerWithSessionIdAsync(CancellationToken cancellationToken = default);

    /// <summary>Crée un Business Partner via le Service Layer.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateBusinessPartnerAsync(Customer customer, CancellationToken cancellationToken = default);

    /// <summary>Crée une commande client via le Service Layer.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateSalesOrderAsync(Order order, Customer customer, IEnumerable<OrderLine> lines, CancellationToken cancellationToken = default);

    /// <summary>Crée un client fictif via le Service Layer.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateTestCustomerAsync(CancellationToken cancellationToken = default);

    /// <summary>Crée une commande fictive via le Service Layer.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> CreateTestOrderAsync(CancellationToken cancellationToken = default);

    /// <summary>Teste le Service Layer (login + client + commande) et retourne les réponses JSON.</summary>
    Task<(bool Success, JsonElement? LoginResponse, JsonElement? CustomerResponse, JsonElement? OrderResponse, int StatusCode, string? ErrorMessage)> RunFullServiceLayerTestAsync(CancellationToken cancellationToken = default);

    /// <summary>GET générique vers SAP Service Layer avec session automatique.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerGetAsync(
        string relativeUrl, CancellationToken cancellationToken = default);

    /// <summary>POST générique vers SAP Service Layer avec session automatique.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerPostAsync(
        string relativeUrl, object? payload, CancellationToken cancellationToken = default);

    /// <summary>PATCH générique vers SAP Service Layer avec session automatique.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerPatchAsync(
        string relativeUrl, object? payload, CancellationToken cancellationToken = default);

    /// <summary>DELETE générique vers SAP Service Layer avec session automatique.</summary>
    Task<(bool Success, JsonElement? Response, int StatusCode, string? ErrorMessage)> ServiceLayerDeleteAsync(
        string relativeUrl, CancellationToken cancellationToken = default);
}
