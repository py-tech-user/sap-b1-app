import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ReportingApiService, CommercialReportingPayload } from '../../core/services/reporting-api.service';

@Component({
  selector: 'app-reporting',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, RouterLink],
  template: `
    <div class="reporting-page">
      <div class="header">
        <div>
          <h2>Reporting</h2>
          <h1>{{ title() }}</h1>
        </div>
      </div>

      <div class="filters">
        <label>
          Mois
          <input type="month" [(ngModel)]="month" (change)="load()"/>
        </label>
        @if (isAdminMode() && salesPeople().length > 0) {
          <label>
            Commercial
            <select [(ngModel)]="selectedSalesPersonCode" (change)="load()">
              <option [ngValue]="0">Toute l'equipe</option>
              @for (sp of salesPeople(); track sp.salesPersonCode) {
                <option [ngValue]="sp.salesPersonCode">{{ sp.salesPersonName }}</option>
              }
            </select>
          </label>
        }
      </div>

      @if (loading()) { <p>Chargement du reporting...</p> }

      @if (!loading() && data(); as report) {
        <div class="kpi-grid">
          <article class="kpi"><h3>Devis</h3><p>{{ report.kpis.quotesCount }}</p><span>{{ formatMoney(report.kpis.quotesAmount) }}</span></article>
          <article class="kpi"><h3>Commandes</h3><p>{{ report.kpis.ordersCount }}</p><span>{{ formatMoney(report.kpis.ordersAmount) }}</span></article>
          <article class="kpi"><h3>Factures</h3><p>{{ report.kpis.invoicesCount }}</p><span>{{ formatMoney(report.kpis.invoicesAmount) }}</span></article>
          <article class="kpi alert"><h3>Impayes</h3><p>{{ report.kpis.unpaidInvoicesCount }}</p><span>{{ formatMoney(report.kpis.unpaidInvoicesAmount) }}</span></article>
        </div>

        @if (isAdminMode()) {
          <section class="panel">
            <h2>Comparatif commerciaux</h2>
            @for (row of sortedTeamPerformances(); track row.salesPersonCode) {
              <div class="row">
                <div class="name">{{ row.salesPersonName || ('Commercial #' + row.salesPersonCode) }}</div>
                <div class="amount">{{ formatMoney(row.ordersAmount) }}</div>
              </div>
            }
            @if (showTopSalesPerson() && report.topSalesPerson && isTopVisibleInComparatif()) {
              <p class="tag">Meilleur commercial: <strong>{{ report.topSalesPerson.salesPersonName || ('#' + report.topSalesPerson.salesPersonCode) }}</strong></p>
            }
          </section>
        }

        <section class="panel">
          <h2>Derniers documents</h2>
          <table>
            <thead><tr><th>Type</th><th>N°</th><th>Client</th><th>Commercial</th><th>Date</th><th>Montant</th><th>Action</th></tr></thead>
            <tbody>
              @for (doc of report.recentDocuments; track doc.type + '-' + doc.docEntry) {
                <tr>
                  <td>{{ doc.type }}</td>
                  <td>{{ doc.docNum }}</td>
                  <td>{{ doc.cardName }}</td>
                  <td>{{ salesPersonNameByCode(doc.salesPersonCode) }}</td>
                  <td>{{ doc.date | date:'dd/MM/yyyy' }}</td>
                  <td>{{ formatMoney(doc.total) }}</td>
                  <td>
                    @if (documentDetailLink(doc.type, doc.docEntry); as link) {
                      <a class="btn-view" [routerLink]="link">Voir</a>
                    } @else {
                      <span>-</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </section>
      }
    </div>
  `,
  styles: [`
    .reporting-page { display: grid; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .subtitle { color: #526074; margin-top: 0.35rem; }
    .filters { display: flex; gap: 1rem; background: #fff; border: 1px solid #dde3ea; border-radius: 12px; padding: .75rem; flex-wrap: wrap; }
    label { display: grid; gap: .3rem; font-weight: 600; color: #2f3a49; }
    input, select, button { border: 1px solid #cfd8e3; border-radius: 8px; padding: .45rem .6rem; background: #fff; }
    .kpi-grid { display: grid; grid-template-columns: repeat(4, minmax(180px, 1fr)); gap: .8rem; }
    .kpi { background: linear-gradient(145deg, #fff, #f8fbff); border: 1px solid #dce6f2; border-radius: 12px; padding: 1rem; min-height: 120px; display: flex; flex-direction: column; justify-content: space-between; }
    .kpi.alert { border-color: #f1b2b2; background: linear-gradient(145deg, #fff, #fff6f6); }
    .kpi h3 { margin: 0; font-size: .9rem; color: #48607a; }
    .kpi p { margin: .4rem 0; font-size: 1.3rem; font-weight: 700; color: #152033; }
    .kpi span { color: #5f7186; font-size: .86rem; }
    .panel { background: #fff; border: 1px solid #dde3ea; border-radius: 12px; padding: .9rem; }
    .row {  display: flex; align-items: center; gap: 10px ; margin:.45rem 0;}
    .name { font-weight: 600; color: #2b3a4b; }
    .amount { color: #304257; font-weight: 600; }
    .tag { margin-top: .6rem; font-size: .92rem; color: #2b3a4b; }
    table { width: 100%; border-collapse: collapse; border-radius: 10px; overflow: hidden; }
    th, td { padding: .55rem; border-bottom: 1px solid #ecf0f5; text-align: left; font-size: .92rem; }
    th { color: #56687e; font-weight: 600; background: #f8fbff; }
    .btn-view { display: inline-block; padding: .35rem .6rem; border: 1px solid #cfd8e3; border-radius: 8px; text-decoration: none; color: #2f3a49; background: #fff; }
    .btn-view:hover { background: #f8fbff; }
    @media (max-width: 1100px) {
      .kpi-grid { grid-template-columns: repeat(2, minmax(180px, 1fr)); }
    }
    @media (max-width: 640px) {
      .kpi-grid { grid-template-columns: 1fr; }
      th, td { font-size: .86rem; padding: .45rem; }
    }
  `]
})
export class ReportingComponent {
  private readonly auth = inject(AuthService);
  private readonly api = inject(ReportingApiService);

