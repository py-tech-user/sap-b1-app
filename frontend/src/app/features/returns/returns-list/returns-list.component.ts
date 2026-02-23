import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ReturnApiService } from '../../../core/services/return-api.service';

@Component({
  selector: 'app-returns-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="header">
        <h1>📦 Retours</h1>
        <a routerLink="/returns/new" class="btn-primary">+ Nouveau retour</a>
      </div>

      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ totalCount() }}</span><span class="kpi-label">Total</span></div>
        <div class="kpi pending"><span class="kpi-value">{{ pendingCount() }}</span><span class="kpi-label">En attente</span></div>
        <div class="kpi approved"><span class="kpi-value">{{ approvedCount() }}</span><span class="kpi-label">Approuvés</span></div>
        <div class="kpi received"><span class="kpi-value">{{ receivedCount() }}</span><span class="kpi-label">Reçus</span></div>
      </div>

      <div class="filters">
        <select [(ngModel)]="statusFilter" (ngModelChange)="load()">
          <option value="">Tous les statuts</option>
          <option value="Pending">En attente</option>
          <option value="Approved">Approuvé</option>
          <option value="Rejected">Rejeté</option>
          <option value="Received">Reçu</option>
          <option value="Processed">Traité</option>
        </select>
      </div>

      <table>
        <thead>
          <tr>
            <th>N°</th>
            <th>Client</th>
            <th>Commande</th>
            <th>Raison</th>
            <th>Date</th>
            <th>Total</th>
            <th>Statut</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (r of items(); track r.id) {
            <tr>
              <td>{{ r.returnNumber }}</td>
              <td>{{ r.customerName }}</td>
              <td>{{ r.orderDocNum }}</td>
              <td>{{ r.reason }}</td>
              <td>{{ r.returnDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ r.totalAmount | currency:'MAD':'symbol':'1.2-2' }}</td>
              <td><span [class]="'badge ' + r.status.toLowerCase()">{{ r.status }}</span></td>
              <td><a [routerLink]="['/returns', r.id]" class="btn-sm">Voir</a></td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="empty">Aucun retour trouvé</td></tr>
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
    .btn-primary { background: #667eea; color: white; padding: 0.6rem 1.2rem; border-radius: 6px; text-decoration: none; font-weight: 500; }
    .btn-primary:hover { background: #5a6fd6; }
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
    .kpi { background: white; border-radius: 8px; padding: 1.2rem; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .kpi-value { display: block; font-size: 1.8rem; font-weight: 700; color: #2d3436; }
    .kpi-label { font-size: 0.8rem; color: #888; }
    .kpi.pending .kpi-value { color: #e17055; }
    .kpi.approved .kpi-value { color: #00b894; }
    .kpi.received .kpi-value { color: #0984e3; }
    .filters { margin-bottom: 1rem; }
    .filters select { padding: 0.5rem 1rem; border: 1px solid #ddd; border-radius: 6px; background: white; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    th, td { padding: 0.85rem 1rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: 0.85rem; }
    .badge { padding: 0.25rem 0.6rem; border-radius: 20px; font-size: 0.75rem; font-weight: 600; }
    .badge.pending { background: #ffeaa7; color: #d35400; }
    .badge.approved { background: #d4edda; color: #155724; }
    .badge.rejected { background: #f8d7da; color: #721c24; }
    .badge.received { background: #d1ecf1; color: #0c5460; }
    .badge.processed { background: #e2e3f1; color: #4a4e88; }
    .btn-sm { padding: 0.3rem 0.7rem; background: #667eea; border-radius: 4px; text-decoration: none; color: white; font-size: 0.8rem; }
    .empty { text-align: center; padding: 2rem; color: #999; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: 0.4rem 0.8rem; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .pagination button:disabled { opacity: .4; cursor: default; }
  `]
})
export class ReturnsListComponent implements OnInit {
  items = signal<any[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  pendingCount = computed(() => this.items().filter(r => r.status === 'Pending').length);
  approvedCount = computed(() => this.items().filter(r => r.status === 'Approved').length);
  receivedCount = computed(() => this.items().filter(r => r.status === 'Received').length);
  page = 1;
  statusFilter = '';

  constructor(private api: ReturnApiService) {}

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
