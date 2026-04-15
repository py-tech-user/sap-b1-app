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
        <input formControlName="search" placeholder="Recherche" (input)="onFiltersChanged()" />
        <input formControlName="customer" placeholder="Client" (input)="onFiltersChanged()" />
        <input type="date" formControlName="dateFrom" (change)="onFiltersChanged()" />
        <input type="date" formControlName="dateTo" (change)="onFiltersChanged()" />
        <button type="submit" class="btn-primary">Filtrer</button>
        <button type="button" class="btn-outline" (click)="resetFilters()">Réinitialiser</button>
      </form>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else if (error()) {
        <div class="alert alert-error">
          <p>{{ error() }}</p>
          <button type="button" class="btn-outline" (click)="applyFilters()">Réessayer</button>
        </div>
      } @else {
        @if (isInvoicesResource()) {
          <h3>Factures ouvertes</h3>
          <table>
            <thead>
              <tr>
                <th>Numéro</th>
                <th>Raison social</th>
                <th>Date</th>
                <th>Statut</th>
                <th>Total</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (doc of openInvoices(); track doc.id) {
                <tr>
                  <td>{{ numberOf(doc) }}</td>
                  <td>{{ partnerNameOf(doc) }}</td>
                  <td>{{ dateOf(doc) ? (dateOf(doc) | date:'dd/MM/yyyy') : '-' }}</td>
                  <td>
                    <span class="badge badge-open">{{ statusPhase(doc.status) }}</span>
                  </td>
                  <td>{{ totalOf(doc) | number:'1.2-2' }}</td>
                  <td class="row-actions">
                    <a class="btn-sm" [routerLink]="['/', resource(), doc.id]" [queryParams]="detailQueryParams()">Voir</a>
                    @if (canManageDocument(doc)) {
                      <a class="btn-sm" [routerLink]="['/', resource(), doc.id, 'edit']" [queryParams]="detailQueryParams()">Modifier</a>
                      <button class="btn-sm btn-danger" type="button" (click)="cancelDocument(doc)">Annuler</button>
                    }
                    @for (a of allowedActions(doc.status); track a.label) {
                      <button class="btn-sm" type="button" (click)="changeStatus(doc, a.to)">{{ a.label }}</button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="empty">Aucune facture ouverte</td></tr>
              }
            </tbody>
          </table>

          <h3>Factures clôturées</h3>
          <table>
            <thead>
              <tr>
                <th>Numéro</th>
                <th>Raison social</th>
                <th>Date</th>
                <th>Statut</th>
                <th>Total</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (doc of closedInvoices(); track doc.id) {
                <tr>
                  <td>{{ numberOf(doc) }}</td>
                  <td>{{ partnerNameOf(doc) }}</td>
                  <td>{{ dateOf(doc) ? (dateOf(doc) | date:'dd/MM/yyyy') : '-' }}</td>
                  <td>
                    <span class="badge badge-closed">{{ statusPhase(doc.status) }}</span>
                  </td>
                  <td>{{ totalOf(doc) | number:'1.2-2' }}</td>
                  <td class="row-actions">
                    <a class="btn-sm" [routerLink]="['/', resource(), doc.id]" [queryParams]="detailQueryParams()">Voir</a>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="empty">Aucune facture clôturée</td></tr>
              }
            </tbody>
          </table>
        } @else {
          <table>
            <thead>
              <tr>
                <th>Numéro</th>
                <th>Raison social</th>
                <th>Date</th>
                <th>Statut</th>
                <th>Total</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (doc of items(); track doc.id) {
                <tr>
                  <td>{{ numberOf(doc) }}</td>
                  <td>{{ partnerNameOf(doc) }}</td>
                  <td>{{ dateOf(doc) ? (dateOf(doc) | date:'dd/MM/yyyy') : '-' }}</td>
                  <td>
                    <span class="badge" [class.badge-open]="isOpenStatus(doc.status)" [class.badge-closed]="!isOpenStatus(doc.status)">
                      {{ statusPhase(doc.status) }}
                    </span>
                  </td>
                  <td>{{ totalOf(doc) | number:'1.2-2' }}</td>
                  <td class="row-actions">
                    <a class="btn-sm" [routerLink]="['/', resource(), doc.id]" [queryParams]="detailQueryParams()">Voir</a>
                    @for (a of allowedActions(doc.status); track a.label) {
                      <button class="btn-sm" type="button" (click)="changeStatus(doc, a.to)">{{ a.label }}</button>
                    }
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="empty">Aucune donnée</td></tr>
              }
            </tbody>
          </table>
        }

        <div class="pager">
          <button class="btn-outline" type="button" (click)="prevPage()" [disabled]="page() <= 1">← Précédent</button>
          <span>Page {{ page() }} / {{ totalPages() }}</span>
          <button class="btn-outline" type="button" (click)="nextPage()" [disabled]="page() >= totalPages()">Suivant →</button>
        </div>
      }

    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .header-actions { display: flex; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
    .filters { display: grid; grid-template-columns: repeat(6, minmax(120px, 1fr)); gap: 0.5rem; align-items: center; }
    .filters input, .filters select { padding: 0.45rem 0.6rem; border: 1px solid #d7d7d7; border-radius: 6px; }
    .loading, .error, .empty { text-align: center; padding: 1rem; }
    .error { color: #b00020; }
    .alert { border-radius: 6px; padding: 1rem; margin-bottom: 1rem; }
    .alert-error { background: #fce4ec; border-left: 4px solid #c2185b; color: #880e4f; }
    .alert-error p { margin: 0 0 0.5rem 0; }
    .alert-error button { margin-top: 0.5rem; }
    .badge { display: inline-block; border-radius: 999px; padding: 0.2rem 0.55rem; font-size: 0.78rem; }
    .badge-open { background: #e8f5e9; color: #1b5e20; }
    .badge-closed { background: #f3f4f6; color: #374151; }
    .row-actions { display: flex; flex-wrap: wrap; gap: 0.25rem; }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .btn-danger { background: #fdecea; color: #c62828; }
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
  readonly forcedDocPhase = signal<'all' | 'open' | 'closed'>(this.resolveForcedDocPhase());
  readonly meta = computed(() => COMMERCIAL_META[this.resource()]);
  readonly isInvoicesResource = computed(() => this.resource() === 'invoices');
  readonly openInvoices = computed(() => this.items().filter(doc => this.isOpenStatus(doc.status)));
  readonly closedInvoices = computed(() => this.items().filter(doc => !this.isOpenStatus(doc.status)));

  readonly loading = signal(false);
  readonly error = signal('');
  readonly toast = signal('');
  readonly items = signal<CommercialDocument[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly totalCount = signal(0);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  private readonly pageCache = new Map<string, CommercialDocument[]>();
  private readonly totalCountCache = new Map<string, number>();

  readonly filtersForm = this.fb.group({
    search: [''],
    customer: [''],
    dateFrom: [''],
    dateTo: ['']
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
    this.filtersForm.reset({ search: '', customer: '', dateFrom: '', dateTo: '' });
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

  isEditable(status: string): boolean {
    return this.isOpenStatus(status);
  }

  canManageDocument(doc: CommercialDocument): boolean {
    if (this.resource() !== 'quotes' && this.resource() !== 'orders') return false;
    return this.isOpenStatus(doc.status);
  }

  cancelDocument(doc: CommercialDocument): void {
    if (!this.canManageDocument(doc)) {
      this.error.set('Annulation autorisee uniquement pour un devis/BC en statut Open.');
      return;
    }
    this.remove(doc);
  }

  statusPhase(status: string): 'En attente' | 'Clôturé' {
    return this.isOpenStatus(status) ? 'En attente' : 'Clôturé';
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

  allowedActions(status: string): { from: string; to: string; label: string }[] {
    if (this.resource() !== 'quotes' && this.resource() !== 'orders') return [];
    if (!this.isOpenStatus(status)) return [];
    const transitions = STATUS_ACTIONS[this.resource()] ?? [];
    const current = (status || '').toLowerCase();
    return transitions.filter(t => t.from === current);
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
          this.toast.set('Statut mis à jour.');
          this.load();
          this.clearToastLater();
        },
        error: () => this.error.set('Erreur lors du changement de statut.')
      });
  }

  remove(doc: CommercialDocument): void {
    if (!confirm(`Supprimer ${this.meta().singular} ${this.numberOf(doc)} ?`)) return;

    this.api.delete(this.resource(), doc.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Suppression impossible.');
            return;
          }
          this.toast.set('Suppression effectuée.');
          this.load();
          this.clearToastLater();
        },
        error: () => this.error.set('Erreur lors de la suppression.')
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
          this.error.set('Erreur lors du chargement des données.');
          this.loading.set(false);
        }
      });
  }

  private clearToastLater(): void {
    setTimeout(() => this.toast.set(''), 2500);
  }

  private buildFilters(page: number): CommercialListFilters {
    const formValue = this.filtersForm.getRawValue();
    const phase = (this.forcedDocPhase() || 'all').trim().toLowerCase();
    const openStatusFilter = this.resource() === 'invoices' ? 'open' : 'O';
    const closedStatusFilter = this.resource() === 'invoices' ? 'closed' : 'C';

    return {
      page,
      pageSize: this.pageSize(),
      search: formValue.search || undefined,
      customer: formValue.customer || undefined,
      status: phase === 'open' ? openStatusFilter : phase === 'closed' ? closedStatusFilter : undefined,
      dateFrom: formValue.dateFrom || undefined,
      dateTo: formValue.dateTo || undefined
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
      dateTo: filters.dateTo ?? ''
    });
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

  private resolveForcedDocPhase(): 'all' | 'open' | 'closed' {
    const routeValue = this.route.snapshot.data['docPhase'] as string | undefined;
    if (routeValue === 'open' || routeValue === 'closed') return routeValue;
    return 'all';
  }
}
