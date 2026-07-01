namespace SapB1App.DTOs;

public class CommercialReportingResponseDto
{
    public string Mode { get; set; } = "Commercial";
    public string PeriodLabel { get; set; } = string.Empty;
    public int? SelectedSalesPersonCode { get; set; }
    public string? SelectedSalesPersonName { get; set; }
    public CommercialReportingKpiDto Kpis { get; set; } = new();
    public List<CommercialSalesPersonPerformanceDto> TeamPerformances { get; set; } = new();
    public List<CommercialRecentDocumentDto> RecentDocuments { get; set; } = new();
    public List<CommercialSalesPersonInfoDto> TeamMembers { get; set; } = new();
    public List<CommercialSalesPersonInfoDto> InactiveSalesPersons { get; set; } = new();
    public CommercialSalesPersonPerformanceDto? TopSalesPerson { get; set; }
    public List<ReportingTopClientDto> TopClients { get; set; } = new();
}

public class CommercialReportingKpiDto
{
    public int QuotesCount { get; set; }
    public decimal QuotesAmount { get; set; }
    public int OrdersCount { get; set; }
    public decimal OrdersAmount { get; set; }
    public int DeliveryNotesCount { get; set; }
    public decimal DeliveryNotesAmount { get; set; }
    public int InvoicesCount { get; set; }
    public decimal InvoicesAmount { get; set; }
    public int UnpaidInvoicesCount { get; set; }
    public decimal UnpaidInvoicesAmount { get; set; }
    public decimal QuoteToOrderRate { get; set; }
    public decimal OrderToDeliveryRate { get; set; }
    public decimal DeliveryToInvoiceRate { get; set; }
    public decimal ConversionRate { get; set; } // Backward-compat: alias QuoteToOrderRate
    public decimal CreditNotesAmount { get; set; }
    public int CreditNotesCount { get; set; }
    public decimal ReturnsAmount { get; set; }
    public int ReturnsCount { get; set; }
    public decimal NetRevenue { get; set; } // CA = Factures - Avoirs
    public decimal PendingRevenue { get; set; } // BC open + BL open
    public int ActivePartnersCount { get; set; }
    public int InactivePartnersCount { get; set; }
    // --- Nouveaux KPIs ---
    public decimal CollectedRevenue { get; set; }          // CA encaissé ce mois (paiements reçus)
    public decimal MonthlyTarget { get; set; }             // Objectif mensuel configuré pour ce commercial
    public decimal PeriodTarget { get; set; }              // Objectif adapté à la période sélectionnée
    public decimal TargetAchievementRate { get; set; }     // Taux d'atteinte de l'objectif (%)
    public decimal AverageQuoteAmount { get; set; }        // Panier moyen des devis émis
    public decimal QuoteValidationDays { get; set; }       // Délai moyen validation devis (jours)
    public int OverdueInvoicesCount { get; set; }          // Factures en retard (échéance dépassée)
    public decimal OverdueInvoicesAmount { get; set; }     // Montant impayé en retard
    public decimal Dso { get; set; }                       // DSO — délai moyen encaissement (jours)
    public decimal PaymentRate { get; set; }               // Taux d'encaissement (%)
    public int NewActivePartnersCount { get; set; }        // Nouveaux partenaires créés dans la période
    public decimal OpenPipelineAmount { get; set; }        // Affaires en cours (devis ouverts non conclus)
    public decimal SalesCycleDays { get; set; }            // Temps de cycle vente moyen (jours)
}

public class CommercialSalesPersonPerformanceDto
{
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public int QuotesCount { get; set; }
    public decimal QuotesAmount { get; set; }
    public int OrdersCount { get; set; }
    public decimal OrdersAmount { get; set; }
    public int DeliveryNotesCount { get; set; }
    public decimal DeliveryNotesAmount { get; set; }
    public int InvoicesCount { get; set; }
    public decimal InvoicesAmount { get; set; }
    public int CreditNotesCount { get; set; }
    public decimal CreditNotesAmount { get; set; }
    public decimal NetRevenue { get; set; } // CA realise = Factures - Avoirs
    public decimal CollectedRevenue { get; set; }
    public decimal PeriodTarget { get; set; }
    public decimal PendingRevenue { get; set; } // BC open + BL open
    public int UnpaidInvoicesCount { get; set; }
    public decimal UnpaidInvoicesAmount { get; set; }
    public decimal QuoteToOrderRate { get; set; }
    public decimal OrderToDeliveryRate { get; set; }
    public decimal DeliveryToInvoiceRate { get; set; }
    public decimal ConversionRate { get; set; } // Backward-compat: alias QuoteToOrderRate
}

