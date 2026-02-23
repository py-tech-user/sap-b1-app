namespace SapB1App.Models;

// ═══════════════════════════════════════════════════════════════════════════
// Retour de marchandise
// ═══════════════════════════════════════════════════════════════════════════

public enum ReturnStatus
{
    Pending,      // En attente de validation
    Approved,     // Approuvé
    Rejected,     // Rejeté
    Received,     // Marchandise reçue
    Processed,    // Traité (avoir émis)
    Closed        // Clôturé
}

public enum ReturnReason
{
    Defective,        // Produit défectueux
    WrongProduct,     // Mauvais produit livré
    Damaged,          // Produit endommagé
    NotAsDescribed,   // Non conforme à la description
    CustomerChanged,  // Changement d'avis client
    Overstock,        // Surplus de stock
    Other             // Autre
}

public class Return
{
    public int          Id            { get; set; }
    public string       ReturnNumber  { get; set; } = string.Empty;  // RET-2026-0001
    public int          CustomerId    { get; set; }
    public int?         OrderId       { get; set; }       // Commande d'origine
    public int?         DeliveryNoteId { get; set; }      // Bon de livraison d'origine
    public ReturnStatus Status        { get; set; } = ReturnStatus.Pending;
    public ReturnReason Reason        { get; set; } = ReturnReason.Defective;
    public string?      ReasonDetails { get; set; }
    public DateTime     RequestDate   { get; set; } = DateTime.UtcNow;
    public DateTime?    ApprovalDate  { get; set; }
    public DateTime?    ReceivedDate  { get; set; }
    public int?         ApprovedBy    { get; set; }       // UserId
    public decimal      TotalAmount   { get; set; }
    public string?      Comments      { get; set; }
    public int?         CreditNoteId  { get; set; }       // Avoir généré
    public int?         SapDocNum     { get; set; }
    public bool         SyncedToSap   { get; set; } = false;
    public DateTime     CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime?    UpdatedAt     { get; set; }

    // Navigation
    public Customer      Customer     { get; set; } = null!;
    public Order?        Order        { get; set; }
    public DeliveryNote? DeliveryNote { get; set; }
    public CreditNote?   CreditNote   { get; set; }
    public AppUser?      Approver     { get; set; }
    public ICollection<ReturnLine> Lines { get; set; } = new List<ReturnLine>();
}

public class ReturnLine
{
    public int     Id          { get; set; }
    public int     ReturnId    { get; set; }
    public int     ProductId   { get; set; }
    public decimal Quantity    { get; set; }
    public decimal UnitPrice   { get; set; }
    public decimal LineTotal   { get; set; }
    public string? Condition   { get; set; }  // État du produit retourné
    public string? Comments    { get; set; }

