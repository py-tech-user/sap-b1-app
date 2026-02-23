import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="customer-form">
      <h1>{{ isEdit ? 'Modifier le client' : 'Nouveau client' }}</h1>

      @if (successMsg) {
        <div class="alert alert-success">✅ {{ successMsg }}</div>
      }
      @if (errorMsg) {
        <div class="alert alert-error">❌ {{ errorMsg }}</div>
      }

      <form (ngSubmit)="onSubmit()">
        <div class="form-row">
          <div class="form-group">
            <label>Code client</label>
            <input [(ngModel)]="customer.cardCode" name="cardCode" required [disabled]="isEdit" />
          </div>
          <div class="form-group">
            <label>Nom</label>
            <input [(ngModel)]="customer.cardName" name="cardName" required />
          </div>
        </div>
        
        <div class="form-row">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="customer.email" name="email" />
          </div>
          <div class="form-group">
            <label>Téléphone</label>
            <input [(ngModel)]="customer.phone" name="phone" />
          </div>
        </div>
        
        <div class="form-group">
          <label>Adresse</label>
          <textarea [(ngModel)]="customer.address" name="address" rows="3"></textarea>
        </div>
        
        <div class="form-group">
          <label>
            <input type="checkbox" [(ngModel)]="customer.isActive" name="isActive" />
            Client actif
          </label>
        </div>
        
        <div class="actions">
          <a routerLink="/customers" class="btn-cancel">Annuler</a>
          <button type="submit" class="btn-submit" [disabled]="loading">
            {{ loading ? 'Enregistrement...' : 'Enregistrer' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .customer-form { max-width: 800px; background: white; padding: 2rem; border-radius: 8px; }
    h1 { margin-bottom: 1rem; }
    .alert { padding: 0.75rem 1rem; border-radius: 6px; margin-bottom: 1rem; font-size: 0.9rem; }
    .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
    .alert-error   { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form-group { margin-bottom: 1rem; }
    label { display: block; margin-bottom: 0.5rem; color: #555; }
    input, textarea { width: 100%; padding: 0.75rem; border: 1px solid #ddd; border-radius: 4px; box-sizing: border-box; }
    input:focus, textarea:focus { outline: none; border-color: #667eea; }
    .actions { display: flex; gap: 1rem; margin-top: 2rem; }
    .btn-cancel { padding: 0.75rem 1.5rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
    .btn-submit { padding: 0.75rem 1.5rem; background: #667eea; color: white; border: none; border-radius: 4px; cursor: pointer; }
    .btn-submit:disabled { opacity: 0.7; }
  `]
})
export class CustomerFormComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  customer: any = { cardCode: '', cardName: '', email: '', phone: '', address: '', isActive: true };
  isEdit = false;
  loading = false;
  successMsg = '';
  errorMsg = '';

  constructor(private http: HttpClient, private router: Router, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    if (id) {
      this.isEdit = true;
      this.http.get<any>(`${environment.apiUrl}/customers/${id}`).subscribe({
        next: (res) => {
          this.customer = res.data ?? res;
          this.cdr.markForCheck();
        },
        error: () => { this.errorMsg = 'Impossible de charger le client.'; this.cdr.markForCheck(); }
      });
    }
  }

  onSubmit(): void {
    this.loading = true;
    this.successMsg = '';
    this.errorMsg = '';

    const req = this.isEdit
      ? this.http.put<any>(`${environment.apiUrl}/customers/${this.customer.id}`, this.customer)
      : this.http.post<any>(`${environment.apiUrl}/customers`, this.customer);

    req.subscribe({
      next: (res) => {
        if (res.success === false) {
          this.errorMsg = res.message || 'Erreur lors de l\'opération.';
          this.loading = false;
          this.cdr.markForCheck();
          return;
        }
        this.successMsg = res.message
          || (this.isEdit ? 'Client modifié avec succès.' : 'Client créé avec succès.');
        this.loading = false;
        this.cdr.markForCheck();
        setTimeout(() => this.router.navigate(['/customers']), 1200);
      },
      error: (err) => {
        console.error('Erreur client:', err);
        if (err.status === 0) {
          this.errorMsg = 'Impossible de contacter le serveur. Vérifiez que le backend est démarré.';
        } else {
          this.errorMsg = err.error?.message || `Erreur ${err.status} lors de ${this.isEdit ? 'la modification' : 'la création'}.`;
        }
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
