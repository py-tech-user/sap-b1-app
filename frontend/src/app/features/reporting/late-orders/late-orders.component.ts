import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ReportingApiService } from '../../../core/services/reporting-api.service';
import { LateOrder } from '../../../core/models/models';

@Component({
  selector: 'app-late-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="late-orders-page">
      <div class="header">
        <div>
          <a routerLink="/reporting" class="back-link">← Retour au reporting</a>
          <h1>⏰ Commandes en retard</h1>
        </div>
        <div class="filter">
          <label>Seuil (jours) :</label>
          <select [(ngModel)]="threshold" (ngModelChange)="load()">
            <option [ngValue]="3">3 jours</option>
            <option [ngValue]="7">7 jours</option>
            <option [ngValue]="14">14 jours</option>
            <option [ngValue]="30">30 jours</option>
          </select>
        </div>
      </div>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else {
        <!-- Summary -->
        <div class="summary-row">
          <div class="summary-card danger">
            <div class="summary-value">{{ data().length }}</div>
            <div class="summary-label">Commandes en retard</div>
          </div>
          <div class="summary-card">
            <div class="summary-value">{{ totalAmount() | number:'1.2-2' }} MAD</div>
            <div class="summary-label">Montant total</div>
          </div>
          <div class="summary-card warning">
            <div class="summary-value">{{ avgDaysLate() | number:'1.0-0' }}</div>
            <div class="summary-label">Jours de retard moyen</div>
          </div>
        </div>

        <!-- Table -->
        <div class="table-card">
          <table>
            <thead>
              <tr>
                <th>Commande</th>
                <th>Client</th>
                <th>Montant</th>
                <th>Date cmd</th>
                <th>Date prévue</th>
                <th>Retard</th>
                <th>Statut</th>
              </tr>
            </thead>
            <tbody>
              @for (o of data(); track o.orderId) {
                <tr [class.critical]="o.daysLate > 14">
                  <td><strong>{{ o.docNum }}</strong></td>
                  <td>{{ o.customerName }}</td>
                  <td class="num">{{ o.total | number:'1.2-2' }} MAD</td>
                  <td>{{ o.orderDate | date:'dd/MM/yyyy' }}</td>
                  <td>{{ o.expectedDate | date:'dd/MM/yyyy' }}</td>
                  <td>
                    <span class="badge"
                          [class.badge-danger]="o.daysLate > 14"
                          [class.badge-warning]="o.daysLate > 7 && o.daysLate <= 14"
                          [class.badge-ok]="o.daysLate <= 7">
                      {{ o.daysLate }}j
                    </span>
                  </td>
                  <td>
                    <span class="status-badge">{{ o.status }}</span>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="empty">Aucune commande en retard 🎉</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .late-orders-page { display: flex; flex-direction: column; gap: 1.25rem; max-width: 1100px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 1rem; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0.25rem 0 0; }
    .back-link { color: #667eea; text-decoration: none; font-size: 0.9rem; }
    .back-link:hover { text-decoration: underline; }

    .filter { display: flex; align-items: center; gap: 0.5rem; }
    .filter label { font-size: 0.85rem; color: #555; font-weight: 500; }
    .filter select { padding: 0.4rem 0.6rem; border: 1px solid #ddd; border-radius: 6px; font-size: 0.85rem; }

    .loading { text-align: center; padding: 3rem; color: #999; }

    .summary-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
    .summary-card {
      background: white; border-radius: 12px; padding: 1.25rem; text-align: center;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06); border-left: 4px solid #667eea;
    }
    .summary-card.danger { border-left-color: #e74c3c; }
    .summary-card.warning { border-left-color: #f39c12; }
    .summary-value { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; }
    .summary-label { font-size: 0.85rem; color: #888; margin-top: 4px; }

    .table-card {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06); overflow-x: auto;
    }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.75rem 0.75rem; text-align: left; border-bottom: 1px solid #eee; font-size: 0.9rem; }
    th { background: #f8f9fa; font-weight: 600; font-size: 0.82rem; color: #555; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .empty { text-align: center; color: #999; padding: 2rem; }
    tr.critical { background: #fff5f5; }

    .badge { padding: 0.2rem 0.5rem; border-radius: 12px; font-size: 0.78rem; font-weight: 600; }
    .badge-danger { background: #fce4ec; color: #c62828; }
    .badge-warning { background: #fff3e0; color: #e65100; }
    .badge-ok { background: #e8f5e9; color: #2e7d32; }
    .status-badge { padding: 0.2rem 0.5rem; background: #e3f2fd; color: #1565c0; border-radius: 12px; font-size: 0.78rem; }
  `]
})
export class LateOrdersComponent implements OnInit {
  private reportingApi = inject(ReportingApiService);

  data = signal<LateOrder[]>([]);
  loading = signal(true);
  totalAmount = signal(0);
  avgDaysLate = signal(0);
  threshold = 7;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.reportingApi.getLateOrders(this.threshold).subscribe({
      next: (res) => {
        const items = res.data ?? [];
        this.data.set(items);
        this.totalAmount.set(items.reduce((s, o) => s + o.total, 0));
        this.avgDaysLate.set(items.length > 0 ? items.reduce((s, o) => s + o.daysLate, 0) / items.length : 0);
        this.loading.set(false);
      },
      error: () => { this.data.set([]); this.loading.set(false); }
    });
  }
}
