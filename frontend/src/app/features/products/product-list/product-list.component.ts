import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { EMPTY, from } from 'rxjs';
import { catchError, concatMap, finalize, map } from 'rxjs/operators';
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
        <div class="header-actions">
          <button type="button" class="btn-secondary" (click)="toggleCart()" [disabled]="cartLines().length === 0">
            Voir panier ({{ cartTotalItems() }})
          </button>
          <button type="button" class="btn-primary" (click)="goToOrderFromCart()" [disabled]="cartLines().length === 0">
            Commander
          </button>
        </div>
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
                    <div class="title" [title]="product.itemName || '-'">{{ product.itemName || '-' }}</div>
                    <div class="meta">{{ product.itemCode || '-' }} - {{ product.groupName || '-' }}</div>
                    <div class="price">{{ product.price | number:'1.2-2' }} {{ currencyOf(product) }}</div>
                    <div class="stock" [class.low-stock]="product.stock < 10">Stock: {{ product.stock }}</div>
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

      @if (showCart() && cartLines().length > 0) {
        <div class="cart-overlay" (click)="closeCart()"></div>
        <aside class="cart-drawer" role="dialog" aria-modal="true" aria-label="Panier">
          <div class="cart-head">
            <h3>Panier ({{ cartTotalItems() }})</h3>
            <button type="button" class="btn-secondary" (click)="closeCart()">Fermer</button>
          </div>
          <div class="cart-body">
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
          </div>
          <div class="cart-footer">
            <button type="button" class="btn-secondary" (click)="clearCart()">Vider panier</button>
            <button type="button" class="btn-primary" (click)="goToOrderFromCart()">Commander</button>
          </div>
        </aside>
      }
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .header-actions { display: flex; gap: .45rem; }
    .subheader { display: flex; align-items: center; gap: .8rem; margin-bottom: .8rem; }
    .subheader h2 { margin: 0; font-size: 1.05rem; color: #1f2937; }
    .status { padding: 1rem; color: #374151; }
    .status.error { color: #b00020; }
    .btn-secondary { padding: 0.55rem 0.95rem; border-radius: 6px; border: 1px solid #d1d5db; background: white; cursor: pointer; font-weight: 500; }
    .group-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: .8rem; }
    .group-card { text-align: left; border: 1px solid #e5e7eb; background: #fff; border-radius: 10px; padding: .85rem; cursor: pointer; display: grid; gap: .25rem; }
    .group-card strong { color: #111827; font-size: .98rem; }
    .group-card span { color: #6b7280; font-size: .85rem; }
    .catalog-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(210px, 1fr)); gap: .6rem; }
    .product-card { background: #fff; border-radius: 10px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); overflow: hidden; border: 1px solid #eef2f7; display: grid; grid-template-columns: 74px 1fr; }
    .product-image-wrap { width: 74px; height: 74px; background: #f8fafc; display: flex; align-items: center; justify-content: center; }
    .product-image { width: 100%; height: 100%; object-fit: cover; }
    .product-image.placeholder { font-size: 0.8rem; color: #94a3b8; font-weight: 600; }
    .product-content { padding: 0.45rem .55rem; }
    .actions { display: flex; justify-content: flex-end; margin-top: .35rem; }
    .btn-primary { padding: 0.55rem 0.95rem; border-radius: 6px; border: 1px solid #1976d2; background: #1976d2; color: #fff; cursor: pointer; font-weight: 600; }
    .btn-primary:disabled { background: #93c5fd; border-color: #93c5fd; cursor: not-allowed; }
    .btn-sm { padding: 0.35rem 0.6rem; font-size: 0.78rem; }

    .mini-table { display: grid; gap: .15rem; }
    .mini-table .title { color: #111827; font-size: .84rem; font-weight: 700; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .mini-table .meta { color: #6b7280; font-size: .74rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .mini-table .price { color: #0f172a; font-size: .82rem; font-weight: 700; }
    .mini-table .stock { color: #334155; font-size: .74rem; }
    .low-stock { color: #dc2626; font-weight: 700; }

    .cart-overlay { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.35); z-index: 1200; }
    .cart-drawer { position: fixed; right: 0; top: 0; height: 100dvh; width: min(420px, 100vw); background: #fff; z-index: 1201; display: flex; flex-direction: column; border-left: 1px solid #e5e7eb; box-shadow: -8px 0 24px rgba(15, 23, 42, 0.2); }
    .cart-head { display: flex; justify-content: space-between; align-items: center; gap: .5rem; padding: .8rem; border-bottom: 1px solid #e5e7eb; }
    .cart-head h3 { margin: 0; font-size: 1rem; }
    .cart-body { flex: 1; overflow: auto; padding: .75rem; }
    .cart-row { display: grid; grid-template-columns: 1fr 80px auto; gap: .5rem; align-items: center; margin-bottom: .5rem; }
    .cart-main { display: grid; }
    .cart-main small { color: #6b7280; }
    .cart-row input { border: 1px solid #d1d5db; border-radius: 6px; padding: .35rem .45rem; }
    .btn-danger { padding: 0.45rem 0.65rem; border-radius: 6px; border: 1px solid #dc2626; background: #fff; color: #dc2626; cursor: pointer; font-weight: 600; }
    .cart-footer { display: flex; justify-content: flex-end; gap: .5rem; padding: .75rem; border-top: 1px solid #e5e7eb; background: #fff; }

    @media (max-width: 720px) {
      .group-grid, .catalog-grid { grid-template-columns: 1fr; }
      .product-card { grid-template-columns: 66px 1fr; }
      .product-image-wrap { width: 66px; height: 66px; }
      .header { align-items: flex-start; gap: .5rem; }
      .header-actions { width: 100%; justify-content: flex-end; }
      .btn-primary, .btn-secondary { padding: .45rem .7rem; font-size: .82rem; }
      .cart-row { grid-template-columns: 1fr 74px auto; }
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
  cartTotalItems = computed(() => this.cartLines().reduce((sum, l) => sum + l.quantity, 0));
  showCart = signal(false);
  selectedGroupCode = signal<number | null>(null);
  loadingGroups = signal(true);
  loadingProducts = signal(false);
  loading = signal(true);
  error = signal('');
  private brokenImageIds = signal<Set<number>>(new Set<number>());
  private readonly productsByGroup = new Map<number, Product[]>();

  constructor(private productApi: ProductApiService) {}

  ngOnInit(): void {
    this.cartLines.set(this.cart.getLines());
    this.loadGroups();
  }

  selectGroup(groupCode: number): void {
    this.selectedGroupCode.set(groupCode);
    const cached = this.productsByGroup.get(groupCode);
    if (cached) {
      this.visibleProducts.set(cached);
      return;
    }

    const filtered = this.products().filter((p) => Number(p.groupCode ?? 0) === groupCode);
    this.visibleProducts.set(filtered);

    if (filtered.length === 0) {
      this.loadSingleGroup(groupCode);
    }
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
    const lines = this.cart.getLines();
    this.cartLines.set(lines);
    if (lines.length === 0) this.showCart.set(false);
  }

  clearCart(): void {
    this.cart.clear();
    this.cartLines.set([]);
    this.showCart.set(false);
  }

  goToOrderFromCart(): void {
    if (this.cartLines().length === 0) return;
    this.showCart.set(false);
    this.router.navigate(['/orders/new'], { queryParams: { fromCatalog: '1' } });
  }

  toggleCart(): void {
    if (this.cartLines().length === 0) return;
    this.showCart.set(!this.showCart());
  }

  closeCart(): void {
    this.showCart.set(false);
  }

  private loadGroups(): void {
    this.loading.set(true);
    this.loadingGroups.set(true);
    this.error.set('');

    this.productApi.getGroups().subscribe({
      next: (groups) => {
        this.groups.set(groups);
        this.preloadProductsByGroupInBackground(groups);
        this.loadingGroups.set(false);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.error || 'Erreur chargement des groupes');
        this.loadingGroups.set(false);
        this.loading.set(false);
      }
    });
  }

  private preloadProductsByGroupInBackground(groups: ProductGroup[]): void {
    const validGroups = groups
      .filter((g) => Number.isFinite(g.groupCode))
      .sort((a, b) => a.groupCode - b.groupCode);
    if (validGroups.length === 0) return;

    this.loadingProducts.set(true);

    from(validGroups).pipe(
      concatMap((group) =>
        this.productApi.getByGroup(group.groupCode).pipe(
          map((items) => ({ groupCode: group.groupCode, items })),
          catchError(() => EMPTY),
          finalize(() => {
            const selected = this.selectedGroupCode();
            if (selected !== null && selected === group.groupCode) {
              const cached = this.productsByGroup.get(group.groupCode) ?? [];
              this.visibleProducts.set(cached);
            }
          })
        )
      ),
      finalize(() => this.loadingProducts.set(false))
    ).subscribe(({ groupCode, items }) => {
      const enriched = items.map((p) => ({
        ...p,
        imageUrl: p.imageUrl || this.catalogImageFor(p)
      }));
      this.productsByGroup.set(groupCode, enriched);
      this.preloadImagesInBackground(enriched);
      this.mergeProducts(enriched);
    });
  }

  private loadSingleGroup(groupCode: number): void {
    this.loadingProducts.set(true);
    this.productApi.getByGroup(groupCode).subscribe({
      next: (items) => {
        const enriched = items.map((p) => ({
          ...p,
          imageUrl: p.imageUrl || this.catalogImageFor(p)
        }));
        this.productsByGroup.set(groupCode, enriched);
        this.preloadImagesInBackground(enriched);
        this.mergeProducts(enriched);
        if (this.selectedGroupCode() === groupCode) {
          this.visibleProducts.set(enriched);
        }
      },
      error: (err) => {
        this.error.set(err?.error?.message || err?.error?.error || 'Erreur chargement des articles');
      },
      complete: () => this.loadingProducts.set(false)
    });
  }

  private mergeProducts(incoming: Product[]): void {
    const merged = new Map<string, Product>();
    for (const p of this.products()) {
      merged.set(String(p.itemCode ?? p.id), p);
    }
    for (const p of incoming) {
      merged.set(String(p.itemCode ?? p.id), p);
    }
    this.products.set(Array.from(merged.values()));
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

