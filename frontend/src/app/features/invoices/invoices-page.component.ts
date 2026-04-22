import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { InvoiceListFilters, InvoiceListItem, InvoicesApiService } from './invoices-api.service';

@Component({
  selector: 'app-invoices-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <div class="header">
        <h1>🧾 Factures</h1>
        <a class="btn-primary" [routerLink]="['/factures/new']">+ Nouvelle facture</a>
      </div>

      <form [formGroup]="filtersForm" class="filters" (ngSubmit)="applyFilters()">
        <input formControlName="search" placeholder="Recherche" (input)="onFiltersChanged()" />
        <input formControlName="customer" placeholder="Client" (input)="onFiltersChanged()" />
        <select formControlName="phase" (change)="onFiltersChanged()">
          <option value="all">Tous les statuts</option>
          <option value="open">En attente</option>
          <option value="closed">Cloturees</option>
          <option value="cancelled">Annulees</option>
        </select>
        <input type="date" formControlName="dateFrom" (change)="onFiltersChanged()" />
        <input type="date" formControlName="dateTo" (change)="onFiltersChanged()" />
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
              <th>Numero</th>
              <th>Client</th>
              <th>Date</th>
              <th>Total</th>
              <th>Statut</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            @for (doc of items(); track doc.id) {
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
          <button class="btn-outline" type="button" (click)="prevPage()" [disabled]="page() <= 1">← Precedent</button>
          <span>Page {{ page() }} / {{ totalPages() }}</span>
          <button class="btn-outline" type="button" (click)="nextPage()" [disabled]="page() >= totalPages()">Suivant →</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: center; }
    .filters { display: grid; grid-template-columns: repeat(6, minmax(120px, 1fr)); gap: 0.5rem; align-items: center; }
    .filters input, .filters select { padding: 0.45rem 0.6rem; border: 1px solid #d7d7d7; border-radius: 6px; }
    .loading, .empty { text-align: center; padding: 1rem; }
    .alert-error { background: #fce4ec; border-left: 4px solid #c2185b; color: #880e4f; padding: 1rem; border-radius: 6px; }
    .badge { display: inline-block; border-radius: 999px; padding: 0.2rem 0.55rem; font-size: 0.78rem; }
    .badge-open { background: #e8f5e9; color: #1b5e20; }
    .badge-closed { background: #f3f4f6; color: #374151; }
    .badge-cancelled { background: #fdecea; color: #c62828; }
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
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  private readonly pageCache = new Map<string, InvoiceListItem[]>();
  private readonly totalCountCache = new Map<string, number>();
  private filterDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  readonly filtersForm = this.fb.group({
    search: [''],
    customer: [''],
    phase: ['all'],
    dateFrom: [''],
    dateTo: ['']
  });

  ngOnInit(): void {
    this.load();
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
      dateTo: form.dateTo || undefined
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

  statusLabel(status: string): 'En attente' | 'Cloturee' | 'Annulee' {
    if (this.isCancelledStatus(status)) return 'Annulee';
    return this.isOpenStatus(status) ? 'En attente' : 'Cloturee';
  }
}
