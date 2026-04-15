namespace SapB1App.Models;

public class ReturnLine
{
    public int     Id        { get; set; }
    public int     ReturnId  { get; set; }
    public int     ProductId { get; set; }
    public int     LineNum   { get; set; }
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
    public decimal LineTotal { get; set; }

    public Return  Return  { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
