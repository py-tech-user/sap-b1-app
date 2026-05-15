import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { AdvancedReportingPayload, ReportingApiService } from '../../core/services/reporting-api.service';

type ModePeriode = 'month' | 'quarter' | 'year' | 'custom';
type MetriqueActive = 'chiffreAffaires' | 'devis' | 'commandes' | 'factures' | 'impayes';

@Component({
  selector: 'app-reporting',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  template: `
    <div class="reporting-page">
      <header class="header">
        <div>
          <h1>Rapport commercial détaillé</h1>
          <p>{{ data()?.periodLabel || etiquettePeriode() }}</p>
        </div>
        </header>

      <section class="panel filters">
        <label>Période
          <select [(ngModel)]="modePeriode" (change)="charger()">
            <option value="month">Mois</option>
            <option value="quarter">Trimestre</option>
            <option value="year">Année</option>
            <option value="custom">Personnalisée</option>
          </select>
        </label>

        @if (modePeriode === 'month') {
          <label>Mois <input type="month" [(ngModel)]="mois" (change)="charger()" /></label>
        }
        @if (modePeriode === 'quarter') {
          <label>Trimestre
            <select [(ngModel)]="trimestre" (change)="charger()">
              <option [ngValue]="1">Premier trimestre</option>
              <option [ngValue]="2">Deuxième trimestre</option>
              <option [ngValue]="3">Troisième trimestre</option>
              <option [ngValue]="4">Quatrième trimestre</option>
            </select>
          </label>
          <label>Année <input type="number" [(ngModel)]="annee" (change)="charger()" /></label>
        }
        @if (modePeriode === 'year') {
          <label>Année <input type="number" [(ngModel)]="annee" (change)="charger()" /></label>
        }
        @if (modePeriode === 'custom') {
          <label>Date de début <input type="date" [(ngModel)]="dateDebut" (change)="charger()" /></label>
          <label>Date de fin <input type="date" [(ngModel)]="dateFin" (change)="charger()" /></label>
        }

        @if (estModeAdministrateur()) {
          <label>Commercial
            <input list="liste-commerciaux" [(ngModel)]="rechercheCommercial" (input)="surSaisieCommercial()" placeholder="Nom du commercial" />
            <datalist id="liste-commerciaux">
              @for (s of suggestionsCommerciaux(); track s) { <option [value]="s"></option> }
            </datalist>
          </label>
        }

      </section>

      @if (chargement()) { <p>Chargement...</p> }

      @if (!chargement() && data(); as rapport) {
        <section class="panel">
          <h2>Indicateurs clés de performance</h2>
          <div class="kpi-grid">
            <article class="kpi" [class.active]="metriqueActive === 'chiffreAffaires'" (click)="activerMetrique('chiffreAffaires')">
              <h3>Chiffre d'affaires net</h3><p>{{ monnaie(rapport.kpis.netRevenue) }}</p>
            </article>
            <article class="kpi" [class.active]="metriqueActive === 'devis'" (click)="activerMetrique('devis')">
              <h3>Devis</h3><p>{{ rapport.kpis.quotesCount }}</p><span>{{ monnaie(rapport.kpis.quotesAmount) }}</span>
            </article>
            <article class="kpi" [class.active]="metriqueActive === 'commandes'" (click)="activerMetrique('commandes')">
              <h3>Commandes</h3><p>{{ rapport.kpis.ordersCount }}</p><span>{{ monnaie(rapport.kpis.ordersAmount) }}</span>
            </article>
            <article class="kpi" [class.active]="metriqueActive === 'factures'" (click)="activerMetrique('factures')">
              <h3>Factures</h3><p>{{ rapport.kpis.invoicesCount }}</p><span>{{ monnaie(rapport.kpis.invoicesAmount) }}</span>
            </article>
            <article class="kpi" [class.active]="metriqueActive === 'impayes'" (click)="activerMetrique('impayes')">
              <h3>Impayés</h3><p>{{ rapport.kpis.unpaidInvoicesCount }}</p><span>{{ monnaie(rapport.kpis.unpaidInvoicesAmount) }}</span>
            </article>
            <article class="kpi">
              <h3>Progression du chiffre d'affaires</h3><p>{{ variation(rapport.kpis.netRevenue, rapport.previousKpis.netRevenue) }}</p>
            </article>
          </div>
        </section>

        @if (estModeAdministrateur()) {
          <section class="panel">
            <h2>Répartition du chiffre d'affaires par commercial</h2>
            <div class="camembert-zone">
              <div class="pie" [style.background]="fondCamembertCommerciaux()"></div>
              <div class="legend">
                @for (l of legendeCommerciaux(); track l.code) {
                  <button type="button" class="legend-item" (click)="selectionnerCommercialDepuisLegende(l.code)">
                    <span class="dot" [style.background]="l.couleur"></span>
                    <span>{{ l.nom }} - {{ l.pourcentage }}</span>
                  </button>
                }
              </div>
            </div>
          </section>
        }

        <section class="panel">
          <h2>Classements globaux</h2>
          <table>
            <thead><tr><th>Article</th><th>Nombre de ventes</th><th>Chiffre d'affaires</th><th>Nombre de commerciaux</th><th>Client principal</th></tr></thead>
            <tbody>@for (p of rapport.topProducts; track p.itemCode) {<tr><td>{{ p.itemName || p.itemCode }}</td><td>{{ p.salesCount }}</td><td>{{ monnaie(p.revenue) }}</td><td>{{ p.salesPeopleCount }}</td><td>{{ p.mainClientName || '-' }}</td></tr>}</tbody>
          </table>
          <table>
            <thead><tr><th>Client</th><th>Chiffre d'affaires</th><th>Montant paye</th><th>Montant en attente</th><th>Nombre de contrats</th><th>Commercial principal</th></tr></thead>
            <tbody>@for (c of rapport.topClients; track c.cardCode) {<tr><td>{{ c.cardName }}</td><td>{{ monnaie(c.revenue) }}</td><td>{{ monnaie(c.paidAmount) }}</td><td>{{ monnaie(c.pendingAmount) }}</td><td>{{ c.contractsCount }}</td><td>{{ c.mainSalesPersonName || '-' }}</td></tr>}</tbody>
          </table>
        </section>

        <section class="panel">
          <h2>Analyses détaillées</h2>
          <table>
            <thead><tr><th>Article</th><th>Quantité vendue</th><th>Chiffre d'affaires</th><th>Nombre de clients</th><th>Client principal</th><th>Évolution</th></tr></thead>
            <tbody>@for (r of rapport.productDetails; track r.itemCode) {<tr><td>{{ r.itemName || r.itemCode }}</td><td>{{ r.quantitySold }}</td><td>{{ monnaie(r.revenue) }}</td><td>{{ r.clientsCount }}</td><td>{{ r.mainClientName || '-' }}</td><td>{{ pourcentage(r.trendPercent) }}</td></tr>}</tbody>
          </table>
          <table>
            <thead><tr><th>Client</th><th>Chiffre d'affaires</th><th>Montant payé</th><th>Montant en attente</th><th>Pourcentage de paiement</th><th>Nombre de contrats</th><th>Article favori</th></tr></thead>
            <tbody>@for (c of rapport.clientDetails; track c.cardCode) {<tr><td>{{ c.cardName }}</td><td>{{ monnaie(c.revenue) }}</td><td>{{ monnaie(c.paidAmount) }}</td><td>{{ monnaie(c.pendingAmount) }}</td><td>{{ pourcentage(c.paymentRate) }}</td><td>{{ c.contractsCount }}</td><td>{{ c.favoriteItemName || '-' }}</td></tr>}</tbody>
          </table>
          <table>
            <thead><tr><th>Partenaire</th><th>Chiffre d'affaires</th><th>Nombre de devis</th><th>Articles vendus</th><th>Commerciaux utilisateurs</th></tr></thead>
            <tbody>@for (p of rapport.partnerDetails; track p.partnerCode) {<tr><td>{{ p.partnerName }}</td><td>{{ monnaie(p.revenue) }}</td><td>{{ p.quotesCount }}</td><td>{{ p.productsCount }}</td><td>{{ p.salesPeopleCount }}</td></tr>}</tbody>
          </table>
        </section>

        <section class="panel" [class.highlight]="metriqueActive === 'impayes'">
          <h2>Impayés</h2>
          <table>
            <thead><tr><th>Client</th><th>Montant du</th><th>Article</th><th>Commercial</th><th>Date limite</th><th>Jours de retard</th></tr></thead>
            <tbody>@for (u of rapport.unpaidItems; track u.cardCode + '-' + u.itemCode) {<tr><td>{{ u.cardName }}</td><td>{{ monnaie(u.dueAmount) }}</td><td>{{ u.itemName || u.itemCode }}</td><td>{{ u.salesPersonName || ('#' + u.salesPersonCode) }}</td><td>{{ u.dueDate | date:'dd/MM/yyyy' }}</td><td>{{ u.overdueDays }}</td></tr>}</tbody>
          </table>
        </section>
      }
    </div>
  `,
  styles: [`
    .reporting-page { display: grid; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: center; gap: .8rem; flex-wrap: wrap; }
    .actions { display: flex; gap: .5rem; }
    .panel { background: #fff; border: 1px solid #dbe4ee; border-radius: 12px; padding: .9rem; }
    .filters { display: grid; grid-template-columns: repeat(4, minmax(170px, 1fr)); gap: .7rem; }
    label { display: grid; gap: .3rem; font-weight: 600; color: #2b3a4a; }
    input, select, button { border: 1px solid #cad7e5; border-radius: 8px; padding: .45rem .6rem; background: #fff; }
    .kpi-grid { display: grid; grid-template-columns: repeat(3, minmax(170px, 1fr)); gap: .7rem; }
    .kpi { border: 1px solid #e4ebf3; border-radius: 10px; padding: .7rem; background: linear-gradient(145deg, #fff, #f7fbff); cursor: pointer; }
    .kpi.active { border-color: #0ea5e9; box-shadow: 0 0 0 2px rgba(14,165,233,0.15); }
    .kpi h3 { margin: 0; font-size: .88rem; color: #52657d; }
    .kpi p { margin: .3rem 0; font-size: 1.2rem; font-weight: 700; }
    .camembert-zone { display: grid; grid-template-columns: 180px 1fr; gap: .8rem; align-items: center; }
    .pie { width: 160px; height: 160px; border-radius: 50%; border: 1px solid #e5e7eb; }
    .legend { display: grid; gap: .35rem; }
    .legend-item { display: flex; align-items: center; gap: .45rem; border: 1px solid #e5e7eb; background: #fff; text-align: left; }
    .dot { width: 10px; height: 10px; border-radius: 999px; display: inline-block; }
    table { width: 100%; border-collapse: collapse; margin-top: .7rem; }
    th, td { border-bottom: 1px solid #ecf1f6; padding: .45rem; text-align: left; font-size: .9rem; }
    th { background: #f8fbff; color: #51657e; }
    .muted { color: #6b7280; font-size: .9rem; }
    .highlight { border-color: #f59e0b; }
    @media (max-width: 1100px) { .filters, .kpi-grid { grid-template-columns: 1fr 1fr; } .camembert-zone { grid-template-columns: 1fr; } }
    @media (max-width: 680px) { .filters, .kpi-grid { grid-template-columns: 1fr; } }
  `]
})
export class ReportingComponent {
  private readonly auth = inject(AuthService);
  private readonly api = inject(ReportingApiService);

