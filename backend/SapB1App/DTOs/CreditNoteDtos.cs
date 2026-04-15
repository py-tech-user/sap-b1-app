namespace SapB1App.DTOs;

public class CreditNoteDto
{
    public int      Id            { get; set; }
    public string   DocNum        { get; set; } = string.Empty;
    public int      InvoiceId     { get; set; }
    public string   InvoiceDocNum { get; set; } = string.Empty;
    public int?     ReturnId      { get; set; }
    public decimal  Amount        { get; set; }
    public string   Reason        { get; set; } = string.Empty;
    public DateTime DocDate       { get; set; }
    public DateTime CreatedAt     { get; set; }
}

public class CreateCreditNoteDto
{
    public int      InvoiceId { get; set; }
    public int?     ReturnId  { get; set; }
    public decimal  Amount    { get; set; }
    public string   Reason    { get; set; } = string.Empty;
    public DateTime? DocDate  { get; set; }
}