    // Navigation
    public Return  Return  { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

// ═══════════════════════════════════════════════════════════════════════════
// Réclamation client
// ═══════════════════════════════════════════════════════════════════════════

public enum ClaimStatus
{
    Open,         // Ouverte
    InProgress,   // En cours de traitement
    Resolved,     // Résolue
    Closed,       // Clôturée
    Cancelled     // Annulée
}

public enum ClaimType
{
    Quality,      // Problème qualité
    Delivery,     // Problème livraison
    Billing,      // Problème facturation
    Service,      // Problème service
    Other         // Autre
}

public enum ClaimPriority
{
    Low,
    Medium,
    High,
    Critical
}

public class Claim
{
    public int           Id            { get; set; }
    public string        ClaimNumber   { get; set; } = string.Empty;  // CLM-2026-0001
    public int           CustomerId    { get; set; }
    public int?          OrderId       { get; set; }
    public int?          ProductId     { get; set; }
    public ClaimType     Type          { get; set; } = ClaimType.Quality;
    public ClaimPriority Priority      { get; set; } = ClaimPriority.Medium;
    public ClaimStatus   Status        { get; set; } = ClaimStatus.Open;
    public string        Subject       { get; set; } = string.Empty;
    public string        Description   { get; set; } = string.Empty;
    public string?       Resolution    { get; set; }
    public DateTime      OpenDate      { get; set; } = DateTime.UtcNow;
    public DateTime?     ResolvedDate  { get; set; }
    public DateTime?     ClosedDate    { get; set; }
    public int?          AssignedTo    { get; set; }      // UserId
    public int?          ReturnId      { get; set; }      // Retour lié
    public int?          CreditNoteId  { get; set; }      // Avoir lié
    public int?          ServiceTicketId { get; set; }    // Ticket SAV lié
    public DateTime      CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime?     UpdatedAt     { get; set; }

    // Navigation
    public Customer       Customer      { get; set; } = null!;
    public Order?         Order         { get; set; }
    public Product?       Product       { get; set; }
    public AppUser?       AssignedUser  { get; set; }
    public Return?        Return        { get; set; }
    public CreditNote?    CreditNote    { get; set; }
    public ServiceTicket? ServiceTicket { get; set; }
    public ICollection<ClaimComment> Comments { get; set; } = new List<ClaimComment>();
}

public class ClaimComment
{
    public int      Id        { get; set; }
    public int      ClaimId   { get; set; }
    public int      UserId    { get; set; }
    public string   Comment   { get; set; } = string.Empty;
    public bool     IsInternal { get; set; } = false;  // Commentaire interne ou visible client
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Claim   Claim { get; set; } = null!;
    public AppUser User  { get; set; } = null!;
}

// ═══════════════════════════════════════════════════════════════════════════
// Ticket SAV (Service Après-Vente)
// ═══════════════════════════════════════════════════════════════════════════

public enum ServiceTicketStatus
{
    Open,
    Scheduled,    // Intervention planifiée
    InProgress,
    OnHold,       // En attente (pièces, etc.)
    Completed,
    Closed
}

public enum ServiceType
{
    Repair,       // Réparation
    Maintenance,  // Maintenance
    Installation, // Installation
    Inspection,   // Inspection
    Replacement,  // Remplacement
    Other
}

public class ServiceTicket
{
    public int                 Id            { get; set; }
    public string              TicketNumber  { get; set; } = string.Empty;  // SAV-2026-0001
    public int                 CustomerId    { get; set; }
    public int?                ProductId     { get; set; }
    public string?             SerialNumber  { get; set; }
    public ServiceType         Type          { get; set; } = ServiceType.Repair;
    public ServiceTicketStatus Status        { get; set; } = ServiceTicketStatus.Open;
    public ClaimPriority       Priority      { get; set; } = ClaimPriority.Medium;
    public string              Description   { get; set; } = string.Empty;
    public string?             Diagnosis     { get; set; }
    public string?             Resolution    { get; set; }
    public DateTime            OpenDate      { get; set; } = DateTime.UtcNow;
    public DateTime?           ScheduledDate { get; set; }
    public DateTime?           CompletedDate { get; set; }
    public int?                AssignedTo    { get; set; }      // Technicien
    public decimal             LaborCost     { get; set; }
    public decimal             PartsCost     { get; set; }
    public decimal             TotalCost     { get; set; }
    public bool                UnderWarranty { get; set; } = false;
    public int?                ClaimId       { get; set; }
    public DateTime            CreatedAt     { get; set; } = DateTime.UtcNow;
    public DateTime?           UpdatedAt     { get; set; }

    // Navigation
    public Customer  Customer     { get; set; } = null!;
    public Product?  Product      { get; set; }
    public AppUser?  Technician   { get; set; }
    public Claim?    Claim        { get; set; }
    public ICollection<ServicePart> Parts { get; set; } = new List<ServicePart>();
}

public class ServicePart
{
    public int     Id              { get; set; }
    public int     ServiceTicketId { get; set; }
    public int     ProductId       { get; set; }
    public decimal Quantity        { get; set; }
    public decimal UnitPrice       { get; set; }
    public decimal LineTotal       { get; set; }

    // Navigation
    public ServiceTicket ServiceTicket { get; set; } = null!;
    public Product       Product       { get; set; } = null!;
}
