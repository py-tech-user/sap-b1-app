import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { ReportingApiService, CommercialReportingPayload, PartnerDebtItem } from '../../core/services/reporting-api.service';
import { environment } from '../../../environments/environment';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';

type SortDirection = 'none' | 'asc' | 'desc';
type PartnerDebtSortKey = 'salesPersonName' | 'cardCode' | 'cardName' | 'partnerOwesCompanyAmount' | 'companyOwesPartnerAmount';
type ModePeriode = 'month' | 'quarter' | 'year' | 'custom';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="dashboard">
      <div class="header">
        <h1>Tableau de bord</h1>
      </div>

      <section class="filters-panel">
        <label class="filter-field">
          Période
          <select [(ngModel)]="modePeriode" (change)="load()">
            <option value="month">Mois</option>
            <option value="quarter">Trimestre</option>
            <option value="year">Année</option>
            <option value="custom">Personnalisée</option>
          </select>
        </label>
        @if (modePeriode === 'month') {
          <label class="filter-field">
            Mois
            <input type="month" [(ngModel)]="mois" (change)="load()" />
          </label>
        }
        @if (modePeriode === 'quarter') {
          <label class="filter-field">
            Trimestre
            <select [(ngModel)]="trimestre" (change)="load()">
              <option [ngValue]="1">Premier trimestre</option>
              <option [ngValue]="2">Deuxième trimestre</option>
              <option [ngValue]="3">Troisième trimestre</option>
              <option [ngValue]="4">Quatrième trimestre</option>
            </select>
          </label>
          <label class="filter-field">
            Année
            <input type="number" [(ngModel)]="annee" (change)="load()" />
          </label>
        }
        @if (modePeriode === 'year') {
          <label class="filter-field">
            Année
            <input type="number" [(ngModel)]="annee" (change)="load()" />
          </label>
        }
        @if (modePeriode === 'custom') {
          <label class="filter-field">
            Date début
            <input type="date" [(ngModel)]="dateDebut" (change)="load()" />
          </label>
          <label class="filter-field">
            Date fin
            <input type="date" [(ngModel)]="dateFin" (change)="load()" />
          </label>
        }
        @if (isAdminMode() && visibleTeamMembers().length) {
          <label class="filter-field">
            Commercial
            <select [(ngModel)]="selectedSalesPersonCode" (change)="load()">
              <option [ngValue]="0">Toute l'equipe</option>
              @for (sp of visibleTeamMembers(); track sp.salesPersonCode) {
                <option [ngValue]="sp.salesPersonCode">{{ sp.salesPersonName }}</option>
              }
            </select>
          </label>
        }
      </section>

      @if (loading()) {
        <div class="loading">Chargement des statistiques...</div>
      }

      <div class="stats-grid">
        <a routerLink="/customers" class="stat-card">
          <span class="stat-value">{{ partnersCount() }}</span>
          <span class="stat-label">Partenaires</span>
        </a>

        <a routerLink="/quotes" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.quotesCount ?? 0 }}</span>
          <span class="stat-label">Devis</span>
          <span class="stat-sub">{{ formatMoney(report()?.kpis?.quotesAmount ?? 0) }}</span>
        </a>

        <a routerLink="/orders" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.ordersCount ?? 0 }}</span>
          <span class="stat-label">Bon de commande</span>
          <span class="stat-sub">CA: {{ formatMoney(report()?.kpis?.ordersAmount ?? 0) }}</span>
        </a>

        <a routerLink="/deliverynotes" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.deliveryNotesCount ?? 0 }}</span>
          <span class="stat-label">Bon de livraison</span>
          <span class="stat-sub">CA: {{ formatMoney(report()?.kpis?.deliveryNotesAmount ?? 0) }}</span>
        </a>

        <a routerLink="/factures" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.invoicesCount ?? 0 }}</span>
          <span class="stat-label">Facture</span>
          <span class="stat-sub">CA: {{ formatMoney(report()?.kpis?.invoicesAmount ?? 0) }}</span>
        </a>

        <a routerLink="/creditnotes" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.creditNotesCount ?? 0 }}</span>
          <span class="stat-label">Avoir</span>
          <span class="stat-sub">{{ formatMoney(report()?.kpis?.creditNotesAmount ?? 0) }}</span>
        </a>

        <a routerLink="/returns" class="stat-card">
          <span class="stat-value">{{ report()?.kpis?.returnsCount ?? 0 }}</span>
          <span class="stat-label">Retour</span>
          <span class="stat-sub">{{ formatMoney(report()?.kpis?.returnsAmount ?? 0) }}</span>
        </a>

        <a routerLink="/products" class="stat-card">
          <span class="stat-value">{{ catalogCount() }}</span>
          <span class="stat-label">Catalogue</span>
        </a>

        @if (isAdminMode()) {
          <a class="stat-card" [routerLink]="['/dashboard/commercials-performance']" [queryParams]="{ month: monthKeyForLinks() }">
            <span class="stat-value">{{ report()?.teamMembers?.length ?? 0 }}</span>
            <span class="stat-label">Commerciaux</span>
          </a>
        }
        <div class="stat-card">
          <span class="stat-value">{{ formatMoney(report()?.kpis?.netRevenue ?? 0) }}</span>
          <span class="stat-label">CA net (Facture - Avoir)</span>
        </div>

        <div class="stat-card">
          <span class="stat-value">{{ formatMoney(report()?.kpis?.pendingRevenue ?? 0) }}</span>
          <span class="stat-label">CA en attente (BC + BL ouverts)</span>
        </div>
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
              @if (isAdminMode()) { <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('salesPersonName')">Commercial {{ sortIndicator(partnerDebtSortKey(), partnerDebtSortDirection(), 'salesPersonName') }}</button></th> }
              <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('cardCode')">Code partenaire {{ sortIndicator(partnerDebtSortKey(), partnerDebtSortDirection(), 'cardCode') }}</button></th>
              <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('cardName')">Nom partenaire {{ sortIndicator(partnerDebtSortKey(), partnerDebtSortDirection(), 'cardName') }}</button></th>
              <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('partnerOwesCompanyAmount')">Partenaire doit a l'entreprise {{ sortIndicator(partnerDebtSortKey(), partnerDebtSortDirection(), 'partnerOwesCompanyAmount') }}</button></th>
              <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('companyOwesPartnerAmount')">L'entreprise doit au partenaire {{ sortIndicator(partnerDebtSortKey(), partnerDebtSortDirection(), 'companyOwesPartnerAmount') }}</button></th>
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
        @if (canExpandPartnerDebts()) {
          <div class="table-actions">
            <button type="button" (click)="expandPartnerDebts()" [disabled]="loadingMorePartnerDebts()">Expand (+10)</button>
          </div>
        }
      </section>
    </div>
  `,
  styles: [`
    .dashboard { display: grid; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .header h1 { margin: 0; }
    .filters-panel { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .85rem; display: grid; grid-template-columns: repeat(4, minmax(180px, 1fr)); gap: .7rem; }
    .filter-field { display: grid; gap: .32rem; font-weight: 600; color: #374151; }
    .filter-field input, .filter-field select { border: 1px solid #d1d5db; border-radius: 8px; padding: .45rem .6rem; background: #fff; }
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
    .sort-btn { border: 0; background: transparent; padding: 0; cursor: pointer; color: inherit; font: inherit; font-weight: 700; }
    .table-actions { display: flex; justify-content: center; margin-top: .75rem; }
    .table-actions button { border: 1px solid #d1d5db; background: #fff; border-radius: 8px; padding: .45rem .9rem; cursor: pointer; }
    .table-actions button[disabled] { opacity: .55; cursor: default; }
    @media (max-width: 900px) {
      .transform-grid { grid-template-columns: 1fr; }
      .filters-panel { grid-template-columns: 1fr; }
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
  modePeriode: ModePeriode = 'month';
  mois = this.defaultMonth();
  trimestre = 1;
  annee = new Date().getFullYear();
  dateDebut = this.firstDayOfMonth();
  dateFin = this.todayIso();
  selectedSalesPersonCode = 0;
  readonly partnerDebtSearch = signal('');
  readonly report = signal<CommercialReportingPayload | null>(null);
  readonly partnerDebts = signal<PartnerDebtItem[]>([]);
  readonly partnerDebtsTotal = signal(0);
  readonly loadingMorePartnerDebts = signal(false);
  readonly partnerDebtSortKey = signal<PartnerDebtSortKey | null>(null);
  readonly partnerDebtSortDirection = signal<SortDirection>('none');
  readonly canExpandPartnerDebts = computed(() => this.partnerDebts().length < this.partnerDebtsTotal());
  private partnerDebtsPage = 1;
  private readonly partnerDebtsPageSize = 10;
  readonly filteredPartnerDebts = computed(() => {
    const q = this.partnerDebtSearch().trim().toLowerCase();
    const baseRows = !q ? this.partnerDebts() : this.partnerDebts().filter((row) =>
      String(row.cardCode ?? '').toLowerCase().includes(q) ||
      String(row.cardName ?? '').toLowerCase().includes(q)
    );
    const sortKey = this.partnerDebtSortKey();
    const sortDirection = this.partnerDebtSortDirection();
    if (!sortKey || sortDirection === 'none') return baseRows;
    const direction = sortDirection === 'asc' ? 1 : -1;
    return [...baseRows].sort((a, b) => this.comparePartnerDebt(a, b, sortKey) * direction);
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

  load(): void {
    this.loading.set(true);

    this.reportingApi.getCommercialReporting(this.buildReportingParams()).subscribe({
      next: (reporting) => {
        this.partnerDebtsPage = 1;
        this.report.set(reporting.data);
        this.partnersCount.set(Number(reporting.data?.kpis?.activePartnersCount ?? 0));
        this.loading.set(false);
        this.loadPartnerDebts();
        this.loadCatalogCount();
      },
      error: () => this.loading.set(false)
    });
  }

  private loadCatalogCount(): void {
    this.http.get<any>(`${this.api}/sap/items?page=1&pageSize=1`).subscribe({
      next: (products) => this.catalogCount.set(this.extractTotal(products)),
      error: () => this.catalogCount.set(0)
    });
  }

  private loadPartnerDebts(): void {
    this.reportingApi.getPartnerDebts(
      this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined,
      1,
      this.partnerDebtsPageSize
    ).subscribe({
      next: (partnerDebts) => {
        this.partnerDebts.set(partnerDebts.data ?? []);
        this.partnerDebtsTotal.set(Number(partnerDebts.totalCount ?? (partnerDebts.data?.length ?? 0)));
      },
      error: () => {
        this.partnerDebts.set([]);
        this.partnerDebtsTotal.set(0);
      }
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

  togglePartnerDebtSort(key: PartnerDebtSortKey): void {
    const currentKey = this.partnerDebtSortKey();
    const currentDirection = this.partnerDebtSortDirection();
    if (currentKey !== key) {
      this.partnerDebtSortKey.set(key);
      this.partnerDebtSortDirection.set('asc');
      return;
    }
    if (currentDirection === 'asc') {
      this.partnerDebtSortDirection.set('desc');
      return;
    }
    if (currentDirection === 'desc') {
      this.partnerDebtSortDirection.set('none');
      this.partnerDebtSortKey.set(null);
      return;
    }
    this.partnerDebtSortDirection.set('asc');
  }

  sortIndicator(activeKey: string | null, activeDirection: SortDirection, key: string): string {
    if (activeKey !== key || activeDirection === 'none') return '';
    return activeDirection === 'asc' ? '↑' : '↓';
  }

  expandPartnerDebts(): void {
    if (!this.canExpandPartnerDebts() || this.loadingMorePartnerDebts()) return;
    this.loadingMorePartnerDebts.set(true);
    const nextPage = this.partnerDebtsPage + 1;
    this.reportingApi.getPartnerDebts(
      this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined,
      nextPage,
      this.partnerDebtsPageSize
    ).subscribe({
      next: (res) => {
        this.partnerDebtsPage = nextPage;
        this.partnerDebts.set([...this.partnerDebts(), ...(res.data ?? [])]);
        this.partnerDebtsTotal.set(Number(res.totalCount ?? this.partnerDebtsTotal()));
        this.loadingMorePartnerDebts.set(false);
      },
      error: () => this.loadingMorePartnerDebts.set(false)
    });
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

  monthKeyForLinks(): string {
    return this.modePeriode === 'month' ? this.mois : this.defaultMonth();
  }

  private comparePartnerDebt(a: PartnerDebtItem, b: PartnerDebtItem, key: PartnerDebtSortKey): number {
    if (key === 'partnerOwesCompanyAmount' || key === 'companyOwesPartnerAmount') {
      return Number((a as any)[key] || 0) - Number((b as any)[key] || 0);
    }
    if (key === 'salesPersonName') {
      const av = String(a.salesPersonName || `#${a.salesPersonCode || 0}`).toLowerCase();
      const bv = String(b.salesPersonName || `#${b.salesPersonCode || 0}`).toLowerCase();
      return av.localeCompare(bv);
    }
    return String((a as any)[key] || '').toLowerCase().localeCompare(String((b as any)[key] || '').toLowerCase());
  }

  private buildReportingParams(): {
    periodType: 'month' | 'quarter' | 'year' | 'custom';
    month?: string;
    quarter?: number;
    year?: number;
    startDate?: string;
    endDate?: string;
    salesPersonCode?: number;
  } {
    return {
      periodType: this.modePeriode,
      month: this.modePeriode === 'month' ? this.mois : undefined,
      quarter: this.modePeriode === 'quarter' ? this.trimestre : undefined,
      year: this.modePeriode === 'quarter' || this.modePeriode === 'year' ? this.annee : undefined,
      startDate: this.modePeriode === 'custom' ? this.dateDebut : undefined,
      endDate: this.modePeriode === 'custom' ? this.dateFin : undefined,
      salesPersonCode: this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined
    };
  }

  private firstDayOfMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
  }

  private todayIso(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}

