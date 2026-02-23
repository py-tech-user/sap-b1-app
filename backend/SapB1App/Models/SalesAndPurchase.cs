namespace SapB1App.Models;

// ═══════════════════════════════════════════════════════════════════════════
// Bon de livraison
// ═══════════════════════════════════════════════════════════════════════════

public enum DeliveryStatus
{
    Draft,        // Brouillon
    Confirmed,    // Confirmé
    InTransit,    // En cours de livraison
    Delivered,    // Livré
    PartiallyDelivered,  // Partiellement livré
    Cancelled     // Annulé
}

public class DeliveryNote
{
    public int            Id            { get; set; }
    public string         DocNum        { get; set; } = string.Empty;  // BL-2026-0001
    public int            CustomerId    { get; set; }
    public int            OrderId       { get; set; }
    public DeliveryStatus Status        { get; set; } = DeliveryStatus.Draft;
    public DateTime       DocDate       { get; set; } = DateTime.UtcNow;
    public DateTime?      DeliveryDate  { get; set; }
    public string?        DeliveryAddress { get; set; }
    public string?        ContactName   { get; set; }
    public string?        ContactPhone  { get; set; }
    public string?        TrackingNumber { get; set; }
    public string?        Carrier       { get; set; }    // Transporteur
    public decimal        TotalWeight   { get; set; }
    public int            PackageCount  { get; set; }
    public string?        Comments      { get; set; }
    public string?        ReceivedBy    { get; set; }    // Nom du réceptionnaire
    public string?        Signature     { get; set; }    // Signature (base64)
    public int?           SapDocNum     { get; set; }
    public bool           SyncedToSap   { get; set; } = false;
    public DateTime       CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime?      UpdatedAt     { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public Order    Order    { get; set; } = null!;
    public ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
}

public class DeliveryNoteLine
{
    public int     Id              { get; set; }
    public int     DeliveryNoteId  { get; set; }
    public int     ProductId       { get; set; }
    public int?    OrderLineId     { get; set; }
    public decimal OrderedQty      { get; set; }
    public decimal DeliveredQty    { get; set; }
    public string? BatchNumber     { get; set; }
    public string? SerialNumber    { get; set; }
    public string? Comments        { get; set; }

