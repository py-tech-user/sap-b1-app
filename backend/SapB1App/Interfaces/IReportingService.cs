using SapB1App.DTOs;

namespace SapB1App.Interfaces;

public interface IReportingService
{
    // ── Dashboard principal ────────────────────────────────────────────────
    
    /// <summary>
    /// Récupère le tableau de bord avancé complet.
    /// </summary>
    Task<AdvancedDashboardDto> GetAdvancedDashboardAsync(ReportFilterDto? filter = null);
    
    // ── Top 10 Clients ─────────────────────────────────────────────────────
    
    /// <summary>
    /// Récupère les top clients par chiffre d'affaires.
    /// </summary>
    Task<List<TopCustomerDto>> GetTopCustomersAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null);
    
    // ── Top Articles vendus ────────────────────────────────────────────────
    
    /// <summary>
    /// Récupère les articles les plus vendus.
    /// </summary>
    Task<List<TopProductDto>> GetTopProductsAsync(int limit = 10, DateTime? startDate = null, DateTime? endDate = null);
    
    // ── Évolution du chiffre d'affaires ────────────────────────────────────
    
    /// <summary>
    /// Récupère l'évolution du chiffre d'affaires par période.
    /// </summary>
    Task<RevenueReportDto> GetRevenueEvolutionAsync(string period = "month", DateTime? startDate = null, DateTime? endDate = null);
    
    // ── Encaissements en attente ───────────────────────────────────────────
    
    /// <summary>
    /// Récupère les commandes avec paiements en attente.
    /// </summary>
    Task<PendingPaymentsReportDto> GetPendingPaymentsAsync(int? customerId = null);
    
    // ── Commandes en retard ────────────────────────────────────────────────
    
    /// <summary>
    /// Récupère les commandes en retard de livraison.
    /// </summary>
    Task<LateOrdersReportDto> GetLateOrdersAsync(int? daysThreshold = null);
    
    // ── Statistiques générales ─────────────────────────────────────────────
    
    /// <summary>
    /// Récupère les KPIs du jour.
    /// </summary>
    Task<DashboardDto> GetDailyKPIsAsync();
}
