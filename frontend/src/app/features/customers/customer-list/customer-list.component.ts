import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface Customer {
  id: number;
  cardCode: string;
  cardName: string;
  email: string;
  phone: string;
  isActive: boolean;
}

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="customer-list">
      <div class="header">
        <h1>Clients</h1>
        <a routerLink="/customers/new" class="btn-primary">+ Nouveau client</a>
      </div>

      <table>
        <thead>
          <tr>
            <th>Code</th>
            <th>Nom</th>
            <th>Email</th>
            <th>Téléphone</th>
            <th>Statut</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (customer of customers(); track customer.id) {
            <tr>
              <td>{{ customer.cardCode }}</td>
              <td>{{ customer.cardName }}</td>
              <td>{{ customer.email }}</td>
              <td>{{ customer.phone }}</td>
              <td>
                <span [class]="customer.isActive ? 'badge active' : 'badge inactive'">
                  {{ customer.isActive ? 'Actif' : 'Inactif' }}
                </span>
              </td>
              <td>
                <a [routerLink]="['/customers', customer.id]" class="btn-sm">Voir</a>
                <a [routerLink]="['/customers', customer.id, 'edit']" class="btn-sm">Modifier</a>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .btn-primary {
      background: #667eea; color: white; padding: 0.75rem 1.5rem;
      border-radius: 4px; text-decoration: none;
    }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; }
    th, td { padding: 1rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f8f9fa; font-weight: 600; }
    .badge { padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.8rem; }
    .badge.active { background: #d4edda; color: #155724; }
    .badge.inactive { background: #f8d7da; color: #721c24; }
    .btn-sm { padding: 0.25rem 0.5rem; margin-right: 0.5rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; font-size: 0.85rem; }
  `]
})
export class CustomerListComponent implements OnInit {
  customers = signal<Customer[]>([]);

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<any>(`${environment.apiUrl}/customers`).subscribe({
      next: (res) => {
        const payload = res.data ?? res;
        this.customers.set(payload.items ?? payload);
      },
      error: (err) => console.error('Erreur:', err)
    });
  }
}
