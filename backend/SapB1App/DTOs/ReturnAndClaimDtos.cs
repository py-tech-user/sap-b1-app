using System.ComponentModel.DataAnnotations;

namespace SapB1App.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Retour de marchandise - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class ReturnDto
{
    public int       Id            { get; set; }
    public string    ReturnNumber  { get; set; } = string.Empty;
    public int       CustomerId    { get; set; }
    public string    CustomerName  { get; set; } = string.Empty;
    public string    CustomerCode  { get; set; } = string.Empty;
    public int?      OrderId       { get; set; }
    public string?   OrderDocNum   { get; set; }
    public int?      DeliveryNoteId { get; set; }
    public string    Status        { get; set; } = string.Empty;
    public string    Reason        { get; set; } = string.Empty;
    public string?   ReasonDetails { get; set; }
    public DateTime  RequestDate   { get; set; }
    public DateTime? ApprovalDate  { get; set; }
    public DateTime? ReceivedDate  { get; set; }
    public int?      ApprovedBy    { get; set; }
    public string?   ApproverName  { get; set; }
    public decimal   TotalAmount   { get; set; }
    public string?   Comments      { get; set; }
    public int?      CreditNoteId  { get; set; }
    public string?   CreditNoteNum { get; set; }
    public bool      SyncedToSap   { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public List<ReturnLineDto> Lines { get; set; } = new();
}

public class ReturnLineDto
{
    public int      Id          { get; set; }
    public int      ReturnId    { get; set; }
    public int      ProductId   { get; set; }
    public string   ItemCode    { get; set; } = string.Empty;
    public string   ItemName    { get; set; } = string.Empty;
    public decimal  Quantity    { get; set; }
    public decimal  UnitPrice   { get; set; }
    public decimal  LineTotal   { get; set; }
    public string?  Condition   { get; set; }
    public string?  Comments    { get; set; }
}

public class CreateReturnDto
{
    [Required]
    public int      CustomerId    { get; set; }
    public int?     OrderId       { get; set; }
    public int?     DeliveryNoteId { get; set; }
    [Required]
    public string   Reason        { get; set; } = "Defective";
    public string?  ReasonDetails { get; set; }
    public string?  Comments      { get; set; }
    [Required]
    [MinLength(1)]
    public List<CreateReturnLineDto> Lines { get; set; } = new();
}

public class CreateReturnLineDto
{
    [Required]
    public int     ProductId { get; set; }
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Condition { get; set; }
    public string? Comments  { get; set; }
}

public class UpdateReturnDto
{
    public string?  Status        { get; set; }
    public string?  Reason        { get; set; }
    public string?  ReasonDetails { get; set; }
    public string?  Comments      { get; set; }
}

public class ApproveReturnDto
{
    public bool    Approved { get; set; }
    public string? Comments { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Réclamation - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class ClaimDto
{
    public int       Id            { get; set; }
    public string    ClaimNumber   { get; set; } = string.Empty;
    public int       CustomerId    { get; set; }
    public string    CustomerName  { get; set; } = string.Empty;
    public string    CustomerCode  { get; set; } = string.Empty;
    public int?      OrderId       { get; set; }
    public string?   OrderDocNum   { get; set; }
    public int?      ProductId     { get; set; }
    public string?   ProductName   { get; set; }
    public string    Type          { get; set; } = string.Empty;
    public string    Priority      { get; set; } = string.Empty;
    public string    Status        { get; set; } = string.Empty;
    public string    Subject       { get; set; } = string.Empty;
    public string    Description   { get; set; } = string.Empty;
    public string?   Resolution    { get; set; }
    public DateTime  OpenDate      { get; set; }
    public DateTime? ResolvedDate  { get; set; }
    public DateTime? ClosedDate    { get; set; }
    public int?      AssignedTo    { get; set; }
    public string?   AssignedToName { get; set; }
    public int?      ReturnId      { get; set; }
    public int?      CreditNoteId  { get; set; }
    public int?      ServiceTicketId { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public List<ClaimCommentDto> Comments { get; set; } = new();
}

public class ClaimCommentDto
{
    public int      Id         { get; set; }
    public int      ClaimId    { get; set; }
    public int      UserId     { get; set; }
    public string   UserName   { get; set; } = string.Empty;
    public string   Comment    { get; set; } = string.Empty;
    public bool     IsInternal { get; set; }
    public DateTime CreatedAt  { get; set; }
}

public class CreateClaimDto
{
    [Required]
    public int    CustomerId  { get; set; }
    public int?   OrderId     { get; set; }
    public int?   ProductId   { get; set; }
    public string Type        { get; set; } = "Quality";
    public string Priority    { get; set; } = "Medium";
    [Required]
    public string Subject     { get; set; } = string.Empty;
    [Required]
    public string Description { get; set; } = string.Empty;
    public int?   AssignedTo  { get; set; }
}

public class UpdateClaimDto
{
    public string? Status      { get; set; }
    public string? Priority    { get; set; }
    public string? Resolution  { get; set; }
    public int?    AssignedTo  { get; set; }
}

public class AddClaimCommentDto
{
    [Required]
    public string Comment    { get; set; } = string.Empty;
    public bool   IsInternal { get; set; } = false;
}

// ═══════════════════════════════════════════════════════════════════════════
// Ticket SAV - DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class ServiceTicketDto
{
    public int       Id            { get; set; }
    public string    TicketNumber  { get; set; } = string.Empty;
    public int       CustomerId    { get; set; }
    public string    CustomerName  { get; set; } = string.Empty;
    public string    CustomerCode  { get; set; } = string.Empty;
    public int?      ProductId     { get; set; }
    public string?   ProductName   { get; set; }
    public string?   SerialNumber  { get; set; }
    public string    Type          { get; set; } = string.Empty;
    public string    Status        { get; set; } = string.Empty;
    public string    Priority      { get; set; } = string.Empty;
    public string    Description   { get; set; } = string.Empty;
    public string?   Diagnosis     { get; set; }
    public string?   Resolution    { get; set; }
    public DateTime  OpenDate      { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int?      AssignedTo    { get; set; }
    public string?   TechnicianName { get; set; }
    public decimal   LaborCost     { get; set; }
    public decimal   PartsCost     { get; set; }
    public decimal   TotalCost     { get; set; }
    public bool      UnderWarranty { get; set; }
    public int?      ClaimId       { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public List<ServicePartDto> Parts { get; set; } = new();
}

public class ServicePartDto
{
    public int     Id        { get; set; }
    public int     ProductId { get; set; }
    public string  ItemCode  { get; set; } = string.Empty;
    public string  ItemName  { get; set; } = string.Empty;
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateServiceTicketDto
{
    [Required]
    public int     CustomerId   { get; set; }
    public int?    ProductId    { get; set; }
    public string? SerialNumber { get; set; }
    public string  Type         { get; set; } = "Repair";
    public string  Priority     { get; set; } = "Medium";
    [Required]
    public string  Description  { get; set; } = string.Empty;
    public int?    AssignedTo   { get; set; }
    public int?    ClaimId      { get; set; }
    public bool    UnderWarranty { get; set; } = false;
}

public class UpdateServiceTicketDto
{
    public string?   Status        { get; set; }
    public string?   Priority      { get; set; }
    public string?   Diagnosis     { get; set; }
    public string?   Resolution    { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public int?      AssignedTo    { get; set; }
    public decimal?  LaborCost     { get; set; }
}

public class AddServicePartDto
{
    [Required]
    public int     ProductId { get; set; }
    [Required]
    public decimal Quantity  { get; set; }
    public decimal UnitPrice { get; set; }
}
