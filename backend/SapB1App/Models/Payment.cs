namespace SapB1App.Models;

public enum PaymentMethod
{
    Cash,         // Espèces
    Check,        // Chèque
    BankTransfer, // Virement
    Card,         // Carte bancaire
    Mobile        // Paiement mobile
}

public class Payment
{
    public int           Id            { get; set; }
    public int           CustomerId    { get; set; }
    public int?          OrderId       { get; set; }
    public int?          InvoiceId     { get; set; }
    public decimal       Amount        { get; set; }
    public DateTime      PaymentDate   { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string?       Reference     { get; set; }
    public string?       Comments      { get; set; }
    public int?          SapDocNum     { get; set; }
    public bool          SyncedToSap   { get; set; } = false;
    public DateTime      CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime?     UpdatedAt     { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public Order?   Order    { get; set; }
    public Invoice? Invoice  { get; set; }
}
