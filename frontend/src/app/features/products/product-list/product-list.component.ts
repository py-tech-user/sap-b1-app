import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Product, ProductApiService, ProductGroup } from '../../../core/services/product-api.service';
import { CatalogCartLine, CatalogCartService } from '../../../core/services/catalog-cart.service';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="product-list">
      <div class="header">
        <h1>Catalogue</h1>
        <button type="button" class="btn-primary" (click)="goToOrderFromCart()" [disabled]="cartLines().length === 0">
          Commander ({{ cartLines().length }})
        </button>
      </div>

      @if (loading()) {
        <div class="status">Chargement du catalogue...</div>
      } @else if (error()) {
        <div class="status error">{{ error() }}</div>
      } @else if (selectedGroupCode() === null) {
        <div class="group-grid">
          @for (group of groups(); track group.groupCode) {
            <button type="button" class="group-card" (click)="selectGroup(group.groupCode)">
              <strong>{{ group.groupName || ('Groupe ' + group.groupCode) }}</strong>
              <span>{{ group.itemsCount }} article(s)</span>
            </button>
          } @empty {
            <div class="status">Aucun groupe trouve.</div>
          }
        </div>
      } @else {
        <div class="subheader">
          <button type="button" class="btn-secondary" (click)="backToGroups()">Retour aux groupes</button>
          <h2>{{ selectedGroupName() }}</h2>
        </div>

        @if (loadingProducts()) {
          <div class="status">Chargement des articles en arriere-plan...</div>
        }

        @if (!loadingProducts() && visibleProducts().length === 0) {
          <div class="status">Aucun article dans cette categorie.</div>
        } @else {
          <div class="catalog-grid">
            @for (product of visibleProducts(); track product.id) {
              <article class="product-card">
                <div class="product-image-wrap">
                  @if (product.imageUrl && !isImageBroken(product.id)) {
                    <img [src]="product.imageUrl" [alt]="product.itemName" class="product-image" (error)="markImageBroken(product.id)" />
                  } @else {
                    <div class="product-image placeholder">IMG</div>
                  }
                </div>

                <div class="product-content">
                  <div class="mini-table">
                    <div class="k">Nom</div><div class="v">{{ product.itemName || '-' }}</div>
                    <div class="k">Code</div><div class="v">{{ product.itemCode || '-' }}</div>
                    <div class="k">Prix</div><div class="v">{{ product.price | number:'1.2-2' }} {{ currencyOf(product) }}</div>
                    <div class="k">Stock</div><div class="v" [class.low-stock]="product.stock < 10">{{ product.stock }}</div>
                    <div class="k">Groupe</div><div class="v">{{ product.groupName || '-' }}</div>
                  </div>
                  <div class="actions">
                    <button type="button" class="btn-primary btn-sm" (click)="addToCart(product)">Ajouter au panier</button>
                  </div>
                </div>
              </article>
            }
          </div>
        }
      }

      @if (cartLines().length > 0) {
        <div class="cart-panel">
          <h3>Panier</h3>
          @for (line of cartLines(); track line.itemCode) {
            <div class="cart-row">
              <div class="cart-main">
                <strong>{{ line.itemName }}</strong>
                <small>{{ line.itemCode }}</small>
              </div>
              <input type="number" min="1" [value]="line.quantity" (change)="onCartQtyChange(line.itemCode, $event)" />
              <button type="button" class="btn-danger" (click)="removeFromCart(line.itemCode)">Suppr.</button>
            </div>
          }
          <div class="cart-footer">
            <button type="button" class="btn-secondary" (click)="clearCart()">Vider panier</button>
            <button type="button" class="btn-primary" (click)="goToOrderFromCart()">Commander</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .subheader { display: flex; align-items: center; gap: .8rem; margin-bottom: .8rem; }
    .subheader h2 { margin: 0; font-size: 1.05rem; color: #1f2937; }
    .status { padding: 1rem; color: #374151; }
    .status.error { color: #b00020; }
    .btn-secondary { padding: 0.55rem 0.95rem; border-radius: 6px; border: 1px solid #d1d5db; background: white; cursor: pointer; font-weight: 500; }
    .group-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: .8rem; }
    .group-card { text-align: left; border: 1px solid #e5e7eb; background: #fff; border-radius: 10px; padding: .85rem; cursor: pointer; display: grid; gap: .25rem; }
    .group-card strong { color: #111827; font-size: .98rem; }
    .group-card span { color: #6b7280; font-size: .85rem; }
    .catalog-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: .75rem; }
    .product-card { background: #fff; border-radius: 10px; box-shadow: 0 1px 2px rgba(0,0,0,0.06); overflow: hidden; border: 1px solid #eef2f7; display: grid; grid-template-columns: 92px 1fr; }
    .product-image-wrap { width: 92px; height: 92px; background: #f8fafc; display: flex; align-items: center; justify-content: center; }
    .product-image { width: 100%; height: 100%; object-fit: cover; }
    .product-image.placeholder { font-size: 0.8rem; color: #94a3b8; font-weight: 600; }
    .product-content { padding: 0.55rem .65rem; }
    .actions { display: flex; justify-content: flex-end; margin-top: .4rem; }
    .btn-primary { padding: 0.55rem 0.95rem; border-radius: 6px; border: 1px solid #1976d2; background: #1976d2; color: #fff; cursor: pointer; font-weight: 600; }
    .btn-primary:disabled { background: #93c5fd; border-color: #93c5fd; cursor: not-allowed; }
    .btn-sm { padding: 0.35rem 0.6rem; font-size: 0.8rem; }
    .cart-panel { margin-top: 1rem; border: 1px solid #e5e7eb; border-radius: 10px; padding: .75rem; background: #fff; }
    .cart-panel h3 { margin: 0 0 .55rem; font-size: 1rem; }
    .cart-row { display: grid; grid-template-columns: 1fr 80px auto; gap: .5rem; align-items: center; margin-bottom: .5rem; }
    .cart-main { display: grid; }
    .cart-main small { color: #6b7280; }
    .cart-row input { border: 1px solid #d1d5db; border-radius: 6px; padding: .35rem .45rem; }
    .btn-danger { padding: 0.45rem 0.65rem; border-radius: 6px; border: 1px solid #dc2626; background: #fff; color: #dc2626; cursor: pointer; font-weight: 600; }
    .cart-footer { display: flex; justify-content: flex-end; gap: .5rem; margin-top: .3rem; }
    .mini-table { display: grid; grid-template-columns: 58px 1fr; gap: .22rem .45rem; align-items: baseline; }
    .mini-table .k { color: #6b7280; font-size: .78rem; font-weight: 600; }
    .mini-table .v { color: #111827; font-size: .82rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .low-stock { color: #dc2626; font-weight: 700; }
    @media (max-width: 720px) {
      .group-grid, .catalog-grid { grid-template-columns: 1fr; }
      .product-card { grid-template-columns: 80px 1fr; }
      .product-image-wrap { width: 80px; height: 80px; }
    }
  `]
})
export class ProductListComponent implements OnInit {
  private readonly cart = inject(CatalogCartService);
  private readonly router = inject(Router);
  groups = signal<ProductGroup[]>([]);
  products = signal<Product[]>([]);
  visibleProducts = signal<Product[]>([]);
  cartLines = signal<CatalogCartLine[]>([]);
  selectedGroupCode = signal<number | null>(null);
  loadingGroups = signal(true);
  loadingProducts = signal(false);
  loading = signal(true);
  error = signal('');
  private brokenImageIds = signal<Set<number>>(new Set<number>());

  constructor(private productApi: ProductApiService) {}

  ngOnInit(): void {
    this.cartLines.set(this.cart.getLines());
    this.loadGroups();
    this.loadProductsInBackground();
  }

  selectGroup(groupCode: number): void {
    this.selectedGroupCode.set(groupCode);
    const filtered = this.products().filter((p) => Number(p.groupCode ?? 0) === groupCode);
    this.visibleProducts.set(filtered);
  }

  backToGroups(): void {
    this.selectedGroupCode.set(null);
    this.visibleProducts.set([]);
  }

  selectedGroupName(): string {
    const code = this.selectedGroupCode();
    if (code === null) return '';
    return this.groups().find((g) => g.groupCode === code)?.groupName ?? `Groupe ${code}`;
  }

  currencyOf(product: Product): string {
    const anyP = product as any;
    return String(anyP.currency ?? anyP.Currency ?? 'MAD');
  }

  isImageBroken(id: number): boolean {
    return this.brokenImageIds().has(id);
  }

  markImageBroken(id: number): void {
    const next = new Set(this.brokenImageIds());
    next.add(id);
    this.brokenImageIds.set(next);
  }

  addToCart(product: Product): void {
    this.cart.addLine({
      itemCode: String(product.itemCode ?? '').trim(),
      itemName: String(product.itemName ?? '').trim(),
      quantity: 1,
      unitPrice: Number((product as any).price ?? 0),
      warehouseCode: String((product as any).warehouseCode ?? '01').trim() || '01'
    });
    this.cartLines.set(this.cart.getLines());
  }

  onCartQtyChange(itemCode: string, event: Event): void {
    const input = event.target as HTMLInputElement;
    this.cart.updateQuantity(itemCode, Number(input.value));
    this.cartLines.set(this.cart.getLines());
  }

  removeFromCart(itemCode: string): void {
    this.cart.removeLine(itemCode);
    this.cartLines.set(this.cart.getLines());
  }

  clearCart(): void {
    this.cart.clear();
    this.cartLines.set([]);
  }

  goToOrderFromCart(): void {
    if (this.cartLines().length === 0) return;
    this.router.navigate(['/orders/new'], { queryParams: { fromCatalog: '1' } });
  }

  private loadGroups(): void {
    this.loading.set(true);
    this.loadingGroups.set(true);
    this.error.set('');

    this.productApi.getGroups().subscribe({
      next: (groups) => {
        this.groups.set(groups);
        this.loadingGroups.set(false);
        // Show groups immediately; products continue in background.
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.error || 'Erreur chargement des groupes');
        this.loadingGroups.set(false);
        this.loading.set(false);
      }
    });
  }

  private loadProductsInBackground(): void {
    this.loadingProducts.set(true);

    this.productApi.getAll(1, 50000).subscribe({
      next: (res) => {
        const items = res.items ?? [];
        const enriched = items.map((p) => ({
          ...p,
          imageUrl: p.imageUrl || this.catalogImageFor(p)
        }));
        this.products.set(enriched);
        this.preloadImagesInBackground(enriched);

        const selected = this.selectedGroupCode();
        if (selected !== null) {
          this.visibleProducts.set(enriched.filter((p) => Number(p.groupCode ?? 0) === selected));
        }

        this.loadingProducts.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.error || 'Erreur chargement des articles');
        this.loadingProducts.set(false);
      }
    });
  }

  private preloadImagesInBackground(products: Product[]): void {
    if (typeof window === 'undefined') return;

    const uniqueUrls = Array.from(new Set(
      products
        .map((p) => String(p.imageUrl ?? '').trim())
        .filter((url) => url !== '')
    ));

    for (const url of uniqueUrls) {
      const img = new Image();
      img.decoding = 'async';
      img.loading = 'eager';
      img.src = url;
    }
  }

  private catalogImageFor(product: Product): string | undefined {
    const token = `${product.itemName ?? ''} ${product.itemCode ?? ''}`
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9]/g, '');

    const imageName = this.pickImageName(token);
    return imageName ? `/assets/catalog/${imageName}.jpg` : undefined;
  }

  private pickImageName(token: string): string | undefined {
    if (token.includes('webcam')) return 'Webcam';
    if (token.includes('ecrant24') || token.includes('ecran24')) return 'ecrant24Pouce';
    if (token.includes('supportecran') || token.includes('support')) return 'Supportecran';
    if (token.includes('sourisled')) return 'SourisLED';
    if (token.includes('souris')) return 'Souris';
    if (token.includes('pcportable') || token.includes('laptop')) return 'PcPortable';
    if (token.includes('ecouteur') || token.includes('filaire')) return 'Ecouteursfilaires';
    if (token.includes('disk') || token.includes('disque')) return 'disk';
    if (token.includes('cle') || token.includes('usb')) return 'cle';
    if (token.includes('clavier') || token.includes('azerty') || token.includes('qwerty') || token.includes('mecanique')) return 'ClavierMecanique';
    if (token.includes('casquefil')) return 'casquefil';
    if (token.includes('casquebluetooth') || token.includes('bluetooth')) return 'CasqueBluetooth';
    if (token.includes('batterie') || token.includes('battery')) return 'Batterie';
    return undefined;
  }
}
