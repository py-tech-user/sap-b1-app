import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ReportingApiService, ReportingRecentDocument } from '../../core/services/reporting-api.service';
import { AuthService } from '../../core/services/auth.service';

type DocTypeFilter = 'all' | 'quotes' | 'orders' | 'deliverynotes' | 'invoices';

@Component({
  selector: 'app-dashboard-documents',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, DatePipe],
  template: `
    <div class="page">
      <a routerLink="/dashboard" class="back">Retour dashboard</a>
      <h1>Documents recents</h1>

      <div class="filters">
        <label>
          Mois
          <input type="month" [(ngModel)]="month" (change)="load()" />
        </label>
        <label>
          Type
          <select [(ngModel)]="type" (change)="onTypeChange()">
            <option value="all">Tous</option>
            <option value="quotes">Devis</option>
            <option value="orders">Commandes</option>
            <option value="deliverynotes">Bons de livraison</option>
            <option value="invoices">Factures</option>
          </select>
        </label>
        <label>
          N document
          <input list="doc-suggestions" [(ngModel)]="docSearch" (input)="noop()" placeholder="Ex: 10258" />
        </label>
        <label>
          Client
          <input list="customer-suggestions" [(ngModel)]="customerSearch" (input)="noop()" placeholder="Nom client" />
        </label>
        <label>
          Statut
          <select [(ngModel)]="statusFilter" (change)="noop()">
            <option value="all">Tous</option>
            <option value="En attente">En attente</option>
            <option value="Cloture">Cloture</option>
            <option value="Annule">Annule</option>
          </select>
        </label>
        @if (isAdminMode()) {
          <label>
            Commercial
            <input type="number" [(ngModel)]="salesPersonCode" (change)="load()" min="0" placeholder="0 = toute l'equipe" />
          </label>
        }
        <datalist id="doc-suggestions">
          @for (s of docSuggestions(); track s) {
            <option [value]="s"></option>
          }
        </datalist>
        <datalist id="customer-suggestions">
          @for (s of customerSuggestions(); track s) {
            <option [value]="s"></option>
          }
        </datalist>
      </div>

      @if (loading()) {
        <p>Chargement...</p>
      } @else {
        <table>
          <thead><tr><th>Type</th><th>N</th><th>Client</th><th>Statut</th><th>Date</th><th>Montant</th><th>Action</th></tr></thead>
          <tbody>
            @for (d of filtered(); track d.type + '-' + d.docEntry) {
              <tr>
                <td data-label="Type">{{ d.type }}</td>
                <td data-label="Numero">{{ d.docNum }}</td>
                <td data-label="Client">{{ d.cardName }}</td>
                <td data-label="Statut">{{ d.status || '-' }}</td>
                <td data-label="Date">{{ d.date | date:'dd/MM/yyyy' }}</td>
                <td data-label="Montant">{{ money(d.total) }}</td>
                <td data-label="Action">
                  @if (detailLink(d); as link) {
                    <a [routerLink]="link">Voir</a>
                  } @else {
                    <span>-</span>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7">Aucun document pour ce filtre.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .page { display: grid; gap: 1rem; }
    .back { color: #1d4ed8; text-decoration: none; }
    .filters { display: grid; grid-template-columns: repeat(4, minmax(160px, 1fr)); gap: .7rem; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .8rem; }
    label { display: grid; gap: .3rem; font-weight: 600; }
    input, select { border: 1px solid #d1d5db; border-radius: 8px; padding: .4rem .55rem; }
    table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; overflow: hidden; }
    th, td { padding: .55rem; border-bottom: 1px solid #edf0f4; text-align: left; }
    th { background: #f8fafc; color: #475569; }
    @media (max-width: 1000px) { .filters { grid-template-columns: 1fr 1fr; } }
    @media (max-width: 640px) {
      .filters { grid-template-columns: 1fr; }
      table, thead, tbody, tr, td { display: block; width: 100%; }
      thead { display: none; }
      tr { border-bottom: 1px solid #e5e7eb; padding: .45rem .5rem; }
      td { border: 0; display: flex; justify-content: space-between; gap: .75rem; padding: .35rem 0; }
      td::before { content: attr(data-label); color: #64748b; font-weight: 700; }
    }
  `]
})
export class DashboardDocumentsComponent implements OnInit {
  private readonly api = inject(ReportingApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  readonly loading = signal(false);
  readonly documents = signal<ReportingRecentDocument[]>([]);
  readonly isAdminMode = signal(false);

  month = this.defaultMonth();
  type: DocTypeFilter = 'all';
  docSearch = '';
  customerSearch = '';
  statusFilter = 'all';
  salesPersonCode = 0;

  readonly filtered = computed(() => {
    const type = this.type;
    const docQ = this.docSearch.trim().toLowerCase();
    const custQ = this.customerSearch.trim().toLowerCase();
    const statusQ = this.statusFilter;
    return this.documents().filter((d) => {
      if (type !== 'all' && this.typeOf(d) !== type) return false;
      if (docQ && !String(d.docNum ?? '').toLowerCase().includes(docQ)) return false;
      if (custQ && !String(d.cardName ?? '').toLowerCase().includes(custQ)) return false;
      if (statusQ !== 'all' && String(d.status ?? '').trim() !== statusQ) return false;
      return true;
    });
  });

  readonly docSuggestions = computed(() => this.documents().map((d) => String(d.docNum ?? '')).filter(Boolean).slice(0, 80));
  readonly customerSuggestions = computed(() => this.documents().map((d) => String(d.cardName ?? '')).filter(Boolean).slice(0, 80));

  ngOnInit(): void {
    this.isAdminMode.set(['Admin', 'Manager'].includes(this.auth.role()));
    const qMonth = this.route.snapshot.queryParamMap.get('month');
    const qType = this.route.snapshot.queryParamMap.get('type') as DocTypeFilter | null;
    const qSalesCode = Number(this.route.snapshot.queryParamMap.get('salesPersonCode') ?? 0);
    if (qMonth) this.month = qMonth;
    if (qType && ['all', 'quotes', 'orders', 'deliverynotes', 'invoices'].includes(qType)) this.type = qType;
    if (Number.isFinite(qSalesCode) && qSalesCode > 0) this.salesPersonCode = qSalesCode;
    this.load();
  }

  onTypeChange(): void {}
  noop(): void {}

  load(): void {
    this.loading.set(true);
    const scopedSales = this.isAdminMode() && this.salesPersonCode > 0 ? this.salesPersonCode : undefined;
    this.api.getCommercialReporting(this.month, scopedSales).subscribe({
      next: (res) => {
        this.documents.set(res.data?.recentDocuments ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  money(value: number): string {
    return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(Number(value || 0))} MAD`;
  }

  detailLink(doc: ReportingRecentDocument): string[] | null {
    const t = this.typeOf(doc);
    if (t === 'quotes') return ['/quotes', String(doc.docEntry)];
    if (t === 'orders') return ['/orders', String(doc.docEntry)];
    if (t === 'deliverynotes') return ['/deliverynotes', String(doc.docEntry)];
    if (t === 'invoices') return ['/factures', String(doc.docEntry)];
    return null;
  }

  private typeOf(doc: ReportingRecentDocument): DocTypeFilter {
    const normalized = String(doc.type ?? '').toLowerCase();
    if (normalized.includes('devis') || normalized.includes('quote')) return 'quotes';
    if (normalized.includes('commande') || normalized.includes('order')) return 'orders';
    if (normalized.includes('livraison') || normalized.includes('delivery')) return 'deliverynotes';
    if (normalized.includes('facture') || normalized.includes('invoice')) return 'invoices';
    return 'all';
  }

  private defaultMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }
}
