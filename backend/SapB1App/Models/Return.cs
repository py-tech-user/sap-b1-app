namespace SapB1App.Models;

public enum ReturnStatus
{
    Pending,
    Validated
}

public class Return
{
    public int          Id             { get; set; }
    public string       ReturnNumber   { get; set; } = string.Empty;
    public int          CustomerId     { get; set; }
    public int          DeliveryNoteId { get; set; }
    public ReturnStatus Status         { get; set; } = ReturnStatus.Pending;
    public string       Reason         { get; set; } = string.Empty;
    public DateTime     DocDate        { get; set; } = DateTime.UtcNow;
    public DateTime     CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime?    UpdatedAt      { get; set; }
    public int?         CreditNoteId   { get; set; }

    public Customer     Customer     { get; set; } = null!;
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public CreditNote?  CreditNote   { get; set; }
    public ICollection<ReturnLine> Lines { get; set; } = new List<ReturnLine>();
}
