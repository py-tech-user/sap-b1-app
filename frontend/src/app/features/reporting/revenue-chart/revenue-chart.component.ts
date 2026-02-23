import { Component, input, signal, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartType } from 'chart.js';
import {
  Chart, LineController, LineElement, PointElement,
  LinearScale, CategoryScale, Filler, Tooltip, Legend
} from 'chart.js';
import { ReportingApiService } from '../../../core/services/reporting-api.service';
import { RevenueEvolution } from '../../../core/models/models';

Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Filler, Tooltip, Legend);

@Component({
  selector: 'app-revenue-chart',
  standalone: true,
  imports: [CommonModule, BaseChartDirective],
  template: `
    <div class="chart-card">
      <div class="chart-header">
        <h3>📈 Évolution du chiffre d'affaires</h3>
        <div class="period-selector">
          <button [class.active]="period() === 'daily'" (click)="switchPeriod('daily')">30 jours</button>
          <button [class.active]="period() === 'monthly'" (click)="switchPeriod('monthly')">12 mois</button>
        </div>
      </div>
      @if (loading()) {
        <div class="chart-loading">Chargement...</div>
      } @else {
        <div class="chart-wrapper">
          <canvas baseChart
            [type]="lineChartType"
            [data]="lineChartData()"
            [options]="lineChartOptions">
          </canvas>
        </div>
      }
    </div>
  `,
  styles: [`
    .chart-card {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .chart-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .chart-header h3 { margin: 0; font-size: 1.05rem; color: #1e2a3a; }
    .period-selector { display: flex; gap: 4px; }
    .period-selector button {
      padding: 0.35rem 0.75rem; border: 1px solid #ddd; background: white;
      border-radius: 6px; cursor: pointer; font-size: 0.8rem; color: #555;
    }
    .period-selector button.active { background: #667eea; color: white; border-color: #667eea; }
    .period-selector button:hover:not(.active) { background: #f5f5f5; }
    .chart-wrapper { position: relative; height: 320px; }
    .chart-loading { text-align: center; padding: 4rem; color: #999; }
  `]
})
export class RevenueChartComponent implements OnInit {
  private reportingApi = inject(ReportingApiService);

  /** Données passées en input (optionnel, sinon charge seul) */
  externalData = input<RevenueEvolution[]>();

  period = signal<'monthly' | 'daily'>('monthly');
  loading = signal(false);

  lineChartType: ChartType = 'line';

  lineChartData = signal<ChartConfiguration['data']>({
    labels: [],
    datasets: []
  });

  lineChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: {
        callbacks: {
          label: (ctx) => `${(ctx.parsed.y ?? 0).toLocaleString('fr-FR')} MAD`
        }
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          callback: (val) => `${Number(val).toLocaleString('fr-FR')} MAD`
        }
      }
    }
  };

  ngOnInit(): void {
    const ext = this.externalData();
    if (ext && ext.length > 0) {
      this.applyData(ext);
    } else {
      this.loadData();
    }
  }

  switchPeriod(p: 'monthly' | 'daily'): void {
    this.period.set(p);
    this.loadData();
  }

  private loadData(): void {
    this.loading.set(true);
    const obs = this.period() === 'monthly'
      ? this.reportingApi.getRevenueMonthly(12)
      : this.reportingApi.getRevenueDaily(30);

    obs.subscribe({
      next: (res) => {
        this.applyData(res.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  private applyData(data: RevenueEvolution[]): void {
    this.lineChartData.set({
      labels: data.map(d => d.period),
      datasets: [{
        data: data.map(d => d.revenue),
        label: 'Chiffre d\'affaires',
        borderColor: '#667eea',
        backgroundColor: 'rgba(102,126,234,0.1)',
        fill: true,
        tension: 0.4,
        pointRadius: 4,
        pointBackgroundColor: '#667eea'
      }]
    });
  }
}
