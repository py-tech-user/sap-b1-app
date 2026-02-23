import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-order-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="order-list">
      <div class="header">
        <h1>Commandes</h1>
        <a routerLink="/orders/new" class="btn-primary">+ Nouvelle commande</a>
      </div>

      <table>
        <thead>
          <tr>
            <th>N° Doc</th>
            <th>Client</th>
            <th>Date</th>
            <th>Total</th>
            <th>Statut</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (order of orders(); track order.id) {
            <tr>
              <td>{{ order.docNum }}</td>
              <td>{{ order.customerName }}</td>
              <td>{{ order.docDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ order.docTotal | currency:'EUR' }}</td>
              <td>
                <span [class]="'badge ' + order.status.toLowerCase()">{{ order.status }}</span>
              </td>
              <td>
                <a [routerLink]="['/orders', order.id]" class="btn-sm">Voir</a>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .btn-primary { background: #667eea; color: white; padding: 0.75rem 1.5rem; border-radius: 4px; text-decoration: none; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; }
    th, td { padding: 1rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f8f9fa; font-weight: 600; }
    .badge { padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.8rem; }
    .badge.draft { background: #ffeaa7; color: #6c5ce7; }
    .badge.pending { background: #fdcb6e; color: #d35400; }
    .badge.confirmed { background: #81ecec; color: #00838f; }
    .badge.delivered { background: #d4edda; color: #155724; }
    .badge.cancelled { background: #f8d7da; color: #721c24; }
    .btn-sm { padding: 0.25rem 0.5rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
  `]
})
export class OrderListComponent implements OnInit {
  orders = signal<any[]>([]);

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<any>(`${environment.apiUrl}/orders`).subscribe({
      next: (res) => {
        const payload = res.data ?? res;
        this.orders.set(payload.items ?? payload);
      },
      error: (err) => console.error('Erreur:', err)
    });
  }
}
