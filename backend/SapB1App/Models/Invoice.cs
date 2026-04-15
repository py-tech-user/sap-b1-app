namespace SapB1App.Models;

public enum InvoiceStatus
{
    Unpaid,
    Paid
}

public class Invoice
{
    public int          Id             { get; set; }
    public string       DocNum         { get; set; } = string.Empty;
    public string       CardCode       { get; set; } = string.Empty;
    public int          CustomerId     { get; set; }
    public int?         DeliveryNoteId { get; set; }
    public DateTime     DocDate        { get; set; } = DateTime.UtcNow;
    public DateTime?    DueDate        { get; set; }
    public InvoiceStatus Status        { get; set; } = InvoiceStatus.Unpaid;
    public decimal      DocTotal       { get; set; }
    public decimal      VatTotal       { get; set; }
    public DocumentBaseType? BaseType  { get; set; }
    public int?         BaseEntry      { get; set; }
    public int?         BaseLine       { get; set; }
    public string       Currency       { get; set; } = "EUR";
    public string?      Comments       { get; set; }
    public DateTime     CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime?    UpdatedAt      { get; set; }

    public Customer     Customer     { get; set; } = null!;
    public DeliveryNote? DeliveryNote { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<CreditNote> CreditNotes { get; set; } = new List<CreditNote>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