  loading = signal(false);
  data = signal<CommercialReportingPayload | null>(null);
  month = this.defaultMonth();
  selectedSalesPersonCode = 0;

  isAdminMode = computed(() => ['Admin', 'Manager'].includes(this.auth.role()));
  salesPeople = computed(() =>
    (this.data()?.teamMembers ?? []).filter(sp => {
      const name = String(sp.salesPersonName ?? '').trim().toLowerCase();
      return name !== 'administrateur';
    })
  );
  showTopSalesPerson = computed(() => this.isAdminMode() && this.selectedSalesPersonCode === 0);
  sortedTeamPerformances = computed(() =>
    [...(this.data()?.teamPerformances ?? [])].sort((a, b) => (b.ordersAmount - a.ordersAmount) || (b.invoicesAmount - a.invoicesAmount))
  );
  title = computed(() => this.data()?.periodLabel ? `Periode: ${this.data()!.periodLabel}` : 'Analyse mensuelle');

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const salesperson = this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined;
    this.api.getCommercialReporting(this.month, salesperson).subscribe({
      next: (res) => {
        this.data.set(res.data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  formatMoney(value: number): string {
    const formatted = new Intl.NumberFormat('fr-FR', { minimumFractionDigits: 0, maximumFractionDigits: 0 })
      .format(Number(value ?? 0))
      .replace(/\u202f/g, ' ');
    return `${formatted} MAD`;
  }

  isTopVisibleInComparatif(): boolean {
    const top = this.data()?.topSalesPerson;
    const first = this.sortedTeamPerformances()[0];
    if (!top || !first) return false;
    return top.salesPersonCode === first.salesPersonCode;
  }

  documentDetailLink(type: string, docEntry: number): string[] | null {
    const normalized = String(type ?? '').trim().toLowerCase();
    if (!Number.isFinite(docEntry)) return null;

    if (normalized.includes('devis') || normalized.includes('quote')) {
      return ['/quotes', String(docEntry)];
    }
    if (normalized.includes('commande') || normalized.includes('order')) {
      return ['/orders', String(docEntry)];
    }
    if (normalized.includes('facture') || normalized.includes('invoice')) {
      return ['/factures', String(docEntry)];
    }

    return null;
  }

  salesPersonNameByCode(code: number): string {
    const numericCode = Number(code);
    if (!Number.isFinite(numericCode)) return '-';
    const match = (this.data()?.teamMembers ?? []).find(sp => sp.salesPersonCode === numericCode);
    return match?.salesPersonName || `#${numericCode}`;
  }

  private defaultMonth(): string {
    const d = new Date();
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    return `${y}-${m}`;
  }
}
