import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ReportingApiService } from '../../../core/services/reporting-api.service';
import { PendingPayment } from '../../../core/models/models';

@Component({
  selector: 'app-pending-payments',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="pending-payments-page">
      <div class="header">
        <div>
          <a routerLink="/reporting" class="back-link">← Retour au reporting</a>
          <h1>💳 Paiements en attente</h1>
        </div>
      </div>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else {
        <!-- Summary -->
        <div class="summary-row">
          <div class="summary-card danger">
            <div class="summary-value">{{ totalRemaining() | number:'1.2-2' }} MAD</div>
            <div class="summary-label">Montant total en attente</div>
          </div>
          <div class="summary-card">
            <div class="summary-value">{{ data().length }}</div>
            <div class="summary-label">Commandes concernées</div>
          </div>
          <div class="summary-card warning">
            <div class="summary-value">{{ criticalCount() }}</div>
            <div class="summary-label">En retard &gt; 30 jours</div>
          </div>
        </div>

        <!-- Table -->
        <div class="table-card">
          <table>
            <colgroup>
              <col class="col-order" />
              <col class="col-customer" />
              <col class="col-money" />
              <col class="col-money" />
              <col class="col-money" />
              <col class="col-delay" />
            </colgroup>
            <thead>
              <tr>
                <th>Commande</th>
                <th>Client</th>
                <th class="num">Total cmd</th>
                <th class="num">Payé</th>
                <th class="num">Reste</th>
                <th>Retard</th>
              </tr>
            </thead>
            <tbody>
              @for (p of data(); track p.orderId) {
                <tr [class.critical]="p.daysOverdue > 30">
                  <td><strong>{{ p.docNum }}</strong></td>
                  <td>{{ p.customerName }}</td>
                  <td class="num">{{ p.orderTotal | number:'1.2-2' }}</td>
                  <td class="num paid">{{ p.paidAmount | number:'1.2-2' }}</td>
                  <td class="num remaining">{{ p.remainingAmount | number:'1.2-2' }}</td>
                  <td>
                    <span class="badge" [class.badge-danger]="p.daysOverdue > 30"
                          [class.badge-warning]="p.daysOverdue > 7 && p.daysOverdue <= 30"
                          [class.badge-ok]="p.daysOverdue <= 7">
                      {{ p.daysOverdue }}j
                    </span>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="6" class="empty">Aucun paiement en attente 🎉</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .pending-payments-page { display: flex; flex-direction: column; gap: 1.25rem; max-width: 1100px; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0.25rem 0 0; }
    .back-link { color: #667eea; text-decoration: none; font-size: 0.9rem; }
    .back-link:hover { text-decoration: underline; }
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
    table { width: 100%; border-collapse: collapse; table-layout: fixed; }
    .col-order { width: 14%; }
    .col-customer { width: 26%; }
    .col-money { width: 15%; }
    .col-delay { width: 15%; }
    th, td { padding: 0.75rem 0.75rem; text-align: left; border-bottom: 1px solid #eee; font-size: 0.9rem; }
    th { background: #f8f9fa; font-weight: 600; font-size: 0.82rem; color: #555; }
    th.num, td.num { text-align: right; font-variant-numeric: tabular-nums; }
    .paid { color: #27ae60; }
    .remaining { color: #e74c3c; font-weight: 600; }
    .empty { text-align: center; color: #999; padding: 2rem; }
    tr.critical { background: #fff5f5; }

    .badge {
      padding: 0.2rem 0.5rem; border-radius: 12px; font-size: 0.78rem; font-weight: 600;
    }
    .badge-danger { background: #fce4ec; color: #c62828; }
    .badge-warning { background: #fff3e0; color: #e65100; }
    .badge-ok { background: #e8f5e9; color: #2e7d32; }
  `]
})
export class PendingPaymentsComponent implements OnInit {
  private reportingApi = inject(ReportingApiService);

  data = signal<PendingPayment[]>([]);
  loading = signal(true);
  totalRemaining = signal(0);
  criticalCount = signal(0);

  ngOnInit(): void {
    this.reportingApi.getPendingPayments().subscribe({
      next: (res) => {
        const items = res.data ?? [];
        this.data.set(items);
        this.totalRemaining.set(items.reduce((s, p) => s + p.remainingAmount, 0));
        this.criticalCount.set(items.filter(p => p.daysOverdue > 30).length);
        this.loading.set(false);
      },
      error: () => { this.data.set([]); this.loading.set(false); }
    });
  }
}
