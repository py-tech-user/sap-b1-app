import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Product, ProductApiService } from '../../../core/services/product-api.service';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="product-list">
      <div class="header">
        <h1>Catalogue</h1>
      </div>

      @if (loading()) {
        <div class="status">Chargement du catalogue...</div>
      } @else if (error()) {
        <div class="status error">{{ error() }}</div>
      } @else if (products().length === 0) {
        <div class="status">Aucun produit</div>
      } @else {
        <div class="catalog-grid">
          @for (product of products(); track product.id) {
            <article class="product-card">
              <div class="product-image-wrap">
                @if (product.imageUrl && !isImageBroken(product.id)) {
                  <img [src]="product.imageUrl" [alt]="product.itemName" class="product-image" (error)="markImageBroken(product.id)" />
                } @else {
                  <div class="product-image placeholder">IMG</div>
                }
              </div>

              <div class="product-content">
                <h3 class="product-name">{{ product.itemName || '-' }}</h3>
                <div class="product-meta">{{ product.itemCode || '-' }}</div>

                <div class="product-row">
                  <span class="label">Prix</span>
                  <strong>{{ product.price | number:'1.2-2' }}</strong>
                </div>

                <div class="product-row">
                  <span class="label">Stock</span>
                  <strong [class.low-stock]="product.stock < 10">{{ product.stock }}</strong>
                </div>
              </div>
            </article>
          }
        </div>

        <div class="pager">
          <button type="button" class="btn-secondary" (click)="prev()" [disabled]="page() <= 1">Précédent</button>
          <span>Page {{ page() }} / {{ totalPages() }}</span>
          <button type="button" class="btn-secondary" (click)="next()" [disabled]="page() >= totalPages()">Suivant</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .status { padding: 1rem; color: #374151; }
    .status.error { color: #b00020; }
    .pager { display: flex; justify-content: space-between; align-items: center; margin-top: 0.75rem; }
    .btn-secondary { padding: 0.65rem 1.25rem; border-radius: 6px; border: 1px solid #d1d5db; background: white; cursor: pointer; font-weight: 500; }
    .btn-secondary:disabled { opacity: 0.6; cursor: not-allowed; }
    .catalog-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 1rem; }
    .product-card { background: #fff; border-radius: 10px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); overflow: hidden; border: 1px solid #f0f0f0; }
    .product-image-wrap { height: 160px; background: #f8fafc; display: flex; align-items: center; justify-content: center; }
    .product-image { width: 100%; height: 100%; object-fit: cover; }
    .product-image.placeholder { font-size: 0.9rem; color: #94a3b8; font-weight: 600; }
    .product-content { padding: 0.85rem; display: flex; flex-direction: column; gap: 0.5rem; }
    .product-name { margin: 0; font-size: 1rem; color: #111827; }
    .product-meta { color: #6b7280; font-size: 0.82rem; }
    .product-row { display: flex; justify-content: space-between; align-items: center; }
    .label { color: #6b7280; }
    .low-stock { color: #dc3545; }
  `]
})
export class ProductListComponent implements OnInit {
  products = signal<Product[]>([]);
  page = signal(1);
  pageSize = signal(15);
  totalCount = signal(0);
  loading = signal(true);
  error = signal('');
  private brokenImageIds = signal<Set<number>>(new Set<number>());
  private readonly pageCache = new Map<number, Product[]>();

  constructor(private productApi: ProductApiService) {}

  ngOnInit(): void {
    this.loadPage(1);
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
  }

  prev(): void {
    if (this.page() <= 1) return;
    this.loadPage(this.page() - 1);
  }

  next(): void {
    if (this.page() >= this.totalPages()) return;
    this.loadPage(this.page() + 1);
  }

  isImageBroken(id: number): boolean {
    return this.brokenImageIds().has(id);
  }

  markImageBroken(id: number): void {
    const next = new Set(this.brokenImageIds());
    next.add(id);
    this.brokenImageIds.set(next);
  }

  private loadPage(targetPage: number): void {
    const cached = this.pageCache.get(targetPage);
    if (cached) {
      this.products.set(cached);
      this.page.set(targetPage);
      this.loading.set(false);
      this.prefetchNextPages(targetPage + 1);
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.productApi.getAll(targetPage, this.pageSize()).subscribe({
      next: (res) => {
        const items = res.items ?? [];
        this.products.set(items);
        this.page.set(targetPage);
        this.totalCount.set(Number.isFinite(res.totalCount) ? res.totalCount : items.length);
        this.pageCache.set(targetPage, items);
        this.prefetchNextPages(targetPage + 1);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.error || 'Erreur chargement catalogue SAP');
        this.loading.set(false);
      },
    });
  }

  private prefetchNextPages(targetPage: number): void {
    if (targetPage > this.totalPages()) return;

    if (this.pageCache.has(targetPage)) {
      setTimeout(() => this.prefetchNextPages(targetPage + 1), 0);
      return;
    }

    this.productApi.getAll(targetPage, this.pageSize()).subscribe({
      next: (res) => {
        const items = res.items ?? [];
        this.pageCache.set(targetPage, items);
        const nextTotal = Number.isFinite(res.totalCount) ? res.totalCount : this.totalCount();
        if (nextTotal > this.totalCount()) {
          this.totalCount.set(nextTotal);
        }
        setTimeout(() => this.prefetchNextPages(targetPage + 1), 0);
      },
      error: () => {
      }
    });
  }
}
