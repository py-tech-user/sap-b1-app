namespace SapB1App.Models;

public enum DeliveryNoteStatus
{
    InProgress,
    Delivered
}

public class DeliveryNote
{
    public int               Id           { get; set; }
    public string            DocNum       { get; set; } = string.Empty;
    public string            CardCode     { get; set; } = string.Empty;
    public int               CustomerId   { get; set; }
    public int?              OrderId      { get; set; }
    public DateTime          DocDate      { get; set; } = DateTime.UtcNow;
    public DateTime?         DeliveryDate { get; set; }
    public DeliveryNoteStatus Status      { get; set; } = DeliveryNoteStatus.InProgress;
    public string?           Signature    { get; set; }
    public decimal           DocTotal     { get; set; }
    public decimal           VatTotal     { get; set; }
    public DocumentBaseType? BaseType     { get; set; }
    public int?              BaseEntry    { get; set; }
    public int?              BaseLine     { get; set; }
    public string?           Comments     { get; set; }
    public DateTime          CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime?         UpdatedAt    { get; set; }

    public Customer Customer { get; set; } = null!;
    public Order?   Order    { get; set; }
    public ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
}
