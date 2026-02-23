import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DeliveryNoteApiService } from '../../../core/services/delivery-note-api.service';

@Component({
  selector: 'app-delivery-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="header">
        <h1>🚚 Bons de livraison</h1>
        <a routerLink="/delivery-notes/new" class="btn-primary">+ Nouveau BL</a>
      </div>

      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ totalCount() }}</span><span class="kpi-label">Total</span></div>
        <div class="kpi draft"><span class="kpi-value">{{ draftCount() }}</span><span class="kpi-label">Brouillons</span></div>
        <div class="kpi shipped"><span class="kpi-value">{{ shippedCount() }}</span><span class="kpi-label">Expédiés</span></div>
        <div class="kpi delivered"><span class="kpi-value">{{ deliveredCount() }}</span><span class="kpi-label">Livrés</span></div>
      </div>

      <div class="filters">
        <select [(ngModel)]="statusFilter" (ngModelChange)="load()">
          <option value="">Tous les statuts</option>
          <option value="Draft">Brouillon</option>
          <option value="Confirmed">Confirmé</option>
          <option value="Shipped">Expédié</option>
          <option value="Delivered">Livré</option>
          <option value="Cancelled">Annulé</option>
        </select>
      </div>

      <table>
        <thead>
          <tr><th>N°</th><th>Client</th><th>Commande</th><th>Date livr.</th><th>Transporteur</th><th>Statut</th><th>Actions</th></tr>
        </thead>
        <tbody>
          @for (d of items(); track d.id) {
            <tr>
              <td>{{ d.deliveryNumber }}</td>
              <td>{{ d.customerName }}</td>
              <td>{{ d.orderDocNum }}</td>
              <td>{{ d.deliveryDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ d.carrierName || '-' }}</td>
              <td><span [class]="'badge ' + d.status.toLowerCase()">{{ d.status }}</span></td>
              <td><a [routerLink]="['/delivery-notes', d.id]" class="btn-sm">Voir</a></td>
            </tr>
          } @empty {
            <tr><td colspan="7" class="empty">Aucun bon de livraison</td></tr>
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
    .kpi.draft .kpi-value { color: #636e72; }
    .kpi.shipped .kpi-value { color: #0984e3; }
    .kpi.delivered .kpi-value { color: #00b894; }
    .filters { margin-bottom: 1rem; }
    .filters select { padding: .5rem 1rem; border: 1px solid #ddd; border-radius: 6px; background: white; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    th, td { padding: .85rem 1rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: .85rem; }
    .badge { padding: .25rem .6rem; border-radius: 20px; font-size: .75rem; font-weight: 600; }
    .badge.draft { background: #dfe6e9; color: #636e72; }
    .badge.confirmed { background: #e2e3f1; color: #4a4e88; }
    .badge.shipped { background: #d1ecf1; color: #0c5460; }
    .badge.delivered { background: #d4edda; color: #155724; }
    .badge.cancelled { background: #f8d7da; color: #721c24; }
    .btn-sm { padding: .3rem .7rem; background: #667eea; border-radius: 4px; text-decoration: none; color: white; font-size: .8rem; }
    .empty { text-align: center; padding: 2rem; color: #999; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: .4rem .8rem; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .pagination button:disabled { opacity: .4; }
  `]
})
export class DeliveryListComponent implements OnInit {
  items = signal<any[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  draftCount = computed(() => this.items().filter(d => d.status === 'Draft').length);
  shippedCount = computed(() => this.items().filter(d => d.status === 'Shipped').length);
  deliveredCount = computed(() => this.items().filter(d => d.status === 'Delivered').length);
  page = 1;
  statusFilter = '';

  constructor(private api: DeliveryNoteApiService) {}
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
