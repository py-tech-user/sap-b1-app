namespace SapB1App.Interfaces;

/// <summary>
/// Contrat pour l'intÃ©gration SAP Business One via Service Layer REST API.
/// </summary>
public interface ISapB1Service
{
    /// <summary>Synchronise un client vers SAP B1 (BusinessPartner).</summary>
    Task<bool> SyncCustomerAsync(int customerId);

    /// <summary>Synchronise une commande vers SAP B1 (Sales Order).</summary>
    Task<bool> SyncOrderAsync(int orderId);

    /// <summary>Teste la connexion au Service Layer SAP B1.</summary>
    Task<bool> TestConnectionAsync();
}
