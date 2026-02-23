namespace SapB1App.Models;

public class Customer
{
    public int      Id          { get; set; }
    public string   CardCode    { get; set; } = string.Empty;   // Code SAP B1
    public string   CardName    { get; set; } = string.Empty;   // Nom SAP B1
    public string?  Phone       { get; set; }
    public string?  Email       { get; set; }
    public string?  Address     { get; set; }
    public string?  City        { get; set; }
    public string?  Country     { get; set; }
    public string   Currency    { get; set; } = "EUR";
    public decimal? CreditLimit { get; set; }
    public bool     IsActive    { get; set; } = true;
    public int?     SapDocNum   { get; set; }   // DocEntry SAP B1
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt  { get; set; }

    // Navigation
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
