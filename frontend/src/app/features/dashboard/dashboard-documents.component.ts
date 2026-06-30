import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
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
            <option value="orders">Bon de commande</option>
            <option value="deliverynotes">Bon de livraison</option>
            <option value="invoices">Facture</option>
          </select>
        </label>
        <label class="search-box">
          N document
          <input [(ngModel)]="docSearch" (input)="openDocSearchSuggestions = true" (focus)="openDocSearchSuggestions = true" placeholder="Ex: 10258" />
          @if (openDocSearchSuggestions && filteredDocSuggestions().length) {
            <div class="filter-suggestions">
              @for (s of filteredDocSuggestions(); track s) {
                <button type="button" (mousedown)="selectDocSuggestion(s)">{{ s }}</button>
              }
            </div>
          }
        </label>
        <label class="search-box">
          Client
          <input [(ngModel)]="customerSearch" (input)="openCustomerSearchSuggestions = true" (focus)="openCustomerSearchSuggestions = true" placeholder="Nom client" />
          @if (openCustomerSearchSuggestions && filteredCustomerSuggestions().length) {
            <div class="filter-suggestions">
              @for (s of filteredCustomerSuggestions(); track s) {
                <button type="button" (mousedown)="selectCustomerSuggestion(s)">{{ s }}</button>
              }
            </div>
          }
        </label>
        <label>
          Statut
          <select [(ngModel)]="statusFilter" (change)="noop()">
            <option value="all">Tous</option>
            <option value="open">En attente</option>
            <option value="closed">Clôturé</option>
            <option value="cancelled">Annulé</option>
          </select>
        </label>
        @if (isAdminMode()) {
          <label>
            Commercial
            <input type="number" [(ngModel)]="salesPersonCode" (change)="load()" min="0" placeholder="0 = toute l'equipe" />
          </label>
        }
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
    .search-box { position: relative; }
    .filter-suggestions { position: absolute; z-index: 20; top: calc(100% + 4px); left: 0; right: 0; display: grid; gap: .15rem; max-height: 280px; overflow: auto; background: #fff; border: 1px solid #cbd5e1; border-radius: 12px; box-shadow: 0 18px 38px rgba(15,23,42,.16); padding: .35rem; }
    .filter-suggestions button { width: 100%; border: 0; background: #fff; text-align: left; padding: .58rem .7rem; border-radius: 8px; cursor: pointer; font-weight: 700; color: #111827; white-space: normal; line-height: 1.25; }
    .filter-suggestions button:hover { background: #eff6ff; color: #1d4ed8; }
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
  openDocSearchSuggestions = false;
  openCustomerSearchSuggestions = false;
  statusFilter: 'all' | 'open' | 'closed' | 'cancelled' = 'all';
  salesPersonCode = 0;

  filtered(): ReportingRecentDocument[] {
    const type = this.type;
    const docQ = this.docSearch.trim().toLowerCase();
    const custQ = this.customerSearch.trim().toLowerCase();
    const statusQ = this.statusFilter;
    return this.documents().filter((d) => {
      if (type !== 'all' && this.typeOf(d) !== type) return false;
      if (docQ && !String(d.docNum ?? '').toLowerCase().includes(docQ)) return false;
      if (custQ && !String(d.cardName ?? '').toLowerCase().includes(custQ)) return false;
      if (statusQ !== 'all' && this.normalizeStatus(d.status) !== statusQ) return false;
      return true;
    });
  }

  readonly docSuggestions = computed(() => this.documents().map((d) => String(d.docNum ?? '')).filter(Boolean).slice(0, 80));
  readonly customerSuggestions = computed(() => this.documents().map((d) => String(d.cardName ?? '')).filter(Boolean).slice(0, 80));

  @HostListener('document:click', ['$event'])
  closeSearchSuggestions(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('.search-box')) return;
    this.openDocSearchSuggestions = false;
    this.openCustomerSearchSuggestions = false;
  }

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

  filteredDocSuggestions(): string[] {
    return this.filterSuggestionValues(this.docSuggestions(), this.docSearch);
  }

  filteredCustomerSuggestions(): string[] {
    return this.filterSuggestionValues(this.customerSuggestions(), this.customerSearch);
  }

  selectDocSuggestion(value: string): void {
    this.docSearch = value;
    this.openDocSearchSuggestions = false;
  }

  selectCustomerSuggestion(value: string): void {
    this.customerSearch = value;
    this.openCustomerSearchSuggestions = false;
  }

  private filterSuggestionValues(values: string[], rawQuery: unknown): string[] {
    const query = String(rawQuery ?? '').trim().toLowerCase();
    return [...new Set(values)]
      .filter(value => !query || value.toLowerCase().includes(query))
      .slice(0, 80);
  }

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

  private normalizeStatus(rawStatus: string | null | undefined): 'open' | 'closed' | 'cancelled' {
    const status = String(rawStatus ?? '')
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '');

    if (
      status.includes('annul') ||
      status.includes('cancel') ||
      status === 'canceled' ||
      status === 'cancelled'
    ) {
      return 'cancelled';
    }

    if (
      status.includes('clotur') ||
      status.includes('ferme') ||
      status.includes('closed') ||
      status === 'close'
    ) {
      return 'closed';
    }

    return 'open';
  }

  private defaultMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }
}