  readonly chargement = signal(false);
  readonly data = signal<AdvancedReportingPayload | null>(null);

  modePeriode: ModePeriode = 'month';
  mois = this.moisParDefaut();
  trimestre = 1;
  annee = new Date().getFullYear();
  dateDebut = this.premierJourMois();
  dateFin = this.dateDuJour();

  rechercheCommercial = '';
  metriqueActive: MetriqueActive = 'chiffreAffaires';

  private delaiRecherche: ReturnType<typeof setTimeout> | null = null;

  readonly estModeAdministrateur = computed(() => ['Admin', 'Manager'].includes(this.auth.role()));
  readonly commerciaux = computed(() => (this.data()?.teamMembers ?? []).filter(sp => String(sp.salesPersonName).trim().toLowerCase() !== 'administrateur'));
  readonly suggestionsCommerciaux = computed(() => this.commerciaux().map(s => s.salesPersonName));
  constructor() { this.charger(); }

  charger(): void {
    this.chargement.set(true);
    this.api.getAdvancedReporting({
      periodType: this.modePeriode,
      month: this.modePeriode === 'month' ? this.mois : undefined,
      quarter: this.modePeriode === 'quarter' ? this.trimestre : undefined,
      year: this.modePeriode === 'quarter' || this.modePeriode === 'year' ? this.annee : undefined,
      startDate: this.modePeriode === 'custom' ? this.dateDebut : undefined,
      endDate: this.modePeriode === 'custom' ? this.dateFin : undefined,
      salesPersonCode: this.codeCommercialSelectionne()
    }).subscribe({
      next: (res) => { this.data.set(res.data); this.chargement.set(false); },
      error: () => this.chargement.set(false)
    });
  }

