import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="order-detail">
      @if (order) {
        <div class="header">
          <h1>Commande {{ order.docNum }}</h1>
          <a routerLink="/orders" class="btn-back">Retour</a>
        </div>
        
        <div class="info-grid">
          <div class="info-card">
            <label>Client</label>
            <span>{{ order.customerName }}</span>
          </div>
          <div class="info-card">
            <label>Date</label>
            <span>{{ order.docDate | date:'dd/MM/yyyy' }}</span>
          </div>
          <div class="info-card">
            <label>Statut</label>
            <span [class]="'status ' + order.status.toLowerCase()">{{ order.status }}</span>
          </div>
          <div class="info-card">
            <label>Total</label>
            <span class="total">{{ order.docTotal | currency:'EUR' }}</span>
          </div>
        </div>
        
        <h3>Lignes</h3>
        <table>
          <thead>
            <tr>
              <th>Produit</th>
              <th>Quantité</th>
              <th>Prix unitaire</th>
              <th>Total ligne</th>
            </tr>
          </thead>
          <tbody>
            @for (line of order.lines; track line.id) {
              <tr>
                <td>{{ line.productName }}</td>
                <td>{{ line.quantity }}</td>
                <td>{{ line.unitPrice | currency:'EUR' }}</td>
                <td>{{ line.lineTotal | currency:'EUR' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .order-detail { background: white; padding: 2rem; border-radius: 8px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .btn-back { padding: 0.5rem 1rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1.5rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 4px; }
    .info-card label { display: block; font-size: 0.85rem; color: #666; margin-bottom: 0.25rem; }
    .info-card span { font-size: 1.1rem; color: #333; }
    .total { font-weight: bold; color: #667eea; }
    h3 { margin-bottom: 1rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f8f9fa; }
    .status { padding: 0.25rem 0.5rem; border-radius: 4px; }
    .status.pending { background: #fdcb6e; color: #d35400; }
    .status.confirmed { background: #81ecec; color: #00838f; }
    .status.delivered { background: #d4edda; color: #155724; }
  `]
})
export class OrderDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  order: any;

  constructor(private http: HttpClient, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    this.http.get<any>(`${environment.apiUrl}/orders/${id}`).subscribe({
      next: (res) => { this.order = res.data ?? res; this.cdr.markForCheck(); }
    });
  }
}
