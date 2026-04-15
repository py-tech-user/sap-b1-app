import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PurchaseOrderApiService } from '../../../core/services/purchase-order-api.service';

@Component({
  selector: 'app-po-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <h1>Nouveau bon de commande fournisseur</h1>
        <a routerLink="/purchase-orders" class="btn-back">Annuler</a>
      </div>

      <form (ngSubmit)="submit()" class="form">
        <div class="row">
          <div class="form-group">
            <label>Fournisseur (ID) *</label>
            <input type="number" [(ngModel)]="form.supplierId" name="supplierId" required />
          </div>
          <div class="form-group">
            <label>Date livraison prevue</label>
            <input type="date" [(ngModel)]="form.expectedDate" name="expectedDate" />
          </div>
        </div>
        <div class="row">
          <div class="form-group">
            <label>Reference</label>
            <input type="text" [(ngModel)]="form.reference" name="reference" placeholder="Ref. interne" />
          </div>
          <div class="form-group">
            <label>Devise</label>
            <input type="text" [(ngModel)]="form.currency" name="currency" placeholder="EUR" />
          </div>
        </div>
        <div class="form-group">
          <label>Commentaires</label>
          <textarea [(ngModel)]="form.comments" name="comments" rows="2" placeholder="Notes pour le fournisseur..."></textarea>
        </div>

        <h3>Lignes de commande</h3>
        @for (line of form.lines; track $index) {
          <div class="line-row">
            <input type="number" [(ngModel)]="line.productId" [name]="'pid'+$index" placeholder="ID Produit *" required />
            <input type="number" [(ngModel)]="line.quantity" [name]="'qty'+$index" placeholder="Qte *" required min="1" />
            <input type="number" [(ngModel)]="line.unitPrice" [name]="'price'+$index" placeholder="Prix unit." step="0.01" />
            <input type="number" [(ngModel)]="line.vatPct" [name]="'vat'+$index" placeholder="TVA %" step="0.01" />
            <button type="button" class="btn-remove" (click)="removeLine($index)">X</button>
          </div>
        }
        <button type="button" class="btn-add" (click)="addLine()">+ Ajouter une ligne</button>

        @if (error()) {
          <div class="error-msg">{{ error() }}</div>
        }

        <div class="form-actions">
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : 'Creer le BC' }}</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .page { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .form { max-width: 900px; }
    .row { display: flex; gap: 1rem; }
    .row .form-group { flex: 1; }
    .form-group { margin-bottom: 1.2rem; }
    .form-group label { display: block; font-weight: 600; margin-bottom: .4rem; color: #555; font-size: .9rem; }
    .form-group input, .form-group textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; }
    h3 { margin: 1.5rem 0 1rem; }
    .line-row { display: flex; gap: .5rem; margin-bottom: .5rem; align-items: center; }
    .line-row input { flex: 1; padding: .5rem; border: 1px solid #ddd; border-radius: 6px; }
    .btn-remove { background: #e17055; color: white; border: none; border-radius: 50%; width: 28px; height: 28px; cursor: pointer; }
    .btn-add { background: none; border: 1px dashed #667eea; color: #667eea; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; margin-top: .5rem; }
    .form-actions { margin-top: 2rem; }
    .btn-primary { background: #667eea; color: white; border: none; padding: .7rem 1.5rem; border-radius: 6px; cursor: pointer; font-weight: 600; font-size: 1rem; }
    .btn-primary:disabled { opacity: .5; }
    .error-msg { background: #ffe0e0; color: #c00; padding: 1rem; border-radius: 6px; margin: 1rem 0; }
  `]
})
export class PoFormComponent {
  saving = signal(false);
  error = signal<string | null>(null);
  
  form = { 
    supplierId: null as number | null, 
    expectedDate: '', 
    reference: '',
    currency: 'EUR',
    comments: '', 
    lines: [{ productId: null as number | null, quantity: 1, unitPrice: 0, vatPct: 20 }] 
  };

  constructor(private api: PurchaseOrderApiService, private router: Router) {}

  addLine(): void { 
    this.form.lines.push({ productId: null, quantity: 1, unitPrice: 0, vatPct: 20 }); 
  }

  removeLine(i: number): void { 
    if (this.form.lines.length > 1) {
      this.form.lines.splice(i, 1); 
    }
  }

  submit(): void {
    this.error.set(null);

    if (!this.form.supplierId) {
      this.error.set('Veuillez saisir l ID du fournisseur.');
      return;
    }

    const validLines = this.form.lines.filter(l => l.productId && l.quantity > 0);
    if (validLines.length === 0) {
      this.error.set('Veuillez ajouter au moins une ligne avec un produit et une quantite.');
      return;
    }

    this.saving.set(true);
    
    const payload = {
      supplierId: this.form.supplierId,
      expectedDate: this.form.expectedDate ? new Date(this.form.expectedDate).toISOString() : null,
      reference: this.form.reference || null,
      currency: this.form.currency || 'EUR',
      comments: this.form.comments || null,
      lines: validLines.map(l => ({
        productId: l.productId,
        quantity: l.quantity,
        unitPrice: l.unitPrice || 0,
        vatPct: l.vatPct || 20
      }))
    };

    console.log('[PO] Envoi payload:', payload);

    this.api.create(payload).subscribe({
      next: () => this.router.navigate(['/purchase-orders']),
      error: (err) => {
        this.saving.set(false);
        this.error.set(err.error?.message || 'Erreur lors de la creation.');
      }
    });
  }
}
