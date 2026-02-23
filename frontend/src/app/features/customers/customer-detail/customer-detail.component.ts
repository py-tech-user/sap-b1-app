import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="customer-detail">
      @if (customer) {
        <div class="header">
          <h1>{{ customer.cardName }}</h1>
          <div class="actions">
            <a [routerLink]="['/customers', customer.id, 'edit']" class="btn-edit">Modifier</a>
            <a routerLink="/customers" class="btn-back">Retour</a>
          </div>
        </div>
        
        <div class="info-grid">
          <div class="info-card">
            <label>Code client</label>
            <span>{{ customer.cardCode }}</span>
          </div>
          <div class="info-card">
            <label>Email</label>
            <span>{{ customer.email || '-' }}</span>
          </div>
          <div class="info-card">
            <label>Téléphone</label>
            <span>{{ customer.phone || '-' }}</span>
          </div>
          <div class="info-card">
            <label>Statut</label>
            <span [class]="customer.isActive ? 'status active' : 'status inactive'">
              {{ customer.isActive ? 'Actif' : 'Inactif' }}
            </span>
          </div>
        </div>
        
        <div class="address-section">
          <label>Adresse</label>
          <p>{{ customer.address || 'Non renseignée' }}</p>
        </div>
      }
    </div>
  `,
  styles: [`
    .customer-detail { background: white; padding: 2rem; border-radius: 8px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .actions { display: flex; gap: 0.5rem; }
    .btn-edit { padding: 0.5rem 1rem; background: #667eea; color: white; border-radius: 4px; text-decoration: none; }
    .btn-back { padding: 0.5rem 1rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1.5rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 4px; }
    .info-card label { display: block; font-size: 0.85rem; color: #666; margin-bottom: 0.25rem; }
    .info-card span { font-size: 1.1rem; color: #333; }
    .status { padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.9rem; }
    .status.active { background: #d4edda; color: #155724; }
    .status.inactive { background: #f8d7da; color: #721c24; }
    .address-section label { display: block; color: #666; margin-bottom: 0.5rem; }
  `]
})
export class CustomerDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  customer: any;

  constructor(private http: HttpClient, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    this.http.get<any>(`${environment.apiUrl}/customers/${id}`).subscribe({
      next: (res) => { this.customer = res.data ?? res; this.cdr.markForCheck(); }
    });
  }
}
