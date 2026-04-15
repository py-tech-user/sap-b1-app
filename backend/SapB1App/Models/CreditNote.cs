namespace SapB1App.Models;

public class CreditNote
{
    public int      Id        { get; set; }
    public string   DocNum    { get; set; } = string.Empty;
    public int      InvoiceId { get; set; }
    public int?     ReturnId  { get; set; }
    public decimal  Amount    { get; set; }
    public string   Reason    { get; set; } = string.Empty;
    public DateTime DocDate   { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Invoice Invoice { get; set; } = null!;
    public Return? Return  { get; set; }
}
