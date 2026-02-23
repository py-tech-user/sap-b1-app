import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ReturnApiService } from '../../../core/services/return-api.service';
import { OrderApiService } from '../../../core/services/order-api.service';

@Component({
  selector: 'app-return-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <h1>📦 Nouveau retour</h1>
        <a routerLink="/returns" class="btn-back">Annuler</a>
      </div>

      <form (ngSubmit)="submit()" class="form">
        <div class="form-group">
          <label>N° Commande (ID)</label>
          <input type="number" [(ngModel)]="form.orderId" name="orderId" required placeholder="ID de la commande" />
        </div>
        <div class="form-group">
          <label>Raison</label>
          <select [(ngModel)]="form.reason" name="reason" required>
            <option value="">Sélectionner...</option>
            <option value="Defective">Défectueux</option>
            <option value="WrongItem">Mauvais article</option>
            <option value="NotAsDescribed">Non conforme</option>
            <option value="Damaged">Endommagé</option>
            <option value="Other">Autre</option>
          </select>
        </div>
        <div class="form-group">
          <label>Commentaires</label>
          <textarea [(ngModel)]="form.comments" name="comments" rows="3" placeholder="Détails du retour..."></textarea>
        </div>

        <h3>Lignes</h3>
        @for (line of form.lines; track $index) {
          <div class="line-row">
            <input type="number" [(ngModel)]="line.productId" [name]="'pid'+$index" placeholder="ID Produit" required />
            <input type="number" [(ngModel)]="line.quantity" [name]="'qty'+$index" placeholder="Qté" required min="1" />
            <input type="number" [(ngModel)]="line.unitPrice" [name]="'price'+$index" placeholder="Prix unit." step="0.01" />
            <input type="text" [(ngModel)]="line.reason" [name]="'lreason'+$index" placeholder="Motif ligne" />
            <button type="button" class="btn-remove" (click)="removeLine($index)">✕</button>
          </div>
        }
        <button type="button" class="btn-add" (click)="addLine()">+ Ajouter une ligne</button>

        <div class="form-actions">
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : 'Créer le retour' }}</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .page { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .btn-back { padding: 0.5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .form { max-width: 800px; }
    .form-group { margin-bottom: 1.2rem; }
    .form-group label { display: block; font-weight: 600; margin-bottom: 0.4rem; color: #555; font-size: 0.9rem; }
    .form-group input, .form-group select, .form-group textarea { width: 100%; padding: 0.6rem; border: 1px solid #ddd; border-radius: 6px; font-size: 0.95rem; }
    h3 { margin: 1.5rem 0 1rem; color: #2d3436; }
    .line-row { display: flex; gap: 0.5rem; margin-bottom: 0.5rem; align-items: center; }
    .line-row input { flex: 1; padding: 0.5rem; border: 1px solid #ddd; border-radius: 6px; }
    .btn-remove { background: #e17055; color: white; border: none; border-radius: 50%; width: 28px; height: 28px; cursor: pointer; font-size: 0.8rem; }
    .btn-add { background: none; border: 1px dashed #667eea; color: #667eea; padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; margin-top: 0.5rem; }
    .form-actions { margin-top: 2rem; }
    .btn-primary { background: #667eea; color: white; border: none; padding: 0.7rem 1.5rem; border-radius: 6px; cursor: pointer; font-weight: 600; font-size: 1rem; }
    .btn-primary:disabled { opacity: .5; }
  `]
})
export class ReturnFormComponent {
  saving = signal(false);
  form: any = { orderId: null, reason: '', comments: '', lines: [{ productId: null, quantity: 1, unitPrice: null, reason: '' }] };

  constructor(private api: ReturnApiService, private router: Router) {}

  addLine(): void { this.form.lines.push({ productId: null, quantity: 1, unitPrice: null, reason: '' }); }
  removeLine(i: number): void { this.form.lines.splice(i, 1); }

  submit(): void {
    this.saving.set(true);
    this.api.create(this.form).subscribe({
      next: () => this.router.navigate(['/returns']),
      error: () => this.saving.set(false)
    });
  }
}
