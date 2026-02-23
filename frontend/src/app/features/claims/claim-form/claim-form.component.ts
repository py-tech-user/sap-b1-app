import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ClaimApiService } from '../../../core/services/claim-api.service';

@Component({
  selector: 'app-claim-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <h1>📋 Nouvelle réclamation</h1>
        <a routerLink="/claims" class="btn-back">Annuler</a>
      </div>

      <form (ngSubmit)="submit()" class="form">
        <div class="row">
          <div class="form-group">
            <label>Client (ID)</label>
            <input type="number" [(ngModel)]="form.customerId" name="customerId" required />
          </div>
          <div class="form-group">
            <label>Commande (ID, optionnel)</label>
            <input type="number" [(ngModel)]="form.orderId" name="orderId" />
          </div>
        </div>
        <div class="row">
          <div class="form-group">
            <label>Type</label>
            <select [(ngModel)]="form.type" name="type" required>
              <option value="">Sélectionner...</option>
              <option value="Product">Produit</option>
              <option value="Service">Service</option>
              <option value="Delivery">Livraison</option>
              <option value="Billing">Facturation</option>
              <option value="Other">Autre</option>
            </select>
          </div>
          <div class="form-group">
            <label>Priorité</label>
            <select [(ngModel)]="form.priority" name="priority" required>
              <option value="Low">Basse</option>
              <option value="Medium">Moyenne</option>
              <option value="High">Haute</option>
              <option value="Critical">Critique</option>
            </select>
          </div>
        </div>
        <div class="form-group">
          <label>Sujet</label>
          <input type="text" [(ngModel)]="form.subject" name="subject" required placeholder="Résumé de la réclamation" />
        </div>
        <div class="form-group">
          <label>Description</label>
          <textarea [(ngModel)]="form.description" name="description" rows="5" required placeholder="Détails de la réclamation..."></textarea>
        </div>

        <div class="form-actions">
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : 'Créer la réclamation' }}</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .page { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .form { max-width: 700px; }
    .row { display: flex; gap: 1rem; }
    .row .form-group { flex: 1; }
    .form-group { margin-bottom: 1.2rem; }
    .form-group label { display: block; font-weight: 600; margin-bottom: .4rem; color: #555; font-size: .9rem; }
    .form-group input, .form-group select, .form-group textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; }
    .form-actions { margin-top: 2rem; }
    .btn-primary { background: #667eea; color: white; border: none; padding: .7rem 1.5rem; border-radius: 6px; cursor: pointer; font-weight: 600; font-size: 1rem; }
    .btn-primary:disabled { opacity: .5; }
  `]
})
export class ClaimFormComponent {
  saving = signal(false);
  form: any = { customerId: null, orderId: null, type: '', priority: 'Medium', subject: '', description: '' };

  constructor(private api: ClaimApiService, private router: Router) {}

  submit(): void {
    this.saving.set(true);
    this.api.create(this.form).subscribe({
      next: () => this.router.navigate(['/claims']),
      error: () => this.saving.set(false)
    });
  }
}
