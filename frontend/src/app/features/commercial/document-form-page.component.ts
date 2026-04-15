import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialApiService } from '../../core/services/commercial-api.service';
import { CustomerApiService } from '../../core/services/customer-api.service';
import { PartnerApiService } from '../../core/services/partner-api.service';
import { Product, ProductApiService } from '../../core/services/product-api.service';
import { COMMERCIAL_META } from './commercial-meta';
import { CommercialDocument, CommercialDocumentLine, CommercialListFilters, CommercialResource, Customer } from '../../core/models/models';

const COMMERCIAL_REFRESH_EVENT = 'commercialDocuments:updated';

@Component({
  selector: 'app-document-form',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="page">
      <a [routerLink]="backRoute()" class="btn-sm">← Retour</a>

      <h1>{{ meta().icon }} {{ isEdit() ? 'Editer' : 'Créer' }} {{ meta().singular }}</h1>

      <form [formGroup]="form" (ngSubmit)="save()" class="card">
        <div class="top-grid">
          <div class="field field-wide">
            <label>Client *</label>
            <input formControlName="cardCode" list="customer-options" placeholder="Rechercher et sélectionner client" />
            <datalist id="customer-options">
              @for (c of customers(); track c.id) {
                <option [value]="c.cardCode || ''" [label]="(c.cardCode || '-') + ' - ' + (c.cardName || '-')"></option>
              }
            </datalist>
          </div>

          <div class="field">
            <label>Date commande *</label>
            <input type="date" formControlName="docDate" />
          </div>
          @if (showDeliveryDate()) {
            <div class="field">
              <label>Date livraison *</label>
              <input type="date" formControlName="dueDate" />
            </div>
          }
          <div class="field field-wide">
            <label>Commentaires</label>
            <textarea rows="3" formControlName="comments"></textarea>
          </div>
          <div class="field field-wide">
            <label>Mode de paiement *</label>
            <input formControlName="paymentMethod" placeholder="Ex: Virement" />
          </div>
        </div>

        <div class="lines-head">
          <h3>Lignes</h3>
          <button class="btn-outline" type="button" (click)="addLine()" [disabled]="!canModify()">+ Ajouter ligne</button>
        </div>

        <p class="lines-hint">
          Sélectionne un article, puis ItemCode et WarehouseCode sont remplis automatiquement.
        </p>

        <div class="lines-scroll">
          <div class="line-row line-row-header" aria-hidden="true">
            <span>Article *</span>
            <span>ItemCode *</span>
            <span>Statut ligne</span>
            <span>Prix HT</span>
            <span>Quantite</span>
            <span>Sous-total HT</span>
            <span>Remise %</span>
            <span>TVA %</span>
            <span>Montant TVA</span>
            <span>Total TTC</span>
            <span>WarehouseCode *</span>
            <span>Action</span>
          </div>

          <div formArrayName="lines">
            @for (line of lines.controls; track $index; let i = $index) {
              <div [formGroupName]="i" class="line-row">
                <input
                  formControlName="productLookup"
                  list="product-options"
                  placeholder="Rechercher et sélectionner article"
                  (input)="onProductLookupInput(i, $event)"
                  [readonly]="!canEditLine(i)" />

                <input formControlName="itemCode" placeholder="Ex: A00001" aria-label="ItemCode" readonly />

                <input formControlName="lineStatus" placeholder="Statut" aria-label="Statut ligne" readonly />

                <input type="number" formControlName="unitPrice" min="0" step="0.01" placeholder="Prix HT" aria-label="Prix unitaire HT" (input)="recalculateLine(i)" [readonly]="!canEditLine(i)" />
                <input type="number" formControlName="quantity" min="1" step="1" placeholder="Qte" aria-label="Quantite" (input)="recalculateLine(i)" [readonly]="!canEditLine(i)" />
                <input type="number" formControlName="subtotalHt" placeholder="Sous-total HT" aria-label="Sous-total HT" readonly />
                <input type="number" formControlName="discountPct" min="0" max="100" step="0.01" placeholder="Remise %" aria-label="Remise" (input)="recalculateLine(i)" [readonly]="!canEditLine(i)" />
                <input type="number" formControlName="vatPct" min="0" step="0.01" placeholder="TVA %" aria-label="TVA" (input)="recalculateLine(i)" [readonly]="!canEditLine(i)" />
                <input type="number" formControlName="vatAmount" placeholder="Montant TVA" aria-label="Montant TVA" readonly />
                <input type="number" formControlName="totalTtc" placeholder="Total TTC" aria-label="Total TTC" readonly />
                <input formControlName="warehouseCode" placeholder="Ex: 01" aria-label="WarehouseCode" [readonly]="!canEditLine(i)" />
                <button type="button" class="btn-outline danger" (click)="removeLine(i)" [disabled]="!canEditLine(i)">Suppr.</button>
              </div>
            } @empty {
              <p class="empty">Aucune ligne.</p>
            }
          </div>
          <datalist id="product-options">
            @for (p of products(); track p.id) {
              <option [value]="productLookupLabel(p)"></option>
            }
          </datalist>
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
          <button class="btn-primary" [disabled]="form.invalid || saving() || loadingLookups() || !canModify()" type="submit">
            {{ saving() ? (isEdit() ? 'Mise à jour...' : 'Création...') : (isEdit() ? 'Mettre à jour' : 'Créer') }}
          </button>
          @if (error()) {
            <span class="action-feedback error">{{ error() }}</span>
          }
          @if (success()) {
            <span class="action-feedback success">{{ success() }}</span>
          }
        </div>

        @if (isEdit() && !canModify()) {
          <div class="error">Modification autorisée uniquement pour un devis/BC en statut Open.</div>
        }
      </form>

    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .card { background: #fff; border-radius: 8px; padding: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.08); display: flex; flex-direction: column; gap: 1rem; }
    .top-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.75rem; }
    .field { display: flex; flex-direction: column; gap: 0.25rem; }
    .field-wide { grid-column: 1 / -1; }
    .field input, .field textarea, .field select { border: 1px solid #d7d7d7; border-radius: 6px; padding: 0.45rem 0.6rem; }
    .lines-head { display: flex; justify-content: space-between; align-items: center; }
    .lines-hint { margin: 0; color: #555; font-size: 0.86rem; }
    .lines-scroll { overflow-x: auto; padding-bottom: 0.25rem; }
    .line-row { display: grid; min-width: 1560px; grid-template-columns: 220px 150px 110px 120px 100px 130px 100px 90px 130px 130px 130px 92px; gap: 0.5rem; margin-bottom: 0.5rem; align-items: center; }
    .line-row-header { margin-bottom: 0.25rem; color: #666; font-size: 0.78rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.02em; }
    .line-row-header span { padding: 0.1rem 0.2rem; }
    .line-row input, .line-row select { width: 100%; border: 1px solid #d7d7d7; border-radius: 6px; padding: 0.45rem 0.6rem; box-sizing: border-box; }
    .totals-row { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.75rem; }
    .total-box { border: 1px solid #d7d7d7; border-radius: 8px; padding: 0.65rem 0.8rem; background: #fafafa; display: flex; flex-direction: column; gap: 0.2rem; }
    .total-label { font-size: 0.78rem; color: #666; letter-spacing: 0.02em; }
    .total-value { font-size: 1.05rem; color: #111827; }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .btn-outline.danger { border-color: #c62828; color: #c62828; }
    .actions { display: flex; justify-content: flex-end; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
    .action-feedback { font-weight: 700; font-size: 0.9rem; }
    .error { color: #b00020; }
    .success { color: #1b5e20; }
    .empty { color: #888; }
    @media (max-width: 1200px) {
      .top-grid { grid-template-columns: 1fr 1fr; }
      .line-row-header { display: grid; }
      .totals-row { grid-template-columns: 1fr; }
    }
  `]
})
export class DocumentFormComponent implements OnInit {
  private readonly api = inject(CommercialApiService);
  private readonly customerApi = inject(CustomerApiService);
  private readonly partnerApi = inject(PartnerApiService);
  private readonly productApi = inject(ProductApiService);
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
  readonly saving = signal(false);
  readonly error = signal('');
  readonly success = signal('');
  readonly loadingLookups = signal(true);
  readonly loadedDocStatus = signal<'Open' | 'Closed'>('Open');
  readonly supportsLineStatusGuard = computed(() => this.resource() === 'quotes' || this.resource() === 'orders');
  readonly canModify = computed(() => !this.isEdit() || (this.supportsLineStatusGuard() && this.loadedDocStatus() === 'Open'));

  readonly customers = signal<Customer[]>([]);
  readonly products = signal<Product[]>([]);
  readonly customerSearch = signal('');
  private loadedCustomerId: number | null = null;

  readonly filteredCustomers = computed(() => {
    const q = this.customerSearch().trim().toLowerCase();
    if (!q) return this.customers();
    return this.customers().filter(c => (`${c.cardCode} ${c.cardName}`).toLowerCase().includes(q));
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
    this.loadLookups();
    if (this.isEdit()) this.load();
    else this.addLine();
  }

  onCustomerSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.customerSearch.set(input.value || '');
  }

  addLine(line?: Partial<CommercialDocumentLine>): void {
    const statusToken = this.normalizeLineStatusToken((line as any)?.lineStatus ?? (line as any)?.LineStatus ?? 'Open');
    const lineStatus = this.isClosedLineStatus(statusToken)
      ? 'Clôturée'
      : 'En attente';

    const group = this.fb.group({
      lineNum: [line?.lineNum ?? line?.id ?? null],
      productId: [line?.productId ?? null, [Validators.required]],
      productLookup: [line?.itemCode ? String(line.itemCode) : ''],
      itemCode: [line?.itemCode || '', [Validators.required]],
      lineStatus: [lineStatus],
      unitPrice: [line?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      quantity: [line?.quantity ?? 1, [Validators.required, Validators.min(1)]],
      subtotalHt: [line?.subtotalHt ?? 0],
      discountPct: [line?.discountPct ?? 0, [Validators.min(0), Validators.max(100)]],
      vatPct: [line?.vatPct ?? 20, [Validators.min(0)]],
      vatAmount: [line?.vatAmount ?? 0],
      totalTtc: [line?.totalTtc ?? 0],
      warehouseCode: [line?.warehouseCode || '', [Validators.required]],
    });

    if (this.isEdit() && this.supportsLineStatusGuard() && this.isClosedLineStatus(statusToken)) {
      group.get('productLookup')?.disable({ emitEvent: false });
      group.get('unitPrice')?.disable({ emitEvent: false });
      group.get('quantity')?.disable({ emitEvent: false });
      group.get('discountPct')?.disable({ emitEvent: false });
      group.get('vatPct')?.disable({ emitEvent: false });
      group.get('warehouseCode')?.disable({ emitEvent: false });
    }

    this.lines.push(group);
    this.recalculateLine(this.lines.length - 1);
  }

  productLookupLabel(product: Product): string {
    return `${product.itemCode || ''} - ${product.itemName || ''}`.trim();
  }

  onProductLookupInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = String(input.value ?? '').trim();
    if (!value) return;

    const normalized = value.toLowerCase();
    const product = this.products().find((p) => {
      const code = String(p.itemCode ?? '').trim().toLowerCase();
      const name = String(p.itemName ?? '').trim().toLowerCase();
      const label = `${code} - ${name}`;
      return normalized === code || normalized === name || normalized === label;
    });

    if (!product) return;

    const group = this.lines.at(index);
    group.patchValue({
      productId: Number(product.id ?? 0),
      productLookup: this.productLookupLabel(product)
    }, { emitEvent: false });

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

  removeLine(i: number): void {
    if (!this.canEditLine(i)) {
      this.error.set('Ligne fermée: suppression impossible.');
      return;
    }
    this.lines.removeAt(i);
  }

  canEditLine(index: number): boolean {
    if (!this.canModify()) return false;
    if (!this.isEdit()) return true;
    if (!this.supportsLineStatusGuard()) return true;

    const group = this.lines.at(index);
    const statusToken = this.normalizeLineStatusToken(group?.get('lineStatus')?.value ?? 'En attente');
    return !this.isClosedLineStatus(statusToken);
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
    const isEditMode = this.isEdit();

    if (isEditMode && !this.canModify()) {
      this.error.set('Modification autorisée uniquement pour un devis/BC en statut Open.');
      return;
    }

    if (this.form.invalid || this.lines.length === 0) {
      this.error.set('Client, dates, mode de paiement et DocumentLines sont obligatoires.');
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
        return {
          lineNum: Number(value.lineNum ?? 0) || undefined,
          itemCode: String(value.itemCode || '').trim(),
          lineStatus: String(value.lineStatus || 'En attente').trim(),
          warehouseCode: String(value.warehouseCode || '').trim(),
          unitPrice: Number(value.unitPrice ?? 0),
          quantity: Number(value.quantity ?? 0),
          discountPct: Number(value.discountPct ?? 0),
          vatPct: Number(value.vatPct ?? 0),
          subtotalHt: Number(value.subtotalHt ?? 0),
          vatAmount: Number(value.vatAmount ?? 0),
          totalTtc: Number(value.totalTtc ?? 0)
        };
      })
    };

    payload.cardCode = this.extractCardCode(payload.cardCode);

    console.debug('[SAP FORM] payload envoyé', payload);

    const request$ = isEditMode
      ? this.api.update(this.resource(), this.id()!, payload)
      : this.api.create(this.resource(), payload);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        console.debug('[SAP FORM] réponse backend', res);
        if (res.success === false || !res.data) {
          this.error.set(res.message || 'Echec d\'enregistrement.');
          this.saving.set(false);
          return;
        }
        const saved = res.data;
        this.success.set(isEditMode ? 'Document mis à jour avec succès' : 'Document créé avec succès');
        setTimeout(() => this.refreshListAfterMutation(saved, isEditMode), 400);
      },
      error: (err) => {
        this.error.set(this.extractError(err));
        this.saving.set(false);
      },
      complete: () => this.saving.set(false)
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

    this.partnerApi.getAll(1, 1000)
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
            finalizeOne();
            return;
          }

          // Secondary fallback for SAP adapters exposing only /sap/clients
          this.customerApi.getAll(1, 1000)
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
                  this.error.set('Aucun client disponible depuis SAP. Vérifiez les routes /sap/partners ou /sap/clients.');
                }
                this.patchCardCodeForEdit(items);
                finalizeOne();
              },
              error: () => {
                this.error.set('Impossible de charger les clients.');
                finalizeOne();
              }
            });
        },
        error: () => {
          this.customerApi.getAll(1, 1000)
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
                  this.error.set('Aucun client disponible depuis SAP. Vérifiez les routes /sap/partners ou /sap/clients.');
                }
                this.patchCardCodeForEdit(items);
                finalizeOne();
              },
              error: () => {
                this.error.set('Impossible de charger les clients.');
                finalizeOne();
              }
            });
        }
      });

    this.productApi.getAll(1, 2000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          const payload = res?.data ?? res;
          const items = Array.isArray(payload?.items) ? payload.items : [];
          this.products.set(items);
          this.syncLineProductsByItemCode();
          finalizeOne();
        },
        error: () => {
          this.error.set('Impossible de charger les articles.');
          finalizeOne();
        }
      });
  }

  private patchCardCodeForEdit(items: Customer[]): void {
    const currentCardCode = String(this.form.get('cardCode')?.value ?? '').trim();
    if (!currentCardCode && this.isEdit()) {
      const guessed = items.find(c => c.id === this.loadedCustomerId)?.cardCode;
      if (guessed) this.form.patchValue({ cardCode: guessed });
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

  private extractCardCode(value: string): string {
    const raw = String(value ?? '').trim();
    if (!raw) return '';
    const separatorIndex = raw.indexOf(' - ');
    return separatorIndex > 0 ? raw.slice(0, separatorIndex).trim() : raw;
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
      return err?.error?.error || err?.error?.message || 'Requête invalide (400). Vérifier CardCode et DocumentLines.';
    }

    if (err?.status === 401) {
      return err?.error?.error || err?.error?.message || 'Accès non autorisé (401).';
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
          if (isEditMode) {
            this.router.navigateByUrl(this.backRoute());
            return;
          }

          this.router.navigate(['/', this.resource(), 'en-attente']);
        },
        error: () => {
          this.router.navigate(['/', this.resource(), saved.id]);
        }
        });
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
    return this.isEdit() ? `/${this.resource()}/en-attente` : `/${this.resource()}`;
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
