import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ReportingApiService, ReportingSalesPerson } from '../../core/services/reporting-api.service';

@Component({
  selector: 'app-dashboard-commercials-performance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <a routerLink="/dashboard" class="back">Retour dashboard</a>
      <h1>Performance des Commerciaux</h1>

      <div class="filters">
        <label>Mois
          <input type="month" [(ngModel)]="month" (change)="load()" />
        </label>
        <label>Commercial
          <input list="sp-suggestions" [(ngModel)]="search" (input)="noop()" placeholder="Nom ou code" />
          <datalist id="sp-suggestions">
            @for (s of suggestions(); track s) {
              <option [value]="s"></option>
            }
          </datalist>
        </label>
      </div>

      @if (loading()) {
        <p>Chargement...</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Commercial</th>
              <th>Devis</th>
              <th>Taux Conv Devis-BC</th>
              <th>Taux Conv BC-BL</th>
              <th>Taux Conv BL-Facture</th>
              <th>CA Realise</th>
              <th>CA Attente</th>
            </tr>
          </thead>
          <tbody>
            @for (r of filteredRows(); track r.salesPersonCode) {
              <tr>
                <td data-label="Commercial">{{ r.salesPersonName || ('#' + r.salesPersonCode) }}</td>
                <td data-label="Devis">{{ r.quotesCount }}</td>
                <td data-label="Taux Devis-BC">{{ pct(r.quoteToOrderRate) }}</td>
                <td data-label="Taux BC-BL">{{ pct(r.orderToDeliveryRate) }}</td>
                <td data-label="Taux BL-Facture">{{ pct(r.deliveryToInvoiceRate) }}</td>
                <td data-label="CA Realise">{{ money(r.netRevenue || (r.invoicesAmount - r.creditNotesAmount)) }}</td>
                <td data-label="CA Attente">{{ money(r.pendingRevenue) }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7">Aucun commercial pour ce filtre.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .page { display: grid; gap: 1rem; }
    .back { text-decoration: none; color: #1d4ed8; }
    .filters { display: grid; grid-template-columns: repeat(2, minmax(220px, 1fr)); gap: .7rem; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .8rem; }
    label { display: grid; gap: .3rem; font-weight: 600; }
    input { border: 1px solid #d1d5db; border-radius: 8px; padding: .4rem .55rem; }
    table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; overflow: hidden; font-size: .92rem; }
    th, td { padding: .52rem; border-bottom: 1px solid #edf0f4; text-align: left; }
    th { background: #f8fafc; color: #475569; white-space: nowrap; }
    @media (max-width: 820px) { .filters { grid-template-columns: 1fr; } }
    @media (max-width: 640px) {
      table, thead, tbody, tr, td { display: block; width: 100%; }
      thead { display: none; }
      tr { border-bottom: 1px solid #e5e7eb; padding: .45rem .5rem; }
      td { border: 0; display: flex; justify-content: space-between; gap: .75rem; padding: .35rem 0; }
      td::before { content: attr(data-label); color: #64748b; font-weight: 700; }
    }
  `]
})
export class DashboardCommercialsPerformanceComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ReportingApiService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(false);
  readonly rows = signal<ReportingSalesPerson[]>([]);

  month = this.defaultMonth();
  search = '';

  readonly filteredRows = computed(() => {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.rows();
    return this.rows().filter((r) =>
      `${r.salesPersonCode}`.includes(q) ||
      (r.salesPersonName || '').toLowerCase().includes(q)
    );
  });

  readonly suggestions = computed(() =>
    this.rows().flatMap((r) => [String(r.salesPersonCode), r.salesPersonName]).filter(Boolean).slice(0, 100)
  );

  ngOnInit(): void {
    if (!['Admin', 'Manager'].includes(this.auth.role())) {
      this.rows.set([]);
      return;
    }
    const qMonth = this.route.snapshot.queryParamMap.get('month');
    if (qMonth) this.month = qMonth;
    this.load();
  }

  noop(): void {}

  load(): void {
    this.loading.set(true);
    this.api.getCommercialReporting(this.month).subscribe({
      next: (res) => {
        this.rows.set(res.data?.teamPerformances ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  money(value: number): string {
    return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(Number(value || 0))} MAD`;
  }

  pct(value: number): string {
    return `${Number(value || 0).toFixed(2)}%`;
  }

  private defaultMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }
}
