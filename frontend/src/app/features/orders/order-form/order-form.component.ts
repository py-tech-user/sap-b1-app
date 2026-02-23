import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, Validators, ReactiveFormsModule, FormArray } from '@angular/forms';
import { CurrencyPipe } from '@angular/common';
import { OrderApiService } from '../../../core/services/order-api.service';
import { CustomerApiService } from '../../../core/services/customer-api.service';
import { ProductApiService, Product } from '../../../core/services/product-api.service';
import { Customer } from '../../../core/models/models';

@Component({
  selector:   'app-order-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe],
  template: `
    <div class="page-header">
      <a routerLink="/orders" class="back-btn">← Retour</a>
      <div>
        <h1>Nouvelle commande</h1>
        <p>Saisie d'une commande client</p>
      </div>
    </div>

    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <div class="top-grid">
        <!-- Header fields -->
        <div class="card">
          <h3>En-tête</h3>
          <div class="header-fields">
            <div class="form-group col-2">
              <label>Client *</label>
              <select formControlName="customerId">
                <option [ngValue]="null" disabled>Sélectionner un client</option>
                @for (c of customers(); track c.id) {
                  <option [ngValue]="c.id">{{ c.cardCode }} — {{ c.cardName }}</option>
                }
              </select>
            </div>
            <div class="form-group">
              <label>Date de livraison</label>
              <input type="date" formControlName="deliveryDate">
            </div>
            <div class="form-group">
              <label>Devise</label>
              <select formControlName="currency">
                <option value="EUR">EUR €</option>
                <option value="USD">USD $</option>
              </select>
            </div>
            <div class="form-group col-2">
              <label>Commentaires</label>
              <textarea formControlName="comments" rows="3"></textarea>
            </div>
          </div>
        </div>

        <!-- Totals card -->
        <div class="card totals-card">
          <h3>Récapitulatif</h3>
          <div class="totals-content">
            <div class="total-row">
              <span>Sous-total HT</span>
              <span>{{ subtotal() | currency:'EUR' }}</span>
            </div>
            <div class="total-row">
              <span>TVA</span>
              <span>{{ vatAmount() | currency:'EUR' }}</span>
            </div>
            <hr />
            <div class="total-row total-grand">
              <span>Total TTC</span>
              <strong>{{ grandTotal() | currency:'EUR' }}</strong>
            </div>
            <button type="submit" class="btn-primary save-btn"
                    [disabled]="form.invalid || lines.length === 0 || saving()">
              {{ saving() ? 'Création...' : '💾 Créer la commande' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Lines -->
      <div class="card lines-card">
        <div class="lines-header">
          <h3>Lignes de commande</h3>
          <button type="button" class="btn-outline" (click)="addLine()">+ Ajouter</button>
        </div>

        @if (lines.length === 0) {
          <div class="empty-lines">
            🛒 Aucune ligne. Cliquez sur "Ajouter".
          </div>
        }

        <div formArrayName="lines">
          @for (line of lines.controls; track $index; let i = $index) {
            <div [formGroupName]="i" class="line-row">
              <span class="line-num">{{ i + 1 }}</span>

              <div class="line-field line-product">
                <label>Article</label>
                <select formControlName="productId"
                        (change)="onProductSelect(+$any($event.target).value, i)">
                  <option [ngValue]="null" disabled>Choisir...</option>
                  @for (p of products(); track p.id) {
                    <option [ngValue]="p.id">{{ p.itemCode }} — {{ p.itemName }}</option>
                  }
                </select>
              </div>

              <div class="line-field line-qty">
                <label>Qté</label>
                <input type="number" formControlName="quantity" min="1" (input)="onChange()">
              </div>

              <div class="line-field line-price">
                <label>Prix HT</label>
                <input type="number" formControlName="unitPrice" min="0" step="0.01" (input)="onChange()">
              </div>

              <div class="line-field line-vat">
                <label>TVA %</label>
                <input type="number" formControlName="vatPct" min="0" max="100" (input)="onChange()">
              </div>

              <div class="line-total">
                {{ lineTotal(i) | currency:'EUR' }}
              </div>

              <button type="button" class="btn-remove" (click)="removeLine(i)">✕</button>
            </div>
          }
        </div>
      </div>
    </form>

    @if (errorMessage()) {
      <div class="error-toast">{{ errorMessage() }}</div>
    }
    @if (successMessage()) {
      <div class="success-toast">{{ successMessage() }}</div>
    }
  `,
  styles: [`
    .page-header { display: flex; align-items: center; gap: 12px; margin-bottom: 24px; }
    .page-header h1 { margin: 0; }
    .page-header p { color: #666; margin: 4px 0 0; }
    .back-btn { text-decoration: none; color: #1976d2; font-size: 14px; padding: 6px 12px; border-radius: 4px; }
    .back-btn:hover { background: #e3f2fd; }

    .card { background: white; border-radius: 8px; padding: 20px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
    .card h3 { margin: 0 0 16px 0; font-size: 16px; color: #333; }

    .top-grid { display: grid; grid-template-columns: 1fr 280px; gap: 16px; margin-bottom: 16px; }

    .header-fields { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .col-2 { grid-column: 1 / -1; }

    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .form-group label { font-size: 13px; color: #555; font-weight: 500; }
    .form-group input, .form-group select, .form-group textarea {
      padding: 8px 10px; border: 1px solid #ddd; border-radius: 4px; font-size: 14px;
    }
    .form-group input:focus, .form-group select:focus, .form-group textarea:focus {
      outline: none; border-color: #1976d2;
    }

    .totals-content { display: flex; flex-direction: column; gap: 10px; }
    .total-row { display: flex; justify-content: space-between; font-size: 14px; }
    .total-grand { font-size: 18px; }

    .btn-primary {
      width: 100%; padding: 10px; background: #1976d2; color: white;
      border: none; border-radius: 4px; font-size: 14px; cursor: pointer; margin-top: 8px;
    }
    .btn-primary:hover:not(:disabled) { background: #1565c0; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }

    .btn-outline {
      padding: 6px 14px; background: white; color: #1976d2; border: 1px solid #1976d2;
      border-radius: 4px; font-size: 13px; cursor: pointer;
    }
    .btn-outline:hover { background: #e3f2fd; }

    .lines-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
    .lines-header h3 { margin: 0; }

    .empty-lines { text-align: center; padding: 40px; color: #9e9e9e; font-size: 14px; }

    .line-row {
      display: flex; align-items: flex-end; gap: 8px; padding: 8px 0;
      border-bottom: 1px solid #f5f5f5; flex-wrap: wrap;
    }
    .line-num { width: 22px; text-align: center; color: #9e9e9e; padding-bottom: 10px; }
    .line-field { display: flex; flex-direction: column; gap: 4px; }
    .line-field label { font-size: 12px; color: #888; }
    .line-field input, .line-field select {
      padding: 6px 8px; border: 1px solid #ddd; border-radius: 4px; font-size: 13px;
    }
    .line-product { flex: 2; min-width: 200px; }
    .line-product select { width: 100%; }
    .line-qty { flex: 0 0 70px; }
    .line-qty input { width: 100%; }
    .line-price { flex: 0 0 100px; }
    .line-price input { width: 100%; }
    .line-vat { flex: 0 0 70px; }
    .line-vat input { width: 100%; }
    .line-total { flex: 0 0 100px; text-align: right; font-weight: 600; font-size: 14px; padding-bottom: 10px; }

    .btn-remove {
      background: none; border: none; color: #e53935; cursor: pointer;
      font-size: 16px; padding: 6px; border-radius: 4px;
    }
    .btn-remove:hover { background: #ffebee; }

    .error-toast {
      position: fixed; bottom: 20px; right: 20px; background: #c62828; color: white;
      padding: 12px 20px; border-radius: 6px; box-shadow: 0 2px 8px rgba(0,0,0,0.2); z-index: 1000;
    }
    .success-toast {
      position: fixed; bottom: 20px; right: 20px; background: #2e7d32; color: white;
      padding: 12px 20px; border-radius: 6px; box-shadow: 0 2px 8px rgba(0,0,0,0.2); z-index: 1000;
    }

    @media (max-width: 900px) {
      .top-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class OrderFormComponent implements OnInit {
  private fb          = inject(FormBuilder);
  private router      = inject(Router);
  private orderApi    = inject(OrderApiService);
  private customerApi = inject(CustomerApiService);
  private productApi  = inject(ProductApiService);

  saving    = signal(false);
  customers = signal<Customer[]>([]);
  products  = signal<Product[]>([]);
  errorMessage = signal('');
  successMessage = signal('');
  loadingData = true;

  // Trigger re-calc
  private _tick = signal(0);

  form = this.fb.group({
    customerId:   [null as number | null, Validators.required],
    deliveryDate: [null as string | null],
    currency:     ['EUR'],
    comments:     [''],
    lines: this.fb.array([])
  });

  get f()     { return this.form.controls; }
  get lines() { return this.form.get('lines') as FormArray; }

  ngOnInit(): void {
    let loaded = 0;
    const checkDone = () => { loaded++; if (loaded >= 2) this.loadingData = false; };

    this.customerApi.getAll(1, 500).subscribe({
      next: r => {
        if (r.data) this.customers.set(r.data.items);
        checkDone();
      },
      error: () => {
        this.errorMessage.set('Impossible de charger la liste des clients.');
        checkDone();
      }
    });
    this.productApi.getAll(1, 500).subscribe({
      next: (r: any) => {
        const payload = r.data ?? r;
        this.products.set(payload.items ?? payload);
        checkDone();
      },
      error: () => {
        this.errorMessage.set('Impossible de charger la liste des articles.');
        checkDone();
      }
    });
  }

  addLine(): void {
    this.lines.push(this.fb.group({
      productId: [null as number | null, Validators.required],
      quantity:  [1,   [Validators.required, Validators.min(1)]],
      unitPrice: [0,   Validators.required],
      vatPct:    [20]
    }));
  }

  removeLine(i: number): void {
    this.lines.removeAt(i);
    this.onChange();
  }

  onProductSelect(productId: number, i: number): void {
    const p = this.products().find(x => x.id === productId);
    if (p) {
      this.lines.at(i).patchValue({ unitPrice: p.price });
      this.onChange();
    }
  }

  onChange(): void { this._tick.update(n => n + 1); }

  lineTotal(i: number): number {
    this._tick(); // reactive dependency
    const l = this.lines.at(i).value;
    const ht = (l.quantity ?? 0) * (l.unitPrice ?? 0);
    return Math.round(ht * (1 + (l.vatPct ?? 0) / 100) * 100) / 100;
  }

  subtotal(): number {
    this._tick();
    let s = 0;
    for (let i = 0; i < this.lines.length; i++) {
      const l = this.lines.at(i).value;
      s += (l.quantity ?? 0) * (l.unitPrice ?? 0);
    }
    return Math.round(s * 100) / 100;
  }

  vatAmount(): number {
    this._tick();
    let v = 0;
    for (let i = 0; i < this.lines.length; i++) {
      const l  = this.lines.at(i).value;
      const ht = (l.quantity ?? 0) * (l.unitPrice ?? 0);
      v += ht * ((l.vatPct ?? 0) / 100);
    }
    return Math.round(v * 100) / 100;
  }

  grandTotal(): number { return this.subtotal() + this.vatAmount(); }

  onSubmit(): void {
    if (this.form.invalid || this.lines.length === 0) return;
    this.saving.set(true);
    this.errorMessage.set('');

    const v = this.form.value;

    this.orderApi.create({
      customerId:   v.customerId!,
      docDate:      new Date().toISOString(),
      deliveryDate: v.deliveryDate ?? undefined,
      comments:     v.comments ?? undefined,
      lines: this.lines.controls.map((_, i) => {
        const l = this.lines.at(i).value;
        return {
          productId: l.productId,
          quantity:  l.quantity,
          unitPrice: l.unitPrice
        };
      })
    }).subscribe({
      next: (res: any) => {
        // Le backend renvoie ApiResponse<OrderDto> : { success, message, data }
        const order = res.data ?? res;
        if (res.success === false) {
          this.errorMessage.set(res.message || 'Erreur lors de la cr\u00e9ation de la commande');
          this.saving.set(false);
          return;
        }
        this.successMessage.set(res.message || `Commande ${order.docNum} cr\u00e9\u00e9e avec succ\u00e8s !`);
        setTimeout(() => this.router.navigate(['/orders', order.id]), 1500);
      },
      error: err => {
        console.error('Erreur commande:', err);
        this.errorMessage.set(err.status === 0
          ? 'Impossible de contacter le serveur. V\u00e9rifiez que le backend est d\u00e9marr\u00e9.'
          : (err.error?.message || 'Erreur lors de la cr\u00e9ation de la commande'));
        this.saving.set(false);
        setTimeout(() => this.errorMessage.set(''), 8000);
      },
      complete: () => this.saving.set(false)
    });
  }
}
