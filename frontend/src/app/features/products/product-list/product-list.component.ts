import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="product-list">
      <div class="header">
        <h1>Produits</h1>
        <a routerLink="/products/new" class="btn-primary">+ Nouveau produit</a>
      </div>

      <table>
        <thead>
          <tr>
            <th>Code</th>
            <th>Nom</th>
            <th>Catégorie</th>
            <th>Prix</th>
            <th>Stock</th>
            <th>Statut</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (product of products(); track product.id) {
            <tr>
              <td>{{ product.itemCode }}</td>
              <td>{{ product.itemName }}</td>
              <td>{{ product.category || '-' }}</td>
              <td>{{ product.price | currency:'EUR' }}</td>
              <td [class]="product.stock < 10 ? 'low-stock' : ''">{{ product.stock }}</td>
              <td>
                <span [class]="product.isActive ? 'badge active' : 'badge inactive'">
                  {{ product.isActive ? 'Actif' : 'Inactif' }}
                </span>
              </td>
              <td>
                <a [routerLink]="['/products', product.id, 'edit']" class="btn-sm">Modifier</a>
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
    .badge.active { background: #d4edda; color: #155724; }
    .badge.inactive { background: #f8d7da; color: #721c24; }
    .low-stock { color: #dc3545; font-weight: bold; }
    .btn-sm { padding: 0.25rem 0.5rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
  `]
})
export class ProductListComponent implements OnInit {
  products = signal<any[]>([]);

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http.get<any>(`${environment.apiUrl}/products`).subscribe({
      next: (res) => {
        const payload = res.data ?? res;
        this.products.set(payload.items ?? payload);
      },
      error: (err) => console.error('Erreur:', err)
    });
  }
}
