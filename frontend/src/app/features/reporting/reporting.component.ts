import { CommonModule } from '@angular/common';
import { Component, HostListener, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { PartnerApiService, PartnerRow } from '../../core/services/partner-api.service';
import {
  AdvancedReportingMonthlyRevenuePoint,
  AdvancedReportingPayload,
  AdvancedReportingTopPartner,
  AdvancedReportingTopProduct,
  PartnerCategoryShare,
  PartnerDocumentReportItem,
  PartnerFocusedReport,
  ReportingApiService,
  ReportingSalesPersonInfo
} from '../../core/services/reporting-api.service';

type ModePeriode = 'month' | 'quarter' | 'year' | 'custom';
type TableId = 'partnerProducts' | 'commercialProducts' | 'commercialPartners';
type SortDirection = 'asc' | 'desc';

type ChartPoint = {
  x: number;
  y: number;
  label: string;
  value: number;
};

@Component({
  selector: 'app-reporting',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="reporting-page">
      <section class="panel filters">
        <label>Période
          <select [(ngModel)]="modePeriode" (change)="charger()">
            <option value="month">Mois</option>
            <option value="quarter">Trimestre</option>
            <option value="year">Année</option>
            <option value="custom">Personnalisée</option>
          </select>
        </label>
        @if (modePeriode === 'month') { <label>Mois <input type="month" [(ngModel)]="mois" (change)="charger()" /></label> }
        @if (modePeriode === 'quarter') {
          <label>Trimestre
            <select [(ngModel)]="trimestre" (change)="charger()">
              <option [ngValue]="1">T1</option><option [ngValue]="2">T2</option><option [ngValue]="3">T3</option><option [ngValue]="4">T4</option>
            </select>
          </label>
          <label>Année <input type="number" [(ngModel)]="annee" (change)="charger()" /></label>
        }
        @if (modePeriode === 'year') { <label>Année <input type="number" [(ngModel)]="annee" (change)="charger()" /></label> }
        @if (modePeriode === 'custom') {
          <label>Du <input type="date" [(ngModel)]="dateDebut" (change)="charger()" /></label>
          <label>Au <input type="date" [(ngModel)]="dateFin" (change)="charger()" /></label>
        }

        <label class="search-box">Partenaire
          <input
            type="text"
            [(ngModel)]="recherchePartenaire"
            (input)="surRecherchePartenaire()"
            (focus)="ouvrirRecherchePartenaire()"
            (keydown)="replaceReportingSearchOnTyping($event, 'partenaire')"
            (blur)="fermerSuggestionsPlusTard('partenaire')"
            (keydown.enter)="selectionnerPartenaireSiUnique($event)"
            placeholder="Écrire un nom ou code partenaire"
          />
          @if (ouvrirPartenaires && partenairesFiltres().length) {
            <div class="suggestions">
              @for (p of partenairesFiltres(); track codePartenaire(p)) {
                <button type="button" (mousedown)="selectionnerPartenaire(p)">{{ nomPartenaire(p) }}</button>
              }
            </div>
          }
        </label>

        @if (estAdmin()) {
          <label class="search-box">Commercial
            <input
              type="text"
              [(ngModel)]="rechercheCommercial"
              (input)="surRechercheCommercial()"
              (focus)="ouvrirRechercheCommercial()"
              (keydown)="replaceReportingSearchOnTyping($event, 'commercial')"
              (blur)="fermerSuggestionsPlusTard('commercial')"
              (keydown.enter)="selectionnerCommercialSiUnique($event)"
              placeholder="Écrire le nom du commercial"
            />
            @if (ouvrirCommerciaux && commerciauxFiltres().length) {
              <div class="suggestions">
                @for (c of commerciauxFiltres(); track c.salesPersonCode) {
                  <button type="button" (mousedown)="selectionnerCommercial(c)">{{ c.salesPersonName }}</button>
                }
              </div>
            }
          </label>
        }

        <button type="button" (click)="charger()" [disabled]="chargement()">{{ chargement() ? 'Chargement...' : 'Actualiser' }}</button>
        @if (!estAdmin()) {
          <button type="button" (click)="monRapport()" [disabled]="chargement()">Mon Rapport</button>
        }
      </section>

      @if (erreur()) { <p class="alert">{{ erreur() }}</p> }

      @if (data(); as rapportTitre) {
        @if (partenaireSelectionne && rapportTitre.partnerReport; as partenaireTitre) {
          <section class="panel title-panel">
            <div>
              <p class="eyebrow">Rapport partenaire</p>
              <h2>{{ partenaireTitre.cardName || partenaireTitre.cardCode }}</h2>
              <p>Commercial affecté : <strong>{{ partenaireTitre.salesPersonName || 'Aucun commercial affecté' }}</strong></p>
              <p>{{ rapportTitre.periodLabel }}</p>
            </div>
          </section>
        } @else if (rapportCommercialVisible()) {
          <section class="panel title-panel">
            <div>
              <p class="eyebrow">Rapport commercial</p>
              <h2>{{ nomCommercialActif(rapportTitre) }}</h2>
              <p>{{ rapportTitre.periodLabel }}</p>
            </div>
          </section>
        }
      }

      @if (!partenaireSelectionne && !rapportCommercialActif()) {
        <section class="panel empty-state">
          <h2>Sélectionnez un partenaire ou un commercial</h2>
        </section>
      }

      @if (data(); as rapport) {
        @if (rapport.partnerReport; as partenaire) {
          <section class="partner-overview">
            <article class="panel partner-revenue-panel">
              <h3>CA total sur la période</h3>
              <strong>{{ monnaie(caPartenairePeriode(partenaire)) }}</strong>
              <span>CA en attente : {{ monnaie(caPartenaireEnAttente(partenaire)) }}</span>
            </article>
            <article class="panel docs-panel">
              <h3>Documents du partenaire</h3>
              <div class="doc-counts">
                <span>Devis <b>{{ nombreDocumentsPartenaire(partenaire, 'devis') }}</b></span>
                <span>BC <b>{{ nombreDocumentsPartenaire(partenaire, 'commande') }}</b></span>
                <span>BL <b>{{ nombreDocumentsPartenaire(partenaire, 'bon de livraison') }}</b></span>
                <span>Factures <b>{{ nombreDocumentsPartenaire(partenaire, 'facture') }}</b></span>
              </div>
            </article>
            <article class="panel">
              <h3>Taux de transformation</h3>
              <div class="funnel">
                @for (step of funnelPartenaire(partenaire); track step.label) {
                  <div class="funnel-step" [style.width.%]="step.width"><b>{{ step.label }}</b><span>{{ pourcentage(step.rate) }}</span></div>
                }
              </div>
            </article>
          </section>

          <section class="grid two">
            <article class="panel">
              <h3>Répartition des achats par catégorie</h3>
              <div class="chart-row">
                <div class="pie" [style.background]="pieBackground(partenaire.categoryShares)"></div>
                <div class="legend">
                  @for (row of partenaire.categoryShares; track row.categoryCode) {
                    <span><i [style.background]="couleur($index)"></i>{{ row.categoryName }} · {{ monnaie(row.revenue) }}</span>
                  } @empty { <p class="muted">Aucune donnée d'achat.</p> }
                </div>
              </div>
            </article>
            <article class="panel">
              <h3>Évolution du CA du partenaire sur la période</h3>
              <ng-container *ngTemplateOutlet="lineChart; context: { points: partenaire.yearlyRevenue, color: '#2563eb' }"></ng-container>
            </article>
          </section>

          <section class="panel">
            <h3>Articles favoris</h3>
            <div class="table-toolbar">
              <input type="text" [(ngModel)]="rechercheArticlesPartenaire" (ngModelChange)="resetTable('partnerProducts')" placeholder="Rechercher un article" />
            </div>
            <div class="table-scroll" (scroll)="chargerPlusTable($event, 'partnerProducts', produitsPartenaireTries(partenaire.topPurchasedProducts).length)">
              <table><thead><tr>
                <th><button type="button" class="sort-btn" (click)="trierTable('partnerProducts', 'item')">Article {{ indicateurTri('partnerProducts', 'item') }}</button></th>
                <th><button type="button" class="sort-btn" (click)="trierTable('partnerProducts', 'quantitySold')">Quantité {{ indicateurTri('partnerProducts', 'quantitySold') }}</button></th>
                <th><button type="button" class="sort-btn" (click)="trierTable('partnerProducts', 'revenue')">Montant {{ indicateurTri('partnerProducts', 'revenue') }}</button></th>
              </tr></thead><tbody>
                @for (p of produitsPartenaireVisibles(partenaire.topPurchasedProducts); track p.itemCode) { <tr><td>{{ p.itemName || p.itemCode }}</td><td>{{ nombre(p.quantitySold) }}</td><td>{{ monnaie(p.revenue) }}</td></tr> }
                @empty { <tr><td colspan="3">Aucun article trouvé.</td></tr> }
              </tbody></table>
            </div>
          </section>
        }

        @if (rapportCommercialVisible()) {
          <section class="commercial-overview">
            <article class="panel target-panel">
              <h3>CA réalisé vs objectif</h3>
              <div class="gauge" [style.background]="gaugeBackground(rapport.kpis.targetAchievementRate)"><span>{{ pourcentage(rapport.kpis.targetAchievementRate) }}</span></div>
              <p class="center">{{ monnaie(rapport.kpis.collectedRevenue) }} encaissé / objectif {{ monnaie(rapport.kpis.periodTarget) }}</p>
            </article>
            <div class="side-stack">
              <article class="panel docs-panel">
                <h3>Documents émis</h3>
                <div class="doc-counts">
                  <span>Devis <b>{{ rapport.kpis.quotesCount }}</b></span>
                  <span>BC <b>{{ rapport.kpis.ordersCount }}</b></span>
                  <span>BL <b>{{ rapport.kpis.deliveryNotesCount }}</b></span>
                  <span>Factures <b>{{ rapport.kpis.invoicesCount }}</b></span>
                </div>
              </article>
              <article class="panel">
                <h3>Taux de transformation devis → BC → BL → facture</h3>
                <div class="funnel">
                  @for (step of funnel(rapport); track step.label) {
                    <div class="funnel-step" [style.width.%]="step.width"><b>{{ step.label }}</b><span>{{ pourcentage(step.rate) }}</span></div>
                  }
                </div>
              </article>
            </div>
          </section>

          <section class="panel">
            <h3>Évolution du CA du commercial sur la période</h3>
            <ng-container *ngTemplateOutlet="lineChart; context: { points: rapport.monthlyRevenue, color: '#16a34a' }"></ng-container>
          </section>

          <section class="grid two">
            <article class="panel">
              <h3>Articles les plus vendus</h3>
              <div class="table-toolbar search-box">
                <input type="text" [(ngModel)]="rechercheArticlesCommercial" (ngModelChange)="surRechercheArticlesCommercial()" (focus)="ouvrirArticlesCommercial = true" placeholder="Rechercher un article" />
                @if (ouvrirArticlesCommercial && suggestionsArticlesCommercial(rapport.topProducts).length) {
                  <div class="suggestions table-suggestions">
                    @for (p of suggestionsArticlesCommercial(rapport.topProducts); track p.itemCode) {
                      <button type="button" (mousedown)="selectionnerArticleCommercial(p)">{{ p.itemName || p.itemCode }}</button>
                    }
                  </div>
                }
              </div>
              <div class="table-scroll" (scroll)="chargerPlusTable($event, 'commercialProducts', produitsCommercialTries(rapport.topProducts).length)">
                <table><thead><tr>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialProducts', 'item')">Article {{ indicateurTri('commercialProducts', 'item') }}</button></th>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialProducts', 'quantitySold')">Quantité {{ indicateurTri('commercialProducts', 'quantitySold') }}</button></th>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialProducts', 'revenue')">CA {{ indicateurTri('commercialProducts', 'revenue') }}</button></th>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialProducts', 'clientsCount')">Clients {{ indicateurTri('commercialProducts', 'clientsCount') }}</button></th>
                </tr></thead><tbody>
                  @for (p of produitsCommercialVisibles(rapport.topProducts); track p.itemCode) { <tr><td>{{ p.itemName || p.itemCode }}</td><td>{{ nombre(p.quantitySold) }}</td><td>{{ monnaie(p.revenue) }}</td><td>{{ p.clientsCount }}</td></tr> }
                  @empty { <tr><td colspan="4">Aucun article vendu sur cette période.</td></tr> }
                </tbody></table>
              </div>
            </article>
            <article class="panel">
              <h3>Top partenaires</h3>
              <div class="table-toolbar search-box">
                <input type="text" [(ngModel)]="recherchePartenairesCommercial" (ngModelChange)="surRecherchePartenairesCommercial()" (focus)="ouvrirPartenairesCommercialTable = true" placeholder="Rechercher un partenaire" />
                @if (ouvrirPartenairesCommercialTable && suggestionsPartenairesCommercial(rapport.topPartners).length) {
                  <div class="suggestions table-suggestions">
                    @for (p of suggestionsPartenairesCommercial(rapport.topPartners); track p.partnerCode) {
                      <button type="button" (mousedown)="selectionnerPartenaireCommercialTable(p)">{{ p.partnerName || p.partnerCode }}</button>
                    }
                  </div>
                }
              </div>
              <div class="table-scroll" (scroll)="chargerPlusTable($event, 'commercialPartners', partenairesCommercialTries(rapport.topPartners).length)">
                <table><thead><tr>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialPartners', 'partner')">Partenaire {{ indicateurTri('commercialPartners', 'partner') }}</button></th>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialPartners', 'revenue')">CA {{ indicateurTri('commercialPartners', 'revenue') }}</button></th>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialPartners', 'quotesCount')">Devis {{ indicateurTri('commercialPartners', 'quotesCount') }}</button></th>
                  <th><button type="button" class="sort-btn" (click)="trierTable('commercialPartners', 'productsCount')">Articles {{ indicateurTri('commercialPartners', 'productsCount') }}</button></th>
                </tr></thead><tbody>
                  @for (p of partenairesCommercialVisibles(rapport.topPartners); track p.partnerCode) { <tr><td>{{ p.partnerName || p.partnerCode }}</td><td>{{ monnaie(p.revenue) }}</td><td>{{ p.quotesCount }}</td><td>{{ p.productsCount }}</td></tr> }
                  @empty { <tr><td colspan="4">Aucun partenaire sur cette période.</td></tr> }
                </tbody></table>
              </div>
            </article>
          </section>
        }
      }

      <ng-template #lineChart let-points="points" let-color="color">
        <div class="line-wrap">
          <svg class="line-chart" viewBox="0 0 720 260" preserveAspectRatio="none">
            @for (tick of yTicks(points); track tick.label) {
              <line x1="70" [attr.y1]="tick.y" x2="700" [attr.y2]="tick.y" stroke="#e2e8f0" stroke-width="1"></line>
              <text x="8" [attr.y]="tick.y + 4" class="axis-label">{{ tick.label }}</text>
            }
            <polyline [attr.points]="pointsCourbe(points)" fill="none" [attr.stroke]="color" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"></polyline>
            @for (pt of pointsAvecCoord(points); track pt.label) {
              <g class="point">
                <circle [attr.cx]="pt.x" [attr.cy]="pt.y" r="5" [attr.fill]="color"></circle>
                <title>{{ pt.label }} : {{ monnaie(pt.value) }}</title>
              </g>
            }
            @for (pt of pointsAvecCoord(points); track 'x-' + pt.label) {
              <text [attr.x]="pt.x" y="246" class="x-label">{{ etiquetteMois(pt.label) }}</text>
            }
          </svg>
        </div>
      </ng-template>
    </div>
  `,
  styles: [`
    .reporting-page { display: grid; gap: 1rem; color: #172033; }
    .panel { background: #fff; border: 1px solid #dbe4ee; border-radius: 18px; padding: 1rem; box-shadow: 0 8px 24px rgba(15,23,42,.05); }
    h2, h3, p { margin-top: 0; } h3 { margin-bottom: .8rem; }
    .eyebrow { color: #2563eb; text-transform: uppercase; letter-spacing: .08em; font-size: .75rem; font-weight: 800; margin-bottom: .25rem; }
    button, select, input { border: 1px solid #c9d7e8; border-radius: 10px; padding: .55rem .7rem; background: #fff; }
    .filters { display: grid; grid-template-columns: repeat(5, minmax(160px, 1fr)); gap: .75rem; align-items: start; }
    label { display: grid; gap: .35rem; color: #334155; font-weight: 700; }
    .search-box { position: relative; }
    .suggestions { position: absolute; z-index: 20; left: 0; right: 0; top: calc(100% + 4px); display: grid; max-height: 260px; overflow: auto; background: #fff; border: 1px solid #cbd5e1; border-radius: 12px; box-shadow: 0 18px 38px rgba(15,23,42,.16); padding: .35rem; }
    .suggestions button { border: 0; border-radius: 8px; text-align: left; padding: .55rem .65rem; cursor: pointer; }
    .suggestions button:hover { background: #eff6ff; }
    .grid { display: grid; gap: 1rem; } .two { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .title-panel { display: flex; justify-content: center; gap: 1rem; align-items: center; text-align: center; }
    .title-panel > div { width: 100%; }
    .partner-overview { display: grid; grid-template-columns: minmax(220px, .8fr) minmax(260px, 1fr) minmax(320px, 1.2fr); gap: 1rem; align-items: stretch; }
    .partner-revenue-panel { display: grid; align-content: center; gap: .35rem; }
    .partner-revenue-panel strong { font-size: 1.85rem; color: #111827; }
    .partner-revenue-panel span { color: #64748b; font-weight: 700; }
    .partner-overview .docs-panel { align-self: stretch; display: grid; align-content: center; }
    .commercial-overview { display: grid; grid-template-columns: minmax(0, 1fr) minmax(280px, .9fr); gap: 1rem; align-items: start; }
    .side-stack { display: grid; gap: 1rem; }
    .target-panel { min-height: 100%; display: grid; align-content: center; }
    .target-panel .gauge { width: 230px; height: 230px; }
    .target-panel .gauge span { width: 152px; height: 152px; font-size: 1.55rem; }
    .docs-panel { align-self: start; padding: .85rem; }
    .docs-panel h3 { text-align: center; margin-bottom: .65rem; }
    .money-strip { display: flex; gap: .6rem; flex-wrap: wrap; }
    .doc-counts { display: grid; grid-template-columns: repeat(2, minmax(88px, 1fr)); gap: .5rem; max-width: 360px; margin: 0 auto; }
    .money-strip span, .doc-counts span { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: .6rem .8rem; }
    .doc-counts span { text-align: center; padding: .5rem .55rem; font-size: .82rem; }
    .money-strip b, .doc-counts b { display: block; }
    .doc-counts b { font-size: 1.2rem; line-height: 1.15; margin-top: .15rem; }
    .chart-row { display: grid; grid-template-columns: 190px 1fr; gap: 1rem; align-items: center; }
    .pie { width: 175px; height: 175px; border-radius: 999px; border: 1px solid #e2e8f0; box-shadow: inset 0 0 0 18px #fff; }
    .legend { display: grid; gap: .45rem; } .legend span { display: flex; align-items: center; gap: .45rem; } .legend i { width: 11px; height: 11px; border-radius: 999px; display: inline-block; }
    .line-chart { width: 100%; height: 260px; border-radius: 14px; background: #fbfdff; overflow: visible; }
    .axis-label { font-size: 11px; fill: #64748b; } .x-label { font-size: 10px; fill: #64748b; text-anchor: middle; }
    .point circle { cursor: help; filter: drop-shadow(0 2px 4px rgba(15,23,42,.22)); }
    .hint { color: #64748b; font-size: .82rem; margin: .35rem 0 0; }
    .gauge { width: 190px; height: 190px; border-radius: 50%; display: grid; place-items: center; margin: 0 auto; } .gauge span { width: 128px; height: 128px; border-radius: 50%; display: grid; place-items: center; background: #fff; font-weight: 900; font-size: 1.4rem; }
    .center { text-align: center; color: #475569; }
    .funnel { display: grid; gap: .55rem; justify-items: center; }
    .funnel-step { min-width: min(100%, 520px); max-width: 100%; background: linear-gradient(90deg,#2563eb,#38bdf8); color: #fff; border-radius: 12px; padding: .65rem .85rem; display: flex; justify-content: space-between; align-items: center; gap: .8rem; }
    .funnel-step b, .funnel-step span { white-space: nowrap; }
    .table-toolbar { margin-bottom: .65rem; display: flex; justify-content: flex-start; position: relative; width: min(100%, 320px); }
    .table-toolbar input { width: min(100%, 320px); }
    .table-suggestions { top: calc(100% + 4px); width: 100%; }
    .table-scroll { height: 360px; overflow: auto; border: 1px solid #edf2f7; border-radius: 12px; }
    table { width: 100%; border-collapse: collapse; } th, td { border-bottom: 1px solid #edf2f7; padding: .5rem; text-align: left; font-size: .9rem; } th { background: #f8fafc; color: #475569; }
    .table-scroll thead th { position: sticky; top: 0; z-index: 1; }
    .sort-btn { border: 0; background: transparent; padding: 0; cursor: pointer; color: inherit; font: inherit; font-weight: 800; text-align: left; }
    .sort-btn:hover { color: #2563eb; }
    .alert { background: #fff7ed; border: 1px solid #fed7aa; color: #9a3412; padding: .75rem; border-radius: 12px; } .muted, .empty-state p { color: #64748b; }
    @media (max-width: 1200px) { .filters { grid-template-columns: repeat(2, minmax(0,1fr)); } .two, .commercial-overview, .partner-overview { grid-template-columns: 1fr; } }
    @media (max-width: 700px) { .title-panel { display: grid; } .filters, .chart-row { grid-template-columns: 1fr; } }
  `]
})
export class ReportingComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly api = inject(ReportingApiService);
  private readonly partnerApi = inject(PartnerApiService);

  readonly chargement = signal(false);
  readonly erreur = signal('');
  readonly data = signal<AdvancedReportingPayload | null>(null);
  readonly partenaires = signal<PartnerRow[]>([]);

  modePeriode: ModePeriode = 'month';
  mois = this.moisParDefaut();
  trimestre = Math.floor(new Date().getMonth() / 3) + 1;
  annee = new Date().getFullYear();
  dateDebut = this.premierJourMois();
  dateFin = this.dateDuJour();

  partenaireSelectionne = '';
  commercialSelectionne?: number;
  recherchePartenaire = '';
  rechercheCommercial = '';
  rechercheArticlesPartenaire = '';
  rechercheArticlesCommercial = '';
  recherchePartenairesCommercial = '';
  ouvrirPartenaires = false;
  ouvrirCommerciaux = false;
  ouvrirArticlesCommercial = false;
  ouvrirPartenairesCommercialTable = false;
  private remplacerPartenaireAuClavier = false;
  private remplacerCommercialAuClavier = false;
  private readonly tablePageSize = 20;
  private tableVisible: Record<TableId, number> = {
    partnerProducts: this.tablePageSize,
    commercialProducts: this.tablePageSize,
    commercialPartners: this.tablePageSize
  };
  private tableSort: Record<TableId, { key: string; direction: SortDirection }> = {
    partnerProducts: { key: 'item', direction: 'asc' },
    commercialProducts: { key: 'revenue', direction: 'desc' },
    commercialPartners: { key: 'revenue', direction: 'desc' }
  };

  readonly estAdmin = computed(() => this.auth.hasRole(['Admin', 'Manager']));
  readonly commerciaux = computed(() => (this.data()?.teamMembers ?? []).filter(c => {
    const name = String(c.salesPersonName ?? '').trim().toLowerCase();
    return c.salesPersonCode > 0 && name !== 'administrateur';
  }));

  @HostListener('document:click', ['$event'])
  fermerSuggestionsSiClicDehors(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.search-box')) {
      this.ouvrirPartenaires = false;
      this.ouvrirCommerciaux = false;
      this.ouvrirArticlesCommercial = false;
      this.ouvrirPartenairesCommercialTable = false;
    }
  }

  ngOnInit(): void {
    this.chargerPartenaires();
    this.charger();
  }

  chargerPartenaires(): void {
    this.partnerApi.getAll(1, 10000).subscribe({
      next: (res) => this.partenaires.set(res.items ?? []),
      error: () => this.erreur.set('Impossible de charger les partenaires.')
    });
  }

  charger(): void {
    this.chargement.set(true);
    this.erreur.set('');
    this.api.getAdvancedReporting({
      periodType: this.modePeriode,
      month: this.modePeriode === 'month' ? this.mois : undefined,
      quarter: this.modePeriode === 'quarter' ? this.trimestre : undefined,
      year: this.modePeriode === 'quarter' || this.modePeriode === 'year' ? this.annee : undefined,
      startDate: this.modePeriode === 'custom' ? this.dateDebut : undefined,
      endDate: this.modePeriode === 'custom' ? this.dateFin : undefined,
      salesPersonCode: this.estAdmin() ? this.commercialSelectionne : undefined,
      cardCode: this.partenaireSelectionne || undefined,
      detailsLimit: 200
    }).subscribe({
      next: (res) => {
        this.data.set(res.data);
        this.resetAllTables();
        if (!this.estAdmin() && !this.rechercheCommercial) {
          this.rechercheCommercial = res.data.selectedSalesPersonName || '';
        }
        this.chargement.set(false);
      },
      error: () => { this.erreur.set('Impossible de charger le reporting.'); this.chargement.set(false); }
    });
  }

  partenairesFiltres(): PartnerRow[] {
    const query = this.normalize(this.recherchePartenaire);
    return this.partenaires()
      .filter(p => !query || this.normalize(this.nomPartenaire(p)).includes(query) || this.normalize(this.codePartenaire(p)).includes(query))
      .sort((a, b) => this.nomPartenaire(a).localeCompare(this.nomPartenaire(b), 'fr'));
  }

  commerciauxFiltres(): ReportingSalesPersonInfo[] {
    const query = this.normalize(this.rechercheCommercial);
    return this.commerciaux()
      .filter(c => !query || this.normalize(c.salesPersonName).includes(query) || String(c.salesPersonCode).includes(query))
      .sort((a, b) => a.salesPersonName.localeCompare(b.salesPersonName, 'fr'));
  }

  ouvrirRecherchePartenaire(): void {
    this.remplacerPartenaireAuClavier = !!this.partenaireSelectionne;
    this.ouvrirPartenaires = true;
  }

  ouvrirRechercheCommercial(): void {
    this.remplacerCommercialAuClavier = !!this.commercialSelectionne;
    this.ouvrirCommerciaux = true;
  }

  replaceReportingSearchOnTyping(event: KeyboardEvent, field: 'partenaire' | 'commercial'): void {
    const shouldReplace = field === 'partenaire'
      ? this.remplacerPartenaireAuClavier
      : this.remplacerCommercialAuClavier;
    if (!shouldReplace || event.ctrlKey || event.metaKey || event.altKey) return;

    if (event.key === 'Backspace' || event.key === 'Delete') {
      event.preventDefault();
      if (field === 'partenaire') {
        this.recherchePartenaire = '';
        this.surRecherchePartenaire();
      } else {
        this.rechercheCommercial = '';
        this.surRechercheCommercial();
      }
      return;
    }

    if (event.key.length !== 1) return;
    event.preventDefault();
    if (field === 'partenaire') {
      this.recherchePartenaire = event.key;
      this.surRecherchePartenaire();
    } else {
      this.rechercheCommercial = event.key;
      this.surRechercheCommercial();
    }
  }

  surRecherchePartenaire(): void {
    this.remplacerPartenaireAuClavier = false;
    this.partenaireSelectionne = '';
    if (this.recherchePartenaire.trim()) {
      this.commercialSelectionne = undefined;
      this.rechercheCommercial = '';
      this.ouvrirCommerciaux = false;
      this.ouvrirPartenaires = true;
      return;
    }

    this.ouvrirPartenaires = true;
    this.charger();
  }

  surRechercheCommercial(): void {
    this.remplacerCommercialAuClavier = false;
    this.commercialSelectionne = undefined;
    if (this.rechercheCommercial.trim()) {
      this.partenaireSelectionne = '';
      this.recherchePartenaire = '';
      this.ouvrirPartenaires = false;
      this.ouvrirCommerciaux = true;
      return;
    }

    this.ouvrirCommerciaux = true;
    this.charger();
  }

  selectionnerPartenaire(row: PartnerRow): void {
    this.commercialSelectionne = undefined;
    this.rechercheCommercial = '';
    this.ouvrirCommerciaux = false;
    this.partenaireSelectionne = this.codePartenaire(row);
    this.recherchePartenaire = this.nomPartenaire(row);
    this.ouvrirPartenaires = false;
    this.charger();
  }

  selectionnerCommercial(row: ReportingSalesPersonInfo): void {
    this.partenaireSelectionne = '';
    this.recherchePartenaire = '';
    this.ouvrirPartenaires = false;
    this.commercialSelectionne = row.salesPersonCode;
    this.rechercheCommercial = row.salesPersonName;
    this.ouvrirCommerciaux = false;
    this.charger();
  }

  monRapport(): void {
    this.partenaireSelectionne = '';
    this.recherchePartenaire = '';
    this.ouvrirPartenaires = false;
    this.commercialSelectionne = undefined;
    this.rechercheCommercial = this.auth.currentUser()?.fullName || this.data()?.selectedSalesPersonName || '';
    this.charger();
  }

  surRechercheArticlesCommercial(): void {
    this.ouvrirArticlesCommercial = true;
    this.ouvrirPartenairesCommercialTable = false;
    this.resetTable('commercialProducts');
  }

  surRecherchePartenairesCommercial(): void {
    this.ouvrirPartenairesCommercialTable = true;
    this.ouvrirArticlesCommercial = false;
    this.resetTable('commercialPartners');
  }

  selectionnerArticleCommercial(row: AdvancedReportingTopProduct): void {
    this.rechercheArticlesCommercial = row.itemName || row.itemCode;
    this.ouvrirArticlesCommercial = false;
    this.resetTable('commercialProducts');
  }

  selectionnerPartenaireCommercialTable(row: AdvancedReportingTopPartner): void {
    this.recherchePartenairesCommercial = row.partnerName || row.partnerCode;
    this.ouvrirPartenairesCommercialTable = false;
    this.resetTable('commercialPartners');
  }

  suggestionsArticlesCommercial(rows: AdvancedReportingTopProduct[]): AdvancedReportingTopProduct[] {
    return this.produitsCommercialTries(rows);
  }

  suggestionsPartenairesCommercial(rows: AdvancedReportingTopPartner[]): AdvancedReportingTopPartner[] {
    return this.partenairesCommercialTries(rows);
  }

  selectionnerPartenaireSiUnique(event: Event): void {
    const rows = this.partenairesFiltres();
    if (rows.length === 1) {
      event.preventDefault();
      this.selectionnerPartenaire(rows[0]);
    }
  }

  selectionnerCommercialSiUnique(event: Event): void {
    const rows = this.commerciauxFiltres();
    if (rows.length === 1) {
      event.preventDefault();
      this.selectionnerCommercial(rows[0]);
    }
  }

  fermerSuggestionsPlusTard(type: 'partenaire' | 'commercial'): void {
    setTimeout(() => {
      if (type === 'partenaire') {
        this.ouvrirPartenaires = false;
      } else {
        this.ouvrirCommerciaux = false;
      }
    }, 120);
  }

  rapportCommercialActif(): boolean {
    return !this.estAdmin() || !!this.commercialSelectionne;
  }

  rapportCommercialVisible(): boolean {
    return !this.partenaireSelectionne && this.rapportCommercialActif();
  }

  nomCommercialActif(rapport: AdvancedReportingPayload): string {
    return rapport.selectedSalesPersonName || this.rechercheCommercial || 'Commercial sélectionné';
  }

  trierTable(table: TableId, key: string): void {
    const current = this.tableSort[table];
    this.tableSort[table] = {
      key,
      direction: current.key === key && current.direction === 'asc' ? 'desc' : 'asc'
    };
    this.resetTable(table);
  }

  indicateurTri(table: TableId, key: string): string {
    const current = this.tableSort[table];
    if (current.key !== key) return '';
    return current.direction === 'asc' ? '↑' : '↓';
  }

  resetTable(table: TableId): void {
    this.tableVisible[table] = this.tablePageSize;
  }

  chargerPlusTable(event: Event, table: TableId, total: number): void {
    const el = event.target as HTMLElement | null;
    if (!el || this.tableVisible[table] >= total) return;
    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    if (distanceFromBottom <= 80) {
      this.tableVisible[table] = Math.min(total, this.tableVisible[table] + this.tablePageSize);
    }
  }

  produitsPartenaireTries(rows: AdvancedReportingTopProduct[]): AdvancedReportingTopProduct[] {
    return this.sortProducts(this.filterProducts(rows, this.rechercheArticlesPartenaire), 'partnerProducts');
  }

  produitsPartenaireVisibles(rows: AdvancedReportingTopProduct[]): AdvancedReportingTopProduct[] {
    return this.produitsPartenaireTries(rows).slice(0, this.tableVisible.partnerProducts);
  }

  produitsCommercialTries(rows: AdvancedReportingTopProduct[]): AdvancedReportingTopProduct[] {
    return this.sortProducts(this.filterProducts(rows, this.rechercheArticlesCommercial), 'commercialProducts');
  }

  produitsCommercialVisibles(rows: AdvancedReportingTopProduct[]): AdvancedReportingTopProduct[] {
    return this.produitsCommercialTries(rows).slice(0, this.tableVisible.commercialProducts);
  }

  partenairesCommercialTries(rows: AdvancedReportingTopPartner[]): AdvancedReportingTopPartner[] {
    const query = this.normalize(this.recherchePartenairesCommercial);
    const filtered = rows.filter(row =>
      !query ||
      this.normalize(row.partnerName).includes(query) ||
      this.normalize(row.partnerCode).includes(query)
    );
    return this.sortPartners(filtered, 'commercialPartners');
  }

  partenairesCommercialVisibles(rows: AdvancedReportingTopPartner[]): AdvancedReportingTopPartner[] {
    return this.partenairesCommercialTries(rows).slice(0, this.tableVisible.commercialPartners);
  }

  codePartenaire(row: PartnerRow): string { return String(row.CardCode ?? (row as any).cardCode ?? '').trim(); }
  nomPartenaire(row: PartnerRow): string { const code = this.codePartenaire(row); return `${String(row.CardName ?? (row as any).cardName ?? code).trim()} (${code})`; }
  monnaie(value: number | null | undefined): string { return new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'MAD', maximumFractionDigits: 0 }).format(Number(value ?? 0)); }
  nombre(value: number | null | undefined): string { return new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 2 }).format(Number(value ?? 0)); }
  pourcentage(value: number | null | undefined): string { return `${this.nombre(value)} %`; }

  caPartenairePeriode(partenaire: PartnerFocusedReport): number {
    return this.documentsPartenaire(partenaire, 'facture')
      .reduce((sum, doc) => sum + Number(doc.total || 0), 0);
  }

  caPartenaireEnAttente(partenaire: PartnerFocusedReport): number {
    return partenaire.documents
      .filter(doc => {
        const type = this.normalize(doc.type);
        const status = this.normalize(doc.status);
        return (type === 'commande' || type === 'bon de livraison') && status === 'ouvert';
      })
      .reduce((sum, doc) => sum + Number(doc.total || 0), 0);
  }

  nombreDocumentsPartenaire(partenaire: PartnerFocusedReport, type: string): number {
    return this.documentsPartenaire(partenaire, type).length;
  }

  funnelPartenaire(partenaire: PartnerFocusedReport): Array<{ label: string; count: string; rate: number; width: number }> {
    const quotes = this.nombreDocumentsPartenaire(partenaire, 'devis');
    const orders = this.nombreDocumentsPartenaire(partenaire, 'commande');
    const deliveries = this.nombreDocumentsPartenaire(partenaire, 'bon de livraison');
    const invoices = this.nombreDocumentsPartenaire(partenaire, 'facture');
    const rate = (next: number, prev: number) => prev <= 0 ? 0 : Math.round((next * 10000) / prev) / 100;
    const widthFromRate = (value: number) => Math.max(42, Math.min(100, Number(value || 0)));
    const quoteToOrder = rate(orders, quotes);
    const orderToDelivery = rate(deliveries, orders);
    const deliveryToInvoice = rate(invoices, deliveries);
    return [
      { label: 'Devis -> BC', count: `${orders} / ${quotes}`, rate: quoteToOrder, width: widthFromRate(quoteToOrder) },
      { label: 'BC -> BL', count: `${deliveries} / ${orders}`, rate: orderToDelivery, width: widthFromRate(orderToDelivery) },
      { label: 'BL -> Factures', count: `${invoices} / ${deliveries}`, rate: deliveryToInvoice, width: widthFromRate(deliveryToInvoice) }
    ];
  }

  pieBackground(rows: PartnerCategoryShare[]): string {
    const total = rows.reduce((sum, row) => sum + Number(row.revenue || 0), 0);
    if (total <= 0) return '#e2e8f0';
    let cursor = 0;
    const parts = rows.map((row, index) => {
      const start = cursor;
      cursor += (Number(row.revenue || 0) / total) * 100;
      return `${this.couleur(index)} ${start}% ${cursor}%`;
    });
    return `conic-gradient(${parts.join(', ')})`;
  }

  couleur(index: number): string {
    const palette = [
      '#2563eb', '#16a34a', '#f97316', '#9333ea', '#dc2626',
      '#0891b2', '#ca8a04', '#475569', '#db2777', '#65a30d',
      '#7c3aed', '#ea580c', '#0f766e', '#be123c', '#4f46e5',
      '#84cc16', '#c026d3', '#0284c7', '#a16207', '#15803d',
      '#e11d48', '#0369a1', '#9333ea', '#854d0e', '#0d9488',
      '#b91c1c', '#6d28d9', '#1d4ed8', '#4d7c0f', '#c2410c'
    ];
    return palette[index % palette.length];
  }

  pointsCourbe(points: AdvancedReportingMonthlyRevenuePoint[]): string {
    return this.pointsAvecCoord(points).map(p => `${p.x},${p.y}`).join(' ');
  }

  pointsAvecCoord(points: AdvancedReportingMonthlyRevenuePoint[]): ChartPoint[] {
    const rows = points?.length ? points : [];
    const max = Math.max(...rows.map(p => Number(p.revenue || 0)), 1);
    const width = 630;
    const height = 180;
    return rows.map((p, index) => ({
      label: p.monthKey,
      value: Number(p.revenue || 0),
      x: 70 + (rows.length <= 1 ? width / 2 : index * (width / (rows.length - 1))),
      y: 25 + height - ((Number(p.revenue || 0) / max) * height)
    }));
  }

  yTicks(points: AdvancedReportingMonthlyRevenuePoint[]): Array<{ y: number; label: string }> {
    const max = Math.max(...(points ?? []).map(p => Number(p.revenue || 0)), 1);
    return [1, 0.75, 0.5, 0.25, 0].map(ratio => ({
      y: 25 + (1 - ratio) * 180,
      label: this.compactMoney(max * ratio)
    }));
  }

  compactMoney(value: number): string {
    return new Intl.NumberFormat('fr-FR', { notation: 'compact', maximumFractionDigits: 1 }).format(value) + ' MAD';
  }

  etiquetteMois(value: string): string {
    const parts = value.split('-');
    if (parts.length === 3) {
      const date = new Date(Number(parts[0]), Number(parts[1]) - 1, Number(parts[2]));
      return date.toLocaleDateString('fr-FR', { day: '2-digit' });
    }
    if (parts.length !== 2) return value;
    const date = new Date(Number(parts[0]), Number(parts[1]) - 1, 1);
    return date.toLocaleDateString('fr-FR', { month: 'short' });
  }

  gaugeBackground(rate: number): string {
    const safe = Math.max(0, Math.min(100, Number(rate || 0)));
    return `conic-gradient(#16a34a 0 ${safe}%, #e2e8f0 ${safe}% 100%)`;
  }

  funnel(rapport: AdvancedReportingPayload): Array<{ label: string; count: string; rate: number; width: number }> {
    const widthFromRate = (rate: number) => Math.max(42, Math.min(100, Number(rate || 0)));
    return [
      {
        label: 'Devis → BC',
        count: `${rapport.kpis.ordersCount} / ${rapport.kpis.quotesCount}`,
        rate: rapport.kpis.quoteToOrderRate,
        width: widthFromRate(rapport.kpis.quoteToOrderRate)
      },
      {
        label: 'BC → BL',
        count: `${rapport.kpis.deliveryNotesCount} / ${rapport.kpis.ordersCount}`,
        rate: rapport.kpis.orderToDeliveryRate,
        width: widthFromRate(rapport.kpis.orderToDeliveryRate)
      },
      {
        label: 'BL → Factures',
        count: `${rapport.kpis.invoicesCount} / ${rapport.kpis.deliveryNotesCount}`,
        rate: rapport.kpis.deliveryToInvoiceRate,
        width: widthFromRate(rapport.kpis.deliveryToInvoiceRate)
      }
    ];
  }

  private normalize(value: unknown): string {
    return String(value ?? '').trim().toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }

  private documentsPartenaire(partenaire: PartnerFocusedReport, type: string): PartnerDocumentReportItem[] {
    const normalizedType = this.normalize(type);
    return partenaire.documents.filter(doc => this.normalize(doc.type) === normalizedType);
  }

  private resetAllTables(): void {
    (Object.keys(this.tableVisible) as TableId[]).forEach(table => this.resetTable(table));
  }

  private filterProducts(rows: AdvancedReportingTopProduct[], search: string): AdvancedReportingTopProduct[] {
    const query = this.normalize(search);
    return rows.filter(row =>
      !query ||
      this.normalize(row.itemName).includes(query) ||
      this.normalize(row.itemCode).includes(query)
    );
  }

  private sortProducts(rows: AdvancedReportingTopProduct[], table: TableId): AdvancedReportingTopProduct[] {
    const sort = this.tableSort[table];
    const direction = sort.direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => {
      if (sort.key === 'item') {
        return this.compareText(a.itemName || a.itemCode, b.itemName || b.itemCode) * direction;
      }
      return (Number((a as any)[sort.key] ?? 0) - Number((b as any)[sort.key] ?? 0)) * direction;
    });
  }

  private sortPartners(rows: AdvancedReportingTopPartner[], table: TableId): AdvancedReportingTopPartner[] {
    const sort = this.tableSort[table];
    const direction = sort.direction === 'asc' ? 1 : -1;
    return [...rows].sort((a, b) => {
      if (sort.key === 'partner') {
        return this.compareText(a.partnerName || a.partnerCode, b.partnerName || b.partnerCode) * direction;
      }
      return (Number((a as any)[sort.key] ?? 0) - Number((b as any)[sort.key] ?? 0)) * direction;
    });
  }

  private compareText(a: unknown, b: unknown): number {
    return String(a ?? '').localeCompare(String(b ?? ''), 'fr', { sensitivity: 'base' });
  }

  private moisParDefaut(): string { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`; }
  private premierJourMois(): string { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`; }
  private dateDuJour(): string { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; }
}
