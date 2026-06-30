import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { InvoiceListFilters, InvoiceListItem, InvoicesApiService } from './invoices-api.service';

type InvoiceSortKey = 'number' | 'partner' | 'date' | 'status' | 'total';
type SortDirection = 'none' | 'asc' | 'desc';

@Component({
  selector: 'app-invoices-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <div class="header">
      <h1>Facture</h1>
        <a class="btn-primary" [routerLink]="['/factures/new']">+ Nouvelle facture</a>
      </div>

      <form [formGroup]="filtersForm" class="filters" (ngSubmit)="applyFilters()">
        <label class="filter-search">Recherche
          <input formControlName="search" placeholder="Recherche" (input)="onFilterInput('search')" (focus)="openFilterSuggestions('search')" />
          @if (openSearchSuggestions && filteredSearchSuggestions().length) {
            <div class="filter-suggestions">
              @for (suggestion of filteredSearchSuggestions(); track suggestion) {
                <button type="button" (mousedown)="selectFilterSuggestion('search', suggestion)">{{ suggestion }}</button>
              }
            </div>
          }
        </label>
        <label class="filter-search">Client
          <input formControlName="customer" placeholder="Client" (input)="onFilterInput('customer')" (focus)="openFilterSuggestions('customer')" />
          @if (openCustomerSuggestions && filteredCustomerSuggestions().length) {
            <div class="filter-suggestions">
              @for (suggestion of filteredCustomerSuggestions(); track suggestion) {
                <button type="button" (mousedown)="selectFilterSuggestion('customer', suggestion)">{{ suggestion }}</button>
              }
            </div>
          }
        </label>
        <label class="date-field">
          <span>Statut</span>
          <select formControlName="phase" (change)="onFiltersChanged()">
            <option value="all">Tous les statuts</option>
            <option value="open">En attente</option>
            <option value="closed">Cloturees</option>
            <option value="cancelled">Annulees</option>
          </select>
        </label>
        <label class="date-field">
          <span>Du</span>
          <input type="date" formControlName="dateFrom" (change)="onFiltersChanged()" />
        </label>
        <label class="date-field">
          <span>Au</span>
          <input type="date" formControlName="dateTo" (change)="onFiltersChanged()" />
        </label>
        <button type="submit" class="btn-primary">Filtrer</button>
        <button type="button" class="btn-outline" (click)="resetFilters()">Reinitialiser</button>
      </form>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else if (error()) {
        <div class="alert alert-error">{{ error() }}</div>
      } @else {
        <table>
          <thead>
            <tr>
              <th><button type="button" class="sort-header" (click)="toggleSort('number')">Numero {{ sortIndicator('number') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('partner')">Client {{ sortIndicator('partner') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('date')">Date {{ sortIndicator('date') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('total')">Total {{ sortIndicator('total') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('status')">Statut {{ sortIndicator('status') }}</button></th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (doc of visibleItems(); track doc.id) {
              <tr>
                <td>{{ doc.docNum || ('#' + doc.id) }}</td>
                <td>{{ doc.cardName || doc.cardCode || '-' }}</td>
                <td>{{ doc.docDate ? (doc.docDate | date:'dd/MM/yyyy') : '-' }}</td>
                <td>{{ doc.docTotal | number:'1.2-2' }}</td>
                <td>
                  <span class="badge" [class.badge-open]="isOpenStatus(doc.status)" [class.badge-closed]="isClosedStatus(doc.status)" [class.badge-cancelled]="isCancelledStatus(doc.status)">
                    {{ statusLabel(doc.status) }}
                  </span>
                </td>
                <td><a class="btn-sm" [routerLink]="['/factures', doc.id]">Voir</a></td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">Aucune facture</td></tr>
            }
          </tbody>
        </table>

        <div class="pager">
          <button class="btn-outline" type="button" (click)="prevPage()" [disabled]="page() <= 1">Précédent</button>
          <span>Page {{ page() }} / {{ totalPages() }}</span>
          <button class="btn-outline" type="button" (click)="nextPage()" [disabled]="page() >= totalPages()">Suivant</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: center; }
    .filters { display: grid; grid-template-columns: repeat(6, minmax(120px, 1fr)); gap: 0.5rem; align-items: center; }
    .filters input, .filters select { padding: 0.45rem 0.6rem; border: 1px solid #d7d7d7; border-radius: 6px; }
    .filter-search { position: relative; display: flex; flex-direction: column; gap: 0.25rem; font-size: 0.85rem; color: #374151; }
    .date-field { display: flex; flex-direction: column; gap: 0.2rem; font-size: 0.85rem; color: #374151; }
    .filter-suggestions { position: absolute; z-index: 20; top: calc(100% + 4px); left: 0; right: 0; display: grid; gap: .15rem; max-height: 280px; overflow: auto; background: #fff; border: 1px solid #cbd5e1; border-radius: 12px; box-shadow: 0 18px 38px rgba(15,23,42,.16); padding: .35rem; }
    .filter-suggestions button { width: 100%; border: 0; background: #fff; text-align: left; padding: .58rem .7rem; border-radius: 8px; cursor: pointer; font-weight: 700; color: #111827; white-space: normal; line-height: 1.25; }
    .filter-suggestions button:hover { background: #eff6ff; color: #1d4ed8; }
    .loading, .empty { text-align: center; padding: 1rem; }
    .alert-error { background: #fce4ec; border-left: 4px solid #c2185b; color: #880e4f; padding: 1rem; border-radius: 6px; }
    .badge { display: inline-block; border-radius: 999px; padding: 0.2rem 0.55rem; font-size: 0.78rem; }
    .badge-open { background: #e8f5e9; color: #1b5e20; }
    .badge-closed { background: #f3f4f6; color: #374151; }
    .badge-cancelled { background: #fdecea; color: #c62828; }
    .sort-header { border: 0; background: transparent; padding: 0; color: inherit; font: inherit; font-weight: 700; cursor: pointer; text-align: left; }
    .sort-header:hover { color: #2563eb; }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .pager { display: flex; justify-content: space-between; align-items: center; }
    @media (max-width: 1024px) {
      .filters { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class InvoicesPageComponent implements OnInit {
  private readonly api = inject(InvoicesApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly items = signal<InvoiceListItem[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly totalCount = signal(0);
  readonly sortKey = signal<InvoiceSortKey | null>(null);
  readonly sortDirection = signal<SortDirection>('none');
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  readonly filtersForm = this.fb.group({
    search: [''],
    customer: [''],
    phase: ['all'],
    dateFrom: [''],
    dateTo: ['']
  });

  readonly customerSuggestions = computed(() =>
    [...new Set(this.items().map(doc => this.partnerNameOf(doc)).filter(v => v && v !== '-'))].slice(0, 30)
  );
  readonly searchSuggestions = computed(() => {
    const numbers = this.items().map(doc => String(doc.docNum || `#${doc.id}`).trim()).filter(Boolean);
    const clients = this.items().map(doc => this.partnerNameOf(doc)).filter(v => v && v !== '-');
    return [...new Set([...numbers, ...clients])].slice(0, 40);
  });
  readonly visibleItems = computed(() => {
    const customerRaw = String(this.filtersForm.getRawValue().customer ?? '').trim().toLowerCase();
    const filtered = !customerRaw
      ? this.items()
      : this.items().filter((doc) => {
        const cardCode = String(doc.cardCode ?? '').trim().toLowerCase();
        const partner = this.partnerNameOf(doc).toLowerCase();
        return partner.includes(customerRaw) || cardCode.includes(customerRaw);
      });

    return this.sortedItems(filtered);
  });

  private readonly pageCache = new Map<string, InvoiceListItem[]>();
  private readonly totalCountCache = new Map<string, number>();
  private filterDebounceHandle: ReturnType<typeof setTimeout> | null = null;
  openSearchSuggestions = false;
  openCustomerSuggestions = false;

  @HostListener('document:click', ['$event'])
  closeFilterSuggestions(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('.filter-search')) return;
    this.openSearchSuggestions = false;
    this.openCustomerSuggestions = false;
  }

  ngOnInit(): void {
    this.load();
  }

  onFilterInput(field: 'search' | 'customer'): void {
    this.openFilterSuggestions(field);
    this.onFiltersChanged();
  }

  openFilterSuggestions(field: 'search' | 'customer'): void {
    this.openSearchSuggestions = field === 'search';
    this.openCustomerSuggestions = field === 'customer';
  }

  selectFilterSuggestion(field: 'search' | 'customer', value: string): void {
    if (field === 'search') this.filtersForm.patchValue({ search: value });
    else this.filtersForm.patchValue({ customer: value });
    this.openSearchSuggestions = false;
    this.openCustomerSuggestions = false;
    this.onFiltersChanged();
  }

  filteredSearchSuggestions(): string[] {
    return this.filterSuggestionValues(this.searchSuggestions(), this.filtersForm.getRawValue().search);
  }

  filteredCustomerSuggestions(): string[] {
    return this.filterSuggestionValues(this.customerSuggestions(), this.filtersForm.getRawValue().customer);
  }

  private filterSuggestionValues(values: string[], rawQuery: unknown): string[] {
    const query = String(rawQuery ?? '').trim().toLowerCase();
    return values
      .filter(value => !query || value.toLowerCase().includes(query))
      .slice(0, 40);
  }

  onFiltersChanged(): void {
    if (this.filterDebounceHandle) {
      clearTimeout(this.filterDebounceHandle);
    }

    this.filterDebounceHandle = setTimeout(() => {
      this.clearCaches();
      this.page.set(1);
      this.load();
    }, 250);
  }

  applyFilters(): void {
    this.clearCaches();
    this.page.set(1);
    this.load();
  }

  resetFilters(): void {
    this.filtersForm.reset({ search: '', customer: '', phase: 'all', dateFrom: '', dateTo: '' });
    this.clearCaches();
    this.page.set(1);
    this.load();
  }

  prevPage(): void {
    if (this.page() <= 1) return;
    this.page.update((p) => p - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() >= this.totalPages()) return;
    this.page.update((p) => p + 1);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');

    const filters = this.buildFilters(this.page());
    const key = this.buildCacheKey(filters);
    const cachedItems = this.pageCache.get(key);
    const cachedTotalCount = this.totalCountCache.get(key);

    if (cachedItems && typeof cachedTotalCount === 'number') {
      this.items.set(cachedItems);
      this.totalCount.set(cachedTotalCount);
      this.loading.set(false);
      this.prefetchNextPage(filters, cachedTotalCount, cachedItems.length);
      return;
    }

    this.api.getList(filters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          const effectiveTotalCount = this.resolveEffectiveTotalCount(this.page(), res.items.length, res.totalCount, res.totalPages);
          this.totalCount.set(effectiveTotalCount);
          this.pageCache.set(key, res.items);
          this.totalCountCache.set(key, effectiveTotalCount);
          this.compactCachesIfNeeded();
          this.prefetchNextPage(filters, effectiveTotalCount, res.items.length);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors du chargement des factures.');
          this.loading.set(false);
        }
      });
  }

  private buildFilters(page: number): InvoiceListFilters {
    const form = this.filtersForm.getRawValue();
    const phase = String(form.phase || 'all').toLowerCase();
    const statusFilter = phase === 'closed'
      ? 'closed'
      : phase === 'open'
        ? 'open'
        : phase === 'cancelled'
          ? 'cancelled'
        : undefined;

    return {
      page,
      pageSize: this.pageSize(),
      search: form.search || undefined,
      customer: form.customer || undefined,
      status: statusFilter,
      dateFrom: form.dateFrom || undefined,
      dateTo: form.dateTo || undefined,
      sortBy: this.sortDirection() === 'none' ? undefined : this.sortKey() ?? undefined,
      sortDirection: this.sortDirection() === 'none' ? undefined : this.sortDirection() as 'asc' | 'desc'
    };
  }

  private prefetchNextPage(currentFilters: InvoiceListFilters, currentTotalCount: number, currentItemsCount: number): void {
    const currentPage = currentFilters.page;
    const totalPages = Math.max(1, Math.ceil(currentTotalCount / currentFilters.pageSize));
    const nextPage = currentPage + 1;
    const shouldProbeNextPage = currentItemsCount >= currentFilters.pageSize;
    if (nextPage > totalPages && !shouldProbeNextPage) return;

    const nextFilters: InvoiceListFilters = { ...currentFilters, page: nextPage };
    const nextKey = this.buildCacheKey(nextFilters);
    if (this.pageCache.has(nextKey) && this.totalCountCache.has(nextKey)) return;

    this.api.getList(nextFilters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const effectiveTotalCount = this.resolveEffectiveTotalCount(nextPage, res.items.length, res.totalCount, res.totalPages);
          this.pageCache.set(nextKey, res.items);
          this.totalCountCache.set(nextKey, effectiveTotalCount);
          if (effectiveTotalCount > this.totalCount()) {
            this.totalCount.set(effectiveTotalCount);
          }
          this.compactCachesIfNeeded();
        },
        error: () => {}
      });
  }

  private buildCacheKey(filters: InvoiceListFilters): string {
    return JSON.stringify(filters);
  }

  private reloadSortedList(): void {
    this.clearCaches();
    this.page.set(1);
    this.load();
  }

  private compactCachesIfNeeded(): void {
    const maxEntries = 30;
    while (this.pageCache.size > maxEntries) {
      const oldestKey = this.pageCache.keys().next().value as string | undefined;
      if (!oldestKey) return;
      this.pageCache.delete(oldestKey);
      this.totalCountCache.delete(oldestKey);
    }
  }

  private clearCaches(): void {
    this.pageCache.clear();
    this.totalCountCache.clear();
  }

  private resolveEffectiveTotalCount(page: number, itemCount: number, totalCountCandidate: unknown, totalPagesCandidate?: unknown): number {
    const pageSize = this.pageSize();
    const parsed = Number(totalCountCandidate);
    const reported = Number.isFinite(parsed) && parsed >= 0 ? Math.floor(parsed) : 0;
    const pagesParsed = Number(totalPagesCandidate);
    const reportedPages = Number.isFinite(pagesParsed) && pagesParsed > 0 ? Math.floor(pagesParsed) : 1;
    const loadedThroughCurrentPage = itemCount > 0
      ? ((Math.max(1, page) - 1) * pageSize) + itemCount
      : 0;
    const minimumFromPages = reportedPages > 1
      ? ((reportedPages - 1) * pageSize) + 1
      : 0;

    return Math.max(reported, loadedThroughCurrentPage, minimumFromPages);
  }

  isOpenStatus(status: string): boolean {
    const s = (status || '').trim().toLowerCase();
    const compact = s.replace(/[\s_-]/g, '');
    return s === 'open'
      || s === 'o'
      || s === 'en attente'
      || compact === 'bostopen'
      || compact === 'enattente'
      || compact === 'unpaid'
      || compact === 'partiallypaid'
      || compact === 'partialpaid'
      || compact === 'overdue'
      || (compact.includes('open') && !compact.includes('close'));
  }

  isCancelledStatus(status: string): boolean {
    const s = (status || '').trim().toLowerCase();
    const compact = s.replace(/[\s_-]/g, '');
    return s === 'cancelled'
      || s === 'canceled'
      || s === 'annule'
      || compact.includes('cancel');
  }

  isClosedStatus(status: string): boolean {
    return !this.isOpenStatus(status) && !this.isCancelledStatus(status);
  }

  toggleSort(key: InvoiceSortKey): void {
    if (this.sortKey() !== key) {
      this.sortKey.set(key);
      this.sortDirection.set('asc');
      this.reloadSortedList();
      return;
    }

    const current = this.sortDirection();
    if (current === 'asc') {
      this.sortDirection.set('desc');
      this.reloadSortedList();
      return;
    }

    if (current === 'desc') {
      this.sortKey.set(null);
      this.sortDirection.set('none');
      this.reloadSortedList();
      return;
    }

    this.sortDirection.set('asc');
    this.reloadSortedList();
  }

  sortIndicator(key: InvoiceSortKey): string {
    if (this.sortKey() !== key) return '';
    if (this.sortDirection() === 'asc') return '↑';
    if (this.sortDirection() === 'desc') return '↓';
    return '';
  }

  statusLabel(status: string): 'En attente' | 'Cloturee' | 'Annulee' {
    if (this.isCancelledStatus(status)) return 'Annulee';
    return this.isOpenStatus(status) ? 'En attente' : 'Cloturee';
  }

  private partnerNameOf(doc: InvoiceListItem): string {
    return String(doc.cardName || doc.cardCode || '-').trim();
  }

  private sortedItems(rows: InvoiceListItem[]): InvoiceListItem[] {
    const key = this.sortKey();
    const direction = this.sortDirection();
    if (!key || direction === 'none') return rows;

    const multiplier = direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => this.compareInvoices(a, b, key) * multiplier);
  }

  private compareInvoices(a: InvoiceListItem, b: InvoiceListItem, key: InvoiceSortKey): number {
    if (key === 'number') return this.compareNumberOrText(a.docNum || `#${a.id}`, b.docNum || `#${b.id}`);
    if (key === 'partner') return this.compareText(a.cardName || a.cardCode || '-', b.cardName || b.cardCode || '-');
    if (key === 'date') return this.compareDate(a.docDate, b.docDate);
    if (key === 'status') return this.compareText(this.statusLabel(a.status), this.statusLabel(b.status));
    if (key === 'total') return this.compareNumber(a.docTotal, b.docTotal);
    return 0;
  }

  private compareText(a: unknown, b: unknown): number {
    return String(a ?? '').localeCompare(String(b ?? ''), 'fr', { sensitivity: 'base', numeric: true });
  }

  private compareNumber(a: unknown, b: unknown): number {
    return Number(a ?? 0) - Number(b ?? 0);
  }

  private compareNumberOrText(a: unknown, b: unknown): number {
    const left = Number(String(a ?? '').replace(/[^0-9.-]/g, ''));
    const right = Number(String(b ?? '').replace(/[^0-9.-]/g, ''));
    if (Number.isFinite(left) && Number.isFinite(right) && (left !== 0 || right !== 0)) {
      return left - right;
    }
    return this.compareText(a, b);
  }

  private compareDate(a: string | undefined, b: string | undefined): number {
    const left = a ? new Date(a).getTime() : 0;
    const right = b ? new Date(b).getTime() : 0;
    return left - right;
  }
}


