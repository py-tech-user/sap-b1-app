using System.ComponentModel.DataAnnotations;

namespace SapB1App.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Bon de livraison - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class DeliveryNoteDto
{
    public int       Id              { get; set; }
    public string    DocNum          { get; set; } = string.Empty;
    public int       CustomerId      { get; set; }
    public string    CustomerName    { get; set; } = string.Empty;
    public string    CustomerCode    { get; set; } = string.Empty;
    public int       OrderId         { get; set; }
    public string    OrderDocNum     { get; set; } = string.Empty;
    public string    Status          { get; set; } = string.Empty;
    public DateTime  DocDate         { get; set; }
    public DateTime? DeliveryDate    { get; set; }
    public string?   DeliveryAddress { get; set; }
    public string?   ContactName     { get; set; }
    public string?   ContactPhone    { get; set; }
    public string?   TrackingNumber  { get; set; }
    public string?   Carrier         { get; set; }
    public decimal   TotalWeight     { get; set; }
    public int       PackageCount    { get; set; }
    public string?   Comments        { get; set; }
    public string?   ReceivedBy      { get; set; }
    public bool      SyncedToSap     { get; set; }
    public DateTime  CreatedAt       { get; set; }
    public List<DeliveryNoteLineDto> Lines { get; set; } = new();
}

public class DeliveryNoteLineDto
{
    public int      Id           { get; set; }
    public int      ProductId    { get; set; }
    public string   ItemCode     { get; set; } = string.Empty;
    public string   ItemName     { get; set; } = string.Empty;
    public decimal  OrderedQty   { get; set; }
    public decimal  DeliveredQty { get; set; }
    public string?  BatchNumber  { get; set; }
    public string?  SerialNumber { get; set; }
}

