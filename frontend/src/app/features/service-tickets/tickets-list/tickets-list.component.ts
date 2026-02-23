import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ServiceTicketApiService } from '../../../core/services/service-ticket-api.service';

@Component({
  selector: 'app-tickets-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="header">
        <h1>🔧 Tickets SAV</h1>
        <a routerLink="/service-tickets/new" class="btn-primary">+ Nouveau ticket</a>
      </div>

      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ totalCount() }}</span><span class="kpi-label">Total</span></div>
        <div class="kpi open"><span class="kpi-value">{{ openCount() }}</span><span class="kpi-label">Ouverts</span></div>
        <div class="kpi scheduled"><span class="kpi-value">{{ scheduledCount() }}</span><span class="kpi-label">Planifiés</span></div>
        <div class="kpi inprogress"><span class="kpi-value">{{ progressCount() }}</span><span class="kpi-label">En cours</span></div>
      </div>

      <div class="filters">
        <select [(ngModel)]="statusFilter" (ngModelChange)="load()">
          <option value="">Tous les statuts</option>
          <option value="Open">Ouvert</option>
          <option value="Scheduled">Planifié</option>
          <option value="InProgress">En cours</option>
          <option value="Completed">Terminé</option>
          <option value="Cancelled">Annulé</option>
        </select>
      </div>

      <table>
        <thead>
          <tr><th>N°</th><th>Client</th><th>Description</th><th>Planifié</th><th>Coût</th><th>Statut</th><th>Actions</th></tr>
        </thead>
        <tbody>
          @for (t of items(); track t.id) {
            <tr>
              <td>{{ t.ticketNumber }}</td>
              <td>{{ t.customerName }}</td>
              <td class="desc">{{ t.description | slice:0:50 }}{{ t.description?.length > 50 ? '...' : '' }}</td>
              <td>{{ t.scheduledDate ? (t.scheduledDate | date:'dd/MM/yyyy') : '-' }}</td>
              <td>{{ t.totalCost | currency:'MAD':'symbol':'1.2-2' }}</td>
              <td><span [class]="'badge ' + t.status.toLowerCase()">{{ t.status }}</span></td>
              <td><a [routerLink]="['/service-tickets', t.id]" class="btn-sm">Voir</a></td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="empty">Aucun ticket</td></tr>
          }
        </tbody>
      </table>

      @if (totalPages() > 1) {
        <div class="pagination">
          <button (click)="page > 1 && changePage(page - 1)" [disabled]="page <= 1">◀</button>
          <span>{{ page }} / {{ totalPages() }}</span>
          <button (click)="page < totalPages() && changePage(page + 1)" [disabled]="page >= totalPages()">▶</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .page { padding: 0; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    h1 { font-size: 1.5rem; color: #2d3436; }
    .btn-primary { background: #667eea; color: white; padding: .6rem 1.2rem; border-radius: 6px; text-decoration: none; font-weight: 500; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
    .kpi { background: white; border-radius: 8px; padding: 1.2rem; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .kpi-value { display: block; font-size: 1.8rem; font-weight: 700; color: #2d3436; }
    .kpi-label { font-size: .8rem; color: #888; }
    .kpi.open .kpi-value { color: #e17055; }
    .kpi.scheduled .kpi-value { color: #6c5ce7; }
    .kpi.inprogress .kpi-value { color: #0984e3; }
    .filters { margin-bottom: 1rem; }
    .filters select { padding: .5rem 1rem; border: 1px solid #ddd; border-radius: 6px; background: white; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    th, td { padding: .85rem 1rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: .85rem; }
    .desc { max-width: 250px; }
    .badge { padding: .25rem .6rem; border-radius: 20px; font-size: .75rem; font-weight: 600; }
    .badge.open { background: #ffeaa7; color: #d35400; }
    .badge.scheduled { background: #e2e3f1; color: #4a4e88; }
    .badge.inprogress { background: #d1ecf1; color: #0c5460; }
    .badge.completed { background: #d4edda; color: #155724; }
    .badge.cancelled { background: #f8d7da; color: #721c24; }
    .btn-sm { padding: .3rem .7rem; background: #667eea; border-radius: 4px; text-decoration: none; color: white; font-size: .8rem; }
    .empty { text-align: center; padding: 2rem; color: #999; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: .4rem .8rem; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .pagination button:disabled { opacity: .4; }
  `]
})
export class TicketsListComponent implements OnInit {
  items = signal<any[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  openCount = computed(() => this.items().filter(t => t.status === 'Open').length);
  scheduledCount = computed(() => this.items().filter(t => t.status === 'Scheduled').length);
  progressCount = computed(() => this.items().filter(t => t.status === 'InProgress').length);
  page = 1;
  statusFilter = '';

  constructor(private api: ServiceTicketApiService) {}
  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getAll(this.page, 10, this.statusFilter || undefined).subscribe({
      next: (res) => {
        const d = res.data ?? (res as any);
        this.items.set(d.items ?? []);
        this.totalCount.set(d.totalCount ?? 0);
        this.totalPages.set(d.totalPages ?? 1);
      }
    });
  }
  changePage(p: number): void { this.page = p; this.load(); }
}
