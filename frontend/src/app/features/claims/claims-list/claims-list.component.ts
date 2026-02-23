import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ClaimApiService } from '../../../core/services/claim-api.service';

@Component({
  selector: 'app-claims-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="header">
        <h1>📋 Réclamations</h1>
        <a routerLink="/claims/new" class="btn-primary">+ Nouvelle réclamation</a>
      </div>

      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ totalCount() }}</span><span class="kpi-label">Total</span></div>
        <div class="kpi open"><span class="kpi-value">{{ openCount() }}</span><span class="kpi-label">Ouvertes</span></div>
        <div class="kpi progress"><span class="kpi-value">{{ progressCount() }}</span><span class="kpi-label">En cours</span></div>
        <div class="kpi urgent"><span class="kpi-value">{{ urgentCount() }}</span><span class="kpi-label">Urgentes</span></div>
      </div>

      <div class="filters">
        <select [(ngModel)]="statusFilter" (ngModelChange)="load()">
          <option value="">Tous les statuts</option>
          <option value="Open">Ouverte</option>
          <option value="InProgress">En cours</option>
          <option value="Resolved">Résolue</option>
          <option value="Closed">Fermée</option>
        </select>
        <select [(ngModel)]="priorityFilter" (ngModelChange)="load()">
          <option value="">Toutes priorités</option>
          <option value="Critical">Critique</option>
          <option value="High">Haute</option>
          <option value="Medium">Moyenne</option>
          <option value="Low">Basse</option>
        </select>
      </div>

      <table>
        <thead>
          <tr><th>N°</th><th>Type</th><th>Client</th><th>Sujet</th><th>Priorité</th><th>Statut</th><th>Date</th><th>Actions</th></tr>
        </thead>
        <tbody>
          @for (c of items(); track c.id) {
            <tr>
              <td>{{ c.claimNumber }}</td>
              <td>{{ c.type }}</td>
              <td>{{ c.customerName }}</td>
              <td>{{ c.subject }}</td>
              <td><span [class]="'priority ' + c.priority.toLowerCase()">{{ c.priority }}</span></td>
              <td><span [class]="'badge ' + c.status.toLowerCase()">{{ c.status }}</span></td>
              <td>{{ c.createdAt | date:'dd/MM/yyyy' }}</td>
              <td><a [routerLink]="['/claims', c.id]" class="btn-sm">Voir</a></td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="empty">Aucune réclamation</td></tr>
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
    .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 1rem; margin-bottom: 1.5rem; }
    .kpi { background: white; border-radius: 8px; padding: 1.2rem; text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .kpi-value { display: block; font-size: 1.8rem; font-weight: 700; color: #2d3436; }
    .kpi-label { font-size: 0.8rem; color: #888; }
    .kpi.open .kpi-value { color: #e17055; }
    .kpi.progress .kpi-value { color: #0984e3; }
    .kpi.urgent .kpi-value { color: #d63031; }
    .filters { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
    .filters select { padding: 0.5rem 1rem; border: 1px solid #ddd; border-radius: 6px; background: white; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    th, td { padding: 0.85rem 1rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: 0.85rem; }
    .badge { padding: 0.25rem 0.6rem; border-radius: 20px; font-size: 0.75rem; font-weight: 600; }
    .badge.open { background: #ffeaa7; color: #d35400; }
    .badge.inprogress { background: #d1ecf1; color: #0c5460; }
    .badge.resolved { background: #d4edda; color: #155724; }
    .badge.closed { background: #e2e3e5; color: #383d41; }
    .priority { padding: 0.2rem 0.5rem; border-radius: 4px; font-size: 0.75rem; font-weight: 600; }
    .priority.critical { background: #e74c3c; color: white; }
    .priority.high { background: #e17055; color: white; }
    .priority.medium { background: #fdcb6e; color: #856404; }
    .priority.low { background: #dfe6e9; color: #636e72; }
    .btn-sm { padding: 0.3rem 0.7rem; background: #667eea; border-radius: 4px; text-decoration: none; color: white; font-size: 0.8rem; }
    .empty { text-align: center; padding: 2rem; color: #999; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: 0.4rem 0.8rem; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .pagination button:disabled { opacity: .4; }
  `]
})
export class ClaimsListComponent implements OnInit {
  items = signal<any[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  openCount = computed(() => this.items().filter(c => c.status === 'Open').length);
  progressCount = computed(() => this.items().filter(c => c.status === 'InProgress').length);
  urgentCount = computed(() => this.items().filter(c => c.priority === 'Critical' || c.priority === 'High').length);
  page = 1;
  statusFilter = '';
  priorityFilter = '';

  constructor(private api: ClaimApiService) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getAll(this.page, 10, this.statusFilter || undefined, this.priorityFilter || undefined).subscribe({
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
