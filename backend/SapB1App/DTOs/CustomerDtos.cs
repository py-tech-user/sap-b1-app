using System.ComponentModel.DataAnnotations;
using SapB1App.Models;

namespace SapB1App.DTOs;

public class CustomerDto
{
    public int      Id              { get; set; }
    public string   CardCode        { get; set; } = string.Empty;      // Code
    public string   PartnerType     { get; set; } = "Client";          // Type
    public string   CardName        { get; set; } = string.Empty;      // Nom
    public string?  ForeignName     { get; set; }                      // Nom étranger
    public string   GroupCode       { get; set; } = "Locaux";          // Groupe
    public string   Currency        { get; set; } = "EUR";             // Devise
    public string?  FederalTaxId    { get; set; }                      // N° Identification entreprise
    [MaxLength(20)]
    public string?  Phone           { get; set; }
    [MaxLength(20)]
    public string?  Phone1          { get; set; }
    [MaxLength(20)]
    public string?  Phone2          { get; set; }
    [MaxLength(20)]
    public string?  MobilePhone     { get; set; }
    [EmailAddress]
    [MaxLength(100)]
    public string?  Email           { get; set; }
    [MaxLength(100)]
    public string?  Contact         { get; set; }
    [MaxLength(50)]
    public string?  AdditionalIdentificationNumber { get; set; }
    [MaxLength(50)]
    public string?  UnifiedTaxIdentificationNumber { get; set; }
    public bool     SyncedToSap     { get; set; }
    public DateTime CreatedAt       { get; set; }
    public int      OrderCount      { get; set; }
}

public class CreateCustomerDto
{
    public string   CardCode        { get; set; } = string.Empty;      // Code
    public string   PartnerType     { get; set; } = "Client";          // Type
    public string   CardName        { get; set; } = string.Empty;      // Nom
    public string?  ForeignName     { get; set; }                      // Nom étranger
    public string   GroupCode       { get; set; } = "Locaux";          // Groupe
    public string   Currency        { get; set; } = "EUR";             // Devise
    public string?  FederalTaxId    { get; set; }                      // N° Identification entreprise
    [MaxLength(20)]
    public string?  Phone           { get; set; }
    [MaxLength(20)]
    public string?  Phone1          { get; set; }
    [MaxLength(20)]
    public string?  Phone2          { get; set; }
    [MaxLength(20)]
    public string?  MobilePhone     { get; set; }
    [EmailAddress]
    [MaxLength(100)]
    public string?  Email           { get; set; }
    [MaxLength(100)]
    public string?  Contact         { get; set; }
    [MaxLength(50)]
    public string?  AdditionalIdentificationNumber { get; set; }
    [MaxLength(50)]
    public string?  UnifiedTaxIdentificationNumber { get; set; }
}

public class UpdateCustomerDto
{
    public string   CardCode        { get; set; } = string.Empty;      // Code (lecture seule mais transmis)
    public string   PartnerType     { get; set; } = "Client";          // Type
    public string   CardName        { get; set; } = string.Empty;      // Nom
    public string?  ForeignName     { get; set; }                      // Nom étranger
    public string   GroupCode       { get; set; } = "Locaux";          // Groupe
    public string   Currency        { get; set; } = "EUR";             // Devise
    public string?  FederalTaxId    { get; set; }                      // N° Identification entreprise
    [MaxLength(20)]
    public string?  Phone           { get; set; }
    [MaxLength(20)]
    public string?  Phone1          { get; set; }
    [MaxLength(20)]
    public string?  Phone2          { get; set; }
    [MaxLength(20)]
    public string?  MobilePhone     { get; set; }
    [EmailAddress]
    [MaxLength(100)]
    public string?  Email           { get; set; }
    [MaxLength(100)]
    public string?  Contact         { get; set; }
    [MaxLength(50)]
    public string?  AdditionalIdentificationNumber { get; set; }
    [MaxLength(50)]
    public string?  UnifiedTaxIdentificationNumber { get; set; }
}

/// <summary>Options pour les listes déroulantes</summary>
public class CustomerOptionsDto
{
    public List<OptionDto> PartnerTypes { get; set; } = new();
    public List<OptionDto> Groups       { get; set; } = new();
    public List<OptionDto> Currencies   { get; set; } = new();
}

public class OptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
