import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialApiService } from '../../../core/services/commercial-api.service';
import {
  CommercialDocument,
  CommercialListFilters,
  CommercialResource
} from '../../../core/models/models';
import { COMMERCIAL_META, STATUS_ACTIONS } from '../commercial-meta';

const COMMERCIAL_REFRESH_EVENT = 'commercialDocuments:updated';
type DocumentSortKey = 'number' | 'partner' | 'date' | 'status' | 'total';
type SortDirection = 'none' | 'asc' | 'desc';

@Component({
  selector: 'app-document-list',
  imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <div class="header">
        <h1>{{ meta().icon }} {{ meta().title }}</h1>
        <div class="header-actions">
          <a class="btn-primary" [routerLink]="['/', resource(), 'new']">+ {{ meta().createLabel }}</a>
          @if (toast()) {
            <span class="action-feedback">{{ toast() }}</span>
          }
        </div>
      </div>

      <form [formGroup]="filtersForm" class="filters" (ngSubmit)="applyFilters()">
        <input formControlName="search" placeholder="Recherche" (input)="onFiltersChanged()" list="doc-search-suggestions" />
        <datalist id="doc-search-suggestions">
          @for (suggestion of searchSuggestions(); track suggestion) {
            <option [value]="suggestion"></option>
          }
        </datalist>
        <input formControlName="customer" placeholder="Client" (input)="onFiltersChanged()" list="doc-customer-suggestions" />
        <datalist id="doc-customer-suggestions">
          @for (suggestion of customerSuggestions(); track suggestion) {
            <option [value]="suggestion"></option>
          }
        </datalist>
        <select formControlName="phase" (change)="onFiltersChanged()">
          <option value="all">Tous les statuts</option>
          <option value="open">En attente</option>
          <option value="closed">Clotures</option>
          <option value="cancelled">Annules</option>
        </select>
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
        <div class="alert alert-error">
          <p>{{ error() }}</p>
          <button type="button" class="btn-outline" (click)="applyFilters()">Reessayer</button>
        </div>
      } @else {
        <table>
          <thead>
            <tr>
              <th><button type="button" class="sort-header" (click)="toggleSort('number')">Numero {{ sortIndicator('number') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('partner')">Raison sociale {{ sortIndicator('partner') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('date')">Date {{ sortIndicator('date') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('status')">Statut {{ sortIndicator('status') }}</button></th>
              <th><button type="button" class="sort-header" (click)="toggleSort('total')">Total {{ sortIndicator('total') }}</button></th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (doc of visibleItems(); track doc.id) {
              <tr>
                <td>{{ numberOf(doc) }}</td>
                <td>{{ partnerNameOf(doc) }}</td>
                <td>{{ dateOf(doc) ? (dateOf(doc) | date:'dd/MM/yyyy') : '-' }}</td>
                <td>
                  <span class="badge" [class.badge-open]="isOpenStatus(doc.status)" [class.badge-closed]="isClosedStatus(doc.status)" [class.badge-cancelled]="isCancelledStatus(doc.status)">
                    {{ statusPhase(doc.status) }}
                  </span>
                </td>
                <td>{{ totalOf(doc) | number:'1.2-2' }}</td>
                <td class="row-actions">
                  <a class="btn-sm" [routerLink]="['/', resource(), doc.id]" [queryParams]="detailQueryParams()">Voir</a>
                  @if (canManageDocument(doc)) {
                    <a class="btn-sm" [routerLink]="['/', resource(), doc.id, 'edit']" [queryParams]="detailQueryParams()">Modifier</a>
                  }
                  @for (a of allowedActions(doc.status); track a.label) {
                    <button class="btn-sm" type="button" (click)="changeStatus(doc, a.to)">{{ a.label }}</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">Aucune donnee</td></tr>
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
    .header-actions { display: flex; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
    .filters { display: grid; grid-template-columns: repeat(6, minmax(120px, 1fr)); gap: 0.5rem; align-items: center; }
    .filters input, .filters select { padding: 0.45rem 0.6rem; border: 1px solid #d7d7d7; border-radius: 6px; }
    .date-field { display: flex; flex-direction: column; gap: 0.2rem; font-size: 0.85rem; color: #374151; }
    .loading, .error, .empty { text-align: center; padding: 1rem; }
    .error { color: #b00020; }
    .alert { border-radius: 6px; padding: 1rem; margin-bottom: 1rem; }
    .alert-error { background: #fce4ec; border-left: 4px solid #c2185b; color: #880e4f; }
    .alert-error p { margin: 0 0 0.5rem 0; }
    .alert-error button { margin-top: 0.5rem; }
    .badge { display: inline-block; border-radius: 999px; padding: 0.2rem 0.55rem; font-size: 0.78rem; }
    .badge-open { background: #e8f5e9; color: #1b5e20; }
    .badge-closed { background: #f3f4f6; color: #374151; }
    .sort-header { border: 0; background: transparent; padding: 0; color: inherit; font: inherit; font-weight: 700; cursor: pointer; text-align: left; }
    .sort-header:hover { color: #2563eb; }
    .badge-cancelled { background: #fdecea; color: #c62828; }
    .row-actions { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .pager { display: flex; justify-content: space-between; align-items: center; }
    .action-feedback { color: #1b5e20; font-weight: 700; }
    @media (max-width: 1024px) {
      .filters { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class DocumentListComponent implements OnInit, OnDestroy {
  private readonly api = inject(CommercialApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  readonly resource = signal<CommercialResource>(this.resolveResource());
  readonly meta = computed(() => COMMERCIAL_META[this.resource()]);

  readonly loading = signal(false);
  readonly error = signal('');
  readonly toast = signal('');
  readonly items = signal<CommercialDocument[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly totalCount = signal(0);
  readonly sortKey = signal<DocumentSortKey | null>(null);
  readonly sortDirection = signal<SortDirection>('none');
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  readonly customerSuggestions = computed(() =>
    [...new Set(this.items().map(doc => this.partnerNameOf(doc)).filter(v => v && v !== '-'))].slice(0, 30)
  );
  readonly searchSuggestions = computed(() => {
    const numbers = this.items().map(doc => this.numberOf(doc)).filter(v => v && v !== '-');
    const clients = this.items().map(doc => this.partnerNameOf(doc)).filter(v => v && v !== '-');
    return [...new Set([...numbers, ...clients])].slice(0, 40);
  });
  private readonly pageCache = new Map<string, CommercialDocument[]>();
  private readonly totalCountCache = new Map<string, number>();

  readonly filtersForm = this.fb.group({
    search: [''],
    customer: [''],
    phase: ['all'],
    dateFrom: [''],
    dateTo: ['']
  });
  readonly visibleItems = computed(() => {
    const customerRaw = String(this.filtersForm.getRawValue().customer ?? '').trim().toLowerCase();
    const filtered = !customerRaw
      ? this.items()
      : this.items().filter((doc) => {
        const raw = doc as any;
        const cardCode = String(raw.cardCode ?? raw.CardCode ?? doc.cardCode ?? '').trim().toLowerCase();
        const partner = this.partnerNameOf(doc).toLowerCase();
        return partner.includes(customerRaw) || cardCode.includes(customerRaw);
      });

    return this.sortedItems(filtered);
  });
  private filterDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  private readonly refreshListener: EventListener = (event: Event) => {
    const detail = (event as CustomEvent<any>).detail;
    if (!detail || detail.resource !== this.resource()) return;

    const mapped = Array.isArray(detail.items) ? detail.items as CommercialDocument[] : [];
    this.items.set(mapped);
    this.totalCount.set(typeof detail.totalCount === 'number' ? detail.totalCount : mapped.length);
    this.page.set(typeof detail.page === 'number' ? detail.page : this.page());
    this.pageSize.set(15);
  };

  ngOnInit(): void {
    this.load();
    if (typeof window !== 'undefined') {
      window.addEventListener(COMMERCIAL_REFRESH_EVENT, this.refreshListener);
    }
  }

  ngOnDestroy(): void {
    if (this.filterDebounceHandle) {
      clearTimeout(this.filterDebounceHandle);
      this.filterDebounceHandle = null;
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener(COMMERCIAL_REFRESH_EVENT, this.refreshListener);
    }
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
    this.page.update(p => p - 1);
    this.load();
  }

  nextPage(): void {
    if (this.page() >= this.totalPages()) return;
    this.page.update(p => p + 1);
    this.load();
  }

  canManageDocument(doc: CommercialDocument): boolean {
    if (this.resource() !== 'quotes' && this.resource() !== 'orders') return false;
    return this.isOpenStatus(doc.status);
  }

  statusPhase(status: string): 'En attente' | 'Cloture' | 'Annule' {
    if (this.isCancelledStatus(status)) return 'Annule';
    return this.isOpenStatus(status) ? 'En attente' : 'Cloture';
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

  allowedActions(status: string): { from: string; to: string; label: string }[] {
    if (this.resource() !== 'quotes' && this.resource() !== 'orders') return [];
    if (!this.isOpenStatus(status)) return [];
    const transitions = STATUS_ACTIONS[this.resource()] ?? [];
    const current = (status || '').toLowerCase();
    return transitions.filter(t => t.from === current);
  }

  toggleSort(key: DocumentSortKey): void {
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

  sortIndicator(key: DocumentSortKey): string {
    if (this.sortKey() !== key) return '';
    if (this.sortDirection() === 'asc') return '↑';
    if (this.sortDirection() === 'desc') return '↓';
    return '';
  }

  changeStatus(doc: CommercialDocument, status: string): void {
    if (!confirm(`Confirmer le changement de statut vers ${status} ?`)) return;
    this.api.updateStatus(this.resource(), doc.id, status)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de changement de statut.');
            return;
          }
          this.toast.set('Statut mis a jour.');
          this.load();
          this.clearToastLater();
        },
        error: () => this.error.set('Erreur lors du changement de statut.')
      });
  }

  numberOf(doc: CommercialDocument): string {
    return doc.docNum || doc.documentNumber || `#${doc.id}`;
  }

  totalOf(doc: CommercialDocument): number {
    return doc.docTotal ?? doc.totalAmount ?? 0;
  }

  dateOf(doc: CommercialDocument): string | undefined {
    return doc.docDate || doc.postingDate || doc.dueDate;
  }

  partnerNameOf(doc: CommercialDocument): string {
    const raw = doc as any;
    const value =
      doc.customerName
      ?? raw.cardName
      ?? raw.CardName
      ?? raw.partnerName
      ?? raw.raisonSociale
      ?? raw.CustomerName
      ?? raw.customer
      ?? raw.CardCode
      ?? doc.cardCode;

    const text = String(value ?? '').trim();
    return text || '-';
  }

  private sortedItems(rows: CommercialDocument[]): CommercialDocument[] {
    const key = this.sortKey();
    const direction = this.sortDirection();
    if (!key || direction === 'none') return rows;

    const multiplier = direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => this.compareDocuments(a, b, key) * multiplier);
  }

  private compareDocuments(a: CommercialDocument, b: CommercialDocument, key: DocumentSortKey): number {
    if (key === 'number') return this.compareNumberOrText(this.numberOf(a), this.numberOf(b));
    if (key === 'partner') return this.compareText(this.partnerNameOf(a), this.partnerNameOf(b));
    if (key === 'date') return this.compareDate(this.dateOf(a), this.dateOf(b));
    if (key === 'status') return this.compareText(this.statusPhase(a.status), this.statusPhase(b.status));
    if (key === 'total') return this.compareNumber(this.totalOf(a), this.totalOf(b));
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

  detailQueryParams(): { returnTo: string } {
    return { returnTo: this.router.url || `/${this.resource()}` };
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');

    const filters = this.buildFilters(this.page());
    const cacheKey = this.buildCacheKey(filters);
    const bypassCache = false;

    if (!bypassCache) {
      const cachedItems = this.pageCache.get(cacheKey);
      const cachedTotalCount = this.totalCountCache.get(cacheKey);
      if (cachedItems && typeof cachedTotalCount === 'number') {
        this.items.set(cachedItems);
        this.totalCount.set(cachedTotalCount);
        this.loading.set(false);
        this.prefetchNextPage(filters, cachedTotalCount, cachedItems.length);
        return;
      }
    }

    this.api.getList(this.resource(), filters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const data = res.data;
          if (!data) {
            this.items.set([]);
            this.totalCount.set(0);
            this.loading.set(false);
            return;
          }

          const source = Array.isArray(data.items) ? data.items : [];
          const resolvedTotalCount = this.resolveEffectiveTotalCount(this.page(), source.length, data.totalCount, data.totalPages);
          this.items.set(source);
          this.totalCount.set(resolvedTotalCount);
          if (!bypassCache) {
            this.pageCache.set(cacheKey, source);
            this.totalCountCache.set(cacheKey, resolvedTotalCount);
            this.compactCachesIfNeeded();
            this.prefetchNextPage(filters, resolvedTotalCount, source.length);
          }
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Erreur lors du chargement des donnees.');
          this.loading.set(false);
        }
      });
  }

  private clearToastLater(): void {
    setTimeout(() => this.toast.set(''), 2500);
  }

  private buildFilters(page: number): CommercialListFilters {
    const formValue = this.filtersForm.getRawValue();
    const phase = String(formValue.phase || 'all').trim().toLowerCase();
    const openStatusFilter = this.resource() === 'invoices' ? 'open' : 'O';
    const closedStatusFilter = this.resource() === 'invoices' ? 'closed' : 'C';
    const cancelledStatusFilter = 'cancelled';

    return {
      page,
      pageSize: this.pageSize(),
      search: formValue.search || undefined,
      customer: formValue.customer || undefined,
      status: phase === 'open'
        ? openStatusFilter
        : phase === 'closed'
          ? closedStatusFilter
          : phase === 'cancelled'
            ? cancelledStatusFilter
            : undefined,
      dateFrom: formValue.dateFrom || undefined,
      dateTo: formValue.dateTo || undefined,
      sortBy: this.sortDirection() === 'none' ? undefined : this.sortKey() ?? undefined,
      sortDirection: this.sortDirection() === 'none' ? undefined : this.sortDirection() as 'asc' | 'desc'
    };
  }

  private prefetchNextPage(currentFilters: CommercialListFilters, currentTotalCount: number, currentItemsCount: number): void {
    const currentPage = currentFilters.page ?? 1;
    const pageSize = currentFilters.pageSize ?? this.pageSize();
    const totalPages = Math.max(1, Math.ceil(currentTotalCount / pageSize));
    const nextPage = currentPage + 1;
    const shouldProbeNextPage = currentItemsCount >= pageSize;
    if (nextPage > totalPages && !shouldProbeNextPage) return;

    const nextFilters: CommercialListFilters = { ...currentFilters, page: nextPage };
    const nextKey = this.buildCacheKey(nextFilters);
    if (this.pageCache.has(nextKey) && this.totalCountCache.has(nextKey)) return;

    this.api.getList(this.resource(), nextFilters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const data = res.data;
          if (!data) return;

          const nextItems = Array.isArray(data.items) ? data.items : [];
          const nextTotalCount = this.resolveEffectiveTotalCount(nextPage, nextItems.length, data.totalCount, data.totalPages);
          this.pageCache.set(nextKey, nextItems);
          this.totalCountCache.set(nextKey, nextTotalCount);
          if (nextTotalCount > this.totalCount()) {
            this.totalCount.set(nextTotalCount);
          }
          this.compactCachesIfNeeded();
        },
        error: () => {
        }
      });
  }

  private buildCacheKey(filters: CommercialListFilters): string {
    return JSON.stringify({
      resource: this.resource(),
      page: filters.page ?? 1,
      pageSize: filters.pageSize ?? this.pageSize(),
      search: filters.search ?? '',
      customer: filters.customer ?? '',
      status: filters.status ?? '',
      dateFrom: filters.dateFrom ?? '',
      dateTo: filters.dateTo ?? '',
      sortBy: filters.sortBy ?? '',
      sortDirection: filters.sortDirection ?? ''
    });
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

  private resolveResource(): CommercialResource {
    const routeData = this.route.snapshot.data['resource'] as CommercialResource | undefined;
    if (routeData) return routeData;
    const parentData = this.route.snapshot.parent?.data['resource'] as CommercialResource | undefined;
    return parentData ?? 'orders';
  }
}


