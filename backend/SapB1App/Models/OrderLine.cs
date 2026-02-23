namespace SapB1App.Models;

public class OrderLine
{
    public int     Id        { get; set; }
    public int     OrderId   { get; set; }
    public int     ProductId { get; set; }
    public int     LineNum   { get; set; }
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
    public decimal LineTotal { get; set; }

    // Navigation
    public Order   Order   { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
