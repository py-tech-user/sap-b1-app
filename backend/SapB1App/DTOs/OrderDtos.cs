namespace SapB1App.DTOs;

public class OrderDto
{
    public int      DocEntry      { get; set; }
    public int      Id           { get; set; }
    public string   DocNum       { get; set; } = string.Empty;
    public string   CardCode     { get; set; } = string.Empty;
    public int      CustomerId   { get; set; }
    public string   CustomerName { get; set; } = string.Empty;
    public string   CustomerCode { get; set; } = string.Empty;
    public DateTime DocDate      { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public decimal  DocTotal     { get; set; }
    public decimal  VatTotal     { get; set; }
    public string?  BaseType     { get; set; }
    public int?     BaseEntry    { get; set; }
    public int?     BaseLine     { get; set; }
    public string   Currency     { get; set; } = "EUR";
    public string?  Comments     { get; set; }
    public bool     SyncedToSap  { get; set; }
    public int?     SapDocNum    { get; set; }
    public DateTime CreatedAt    { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
}

public class OrderLineDto
{
    public int     Id        { get; set; }
    public int     ProductId { get; set; }
    public string  ItemCode  { get; set; } = string.Empty;
    public string  ItemName  { get; set; } = string.Empty;
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Price     { get; set; }
    public decimal VatPct    { get; set; }
    public decimal LineTotal { get; set; }
    public int?    BaseEntry { get; set; }
    public int?    BaseLine  { get; set; }
}

public class CreateOrderDto
{
    public int       CustomerId   { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string    Currency     { get; set; } = "EUR";
    public string?   Comments     { get; set; }
    public List<CreateOrderLineDto> Lines { get; set; } = new();
}

public class CreateOrderLineDto
{
    public int     ProductId { get; set; }
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
}

public class UpdateOrderStatusDto
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateOrderLineDto
{
    public int     Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
}
