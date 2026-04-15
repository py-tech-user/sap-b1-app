namespace SapB1App.Models;

/// <summary>Type de partenaire : Prospect ou Client</summary>
public enum PartnerType
{
    Client,
    Prospect
}

/// <summary>Groupe de partenaire</summary>
public enum CustomerGroup
{
    Etranger,
    GroupeScolaire,
    LesParticuliersGP,
    LesRevendeurs,
    LesSallesDeSports,
    Locaux,
    OrganismePublic
}

/// <summary>Devise</summary>
public enum CurrencyType
{
    CHF,
    DKK,
    EUR,
    GBP,
    JPY,
    MAD,
    NOK,
    SEK,
    USD,
    ToutesDevises
}

public class Customer
{
    public int           Id                       { get; set; }
    public string        CardCode                 { get; set; } = string.Empty;   // Code
    public PartnerType   PartnerType              { get; set; } = PartnerType.Client; // Type
    public string        CardName                 { get; set; } = string.Empty;   // Nom
    public string?       ForeignName              { get; set; }                    // Nom étranger
    public CustomerGroup GroupCode                { get; set; } = CustomerGroup.Locaux; // Groupe
    public CurrencyType  Currency                 { get; set; } = CurrencyType.EUR;     // Devise
    public string?       FederalTaxId             { get; set; }                    // N° Identification entreprise

    // Champs supplémentaires (non affichés dans le formulaire principal)
    public string?       Phone                    { get; set; }
    public string?       Phone1                   { get; set; }
    public string?       Phone2                   { get; set; }
    public string?       MobilePhone              { get; set; }
    public string?       Email                    { get; set; }
    public string?       Contact                  { get; set; }
    public string?       AdditionalIdentificationNumber { get; set; }
    public string?       UnifiedTaxIdentificationNumber { get; set; }
    public string?       Location                 { get; set; }
    public string?       City                     { get; set; }
    public string?       Country                  { get; set; }
    public decimal?      CreditLimit              { get; set; }
    public int?          SapDocNum                { get; set; }   // DocEntry SAP B1
    public DateTime      CreatedAt                { get; set; } = DateTime.UtcNow;
    public DateTime?     UpdatedAt                { get; set; }

    // Navigation
    public ICollection<Order>        Orders        { get; set; } = new List<Order>();
    public ICollection<Quote>        Quotes        { get; set; } = new List<Quote>();
    public ICollection<DeliveryNote> DeliveryNotes { get; set; } = new List<DeliveryNote>();
    public ICollection<Invoice>      Invoices      { get; set; } = new List<Invoice>();
    public ICollection<Return>       Returns       { get; set; } = new List<Return>();
}
