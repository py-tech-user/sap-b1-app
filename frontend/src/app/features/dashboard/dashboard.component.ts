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
  pendingOrders: number;
  totalVisits: number;
  totalPayments: number;
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
            <span class="stat-label">Clients</span>
          </div>
        </a>

        <a routerLink="/orders" class="stat-card">
          <div class="stat-icon">📦</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalOrders }}</span>
            <span class="stat-label">Commandes</span>
          </div>
        </a>

        <a routerLink="/products" class="stat-card">
          <div class="stat-icon">🏷️</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalProducts }}</span>
            <span class="stat-label">Produits</span>
          </div>
        </a>

        <a routerLink="/orders" class="stat-card pending">
          <div class="stat-icon">⏳</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().pendingOrders }}</span>
            <span class="stat-label">Cmd. en attente</span>
          </div>
        </a>

        <a routerLink="/visits" class="stat-card visits">
          <div class="stat-icon">📋</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalVisits }}</span>
            <span class="stat-label">Visites</span>
          </div>
        </a>

        <a routerLink="/payments" class="stat-card payments">
          <div class="stat-icon">💰</div>
          <div class="stat-info">
            <span class="stat-value">{{ stats().totalPayments }}</span>
            <span class="stat-label">Encaissements</span>
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
    .stat-card.pending   { border-left: 4px solid #f39c12; }
    .stat-card.visits    { border-left: 4px solid #3498db; }
    .stat-card.payments  { border-left: 4px solid #2ecc71; }
  `]
})
export class DashboardComponent implements OnInit {
  private http = inject(HttpClient);
  private api  = environment.apiUrl;

  stats = signal<DashboardStats>({
    totalCustomers: 0, totalOrders: 0, totalProducts: 0,
    pendingOrders: 0, totalVisits: 0, totalPayments: 0
  });
  loading = signal(true);

  ngOnInit(): void {
    forkJoin({
      customers: this.http.get<any>(`${this.api}/customers?page=1&pageSize=1`).pipe(catchError(() => of(null))),
      orders:    this.http.get<any>(`${this.api}/orders?page=1&pageSize=1`).pipe(catchError(() => of(null))),
      products:  this.http.get<any>(`${this.api}/products?page=1&pageSize=1`).pipe(catchError(() => of(null))),
      visits:    this.http.get<any>(`${this.api}/visits?page=1&pageSize=1`).pipe(catchError(() => of(null))),
      payments:  this.http.get<any>(`${this.api}/payments?page=1&pageSize=1`).pipe(catchError(() => of(null)))
    }).subscribe({
      next: (res) => {
        const extract = (r: any) => {
          if (!r) return 0;
          const payload = r.data ?? r;
          return payload.totalCount ?? payload.totalItems ?? payload.items?.length ?? 0;
        };
        this.stats.set({
          totalCustomers: extract(res.customers),
          totalOrders:    extract(res.orders),
          totalProducts:  extract(res.products),
          pendingOrders:  0,
          totalVisits:    extract(res.visits),
          totalPayments:  extract(res.payments)
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
