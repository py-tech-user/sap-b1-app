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
    public decimal NetRevenue { get; set; } // CA = Factures - Avoirs
    public decimal PendingRevenue { get; set; } // BC open + BL open
    public int ActivePartnersCount { get; set; }
    public int InactivePartnersCount { get; set; }
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
}

public class ReportingMonthlyRevenuePointDto
{
    public string MonthKey { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
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

public class PartnerDebtDto
{
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public decimal PartnerOwesCompanyAmount { get; set; }
    public decimal CompanyOwesPartnerAmount { get; set; }
}
