import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialApiService } from '../../../core/services/commercial-api.service';
import { CustomerApiService } from '../../../core/services/customer-api.service';
import { Product, ProductApiService } from '../../../core/services/product-api.service';
import { COMMERCIAL_META } from '../commercial-meta';
import { CommercialDocument, CommercialDocumentLine, CommercialListFilters, CommercialResource, Customer } from '../../../core/models/models';

const COMMERCIAL_REFRESH_EVENT = 'commercialDocuments:updated';

@Component({
  selector: 'app-document-form',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="page">
      <a [routerLink]="['/', resource()]" class="btn-sm">← Retour</a>

      <h1>{{ meta().icon }} {{ isEdit() ? 'Editer' : 'Créer' }} {{ meta().singular }}</h1>

      <form [formGroup]="form" (ngSubmit)="save()" class="card">
        <div class="top-grid">
          <div class="field field-wide">
            <label>Client *</label>
            <select formControlName="customerId">
              <option [ngValue]="null" disabled>Sélectionner un client</option>
              @for (c of filteredCustomers(); track c.id) {
                <option [ngValue]="c.id">{{ c.cardCode }} - {{ c.cardName }}</option>
              }
            </select>
          </div>

          <div class="field">
            <label>Date document</label>
            <input type="date" formControlName="docDate" />
          </div>
          <div class="field">
            <label>Echéance</label>
            <input type="date" formControlName="dueDate" />
          </div>
          <div class="field">
            <label>Devise</label>
            <input formControlName="currency" placeholder="MAD" />
          </div>
          <div class="field field-wide">
            <label>Commentaires</label>
            <textarea rows="3" formControlName="comments"></textarea>
          </div>
        </div>

        <div class="lines-head">
          <h3>Lignes</h3>
          <button class="btn-outline" type="button" (click)="addLine()">+ Ajouter ligne</button>
        </div>

        <p class="lines-hint">
          Sélectionne un article existant. Les informations article/code sont remplies automatiquement.
        </p>

        <div class="line-row line-row-header" aria-hidden="true">
          <span>Article</span>
          <span>Code</span>
          <span>Quantite</span>
          <span>Prix unitaire</span>
          <span>TVA (%)</span>
          <span>Action</span>
        </div>

        <div formArrayName="lines">
          @for (line of lines.controls; track $index; let i = $index) {
            <div [formGroupName]="i" class="line-row">
              <select formControlName="productId" (change)="onProductSelected(i)">
                <option [ngValue]="null" disabled>Sélectionner article</option>
                @for (p of products(); track p.id) {
                  <option [ngValue]="p.id">{{ p.itemName }}</option>
                }
              </select>

              <input [value]="selectedProductCode(i)" readonly placeholder="Code auto" />

              <input type="number" formControlName="quantity" min="1" step="1" placeholder="Qte" aria-label="Quantite" />
              <input type="number" formControlName="unitPrice" min="0" step="0.01" placeholder="Prix unitaire" aria-label="Prix unitaire" />
              <input type="number" formControlName="vatPct" min="0" step="0.01" placeholder="TVA %" aria-label="TVA pourcentage" />
              <button type="button" class="btn-outline danger" (click)="removeLine(i)">Suppr.</button>
            </div>
          } @empty {
            <p class="empty">Aucune ligne.</p>
          }
        </div>

        <div class="actions">
          <button class="btn-primary" [disabled]="form.invalid || saving() || loadingLookups()" type="submit">
            {{ saving() ? 'Enregistrement...' : 'Enregistrer' }}
          </button>
        </div>
      </form>

      @if (error()) {
        <div class="error">{{ error() }}</div>
      }
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
    .line-row { display: grid; grid-template-columns: 1.8fr 1fr 0.7fr 0.9fr 0.7fr auto; gap: 0.5rem; margin-bottom: 0.5rem; }
    .line-row-header { margin-bottom: 0.25rem; color: #666; font-size: 0.78rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.02em; }
    .line-row-header span { padding: 0.1rem 0.2rem; }
    .line-row input, .line-row select { border: 1px solid #d7d7d7; border-radius: 6px; padding: 0.45rem 0.6rem; }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .btn-outline.danger { border-color: #c62828; color: #c62828; }
    .actions { display: flex; justify-content: flex-end; }
    .error { color: #b00020; }
    .empty { color: #888; }
    @media (max-width: 1200px) {
      .top-grid { grid-template-columns: 1fr 1fr; }
      .line-row-header { display: none; }
      .line-row { grid-template-columns: 1fr; }
    }
  `]
})
export class DocumentFormComponent implements OnInit {
  private readonly api = inject(CommercialApiService);
  private readonly customerApi = inject(CustomerApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  readonly resource = signal<CommercialResource>(this.resolveResource());
  readonly id = signal<number | null>(this.route.snapshot.paramMap.has('id') ? Number(this.route.snapshot.paramMap.get('id')) : null);
  readonly isEdit = computed(() => this.id() !== null);
  readonly meta = computed(() => COMMERCIAL_META[this.resource()]);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly loadingLookups = signal(true);

  readonly customers = signal<Customer[]>([]);
  readonly products = signal<Product[]>([]);
  readonly customerSearch = signal('');

  readonly filteredCustomers = computed(() => {
    const q = this.customerSearch().trim().toLowerCase();
    if (!q) return this.customers();
    return this.customers().filter(c => (`${c.cardCode} ${c.cardName}`).toLowerCase().includes(q));
  });

  readonly form = this.fb.group({
    customerId: [null as number | null, [Validators.required, Validators.min(1)]],
    docDate: [new Date().toISOString().slice(0, 10)],
    dueDate: [''],
    currency: ['MAD'],
    comments: [''],
    lines: this.fb.array([])
  });

  get lines(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  constructor() {
    this.addLine();
  }

  ngOnInit(): void {
    this.loadLookups();
    if (this.isEdit()) this.load();
  }

  onCustomerSearch(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.customerSearch.set(input.value || '');
  }

  addLine(line?: Partial<CommercialDocumentLine>): void {
    this.lines.push(this.fb.group({
      productId: [line?.productId ?? null, Validators.required],
      itemCode: [line?.itemCode || ''],
      itemName: [line?.itemName || ''],
      quantity: [line?.quantity ?? 1, [Validators.required, Validators.min(1)]],
      unitPrice: [line?.unitPrice ?? 0, [Validators.required, Validators.min(0)]],
      vatPct: [line?.vatPct ?? 20]
    }));
  }

  removeLine(i: number): void {
    this.lines.removeAt(i);
  }

  selectedProductCode(index: number): string {
    const group = this.lines.at(index);
    const productId = Number(group.get('productId')?.value ?? 0);
    const product = this.products().find(p => p.id === productId);
    if (product) return product.itemCode;
    return String(group.get('itemCode')?.value || '');
  }

  onProductSelected(index: number): void {
    const group = this.lines.at(index);
    const productId = Number(group.get('productId')?.value ?? 0);
    const product = this.products().find(p => p.id === productId);
    if (!product) return;

    group.patchValue({
      itemCode: product.itemCode,
      itemName: product.itemName,
      unitPrice: product.price
    });
  }

  save(): void {
    if (this.form.invalid || this.lines.length === 0) return;

    const hasLineWithoutProduct = this.lines.controls.some(c => !c.get('productId')?.value);
    if (hasLineWithoutProduct) {
      this.error.set('Chaque ligne doit avoir un article sélectionné.');
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    const payload = {
      customerId: raw.customerId!,
      docDate: raw.docDate || undefined,
      dueDate: raw.dueDate || undefined,
      currency: raw.currency || undefined,
      comments: raw.comments || undefined,
      lines: this.lines.controls.map(c => {
        const value = c.getRawValue();
        return {
          productId: Number(value.productId),
          itemCode: value.itemCode || undefined,
          itemName: value.itemName || undefined,
          quantity: Number(value.quantity ?? 0),
          unitPrice: Number(value.unitPrice ?? 0),
          vatPct: Number(value.vatPct ?? 0)
        };
      })
    };

    const request$ = this.isEdit()
      ? this.api.update(this.resource(), this.id()!, payload)
      : this.api.create(this.resource(), payload);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        if (res.success === false || !res.data) {
          this.error.set(res.message || 'Echec d\'enregistrement.');
          this.saving.set(false);
          return;
        }
        this.refreshListAfterMutation(res.data);
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
    const endOne = () => {
      done += 1;
      if (done >= 2) {
        this.loadingLookups.set(false);
        this.syncLinesWithProducts();
      }
    };

    this.customerApi.getAll(1, 1000, undefined, true)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.customers.set(res.data?.items ?? []);
          endOne();
        },
        error: () => {
          this.error.set('Impossible de charger les clients.');
          endOne();
        }
      });

    this.productApi.getAll(1, 2000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: any) => {
          const payload = res.data ?? res;
          this.products.set(payload.items ?? []);
          endOne();
        },
        error: () => {
          this.error.set('Impossible de charger les articles.');
          endOne();
        }
      });
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

          this.form.patchValue({
            customerId: d.customerId,
            docDate: (d.docDate || d.postingDate || '').slice(0, 10),
            dueDate: (d.dueDate || '').slice(0, 10),
            currency: d.currency || 'MAD',
            comments: d.comments || ''
          });

          this.lines.clear();
          for (const l of d.lines || []) this.addLine(l);
          if (this.lines.length === 0) this.addLine();
          this.syncLinesWithProducts();
        },
        error: () => this.error.set('Erreur lors du chargement du document.')
      });
  }

  private syncLinesWithProducts(): void {
    const allProducts = this.products();
    if (!allProducts.length) return;

    for (let i = 0; i < this.lines.length; i++) {
      const group = this.lines.at(i);
      const productId = Number(group.get('productId')?.value ?? 0);
      if (productId > 0) {
        this.onProductSelected(i);
        continue;
      }

      const itemCode = String(group.get('itemCode')?.value || '').trim().toLowerCase();
      if (!itemCode) continue;

      const match = allProducts.find(p => p.itemCode.toLowerCase() === itemCode);
      if (!match) continue;

      group.patchValue({
        productId: match.id,
        itemCode: match.itemCode,
        itemName: match.itemName
      });
    }
  }

  private extractError(err: any): string {
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

  private refreshListAfterMutation(saved: CommercialDocument): void {
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
          this.router.navigate(['/', this.resource()]);
        },
        error: () => {
          this.router.navigate(['/', this.resource(), saved.id]);
        }
      });
  }

  private resolveResource(): CommercialResource {
    const routeData = this.route.snapshot.data['resource'] as CommercialResource | undefined;
    if (routeData) return routeData;
    const parentData = this.route.snapshot.parent?.data['resource'] as CommercialResource | undefined;
    return parentData ?? 'orders';
  }
}
