import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PartnerApiService } from '../../../core/services/partner-api.service';

@Component({
  selector: 'app-partner-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <div>
          <h1>Partenaires</h1>
          <p class="subtitle">Liste SAP via /api/sap/partners</p>
        </div>
        <div class="actions">
          <button type="button" class="btn-outline" (click)="reload()" [disabled]="loading()">
            {{ loading() ? 'Chargement...' : 'Actualiser' }}
          </button>
          <a routerLink="/customers/new" class="btn-primary">+ Créer un partenaire</a>
        </div>
      </div>

      @if (error()) {
        <div class="error">{{ error() }}</div>
      }

      @if (loading()) {
        <div class="status">Chargement des partenaires...</div>
      } @else {
        <div class="filters">
          <input
            type="text"
            placeholder="Recherche (code, nom, email, téléphone...)"
            [(ngModel)]="search"
            (ngModelChange)="onFilterChanged()"
          />

          <select [(ngModel)]="typeFilter" (ngModelChange)="onFilterChanged()">
            <option value="">Tous les types</option>
            <option value="client">Client</option>
            <option value="prospect">Prospect</option>
          </select>
        </div>

        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Raison social</th>
              <th>Type</th>
              <th>Devise</th>
              <th>Téléphone</th>
              <th>Email</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            @for (p of pagedItems(); track p.CardCode) {
              <tr>
                <td>{{ p.CardCode }}</td>
                <td>{{ p.CardName }}</td>
                <td>{{ p.CardType || '-' }}</td>
                <td>{{ p.Currency || '-' }}</td>
                <td>{{ p.Cellular || p.Phone1 || '-' }}</td>
                <td>{{ p.EmailAddress || '-' }}</td>
                <td><button type="button" class="btn-outline" (click)="openDetails(p)">Détails</button></td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="empty">Aucune donnée</td></tr>
            }
          </tbody>
        </table>

        <div class="pager">
          <button class="btn-outline" (click)="prev()" [disabled]="page() <= 1">← Précédent</button>
          <span>Page {{ page() }} / {{ totalPages() }}</span>
          <button class="btn-outline" (click)="next()" [disabled]="page() >= totalPages()">Suivant →</button>
        </div>
      }

      @if (selectedPartner()) {
        <div class="drawer-backdrop" (click)="closeDetails()"></div>
        <aside class="drawer" role="dialog" aria-modal="true" aria-label="Détails partenaire">
          <div class="drawer-header">
            <h3>Détails partenaire {{ selectedPartnerName() }}</h3>
            <button type="button" class="btn-outline" (click)="closeDetails()">Fermer</button>
          </div>
          <div class="drawer-body">
            <dl class="details-list">
              @for (entry of selectedPartnerFields(); track entry.key) {
                <div class="details-row">
                  <dt>{{ entry.key }}</dt>
                  <dd>{{ entry.value }}</dd>
                </div>
              }
            </dl>
          </div>
        </aside>
      }
    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .actions { display: flex; align-items: center; gap: 0.6rem; }
    .subtitle { margin: 0.2rem 0 0; color: #666; }
    .filters { display: grid; grid-template-columns: 1fr 220px; gap: 0.6rem; }
    .filters input, .filters select { width: 100%; border: 1px solid #d0d7de; border-radius: 6px; padding: 0.45rem 0.6rem; }
    table { width: 100%; border-collapse: collapse; background: #fff; border-radius: 8px; overflow: hidden; }
    th, td { text-align: left; padding: 0.75rem; border-bottom: 1px solid #eceff4; }
    th { background: #f5f7fb; }
    .empty, .status { text-align: center; color: #666; padding: 1rem; }
    .error { color: #b00020; }
    .pager { display: flex; justify-content: space-between; align-items: center; }
    .btn-outline { border: 1px solid #1976d2; background: #fff; color: #1976d2; border-radius: 4px; padding: 0.35rem 0.6rem; cursor: pointer; }
    .btn-primary { border: 1px solid #1976d2; background: #1976d2; color: #fff; border-radius: 4px; padding: 0.35rem 0.6rem; text-decoration: none; }
    .drawer-backdrop { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.35); z-index: 30; }
    .drawer { position: fixed; top: 0; right: 0; height: 100vh; width: min(460px, 100vw); background: #fff; box-shadow: -8px 0 24px rgba(15, 23, 42, 0.18); z-index: 31; display: flex; flex-direction: column; }
    .drawer-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: 1rem; border-bottom: 1px solid #e5e7eb; }
    .drawer-body { padding: 1rem; overflow: auto; }
    .details-list { margin: 0; }
    .details-row { display: grid; grid-template-columns: minmax(120px, 1fr) 2fr; gap: 0.75rem; padding: 0.5rem 0; border-bottom: 1px dashed #e5e7eb; }
    .details-row dt { color: #374151; font-weight: 600; }
    .details-row dd { margin: 0; color: #111827; word-break: break-word; }
    @media (max-width: 900px) { .filters { grid-template-columns: 1fr; } }
  `]
})
export class PartnerListComponent implements OnInit {
  private readonly api = inject(PartnerApiService);
  private readonly pageCache = new Map<number, any[]>();

  readonly loading = signal(false);
  readonly error = signal('');
  readonly allItems = signal<any[]>([]);
  readonly items = computed(() => this.filteredItems());
  readonly selectedPartner = signal<any | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(15);
  readonly totalCount = signal(0);
  search = '';
  typeFilter = '';
  readonly filteredItems = computed(() => {
    const search = this.search.trim().toLowerCase();
    const type = this.typeFilter.trim().toLowerCase();

    return this.allItems().filter((partner) => {
      const cardType = this.getNormalizedTypeLabel(partner).toLowerCase();
      const matchesType = !type || cardType.includes(type);
      if (!matchesType) return false;

      if (!search) return true;

      const haystack = [
        partner?.CardCode,
        partner?.CardName,
        partner?.EmailAddress,
        partner?.Cellular,
        partner?.Phone1,
        partner?.Currency,
        partner?.CardType
      ]
        .map((v: unknown) => String(v ?? '').toLowerCase())
        .join(' ');

      return haystack.includes(search);
    });
  });
  readonly totalPages = computed(() => {
    const fromApi = Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
    const fromLoaded = Math.max(1, Math.ceil(this.filteredItems().length / this.pageSize()));
    return Math.max(fromApi, fromLoaded);
  });
  readonly pagedItems = computed(() => {
    const start = (this.page() - 1) * this.pageSize();
    return this.filteredItems().slice(start, start + this.pageSize());
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.error.set('');
    this.pageCache.clear();
    this.allItems.set([]);
    this.totalCount.set(0);
    this.page.set(1);

    this.api.getAll(1, this.pageSize())
      .subscribe({
        next: (res) => {
          const firstPage = res.items ?? [];
          this.pageCache.set(1, firstPage);
          this.allItems.set(firstPage);
          this.totalCount.set(Number(res.totalCount ?? firstPage.length));
          this.prefetchNextPages(2);
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors du chargement des partenaires.');
          this.loading.set(false);
        }
      });
  }

  private prefetchNextPages(startPage: number): void {
    const maxPage = Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
    if (startPage > maxPage) return;

    this.api.getAll(startPage, this.pageSize())
      .subscribe({
        next: (res) => {
          const rows = res.items ?? [];
          this.pageCache.set(startPage, rows);

          if (rows.length > 0) {
            const merged = new Map<string, any>();
            for (const item of this.allItems()) {
              const key = String(item?.CardCode ?? item?.['Code'] ?? '').trim();
              if (key) merged.set(key, item);
            }
            for (const item of rows) {
              const key = String(item?.CardCode ?? item?.['Code'] ?? '').trim();
              if (!key) continue;
              merged.set(key, item);
            }

            const mergedItems = Array.from(merged.values());
            this.allItems.set(mergedItems);
          }

          this.prefetchNextPages(startPage + 1);
        },
        error: () => {
        }
      });
  }

  prev(): void {
    if (this.page() <= 1) return;
    this.page.update((p) => p - 1);
  }

  onFilterChanged(): void {
    this.page.set(1);
  }

  next(): void {
    if (this.page() >= this.totalPages()) return;
    this.page.update((p) => p + 1);
  }

  openDetails(partner: any): void {
    this.selectedPartner.set(partner ?? null);
  }

  closeDetails(): void {
    this.selectedPartner.set(null);
  }

  private getNormalizedTypeLabel(partner: any): string {
    const raw = String(partner?.CardType ?? '').trim().toLowerCase();
    if (!raw) return '';
    if (raw.includes('prospect') || raw === 'clid' || raw === 'lead' || raw === 'cLid'.toLowerCase()) return 'Prospect';
    if (raw.includes('fournisseur') || raw.includes('supplier') || raw.includes('vendor') || raw === 'csupplier') return 'Fournisseur';
    if (raw.includes('client') || raw.includes('customer') || raw === 'ccustomer') return 'Client';
    return String(partner?.CardType ?? '');
  }

  selectedPartnerName(): string {
    const p = this.selectedPartner();
    return String(p?.CardName ?? p?.cardName ?? p?.Name ?? p?.name ?? '-');
  }

  selectedPartnerFields(): Array<{ key: string; value: string }> {
    const p = this.selectedPartner();
    if (!p) return [];

    return Object.entries(p).map(([key, value]) => ({
      key,
      value: value === null || value === undefined || String(value).trim() === '' ? '-' : String(value)
    }));
  }
}
