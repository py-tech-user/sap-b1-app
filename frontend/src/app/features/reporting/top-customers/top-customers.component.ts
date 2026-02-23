import { Component, OnInit, inject, signal, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportingApiService } from '../../../core/services/reporting-api.service';
import { TopCustomer } from '../../../core/models/models';

@Component({
  selector: 'app-top-customers',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card">
      <h3>🏆 Top {{ data().length }} Clients</h3>
      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else {
        <table>
          <thead>
            <tr>
              <th>#</th>
              <th>Client</th>
              <th>CA Total</th>
              <th>Commandes</th>
              <th>Moy. cmd</th>
            </tr>
          </thead>
          <tbody>
            @for (c of data(); track c.customerId; let i = $index) {
              <tr>
                <td class="rank">
                  @switch (i) {
                    @case (0) { 🥇 }
                    @case (1) { 🥈 }
                    @case (2) { 🥉 }
                    @default { {{ i + 1 }} }
                  }
                </td>
                <td>
                  <strong>{{ c.cardName }}</strong>
                  @if (c.city) { <small class="city">{{ c.city }}</small> }
                </td>
                <td class="num">{{ c.totalRevenue | number:'1.2-2' }} MAD</td>
                <td class="num">{{ c.orderCount }}</td>
                <td class="num">{{ c.avgOrderValue | number:'1.0-0' }} MAD</td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="empty">Aucune donnée.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .card {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06); height: 100%;
    }
    h3 { margin: 0 0 1rem; font-size: 1.05rem; color: #1e2a3a; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.6rem 0.5rem; text-align: left; border-bottom: 1px solid #f0f0f0; font-size: 0.85rem; }
    th { font-weight: 600; color: #888; font-size: 0.78rem; text-transform: uppercase; }
    .rank { width: 40px; text-align: center; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .city { display: block; font-size: 0.75rem; color: #999; }
    .empty { text-align: center; color: #999; padding: 1.5rem; }
    .loading { text-align: center; padding: 2rem; color: #999; }
  `]
})
export class TopCustomersComponent implements OnInit {
  private reportingApi = inject(ReportingApiService);

  externalData = input<TopCustomer[]>();
  data = signal<TopCustomer[]>([]);
  loading = signal(false);

  ngOnInit(): void {
    const ext = this.externalData();
    if (ext && ext.length > 0) {
      this.data.set(ext);
    } else {
      this.loading.set(true);
      this.reportingApi.getTopCustomers(10).subscribe({
        next: (res) => { this.data.set(res.data ?? []); this.loading.set(false); },
        error: () => this.loading.set(false)
      });
    }
  }
}
