import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { ReportingApiService, CommercialReportingPayload, PartnerDebtItem } from '../../core/services/reporting-api.service';
import { environment } from '../../../environments/environment';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="dashboard">
      <div class="header">
        <div class="header-left">
          <h1>Tableau de bord</h1>
          <label class="month-filter">
            Mois
            <input type="month" [value]="month()" (change)="onMonthChange($event)" />
          </label>
          @if (isAdminMode() && visibleTeamMembers().length) {
            <label class="month-filter">
              Commercial
              <select [(ngModel)]="selectedSalesPersonCode" (change)="load()">
                <option [ngValue]="0">Toute l'equipe</option>
                @for (sp of visibleTeamMembers(); track sp.salesPersonCode) {
                  <option [ngValue]="sp.salesPersonCode">{{ sp.salesPersonName }}</option>
                }
              </select>
            </label>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="loading">Chargement des statistiques...</div>
      }

      <div class="stats-grid">
        <a routerLink="/customers" class="stat-card">
          <span class="stat-value">{{ partnersCount() }}</span>
          <span class="stat-label">Partenaires</span>
        </a>

        <a [routerLink]="['/dashboard/documents']" [queryParams]="{ type: 'quotes', month: month(), salesPersonCode: selectedSalesPersonCode || null }" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.quotesCount ?? 0 }}</span>
          <span class="stat-label">Devis</span>
          <span class="stat-sub">{{ formatMoney(report()?.kpis?.quotesAmount ?? 0) }}</span>
        </a>

        <a [routerLink]="['/dashboard/documents']" [queryParams]="{ type: 'orders', month: month(), salesPersonCode: selectedSalesPersonCode || null }" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.ordersCount ?? 0 }}</span>
          <span class="stat-label">Commandes</span>
          <span class="stat-sub">CA: {{ formatMoney(report()?.kpis?.ordersAmount ?? 0) }}</span>
        </a>

        <a [routerLink]="['/dashboard/documents']" [queryParams]="{ type: 'deliverynotes', month: month(), salesPersonCode: selectedSalesPersonCode || null }" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.deliveryNotesCount ?? 0 }}</span>
          <span class="stat-label">Bons de livraison</span>
          <span class="stat-sub">CA: {{ formatMoney(report()?.kpis?.deliveryNotesAmount ?? 0) }}</span>
        </a>

        <a [routerLink]="['/dashboard/documents']" [queryParams]="{ type: 'invoices', month: month(), salesPersonCode: selectedSalesPersonCode || null }" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.invoicesCount ?? 0 }}</span>
          <span class="stat-label">Factures</span>
          <span class="stat-sub">CA: {{ formatMoney(report()?.kpis?.invoicesAmount ?? 0) }}</span>
        </a>

        <a routerLink="/products" class="stat-card">
          <span class="stat-value">{{ catalogCount() }}</span>
          <span class="stat-label">Catalogue</span>
        </a>

        @if (isAdminMode()) {
          <a class="stat-card" [routerLink]="['/dashboard/commercials-performance']" [queryParams]="{ month: month() }">
            <span class="stat-value">{{ report()?.teamMembers?.length ?? 0 }}</span>
            <span class="stat-label">Commerciaux</span>
          </a>
        }
        <a class="stat-card" [routerLink]="['/dashboard/partners-activity']" [queryParams]="{ month: month(), startDate: monthStartIso(), endDate: monthEndIso(), activity: 'active', salesPersonCode: selectedSalesPersonCode || null }">
          <span class="stat-value">{{ report()?.kpis?.activePartnersCount ?? 0 }}</span>
          <span class="stat-label">Partenaires actifs</span>
        </a>

        <a class="stat-card" [routerLink]="['/dashboard/partners-activity']" [queryParams]="{ month: month(), startDate: monthStartIso(), endDate: monthEndIso(), activity: 'inactive', salesPersonCode: selectedSalesPersonCode || null }">
          <span class="stat-value">{{ report()?.kpis?.inactivePartnersCount ?? 0 }}</span>
          <span class="stat-label">Partenaires inactifs</span>
        </a>

        <a class="stat-card">
          <span class="stat-value">{{ formatMoney(report()?.kpis?.netRevenue ?? 0) }}</span>
          <span class="stat-label">CA net (Facture - Avoir)</span>
        </a>

        <a class="stat-card">
          <span class="stat-value">{{ formatMoney(report()?.kpis?.pendingRevenue ?? 0) }}</span>
          <span class="stat-label">CA en attente (BC + BL ouverts)</span>
        </a>
      </div>

      <div class="transform-grid">
        <article class="transform-card">
          <h3>Transformation Devis → BC</h3>
          <p>{{ formatPct(report()?.kpis?.quoteToOrderRate ?? report()?.kpis?.conversionRate ?? 0) }}</p>
        </article>
        <article class="transform-card">
          <h3>Transformation BC → BL</h3>
          <p>{{ formatPct(report()?.kpis?.orderToDeliveryRate ?? 0) }}</p>
        </article>
        <article class="transform-card">
          <h3>Transformation BL → Facture</h3>
          <p>{{ formatPct(report()?.kpis?.deliveryToInvoiceRate ?? 0) }}</p>
        </article>
      </div>

      <section class="debts-card">
        <h2>Dettes partenaires</h2>
        <div class="debts-filters">
          <label>
            Rechercher partenaire
            <input
              type="text"
              list="partner-debt-suggestions"
              [ngModel]="partnerDebtSearch()"
              (ngModelChange)="onPartnerDebtSearchChange($event)"
              placeholder="Code ou nom partenaire"
            />
            <datalist id="partner-debt-suggestions">
              @for (s of partnerDebtSuggestions(); track s) {
                <option [value]="s"></option>
              }
            </datalist>
          </label>
        </div>
        <table>
          <thead>
            <tr>
              @if (isAdminMode()) { <th>Commercial</th> }
              <th>Code partenaire</th>
              <th>Nom partenaire</th>
              <th>Partenaire doit a l'entreprise</th>
              <th>L'entreprise doit au partenaire</th>
            </tr>
          </thead>
          <tbody>
            @for (row of filteredPartnerDebts(); track row.cardCode + '-' + row.salesPersonCode) {
              <tr>
                @if (isAdminMode()) { <td data-label="Commercial">{{ row.salesPersonName || ('#' + row.salesPersonCode) }}</td> }
                <td data-label="Code partenaire">{{ row.cardCode }}</td>
                <td data-label="Nom partenaire">{{ row.cardName }}</td>
                <td data-label="Partenaire doit a l'entreprise">{{ formatMoney(row.partnerOwesCompanyAmount) }}</td>
                <td data-label="L'entreprise doit au partenaire">{{ formatMoney(row.companyOwesPartnerAmount) }}</td>
              </tr>
            } @empty {
              <tr><td [attr.colspan]="isAdminMode() ? 5 : 4">Aucune dette en cours.</td></tr>
            }
          </tbody>
        </table>
      </section>
    </div>
  `,
  styles: [`
    .dashboard { display: grid; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: end; gap: 1rem; flex-wrap: wrap; }
    .header-left { display: flex; align-items: end; gap: .75rem; flex-wrap: wrap; }
    .month-filter { display: grid; gap: .35rem; font-weight: 600; }
    .month-filter input { border: 1px solid #d0d7de; border-radius: 8px; padding: .45rem .6rem; }
    .loading { text-align: center; padding: 1rem; color: #666; }
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: .85rem; }
    .stat-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .9rem; display: grid; gap: .2rem; text-decoration: none; color: inherit; }
    .stat-value { font-size: 1.45rem; font-weight: 700; color: #111827; }
    .stat-label { color: #374151; font-size: .92rem; }
    .stat-sub { color: #6b7280; font-size: .85rem; }
    .transform-grid { display: grid; grid-template-columns: repeat(3, minmax(180px, 1fr)); gap: .85rem; }
    .transform-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .85rem; }
    .transform-card h3 { margin: 0 0 .35rem; font-size: .9rem; color: #4b5563; }
    .transform-card p { margin: 0; font-size: 1.2rem; font-weight: 700; color: #111827; }
    .debts-card { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .85rem; }
    .debts-filters { margin: .35rem 0 .6rem; max-width: 420px; }
    .debts-filters label { display: grid; gap: .3rem; font-weight: 600; color: #374151; }
    .debts-filters input { border: 1px solid #d1d5db; border-radius: 8px; padding: .45rem .6rem; }
    .debts-card table { width: 100%; border-collapse: collapse; margin-top: .5rem; }
    .debts-card th, .debts-card td { border-bottom: 1px solid #edf0f4; padding: .5rem; text-align: left; }
    .debts-card th { background: #f8fafc; color: #475569; }
    @media (max-width: 900px) {
      .transform-grid { grid-template-columns: 1fr; }
      .header-left { width: 100%; }
      .month-filter { width: 100%; }
      .month-filter input, .month-filter select { width: 100%; }
      .stats-grid { grid-template-columns: 1fr; }
      .stat-card { padding: .8rem; }
      .stat-value { font-size: 1.2rem; }
      .debts-card table, .debts-card thead, .debts-card tbody, .debts-card tr, .debts-card td { display: block; width: 100%; }
      .debts-card thead { display: none; }
      .debts-card tr { border-bottom: 1px solid #e5e7eb; padding: .45rem 0; }
      .debts-card td { border: 0; display: flex; justify-content: space-between; gap: .75rem; padding: .35rem 0; }
      .debts-card td::before { content: attr(data-label); color: #64748b; font-weight: 700; }
    }
  `]
})
export class DashboardComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly reportingApi = inject(ReportingApiService);
  private readonly api = environment.apiUrl;
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly month = signal(this.defaultMonth());
  selectedSalesPersonCode = 0;
  readonly partnerDebtSearch = signal('');
  readonly report = signal<CommercialReportingPayload | null>(null);
  readonly partnerDebts = signal<PartnerDebtItem[]>([]);
  readonly filteredPartnerDebts = computed(() => {
    const q = this.partnerDebtSearch().trim().toLowerCase();
    if (!q) return this.partnerDebts();
    return this.partnerDebts().filter((row) =>
      String(row.cardCode ?? '').toLowerCase().includes(q) ||
      String(row.cardName ?? '').toLowerCase().includes(q)
    );
  });
  readonly partnerDebtSuggestions = computed(() =>
    this.partnerDebts()
      .flatMap((row) => [
        String(row.cardCode ?? ''),
        String(row.cardName ?? '')
      ])
      .filter(Boolean)
      .slice(0, 150)
  );
  readonly visibleTeamMembers = computed(() =>
    (this.report()?.teamMembers ?? []).filter(sp => {
      const name = String(sp.salesPersonName ?? '').trim().toLowerCase();
      return name !== 'administrateur';
    })
  );
  readonly partnersCount = signal(0);
  readonly catalogCount = signal(0);
  readonly isAdminMode = signal(false);

  ngOnInit(): void {
    this.isAdminMode.set(['Admin', 'Manager'].includes(this.auth.role()));
    this.load();
  }

  onMonthChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.month.set(input.value || this.defaultMonth());
    this.load();
  }

  load(): void {
    this.loading.set(true);

    forkJoin({
      reporting: this.reportingApi.getCommercialReporting(
        this.month(),
        this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined
      ),
      partnerDebts: this.reportingApi.getPartnerDebts(
        this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined
      ),
      customers: this.http.get<any>(`${this.api}/sap/partners?page=1&pageSize=1`),
      products: this.http.get<any>(`${this.api}/sap/items?page=1&pageSize=1`)
    }).subscribe({
      next: ({ reporting, partnerDebts, customers, products }) => {
        this.report.set(reporting.data);
        this.partnerDebts.set(partnerDebts.data ?? []);
        this.partnersCount.set(this.extractTotal(customers));
        this.catalogCount.set(this.extractTotal(products));
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  formatMoney(value: number): string {
    return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(Number(value || 0))} MAD`;
  }

  formatPct(value: number): string {
    return `${Number(value || 0).toFixed(2)}%`;
  }

  onPartnerDebtSearchChange(value: string): void {
    this.partnerDebtSearch.set(String(value ?? ''));
  }

  private extractTotal(res: any): number {
    if (typeof res?.totalCount === 'number') return res.totalCount;
    if (typeof res?.TotalCount === 'number') return res.TotalCount;
    if (typeof res?.data?.totalCount === 'number') return res.data.totalCount;
    if (Array.isArray(res?.data?.items)) return res.data.items.length;
    if (Array.isArray(res?.value)) return res.value.length;
    return 0;
  }

  private defaultMonth(): string {
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    return `${y}-${m}`;
  }

  monthStartIso(): string {
    const [y, m] = this.month().split('-').map(Number);
    const start = new Date(y, (m || 1) - 1, 1, 0, 0, 0, 0);
    return start.toISOString();
  }

  monthEndIso(): string {
    const [y, m] = this.month().split('-').map(Number);
    const end = new Date(y, (m || 1), 1, 0, 0, 0, 0);
    return end.toISOString();
  }
}
