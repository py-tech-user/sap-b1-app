import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { GoodsReceiptApiService } from '../../../core/services/goods-receipt-api.service';

@Component({
  selector: 'app-receipt-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <h1>📥 Nouvelle réception marchandises</h1>
        <a routerLink="/goods-receipts" class="btn-back">Annuler</a>
      </div>

      <form (ngSubmit)="submit()" class="form">
        <div class="row">
          <div class="form-group">
            <label>Fournisseur (ID)</label>
            <input type="number" [(ngModel)]="form.supplierId" name="supplierId" required />
          </div>
          <div class="form-group">
            <label>BC (ID, optionnel)</label>
            <input type="number" [(ngModel)]="form.purchaseOrderId" name="purchaseOrderId" />
          </div>
        </div>
        <div class="form-group">
          <label>Notes</label>
          <textarea [(ngModel)]="form.notes" name="notes" rows="3" placeholder="Notes de réception..."></textarea>
        </div>

        <h3>Lignes</h3>
        @for (line of form.lines; track $index) {
          <div class="line-row">
            <input type="number" [(ngModel)]="line.productId" [name]="'pid'+$index" placeholder="ID Produit" required />
            <input type="number" [(ngModel)]="line.expectedQuantity" [name]="'eqty'+$index" placeholder="Qté attendue" required />
            <input type="number" [(ngModel)]="line.receivedQuantity" [name]="'rqty'+$index" placeholder="Qté reçue" required />
            <input type="number" [(ngModel)]="line.unitPrice" [name]="'price'+$index" placeholder="Prix unit." step="0.01" />
            <button type="button" class="btn-remove" (click)="removeLine($index)">✕</button>
          </div>
        }
        <button type="button" class="btn-add" (click)="addLine()">+ Ajouter une ligne</button>

        <div class="form-actions">
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : 'Créer la réception' }}</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .page { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .form { max-width: 800px; }
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
  `]
})
export class ReceiptFormComponent {
  saving = signal(false);
  form: any = { supplierId: null, purchaseOrderId: null, notes: '', lines: [{ productId: null, expectedQuantity: 1, receivedQuantity: 1, unitPrice: null }] };

  constructor(private api: GoodsReceiptApiService, private router: Router) {}

  addLine(): void { this.form.lines.push({ productId: null, expectedQuantity: 1, receivedQuantity: 1, unitPrice: null }); }
  removeLine(i: number): void { this.form.lines.splice(i, 1); }

  submit(): void {
    this.saving.set(true);
    this.api.create(this.form).subscribe({
      next: () => this.router.navigate(['/goods-receipts']),
      error: () => this.saving.set(false)
    });
  }
}