public class CommercialRecentDocumentDto
{
    public string Type { get; set; } = string.Empty;
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime? Date { get; set; }
    public int SalesPersonCode { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CommercialSalesPersonInfoDto
{
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class CommercialPartnerActivityDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public int SalesPersonCode { get; set; }
    public int QuotesCount { get; set; }
    public decimal QuotesAmount { get; set; }
    public int OrdersCount { get; set; }
    public decimal OrdersAmount { get; set; }
    public int DeliveryNotesCount { get; set; }
    public decimal DeliveryNotesAmount { get; set; }
    public int InvoicesCount { get; set; }
    public decimal InvoicesAmount { get; set; }
    public int CreditNotesCount { get; set; }
    public decimal CreditNotesAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public bool IsActive { get; set; }
}

public class AdvancedReportingResponseDto
{
    public string Mode { get; set; } = "Commercial";
    public string PeriodType { get; set; } = "month";
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int? SelectedSalesPersonCode { get; set; }
    public string? SelectedSalesPersonName { get; set; }
    public CommercialReportingKpiDto Kpis { get; set; } = new();
    public CommercialReportingKpiDto PreviousKpis { get; set; } = new();
    public List<CommercialSalesPersonInfoDto> TeamMembers { get; set; } = new();
    public List<ReportingMonthlyRevenuePointDto> MonthlyRevenue { get; set; } = new();
    public List<ReportingMonthlyRevenuePointDto> MonthlyRevenuePreviousYear { get; set; } = new();
    public List<ReportingTopProductDto> TopProducts { get; set; } = new();
    public List<ReportingTopClientDto> TopClients { get; set; } = new();
    public List<ReportingTopPartnerDto> TopPartners { get; set; } = new();
    public List<CommercialSalesPersonPerformanceDto> TopCommercials { get; set; } = new();
    public List<ReportingUnpaidDto> UnpaidItems { get; set; } = new();
    public List<ReportingProductDetailDto> ProductDetails { get; set; } = new();
    public List<ReportingClientDetailDto> ClientDetails { get; set; } = new();
    public List<ReportingPartnerDetailDto> PartnerDetails { get; set; } = new();
    public ReportingPartnerFocusedReportDto? PartnerReport { get; set; }
}

public class ReportingMonthlyRevenuePointDto
{
    public string MonthKey { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class ReportingRevenueBreakdownRowDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Quantity { get; set; }
    public int DocumentsCount { get; set; }
}

public class ReportingTopProductDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public int SalesCount { get; set; }
    public int ClientsCount { get; set; }
    public int SalesPeopleCount { get; set; }
    public string MainClientName { get; set; } = string.Empty;
}

public class ReportingTopClientDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public int ContractsCount { get; set; }
    public int ProductsCount { get; set; }
    public string MainSalesPersonName { get; set; } = string.Empty;
}

public class ReportingTopPartnerDto
{
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int QuotesCount { get; set; }
    public int ProductsCount { get; set; }
    public int SalesPeopleCount { get; set; }
}

public class ReportingUnpaidDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal DueAmount { get; set; }
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int OverdueDays { get; set; }
}

public class ReportingProductDetailDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public int ClientsCount { get; set; }
    public string MainClientName { get; set; } = string.Empty;
    public decimal TrendPercent { get; set; }
}

public class ReportingClientDetailDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal PaymentRate { get; set; }
    public int ContractsCount { get; set; }
    public int ProductsCount { get; set; }
    public string FavoriteItemName { get; set; } = string.Empty;
}

public class ReportingPartnerDetailDto
{
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int QuotesCount { get; set; }
    public int ProductsCount { get; set; }
    public int SalesPeopleCount { get; set; }
    public decimal TrendPercent { get; set; }
    public decimal RoiPercent { get; set; }
}

public class ReportingPartnerFocusedReportDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public ReportingPartnerFinancialSummaryDto FinancialSummary { get; set; } = new();
    public List<ReportingPartnerDocumentDto> Documents { get; set; } = new();
    public List<ReportingTopProductDto> TopPurchasedProducts { get; set; } = new();
    public List<ReportingCategoryShareDto> CategoryShares { get; set; } = new();
    public List<ReportingMonthlyRevenuePointDto> YearlyRevenue { get; set; } = new();
}

public class ReportingPartnerFinancialSummaryDto
{
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public class ReportingPartnerDocumentDto
{
    public string Type { get; set; } = string.Empty;
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public DateTime? DocDate { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ReportingCategoryShareDto
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class PartnerDebtDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public decimal PartnerOwesCompanyAmount { get; set; }
    public decimal CompanyOwesPartnerAmount { get; set; }
    public decimal Balance { get; set; }
}

public class AdminDashboardDto
{
    public List<AdminCommercialSummaryDto> CommercialSummaries { get; set; } = new();
    public List<AdminTopPartnerDto> TopPartners { get; set; } = new();
    public List<AdminTopProductDto> TopProducts { get; set; } = new();
    public List<AdminMonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
    public decimal TotalPipelineAmount { get; set; }
    public int GlobalOverdueInvoicesCount { get; set; }
    public decimal GlobalOverdueInvoicesAmount { get; set; }
}

public class AdminCommercialSummaryDto
{
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int QuotesCount { get; set; }
    public decimal QuoteToOrderRate { get; set; }
    public decimal CollectedRevenue { get; set; }
    public int OverdueInvoicesCount { get; set; }
    public decimal OverdueInvoicesAmount { get; set; }
}

public class AdminTopPartnerDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
}

public class AdminTopProductDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class AdminMonthlyRevenueDto
{
    public string MonthKey { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PendingRevenue { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
}

public class ReportingEvolutionDto
{
    public List<ReportingEvolutionPointDto> Points { get; set; } = new();
}

public class ReportingEvolutionPointDto
{
    public string MonthKey { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PendingRevenue { get; set; }
    public decimal CollectedRevenue { get; set; }
    public int QuotesCount { get; set; }
    public int OrdersCount { get; set; }
    public int DeliveryNotesCount { get; set; }
    public decimal QuotesAmount { get; set; }
    public decimal OrdersAmount { get; set; }
    public decimal DeliveryNotesAmount { get; set; }
    public decimal InvoicesAmount { get; set; }
}

public class QuoteToRelaunchDto
{
    public int DocEntry { get; set; }
    public int DocNum { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime DocDate { get; set; }
    public int DaysSinceQuote { get; set; }
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
}
