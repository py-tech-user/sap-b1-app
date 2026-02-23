namespace SapB1App.Models;

public enum OrderStatus
{
    Draft,
    Confirmed,
    Shipped,
    Delivered,
    Cancelled
}

public class Order
{
    public int          Id           { get; set; }
    public string       DocNum       { get; set; } = string.Empty;  // Numéro interne
    public int          CustomerId   { get; set; }
    public DateTime     DocDate      { get; set; } = DateTime.UtcNow;
    public DateTime?    DeliveryDate { get; set; }
    public OrderStatus  Status       { get; set; } = OrderStatus.Draft;
    public decimal      DocTotal     { get; set; }
    public decimal      VatTotal     { get; set; }
    public string       Currency     { get; set; } = "EUR";
    public string?      Comments     { get; set; }
    public int?         SapDocNum    { get; set; }   // DocEntry SAP B1
    public bool         SyncedToSap  { get; set; } = false;
    public DateTime     CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime?    UpdatedAt    { get; set; }

    // Navigation
    public Customer             Customer { get; set; } = null!;
    public ICollection<OrderLine> Lines  { get; set; } = new List<OrderLine>();
}
