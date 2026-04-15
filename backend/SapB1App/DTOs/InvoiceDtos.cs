namespace SapB1App.DTOs;

public class InvoiceDto
{
    public int      DocEntry         { get; set; }
    public int      Id              { get; set; }
    public string   DocNum          { get; set; } = string.Empty;
    public string   CardCode        { get; set; } = string.Empty;
    public int      CustomerId      { get; set; }
    public string   CustomerName    { get; set; } = string.Empty;
    public string   CustomerCode    { get; set; } = string.Empty;
    public int?     DeliveryNoteId  { get; set; }
    public string   DeliveryDocNum  { get; set; } = string.Empty;
    public DateTime DocDate         { get; set; }
    public DateTime? DueDate         { get; set; }
    public string   Status          { get; set; } = string.Empty;
    public decimal  DocTotal        { get; set; }
    public decimal  VatTotal        { get; set; }
    public string?  BaseType        { get; set; }
    public int?     BaseEntry       { get; set; }
    public int?     BaseLine        { get; set; }
    public string   Currency        { get; set; } = "EUR";
    public string?  Comments        { get; set; }
    public DateTime CreatedAt       { get; set; }
    public List<InvoiceLineDto> Lines { get; set; } = new();
    public List<CreditNoteSummaryDto> CreditNotes { get; set; } = new();
}

public class InvoiceLineDto
{
    public int     Id        { get; set; }
    public int     ProductId { get; set; }
    public string  ItemCode  { get; set; } = string.Empty;
    public string  ItemName  { get; set; } = string.Empty;
    public int     Quantity  { get; set; }
    public decimal Price     { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; }
    public decimal LineTotal { get; set; }
    public int?    BaseEntry { get; set; }
    public int?    BaseLine  { get; set; }
}

public class CreditNoteSummaryDto
{
    public int      Id      { get; set; }
    public string   DocNum  { get; set; } = string.Empty;
    public decimal  Amount  { get; set; }
    public string   Reason  { get; set; } = string.Empty;
    public DateTime DocDate { get; set; }
}

public class CreateInvoiceDto
{
    public int       CustomerId     { get; set; }
    public int?      DeliveryNoteId { get; set; }
    public DateTime? DueDate        { get; set; }
    public string    Currency       { get; set; } = "EUR";
    public string?   Comments       { get; set; }
    public List<CreateInvoiceLineDto> Lines { get; set; } = new();
}

public class CreateInvoiceLineDto
{
    public int     ProductId { get; set; }
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
}

public class UpdateInvoiceStatusDto
{
    public string Status { get; set; } = string.Empty;
}
