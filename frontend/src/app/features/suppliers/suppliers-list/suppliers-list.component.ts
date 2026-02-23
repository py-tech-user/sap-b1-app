import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { SupplierApiService } from '../../../core/services/supplier-api.service';

@Component({
  selector: 'app-suppliers-list',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      <div class="header">
        <h1>🏭 Fournisseurs</h1>
        <a routerLink="/suppliers/new" class="btn-primary">+ Nouveau fournisseur</a>
      </div>

      <div class="search-bar">
        <input type="text" [(ngModel)]="searchQuery" (keyup.enter)="load()" placeholder="🔍 Rechercher un fournisseur..." />
        <button (click)="load()">Rechercher</button>
      </div>

      <table>
        <thead>
          <tr><th>Code</th><th>Nom</th><th>Contact</th><th>Téléphone</th><th>Email</th><th>Ville</th><th>Actif</th><th>Actions</th></tr>
        </thead>
        <tbody>
          @for (s of items(); track s.id) {
            <tr>
              <td>{{ s.supplierCode }}</td>
              <td><strong>{{ s.supplierName }}</strong></td>
              <td>{{ s.contactPerson || '-' }}</td>
              <td>{{ s.phone || '-' }}</td>
              <td>{{ s.email || '-' }}</td>
              <td>{{ s.city || '-' }}</td>
              <td><span [class]="s.isActive ? 'active-badge' : 'inactive-badge'">{{ s.isActive ? 'Actif' : 'Inactif' }}</span></td>
              <td>
                <a [routerLink]="['/suppliers/edit', s.id]" class="btn-sm">Modifier</a>
              </td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="empty">Aucun fournisseur</td></tr>
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
    .search-bar { display: flex; gap: .5rem; margin-bottom: 1.5rem; }
    .search-bar input { flex: 1; padding: .6rem 1rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; }
    .search-bar button { padding: .6rem 1.2rem; background: #667eea; color: white; border: none; border-radius: 6px; cursor: pointer; }
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    th, td { padding: .85rem 1rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: .85rem; }
    .active-badge { background: #d4edda; color: #155724; padding: .2rem .5rem; border-radius: 20px; font-size: .75rem; font-weight: 600; }
    .inactive-badge { background: #f8d7da; color: #721c24; padding: .2rem .5rem; border-radius: 20px; font-size: .75rem; font-weight: 600; }
    .btn-sm { padding: .3rem .7rem; background: #667eea; border-radius: 4px; text-decoration: none; color: white; font-size: .8rem; }
    .empty { text-align: center; padding: 2rem; color: #999; }
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; }
    .pagination button { padding: .4rem .8rem; border: 1px solid #ddd; border-radius: 4px; background: white; cursor: pointer; }
    .pagination button:disabled { opacity: .4; }
  `]
})
export class SuppliersListComponent implements OnInit {
  items = signal<any[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  page = 1;
  searchQuery = '';

  constructor(private api: SupplierApiService) {}
  ngOnInit(): void { this.load(); }

  load(): void {
    this.api.getAll(this.page, 10, this.searchQuery || undefined).subscribe({
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
