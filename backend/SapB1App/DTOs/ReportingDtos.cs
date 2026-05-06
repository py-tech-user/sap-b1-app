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
    public int InvoicesCount { get; set; }
    public decimal InvoicesAmount { get; set; }
    public int UnpaidInvoicesCount { get; set; }
    public decimal UnpaidInvoicesAmount { get; set; }
    public decimal ConversionRate { get; set; }
}

public class CommercialSalesPersonPerformanceDto
{
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
    public int QuotesCount { get; set; }
    public decimal QuotesAmount { get; set; }
    public int OrdersCount { get; set; }
    public decimal OrdersAmount { get; set; }
    public int InvoicesCount { get; set; }
    public decimal InvoicesAmount { get; set; }
    public int UnpaidInvoicesCount { get; set; }
    public decimal UnpaidInvoicesAmount { get; set; }
    public decimal ConversionRate { get; set; }
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
}

public class CommercialSalesPersonInfoDto
{
    public int SalesPersonCode { get; set; }
    public string SalesPersonName { get; set; } = string.Empty;
}
