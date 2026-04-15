namespace SapB1App.Models;

public class InvoiceLine
{
    public int     Id        { get; set; }
    public int     InvoiceId { get; set; }
    public string  ItemCode  { get; set; } = string.Empty;
    public int     ProductId { get; set; }
    public int     LineNum   { get; set; }
    public int     Quantity  { get; set; }
    public decimal Price     { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
    public decimal LineTotal { get; set; }
    public int?    BaseEntry { get; set; }
    public int?    BaseLine  { get; set; }

    public Invoice Invoice { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