    // Navigation
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public Product      Product      { get; set; } = null!;
    public OrderLine?   OrderLine    { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Bon de commande fournisseur (Purchase Order)
// ═══════════════════════════════════════════════════════════════════════════

public enum PurchaseOrderStatus
{
    Draft,
    Sent,         // Envoyé au fournisseur
    Confirmed,    // Confirmé par le fournisseur
    PartiallyReceived,
    Received,     // Réceptionné
    Cancelled
}

public class PurchaseOrder
{
    public int                 Id           { get; set; }
    public string              DocNum       { get; set; } = string.Empty;  // PO-2026-0001
    public int                 SupplierId   { get; set; }
    public PurchaseOrderStatus Status       { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime            DocDate      { get; set; } = DateTime.UtcNow;
    public DateTime?           ExpectedDate { get; set; }
    public DateTime?           ReceivedDate { get; set; }
    public decimal             DocTotal     { get; set; }
    public decimal             VatTotal     { get; set; }
    public string              Currency     { get; set; } = "EUR";
    public string?             Reference    { get; set; }   // Référence fournisseur
    public string?             Comments     { get; set; }
    public int?                SapDocNum    { get; set; }
    public bool                SyncedToSap  { get; set; } = false;
    public DateTime            CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime?           UpdatedAt    { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}

public class PurchaseOrderLine
{
    public int     Id              { get; set; }
    public int     PurchaseOrderId { get; set; }
    public int     ProductId       { get; set; }
    public decimal Quantity        { get; set; }
    public decimal ReceivedQty     { get; set; }
    public decimal UnitPrice       { get; set; }
    public decimal VatPct          { get; set; }
    public decimal LineTotal       { get; set; }
    public DateTime? ExpectedDate  { get; set; }
    public string? Comments        { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product       Product       { get; set; } = null!;
}

// ═══════════════════════════════════════════════════════════════════════════
// Fournisseur
// ═══════════════════════════════════════════════════════════════════════════

public class Supplier
{
    public int      Id        { get; set; }
    public string   CardCode  { get; set; } = string.Empty;
    public string   CardName  { get; set; } = string.Empty;
    public string?  Address   { get; set; }
    public string?  City      { get; set; }
    public string?  Country   { get; set; }
    public string?  Phone     { get; set; }
    public string?  Email     { get; set; }
    public string?  TaxId     { get; set; }
    public string   Currency  { get; set; } = "EUR";
    public int      PaymentTerms { get; set; } = 30;  // Jours
    public bool     IsActive  { get; set; } = true;
    public int?     SapDocNum { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}

// ═══════════════════════════════════════════════════════════════════════════
// Avoir (Credit Note)
// ═══════════════════════════════════════════════════════════════════════════

public enum CreditNoteStatus
{
    Draft,
    Confirmed,
    Applied,      // Appliqué sur facture
    Refunded,     // Remboursé
    Cancelled
}

public enum CreditNoteReason
{
    Return,       // Retour marchandise
    Discount,     // Remise commerciale
    Error,        // Erreur de facturation
    Damage,       // Marchandise endommagée
    Service,      // Geste commercial
    Other
}

public class CreditNote
{
    public int              Id          { get; set; }
    public string           DocNum      { get; set; } = string.Empty;  // AV-2026-0001
    public int              CustomerId  { get; set; }
    public int?             OrderId     { get; set; }
    public int?             ReturnId    { get; set; }
    public CreditNoteStatus Status      { get; set; } = CreditNoteStatus.Draft;
    public CreditNoteReason Reason      { get; set; } = CreditNoteReason.Return;
    public DateTime         DocDate     { get; set; } = DateTime.UtcNow;
    public decimal          DocTotal    { get; set; }
    public decimal          VatTotal    { get; set; }
    public string           Currency    { get; set; } = "EUR";
    public string?          Comments    { get; set; }
    public DateTime?        AppliedDate { get; set; }
    public int?             AppliedToInvoiceId { get; set; }
    public int?             SapDocNum   { get; set; }
    public bool             SyncedToSap { get; set; } = false;
    public DateTime         CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime?        UpdatedAt   { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public Order?   Order    { get; set; }
    public Return?  Return   { get; set; }
    public ICollection<CreditNoteLine> Lines { get; set; } = new List<CreditNoteLine>();
}

public class CreditNoteLine
{
    public int     Id           { get; set; }
    public int     CreditNoteId { get; set; }
    public int     ProductId    { get; set; }
    public decimal Quantity     { get; set; }
    public decimal UnitPrice    { get; set; }
    public decimal VatPct       { get; set; }
    public decimal LineTotal    { get; set; }
    public string? Comments     { get; set; }

    // Navigation
    public CreditNote CreditNote { get; set; } = null!;
    public Product    Product    { get; set; } = null!;
}

// ═══════════════════════════════════════════════════════════════════════════
// Réception de marchandise (Goods Receipt)
// ═══════════════════════════════════════════════════════════════════════════

public enum GoodsReceiptStatus
{
    Draft,
    Confirmed,
    Cancelled
}

public class GoodsReceipt
{
    public int                Id              { get; set; }
    public string             DocNum          { get; set; } = string.Empty;  // GR-2026-0001
    public int                SupplierId      { get; set; }
    public int?               PurchaseOrderId { get; set; }
    public GoodsReceiptStatus Status          { get; set; } = GoodsReceiptStatus.Draft;
    public DateTime           DocDate         { get; set; } = DateTime.UtcNow;
    public string?            DeliveryNoteRef { get; set; }  // Référence BL fournisseur
    public string?            Comments        { get; set; }
    public int?               SapDocNum       { get; set; }
    public bool               SyncedToSap     { get; set; } = false;
    public DateTime           CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime?          UpdatedAt       { get; set; }

    // Navigation
    public Supplier       Supplier      { get; set; } = null!;
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

public class GoodsReceiptLine
{
    public int      Id             { get; set; }
    public int      GoodsReceiptId { get; set; }
    public int      ProductId      { get; set; }
    public decimal  Quantity       { get; set; }
    public decimal  UnitPrice      { get; set; }
    public decimal  LineTotal      { get; set; }
    public string?  BatchNumber    { get; set; }
    public string?  SerialNumber   { get; set; }
    public string?  Location       { get; set; }   // Emplacement stock
    public string?  Comments       { get; set; }

    // Navigation
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public Product      Product      { get; set; } = null!;
}
