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
                @if (product.imageUrl) {
                  <img [src]="product.imageUrl" [alt]="product.itemName" class="product-image" />
                } @else {
                  <div class="product-image placeholder">📦</div>
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
      }
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .status { padding: 1rem; color: #374151; }
    .status.error { color: #b00020; }
    .catalog-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 1rem; }
    .product-card { background: #fff; border-radius: 10px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); overflow: hidden; border: 1px solid #f0f0f0; }
    .product-image-wrap { height: 160px; background: #f8fafc; display: flex; align-items: center; justify-content: center; }
    .product-image { width: 100%; height: 100%; object-fit: cover; }
    .product-image.placeholder { font-size: 2rem; color: #94a3b8; }
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
  loading = signal(true);
  error = signal('');

  constructor(private productApi: ProductApiService) {}

  ngOnInit(): void {
    this.productApi.getAll(1, 500).subscribe({
      next: (res) => {
        this.products.set(res.items ?? []);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.error || 'Erreur chargement catalogue SAP');
        this.loading.set(false);
      },
    });
  }
}
