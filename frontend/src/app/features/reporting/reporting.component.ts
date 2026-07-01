import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { PartnerApiService, PartnerRow } from '../../core/services/partner-api.service';
import {
  ReportingApiService,
  ReportingRevenueBreakdownRow,
  ReportingRevenueBreakdownType,
  ReportingSalesPersonInfo
} from '../../core/services/reporting-api.service';

type PeriodType = 'month' | 'quarter' | 'year' | 'custom';
type SortKey = 'code' | 'name' | 'quantity' | 'documentsCount' | 'revenue';
type SortDirection = 'asc' | 'desc';

type ReportingFilterState = {
  periodType?: PeriodType;
  month?: string;
  quarter?: number;
  year?: number;
  startDate?: string;
  endDate?: string;
  selectedSalesPersonCode?: number;
  selectedPartnerCode?: string;
  commercialSearch?: string;
  partnerSearch?: string;
};

@Component({
  selector: 'app-reporting',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="reporting-page">
      <header class="page-header">
        <div>
          <h1>{{ config().title }}</h1>
          <p>{{ config().subtitle }}</p>
        </div>
        <strong>{{ money(totalRevenue()) }}</strong>
      </header>

      <section class="filters">
        <label>
          Periode
          <select [(ngModel)]="periodType" (change)="onFilterChange()">
            <option value="month">Mois</option>
            <option value="quarter">Trimestre</option>
            <option value="year">Année</option>
            <option value="custom">Personnalisée</option>
          </select>
        </label>

        @if (periodType === 'month') {
          <label>
            Mois
            <input type="month" [(ngModel)]="month" (change)="onFilterChange()" />
          </label>
        }

        @if (periodType === 'quarter') {
          <label>
            Trimestre
            <select [(ngModel)]="quarter" (change)="onFilterChange()">
              <option [ngValue]="1">T1</option>
              <option [ngValue]="2">T2</option>
              <option [ngValue]="3">T3</option>
              <option [ngValue]="4">T4</option>
            </select>
          </label>
          <label>
            Année
            <select [(ngModel)]="year" (change)="onFilterChange()">
              @for (option of yearOptions(); track option) {
                <option [ngValue]="option">{{ option }}</option>
              }
            </select>
          </label>
        }

        @if (periodType === 'year') {
          <label>
            Année
            <select [(ngModel)]="year" (change)="onFilterChange()">
              @for (option of yearOptions(); track option) {
                <option [ngValue]="option">{{ option }}</option>
              }
            </select>
          </label>
        }

        @if (periodType === 'custom') {
          <label>
            Debut
            <input type="date" [(ngModel)]="startDate" (change)="onFilterChange()" />
          </label>
          <label>
            Fin
            <input type="date" [(ngModel)]="endDate" (change)="onFilterChange()" />
          </label>
        }

        @if (isManagerMode()) {
          <label class="search-box">
            Commercial
            <input
              type="search"
              [ngModel]="commercialSearch()"
              (keydown)="replaceSelectionOnTyping($event, 'commercial')"
              (ngModelChange)="onCommercialInput($event)"
              (focus)="onCommercialFocus($event)"
              placeholder="Tous les commerciaux" />
            @if (openCommercialSuggestions) {
              <div class="suggestions">
                <button type="button" (click)="selectCommercial(null)">Tous les commerciaux</button>
                @for (sp of commercialSuggestions(); track sp.salesPersonCode) {
                  <button type="button" (click)="selectCommercial(sp)">
                    {{ sp.salesPersonName || ('#' + sp.salesPersonCode) }}
                  </button>
                }
              </div>
            }
          </label>
        }

        @if (showPartnerFilter()) {
        <label class="search-box">
          Partenaire
          <input
            type="search"
            [ngModel]="partnerSearch()"
            (keydown)="replaceSelectionOnTyping($event, 'partner')"
            (ngModelChange)="onPartnerInput($event)"
            (focus)="onPartnerFocus($event)"
            placeholder="Tous les partenaires" />
          @if (openPartnerSuggestions) {
            <div class="suggestions">
              <button type="button" (click)="selectPartner(null)">Tous les partenaires</button>
              @for (partner of partnerSuggestions(); track partnerCode(partner)) {
                <button type="button" (click)="selectPartner(partner)">
                  {{ partnerName(partner) }}
                </button>
              }
            </div>
          }
        </label>
        }
      </section>

      @if (loading()) {
        <p class="state">Chargement...</p>
      } @else if (error()) {
        <p class="state error">{{ error() }}</p>
      } @else {
        <section class="table-wrap">
          <table>
            <thead>
              <tr>
                <th><button type="button" (click)="toggleSort('name')">{{ config().nameLabel }} {{ sortIndicator('name') }}</button></th>
                @if (showQuantity()) {
                  <th><button type="button" (click)="toggleSort('quantity')">Quantité {{ sortIndicator('quantity') }}</button></th>
                }
                <th><button type="button" (click)="toggleSort('documentsCount')">Documents {{ sortIndicator('documentsCount') }}</button></th>
                <th><button type="button" (click)="toggleSort('revenue')">CA {{ sortIndicator('revenue') }}</button></th>
                <th>Part du total</th>
              </tr>
            </thead>
            <tbody>
              @for (row of sortedRows(); track row.code) {
                <tr>
                  <td>{{ row.name || row.code }}</td>
                  @if (showQuantity()) {
                    <td>{{ number(row.quantity) }}</td>
                  }
                  <td>{{ row.documentsCount }}</td>
                  <td>{{ money(row.revenue) }}</td>
                  <td>
                    <div class="share">
                      <i [style.width.%]="share(row.revenue)"></i>
                    </div>
                    <span>{{ percent(share(row.revenue)) }}</span>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td [attr.colspan]="emptyColspan()" class="empty">Aucune donnee sur cette periode</td>
                </tr>
              }
            </tbody>
          </table>
        </section>
      }
    </div>
  `,
  styles: [`
    .reporting-page { display: grid; gap: 1rem; color: #172033; }
    .page-header {
      display: flex;
      align-items: end;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem 0;
      border-bottom: 1px solid #e5e7eb;
    }
    h1 { margin: 0; font-size: 1.6rem; }
    p { margin: .25rem 0 0; color: #64748b; }
    .page-header strong { font-size: 1.4rem; color: #0f766e; white-space: nowrap; }
    .filters {
      display: grid;
      grid-template-columns: repeat(6, minmax(140px, 1fr));
      gap: .75rem;
      align-items: end;
    }
    label { display: grid; gap: .3rem; font-size: .78rem; color: #64748b; font-weight: 700; }
    input, select {
      width: 100%;
      border: 1px solid #d7dee8;
      border-radius: 6px;
      padding: .55rem .65rem;
      font: inherit;
      color: #172033;
      background: white;
    }
    .search-box { position: relative; }
    .suggestions {
      position: absolute;
      top: calc(100% + 4px);
      left: 0;
      right: 0;
      z-index: 20;
      max-height: 240px;
      overflow: auto;
      border: 1px solid #d7dee8;
      border-radius: 8px;
      background: white;
      box-shadow: 0 14px 34px rgba(15, 23, 42, .16);
      padding: .25rem;
    }
    .suggestions button {
      width: 100%;
      border: 0;
      background: transparent;
      border-radius: 6px;
      padding: .5rem .55rem;
      text-align: left;
      cursor: pointer;
      color: #172033;
    }
    .suggestions button:hover { background: #f1f5f9; }
    .table-wrap {
      overflow: auto;
      border: 1px solid #e5e7eb;
      border-radius: 8px;
      background: white;
    }
    table { width: 100%; border-collapse: collapse; min-width: 760px; }
    th, td { padding: .7rem .8rem; border-bottom: 1px solid #edf2f7; text-align: left; }
    th { background: #f8fafc; color: #475569; font-size: .78rem; text-transform: uppercase; }
    th button {
      border: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      font-weight: 800;
      text-transform: inherit;
      cursor: pointer;
      padding: 0;
    }
    td { font-size: .9rem; }
    td:nth-last-child(2), th:nth-last-child(2), td:nth-last-child(3), th:nth-last-child(3) { text-align: right; }
    .share { height: 8px; background: #e5e7eb; border-radius: 999px; overflow: hidden; min-width: 110px; }
    .share i { display: block; height: 100%; background: #14b8a6; border-radius: inherit; }
    .share + span { display: block; margin-top: .25rem; color: #64748b; font-size: .75rem; }
    .state, .empty { padding: 1rem; color: #64748b; }
    .error { color: #b91c1c; }

    @media (max-width: 1100px) {
      .filters { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }

    @media (max-width: 700px) {
      .page-header { align-items: start; flex-direction: column; }
      .filters { grid-template-columns: 1fr; }
    }
  `]
})
export class ReportingComponent implements OnInit {
  private readonly api = inject(ReportingApiService);
  private readonly partnerApi = inject(PartnerApiService);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly storageKey = 'sap-b1-reporting-filters-v1';

  readonly rows = signal<ReportingRevenueBreakdownRow[]>([]);
  readonly teamMembers = signal<ReportingSalesPersonInfo[]>([]);
  readonly partners = signal<PartnerRow[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly view = signal<ReportingRevenueBreakdownType>('family');
  readonly search = signal('');
  readonly commercialSearch = signal('Tous les commerciaux');
  readonly partnerSearch = signal('Tous les partenaires');

  periodType: PeriodType = 'month';
  month = new Date().toISOString().slice(0, 7);
  quarter = Math.floor(new Date().getMonth() / 3) + 1;
  year = new Date().getFullYear();
  startDate = `${new Date().getFullYear()}-01-01`;
  endDate = new Date().toISOString().slice(0, 10);
  selectedSalesPersonCode = 0;
  selectedPartnerCode = '';
  readonly sortKey = signal<SortKey>('revenue');
  readonly sortDirection = signal<SortDirection>('desc');
  openCommercialSuggestions = false;
  openPartnerSuggestions = false;
  private replaceCommercialOnType = false;
  private replacePartnerOnType = false;

  readonly config = computed(() => {
    const view = this.view();
    if (view === 'article') {
      return { title: 'CA par article', subtitle: "Chiffre d'affaires net par article", nameLabel: 'Article' };
    }
    if (view === 'client') {
      return { title: 'CA par client', subtitle: "Chiffre d'affaires net par client", nameLabel: 'Client' };
    }
    return { title: 'CA par famille', subtitle: "Chiffre d'affaires net par famille article", nameLabel: 'Famille' };
  });

  readonly filteredRows = computed(() => {
    return this.rows();
  });

  readonly sortedRows = computed(() => {
    const key = this.sortKey();
    const dir = this.sortDirection() === 'asc' ? 1 : -1;
    return [...this.filteredRows()].sort((a, b) => this.compareRows(a, b, key) * dir);
  });

  readonly totalRevenue = computed(() =>
    this.filteredRows().reduce((sum, row) => sum + Number(row.revenue || 0), 0)
  );

  readonly commercialSuggestions = computed(() => {
    const term = this.normalize(this.commercialSearch());
    return this.teamMembers()
      .filter(sp => !term || term === this.normalize('Tous les commerciaux') || this.normalize(sp.salesPersonName).includes(term) || String(sp.salesPersonCode).includes(term))
      .slice(0, 20);
  });

  readonly partnerSuggestions = computed(() => {
    const term = this.normalize(this.partnerSearch());
    return this.partners()
      .filter(partner => !term || term === this.normalize('Tous les partenaires') || this.normalize(this.partnerCode(partner)).includes(term) || this.normalize(this.partnerName(partner)).includes(term))
      .slice(0, 20);
  });

  ngOnInit(): void {
    this.restoreFilters();
    this.loadFilterOptions();
    this.route.data.subscribe(data => {
      const routeView = data['reportingView'];
      this.view.set(routeView === 'article' || routeView === 'client' ? routeView : 'family');
      if (this.view() === 'client') {
        this.selectedPartnerCode = '';
        this.partnerSearch.set('Tous les partenaires');
      }
      this.sortKey.set('revenue');
      this.sortDirection.set('desc');
      this.persistFilters();
      this.load();
    });
  }

  @HostListener('document:click', ['$event'])
  closeSuggestions(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('.search-box')) return;
    this.openCommercialSuggestions = false;
    this.openPartnerSuggestions = false;
  }

  isManagerMode(): boolean {
    return this.auth.hasRole(['Admin', 'Manager']);
  }

  yearOptions(): number[] {
    const currentYear = new Date().getFullYear();
    const selectedYear = Number(this.year || currentYear);
    const start = Math.max(2000, Math.min(currentYear - 5, selectedYear - 2));
    const end = Math.max(currentYear + 1, selectedYear + 2);
    const years: number[] = [];
    for (let year = end; year >= start; year--) {
      years.push(year);
    }
    return years;
  }

  onFilterChange(): void {
    this.persistFilters();
    this.load();
  }

  onCommercialInput(value: string): void {
    this.replaceCommercialOnType = false;
    const hadSelection = this.selectedSalesPersonCode > 0 || !!this.selectedPartnerCode;
    this.commercialSearch.set(String(value ?? ''));
    this.selectedSalesPersonCode = 0;
    this.selectedPartnerCode = '';
    this.partnerSearch.set('Tous les partenaires');
    this.openCommercialSuggestions = true;
    this.openPartnerSuggestions = false;
    this.persistFilters();
    if (hadSelection) this.load();
  }

  selectCommercial(sp: ReportingSalesPersonInfo | null): void {
    this.selectedSalesPersonCode = sp?.salesPersonCode ?? 0;
    this.commercialSearch.set(sp ? (sp.salesPersonName || String(sp.salesPersonCode)) : 'Tous les commerciaux');
    this.selectedPartnerCode = '';
    this.partnerSearch.set('Tous les partenaires');
    this.openCommercialSuggestions = false;
    this.persistFilters();
    this.load();
  }

  onPartnerInput(value: string): void {
    this.replacePartnerOnType = false;
    const hadSelection = this.selectedSalesPersonCode > 0 || !!this.selectedPartnerCode;
    this.partnerSearch.set(String(value ?? ''));
    this.selectedPartnerCode = '';
    this.selectedSalesPersonCode = 0;
    this.commercialSearch.set('Tous les commerciaux');
    this.openPartnerSuggestions = true;
    this.openCommercialSuggestions = false;
    this.persistFilters();
    if (hadSelection) this.load();
  }

  onCommercialFocus(event: FocusEvent): void {
    this.openCommercialSuggestions = true;
    this.openPartnerSuggestions = false;
    this.replaceCommercialOnType = true;
    (event.target as HTMLInputElement | null)?.select();
  }

  onPartnerFocus(event: FocusEvent): void {
    this.openPartnerSuggestions = true;
    this.openCommercialSuggestions = false;
    this.replacePartnerOnType = true;
    (event.target as HTMLInputElement | null)?.select();
  }

  replaceSelectionOnTyping(event: KeyboardEvent, field: 'commercial' | 'partner'): void {
    if (event.ctrlKey || event.metaKey || event.altKey || event.key.length !== 1) return;
    const shouldReplace = field === 'commercial' ? this.replaceCommercialOnType : this.replacePartnerOnType;
    if (!shouldReplace) return;
    const input = event.target as HTMLInputElement | null;
    input?.select();
  }

  selectPartner(partner: PartnerRow | null): void {
    this.selectedPartnerCode = partner ? this.partnerCode(partner) : '';
    this.partnerSearch.set(partner ? this.partnerName(partner) : 'Tous les partenaires');
    this.selectedSalesPersonCode = 0;
    this.commercialSearch.set('Tous les commerciaux');
    this.openPartnerSuggestions = false;
    this.persistFilters();
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getRevenueBreakdown({
      type: this.view(),
      periodType: this.periodType,
      month: this.periodType === 'month' ? this.month : undefined,
      quarter: this.periodType === 'quarter' ? this.quarter : undefined,
      year: this.periodType === 'quarter' || this.periodType === 'year' ? this.year : undefined,
      startDate: this.periodType === 'custom' ? this.startDate : undefined,
      endDate: this.periodType === 'custom' ? this.endDate : undefined,
      salesPersonCode: this.isManagerMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined,
      cardCode: this.showPartnerFilter() && this.selectedPartnerCode ? this.selectedPartnerCode : undefined,
      limit: 300
    }).subscribe({
      next: response => {
        this.rows.set(response.data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Impossible de charger le reporting.');
        this.loading.set(false);
      }
    });
  }

  toggleSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDirection.set(this.sortDirection() === 'desc' ? 'asc' : 'desc');
    } else {
      this.sortKey.set(key);
      this.sortDirection.set(key === 'name' || key === 'code' ? 'asc' : 'desc');
    }
  }

  sortIndicator(key: SortKey): string {
    if (this.sortKey() !== key) return '';
    return this.sortDirection() === 'asc' ? '^' : 'v';
  }

  showQuantity(): boolean {
    return this.view() !== 'client';
  }

  showPartnerFilter(): boolean {
    return this.view() !== 'client';
  }

  emptyColspan(): number {
    return 1 + (this.showQuantity() ? 1 : 0) + 3;
  }

  share(value: number): number {
    const total = Math.max(0, Number(this.totalRevenue() || 0));
    if (total <= 0) return 0;
    return Math.max(0, Math.min(100, Number(value || 0) * 100 / total));
  }

  money(value: number): string {
    return new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'MAD', maximumFractionDigits: 0 }).format(Number(value || 0));
  }

  number(value: number): string {
    return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 2 }).format(Number(value || 0));
  }

  percent(value: number): string {
    return `${this.number(value)} %`;
  }

  partnerCode(row: PartnerRow): string {
    return String(row.CardCode ?? (row as any).cardCode ?? '').trim();
  }

  partnerName(row: PartnerRow): string {
    const code = this.partnerCode(row);
    const name = String(row.CardName ?? (row as any).cardName ?? code).trim();
    return code ? `${name} (${code})` : name;
  }

  private loadFilterOptions(): void {
    if (this.isManagerMode()) {
      this.api.getCommercialReporting({
        periodType: 'month',
        month: this.month,
        includeRecentDocuments: false,
        includeTeamPerformance: false
      }).subscribe({
        next: response => this.teamMembers.set(response.data?.teamMembers ?? []),
        error: () => this.teamMembers.set([])
      });
    } else {
      this.selectedSalesPersonCode = 0;
      this.commercialSearch.set('Tous les commerciaux');
    }

    this.loadPartnersPage();
  }

  private loadPartnersPage(page = 1, pageSize = 500, accumulated: PartnerRow[] = []): void {
    this.partnerApi.getAll(page, pageSize).subscribe({
      next: response => {
        const merged = this.mergePartners(accumulated, response.items ?? []);
        this.partners.set(merged);
        if (merged.length < response.totalCount && (response.items ?? []).length > 0) {
          this.loadPartnersPage(page + 1, pageSize, merged);
        } else {
          this.ensureSelectedPartnerStillExists(merged);
        }
      },
      error: () => {
        if (page === 1) this.partners.set([]);
      }
    });
  }

  private mergePartners(current: PartnerRow[], next: PartnerRow[]): PartnerRow[] {
    const byCode = new Map<string, PartnerRow>();
    [...current, ...next].forEach(row => {
      const code = this.partnerCode(row);
      if (code) byCode.set(code, row);
    });
    return [...byCode.values()];
  }

  private ensureSelectedPartnerStillExists(partners: PartnerRow[]): void {
    if (!this.selectedPartnerCode) return;
    const exists = partners.some(partner => this.partnerCode(partner) === this.selectedPartnerCode);
    if (exists) return;
    this.selectedPartnerCode = '';
    this.partnerSearch.set('Tous les partenaires');
    this.persistFilters();
    this.load();
  }

  private persistFilters(): void {
    try {
      const state: ReportingFilterState = {
        periodType: this.periodType,
        month: this.month,
        quarter: this.quarter,
        year: this.year,
        startDate: this.startDate,
        endDate: this.endDate,
        selectedSalesPersonCode: this.selectedSalesPersonCode,
        selectedPartnerCode: this.selectedPartnerCode,
        commercialSearch: this.commercialSearch(),
        partnerSearch: this.partnerSearch()
      };
      localStorage.setItem(this.storageKey, JSON.stringify(state));
    } catch {
    }
  }

  private restoreFilters(): void {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) return;
      const state = JSON.parse(raw) as ReportingFilterState;
      if (this.isPeriodType(state.periodType)) this.periodType = state.periodType;
      if (typeof state.month === 'string' && /^\d{4}-\d{2}$/.test(state.month)) this.month = state.month;
      if (Number.isInteger(state.quarter) && Number(state.quarter) >= 1 && Number(state.quarter) <= 4) this.quarter = Number(state.quarter);
      if (Number.isInteger(state.year) && Number(state.year) >= 2000 && Number(state.year) <= 2100) this.year = Number(state.year);
      if (typeof state.startDate === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(state.startDate)) this.startDate = state.startDate;
      if (typeof state.endDate === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(state.endDate)) this.endDate = state.endDate;
      this.selectedSalesPersonCode = Number(state.selectedSalesPersonCode || 0);
      this.selectedPartnerCode = String(state.selectedPartnerCode ?? '').trim();
      this.commercialSearch.set(state.commercialSearch || 'Tous les commerciaux');
      this.partnerSearch.set(state.partnerSearch || 'Tous les partenaires');
      if (!this.isManagerMode()) {
        this.selectedSalesPersonCode = 0;
        this.commercialSearch.set('Tous les commerciaux');
      }
      if (this.selectedSalesPersonCode > 0) {
        this.selectedPartnerCode = '';
        this.partnerSearch.set('Tous les partenaires');
      } else if (this.selectedPartnerCode) {
        this.selectedSalesPersonCode = 0;
        this.commercialSearch.set('Tous les commerciaux');
      }
    } catch {
    }
  }

  private compareRows(a: ReportingRevenueBreakdownRow, b: ReportingRevenueBreakdownRow, key: SortKey): number {
    if (key === 'name' || key === 'code') {
      return String(a[key] ?? '').localeCompare(String(b[key] ?? ''), 'fr', { sensitivity: 'base' });
    }
    return Number(a[key] || 0) - Number(b[key] || 0);
  }

  private normalize(value: string): string {
    return String(value ?? '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim();
  }

  private isPeriodType(value: unknown): value is PeriodType {
    return value === 'month' || value === 'quarter' || value === 'year' || value === 'custom';
  }
}