public class CreateDeliveryNoteDto
{
    [Required]
    public int     OrderId         { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? ContactName     { get; set; }
    public string? ContactPhone    { get; set; }
    public string? Carrier         { get; set; }
    public string? Comments        { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreateDeliveryNoteLineDto> Lines { get; set; } = new();
}

public class CreateDeliveryNoteLineDto
{
    [Required]
    public int     ProductId    { get; set; }
    public int?    OrderLineId  { get; set; }
    public decimal OrderedQty   { get; set; }
    [Required]
    public decimal DeliveredQty { get; set; }
    public string? BatchNumber  { get; set; }
    public string? SerialNumber { get; set; }
}

public class UpdateDeliveryNoteDto
{
    public string?   Status         { get; set; }
    public DateTime? DeliveryDate   { get; set; }
    public string?   TrackingNumber { get; set; }
    public string?   ReceivedBy     { get; set; }
    public string?   Comments       { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Bon de commande fournisseur - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class PurchaseOrderDto
{
    public int       Id           { get; set; }
    public string    DocNum       { get; set; } = string.Empty;
    public int       SupplierId   { get; set; }
    public string    SupplierName { get; set; } = string.Empty;
    public string    SupplierCode { get; set; } = string.Empty;
    public string    Status       { get; set; } = string.Empty;
    public DateTime  DocDate      { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public decimal   DocTotal     { get; set; }
    public decimal   VatTotal     { get; set; }
    public string    Currency     { get; set; } = string.Empty;
    public string?   Reference    { get; set; }
    public string?   Comments     { get; set; }
    public bool      SyncedToSap  { get; set; }
    public DateTime  CreatedAt    { get; set; }
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}

public class PurchaseOrderLineDto
{
    public int      Id           { get; set; }
    public int      ProductId    { get; set; }
    public string   ItemCode     { get; set; } = string.Empty;
    public string   ItemName     { get; set; } = string.Empty;
    public decimal  Quantity     { get; set; }
    public decimal  ReceivedQty  { get; set; }
    public decimal  UnitPrice    { get; set; }
    public decimal  VatPct       { get; set; }
    public decimal  LineTotal    { get; set; }
    public DateTime? ExpectedDate { get; set; }
}

public class CreatePurchaseOrderDto
{
    [Required]
    public int       SupplierId   { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string    Currency     { get; set; } = "EUR";
    public string?   Reference    { get; set; }
    public string?   Comments     { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreatePurchaseOrderLineDto> Lines { get; set; } = new();
}

public class CreatePurchaseOrderLineDto
{
    [Required]
    public int     ProductId { get; set; }
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
}

public class UpdatePurchaseOrderDto
{
    public string?   Status       { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string?   Reference    { get; set; }
    public string?   Comments     { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Fournisseur - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class SupplierDto
{
    public int      Id           { get; set; }
    public string   CardCode     { get; set; } = string.Empty;
    public string   CardName     { get; set; } = string.Empty;
    public string?  Address      { get; set; }
    public string?  City         { get; set; }
    public string?  Country      { get; set; }
    public string?  Phone        { get; set; }
    public string?  Email        { get; set; }
    public string?  TaxId        { get; set; }
    public string   Currency     { get; set; } = string.Empty;
    public int      PaymentTerms { get; set; }
    public bool     IsActive     { get; set; }
    public DateTime CreatedAt    { get; set; }
}

public class CreateSupplierDto
{
    [Required]
    public string  CardCode     { get; set; } = string.Empty;
    [Required]
    public string  CardName     { get; set; } = string.Empty;
    public string? Address      { get; set; }
    public string? City         { get; set; }
    public string? Country      { get; set; }
    public string? Phone        { get; set; }
    public string? Email        { get; set; }
    public string? TaxId        { get; set; }
    public string  Currency     { get; set; } = "EUR";
    public int     PaymentTerms { get; set; } = 30;
}

public class UpdateSupplierDto
{
    public string? CardName     { get; set; }
    public string? Address      { get; set; }
    public string? City         { get; set; }
    public string? Country      { get; set; }
    public string? Phone        { get; set; }
    public string? Email        { get; set; }
    public string? TaxId        { get; set; }
    public int?    PaymentTerms { get; set; }
    public bool?   IsActive     { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Avoir - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class CreditNoteDto
{
    public int       Id           { get; set; }
    public string    DocNum       { get; set; } = string.Empty;
    public int       CustomerId   { get; set; }
    public string    CustomerName { get; set; } = string.Empty;
    public string    CustomerCode { get; set; } = string.Empty;
    public int?      OrderId      { get; set; }
    public string?   OrderDocNum  { get; set; }
    public int?      ReturnId     { get; set; }
    public string?   ReturnNumber { get; set; }
    public string    Status       { get; set; } = string.Empty;
    public string    Reason       { get; set; } = string.Empty;
    public DateTime  DocDate      { get; set; }
    public decimal   DocTotal     { get; set; }
    public decimal   VatTotal     { get; set; }
    public string    Currency     { get; set; } = string.Empty;
    public string?   Comments     { get; set; }
    public DateTime? AppliedDate  { get; set; }
    public bool      SyncedToSap  { get; set; }
    public DateTime  CreatedAt    { get; set; }
    public List<CreditNoteLineDto> Lines { get; set; } = new();
}

public class CreditNoteLineDto
{
    public int     Id        { get; set; }
    public int     ProductId { get; set; }
    public string  ItemCode  { get; set; } = string.Empty;
    public string  ItemName  { get; set; } = string.Empty;
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateCreditNoteDto
{
    [Required]
    public int     CustomerId { get; set; }
    public int?    OrderId    { get; set; }
    public int?    ReturnId   { get; set; }
    public string  Reason     { get; set; } = "Return";
    public string  Currency   { get; set; } = "EUR";
    public string? Comments   { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreateCreditNoteLineDto> Lines { get; set; } = new();
}

public class CreateCreditNoteLineDto
{
    [Required]
    public int     ProductId { get; set; }
    [Required]
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPct    { get; set; } = 20;
}

public class UpdateCreditNoteDto
{
    public string? Status   { get; set; }
    public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Réception de marchandise - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class GoodsReceiptDto
{
    public int       Id              { get; set; }
    public string    DocNum          { get; set; } = string.Empty;
    public int       SupplierId      { get; set; }
    public string    SupplierName    { get; set; } = string.Empty;
    public string    SupplierCode    { get; set; } = string.Empty;
    public int?      PurchaseOrderId { get; set; }
    public string?   PurchaseOrderNum { get; set; }
    public string    Status          { get; set; } = string.Empty;
    public DateTime  DocDate         { get; set; }
    public string?   DeliveryNoteRef { get; set; }
    public string?   Comments        { get; set; }
    public bool      SyncedToSap     { get; set; }
    public DateTime  CreatedAt       { get; set; }
    public List<GoodsReceiptLineDto> Lines { get; set; } = new();
}

public class GoodsReceiptLineDto
{
    public int      Id           { get; set; }
    public int      ProductId    { get; set; }
    public string   ItemCode     { get; set; } = string.Empty;
    public string   ItemName     { get; set; } = string.Empty;
    public decimal  Quantity     { get; set; }
    public decimal  UnitPrice    { get; set; }
    public decimal  LineTotal    { get; set; }
    public string?  BatchNumber  { get; set; }
    public string?  SerialNumber { get; set; }
    public string?  Location     { get; set; }
}

public class CreateGoodsReceiptDto
{
    [Required]
    public int     SupplierId      { get; set; }
    public int?    PurchaseOrderId { get; set; }
    public string? DeliveryNoteRef { get; set; }
    public string? Comments        { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreateGoodsReceiptLineDto> Lines { get; set; } = new();
}

public class CreateGoodsReceiptLineDto
{
    [Required]
    public int     ProductId { get; set; }
    [Required]
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Location  { get; set; }
}

public class UpdateGoodsReceiptDto
{
    public string? Status   { get; set; }
    public string? Comments { get; set; }
}
