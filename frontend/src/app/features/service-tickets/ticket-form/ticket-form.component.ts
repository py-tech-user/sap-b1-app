import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ServiceTicketApiService } from '../../../core/services/service-ticket-api.service';

@Component({
  selector: 'app-ticket-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <h1>🔧 Nouveau ticket SAV</h1>
        <a routerLink="/service-tickets" class="btn-back">Annuler</a>
      </div>

      <form (ngSubmit)="submit()" class="form">
        <div class="row">
          <div class="form-group">
            <label>Client (ID)</label>
            <input type="number" [(ngModel)]="form.customerId" name="customerId" required />
          </div>
          <div class="form-group">
            <label>Produit (ID, optionnel)</label>
            <input type="number" [(ngModel)]="form.productId" name="productId" />
          </div>
        </div>
        <div class="form-group">
          <label>Commande (ID, optionnel)</label>
          <input type="number" [(ngModel)]="form.orderId" name="orderId" />
        </div>
        <div class="form-group">
          <label>Description du problème</label>
          <textarea [(ngModel)]="form.description" name="description" rows="5" required placeholder="Décrivez le problème en détail..."></textarea>
        </div>

        <div class="form-actions">
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : 'Créer le ticket' }}</button>
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
    .form-group input, .form-group textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; }
    .form-actions { margin-top: 2rem; }
    .btn-primary { background: #667eea; color: white; border: none; padding: .7rem 1.5rem; border-radius: 6px; cursor: pointer; font-weight: 600; font-size: 1rem; }
    .btn-primary:disabled { opacity: .5; }
  `]
})
export class TicketFormComponent {
  saving = signal(false);
  form: any = { customerId: null, productId: null, orderId: null, description: '' };

  constructor(private api: ServiceTicketApiService, private router: Router) {}

  submit(): void {
    this.saving.set(true);
    this.api.create(this.form).subscribe({
      next: () => this.router.navigate(['/service-tickets']),
      error: () => this.saving.set(false)
    });
  }
}
