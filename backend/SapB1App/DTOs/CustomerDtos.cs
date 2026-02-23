namespace SapB1App.DTOs;

public class CustomerDto
{
    public int      Id          { get; set; }
    public string   CardCode    { get; set; } = string.Empty;
    public string   CardName    { get; set; } = string.Empty;
    public string?  Phone       { get; set; }
    public string?  Email       { get; set; }
    public string?  Address     { get; set; }
    public string?  City        { get; set; }
    public string?  Country     { get; set; }
    public string   Currency    { get; set; } = "EUR";
    public decimal? CreditLimit { get; set; }
    public bool     IsActive    { get; set; }
    public bool     SyncedToSap { get; set; }
    public DateTime CreatedAt   { get; set; }
    public int      OrderCount  { get; set; }
}

public class CreateCustomerDto
{
    public string   CardCode    { get; set; } = string.Empty;
    public string   CardName    { get; set; } = string.Empty;
    public string?  Phone       { get; set; }
    public string?  Email       { get; set; }
    public string?  Address     { get; set; }
    public string?  City        { get; set; }
    public string?  Country     { get; set; }
    public string   Currency    { get; set; } = "EUR";
    public decimal? CreditLimit { get; set; }
}

public class UpdateCustomerDto
{
    public string   CardName    { get; set; } = string.Empty;
    public string?  Phone       { get; set; }
    public string?  Email       { get; set; }
    public string?  Address     { get; set; }
    public string?  City        { get; set; }
    public string?  Country     { get; set; }
    public decimal? CreditLimit { get; set; }
}
