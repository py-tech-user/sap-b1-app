import { CommonModule } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialApiService } from '../../core/services/commercial-api.service';
import { NotificationService } from '../../core/services/notification.service';
import { CustomerApiService } from '../../core/services/customer-api.service';
import { PartnerApiService } from '../../core/services/partner-api.service';
import { Product, ProductApiService } from '../../core/services/product-api.service';
import { AuthService } from '../../core/services/auth.service';
import { CatalogCartService } from '../../core/services/catalog-cart.service';
import { COMMERCIAL_META } from './commercial-meta';
import { CommercialDocument, CommercialDocumentLine, CommercialListFilters, CommercialResource, Customer } from '../../core/models/models';

const COMMERCIAL_REFRESH_EVENT = 'commercialDocuments:updated';
const INITIAL_CUSTOMERS_PAGE_SIZE = 250;
const INITIAL_PRODUCTS_PAGE_SIZE = 300;
const BACKGROUND_CUSTOMERS_PAGE_SIZE = 1000;
const BACKGROUND_PRODUCTS_PAGE_SIZE = 2000;

@Component({
  selector: 'app-document-form',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="page">
      <a [routerLink]="backRoute()" class="btn-sm">Retour</a>

      <h1>{{ pageTitle() }}</h1>

      <form [formGroup]="form" (ngSubmit)="save()" class="card" [class.generation-draft]="isGenerationDraft()" [class.edit-mode]="isEdit()" [class.compact-form]="isCompactForm()">
        <div class="top-grid">
          <div class="field field-client">
            <label>Client *</label>
            <input
              [value]="customerSearch()"
              (input)="onCustomerSearch($event)"
              (focus)="openCustomerSuggestions()"
              (keydown)="replaceCustomerSearchOnTyping($event)"
              (keydown.enter)="selectCustomerIfUnique($event)"
              placeholder="Rechercher et sélectionner client"
              [readonly]="isGenerationDraft()" />
            @if (openCustomerPanel() && filteredCustomers().length) {
              <div class="customer-suggestions">
                @for (c of filteredCustomers(); track c.cardCode) {
                  <button type="button" (mousedown)="selectCustomer(c)">
                    {{ customerDisplayName(c) }}
                  </button>
                }
              </div>
            }
          </div>

          <div class="field">
            <label>Date document *</label>
            <input type="date" formControlName="docDate" />
          </div>
          @if (showDeliveryDate()) {
            <div class="field">
              <label>Date livraison *</label>
              <input type="date" formControlName="dueDate" />
            </div>
          }
          <div class="field field-comments">
            <label>Commentaires</label>
            <textarea rows="2" formControlName="comments"></textarea>
          </div>
          <div class="field field-payment">
            <label>Mode de paiement *</label>
            <input formControlName="paymentMethod" placeholder="Ex: Virement" />
          </div>
        </div>

        @if (isEdit()) {
          <div class="draft-action-bar">
            <div class="draft-totals" aria-label="Totaux du document">
              <span>HT <strong>{{ totalHt() | number:'1.2-2' }}</strong></span>
              <span>TVA <strong>{{ totalVat() | number:'1.2-2' }}</strong></span>
              <span>TTC <strong>{{ totalTtc() | number:'1.2-2' }}</strong></span>
            </div>
            <button class="btn-primary" [disabled]="saving() || shouldBlockSubmitForLookups() || !canModify()" type="submit">
              {{ submitButtonLabel() }}
            </button>
          </div>
        }

        <div class="lines-head">
          <h3>Lignes</h3>
          <button class="btn-outline" type="button" (click)="addLine()" [disabled]="!canAddLines()">+ Ajouter ligne</button>
        </div>

        @if (isGenerationDraft()) {
          <p class="lines-hint">
            Ajustez librement les quantités. Pour retirer une ligne, utilisez Suppr. Il faut au moins une ligne pour valider la création.
          </p>
        }

        

        <div class="lines-scroll">
          <div class="line-row line-row-header" aria-hidden="true">
            <span>Code *</span>
            <span>Designation *</span>
            <span>Quantite</span>
            <span>Warehouse code *</span>
            <span>Prix HT</span>
            <span>Code TVA</span>
            <span>Montant TVA</span>
            <span>Remise %</span>
            <span>Total</span>
            <span>Statut</span>
            <span>Action</span>
          </div>

          <div formArrayName="lines">
            @for (line of lines.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="line-row">
                <span class="mobile-label">Code</span>
                <div class="product-lookup-cell">
                  <input
                    formControlName="itemCode"
                    placeholder="Rechercher code article"
                    aria-label="ItemCode"
                    (input)="onItemCodeInput(i, $event)"
                    (focus)="openProductSuggestions(i, 'code')"
                    (keydown)="replaceProductSearchOnTyping(i, $event)"
                    (keydown.enter)="selectProductIfUnique(i, $event)"
                    (blur)="onItemCodeBlur(i)"
                    [readonly]="!canEditItemFields(i)" />
                  @if (isProductPanelOpen(i, 'code')) {
                    <div class="product-suggestions">
                      @for (p of filteredProductsForLine(i); track p.itemCode) {
                        <button type="button" (mousedown)="selectProduct(i, p)">
                          <strong>{{ p.itemCode }}</strong>
                          <small>{{ productLookupLabel(p) }} - {{ productPriceLabel(p) }}</small>
                        </button>
                      } @empty {
                        <div class="product-suggestion-empty">{{ productSuggestionEmptyLabel() }}</div>
                      }
                    </div>
                  }
                </div>

                <span class="mobile-label">Designation</span>
                <div class="product-lookup-cell">
                  <input
                    formControlName="productLookup"
                    placeholder="Rechercher et selectionner article"
                    (input)="onProductLookupInput(i, $event)"
                    (focus)="openProductSuggestions(i, 'name')"
                    (keydown)="replaceProductSearchOnTyping(i, $event)"
                    (keydown.enter)="selectProductIfUnique(i, $event)"
                    (blur)="onProductLookupBlur(i)"
                    [readonly]="!canEditItemFields(i)" />
                  @if (isProductPanelOpen(i, 'name')) {
                    <div class="product-suggestions">
                      @for (p of filteredProductsForLine(i); track p.itemCode) {
                        <button type="button" (mousedown)="selectProduct(i, p)">
                          <strong>{{ productLookupLabel(p) }}</strong>
                          <small>{{ p.itemCode }} - {{ productPriceLabel(p) }}</small>
                        </button>
                      } @empty {
                        <div class="product-suggestion-empty">{{ productSuggestionEmptyLabel() }}</div>
                      }
                    </div>
                  }
                </div>

                <span class="mobile-label">Quantite</span>
                <input type="number" formControlName="quantity" min="1" step="1" placeholder="Quantité" aria-label="Quantite" (input)="onQuantityInput(i)" (blur)="onQuantityBlur(i)" [readonly]="!canEditQuantity(i)" />
                <span class="mobile-label">Warehouse code</span>
                <input formControlName="warehouseCode" placeholder="Ex: 01" aria-label="WarehouseCode" [readonly]="!canEditItemFields(i)" />
                <span class="mobile-label">Prix HT</span>
                <input type="number" formControlName="unitPrice" min="0" step="0.01" placeholder="Prix HT" aria-label="Prix unitaire HT" (input)="recalculateLine(i)" [readonly]="!canEditItemFields(i)" />
                <span class="mobile-label">Code TVA</span>
                <input type="number" formControlName="vatPct" min="0" step="0.01" placeholder="Code TVA" aria-label="Code TVA" (input)="recalculateLine(i)" [readonly]="!canEditItemFields(i)" />
                <span class="mobile-label">Montant TVA</span>
                <input type="number" formControlName="vatAmount" placeholder="Montant TVA" aria-label="Montant TVA" readonly />
                <span class="mobile-label">Remise %</span>
                <input type="number" formControlName="discountPct" min="0" max="100" step="0.01" placeholder="Remise %" aria-label="Remise" (input)="recalculateLine(i)" [readonly]="!canEditItemFields(i)" />
                <span class="mobile-label">Total</span>
                <input type="number" formControlName="totalTtc" placeholder="Total TTC" aria-label="Total TTC" readonly />
                <span class="mobile-label">Statut</span>
                <input formControlName="lineStatus" placeholder="Statut" aria-label="Statut ligne" readonly />
                <span class="mobile-label">Action</span>
                <button type="button" class="btn-outline danger" (click)="removeLine(i)" [disabled]="!canRemoveLine(i)">Suppr.</button>
              </div>
            } @empty {
              <p class="empty">Aucune ligne.</p>
            }
          </div>
        </div>

        <div class="totals-row" aria-label="Totaux du document">
          <div class="total-box">
            <span class="total-label">TOTAL HT</span>
            <strong class="total-value">{{ totalHt() | number:'1.2-2' }}</strong>
          </div>
          <div class="total-box">
            <span class="total-label">TVA TOTAL</span>
            <strong class="total-value">{{ totalVat() | number:'1.2-2' }}</strong>
          </div>
          <div class="total-box">
            <span class="total-label">TOTAL TTC</span>
            <strong class="total-value">{{ totalTtc() | number:'1.2-2' }}</strong>
          </div>
        </div>

        <div class="actions">
          <button class="btn-primary" [disabled]="saving() || shouldBlockSubmitForLookups() || !canModify()" type="submit">
            {{ submitButtonLabel() }}
          </button>
          @if (error()) {
            <span class="action-feedback error">{{ error() }}</span>
          }
          @if (success()) {
            <span class="action-feedback success">{{ success() }}</span>
          }
        </div>

        @if (isEdit() && !canModify()) {
          <div class="error">Modification autorisee uniquement pour un document en statut Open.</div>
        }
      </form>

    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 0.65rem; }
    .page h1 { margin: 0; font-size: 1.15rem; line-height: 1.2; }
    .card { background: #fff; border-radius: 8px; padding: 0.65rem; box-shadow: 0 1px 3px rgba(0,0,0,0.08); display: flex; flex-direction: column; gap: 0.6rem; }
    .top-grid { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 0.45rem; }
    .field { display: flex; flex-direction: column; gap: 0.25rem; }
    .field-client { grid-column: span 3; position: relative; }
    .field-comments { grid-column: 1 / span 2; }
    .field-payment { grid-column: 3 / span 1; }
    .field-wide { grid-column: 1 / -1; }
    .field input, .field textarea, .field select { border: 1px solid #d7d7d7; border-radius: 6px; padding: 0.45rem 0.6rem; }
    .field-client input { border-color: #c9d7e8; border-radius: 10px; background: #fff; box-shadow: inset 0 1px 0 rgba(15,23,42,.02); }
    .field-client input:focus { outline: 2px solid rgba(37,99,235,.16); border-color: #60a5fa; }
    .customer-suggestions { position: absolute; z-index: 20; top: calc(100% + 4px); left: 0; right: 0; display: grid; gap: .15rem; max-height: 280px; overflow: auto; background: #fff; border: 1px solid #cbd5e1; border-radius: 12px; box-shadow: 0 18px 38px rgba(15,23,42,.16); padding: .35rem; }
    .customer-suggestions button { width: 100%; border: 0; background: #fff; text-align: left; padding: .58rem .7rem; border-radius: 8px; cursor: pointer; font-weight: 700; color: #111827; white-space: normal; line-height: 1.25; }
    .customer-suggestions button:hover { background: #eff6ff; color: #1d4ed8; }
    .lines-head { display: flex; justify-content: space-between; align-items: center; }
    .lines-hint { margin: 0; color: #555; font-size: 0.86rem; }
    .lines-scroll { overflow-x: auto; padding-bottom: 0.2rem; }
    .line-row { display: grid; min-width: 1380px; grid-template-columns: 130px 220px 85px 100px 100px 85px 105px 90px 110px 100px 84px; gap: 0.4rem; margin-bottom: 0.4rem; align-items: start; }
    .line-row-header { margin-bottom: 0.25rem; color: #666; font-size: 0.78rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.02em; }
    .line-row-header span { padding: 0.1rem 0.2rem; }
    .mobile-label { display: none; }
    .line-row input, .line-row select { width: 100%; border: 1px solid #d7d7d7; border-radius: 6px; padding: 0.38rem 0.5rem; box-sizing: border-box; font-size: 0.9rem; }
    .product-lookup-cell { position: relative; min-width: 0; }
    .product-lookup-cell input { border-color: #c9d7e8; border-radius: 10px; background: #fff; box-shadow: inset 0 1px 0 rgba(15,23,42,.02); }
    .product-lookup-cell input:focus { outline: 2px solid rgba(37,99,235,.16); border-color: #60a5fa; }
    .product-suggestions { position: static; z-index: 30; margin-top: 4px; min-width: 340px; display: grid; gap: .15rem; max-height: 280px; overflow: auto; background: #fff; border: 1px solid #cbd5e1; border-radius: 12px; box-shadow: 0 18px 38px rgba(15,23,42,.16); padding: .35rem; }
    .product-suggestions button { width: 100%; border: 0; background: #fff; text-align: left; padding: .58rem .7rem; border-radius: 8px; cursor: pointer; color: #111827; display: grid; gap: .16rem; }
    .product-suggestions button:hover { background: #eff6ff; color: #1d4ed8; }
    .product-suggestions strong { font-size: .9rem; line-height: 1.2; }
    .product-suggestions small { color: #64748b; font-weight: 700; line-height: 1.2; }
    .product-suggestion-empty { padding: .58rem .7rem; color: #64748b; font-weight: 700; font-size: .86rem; }
    .totals-row { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.75rem; }
    .total-box { border: 1px solid #d7d7d7; border-radius: 8px; padding: 0.65rem 0.8rem; background: #fafafa; display: flex; flex-direction: column; gap: 0.2rem; }
    .total-label { font-size: 0.78rem; color: #666; letter-spacing: 0.02em; }
    .total-value { font-size: 1.05rem; color: #111827; }
    .draft-action-bar { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; border: 1px solid #d7d7d7; border-radius: 8px; background: #f8fafc; padding: 0.45rem 0.55rem; }
    .draft-totals { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; color: #4b5563; font-size: 0.86rem; }
    .draft-totals strong { color: #111827; margin-left: 0.2rem; }
    .compact-form { gap: 0.45rem; }
    .compact-form.card { padding: 0.5rem; }
    .compact-form .top-grid { gap: 0.35rem; }
    .compact-form .field input,
    .compact-form .field textarea,
    .compact-form .field select { padding: 0.34rem 0.48rem; }
    .compact-form .field-comments textarea { min-height: 34px; }
    .compact-form .lines-head h3 { margin: 0; font-size: 1rem; }
    .compact-form .line-row { min-width: 1180px; grid-template-columns: 110px 185px 72px 86px 86px 72px 92px 76px 96px 82px 70px; gap: 0.3rem; margin-bottom: 0.25rem; }
    .compact-form .line-row input,
    .compact-form .line-row select { padding: 0.28rem 0.38rem; font-size: 0.84rem; }
    .compact-form .totals-row { gap: 0.45rem; }
    .compact-form .total-box { padding: 0.45rem 0.6rem; }
    .generation-draft .lines-hint { display: none; }
    .generation-draft .actions {
      position: sticky;
      bottom: 0;
      z-index: 5;
      padding: 0.45rem 0 0;
      background: linear-gradient(to bottom, rgba(255,255,255,0.88), #fff 35%);
      border-top: 1px solid #e5e7eb;
    }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .btn-outline.danger { border-color: #c62828; color: #c62828; }
    .actions { display: flex; justify-content: flex-end; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
    .action-feedback { font-weight: 700; font-size: 0.9rem; }
    .error { color: #b00020; }
    .success { color: #1b5e20; }
    .empty { color: #888; }
    @media (max-width: 1200px) {
      .top-grid { grid-template-columns: 1fr 1fr; }
      .field-client, .field-comments { grid-column: 1 / -1; }
      .totals-row { grid-template-columns: 1fr; }
    }
    @media (max-width: 900px) {
      .top-grid { grid-template-columns: 1fr; }
      .lines-scroll { overflow-x: hidden; }
      .line-row-header { display: none; }
      .mobile-label {
        display: block;
        font-size: 0.72rem;
        color: #6b7280;
        text-transform: uppercase;
        letter-spacing: 0.02em;
        margin-top: 0.15rem;
      }
      .line-row {
        min-width: 0;
        grid-template-columns: 1fr 1fr;
        gap: 0.45rem;
        border: 1px solid #e5e7eb;
        border-radius: 8px;
        padding: 0.55rem;
      }
      .product-suggestions { min-width: min(340px, 88vw); }
      .line-row .btn-outline.danger { grid-column: 1 / -1; }
      .draft-action-bar { align-items: stretch; flex-direction: column; }
      .draft-action-bar .btn-primary { width: 100%; }
      .actions { justify-content: stretch; }
      .actions .btn-primary { width: 100%; }
    }
  `]
})
export class DocumentFormComponent implements OnInit {
  private readonly api = inject(CommercialApiService);
  private readonly notifications = inject(NotificationService);
  private readonly customerApi = inject(CustomerApiService);
  private readonly partnerApi = inject(PartnerApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly auth = inject(AuthService);
  private readonly catalogCart = inject(CatalogCartService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  readonly resource = signal<CommercialResource>(this.resolveResource());
  readonly id = signal<number | null>(this.resolveDocumentId());
  readonly returnTo = signal(this.route.snapshot.queryParamMap.get('returnTo') ?? '');
  readonly isEdit = computed(() => this.id() !== null);
  readonly showDeliveryDate = computed(() => this.resource() !== 'quotes' || this.isEdit());
  readonly meta = computed(() => COMMERCIAL_META[this.resource()]);
  readonly sourceResource = signal<CommercialResource | null>(this.resolveSourceResource());
  readonly sourceDocumentId = signal<number | null>(this.resolveSourceDocumentId());
  readonly sourceLineNums = signal<number[]>(this.resolveSourceLineNums());
  readonly isGenerationDraft = computed(() => !this.isEdit() && !!this.sourceResource() && !!this.sourceDocumentId());
  readonly isCompactForm = computed(() => this.isEdit() || this.isGenerationDraft());
  readonly pageTitle = computed(() => {
    if (this.isGenerationDraft()) {
      return `${this.meta().icon} Préparer ${this.meta().singular}`;
    }
    return `${this.meta().icon} ${this.isEdit() ? 'Éditer' : 'Créer'} ${this.meta().singular}`;
  });
  readonly submitButtonLabel = computed(() => {
    if (this.saving()) {
      if (this.isEdit()) return 'Mise à jour...';
      if (this.isGenerationDraft()) return `Validation de la création du ${this.entityLabel()}...`;
      return 'Création...';
    }

    if (this.isEdit()) return 'Mettre à jour';
    if (this.isGenerationDraft()) return `Valider la création du ${this.entityLabel()}`;
    return 'Créer';
  });
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly loadingLookups = signal(true);
  readonly loadedDocStatus = signal<'Open' | 'Closed'>('Open');
  readonly supportsLineStatusGuard = computed(() => true);
  readonly canModify = computed(() => !this.isEdit() || (this.supportsLineStatusGuard() && this.loadedDocStatus() === 'Open'));

  readonly customers = signal<Customer[]>([]);
  readonly products = signal<Product[]>([]);
  readonly customerSearch = signal('');
  readonly openCustomerPanel = signal(false);
  readonly openProductPanelIndex = signal<number | null>(null);
  readonly openProductPanelField = signal<'code' | 'name'>('name');
  private customerReplaceOnType = false;
  private productReplaceOnTypeIndex: number | null = null;
  private loadedCustomerId: number | null = null;

  readonly filteredCustomers = computed(() => {
    const q = this.normalizeSearch(this.customerSearch());
    if (!q) return this.customers();
    return this.customers().filter(c =>
      this.normalizeSearch(this.customerDisplayName(c)).includes(q) ||
      this.normalizeSearch(c.cardCode).includes(q)
    );
  });

  readonly form = this.fb.group({
    cardCode: ['', [Validators.required]],
    docDate: [new Date().toISOString().slice(0, 10), [Validators.required]],
    dueDate: [new Date().toISOString().slice(0, 10), [Validators.required]],
    comments: [''],
    paymentMethod: ['Virement', [Validators.required]],
    lines: this.fb.array([])
  });

  get lines(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  constructor() {}

  ngOnInit(): void {
    if (this.isEdit()) {
      this.loadingLookups.set(false);
      this.load();
      return;
    }

    this.loadLookups();
    if (this.isGenerationDraft()) {
      this.loadGenerationDraft();
    }
    else {
      this.hydrateFromCatalogCartIfNeeded();
      if (this.lines.length === 0) this.addLine();
    }
  }

  @HostListener('document:click', ['$event'])
  closeCustomerSuggestions(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.field-client')) {
      this.openCustomerPanel.set(false);
      this.customerReplaceOnType = false;
    }
    if (!target?.closest('.product-lookup-cell')) {
      this.openProductPanelIndex.set(null);
      this.productReplaceOnTypeIndex = null;
    }
  }

  onCustomerSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    const previousDisplay = this.customerSearch();
    const hadSelection = !!String(this.form.get('cardCode')?.value ?? '').trim();
    const nextValue = this.nextSearchValueAfterTyping(input.value || '', previousDisplay, hadSelection);
    this.customerReplaceOnType = false;
    this.customerSearch.set(nextValue);
    this.form.patchValue({ cardCode: '' }, { emitEvent: false });
    this.openCustomerPanel.set(true);
  }

  openCustomerSuggestions(): void {
    this.customerReplaceOnType = !!String(this.form.get('cardCode')?.value ?? '').trim();
    this.openCustomerPanel.set(true);
  }

  replaceCustomerSearchOnTyping(event: KeyboardEvent): void {
    if (!this.customerReplaceOnType || event.ctrlKey || event.metaKey || event.altKey) return;

    if (event.key === 'Backspace' || event.key === 'Delete') {
      event.preventDefault();
      this.customerSearch.set('');
      this.form.patchValue({ cardCode: '' }, { emitEvent: false });
      this.openCustomerPanel.set(true);
      this.customerReplaceOnType = false;
      return;
    }

    if (event.key.length !== 1) return;
    event.preventDefault();
    this.customerSearch.set(event.key);
    this.form.patchValue({ cardCode: '' }, { emitEvent: false });
    this.openCustomerPanel.set(true);
    this.customerReplaceOnType = false;
  }

  private nextSearchValueAfterTyping(value: string, previousDisplay: string, hadSelection: boolean): string {
    const nextValue = String(value ?? '');
    const previousValue = String(previousDisplay ?? '');
    if (!hadSelection || !previousValue || nextValue === previousValue) return nextValue;

    if (nextValue.startsWith(previousValue)) {
      return nextValue.slice(previousValue.length).trimStart();
    }

    if (nextValue.endsWith(previousValue)) {
      return nextValue.slice(0, nextValue.length - previousValue.length).trimEnd();
    }

    const previousIndex = nextValue.indexOf(previousValue);
    if (previousIndex >= 0) {
      return `${nextValue.slice(0, previousIndex)}${nextValue.slice(previousIndex + previousValue.length)}`.trim();
    }

    return nextValue;
  }

  selectCustomer(customer: Customer): void {
    this.form.patchValue({ cardCode: String(customer.cardCode ?? '').trim() }, { emitEvent: false });
    this.customerSearch.set(this.customerDisplayName(customer));
    this.openCustomerPanel.set(false);
    this.customerReplaceOnType = false;
  }

  selectCustomerIfUnique(event: Event): void {
    const matches = this.filteredCustomers();
    if (matches.length === 1) {
      event.preventDefault();
      this.selectCustomer(matches[0]);
    }
  }

  addLine(line?: Partial<CommercialDocumentLine>): void {
    const statusToken = this.normalizeLineStatusToken((line as any)?.lineStatus ?? (line as any)?.LineStatus ?? 'Open');
    const lineStatus = this.isClosedLineStatus(statusToken)
      ? 'Cloturee'
      : 'En attente';
    const quantity = Math.max(1, Number(line?.quantity ?? 1));
    const maxQuantity = Math.max(1, Number((line as any)?.maxQuantity ?? quantity));

    const group = this.fb.group({
      lineNum: [line?.lineNum ?? line?.id ?? null],
      productId: [line?.productId ?? null],
      productLookup: [line?.itemName ? String(line.itemName) : (line?.itemCode ? String(line.itemCode) : '')],
      itemCode: [line?.itemCode || '', [Validators.required]],
      lineStatus: [lineStatus],
      unitPrice: [line?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      quantity: [quantity, [Validators.required, Validators.min(1)]],
      maxQuantity: [maxQuantity],
      subtotalHt: [line?.subtotalHt ?? 0],
      discountPct: [line?.discountPct ?? 0, [Validators.min(0), Validators.max(100)]],
      vatPct: [line?.vatPct ?? 20, [Validators.min(0)]],
      vatAmount: [line?.vatAmount ?? 0],
      totalTtc: [line?.totalTtc ?? 0],
      warehouseCode: [line?.warehouseCode || '', [Validators.required]],
      baseType: [line?.baseType ?? null],
      baseEntry: [line?.baseEntry ?? null],
      baseLine: [line?.baseLine ?? null],
    });

    if (this.isEdit() && this.supportsLineStatusGuard() && this.isClosedLineStatus(statusToken)) {
      group.get('productLookup')?.disable({ emitEvent: false });
      group.get('unitPrice')?.disable({ emitEvent: false });
      group.get('quantity')?.disable({ emitEvent: false });
      group.get('discountPct')?.disable({ emitEvent: false });
      group.get('vatPct')?.disable({ emitEvent: false });
      group.get('warehouseCode')?.disable({ emitEvent: false });
    }

    if (this.isGenerationDraft()) {
      group.get('productLookup')?.disable({ emitEvent: false });
      group.get('itemCode')?.disable({ emitEvent: false });
      group.get('unitPrice')?.disable({ emitEvent: false });
      group.get('discountPct')?.disable({ emitEvent: false });
      group.get('vatPct')?.disable({ emitEvent: false });
      group.get('warehouseCode')?.disable({ emitEvent: false });
    }

    this.lines.push(group);
    this.recalculateLine(this.lines.length - 1);
  }

  productLookupLabel(product: Product): string {
    return String(product.itemName || product.itemCode || '').trim();
  }

  productPriceLabel(product: Product): string {
    const price = Number((product as any).price ?? 0);
    if (!Number.isFinite(price) || price <= 0) return 'Prix non renseigne';
    return `${price.toFixed(2)} HT`;
  }

  productSuggestionEmptyLabel(): string {
    return this.products().length === 0
      ? 'Chargement des articles...'
      : 'Aucun article trouve';
  }

  filteredProductsForLine(index: number): Product[] {
    const group = this.lines.at(index);
    const activeField = this.openProductPanelField();
    const queryValue = activeField === 'code'
      ? group?.get('itemCode')?.value
      : group?.get('productLookup')?.value;
    const query = this.normalizeSearch(queryValue ?? '');
    const products = this.products();

    if (!query) return products.slice(0, 80);

    return products
      .filter((product) => {
        const code = this.normalizeSearch(product.itemCode);
        const name = this.normalizeSearch(product.itemName);
        return code.includes(query) || name.includes(query);
      })
      .slice(0, 80);
  }

  isProductPanelOpen(index: number, field: 'code' | 'name'): boolean {
    return this.openProductPanelIndex() === index && this.openProductPanelField() === field;
  }

  openProductSuggestions(index: number, field: 'code' | 'name'): void {
    if (!this.canEditItemFields(index)) return;
    const group = this.lines.at(index);
    this.productReplaceOnTypeIndex = group?.get('productId')?.value ? index : null;
    this.openProductPanelField.set(field);
    this.openProductPanelIndex.set(index);
  }

  replaceProductSearchOnTyping(index: number, event: KeyboardEvent): void {
    if (this.productReplaceOnTypeIndex !== index || event.ctrlKey || event.metaKey || event.altKey) return;

    const group = this.lines.at(index);
    if (!group) return;

    if (event.key === 'Backspace' || event.key === 'Delete') {
      event.preventDefault();
      group.patchValue({ productId: null, itemCode: '', productLookup: '' }, { emitEvent: false });
      this.openProductPanelIndex.set(index);
      this.productReplaceOnTypeIndex = null;
      return;
    }

    if (event.key.length !== 1) return;
    event.preventDefault();
    if (this.openProductPanelField() === 'code') {
      group.patchValue({ productId: null, itemCode: event.key, productLookup: '' }, { emitEvent: false });
    } else {
      group.patchValue({ productId: null, itemCode: '', productLookup: event.key }, { emitEvent: false });
    }
    this.openProductPanelIndex.set(index);
    this.productReplaceOnTypeIndex = null;
  }

  selectProductIfUnique(index: number, event: Event): void {
    const matches = this.filteredProductsForLine(index);
    if (matches.length === 1) {
      event.preventDefault();
      this.selectProduct(index, matches[0]);
    }
  }

  selectProduct(index: number, product: Product): void {
    const group = this.lines.at(index);
    if (!group) return;

    group.patchValue({
      productId: Number(product.id ?? 0),
      itemCode: String(product.itemCode ?? '').trim(),
      productLookup: this.productLookupLabel(product)
    }, { emitEvent: false });

    this.openProductPanelIndex.set(null);
    this.productReplaceOnTypeIndex = null;
    this.onProductSelected(index);
  }

  onProductLookupInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const group = this.lines.at(index);
    if (!group) return;

    const previousDisplay = String(group.get('productLookup')?.value ?? '');
    const hadSelection = !!Number(group.get('productId')?.value ?? 0);
    const value = this.nextSearchValueAfterTyping(input.value || '', previousDisplay, hadSelection).trim();

    group.patchValue({
      productId: null,
      itemCode: '',
      productLookup: value
    }, { emitEvent: false });
    this.openProductPanelField.set('name');
    this.openProductPanelIndex.set(index);
    this.productReplaceOnTypeIndex = null;

    if (!value) return;

    const product = this.findProductByCodeOrName(value);

    if (!product) return;

    this.selectProduct(index, product);
  }

  onProductLookupBlur(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    const value = String(group.get('productLookup')?.value ?? '').trim();
    if (!value) {
      this.restoreSelectedProductLabel(index);
      this.openProductPanelIndex.set(null);
      this.productReplaceOnTypeIndex = null;
      return;
    }

    const product = this.findProductByCodeOrName(value);
    if (!product) {
      this.restoreSelectedProductLabel(index);
      this.openProductPanelIndex.set(null);
      this.productReplaceOnTypeIndex = null;
      return;
    }

    group.patchValue({
      productId: Number(product.id ?? 0),
      itemCode: String(product.itemCode ?? '').trim(),
      productLookup: this.productLookupLabel(product)
    }, { emitEvent: false });
    this.openProductPanelIndex.set(null);
    this.productReplaceOnTypeIndex = null;
    this.onProductSelected(index);
  }

  onItemCodeInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = String(input.value ?? '').trim();
    const group = this.lines.at(index);
    if (!group) return;

    group.patchValue({
      productId: null,
      itemCode: value,
      productLookup: ''
    }, { emitEvent: false });
    this.openProductPanelField.set('code');
    this.openProductPanelIndex.set(index);
    this.productReplaceOnTypeIndex = null;

    if (!value) return;

    const normalized = value.toLowerCase();
    const product = this.products().find((p) =>
      String(p.itemCode ?? '').trim().toLowerCase() === normalized
    );
    if (!product) return;

    this.selectProduct(index, product);
  }

  onItemCodeBlur(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    const value = String(group.get('itemCode')?.value ?? '').trim();
    if (!value) {
      this.restoreSelectedProductCode(index);
      this.openProductPanelIndex.set(null);
      this.productReplaceOnTypeIndex = null;
      return;
    }

    const product = this.products().find((p) =>
      String(p.itemCode ?? '').trim().toLowerCase() === value.toLowerCase()
    );

    if (!product) {
      this.restoreSelectedProductCode(index);
      this.openProductPanelIndex.set(null);
      this.productReplaceOnTypeIndex = null;
      return;
    }

    group.patchValue({
      productId: Number(product.id ?? 0),
      itemCode: String(product.itemCode ?? '').trim(),
      productLookup: this.productLookupLabel(product)
    }, { emitEvent: false });
    this.openProductPanelIndex.set(null);
    this.productReplaceOnTypeIndex = null;
    this.onProductSelected(index);
  }

  onProductSelected(index: number): void {
    const group = this.lines.at(index);
    const productId = Number(group.get('productId')?.value ?? 0);
    const product = this.products().find((p: any) => Number(p.id) === productId);
    if (!product) return;

    const productAny = product as any;
    const warehouseCode = String(
      productAny.warehouseCode
      ?? productAny.WarehouseCode
      ?? productAny.whsCode
      ?? productAny.WhsCode
      ?? '01'
    ).trim();

    group.patchValue({
      itemCode: String(product.itemCode ?? '').trim(),
      warehouseCode: warehouseCode || '01',
      unitPrice: Number((product as any).price ?? 0)
    });
    this.recalculateLine(index);
  }

  private findProductByCodeOrName(value: string): Product | undefined {
    const normalized = String(value ?? '').trim().toLowerCase();
    if (!normalized) return undefined;

    return this.products().find((p) => {
      const code = String(p.itemCode ?? '').trim().toLowerCase();
      const name = String(p.itemName ?? '').trim().toLowerCase();
      return normalized === code || normalized === name;
    });
  }

  private restoreSelectedProductLabel(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    const productId = Number(group.get('productId')?.value ?? 0);
    const product = this.products().find((p) => Number(p.id ?? 0) === productId);
    group.patchValue({
      productLookup: product ? this.productLookupLabel(product) : ''
    }, { emitEvent: false });
  }

  private restoreSelectedProductCode(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    const productId = Number(group.get('productId')?.value ?? 0);
    const product = this.products().find((p) => Number(p.id ?? 0) === productId);
    group.patchValue({
      itemCode: product ? String(product.itemCode ?? '').trim() : ''
    }, { emitEvent: false });
  }

  recalculateLine(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    const quantity = Math.max(0, Number(group.get('quantity')?.value ?? 0));
    const unitPrice = Math.max(0, Number(group.get('unitPrice')?.value ?? 0));
    const discountPct = Math.min(100, Math.max(0, Number(group.get('discountPct')?.value ?? 0)));
    const vatPct = Math.max(0, Number(group.get('vatPct')?.value ?? 0));

    const grossHt = quantity * unitPrice;
    const discountAmount = grossHt * (discountPct / 100);
    const subtotalHt = grossHt - discountAmount;
    const vatAmount = subtotalHt * (vatPct / 100);
    const totalTtc = subtotalHt + vatAmount;

    group.patchValue({
      subtotalHt: Number(subtotalHt.toFixed(2)),
      vatAmount: Number(vatAmount.toFixed(2)),
      totalTtc: Number(totalTtc.toFixed(2))
    }, { emitEvent: false });
  }

  onQuantityInput(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    this.recalculateLine(index);
  }

  onQuantityBlur(index: number): void {
    const group = this.lines.at(index);
    if (!group) return;

    const rawValue = group.get('quantity')?.value;
    const quantity = Number(rawValue ?? 0);

    if (!Number.isFinite(quantity) || quantity <= 0) {
      group.patchValue({ quantity: 1 }, { emitEvent: false });
      if (this.isGenerationDraft()) {
        this.error.set('Une ligne ne peut pas avoir une quantité à 0. Supprimez la ligne si besoin.');
      }
    } else if (this.shouldCapGeneratedQuantity()) {
      const maxQuantity = Number(group.get('maxQuantity')?.value ?? 0);
      if (Number.isFinite(maxQuantity) && maxQuantity > 0 && quantity > maxQuantity) {
        group.patchValue({ quantity: maxQuantity }, { emitEvent: false });
        this.error.set('La quantité du document cible ne peut pas dépasser la quantité du document source.');
      }
    }

    this.recalculateLine(index);
  }

  removeLine(i: number): void {
    if (!this.canRemoveLine(i)) {
      this.error.set('Ligne fermee: suppression impossible.');
      return;
    }
    this.lines.removeAt(i);
  }

  canAddLines(): boolean {
    return this.canModify() && !this.isGenerationDraft();
  }

  canEditLine(index: number): boolean {
    if (!this.canModify()) return false;
    if (!this.isEdit()) return true;
    if (!this.supportsLineStatusGuard()) return true;

    const group = this.lines.at(index);
    const statusToken = this.normalizeLineStatusToken(group?.get('lineStatus')?.value ?? 'En attente');
    return !this.isClosedLineStatus(statusToken);
  }

  canEditItemFields(index: number): boolean {
    return this.canEditLine(index) && !this.isGenerationDraft();
  }

  canEditQuantity(index: number): boolean {
    return this.canEditLine(index);
  }

  private shouldCapGeneratedQuantity(): boolean {
    if (!this.isGenerationDraft()) return false;
    return this.resource() === 'deliverynotes' || this.resource() === 'invoices';
  }

  canRemoveLine(index: number): boolean {
    return this.canEditLine(index);
  }

  shouldBlockSubmitForLookups(): boolean {
    return !this.isEdit() && this.loadingLookups();
  }

  totalHt(): number {
    return this.sumLineField('subtotalHt');
  }

  totalVat(): number {
    return this.sumLineField('vatAmount');
  }

  totalTtc(): number {
    return this.sumLineField('totalTtc');
  }

  save(): void {
    if (this.saving()) {
      return;
    }

    const isEditMode = this.isEdit();

    if (isEditMode && !this.canModify()) {
      this.error.set('Modification autorisee uniquement pour un document en statut Open.');
      this.notifications.showError(this.error());
      return;
    }

    if (this.form.invalid || this.lines.length === 0) {
      this.error.set(this.buildMissingFieldsMessage());
      this.notifications.showError(this.error());
      return;
    }

    const hasInvalidLine = this.lines.controls.some(c => {
      const itemCode = String(c.get('itemCode')?.value ?? '').trim();
      const quantity = Number(c.get('quantity')?.value ?? 0);
      const unitPrice = Number(c.get('unitPrice')?.value ?? 0);
      const discountPct = Number(c.get('discountPct')?.value ?? 0);
      const vatPct = Number(c.get('vatPct')?.value ?? 0);
      const warehouseCode = String(c.get('warehouseCode')?.value ?? '').trim();
      return itemCode === ''
        || warehouseCode === ''
        || !Number.isFinite(quantity)
        || quantity <= 0
        || !Number.isFinite(unitPrice)
        || unitPrice < 0
        || !Number.isFinite(discountPct)
        || discountPct < 0
        || discountPct > 100
        || !Number.isFinite(vatPct)
        || vatPct < 0;
    });
    if (hasInvalidLine) {
      this.error.set('Chaque ligne doit contenir ItemCode, WarehouseCode et Quantity > 0.');
      this.notifications.showError(this.error());
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.success.set('');

    const raw = this.form.getRawValue();
    const payload = {
      cardCode: String(raw.cardCode ?? '').trim(),
      docDate: raw.docDate || undefined,
      dueDate: raw.dueDate || undefined,
      comments: raw.comments || undefined,
      paymentMethod: raw.paymentMethod || undefined,
      lines: this.lines.controls.map(c => {
        const value = c.getRawValue();
        const rawLineNum = Number(value.lineNum);
        const rawBaseEntry = Number(value.baseEntry);
        const rawBaseLine = Number(value.baseLine);
        return {
          lineNum: Number.isFinite(rawLineNum) && rawLineNum >= 0 ? rawLineNum : undefined,
          itemCode: String(value.itemCode || '').trim(),
          lineStatus: String(value.lineStatus || 'En attente').trim(),
          warehouseCode: String(value.warehouseCode || '').trim(),
          unitPrice: Number(value.unitPrice ?? 0),
          quantity: Number(value.quantity ?? 0),
          discountPct: Number(value.discountPct ?? 0),
          vatPct: Number(value.vatPct ?? 0),
          subtotalHt: Number(value.subtotalHt ?? 0),
          vatAmount: Number(value.vatAmount ?? 0),
          totalTtc: Number(value.totalTtc ?? 0),
          baseType: String(value.baseType ?? '').trim() || undefined,
          baseEntry: Number.isFinite(rawBaseEntry) && rawBaseEntry > 0 ? rawBaseEntry : undefined,
          baseLine: Number.isFinite(rawBaseLine) && rawBaseLine >= 0 ? rawBaseLine : undefined
        };
      })
    };

    payload.cardCode = this.extractCardCode(payload.cardCode);

    console.debug('[SAP FORM] payload envoye', payload);

    const request$ = isEditMode
      ? this.api.update(this.resource(), this.id()!, payload)
      : this.api.create(this.resource(), payload);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        console.debug('[SAP FORM] reponse backend', res);
        if (res.success === false || !res.data) {
          this.error.set(res.message || 'Echec d\'enregistrement.');
          this.saving.set(false);
          return;
        }
        const saved = res.data;
        this.success.set(isEditMode ? 'Document mis a jour avec succes' : 'Document cree avec succes');
        this.refreshListAfterMutation(saved, isEditMode);
      },
      error: (err) => {
        this.error.set(this.extractError(err));
        this.saving.set(false);
      },
      complete: () => {}
    });
  }

  private loadLookups(): void {
    let done = 0;
    const finalizeOne = () => {
      done += 1;
      if (done >= 2) {
        this.loadingLookups.set(false);
      }
    };

    this.partnerApi.getAll(1, INITIAL_CUSTOMERS_PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (partnersRes) => {
          const pickText = (...values: Array<unknown>): string => {
            for (const value of values) {
              const text = String(value ?? '').trim();
              if (text) return text;
            }
            return '-';
          };

          const mapped = (partnersRes.items ?? [])
            .map((row: any, index: number) => ({
              id: Number(row?.id ?? row?.DocEntry ?? index + 1),
              cardCode: pickText(row?.CardCode, row?.cardCode, row?.CustomerCode, row?.code),
              cardName: pickText(row?.CardName, row?.cardName, row?.CustomerName, row?.name),
              isActive: true
            } as Customer))
            .filter((c) => c.cardCode !== '-');

          if (mapped.length > 0) {
            this.customers.set(mapped);
            this.patchCardCodeForEdit(mapped);
            this.preloadCustomersInBackground();
            finalizeOne();
            return;
          }

          // Secondary fallback for SAP adapters exposing only /sap/clients
          if (!['Admin', 'Manager'].includes(this.auth.role())) {
            this.customers.set([]);
            this.error.set('Aucun client associe a ce commercial n est disponible.');
            finalizeOne();
            return;
          }

          this.customerApi.getAll(1, INITIAL_CUSTOMERS_PAGE_SIZE)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (res) => {
                const items = (res.data?.items ?? []).map((c) => ({
                  ...c,
                  cardCode: String(c.cardCode ?? '-').trim() || '-',
                  cardName: String(c.cardName ?? '-').trim() || '-'
                }));
                this.customers.set(items);
                if (items.length === 0) {
                  this.error.set('Aucun client disponible depuis SAP. Verifiez les routes /sap/partners ou /sap/clients.');
                }
                this.patchCardCodeForEdit(items);
                this.preloadCustomersInBackground();
                finalizeOne();
              },
              error: () => {
                this.error.set('Impossible de charger les clients.');
                finalizeOne();
              }
            });
        },
        error: () => {
          if (!['Admin', 'Manager'].includes(this.auth.role())) {
            this.customers.set([]);
            this.error.set('Impossible de charger vos partenaires commerciaux.');
            finalizeOne();
            return;
          }

          this.customerApi.getAll(1, INITIAL_CUSTOMERS_PAGE_SIZE)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe({
              next: (res) => {
                const items = (res.data?.items ?? []).map((c) => ({
                  ...c,
                  cardCode: String(c.cardCode ?? '-').trim() || '-',
                  cardName: String(c.cardName ?? '-').trim() || '-'
                }));
                this.customers.set(items);
                if (items.length === 0) {
                  this.error.set('Aucun client disponible depuis SAP. Verifiez les routes /sap/partners ou /sap/clients.');
                }
                this.patchCardCodeForEdit(items);
                this.preloadCustomersInBackground();
                finalizeOne();
              },
              error: () => {
                this.error.set('Impossible de charger les clients.');
                finalizeOne();
              }
            });
        }
      });

    this.productApi.getAll(1, INITIAL_PRODUCTS_PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          const payload = res?.data ?? res;
          const items = Array.isArray(payload?.items) ? payload.items : [];
          this.products.set(items);
          this.syncLineProductsByItemCode();
          this.hydrateFromCatalogCartIfNeeded();
          this.preloadProductsInBackground();
          finalizeOne();
        },
        error: () => {
          this.error.set('Impossible de charger les articles.');
          finalizeOne();
        }
      });
  }

  private preloadCustomersInBackground(): void {
    this.partnerApi.getAll(1, BACKGROUND_CUSTOMERS_PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (partnersRes) => {
          const pickText = (...values: Array<unknown>): string => {
            for (const value of values) {
              const text = String(value ?? '').trim();
              if (text) return text;
            }
            return '-';
          };

          const mapped = (partnersRes.items ?? [])
            .map((row: any, index: number) => ({
              id: Number(row?.id ?? row?.DocEntry ?? index + 1),
              cardCode: pickText(row?.CardCode, row?.cardCode, row?.CustomerCode, row?.code),
              cardName: pickText(row?.CardName, row?.cardName, row?.CustomerName, row?.name),
              isActive: true
            } as Customer))
            .filter((c) => c.cardCode !== '-');

          if (mapped.length === 0) return;
          this.customers.update((current) => this.mergeCustomers(current, mapped));
          this.patchCardCodeForEdit(this.customers());
        },
        error: () => {}
      });
  }

  private preloadProductsInBackground(): void {
    this.productApi.getAll(1, BACKGROUND_PRODUCTS_PAGE_SIZE)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          const payload = res?.data ?? res;
          const items = Array.isArray(payload?.items) ? payload.items : [];
          if (items.length === 0) return;
          this.products.update((current) => this.mergeProducts(current, items));
          this.syncLineProductsByItemCode();
          this.hydrateFromCatalogCartIfNeeded();
        },
        error: () => {}
      });
  }

  private hydrateFromCatalogCartIfNeeded(): void {
    if (this.isEdit()) return;
    if (this.resource() !== 'orders') return;
    if (this.route.snapshot.queryParamMap.get('fromCatalog') !== '1') return;
    if (this.lines.length > 0) return;

    const cartLines = this.catalogCart.getLines();
    if (cartLines.length === 0) return;

    for (const cartLine of cartLines) {
      const code = String(cartLine.itemCode ?? '').trim();
      if (!code) continue;
      this.addLine({
        itemCode: code,
        quantity: Math.max(1, Number(cartLine.quantity ?? 1)),
        unitPrice: Math.max(0, Number(cartLine.unitPrice ?? 0)),
        warehouseCode: String(cartLine.warehouseCode ?? '01').trim() || '01',
        vatPct: 20
      });
    }

    this.syncLineProductsByItemCode();
    this.catalogCart.clear();
  }

  private mergeCustomers(existing: Customer[], incoming: Customer[]): Customer[] {
    const byCode = new Map<string, Customer>();
    for (const item of existing) {
      const key = String(item.cardCode ?? '').trim().toLowerCase();
      if (key) byCode.set(key, item);
    }
    for (const item of incoming) {
      const key = String(item.cardCode ?? '').trim().toLowerCase();
      if (!key) continue;
      if (!byCode.has(key)) byCode.set(key, item);
    }
    return Array.from(byCode.values());
  }

  private mergeProducts(existing: Product[], incoming: Product[]): Product[] {
    const byCode = new Map<string, Product>();
    for (const item of existing) {
      const key = String(item.itemCode ?? '').trim().toLowerCase();
      if (key) byCode.set(key, item);
    }
    for (const item of incoming) {
      const key = String(item.itemCode ?? '').trim().toLowerCase();
      if (!key) continue;
      if (!byCode.has(key)) byCode.set(key, item);
    }
    return Array.from(byCode.values());
  }

  private patchCardCodeForEdit(items: Customer[]): void {
    const currentValue = String(this.form.get('cardCode')?.value ?? '').trim();

    if (!currentValue && this.isEdit()) {
      const guessed = items.find(c => c.id === this.loadedCustomerId)?.cardCode;
      if (guessed) {
        const customer = items.find(c => c.cardCode === guessed);
        this.form.patchValue({ cardCode: customer ? this.customerPickerValue(customer) : guessed });
        this.customerSearch.set(customer ? this.customerDisplayName(customer) : guessed);
      }
      return;
    }

    if (currentValue) {
      const code = this.extractCardCode(currentValue);
      const customer = items.find(c => String(c.cardCode ?? '').trim().toLowerCase() === code.toLowerCase());
      if (customer) {
        this.form.patchValue({ cardCode: this.customerPickerValue(customer) }, { emitEvent: false });
        this.customerSearch.set(this.customerDisplayName(customer));
      }
    }
  }

  private load(): void {
    this.api.getById(this.resource(), this.id()!)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const d = res.data;
          if (!d) {
            this.error.set('Document introuvable.');
            return;
          }

          this.loadedCustomerId = Number(d.customerId ?? 0) || null;
          const rawStatus = String(d.status ?? '').trim().toLowerCase();
          const compactStatus = rawStatus.replace(/[\s_-]/g, '');
          const isOpen = rawStatus === 'open'
            || rawStatus === 'o'
            || compactStatus === 'bostopen'
            || (compactStatus.includes('open') && !compactStatus.includes('close'));
          this.loadedDocStatus.set(isOpen ? 'Open' : 'Closed');

          this.form.patchValue({
            cardCode: d.cardCode || '',
            docDate: (d.docDate || d.postingDate || '').slice(0, 10),
            dueDate: (d.dueDate || '').slice(0, 10),
            comments: d.comments || '',
            paymentMethod: d.paymentMethod || 'Virement'
          });
          this.customerSearch.set(this.customerDisplayFromCode(d.cardCode || '') || this.documentCardName(d) || d.cardCode || '');

          if (!this.canModify()) {
            this.form.disable({ emitEvent: false });
          }

          this.lines.clear();
          const safeLines = (d.lines || []).filter((l) => {
            const itemCode = String(l?.itemCode ?? '').trim();
            const quantity = Number(l?.quantity ?? 0);
            const unitPrice = Number(l?.unitPrice ?? 0);
            return itemCode !== '' || quantity > 0 || unitPrice > 0;
          });
          for (const l of safeLines) this.addLine(l);
          this.syncLineProductsByItemCode();
        },
        error: () => this.error.set('Erreur lors du chargement du document.')
      });
  }

  private loadGenerationDraft(): void {
    const sourceResource = this.sourceResource();
    const sourceDocumentId = this.sourceDocumentId();
    if (!sourceResource || !sourceDocumentId) {
      this.error.set('Document source introuvable pour préparer le brouillon.');
      return;
    }

    this.api.getById(sourceResource, sourceDocumentId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const source = res.data;
          if (!source) {
            this.error.set('Document source introuvable.');
            return;
          }

          const cardCode = String(source.cardCode ?? '').trim();
          if (!cardCode) {
            this.error.set('Le document source ne contient pas de client valide.');
            return;
          }

          const today = new Date().toISOString().slice(0, 10);
          this.form.patchValue({
            cardCode,
            docDate: today,
            dueDate: today,
            comments: '',
            paymentMethod: source.paymentMethod || 'Virement'
          });
          this.customerSearch.set(this.customerDisplayFromCode(cardCode) || this.documentCardName(source) || cardCode);

          this.lines.clear();
          const lines = this.buildDraftLinesFromSource(source);
          for (const line of lines) {
            this.addLine(line);
          }
          this.syncLineProductsByItemCode();

          if (this.lines.length === 0) {
            this.error.set('Aucune ligne ouverte disponible pour créer ce document.');
          }
        },
        error: () => this.error.set('Erreur lors du chargement du document source.')
      });
  }

  private buildDraftLinesFromSource(source: CommercialDocument): Partial<CommercialDocumentLine>[] {
    const selected = new Set(this.sourceLineNums());
    const hasSelection = selected.size > 0;
    const baseType = this.docObjectCodeForResource(this.sourceResource());

    return (source.lines ?? [])
      .map((line, index) => ({ line, index }))
      .filter(({ line, index }) => {
        if (this.isClosedLineStatus(this.normalizeLineStatusToken(line.lineStatus ?? 'Open'))) {
          return false;
        }
        const lineNum = Number(line.lineNum ?? index);
        return !hasSelection || selected.has(lineNum);
      })
      .map(({ line, index }) => ({
        lineNum: Number(line.lineNum ?? index),
        productId: line.productId,
        itemCode: String(line.itemCode ?? '').trim(),
        itemName: line.itemName,
        warehouseCode: String(line.warehouseCode ?? '01').trim() || '01',
        quantity: Math.max(1, Number(line.quantity ?? 1)),
        maxQuantity: Math.max(1, Number(line.quantity ?? 1)),
        unitPrice: Number(line.unitPrice ?? 0),
        discountPct: Number(line.discountPct ?? 0),
        vatPct: Number(line.vatPct ?? 0),
        subtotalHt: Number(line.subtotalHt ?? line.lineTotal ?? 0),
        vatAmount: Number(line.vatAmount ?? 0),
        totalTtc: Number(line.totalTtc ?? ((Number(line.subtotalHt ?? line.lineTotal ?? 0)) + Number(line.vatAmount ?? 0))),
        lineStatus: 'Open',
        baseType,
        baseEntry: source.id,
        baseLine: Number(line.lineNum ?? index)
      }))
      .filter((line) => line.itemCode !== '' && Number(line.quantity ?? 0) > 0);
  }

  private syncLineProductsByItemCode(): void {
    const products = this.products();
    if (products.length === 0 || this.lines.length === 0) {
      return;
    }

    for (let i = 0; i < this.lines.length; i++) {
      const group = this.lines.at(i);
      const itemCode = String(group.get('itemCode')?.value ?? '').trim().toLowerCase();
      if (!itemCode) continue;

      const found = products.find((p) => String(p.itemCode ?? '').trim().toLowerCase() === itemCode);
      if (!found) {
        this.recalculateLine(i);
        continue;
      }

      group.patchValue({ productId: found.id, productLookup: this.productLookupLabel(found) }, { emitEvent: false });
      this.recalculateLine(i);
    }
  }

  customerPickerValue(customer: Customer): string {
    const cardCode = String(customer.cardCode ?? '').trim();
    return cardCode;
  }

  private documentCardName(document: CommercialDocument): string {
    return String((document as CommercialDocument & { cardName?: string }).cardName ?? '').trim();
  }

  customerDisplayName(customer: Customer): string {
    const name = String(customer.cardName ?? '').trim();
    const code = String(customer.cardCode ?? '').trim();
    return name || code;
  }

  private customerDisplayFromCode(cardCode: string): string {
    const code = String(cardCode ?? '').trim();
    if (!code) return '';
    const customer = this.customers().find(c => String(c.cardCode ?? '').trim().toLowerCase() === code.toLowerCase());
    return customer ? this.customerDisplayName(customer) : '';
  }

  private extractCardCode(value: string): string {
    const raw = String(value ?? '').trim();
    if (!raw) return '';

    const customers = this.customers();
    const asCode = customers.find(c => String(c.cardCode ?? '').trim().toLowerCase() === raw.toLowerCase());
    if (asCode?.cardCode) return asCode.cardCode;

    const asName = customers.find(c => String(c.cardName ?? '').trim().toLowerCase() === raw.toLowerCase());
    if (asName?.cardCode) return asName.cardCode;

    const separatorIndex = raw.indexOf(' - ');
    if (separatorIndex > 0) {
      const left = raw.slice(0, separatorIndex).trim();
      const right = raw.slice(separatorIndex + 3).trim();

      const leftAsCode = customers.find(c => String(c.cardCode ?? '').trim().toLowerCase() === left.toLowerCase());
      if (leftAsCode?.cardCode) return leftAsCode.cardCode;

      const rightAsCode = customers.find(c => String(c.cardCode ?? '').trim().toLowerCase() === right.toLowerCase());
      if (rightAsCode?.cardCode) return rightAsCode.cardCode;

      const leftAsName = customers.find(c => String(c.cardName ?? '').trim().toLowerCase() === left.toLowerCase());
      if (leftAsName?.cardCode) return leftAsName.cardCode;

      const rightAsName = customers.find(c => String(c.cardName ?? '').trim().toLowerCase() === right.toLowerCase());
      if (rightAsName?.cardCode) return rightAsName.cardCode;
    }

    return raw;
  }

  private normalizeSearch(value: unknown): string {
    return String(value ?? '').trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  private buildMissingFieldsMessage(): string {
    const missingFields: string[] = [];

    const cardCode = String(this.form.get('cardCode')?.value ?? '').trim();
    const docDate = String(this.form.get('docDate')?.value ?? '').trim();
    const dueDate = String(this.form.get('dueDate')?.value ?? '').trim();
    const paymentMethod = String(this.form.get('paymentMethod')?.value ?? '').trim();

    if (!cardCode) {
      missingFields.push('Client');
    }

    if (!docDate) {
      missingFields.push('Date document');
    }

    if (this.showDeliveryDate() && !dueDate) {
      missingFields.push('Date livraison');
    }

    if (!paymentMethod) {
      missingFields.push('Mode de paiement');
    }

    if (this.lines.length === 0) {
      missingFields.push('Au moins une ligne');
    }

    if (missingFields.length === 0) {
      return 'vous devez ajouter au moins une ligne de commande.';
    }

    if (missingFields.length === 1) {
      return `${missingFields[0]} est obligatoire.`;
    }

    if (missingFields.length === 2) {
      return `${missingFields[0]} et ${missingFields[1]} sont obligatoires.`;
    }

    return `${missingFields.slice(0, -1).join(', ')} et ${missingFields.at(-1)} sont obligatoires.`;
  }

  private sumLineField(field: 'subtotalHt' | 'vatAmount' | 'totalTtc'): number {
    return this.lines.controls.reduce((acc, control) => {
      const value = Number(control.get(field)?.value ?? 0);
      return acc + (Number.isFinite(value) ? value : 0);
    }, 0);
  }

  private extractError(err: any): string {
    console.error('[SAP FORM] erreur backend', err);

    const explicitError = err?.error?.error;
    if (typeof explicitError === 'string' && explicitError.trim() !== '') {
      return explicitError;
    }

    const explicitMessage = err?.error?.message;
    if (typeof explicitMessage === 'string' && explicitMessage.trim() !== '') {
      return explicitMessage;
    }

    if (err?.status === 400) {
      return err?.error?.error || err?.error?.message || 'Requete invalide (400). Verifier CardCode et DocumentLines.';
    }

    if (err?.status === 401) {
      return err?.error?.error || err?.error?.message || 'Acces non autorise (401).';
    }

    if (err?.status === 500) {
      return err?.error?.error || err?.error?.message || 'Erreur serveur (500).';
    }

    const apiMessage = err?.error?.message;
    if (apiMessage) return apiMessage;

    const apiErrors = err?.error?.errors;
    if (Array.isArray(apiErrors) && apiErrors.length > 0) {
      return apiErrors.join(' | ');
    }

    if (typeof err?.error === 'string' && err.error.trim() !== '') {
      return err.error;
    }

    if (err?.status === 0) {
      return 'Impossible de contacter le serveur API.';
    }

    return 'Erreur lors de l\'enregistrement.';
  }

  private refreshListAfterMutation(saved: CommercialDocument, isEditMode: boolean): void {
    if (isEditMode) {
      this.router.navigateByUrl(this.backRoute());
      return;
    } else {
      this.router.navigate(['/', this.routeSegmentForResource(), saved.id]);
    }

    const filters: CommercialListFilters = { page: 1, pageSize: 100 };
    this.api.getList(this.resource(), filters)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          const payload = res.data;
          const items = payload?.items ?? [];
          if (typeof window !== 'undefined') {
            window.dispatchEvent(new CustomEvent(COMMERCIAL_REFRESH_EVENT, {
              detail: {
                resource: this.resource(),
                items,
                totalCount: payload?.totalCount ?? items.length,
                page: payload?.page ?? 1,
                pageSize: payload?.pageSize ?? 100
              }
            }));
          }
        },
        error: () => {}
      });
  }

  private routeSegmentForResource(): string {
    return this.resource() === 'invoices' ? 'factures' : this.resource();
  }

  private normalizeLineStatusToken(value: unknown): string {
    return String(value ?? '')
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[\s_-]/g, '');
  }

  private isClosedLineStatus(statusToken: string): boolean {
    return statusToken === 'c'
      || statusToken.includes('close')
      || statusToken.includes('ferme')
      || statusToken.includes('clotur')
      || statusToken === 'bostclose';
  }

  backRoute(): string {
    const target = this.returnTo().trim();
    if (target.startsWith('/')) return target;
    return `/${this.routeSegmentForResource()}`;
  }

  private entityLabel(): string {
    switch (this.resource()) {
      case 'orders': return 'BC';
      case 'deliverynotes': return 'BL';
      case 'invoices': return 'facture';
      case 'quotes': return 'devis';
      case 'creditnotes': return 'avoir';
      case 'returns': return 'retour';
      default: return 'document';
    }
  }

  private resolveSourceResource(): CommercialResource | null {
    const raw = String(this.route.snapshot.queryParamMap.get('sourceResource') ?? '').trim().toLowerCase();
    if (raw === 'quotes' || raw === 'orders' || raw === 'deliverynotes' || raw === 'invoices' || raw === 'creditnotes' || raw === 'returns') {
      return raw;
    }
    return null;
  }

  private resolveSourceDocumentId(): number | null {
    const raw = Number(this.route.snapshot.queryParamMap.get('sourceId'));
    return Number.isFinite(raw) && raw > 0 ? Math.trunc(raw) : null;
  }

  private resolveSourceLineNums(): number[] {
    const raw = String(this.route.snapshot.queryParamMap.get('lineNums') ?? '').trim();
    if (!raw) return [];
    return raw
      .split(',')
      .map((value) => Number(value.trim()))
      .filter((value, index, array) => Number.isFinite(value) && value >= 0 && array.indexOf(value) === index)
      .map((value) => Math.trunc(value));
  }

  private docObjectCodeForResource(resource: CommercialResource | null): string | undefined {
    switch (resource) {
      case 'quotes': return '23';
      case 'orders': return '17';
      case 'deliverynotes': return '15';
      case 'invoices': return '13';
      case 'creditnotes': return '14';
      case 'returns': return '16';
      default: return undefined;
    }
  }

  private resolveResource(): CommercialResource {
    const routeData = this.route.snapshot.data['resource'] as CommercialResource | undefined;
    if (routeData) return routeData;
    const parentData = this.route.snapshot.parent?.data['resource'] as CommercialResource | undefined;
    return parentData ?? 'orders';
  }

  private resolveDocumentId(): number | null {
    const snapshots = [...this.route.snapshot.pathFromRoot].reverse();
    for (const snapshot of snapshots) {
      const rawId = snapshot.paramMap.get('id');
      if (!rawId) continue;

      const id = Number(rawId);
      if (Number.isFinite(id) && id > 0) {
        return Math.trunc(id);
      }
    }

    return null;
  }
}
