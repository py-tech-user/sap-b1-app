import { CommonModule, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialDashboardApiService } from '../../../core/services/commercial-dashboard-api.service';
import { CommercialDashboard } from '../../../core/models/models';

@Component({
  selector: 'app-commercial-dashboard',
  imports: [CommonModule, RouterLink, DecimalPipe],
  template: `
    <div class="page">
      <h1>📌 Dashboard commercial</h1>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else if (error()) {
        <div class="error">{{ error() }}</div>
      } @else if (data()) {
        <div class="grid">
          <a class="card" routerLink="/quotes">
            <div class="value">{{ data()!.pendingQuotes }}</div>
            <div class="label">Devis en attente</div>
          </a>
          <a class="card" routerLink="/orders">
            <div class="value">{{ data()!.ordersInPreparation }}</div>
            <div class="label">BC en préparation</div>
          </a>
          <a class="card" routerLink="/deliverynotes">
            <div class="value">{{ data()!.deliveryInProgress }}</div>
            <div class="label">BL en cours</div>
          </a>
          <a class="card" routerLink="/invoices">
            <div class="value">{{ data()!.unpaidInvoices }}</div>
            <div class="label">Factures impayées</div>
          </a>
          <a class="card" routerLink="/returns">
            <div class="value">{{ data()!.pendingReturns }}</div>
            <div class="label">Retours à traiter</div>
          </a>
          <a class="card" routerLink="/creditnotes">
            <div class="value">{{ data()!.totalCreditNotes }}</div>
            <div class="label">Avoirs</div>
          </a>
        </div>

        <div class="amounts card">
          <h3>Montants agrégés</h3>
          <div class="amount-grid">
            <div>Devis: <strong>{{ data()!.amounts?.quotes ?? 0 | number:'1.2-2' }}</strong></div>
            <div>BC: <strong>{{ data()!.amounts?.orders ?? 0 | number:'1.2-2' }}</strong></div>
            <div>BL: <strong>{{ data()!.amounts?.deliveryNotes ?? 0 | number:'1.2-2' }}</strong></div>
            <div>Factures: <strong>{{ data()!.amounts?.invoices ?? 0 | number:'1.2-2' }}</strong></div>
            <div>Avoirs: <strong>{{ data()!.amounts?.creditNotes ?? 0 | number:'1.2-2' }}</strong></div>
            <div>Retours: <strong>{{ data()!.amounts?.returns ?? 0 | number:'1.2-2' }}</strong></div>
            <div>Impayé: <strong>{{ data()!.amounts?.unpaidInvoices ?? 0 | number:'1.2-2' }}</strong></div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0.75rem; }
    .card {
      background: #fff;
      border-radius: 10px;
      padding: 1rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.08);
      text-decoration: none;
      color: inherit;
    }
    .value { font-size: 1.7rem; font-weight: 700; }
    .label { color: #666; }
    .amount-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.5rem; }
    .error { color: #b00020; }
    @media (max-width: 1024px) {
      .grid { grid-template-columns: 1fr 1fr; }
      .amount-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class CommercialDashboardComponent {
  private readonly api = inject(CommercialDashboardApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly data = signal<CommercialDashboard | null>(null);

  constructor() {
    this.api.getDashboard()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.data.set(res.data ?? null);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Impossible de charger le dashboard commercial.');
          this.loading.set(false);
        }
      });
  }
}
