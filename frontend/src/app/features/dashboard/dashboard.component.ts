import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { forkJoin, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';

interface DashboardStats {
  totalCustomers: number;
  totalOrders: number;
  totalProducts: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="dashboard">
      <h1>📊 Tableau de bord</h1>

      @if (loading()) {
        <div class="loading">Chargement des statistiques...</div>
      }

      <div class="stats-grid">
        <a routerLink="/customers" class="stat-card">
          <div class="stat-icon">👥</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalCustomers }}</span>
            <span class="stat-label">Partenaires</span>
          </div>
        </a>

        <a routerLink="/orders" class="stat-card">
          <div class="stat-icon">🛒</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalOrders }}</span>
            <span class="stat-label">Commandes</span>
          </div>
        </a>

        <a routerLink="/products" class="stat-card">
          <div class="stat-icon">🏷️</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalProducts }}</span>
            <span class="stat-label">Catalogue</span>
          </div>
        </a>

        <a routerLink="/reporting" class="stat-card">
          <div class="stat-icon">📈</div>
          <div class="stat-info">
            <span class="stat-value">↗</span>
            <span class="stat-label">Reporting</span>
          </div>
        </a>

      </div>
    </div>
  `,
  styles: [`
    .dashboard h1 { margin-bottom: 2rem; color: #333; }
    .loading { text-align: center; padding: 1rem; color: #888; }
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1.5rem;
    }
    .stat-card {
      background: white;
      padding: 1.5rem;
      border-radius: 8px;
      box-shadow: 0 2px 10px rgba(0,0,0,0.08);
      display: flex;
      align-items: center;
      gap: 1rem;
      text-decoration: none;
      color: inherit;
      transition: transform 0.15s, box-shadow 0.15s;
      cursor: pointer;
    }
    .stat-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 16px rgba(0,0,0,0.12);
    }
    .stat-icon { font-size: 2.5rem; }
    .stat-info { display: flex; flex-direction: column; }
    .stat-value { font-size: 1.75rem; font-weight: bold; color: #333; }
    .stat-label { color: #666; font-size: 0.9rem; }
  `]
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);
  private api  = environment.apiUrl;

  stats = signal<DashboardStats>({
    totalCustomers: 0, totalOrders: 0, totalProducts: 0
  });
  loading = signal(true);

  ngOnInit(): void {
    forkJoin({
      customers: this.http.get<any>(`${this.api}/sap/partners`).pipe(catchError(() => of(null))),
      orders:    this.http.get<any>(`${this.api}/sap/orders`).pipe(catchError(() => of(null))),
      products:  this.http.get<any>(`${this.api}/products?page=1&pageSize=1`).pipe(catchError(() => of(null)))
    }).subscribe({
      next: (res) => {
        const extract = (r: any) => {
          if (!r) return 0;
          const payload = r.data ?? r;
          if (typeof payload.totalCount === 'number') return payload.totalCount;
          if (typeof payload.totalItems === 'number') return payload.totalItems;
          if (Array.isArray(payload.items)) return payload.items.length;
          if (Array.isArray(payload.value)) return payload.value.length;
          if (Array.isArray(payload.data?.items)) return payload.data.items.length;
          if (Array.isArray(payload.data?.value)) return payload.data.value.length;
          if (Array.isArray(payload.data)) return payload.data.length;
          if (Array.isArray(payload)) return payload.length;
          return 0;
        };
        this.stats.set({
          totalCustomers: extract(res.customers),
          totalOrders:    extract(res.orders),
          totalProducts:  extract(res.products)
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
