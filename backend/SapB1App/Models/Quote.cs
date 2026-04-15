namespace SapB1App.Models;

public enum QuoteStatus
{
    Pending,
    Accepted,
    Rejected
}

public class Quote
{
    public int         Id         { get; set; }
    public string      DocNum     { get; set; } = string.Empty;
    public string      CardCode   { get; set; } = string.Empty;
    public int         CustomerId { get; set; }
    public DateTime    DocDate    { get; set; } = DateTime.UtcNow;
    public QuoteStatus Status     { get; set; } = QuoteStatus.Pending;
    public decimal     DocTotal   { get; set; }
    public decimal     VatTotal   { get; set; }
    public DocumentBaseType? BaseType { get; set; }
    public int?        BaseEntry  { get; set; }
    public int?        BaseLine   { get; set; }
    public string      Currency   { get; set; } = "EUR";
    public string?     Comments   { get; set; }
    public DateTime    CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime?   UpdatedAt  { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
}
