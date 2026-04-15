namespace SapB1App.DTOs;

public class ReturnDto
{
    public int      Id               { get; set; }
    public string   ReturnNumber     { get; set; } = string.Empty;
    public int      CustomerId       { get; set; }
    public string   CustomerName     { get; set; } = string.Empty;
    public string   CustomerCode     { get; set; } = string.Empty;
    public int      DeliveryNoteId   { get; set; }
    public string   DeliveryDocNum   { get; set; } = string.Empty;
    public string   Status           { get; set; } = string.Empty;
    public string   Reason           { get; set; } = string.Empty;
    public DateTime DocDate          { get; set; }
    public DateTime CreatedAt        { get; set; }
    public int?     CreditNoteId     { get; set; }
    public List<ReturnLineDto> Lines { get; set; } = new();
}

public class ReturnLineDto
{
    public int     Id        { get; set; }
    public int     ProductId { get; set; }
    public string  ItemCode  { get; set; } = string.Empty;
    public string  ItemName  { get; set; } = string.Empty;
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateReturnDto
{
    public int      CustomerId     { get; set; }
    public int      DeliveryNoteId { get; set; }
    public string   Reason         { get; set; } = string.Empty;
    public DateTime? DocDate       { get; set; }
    public List<CreateReturnLineDto> Lines { get; set; } = new();
}

public class CreateReturnLineDto
{
    public int     ProductId { get; set; }
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
}

public class UpdateReturnStatusDto
{
    public string Status { get; set; } = string.Empty;
}
