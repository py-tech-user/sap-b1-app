import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdvancedDashboard, LateOrder, PendingPayment } from '../../../core/models/models';
import { ReportingApiService } from '../../../core/services/reporting-api.service';
import { RevenueChartComponent } from '../revenue-chart/revenue-chart.component';
import { TopCustomersComponent } from '../top-customers/top-customers.component';
import { TopProductsComponent } from '../top-products/top-products.component';

@Component({
  selector: 'app-reporting-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, RevenueChartComponent, TopCustomersComponent, TopProductsComponent],
  template: `
    <section class="reporting-page">
      <header class="page-header">
        <div>
          <h1>Reporting Commercial</h1>
          <p class="muted">Vue globale des ventes, impayes et retards.</p>
        </div>
        <button class="btn" type="button" (click)="reload()" [disabled]="loading()">Actualiser</button>
      </header>

      <section class="filters">
        <label>
          Recherche client dynamique
          <input
            type="text"
            [ngModel]="searchText()"
            (ngModelChange)="onClientSearchInput($event)"
            list="reporting-client-options"
            placeholder="Rechercher et selectionner un client" />
          <datalist id="reporting-client-options">
            @for (client of clientOptions(); track client) {
              <option [value]="client"></option>
            }
          </datalist>
        </label>
      </section>

      @if (loading()) {
        <div class="loading">Chargement du reporting...</div>
      } @else {
        <section class="kpi-grid">
          <article class="kpi-card">
            <span class="kpi-label">Clients actifs</span>
            <strong class="kpi-value">{{ d().activeCustomers }}</strong>
            <small>{{ d().totalCustomers }} clients au total</small>
          </article>
          <article class="kpi-card">
            <span class="kpi-label">Commandes</span>
            <strong class="kpi-value">{{ d().totalOrders }}</strong>
            <small>{{ d().pendingOrdersCount }} en attente</small>
          </article>
          <article class="kpi-card">
            <span class="kpi-label">CA total</span>
            <strong class="kpi-value">{{ d().totalRevenue | number:'1.2-2' }} MAD</strong>
            <small>Ce mois: {{ d().revenueThisMonth | number:'1.2-2' }} MAD</small>
          </article>
          <article class="kpi-card">
            <span class="kpi-label">Impayes</span>
            <strong class="kpi-value">{{ d().pendingPaymentsAmount | number:'1.2-2' }} MAD</strong>
            <small>{{ filteredPendingPayments().length }} lignes</small>
          </article>
        </section>

        <section class="alerts">
          <a routerLink="/reporting/pending-payments" class="alert warning">
            Paiements en attente: {{ filteredPendingPayments().length }}
          </a>
          <a routerLink="/reporting/late-orders" class="alert danger">
            Commandes en retard: {{ filteredLateOrders().length }}
          </a>
        </section>

        <app-revenue-chart [externalData]="d().revenueEvolution" />

        <section class="top-grid">
          <app-top-customers [externalData]="d().topCustomers" />
          <app-top-products [externalData]="d().topProducts" />
        </section>

        <article class="card">
          <header class="card-head">
            <h3>Top 5 commerciaux</h3>
          </header>
          <table>
            <thead>
              <tr>
                <th>Commercial</th>
                <th class="num">Documents</th>
                <th class="num">CA</th>
              </tr>
            </thead>
            <tbody>
              @for (c of d().topSalesPersons; track c.salesPersonCode) {
                <tr>
                  <td>{{ c.salesPersonName }}</td>
                  <td class="num">{{ c.documentCount }}</td>
                  <td class="num">{{ c.totalRevenue | number:'1.2-2' }} MAD</td>
                </tr>
              } @empty {
                <tr><td colspan="3" class="empty">Aucune donnee.</td></tr>
              }
            </tbody>
          </table>
        </article>

        <section class="tables-grid">
          <article class="card">
            <header class="card-head">
              <h3>Paiements en attente</h3>
              <a routerLink="/reporting/pending-payments">Voir detail</a>
            </header>
            <table>
              <thead>
                <tr>
                  <th>Document</th>
                  <th>Client</th>
                  <th class="num">Reste</th>
                  <th>Retard</th>
                </tr>
              </thead>
              <tbody>
                @for (p of filteredPendingPayments(); track p.orderId) {
                  <tr>
                    <td>{{ p.docNum }}</td>
                    <td>{{ p.customerName }}</td>
                    <td class="num">{{ p.remainingAmount | number:'1.2-2' }}</td>
                    <td>{{ p.daysOverdue }}j</td>
                  </tr>
                } @empty {
                  <tr><td colspan="4" class="empty">Aucune ligne.</td></tr>
                }
              </tbody>
            </table>
          </article>

          <article class="card">
            <header class="card-head">
              <h3>Commandes en retard</h3>
              <a routerLink="/reporting/late-orders">Voir detail</a>
            </header>
            <table>
              <thead>
                <tr>
                  <th>Document</th>
                  <th>Client</th>
                  <th class="num">Montant</th>
                  <th>Retard</th>
                </tr>
              </thead>
              <tbody>
                @for (o of filteredLateOrders(); track o.orderId) {
                  <tr>
                    <td>{{ o.docNum }}</td>
                    <td>{{ o.customerName }}</td>
                    <td class="num">{{ o.total | number:'1.2-2' }}</td>
                    <td>{{ o.daysLate }}j</td>
                  </tr>
                } @empty {
                  <tr><td colspan="4" class="empty">Aucune ligne.</td></tr>
                }
              </tbody>
            </table>
          </article>
        </section>
      }

      @if (errorMsg()) {
        <p class="error">{{ errorMsg() }}</p>
      }
    </section>
  `,
  styles: [`
    .reporting-page { display: grid; gap: 1rem; max-width: 1280px; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; }
    .page-header h1 { margin: 0; font-size: 1.5rem; color: #1f2937; }
    .muted { margin: 0.25rem 0 0; color: #6b7280; font-size: 0.9rem; }
    .btn { border: 1px solid #d1d5db; background: #fff; border-radius: 8px; padding: 0.5rem 0.8rem; cursor: pointer; }
    .btn:disabled { opacity: 0.6; cursor: not-allowed; }
    .filters { display: grid; grid-template-columns: 1fr; gap: 0.75rem; }
    .filters label { display: grid; gap: 0.35rem; font-size: 0.82rem; color: #374151; }
    .filters input { border: 1px solid #d1d5db; border-radius: 8px; padding: 0.5rem 0.65rem; font-size: 0.9rem; background: #fff; }
    .kpi-grid { display: grid; grid-template-columns: repeat(4, minmax(180px, 1fr)); gap: 0.75rem; }
    .kpi-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 0.9rem; display: grid; gap: 0.25rem; }
    .kpi-label { color: #6b7280; font-size: 0.8rem; }
    .kpi-value { color: #111827; font-size: 1.2rem; }
    .kpi-card small { color: #6b7280; }
    .alerts { display: flex; gap: 0.75rem; flex-wrap: wrap; }
    .alert { text-decoration: none; padding: 0.45rem 0.7rem; border-radius: 8px; font-size: 0.86rem; }
    .alert.warning { background: #fff7ed; color: #9a3412; border: 1px solid #fdba74; }
    .alert.danger { background: #fef2f2; color: #991b1b; border: 1px solid #fca5a5; }
    .top-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .tables-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem; }
    .card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 0.9rem; }
    .card-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.6rem; }
    .card-head h3 { margin: 0; font-size: 1rem; color: #111827; }
    .card-head a { color: #2563eb; text-decoration: none; font-size: 0.85rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border-bottom: 1px solid #f3f4f6; padding: 0.5rem 0.35rem; font-size: 0.85rem; text-align: left; }
    th { color: #6b7280; font-weight: 600; font-size: 0.78rem; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .empty { text-align: center; color: #9ca3af; padding: 1rem; }
    .loading { padding: 2rem; text-align: center; color: #6b7280; background: #fff; border-radius: 10px; border: 1px solid #e5e7eb; }
    .error { color: #b91c1c; background: #fef2f2; border: 1px solid #fecaca; border-radius: 8px; padding: 0.6rem; }
    @media (max-width: 1024px) {
      .kpi-grid { grid-template-columns: 1fr 1fr; }
      .top-grid, .tables-grid { grid-template-columns: 1fr; }
    }
    @media (max-width: 640px) {
      .kpi-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class ReportingDashboardComponent implements OnInit {
  private readonly reportingApi = inject(ReportingApiService);

  private readonly emptyDashboard: AdvancedDashboard = {
    totalCustomers: 0,
    activeCustomers: 0,
    totalOrders: 0,
    totalRevenue: 0,
    revenueThisMonth: 0,
    growthPercent: 0,
    pendingOrdersCount: 0,
    lateOrdersCount: 0,
    pendingPaymentsAmount: 0,
    topCustomers: [],
    topProducts: [],
    topSalesPersons: [],
    revenueEvolution: [],
    recentOrders: [],
    lateOrders: [],
    pendingPayments: []
  };

  d = signal<AdvancedDashboard>(this.emptyDashboard);
  loading = signal(true);
  errorMsg = signal('');
  searchText = signal('');
  clientOptions = computed(() => {
    const set = new Set<string>();
    for (const c of this.d().topCustomers) {
      const code = String(c.cardCode ?? '').trim();
      const name = String(c.cardName ?? '').trim();
      if (name && code) set.add(`${name} (${code})`);
      else if (name) set.add(name);
      else if (code) set.add(code);
    }
    for (const p of this.d().pendingPayments) {
      const name = String(p.customerName ?? '').trim();
      const code = String(p.customerId ?? '').trim();
      if (name && code) set.add(`${name} (${code})`);
      else if (name) set.add(name);
      else if (code) set.add(code);
    }
    for (const o of this.d().lateOrders) {
      const name = String(o.customerName ?? '').trim();
      const code = '';
      if (name && code) set.add(`${name} (${code})`);
      else if (name) set.add(name);
      else if (code) set.add(code);
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b, 'fr', { sensitivity: 'base' }));
  });

  readonly filteredPendingPayments = computed<PendingPayment[]>(() => {
    const q = this.normalizeSearchToken(this.searchText());
    return this.d().pendingPayments.filter((x) => {
      if (!q) return true;
      const name = this.normalizeSearchToken(x.customerName);
      const code = this.normalizeSearchToken(x.customerId);
      return name.includes(q) || code.includes(q) || `${name} (${code})`.includes(q);
    });
  });

  readonly filteredLateOrders = computed<LateOrder[]>(() => {
    const q = this.normalizeSearchToken(this.searchText());
    return this.d().lateOrders.filter((x) => {
      if (!q) return true;
      const name = this.normalizeSearchToken(x.customerName);
      const code = '';
      return name.includes(q) || code.includes(q) || `${name} (${code})`.includes(q);
    });
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.errorMsg.set('');
    this.reportingApi.getDashboard().subscribe({
      next: (res) => {
        this.d.set(res.data ?? this.emptyDashboard);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMsg.set(err?.error?.message || 'Impossible de charger le reporting.');
        this.d.set(this.emptyDashboard);
        this.loading.set(false);
      }
    });
  }

  onClientSearchInput(value: string): void {
    this.searchText.set(String(value ?? '').trim());
  }

  private normalizeSearchToken(value: unknown): string {
    return String(value ?? '').trim().toLowerCase();
  }
}
