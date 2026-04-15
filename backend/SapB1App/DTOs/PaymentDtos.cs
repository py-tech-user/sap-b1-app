namespace SapB1App.DTOs;

public class PaymentDto
{
    public int      Id            { get; set; }
    public int      CustomerId    { get; set; }
    public string   CustomerName  { get; set; } = string.Empty;
    public string   CustomerCode  { get; set; } = string.Empty;
    public int?     OrderId       { get; set; }
    public string?  OrderDocNum   { get; set; }
    public int?     InvoiceId     { get; set; }
    public string?  InvoiceDocNum { get; set; }
    public decimal  Amount        { get; set; }
    public DateTime PaymentDate   { get; set; }
    public string   PaymentMethod { get; set; } = string.Empty;
    public string?  Reference     { get; set; }
    public string?  Comments      { get; set; }
    public bool     SyncedToSap   { get; set; }
    public DateTime CreatedAt     { get; set; }
}

public class CreatePaymentDto
{
    public int      CustomerId    { get; set; }
    public int?     OrderId       { get; set; }
    public int?     InvoiceId     { get; set; }
    public decimal  Amount        { get; set; }
    public DateTime PaymentDate   { get; set; }
    public string   PaymentMethod { get; set; } = "Cash";
    public string?  Reference     { get; set; }
    public string?  Comments      { get; set; }
}

public class UpdatePaymentDto
{
    public decimal  Amount        { get; set; }
    public DateTime PaymentDate   { get; set; }
    public string   PaymentMethod { get; set; } = string.Empty;
    public string?  Reference     { get; set; }
    public string?  Comments      { get; set; }
}
