import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { GoodsReceiptApiService } from '../../../core/services/goods-receipt-api.service';

@Component({
  selector: 'app-receipts-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="header">
        <h1>📥 Réceptions marchandises</h1>
        <a routerLink="/goods-receipts/new" class="btn-primary">+ Nouvelle réception</a>
      </div>

      <div class="kpis">
        <div class="kpi"><span class="kpi-value">{{ totalCount() }}</span><span class="kpi-label">Total</span></div>
        <div class="kpi draft"><span class="kpi-value">{{ draftCount() }}</span><span class="kpi-label">Brouillons</span></div>
        <div class="kpi confirmed"><span class="kpi-value">{{ confirmedCount() }}</span><span class="kpi-label">Confirmées</span></div>
      </div>

      <table>
        <thead>
          <tr><th>N°</th><th>Fournisseur</th><th>BC</th><th>Date réception</th><th>Statut</th><th>Actions</th></tr>
        </thead>
        <tbody>
          @for (g of items(); track g.id) {
            <tr>
              <td>{{ g.receiptNumber }}</td>
              <td>{{ g.supplierName }}</td>
              <td>{{ g.poNumber || '-' }}</td>
              <td>{{ g.receiptDate | date:'dd/MM/yyyy' }}</td>
              <td><span [class]="'badge ' + g.status.toLowerCase()">{{ g.status }}</span></td>
              <td>
                <a [routerLink]="['/goods-receipts', g.id]" class="btn-sm">Voir</a>
                @if (g.status === 'Draft') {
                  <button class="btn-sm-success" (click)="confirmReceipt(g.id)">Confirmer</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="6" class="empty">Aucune réception</td></tr>
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
    .kpi.confirmed .kpi-value { color: #00b894; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    th, td { padding: .85rem 1rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: .85rem; }
    .badge { padding: .25rem .6rem; border-radius: 20px; font-size: .75rem; font-weight: 600; }
    .badge.draft { background: #dfe6e9; color: #636e72; }
    .badge.confirmed { background: #d4edda; color: #155724; }
    .btn-sm { padding: .3rem .7rem; background: #667eea; border-radius: 4px; text-decoration: none; color: white; font-size: .8rem; margin-right: .3rem; }
    .btn-sm-success { padding: .3rem .7rem; background: #00b894; border: none; border-radius: 4px; color: white; font-size: .8rem; cursor: pointer; }
    .empty { text-align: center; padding: 2rem; color: #999; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: .4rem .8rem; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .pagination button:disabled { opacity: .4; }
  `]
})
export class ReceiptsListComponent implements OnInit {
  items = signal<any[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  draftCount = computed(() => this.items().filter(g => g.status === 'Draft').length);
  confirmedCount = computed(() => this.items().filter(g => g.status === 'Confirmed').length);
  page = 1;

  constructor(private api: GoodsReceiptApiService) {}
  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getAll(this.page, 10).subscribe({
      next: (res) => {
        const d = res.data ?? (res as any);
        this.items.set(d.items ?? []);
        this.totalCount.set(d.totalCount ?? 0);
        this.totalPages.set(d.totalPages ?? 1);
      }
    });
  }

  confirmReceipt(id: number): void {
    this.api.confirm(id).subscribe({ next: () => this.load() });
  }
  changePage(p: number): void { this.page = p; this.load(); }
}
