namespace SapB1App.DTOs;

// ═══════════════════════════════════════════════════════════════════════════
// Dashboard principal
// ═══════════════════════════════════════════════════════════════════════════

public class DashboardDto
{
    public int     TotalCustomers   { get; set; }
    public int     TotalOrders      { get; set; }
    public decimal TotalRevenue     { get; set; }
    public int     PendingOrders    { get; set; }
    public int     LowStockProducts { get; set; }
    public List<RecentOrderDto> RecentOrders { get; set; } = new();
    public List<TopProductDto>  TopProducts  { get; set; } = new();
}

public class CommercialDashboardDto
{
    public int TotalQuotesPending { get; set; }
    public int TotalOrdersInPreparation { get; set; }
    public int TotalDeliveryInProgress { get; set; }
    public int TotalInvoicesUnpaid { get; set; }
    public int TotalReturnsPending { get; set; }
    public int TotalCreditNotes { get; set; }
    public decimal TotalInvoicesUnpaidAmount { get; set; }
    public decimal TotalQuotesPendingAmount { get; set; }
    public decimal TotalReturnsPendingAmount { get; set; }
}

public class RecentOrderDto
{
    public int      Id           { get; set; }
    public string   DocNum       { get; set; } = string.Empty;
    public string   CustomerName { get; set; } = string.Empty;
    public decimal  Total        { get; set; }
    public string   Status       { get; set; } = string.Empty;
    public DateTime Date         { get; set; }
}

public class TopProductDto
{
    public int     ProductId     { get; set; }
    public string  ItemCode      { get; set; } = string.Empty;
    public string  ItemName      { get; set; } = string.Empty;
    public int     TotalQuantity { get; set; }
    public decimal TotalRevenue  { get; set; }
    public int     OrderCount    { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Top 10 Clients
// ═══════════════════════════════════════════════════════════════════════════

public class TopCustomerDto
{
    public int     CustomerId    { get; set; }
    public string  CardCode      { get; set; } = string.Empty;
    public string  CardName      { get; set; } = string.Empty;
    public string? City          { get; set; }
    public decimal TotalRevenue  { get; set; }
    public int     OrderCount    { get; set; }
    public int     VisitCount    { get; set; }
    public decimal AvgOrderValue { get; set; }
    public DateTime? LastOrderDate { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Évolution du chiffre d'affaires
// ═══════════════════════════════════════════════════════════════════════════

public class RevenueEvolutionDto
{
    public string   Period        { get; set; } = string.Empty;  // "2026-01", "2026-02", etc.
    public decimal  Revenue       { get; set; }
    public int      OrderCount    { get; set; }
    public decimal  AvgOrderValue { get; set; }
    public decimal? GrowthPercent { get; set; }  // % par rapport à la période précédente
}

public class RevenueReportDto
{
    public decimal TotalRevenue        { get; set; }
    public decimal RevenueThisMonth    { get; set; }
    public decimal RevenueLastMonth    { get; set; }
    public decimal GrowthPercent       { get; set; }
    public decimal AvgDailyRevenue     { get; set; }
    public List<RevenueEvolutionDto> MonthlyEvolution { get; set; } = new();
    public List<RevenueEvolutionDto> DailyEvolution   { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// Encaissements en attente
// ═══════════════════════════════════════════════════════════════════════════

public class PendingPaymentDto
{
    public int      OrderId       { get; set; }
    public string   DocNum        { get; set; } = string.Empty;
    public int      CustomerId    { get; set; }
    public string   CustomerName  { get; set; } = string.Empty;
    public string   CustomerCode  { get; set; } = string.Empty;
    public decimal  OrderTotal    { get; set; }
    public decimal  PaidAmount    { get; set; }
    public decimal  RemainingAmount { get; set; }
    public DateTime OrderDate     { get; set; }
    public int      DaysOverdue   { get; set; }
    public string   Status        { get; set; } = string.Empty;
}

public class PendingPaymentsReportDto
{
    public decimal TotalPending      { get; set; }
    public int     TotalOrdersCount  { get; set; }
    public decimal OverdueAmount     { get; set; }
    public int     OverdueCount      { get; set; }
    public List<PendingPaymentDto> PendingPayments { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// Commandes en retard
// ═══════════════════════════════════════════════════════════════════════════

public class LateOrderDto
{
    public int      OrderId         { get; set; }
    public string   DocNum          { get; set; } = string.Empty;
    public int      CustomerId      { get; set; }
    public string   CustomerName    { get; set; } = string.Empty;
    public string   CustomerCode    { get; set; } = string.Empty;
    public decimal  Total           { get; set; }
    public DateTime OrderDate       { get; set; }
    public DateTime? DeliveryDate   { get; set; }
    public DateTime ExpectedDate    { get; set; }
    public int      DaysLate        { get; set; }
    public string   Status          { get; set; } = string.Empty;
}

public class LateOrdersReportDto
{
    public int     TotalLateOrders  { get; set; }
    public decimal TotalLateAmount  { get; set; }
    public int     AvgDaysLate      { get; set; }
    public List<LateOrderDto> LateOrders { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// Rapport complet du tableau de bord
// ═══════════════════════════════════════════════════════════════════════════

public class AdvancedDashboardDto
{
    // KPIs principaux
    public int     TotalCustomers     { get; set; }
    public int     ActiveCustomers    { get; set; }
    public int     TotalOrders        { get; set; }
    public decimal TotalRevenue       { get; set; }
    public decimal RevenueThisMonth   { get; set; }
    public decimal GrowthPercent      { get; set; }

    // Alertes
    public int     PendingOrdersCount { get; set; }
    public int     LateOrdersCount    { get; set; }
    public decimal PendingPaymentsAmount { get; set; }

    // Top 10
    public List<TopCustomerDto> TopCustomers { get; set; } = new();
    public List<TopProductDto>  TopProducts  { get; set; } = new();

    // Évolution
    public List<RevenueEvolutionDto> RevenueEvolution { get; set; } = new();

    // Détails
    public List<RecentOrderDto>    RecentOrders    { get; set; } = new();
    public List<LateOrderDto>      LateOrders      { get; set; } = new();
    public List<PendingPaymentDto> PendingPayments { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// Paramètres de filtrage des rapports
// ═══════════════════════════════════════════════════════════════════════════

public class ReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate   { get; set; }
    public int?      Limit     { get; set; } = 10;
    public string?   Period    { get; set; } = "month";  // "day", "week", "month", "year"
}
