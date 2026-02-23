using SapB1App.DTOs;

namespace SapB1App.Interfaces;

// ═══════════════════════════════════════════════════════════════════════════
// Service Retours
// ═══════════════════════════════════════════════════════════════════════════

public interface IReturnService
{
    Task<PagedResult<ReturnDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId);
    Task<ReturnDto?> GetByIdAsync(int id);
    Task<ReturnDto> CreateAsync(CreateReturnDto dto);
    Task<ReturnDto?> UpdateAsync(int id, UpdateReturnDto dto);
    Task<ReturnDto?> ApproveAsync(int id, int approverId, ApproveReturnDto dto);
    Task<ReturnDto?> ReceiveAsync(int id);
    Task<ReturnDto?> ProcessAsync(int id);  // Génère l'avoir
    Task<bool> DeleteAsync(int id);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Réclamations
// ═══════════════════════════════════════════════════════════════════════════

public interface IClaimService
{
    Task<PagedResult<ClaimDto>> GetAllAsync(int page, int pageSize, string? search, string? status, string? priority, int? customerId);
    Task<ClaimDto?> GetByIdAsync(int id);
    Task<ClaimDto> CreateAsync(CreateClaimDto dto);
    Task<ClaimDto?> UpdateAsync(int id, UpdateClaimDto dto);
    Task<ClaimDto?> AddCommentAsync(int id, int userId, AddClaimCommentDto dto);
    Task<ClaimDto?> AssignAsync(int id, int userId);
    Task<ClaimDto?> ResolveAsync(int id, string resolution);
    Task<ClaimDto?> CloseAsync(int id);
    Task<bool> DeleteAsync(int id);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service SAV
// ═══════════════════════════════════════════════════════════════════════════

public interface IServiceTicketService
{
    Task<PagedResult<ServiceTicketDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId);
    Task<ServiceTicketDto?> GetByIdAsync(int id);
    Task<ServiceTicketDto> CreateAsync(CreateServiceTicketDto dto);
    Task<ServiceTicketDto?> UpdateAsync(int id, UpdateServiceTicketDto dto);
    Task<ServiceTicketDto?> AddPartAsync(int id, AddServicePartDto dto);
    Task<ServiceTicketDto?> RemovePartAsync(int id, int partId);
    Task<ServiceTicketDto?> ScheduleAsync(int id, DateTime scheduledDate, int? technicianId);
    Task<ServiceTicketDto?> CompleteAsync(int id, string resolution);
    Task<bool> DeleteAsync(int id);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Bons de livraison
// ═══════════════════════════════════════════════════════════════════════════

public interface IDeliveryNoteService
{
    Task<PagedResult<DeliveryNoteDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId);
    Task<DeliveryNoteDto?> GetByIdAsync(int id);
    Task<DeliveryNoteDto> CreateAsync(CreateDeliveryNoteDto dto);
    Task<DeliveryNoteDto?> UpdateAsync(int id, UpdateDeliveryNoteDto dto);
    Task<DeliveryNoteDto?> ConfirmAsync(int id);
    Task<DeliveryNoteDto?> ShipAsync(int id, string? trackingNumber);
    Task<DeliveryNoteDto?> DeliverAsync(int id, string? receivedBy);
    Task<bool> DeleteAsync(int id);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Fournisseurs
// ═══════════════════════════════════════════════════════════════════════════

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetAllAsync(int page, int pageSize, string? search);
    Task<SupplierDto?> GetByIdAsync(int id);
    Task<SupplierDto> CreateAsync(CreateSupplierDto dto);
    Task<SupplierDto?> UpdateAsync(int id, UpdateSupplierDto dto);
    Task<bool> DeleteAsync(int id);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Bons de commande fournisseur
// ═══════════════════════════════════════════════════════════════════════════

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? supplierId);
    Task<PurchaseOrderDto?> GetByIdAsync(int id);
    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto dto);
    Task<PurchaseOrderDto?> UpdateAsync(int id, UpdatePurchaseOrderDto dto);
    Task<PurchaseOrderDto?> SendAsync(int id);
    Task<PurchaseOrderDto?> ConfirmAsync(int id);
    Task<bool> DeleteAsync(int id);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Avoirs
// ═══════════════════════════════════════════════════════════════════════════

public interface ICreditNoteService
{
    Task<PagedResult<CreditNoteDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? customerId);
    Task<CreditNoteDto?> GetByIdAsync(int id);
    Task<CreditNoteDto> CreateAsync(CreateCreditNoteDto dto);
    Task<CreditNoteDto?> UpdateAsync(int id, UpdateCreditNoteDto dto);
    Task<CreditNoteDto?> ConfirmAsync(int id);
    Task<CreditNoteDto?> ApplyAsync(int id, int? invoiceId);
    Task<CreditNoteDto?> RefundAsync(int id);
    Task<bool> DeleteAsync(int id);
    Task<CreditNoteDto> CreateFromReturnAsync(int returnId);
}

// ═══════════════════════════════════════════════════════════════════════════
// Service Réception marchandise
// ═══════════════════════════════════════════════════════════════════════════

public interface IGoodsReceiptService
{
    Task<PagedResult<GoodsReceiptDto>> GetAllAsync(int page, int pageSize, string? search, string? status, int? supplierId);
    Task<GoodsReceiptDto?> GetByIdAsync(int id);
    Task<GoodsReceiptDto> CreateAsync(CreateGoodsReceiptDto dto);
    Task<GoodsReceiptDto?> ConfirmAsync(int id);
    Task<bool> DeleteAsync(int id);
    Task<GoodsReceiptDto> CreateFromPurchaseOrderAsync(int purchaseOrderId, List<CreateGoodsReceiptLineDto> lines);
}