  chargerAvecDelai(): void {
    if (this.delaiRecherche) clearTimeout(this.delaiRecherche);
    this.delaiRecherche = setTimeout(() => this.charger(), 300);
  }

  surSaisieCommercial(): void {
    this.chargerAvecDelai();
  }

  activerMetrique(metrique: MetriqueActive): void {
    this.metriqueActive = metrique;
  }

  legendeCommerciaux(): Array<{ code: number; nom: string; pourcentage: string; couleur: string }> {
    const palette = ['#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#14b8a6', '#f97316'];
    const liste = (this.data()?.topCommercials ?? []).slice(0, 8);
    const total = Math.max(1, liste.reduce((sum, row) => sum + Number(row.netRevenue || 0), 0));
    return liste.map((row, index) => ({
      code: row.salesPersonCode,
      nom: row.salesPersonName || `Commercial ${row.salesPersonCode}`,
      pourcentage: this.pourcentage((Number(row.netRevenue || 0) * 100) / total),
      couleur: palette[index % palette.length]
    }));
  }

  fondCamembertCommerciaux(): string {
    const palette = ['#0ea5e9', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899', '#14b8a6', '#f97316'];
    const liste = (this.data()?.topCommercials ?? []).slice(0, 8);
    if (liste.length === 0) return 'conic-gradient(#e5e7eb 0 100%)';
    const total = Math.max(1, liste.reduce((sum, row) => sum + Number(row.netRevenue || 0), 0));
    let depart = 0;
    const segments = liste.map((row, index) => {
      const valeur = (Number(row.netRevenue || 0) * 100) / total;
      const fin = depart + valeur;
      const segment = `${palette[index % palette.length]} ${depart.toFixed(2)}% ${fin.toFixed(2)}%`;
      depart = fin;
      return segment;
    });
    return `conic-gradient(${segments.join(', ')})`;
  }

  selectionnerCommercialDepuisLegende(code: number): void {
    if (!this.estModeAdministrateur()) return;
    const commercial = this.commerciaux().find((x) => x.salesPersonCode === code);
    if (!commercial) return;
    this.rechercheCommercial = commercial.salesPersonName;
    this.charger();
  }

  etiquettePeriode(): string {
    if (this.modePeriode === 'month') return `Période: ${this.mois}`;
    if (this.modePeriode === 'quarter') return `Période: trimestre ${this.trimestre} ${this.annee}`;
    if (this.modePeriode === 'year') return `Période: ${this.annee}`;
    return `Période: ${this.dateDebut} au ${this.dateFin}`;
  }

  variation(courant: number, precedent: number): string {
    const base = Number(precedent || 0);
    if (base <= 0) return '+100.00%';
    return this.pourcentage(((Number(courant || 0) - base) * 100) / base);
  }

  monnaie(valeur: number): string {
    return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(Number(valeur || 0))} MAD`;
  }

  pourcentage(valeur: number): string {
    return `${Number(valeur || 0).toFixed(2)}%`;
  }

  private codeCommercialSelectionne(): number | undefined {
    if (!this.estModeAdministrateur()) return undefined;
    const saisie = this.rechercheCommercial.trim().toLowerCase();
    if (!saisie) return undefined;
    const commercial = this.commerciaux().find((x) => x.salesPersonName.trim().toLowerCase().includes(saisie));
    return commercial?.salesPersonCode;
  }

  private moisParDefaut(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  private premierJourMois(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
  }

  private dateDuJour(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}

