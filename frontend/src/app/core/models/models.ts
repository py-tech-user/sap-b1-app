// ── Generic wrappers ─────────────────────────────────────────────

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ── Customer ─────────────────────────────────────────────────────

export interface Customer {
  id: number;
  cardCode: string;
  cardName: string;
  email?: string;
  phone?: string;
  address?: string;
  isActive: boolean;
  sapBpCode?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateCustomer {
  cardCode: string;
  cardName: string;
  email?: string;
  phone?: string;
  address?: string;
  isActive: boolean;
}

export interface UpdateCustomer extends Partial<CreateCustomer> {}

// ── Product ──────────────────────────────────────────────────────

export interface Product {
  id: number;
  itemCode: string;
  itemName: string;
  price: number;
  category?: string;
  stock: number;
  unit?: string;
  isActive: boolean;
}

export interface CreateProduct {
  itemCode: string;
  itemName: string;
  price: number;
  category?: string;
  stock: number;
  unit?: string;
  isActive: boolean;
}

// ── Order ────────────────────────────────────────────────────────

export interface Order {
  id: number;
  docNum: string;
  customerId: number;
  customerName: string;
  docDate: string;
  deliveryDate?: string;
  status: string;
  docTotal: number;
  vatTotal: number;
  currency: string;
  comments?: string;
  lines: OrderLine[];
}

export interface OrderLine {
  id: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  vatPct: number;
  lineTotal: number;
}

export interface CreateOrder {
  customerId: number;
  docDate: string;
  deliveryDate?: string;
  currency?: string;
  comments?: string;
  lines: CreateOrderLine[];
}

export interface CreateOrderLine {
  productId: number;
  quantity: number;
  unitPrice: number;
  vatPct?: number;
}

// ── Visit ────────────────────────────────────────────────────────

export interface Visit {
  id: number;
  customerId: number;
  customerName?: string;
  visitDate: string;
  subject: string;
  notes?: string;
  status: string;
  location?: string;
  sapDocEntry?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateVisit {
  customerId: number;
  visitDate: string;
  subject: string;
  notes?: string;
  status?: string;
  location?: string;
}

export interface UpdateVisit extends Partial<CreateVisit> {}

// ── Payment (Encaissement) ───────────────────────────────────────

export interface Payment {
  id: number;
  customerId: number;
  customerName?: string;
  orderId?: number;
  orderDocNum?: string;
  paymentDate: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  reference?: string;
  status: string;
  sapDocEntry?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreatePayment {
  customerId: number;
  orderId?: number;
  paymentDate: string;
  amount: number;
  currency?: string;
  paymentMethod: string;
  reference?: string;
}

export interface UpdatePayment extends Partial<CreatePayment> {}

// ── Tracking (GPS) ───────────────────────────────────────────────

export interface LocationTrack {
  id: number;
  userId: number;
  userName?: string;
  latitude: number;
  longitude: number;
  accuracy?: number;
  timestamp: string;
  visitId?: number;
  eventType: string; // 'auto' | 'check-in' | 'check-out'
}

export interface CreateLocationTrack {
  latitude: number;
  longitude: number;
  accuracy?: number;
  visitId?: number;
  eventType: string;
}

export interface UserLivePosition {
  userId: number;
  userName: string;
  latitude: number;
  longitude: number;
  accuracy?: number;
  lastUpdate: string;
  currentVisitId?: number;
  currentCustomerName?: string;
}

export interface CheckInRequest {
  visitId: number;
  latitude: number;
  longitude: number;
}

export interface CheckOutRequest {
  visitId: number;
  latitude: number;
  longitude: number;
  notes?: string;
}

export interface UserTrackingStats {
  userId: number;
  userName: string;
  totalVisits: number;
  completedVisits: number;
  totalDistanceKm: number;
  avgVisitDurationMin: number;
  lastActivity?: string;
}

export interface TrackPoint {
  latitude: number;
  longitude: number;
  timestamp: string;
  eventType: string;
}

// ── Reporting ────────────────────────────────────────────────────

export interface TopCustomer {
  customerId: number;
  cardCode: string;
  cardName: string;
  city?: string;
  totalRevenue: number;
  orderCount: number;
  visitCount: number;
  avgOrderValue: number;
  lastOrderDate?: string;
}

export interface TopProduct {
  productId: number;
  itemCode: string;
  itemName: string;
  totalQuantity: number;
  totalRevenue: number;
  orderCount: number;
}

export interface RevenueEvolution {
  period: string;
  revenue: number;
  orderCount: number;
  avgOrderValue: number;
  growthPercent?: number;
}

export interface PendingPayment {
  orderId: number;
  docNum: string;
  customerId: number;
  customerName: string;
  orderTotal: number;
  paidAmount: number;
  remainingAmount: number;
  daysOverdue: number;
}

export interface LateOrder {
  orderId: number;
  docNum: string;
  customerName: string;
  total: number;
  orderDate: string;
  expectedDate: string;
  daysLate: number;
  status: string;
}

export interface RecentOrder {
  id: number;
  docNum: string;
  customerName: string;
  docDate: string;
  docTotal: number;
  status: string;
}

export interface AdvancedDashboard {
  totalCustomers: number;
  activeCustomers: number;
  totalOrders: number;
  totalRevenue: number;
  revenueThisMonth: number;
  growthPercent: number;
  pendingOrdersCount: number;
  lateOrdersCount: number;
  pendingPaymentsAmount: number;
  topCustomers: TopCustomer[];
  topProducts: TopProduct[];
  revenueEvolution: RevenueEvolution[];
  recentOrders: RecentOrder[];
  lateOrders: LateOrder[];
  pendingPayments: PendingPayment[];
}

export interface DailyKpis {
  totalCustomers: number;
  totalOrders: number;
  totalRevenue: number;
  revenueThisMonth: number;
  growthPercent: number;
  pendingOrdersCount: number;
  lateOrdersCount: number;
  pendingPaymentsAmount: number;
}

// ── Retours ──────────────────────────────────────────────────────

export type ReturnStatus = 'Pending' | 'Approved' | 'Rejected' | 'Received' | 'Processed' | 'Closed';
export type ReturnReason = 'Defective' | 'WrongProduct' | 'Damaged' | 'NotAsDescribed' | 'CustomerChanged' | 'Other';

export interface ReturnLine {
  id: number;
  productId: number;
  itemCode: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  condition?: string;
}

export interface Return {
  id: number;
  returnNumber: string;
  customerId: number;
  customerName: string;
  orderId?: number;
  status: ReturnStatus;
  reason: ReturnReason;
  reasonDetails?: string;
  requestDate: string;
  approvalDate?: string;
  totalAmount: number;
  creditNoteId?: number;
  lines: ReturnLine[];
}

export interface CreateReturn {
  customerId: number;
  orderId?: number;
  reason: ReturnReason;
  reasonDetails?: string;
  lines: { productId: number; quantity: number; unitPrice?: number; condition?: string }[];
}

// ── Réclamations ─────────────────────────────────────────────────

export type ClaimStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';
export type ClaimType = 'Quality' | 'Delivery' | 'Billing' | 'Service' | 'Other';
export type ClaimPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export interface ClaimComment {
  id: number;
  userId: number;
  userName: string;
  comment: string;
  isInternal: boolean;
  createdAt: string;
}

export interface Claim {
  id: number;
  claimNumber: string;
  customerId: number;
  customerName: string;
  type: ClaimType;
  priority: ClaimPriority;
  status: ClaimStatus;
  subject: string;
  description: string;
  resolution?: string;
  openDate: string;
  assignedTo?: string;
  comments: ClaimComment[];
}

export interface CreateClaim {
  customerId: number;
  orderId?: number;
  productId?: number;
  type: ClaimType;
  priority: ClaimPriority;
  subject: string;
  description: string;
}

// ── Tickets SAV ──────────────────────────────────────────────────

export type ServiceTicketStatus = 'Open' | 'Scheduled' | 'InProgress' | 'OnHold' | 'Completed' | 'Closed';

export interface ServicePart {
  id: number;
  productId: number;
  itemCode: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface ServiceTicket {
  id: number;
  ticketNumber: string;
  customerId: number;
  customerName?: string;
  productId?: number;
  serialNumber?: string;
  type: string;
  status: ServiceTicketStatus;
  priority: ClaimPriority;
  description: string;
  diagnosis?: string;
  resolution?: string;
  scheduledDate?: string;
  laborCost: number;
  partsCost: number;
  totalCost: number;
  underWarranty: boolean;
  parts: ServicePart[];
}

export interface CreateServiceTicket {
  customerId: number;
  productId?: number;
  serialNumber?: string;
  type: string;
  priority: ClaimPriority;
  description: string;
  underWarranty?: boolean;
}

// ── Bons de livraison ────────────────────────────────────────────

export type DeliveryStatus = 'Draft' | 'Confirmed' | 'InTransit' | 'Delivered' | 'Cancelled';

export interface DeliveryNoteLine {
  id: number;
  productId: number;
  itemCode: string;
  itemName: string;
  orderedQty: number;
  deliveredQty: number;
}

export interface DeliveryNote {
  id: number;
  docNum: string;
  customerId: number;
  customerName: string;
  orderId: number;
  orderDocNum: string;
  status: DeliveryStatus;
  docDate: string;
  deliveryDate?: string;
  deliveryAddress?: string;
  trackingNumber?: string;
  carrier?: string;
  receivedBy?: string;
  lines: DeliveryNoteLine[];
}

export interface CreateDeliveryNote {
  orderId: number;
  deliveryAddress?: string;
  contactName?: string;
  carrier?: string;
  lines: { productId: number; orderedQty: number; deliveredQty: number }[];
}

// ── Fournisseurs ─────────────────────────────────────────────────

export interface Supplier {
  id: number;
  cardCode: string;
  cardName: string;
  address?: string;
  city?: string;
  phone?: string;
  email?: string;
  paymentTerms?: string;
  isActive: boolean;
}

export interface CreateSupplier {
  cardCode: string;
  cardName: string;
  address?: string;
  city?: string;
  phone?: string;
  email?: string;
  paymentTerms?: string;
}

// ── Bons de commande fournisseur ─────────────────────────────────

export type PurchaseOrderStatus = 'Draft' | 'Sent' | 'Confirmed' | 'PartiallyReceived' | 'Received' | 'Cancelled';

export interface PurchaseOrderLine {
  id: number;
  productId: number;
  itemCode: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  vatPct: number;
  lineTotal: number;
  receivedQty?: number;
}

export interface PurchaseOrder {
  id: number;
  docNum: string;
  supplierId: number;
  supplierName: string;
  status: PurchaseOrderStatus;
  docDate: string;
  expectedDate?: string;
  reference?: string;
  docTotal: number;
  vatTotal: number;
  lines: PurchaseOrderLine[];
}

export interface CreatePurchaseOrder {
  supplierId: number;
  expectedDate?: string;
  reference?: string;
  lines: { productId: number; quantity: number; unitPrice?: number; vatPct?: number }[];
}

// ── Avoirs ───────────────────────────────────────────────────────

export type CreditNoteStatus = 'Draft' | 'Confirmed' | 'Applied' | 'Refunded' | 'Cancelled';

export interface CreditNoteLine {
  id: number;
  productId: number;
  itemCode: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  vatPct: number;
  lineTotal: number;
}

export interface CreditNote {
  id: number;
  docNum: string;
  customerId: number;
  customerName: string;
  returnId?: number;
  returnNumber?: string;
  status: CreditNoteStatus;
  reason: string;
  docDate: string;
  docTotal: number;
  vatTotal: number;
  lines: CreditNoteLine[];
}

export interface CreateCreditNote {
  customerId: number;
  orderId?: number;
  returnId?: number;
  reason: string;
  lines: { productId: number; quantity: number; unitPrice?: number; vatPct?: number }[];
}

// ── Réceptions de marchandise ────────────────────────────────────

export interface GoodsReceiptLine {
  id: number;
  productId: number;
  itemCode: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  batchNumber?: string;
  location?: string;
}

export interface GoodsReceipt {
  id: number;
  docNum: string;
  supplierId: number;
  supplierName: string;
  purchaseOrderId?: number;
  status: string;
  docDate: string;
  lines: GoodsReceiptLine[];
}

export interface CreateGoodsReceipt {
  supplierId: number;
  purchaseOrderId?: number;
  deliveryNoteRef?: string;
  lines: { productId: number; quantity: number; unitPrice?: number; batchNumber?: string; location?: string }[];
}
