import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReportingApiService } from '../../../core/services/reporting-api.service';
import { AdvancedDashboard } from '../../../core/models/models';
import { RevenueChartComponent } from '../revenue-chart/revenue-chart.component';
import { TopCustomersComponent } from '../top-customers/top-customers.component';
import { TopProductsComponent } from '../top-products/top-products.component';

@Component({
  selector: 'app-reporting-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, RevenueChartComponent, TopCustomersComponent, TopProductsComponent],
  template: `
    <div class="reporting-page">
      <div class="header">
        <h1>📊 Reporting avancé</h1>
      </div>

      @if (loading()) {
        <div class="loading">
          <div class="spinner"></div>
          Chargement du tableau de bord...
        </div>
      } @else {
        <!-- KPI Cards -->
        <div class="kpi-grid">
          <div class="kpi-card blue">
            <div class="kpi-icon">👥</div>
            <div class="kpi-body">
              <div class="kpi-value">{{ d().totalCustomers }}</div>
              <div class="kpi-label">Total Clients</div>
              <div class="kpi-sub">{{ d().activeCustomers }} actifs</div>
            </div>
          </div>
          <div class="kpi-card indigo">
            <div class="kpi-icon">🛒</div>
            <div class="kpi-body">
              <div class="kpi-value">{{ d().totalOrders }}</div>
              <div class="kpi-label">Total Commandes</div>
            </div>
          </div>
          <div class="kpi-card green">
            <div class="kpi-icon">💰</div>
            <div class="kpi-body">
              <div class="kpi-value">{{ d().totalRevenue | number:'1.2-2' }}</div>
              <div class="kpi-label">CA Total (MAD)</div>
            </div>
          </div>
          <div class="kpi-card teal">
            <div class="kpi-icon">📈</div>
            <div class="kpi-body">
              <div class="kpi-value">{{ d().revenueThisMonth | number:'1.2-2' }}</div>
              <div class="kpi-label">CA ce mois (MAD)</div>
              @if (d().growthPercent !== 0) {
                <div class="kpi-growth" [class.positive]="d().growthPercent > 0" [class.negative]="d().growthPercent < 0">
                  {{ d().growthPercent > 0 ? '+' : '' }}{{ d().growthPercent | number:'1.1-1' }}%
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Alert Banners -->
        <div class="alerts-row">
          @if (d().pendingPaymentsAmount > 0) {
            <a routerLink="/reporting/pending-payments" class="alert-banner yellow">
              <span class="alert-count">{{ d().pendingPaymentsAmount | number:'1.0-0' }} MAD</span>
              <span>Paiements en attente</span>
            </a>
          }
        </div>

        <!-- Revenue Chart -->
        <app-revenue-chart [externalData]="d().revenueEvolution" />

        <!-- Top 10 side by side -->
        <div class="top-grid">
          <app-top-customers [externalData]="d().topCustomers" />
          <app-top-products [externalData]="d().topProducts" />
        </div>

        <!-- Recent Orders -->
        <div class="recent-card">
          <div class="recent-header">
            <h3>🕐 Dernières commandes</h3>
            <a routerLink="/orders" class="view-all">Voir tout →</a>
          </div>
          <div class="recent-list">
            @for (o of d().recentOrders; track o.id) {
              <div class="recent-item">
                <div class="recent-left">
                  <strong>{{ o.docNum }}</strong>
                  <span class="recent-customer">{{ o.customerName }}</span>
                </div>
                <div class="recent-right">
                  <span class="recent-amount">{{ o.docTotal | number:'1.2-2' }} MAD</span>
                  <span class="recent-date">{{ o.docDate | date:'dd/MM/yyyy' }}</span>
                  <span class="status-badge" [class]="'status-' + o.status.toLowerCase()">{{ o.status }}</span>
                </div>
              </div>
            } @empty {
              <div class="empty">Aucune commande récente.</div>
            }
          </div>
        </div>
      }

      @if (errorMsg()) {
        <div class="error-banner">❌ {{ errorMsg() }}</div>
      }
    </div>
  `,
  styles: [`
    .reporting-page { display: flex; flex-direction: column; gap: 1.25rem; max-width: 1200px; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0; }

    .loading {
      display: flex; flex-direction: column; align-items: center; gap: 1rem;
      padding: 4rem; color: #888; font-size: 1rem;
    }
    .spinner {
      width: 36px; height: 36px; border: 3px solid #e0e0e0;
      border-top-color: #667eea; border-radius: 50%; animation: spin 0.7s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }

    /* KPI Grid */
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
    .kpi-card {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
      display: flex; align-items: center; gap: 1rem;
      border-left: 4px solid #667eea;
    }
    .kpi-card.blue   { border-left-color: #3498db; }
    .kpi-card.indigo { border-left-color: #667eea; }
    .kpi-card.green  { border-left-color: #27ae60; }
    .kpi-card.teal   { border-left-color: #1abc9c; }
    .kpi-icon { font-size: 2rem; }
    .kpi-body { display: flex; flex-direction: column; }
    .kpi-value { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; }
    .kpi-label { font-size: 0.82rem; color: #888; }
    .kpi-sub { font-size: 0.78rem; color: #aaa; }
    .kpi-growth { font-size: 0.82rem; font-weight: 600; }
    .kpi-growth.positive { color: #27ae60; }
    .kpi-growth.negative { color: #e74c3c; }

    /* Alerts */
    .alerts-row { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .alert-banner {
      display: flex; align-items: center; gap: 0.5rem; padding: 0.65rem 1rem;
      border-radius: 8px; font-size: 0.9rem; font-weight: 500; text-decoration: none;
      cursor: pointer; transition: transform 0.15s;
    }
    .alert-banner:hover { transform: translateY(-1px); }
    .alert-banner.orange { background: #fff3e0; color: #e65100; }
    .alert-banner.red    { background: #fce4ec; color: #c62828; }
    .alert-banner.yellow { background: #fffde7; color: #f57f17; }
    .alert-count { font-weight: 700; font-size: 1.1rem; }

    /* Top Grid */
    .top-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    @media (max-width: 900px) { .top-grid { grid-template-columns: 1fr; } }

    /* Recent Orders */
    .recent-card {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .recent-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.75rem; }
    .recent-header h3 { margin: 0; font-size: 1.05rem; color: #1e2a3a; }
    .view-all { color: #667eea; text-decoration: none; font-size: 0.85rem; }
    .view-all:hover { text-decoration: underline; }

    .recent-list { max-height: 320px; overflow-y: auto; }
    .recent-item {
      display: flex; justify-content: space-between; align-items: center;
      padding: 0.65rem 0; border-bottom: 1px solid #f0f0f0;
    }
    .recent-item:last-child { border-bottom: none; }
    .recent-left { display: flex; flex-direction: column; gap: 2px; }
    .recent-left strong { font-size: 0.9rem; color: #1e2a3a; }
    .recent-customer { font-size: 0.8rem; color: #888; }
    .recent-right { display: flex; align-items: center; gap: 0.75rem; }
    .recent-amount { font-weight: 600; font-size: 0.9rem; color: #1e2a3a; }
    .recent-date { font-size: 0.8rem; color: #999; }
    .status-badge {
      padding: 0.15rem 0.5rem; border-radius: 12px; font-size: 0.72rem; font-weight: 500;
    }
    .status-pending    { background: #fff3e0; color: #e65100; }
    .status-confirmed  { background: #e3f2fd; color: #1565c0; }
    .status-delivered  { background: #e8f5e9; color: #2e7d32; }
    .status-cancelled  { background: #fce4ec; color: #c62828; }

    .empty { text-align: center; color: #999; padding: 1.5rem; font-size: 0.9rem; }
    .error-banner {
      background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb;
      padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem;
    }
  `]
})
export class ReportingDashboardComponent implements OnInit {
  private reportingApi = inject(ReportingApiService);

  private readonly emptyDashboard: AdvancedDashboard = {
    totalCustomers: 0, activeCustomers: 0, totalOrders: 0, totalRevenue: 0,
    revenueThisMonth: 0, growthPercent: 0, pendingOrdersCount: 0,
    lateOrdersCount: 0, pendingPaymentsAmount: 0,
    topCustomers: [], topProducts: [], revenueEvolution: [],
    recentOrders: [], lateOrders: [], pendingPayments: []
  };

  d = signal<AdvancedDashboard>(this.emptyDashboard);
  loading = signal(true);
  errorMsg = signal('');

  ngOnInit(): void {
    this.reportingApi.getDashboard().subscribe({
      next: (res) => {
        if (res.data) this.d.set(res.data);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err?.error?.message || 'Impossible de charger le reporting.');
        this.loading.set(false);
      }
    });
  }
}
