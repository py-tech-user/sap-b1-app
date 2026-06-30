import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReportingApiService, CommercialReportingPayload,
  PartnerDebtItem,
  ReportingEvolutionPoint,
  ReportingSalesPersonInfo,
} from '../../core/services/reporting-api.service';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { PartnerApiService, PartnerRow } from '../../core/services/partner-api.service';
import { BaseChartDirective } from 'ng2-charts';
import { Chart, registerables, ChartData } from 'chart.js';
Chart.register(...registerables);

type SortDirection = 'none' | 'asc' | 'desc';
type PartnerDebtSortKey = 'salesPersonName' | 'cardCode' | 'cardName' | 'partnerOwesCompanyAmount' | 'companyOwesPartnerAmount' | 'balance';
type PeriodeType = 'week' | 'month' | 'quarter' | 'year' | 'custom';
type RevenueBreakdownRow = { label: string; revenue: number; percent: number };

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, BaseChartDirective],
  template: `
    <div class="dashboard">
      <!-- 1. Filtres -->
      <section class="filters-panel">
        <label class="filter-field">
          Type de période
          <select [(ngModel)]="periode" (change)="onPeriodTypeChange()">
            <option value="week">Semaine</option>
            <option value="month">Mois</option>
            <option value="quarter">Trimestre</option>
            <option value="year">Année</option>
            <option value="custom">Période personnalisée</option>
          </select>
        </label>
        @if (periode === 'week') {
          <label class="filter-field">
            Semaine
            <input type="week" [(ngModel)]="selectedWeek" (change)="load()" />
          </label>
        }
        @if (periode === 'month') {
          <label class="filter-field">
            Mois
            <input type="month" [(ngModel)]="selectedMonth" (change)="load()" />
          </label>
        }
        @if (periode === 'quarter') {
          <label class="filter-field">
            Trimestre
            <select [(ngModel)]="selectedQuarter" (change)="load()">
              <option [ngValue]="1">T1</option>
              <option [ngValue]="2">T2</option>
              <option [ngValue]="3">T3</option>
              <option [ngValue]="4">T4</option>
            </select>
          </label>
          <label class="filter-field">
            Année
            <select [(ngModel)]="selectedYear" (change)="load()">
              @for (year of availableYears; track year) {
                <option [ngValue]="year">{{ year }}</option>
              }
            </select>
          </label>
        }
        @if (periode === 'year') {
          <label class="filter-field">
            Année
            <select [(ngModel)]="selectedYear" (change)="load()">
              @for (year of availableYears; track year) {
                <option [ngValue]="year">{{ year }}</option>
              }
            </select>
          </label>
        }
        @if (periode === 'custom') {
          <label class="filter-field">
            Début
            <input type="date" [(ngModel)]="dateDebut" (change)="load()" />
          </label>
          <label class="filter-field">
            Fin
            <input type="date" [(ngModel)]="dateFin" (change)="load()" />
          </label>
        }
        @if (isAdminMode() && visibleTeamMembers().length) {
          <label class="filter-field dashboard-search-box">
            Commercial
            <input type="text"
              [ngModel]="dashboardCommercialSearch()"
              (ngModelChange)="onDashboardCommercialInput($event)"
              (focus)="openDashboardCommercialPanel()"
              (keydown)="replaceDashboardSearchOnTyping($event, 'commercial')"
              (keydown.enter)="selectDashboardCommercialIfUnique($event)"
              placeholder="Rechercher commercial" />
            @if (openDashboardCommercialSuggestions) {
              <div class="suggestions-panel">
                <button type="button" (mousedown)="selectDashboardCommercial(null)">Tous les commerciaux</button>
                @for (sp of dashboardCommercialSuggestions(); track sp.salesPersonCode) {
                  <button type="button" (mousedown)="selectDashboardCommercial(sp)">
                    {{ sp.salesPersonName }}
                  </button>
                }
              </div>
            }
          </label>
        }
        <label class="filter-field dashboard-search-box">
          Partenaire
          <input type="text"
            [ngModel]="dashboardPartnerSearch()"
            (ngModelChange)="onDashboardPartnerInput($event)"
            (focus)="openDashboardPartnerPanel()"
            (keydown)="replaceDashboardSearchOnTyping($event, 'partner')"
            (keydown.enter)="selectDashboardPartnerIfUnique($event)"
            placeholder="Rechercher partenaire" />
          @if (openDashboardPartnerSuggestions) {
            <div class="suggestions-panel">
              <button type="button" (mousedown)="selectDashboardPartner(null)">Tous les partenaires</button>
              @for (partner of dashboardPartnerSuggestions(); track partnerCode(partner)) {
                <button type="button" (mousedown)="selectDashboardPartner(partner)">
                  {{ partnerName(partner) }}
                </button>
              }
            </div>
          }
        </label>
      </section>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      }

      @if (report(); as r) {
        <!-- 2. KPIs Financiers -->
        <section class="section">
          <h2>Indicateurs financiers</h2>
          <div class="kpi-grid">
            <div class="kpi-card">
              <h2 class="kpi-label">CA net</h2>
              <span class="kpi-value">{{ formatMoney(r.kpis.netRevenue) }}</span>
              
            </div>
            <div class="kpi-card">
              <h2 class="kpi-label">CA en attente</h2>
              <span class="kpi-value">{{ formatMoney(r.kpis.pendingRevenue) }}</span>
              
            </div>
            <div class="kpi-card">
              <h2 class="kpi-label">Panier moyen</h2>
              <span class="kpi-value">{{ formatMoney(r.kpis.averageQuoteAmount) }}</span>
              
            </div>
            <div class="kpi-card">
              <h2 class="kpi-label">Taux d'objectif de la période</h2>
              <span class="kpi-value">{{ formatPct(r.kpis.targetAchievementRate) }}</span>
              @if (canEditTarget()) {
                <label class="target-editor">
                  Objectif CA mensuel par commercial
                  <input type="number" min="0" step="100" [ngModel]="monthlyTargetInput()" (ngModelChange)="monthlyTargetInput.set($event)" (change)="saveMonthlyTarget()" />
                </label>
                <span class="kpi-sub">Objectif période: {{ formatMoney(r.kpis.periodTarget) }}</span>
              } @else {
                <span class="kpi-sub">Objectif mensuel: {{ formatMoney(r.kpis.monthlyTarget) }}</span>
                <span class="kpi-sub">Objectif période: {{ formatMoney(r.kpis.periodTarget) }}</span>
              }
              @if (targetSaving()) { <span class="kpi-sub">Enregistrement...</span> }
              @if (targetMessage()) { <span class="kpi-sub">{{ targetMessage() }}</span> }
              <div class="gauge-wrapper">
                <div class="gauge-bg">
                  <div class="gauge-fill" [style.width.%]="Math.min(r.kpis.targetAchievementRate, 100)" [style.background]="gaugeColor(r.kpis.targetAchievementRate)"></div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- 3. Indicateurs de transformation -->
        <section class="section">
          <h2>Taux de transformation</h2>
          <div class="transform-grid">
            <div class="transform-card">
              <div class="transform-header">
                <span>Devis → BC</span>
                <strong>{{ formatPct(r.kpis.quoteToOrderRate || r.kpis.conversionRate) }}</strong>
              </div>
              <div class="progress-bar">
                <div class="progress-fill" [style.width.%]="Math.min(r.kpis.quoteToOrderRate || r.kpis.conversionRate, 100)"></div>
              </div>
              <div class="transform-detail">{{ r.kpis.quotesCount }} devis → {{ getConvertedCount('quote') }} BC</div>
            </div>
            <div class="transform-card">
              <div class="transform-header">
                <span>BC → BL</span>
                <strong>{{ formatPct(r.kpis.orderToDeliveryRate) }}</strong>
              </div>
              <div class="progress-bar">
                <div class="progress-fill" [style.width.%]="Math.min(r.kpis.orderToDeliveryRate, 100)"></div>
              </div>
              <div class="transform-detail">{{ r.kpis.ordersCount }} BC → {{ getConvertedCount('order') }} BL</div>
            </div>
            <div class="transform-card">
              <div class="transform-header">
                <span>BL → Facture</span>
                <strong>{{ formatPct(r.kpis.deliveryToInvoiceRate) }}</strong>
              </div>
              <div class="progress-bar">
                <div class="progress-fill" [style.width.%]="Math.min(r.kpis.deliveryToInvoiceRate, 100)"></div>
              </div>
              <div class="transform-detail">{{ r.kpis.deliveryNotesCount }} BL → {{ getConvertedCount('delivery') }} factures</div>
            </div>
          </div>
        </section>
      }

      <!-- 5. Évolution CA net vs CA en attente -->
      <section class="section">
        <div class="section-header">
          <h2>Évolution CA net vs CA en attente — {{ evolutionYear }}</h2>
          <label class="chart-year-field">
            Année
            <select [(ngModel)]="evolutionYear" (change)="loadEvolution()">
              @for (year of availableYears; track year) {
                <option [ngValue]="year">{{ year }}</option>
              }
            </select>
          </label>
        </div>
        @if (evolutionPoints().length) {
          <div class="chart-container">
            <canvas baseChart
              [data]="chartData()"
              [options]="chartOptions"
              [type]="'line'">
            </canvas>
          </div>
        } @else if (!loadingEvolution()) {
          <p class="empty-msg">Aucune donnée d'évolution disponible</p>
        }
      </section>

      @if (report(); as r) {
        <section class="section revenue-insights">
          <div class="revenue-block">
            <h2>{{ revenueBreakdownTitle() }}</h2>
            @if (revenueBreakdownRows().length) {
              <div class="revenue-breakdown">
                @for (row of revenueBreakdownRows(); track row.label) {
                  <div class="revenue-row">
                    <div>
                      <strong>{{ row.label }}</strong>
                      <span>{{ formatPct(row.percent) }}</span>
                    </div>
                    <div class="revenue-bar"><i [style.width.%]="row.percent"></i></div>
                    <b>{{ formatMoney(row.revenue) }}</b>
                  </div>
                }
              </div>
            } @else {
              <p class="empty-msg">Aucune répartition disponible</p>
            }
          </div>

          <div class="revenue-block">
            <h2>Top 5 partenaires par chiffre d'affaires</h2>
            @if ((r.topClients ?? []).length) {
              <div class="revenue-breakdown">
                @for (client of (r.topClients ?? []).slice(0, 5); track client.cardCode) {
                  <div class="revenue-row">
                    <div>
                      <strong>{{ client.cardName || client.cardCode }}</strong>
                      <span>{{ client.cardCode }}</span>
                    </div>
                    <div class="revenue-bar"><i [style.width.%]="topClientBarWidth(client.revenue)"></i></div>
                    <b>{{ formatMoney(client.revenue) }}</b>
                  </div>
                }
              </div>
            } @else {
              <p class="empty-msg">Aucun client à afficher sur la période</p>
            }
          </div>
        </section>
      }

      <section class="section">
        <h2>Top 5 partenaires par solde</h2>
        @if (topPartnerBalances().length) {
          <div class="balance-list">
            @for (row of topPartnerBalances(); track row.cardCode) {
              <div class="balance-row">
                <div>
                  <strong>{{ row.cardName || row.cardCode }}</strong>
                  @if (isAdminMode()) { <span>{{ row.salesPersonName || ('#' + row.salesPersonCode) }}</span> }
                </div>
                <div class="balance-bar">
                  <i [style.width.%]="balanceBarWidth(row)"></i>
                </div>
                <b>{{ formatSignedMoney(row.balance) }}</b>
              </div>
            }
          </div>
        } @else {
          <p class="empty-msg">Aucun solde client à afficher</p>
        }
      </section>

      <!-- 7. Dettes partenaires -->
      <section class="section">
        <h2>Dettes partenaires</h2>
        <div class="debts-filters">
          <label class="dashboard-search-box">
            Rechercher
            <input type="text"
              [ngModel]="partnerDebtSearch()"
              (ngModelChange)="onPartnerDebtSearchInput($event)"
              (focus)="openPartnerDebtPanel()"
              (keydown.enter)="selectPartnerDebtIfUnique($event)"
              placeholder="Code ou nom partenaire" />
            @if (openPartnerDebtSuggestions && partnerDebtSuggestions().length) {
              <div class="suggestions-panel">
                @for (s of partnerDebtSuggestions(); track s) {
                  <button type="button" (mousedown)="selectPartnerDebt(s)">{{ s }}</button>
                }
              </div>
            }
          </label>
          @if (isAdminMode()) {
            <label class="dashboard-search-box">
              Commercial
              <input type="text"
                [ngModel]="partnerDebtCommercialSearch()"
                (ngModelChange)="onPartnerDebtCommercialInput($event)"
                (focus)="openPartnerDebtCommercialPanel()"
                (keydown.enter)="selectPartnerDebtCommercialIfUnique($event)"
                placeholder="Nom commercial" />
              @if (openPartnerDebtCommercialSuggestions && partnerDebtCommercialSuggestions().length) {
                <div class="suggestions-panel">
                  @for (sp of partnerDebtCommercialSuggestions(); track sp.salesPersonCode) {
                    <button type="button" (mousedown)="selectPartnerDebtCommercial(sp)">
                      {{ sp.salesPersonName }}
                    </button>
                  }
                </div>
              }
            </label>
          }
        </div>
        @if (filteredPartnerDebts(); as debts) {
          @if (debts.length || isCurrentPartnerDebtPageLoading()) {
            <div class="table-wrapper debts-table-scroll">
              <table>
                <thead>
                  <tr>
                    @if (isAdminMode()) { <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('salesPersonName')">Commercial {{ sortIndicator('salesPersonName') }}</button></th> }
                    <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('cardName')">Partenaire {{ sortIndicator('cardName') }}</button></th>
                    <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('partnerOwesCompanyAmount')">Débit {{ sortIndicator('partnerOwesCompanyAmount') }}</button></th>
                    <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('companyOwesPartnerAmount')">Crédit {{ sortIndicator('companyOwesPartnerAmount') }}</button></th>
                    <th><button type="button" class="sort-btn" (click)="togglePartnerDebtSort('balance')">Solde {{ sortIndicator('balance') }}</button></th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of debts; track row.cardCode) {
                    <tr>
                      @if (isAdminMode()) { <td>{{ row.salesPersonName || ('#' + row.salesPersonCode) }}</td> }
                      <td>{{ row.cardName || row.cardCode }}</td>
                      <td>{{ formatMoney(row.partnerOwesCompanyAmount) }}</td>
                      <td>{{ formatMoney(row.companyOwesPartnerAmount) }}</td>
                      <td>{{ formatSignedMoney(row.balance) }}</td>
                    </tr>
                  }
                  @if (!debts.length && isCurrentPartnerDebtPageLoading()) {
                    <tr>
                      <td [attr.colspan]="isAdminMode() ? 5 : 4">Chargement de la page...</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <div class="debts-pagination">
              <button type="button" (click)="goToPartnerDebtsPage(partnerDebtsCurrentPage() - 1)" [disabled]="!canGoToPreviousPartnerDebtsPage()">&#8249;</button>
              <span>{{ partnerDebtsPageSummary() }}</span>
              <button type="button" (click)="goToPartnerDebtsPage(partnerDebtsCurrentPage() + 1)" [disabled]="!canGoToNextPartnerDebtsPage()">&#8250;</button>
            </div>
            @if (isPartnerDebtBackgroundLoading()) {
              <p class="loading-more">Chargement des autres pages en arriÃ¨re-plan...</p>
            }
          } @else {
            <p class="empty-msg">Aucun partenaire à afficher</p>
          }
        }
      </section>
    </div>
  `,
  styles: [`
    .dashboard { display: grid; gap: 1rem; }
    .section { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .85rem; }
    .section h2 { margin: 0 0 .6rem; font-size: 1.05rem; color: #111827; }
    .filters-panel { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .85rem; display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .7rem; }
    .filter-field { display: grid; gap: .32rem; font-weight: 600; color: #374151; }
    .filter-field input, .filter-field select { border: 1px solid #d1d5db; border-radius: 8px; padding: .45rem .6rem; background: #fff; }
    .dashboard-search-box { position: relative; }
    .suggestions-panel { position: absolute; top: calc(100% + 4px); left: 0; right: 0; z-index: 30; max-height: 260px; overflow: auto; background: #fff; border: 1px solid #d1d5db; border-radius: 10px; box-shadow: 0 14px 30px rgba(15, 23, 42, .14); padding: .25rem; }
    .suggestions-panel button { width: 100%; border: 0; background: transparent; text-align: left; padding: .5rem .6rem; border-radius: 8px; cursor: pointer; color: #111827; }
    .suggestions-panel button:hover { background: #eff6ff; color: #1d4ed8; }
    .loading { text-align: center; padding: 1.5rem; color: #666; }
    .empty-msg { color: #9ca3af; padding: .5rem 0; }

    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: .7rem; }
    .kpi-card { background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: .75rem; display: grid; gap: .15rem; }
    .kpi-card.small { grid-template-rows: auto auto auto; }
    .kpi-value { font-size: 1.3rem; font-weight: 700; color: #111827; }
    .kpi-label { color: #374151; font-size: .88rem; }
    .kpi-sub { color: #6b7280; font-size: .82rem; }

    .gauge-wrapper { margin-top: .35rem; }
    .gauge-bg { height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; }
    .gauge-fill { height: 100%; border-radius: 4px; transition: width .4s; }

    .transform-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: .7rem; }
    .transform-card { background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: .7rem; }
    .transform-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: .35rem; font-size: .9rem; color: #374151; }
    .transform-header strong { font-size: 1.05rem; color: #111827; }
    .progress-bar { height: 7px; background: #e5e7eb; border-radius: 4px; overflow: hidden; margin-bottom: .35rem; }
    .progress-fill { height: 100%; background: #3b82f6; border-radius: 4px; transition: width .4s; }
    .transform-detail { color: #6b7280; font-size: .82rem; }

    .chart-container { max-height: 320px; position: relative; }
    .chart-container canvas { max-height: 300px; }

    .table-wrapper { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border-bottom: 1px solid #edf0f4; padding: .5rem; text-align: left; font-size: .9rem; }
    th { background: #f8fafc; color: #475569; font-weight: 700; }
    .sort-btn { border: 0; background: transparent; padding: 0; cursor: pointer; color: inherit; font: inherit; font-weight: 700; }
    .section-header { display: flex; align-items: center; justify-content: space-between; gap: .75rem; flex-wrap: wrap; margin-bottom: .6rem; }
    .section-header h2 { margin: 0; }
    .chart-year-field, .target-editor { display: grid; gap: .25rem; font-weight: 600; color: #374151; font-size: .86rem; }
    .chart-year-field select, .target-editor input { border: 1px solid #d1d5db; border-radius: 8px; padding: .4rem .55rem; background: #fff; }

    .debts-table-scroll { height: 420px; overflow: auto; }
    .debts-table-scroll thead th { position: sticky; top: 0; z-index: 1; }
    .loading-more { text-align: center; margin: .65rem 0 0; color: #6b7280; font-size: .86rem; }
    .debts-pagination { display: flex; align-items: center; justify-content: center; gap: .7rem; margin-top: .7rem; color: #475569; font-size: .88rem; }
    .debts-pagination button { width: 34px; height: 34px; border: 1px solid #d1d5db; border-radius: 8px; background: #fff; color: #111827; font-size: 1.4rem; line-height: 1; cursor: pointer; }
    .debts-pagination button:hover:not(:disabled) { background: #eff6ff; border-color: #93c5fd; color: #1d4ed8; }
    .debts-pagination button:disabled { opacity: .45; cursor: not-allowed; }

    .debts-filters { margin-bottom: .6rem; display: grid; grid-template-columns: repeat(2, minmax(220px, 1fr)); gap: .7rem; max-width: 580px; }
    .debts-filters label { display: grid; gap: .3rem; font-weight: 600; color: #374151; }
    .debts-filters input { border: 1px solid #d1d5db; border-radius: 8px; padding: .4rem .6rem; }
    .balance-list { display: grid; gap: .55rem; }
    .balance-row { display: grid; grid-template-columns: minmax(180px, 1.2fr) minmax(120px, 2fr) auto; gap: .75rem; align-items: center; }
    .balance-row strong, .balance-row span { display: block; }
    .balance-row span { color: #6b7280; font-size: .8rem; margin-top: .12rem; }
    .balance-row b { white-space: nowrap; color: #111827; }
    .balance-bar { height: 9px; background: #e5e7eb; border-radius: 999px; overflow: hidden; }
    .balance-bar i { display: block; height: 100%; background: #3b82f6; border-radius: inherit; }
    .revenue-insights { display: grid; gap: 1rem; }
    .revenue-block + .revenue-block { padding-top: .85rem; border-top: 1px solid #edf0f4; }
    .revenue-breakdown { display: grid; gap: .55rem; }
    .revenue-row { display: grid; grid-template-columns: minmax(170px, 1.2fr) minmax(120px, 2fr) auto; gap: .75rem; align-items: center; }
    .revenue-row strong, .revenue-row span { display: block; }
    .revenue-row span { color: #6b7280; font-size: .8rem; margin-top: .12rem; }
    .revenue-row b { white-space: nowrap; color: #111827; }
    .revenue-bar { height: 9px; background: #e5e7eb; border-radius: 999px; overflow: hidden; }
    .revenue-bar i { display: block; height: 100%; background: #14b8a6; border-radius: inherit; }

    @media (max-width: 900px) {
      .transform-grid { grid-template-columns: 1fr; }
      .filters-panel { grid-template-columns: 1fr; }
      .kpi-grid { grid-template-columns: 1fr; }
      .debts-filters { grid-template-columns: 1fr; }
      .kpi-card { padding: .65rem; }
      .kpi-value { font-size: 1.1rem; }
      .chart-container { max-height: 260px; }
      th, td { padding: .35rem; }
      .balance-row { grid-template-columns: 1fr; gap: .35rem; }
      .revenue-row { grid-template-columns: 1fr; gap: .35rem; }
    }
  `]
})
export class DashboardComponent implements OnInit {
  readonly Math = Math;
  private readonly reportingApi = inject(ReportingApiService);
  private readonly auth = inject(AuthService);
  private readonly partnerApi = inject(PartnerApiService);

  readonly loading = signal(true);
  readonly loadingEvolution = signal(false);
  readonly loadingMorePartnerDebts = signal(false);

  periode: PeriodeType = 'month';
  selectedMonth = this.defaultMonth();
  selectedWeek = this.defaultWeek();
  selectedQuarter = Math.floor(new Date().getMonth() / 3) + 1;
  selectedYear = new Date().getFullYear();
  evolutionYear = new Date().getFullYear();
  readonly availableYears = this.buildAvailableYears();
  dateDebut = this.firstDayOfMonth();
  dateFin = this.todayIso();
  selectedSalesPersonCode = 0;
  selectedPartnerCode = '';
  readonly dashboardCommercialSearch = signal('Tous les commerciaux');
  readonly dashboardPartnerSearch = signal('Tous les partenaires');
  openDashboardCommercialSuggestions = false;
  openDashboardPartnerSuggestions = false;
  openPartnerDebtSuggestions = false;
  openPartnerDebtCommercialSuggestions = false;
  private dashboardCommercialReplaceOnType = false;
  private dashboardPartnerReplaceOnType = false;
  readonly monthlyTargetInput = signal<number | string>(0);
  readonly targetSaving = signal(false);
  readonly targetMessage = signal('');

  readonly report = signal<CommercialReportingPayload | null>(null);
  readonly partners = signal<PartnerRow[]>([]);
  readonly partnerDebtSearch = signal('');
  readonly partnerDebtCommercialSearch = signal('');
  readonly partnerDebts = signal<PartnerDebtItem[]>([]);
  readonly partnerDebtsTotal = signal(0);
  readonly partnerDebtsCurrentPage = signal(1);
  readonly partnerDebtPagesLoading = signal<Set<number>>(new Set<number>());
  readonly partnerDebtSortKey = signal<PartnerDebtSortKey | null>('balance');
  readonly partnerDebtSortDirection = signal<SortDirection>('desc');
  private readonly partnersPageSize = 10;
  private partnersLoadVersion = 0;
  private partnersBackgroundLoading = false;
  private partnerDebtsPage = 1;
  private readonly partnerDebtsPageSize = 50;
  private partnerDebtPagesLoaded = new Set<number>();
  private partnerDebtSearchTimer: ReturnType<typeof setTimeout> | null = null;
  private partnerDebtRequestVersion = 0;
  private lastPartnerDebtsSalesPersonCode: number | null = null;
  private lastPartnerDebtsPartnerCode = '';
  private lastEvolutionSalesPersonCode: number | null = null;
  private lastEvolutionPartnerCode = '';
  private lastEvolutionPeriodKey = '';

  readonly evolutionPoints = signal<ReportingEvolutionPoint[]>([]);

  readonly isAdminMode = signal(false);

  readonly visibleTeamMembers = computed(() =>
    (this.report()?.teamMembers ?? []).filter(sp => {
      const name = String(sp.salesPersonName ?? '').trim().toLowerCase();
      return name !== 'administrateur';
    })
  );

  readonly dashboardCommercialSuggestions = computed(() => {
    const search = this.normalizeSearch(this.dashboardCommercialSearch() === 'Tous les commerciaux' ? '' : this.dashboardCommercialSearch());
    return this.visibleTeamMembers().filter(sp =>
      !search ||
      this.normalizeSearch(sp.salesPersonName).includes(search) ||
      String(sp.salesPersonCode).includes(search)
    );
  });

  readonly visiblePartners = computed(() => {
    const selectedSalesPerson = this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : 0;
    return this.partners()
      .filter(partner => !selectedSalesPerson || this.partnerSalesPersonCode(partner) === selectedSalesPerson)
      .sort((a, b) => this.partnerName(a).localeCompare(this.partnerName(b), 'fr', { sensitivity: 'base' }));
  });

  readonly dashboardPartnerSuggestions = computed(() => {
    const search = this.normalizeSearch(this.dashboardPartnerSearch() === 'Tous les partenaires' ? '' : this.dashboardPartnerSearch());
    return this.visiblePartners().filter(partner =>
      !search ||
      this.normalizeSearch(this.partnerName(partner)).includes(search) ||
      this.normalizeSearch(this.partnerCode(partner)).includes(search)
    );
  });

  readonly matchingPartnerDebts = computed(() => {
    const baseRows = this.partnerDebts();
    const key = this.partnerDebtSortKey();
    const dir = this.partnerDebtSortDirection();
    if (!key || dir === 'none') return baseRows;
    if (key === 'balance') return [...baseRows].sort((a, b) => this.compareBalanceImpact(a.balance, b.balance));
    const d = dir === 'asc' ? 1 : -1;
    return [...baseRows].sort((a, b) => this.comparePartnerDebt(a, b, key) * d);
  });

  readonly filteredPartnerDebts = computed(() => {
    const start = (this.partnerDebtsCurrentPage() - 1) * this.partnerDebtsPageSize;
    return this.matchingPartnerDebts().slice(start, start + this.partnerDebtsPageSize);
  });

  readonly topPartnerBalances = computed(() =>
    [...this.partnerDebts()]
      .sort((a, b) => Math.abs(Number(b.balance || 0)) - Math.abs(Number(a.balance || 0)))
      .slice(0, 5)
  );

  readonly revenueBreakdownRows = computed<RevenueBreakdownRow[]>(() => {
    const report = this.report();
    if (!report) return [];
    const rows = this.shouldBreakdownByCommercial()
      ? (report.teamPerformances ?? [])
          .map(row => ({ label: row.salesPersonName || `Commercial #${row.salesPersonCode}`, revenue: Number(row.netRevenue || 0) }))
      : (report.topClients ?? [])
          .map(row => ({ label: row.cardName || row.cardCode, revenue: Number(row.revenue || 0) }));
    const positiveRows = rows
      .filter(row => row.revenue > 0)
      .sort((a, b) => b.revenue - a.revenue)
      .slice(0, this.shouldBreakdownByCommercial() ? 3 : 5);
    const total = positiveRows.reduce((sum, row) => sum + row.revenue, 0);
    return positiveRows.map(row => ({
      ...row,
      percent: total > 0 ? Math.round((row.revenue * 10000) / total) / 100 : 0
    }));
  });

  readonly canExpandPartnerDebts = computed(() =>
    this.partnerDebtsCurrentPage() < this.partnerDebtsTotalPages() ||
    this.partnerDebts().length < this.partnerDebtsTotal()
  );

  readonly partnerDebtSuggestions = computed(() => {
    const search = this.normalizeSearch(this.partnerDebtSearch());
    const options = this.partnerDebts()
      .flatMap(r => [r.cardName ?? '', r.cardCode ?? ''])
      .map(v => String(v ?? '').trim())
      .filter(Boolean);

    return [...new Set(options)].filter(value =>
      !search || this.normalizeSearch(value).includes(search)
    );
  });

  readonly partnerDebtCommercialSuggestions = computed(() =>
    this.isAdminMode()
      ? this.visibleTeamMembers().filter(sp => {
          const search = this.normalizeSearch(this.partnerDebtCommercialSearch());
          return !search ||
            this.normalizeSearch(sp.salesPersonName).includes(search) ||
            String(sp.salesPersonCode).includes(search);
        })
      : []
  );

  readonly chartData = computed(() => {
    const pts = this.evolutionPoints();
    return {
      labels: pts.length ? pts.map(p => p.monthKey) : [],
      datasets: [
        { label: 'CA net', data: pts.map(p => p.revenue), borderColor: '#3b82f6', backgroundColor: 'rgba(59,130,246,.1)', fill: true, tension: .3, pointRadius: 4 },
        { label: 'CA en attente', data: pts.map(p => p.pendingRevenue), borderColor: '#f97316', backgroundColor: 'rgba(249,115,22,.1)', fill: true, tension: .3, pointRadius: 4 },
      ]
    } as ChartData<'line', number[], string>;
  });

  readonly chartOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom', labels: { boxWidth: 12, padding: 16, font: { size: 12 } } }
    },
    scales: {
      y: { beginAtZero: true, ticks: { callback: (v: any) => new Intl.NumberFormat('fr-FR', { notation: 'compact', maximumFractionDigits: 1 }).format(v) + ' MAD' } },
      x: { grid: { display: false } }
    }
  };

  private convertedCounts = { quote: 0, order: 0, delivery: 0 };
  private dashboardRequestVersion = 0;

  revenueBreakdownTitle(): string {
    return this.shouldBreakdownByCommercial()
      ? "Top 3 commerciaux par chiffre d'affaires"
      : "Top 5 partenaires par chiffre d'affaires";
  }

  private shouldBreakdownByCommercial(): boolean {
    return this.isAdminMode() && this.selectedSalesPersonCode <= 0 && !this.selectedPartnerCode;
  }

  ngOnInit(): void {
    this.isAdminMode.set(this.auth.hasRole(['Admin', 'Manager']));
    this.load();
    setTimeout(() => this.loadPartners());
  }

  private loadPartners(): void {
    const loadVersion = ++this.partnersLoadVersion;
    this.partnersBackgroundLoading = false;
    this.partnerApi.getAll(1, this.partnersPageSize).subscribe({
      next: (res) => {
        if (loadVersion !== this.partnersLoadVersion) return;
        const firstPage = res.items ?? [];
        this.partners.set(firstPage);
        const totalCount = Number(res.totalCount ?? firstPage.length);
        if (totalCount > firstPage.length) {
          setTimeout(() => this.loadRemainingPartners(loadVersion, 2, totalCount));
        }
      },
      error: () => this.partners.set([])
    });
  }

  private loadRemainingPartners(loadVersion: number, page: number, totalCount: number): void {
    if (loadVersion !== this.partnersLoadVersion || this.partnersBackgroundLoading) return;
    if (this.partners().length >= totalCount) return;

    this.partnersBackgroundLoading = true;
    this.partnerApi.getAll(page, this.partnersPageSize).subscribe({
      next: (res) => {
        this.partnersBackgroundLoading = false;
        if (loadVersion !== this.partnersLoadVersion) return;
        this.partners.set(this.mergePartners(this.partners(), res.items ?? []));
        if (this.partners().length < totalCount && (res.items ?? []).length > 0) {
          setTimeout(() => this.loadRemainingPartners(loadVersion, page + 1, totalCount));
        }
      },
      error: () => {
        this.partnersBackgroundLoading = false;
      }
    });
  }

  private mergePartners(current: PartnerRow[], incoming: PartnerRow[]): PartnerRow[] {
    const map = new Map<string, PartnerRow>();
    [...current, ...incoming].forEach(partner => {
      const code = this.partnerCode(partner);
      if (code) map.set(code, partner);
    });
    return [...map.values()];
  }

  @HostListener('document:click', ['$event'])
  closeSearchPanels(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('.dashboard-search-box')) return;
    this.openDashboardCommercialSuggestions = false;
    this.openDashboardPartnerSuggestions = false;
    this.openPartnerDebtSuggestions = false;
    this.openPartnerDebtCommercialSuggestions = false;
  }

  load(): void {
    const requestVersion = ++this.dashboardRequestVersion;
    this.loading.set(true);
    this.reportingApi.getCommercialReporting(this.buildReportingParams({ includeTeamPerformance: false })).subscribe({
      next: (reporting) => {
        if (requestVersion !== this.dashboardRequestVersion) return;
        this.report.set(reporting.data);
        this.monthlyTargetInput.set(reporting.data?.kpis?.monthlyTarget ?? 0);
        this.computeConvertedCounts(reporting.data);
        this.loading.set(false);
        this.loadPartnerDebtsIfScopeChanged();
        const evolutionSalesPersonCode = this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : null;
        const evolutionPeriodKey = this.evolutionPeriodKey();
        if (!this.evolutionPoints().length || this.lastEvolutionSalesPersonCode !== evolutionSalesPersonCode || this.lastEvolutionPartnerCode !== this.selectedPartnerCode || this.lastEvolutionPeriodKey !== evolutionPeriodKey) {
          this.loadEvolution();
        }
        setTimeout(() => this.loadDashboardInBackground(requestVersion));
      },
      error: () => {
        if (requestVersion === this.dashboardRequestVersion) this.loading.set(false);
      }
    });
  }

  private loadDashboardInBackground(requestVersion: number): void {
    this.reportingApi.getCommercialReporting(this.buildReportingParams({ includeTeamPerformance: true })).subscribe({
      next: (reporting) => {
        if (requestVersion !== this.dashboardRequestVersion) return;
        this.report.set(reporting.data);
        this.monthlyTargetInput.set(reporting.data?.kpis?.monthlyTarget ?? 0);
        this.computeConvertedCounts(reporting.data);
      },
      error: () => {}
    });
  }

  private computeConvertedCounts(r: CommercialReportingPayload | null): void {
    if (!r?.kpis) { this.convertedCounts = { quote: 0, order: 0, delivery: 0 }; return; }
    const k = r.kpis;
    const qRate = k.quoteToOrderRate ?? k.conversionRate ?? 0;
    this.convertedCounts = {
      quote: k.quotesCount > 0 ? Math.round(k.quotesCount * qRate / 100) : 0,
      order: k.ordersCount > 0 ? Math.round(k.ordersCount * (k.orderToDeliveryRate ?? 0) / 100) : 0,
      delivery: k.deliveryNotesCount > 0 ? Math.round(k.deliveryNotesCount * (k.deliveryToInvoiceRate ?? 0) / 100) : 0,
    };
  }

  getConvertedCount(type: 'quote' | 'order' | 'delivery'): number {
    return this.convertedCounts[type];
  }

  loadEvolution(): void {
    this.loadingEvolution.set(true);
    this.lastEvolutionSalesPersonCode = this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : null;
    this.lastEvolutionPartnerCode = this.selectedPartnerCode;
    this.lastEvolutionPeriodKey = this.evolutionPeriodKey();
    this.reportingApi.getReportingEvolution({
      ...this.buildEvolutionParams(),
      salesPersonCode: this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined,
      cardCode: this.selectedPartnerCode || undefined
    }).subscribe({
      next: (res) => { this.evolutionPoints.set(res.data?.points ?? []); this.loadingEvolution.set(false); },
      error: () => { this.evolutionPoints.set([]); this.loadingEvolution.set(false); }
    });
  }

  private loadPartnerDebts(): void {
    this.lastPartnerDebtsSalesPersonCode = this.currentDebtSalesPersonCode();
    this.lastPartnerDebtsPartnerCode = this.selectedPartnerCode;
    const requestVersion = ++this.partnerDebtRequestVersion;
    this.partnerDebtsPage = 1;
    this.loadingMorePartnerDebts.set(false);
    this.partnerDebtsCurrentPage.set(1);
    this.partnerDebtPagesLoaded = new Set<number>();
    this.partnerDebtPagesLoading.set(new Set<number>());
    this.partnerDebts.set([]);
    this.partnerDebtsTotal.set(0);
    this.resetPartnerDebtsScrollTop();
    this.loadPartnerDebtPage(1, requestVersion, { activate: true, foreground: true });
  }

  private loadPartnerDebtsIfScopeChanged(): void {
    const salesPersonCode = this.currentDebtSalesPersonCode();
    if (
      this.partnerDebts().length === 0 ||
      this.lastPartnerDebtsSalesPersonCode !== salesPersonCode ||
      this.lastPartnerDebtsPartnerCode !== this.selectedPartnerCode
    ) {
      this.loadPartnerDebts();
    }
  }

  private currentDebtSalesPersonCode(): number | null {
    return this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : null;
  }

  goToPartnerDebtsPage(page: number): void {
    const targetPage = Math.max(1, Math.min(page, this.partnerDebtsTotalPages()));
    if (targetPage === this.partnerDebtsCurrentPage()) return;
    this.partnerDebtsCurrentPage.set(targetPage);
    this.partnerDebtsPage = Math.max(this.partnerDebtsPage, targetPage);
    this.resetPartnerDebtsScrollTop();
    this.loadPartnerDebtPage(targetPage, this.partnerDebtRequestVersion, { activate: true, foreground: true });
    this.loadPartnerDebtPage(targetPage + 1, this.partnerDebtRequestVersion, { background: true });
  }

  canGoToPreviousPartnerDebtsPage(): boolean {
    return this.partnerDebtsCurrentPage() > 1;
  }

  canGoToNextPartnerDebtsPage(): boolean {
    return this.partnerDebtsCurrentPage() < this.partnerDebtsTotalPages();
  }

  partnerDebtsTotalPages(): number {
    const total = this.partnerDebtsTotal();
    if (total <= 0) return 1;
    return Math.max(1, Math.ceil(total / this.partnerDebtsPageSize));
  }

  partnerDebtsPageSummary(): string {
    const total = this.partnerDebtsTotal();
    if (total <= 0) return 'Page 1 / 1';
    const currentPage = this.partnerDebtsCurrentPage();
    const start = (currentPage - 1) * this.partnerDebtsPageSize + 1;
    const end = Math.min(currentPage * this.partnerDebtsPageSize, total);
    return `Page ${currentPage} / ${this.partnerDebtsTotalPages()} - ${start}-${end} sur ${total}`;
  }

  isPartnerDebtBackgroundLoading(): boolean {
    return [...this.partnerDebtPagesLoading()].some(page => page !== this.partnerDebtsCurrentPage());
  }

  isCurrentPartnerDebtPageLoading(): boolean {
    return this.partnerDebtPagesLoading().has(this.partnerDebtsCurrentPage());
  }

  private loadPartnerDebtPage(
    page: number,
    requestVersion: number,
    options: { activate?: boolean; foreground?: boolean; background?: boolean } = {}
  ): void {
    if (page < 1) return;
    const totalPages = this.partnerDebtsTotalPages();
    if (this.partnerDebtsTotal() > 0 && page > totalPages) return;
    if (this.partnerDebtPagesLoaded.has(page)) return;
    if (this.partnerDebtPagesLoading().has(page)) return;

    this.setPartnerDebtPageLoading(page, true);
    if (options.foreground) this.loadingMorePartnerDebts.set(true);
    this.reportingApi.getPartnerDebts(
      this.currentDebtSalesPersonCode() ?? undefined,
      page, this.partnerDebtsPageSize,
      this.partnerDebtSearch(),
      this.isAdminMode() ? this.partnerDebtCommercialSearch() : undefined,
      this.selectedPartnerCode || undefined
    ).subscribe({
      next: (res) => {
        if (requestVersion !== this.partnerDebtRequestVersion) {
          this.setPartnerDebtPageLoading(page, false);
          if (options.foreground) this.loadingMorePartnerDebts.set(false);
          return;
        }
        this.partnerDebtsPage = Math.max(this.partnerDebtsPage, page);
        this.partnerDebtPagesLoaded.add(page);
        this.partnerDebts.set(this.mergePartnerDebts(this.partnerDebts(), res.data ?? []));
        this.partnerDebtsTotal.set(Number(res.totalCount ?? this.partnerDebtsTotal()));
        this.setPartnerDebtPageLoading(page, false);
        if (options.foreground) this.loadingMorePartnerDebts.set(false);
        if (options.activate) this.partnerDebtsCurrentPage.set(page);
        if (page === 1 || options.background) this.preloadNextPartnerDebtPage(page + 1, requestVersion);
      },
      error: () => {
        if (requestVersion !== this.partnerDebtRequestVersion) {
          this.setPartnerDebtPageLoading(page, false);
          if (options.foreground) this.loadingMorePartnerDebts.set(false);
          return;
        }
        this.setPartnerDebtPageLoading(page, false);
        if (options.foreground) this.loadingMorePartnerDebts.set(false);
      }
    });
  }

  private preloadNextPartnerDebtPage(page: number, requestVersion: number): void {
    if (requestVersion !== this.partnerDebtRequestVersion) return;
    if (this.partnerDebtsTotal() > 0 && page > this.partnerDebtsTotalPages()) return;
    setTimeout(() => this.loadPartnerDebtPage(page, requestVersion, { background: true }), 80);
  }

  private mergePartnerDebts(current: PartnerDebtItem[], incoming: PartnerDebtItem[]): PartnerDebtItem[] {
    const map = new Map<string, PartnerDebtItem>();
    [...current, ...incoming].forEach(row => {
      const key = String(row.cardCode ?? '').trim().toLowerCase();
      if (key) map.set(key, row);
    });
    return [...map.values()];
  }

  private setPartnerDebtPageLoading(page: number, loading: boolean): void {
    const next = new Set(this.partnerDebtPagesLoading());
    if (loading) next.add(page);
    else next.delete(page);
    this.partnerDebtPagesLoading.set(next);
  }

  private resetPartnerDebtsScrollTop(): void {
    setTimeout(() => {
      const table = document.querySelector<HTMLElement>('.debts-table-scroll');
      if (table) table.scrollTop = 0;
    });
  }

  formatMoney(value: number): string {
    return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(Number(value || 0))} MAD`;
  }

  formatSignedMoney(value: number): string {
    const numeric = Number(value || 0);
    if (Math.abs(numeric) < 0.0001) return this.formatMoney(0);
    const sign = numeric > 0 ? '+' : '−';
    return `${sign}${this.formatMoney(Math.abs(numeric))}`;
  }

  formatPct(value: number): string {
    return `${Number(value || 0).toFixed(2)}%`;
  }

  gaugeColor(value: number): string {
    if (value >= 80) return '#22c55e';
    if (value >= 50) return '#f97316';
    return '#ef4444';
  }

  canEditTarget(): boolean {
    return this.auth.hasRole(['Admin', 'Manager']);
  }

  saveMonthlyTarget(): void {
    if (!this.canEditTarget() || this.targetSaving()) return;
    const monthlyTarget = Math.max(0, Number(this.monthlyTargetInput() || 0));
    this.monthlyTargetInput.set(monthlyTarget);
    this.targetSaving.set(true);
    this.targetMessage.set('');
    this.reportingApi.updateMonthlyTarget({
      monthlyTarget,
      salesPersonCode: this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined
    }).subscribe({
      next: () => {
        this.targetSaving.set(false);
        this.targetMessage.set('Objectif enregistré.');
        this.load();
      },
      error: () => {
        this.targetSaving.set(false);
        this.targetMessage.set("Impossible d'enregistrer l'objectif.");
      }
    });
  }

  onPeriodTypeChange(): void { this.load(); }

  onDashboardCommercialInput(value: string): void {
    this.dashboardCommercialReplaceOnType = false;
    this.dashboardCommercialSearch.set(String(value ?? ''));
    this.openDashboardCommercialSuggestions = true;
  }

  openDashboardCommercialPanel(): void {
    this.dashboardCommercialReplaceOnType = this.selectedSalesPersonCode > 0 || this.dashboardCommercialSearch() === 'Tous les commerciaux';
    this.openDashboardCommercialSuggestions = true;
    this.openDashboardPartnerSuggestions = false;
    this.openPartnerDebtSuggestions = false;
    this.openPartnerDebtCommercialSuggestions = false;
  }

  selectDashboardCommercial(sp: ReportingSalesPersonInfo | null): void {
    this.selectedSalesPersonCode = sp?.salesPersonCode ?? 0;
    this.dashboardCommercialSearch.set(sp?.salesPersonName || 'Tous les commerciaux');
    this.openDashboardCommercialSuggestions = false;
    if (this.selectedPartnerCode && !this.visiblePartners().some(partner => this.partnerCode(partner) === this.selectedPartnerCode)) {
      this.selectDashboardPartner(null, false);
    }
    this.load();
  }

  selectDashboardCommercialIfUnique(event: Event): void {
    const matches = this.dashboardCommercialSuggestions();
    if (matches.length === 1) {
      event.preventDefault();
      this.selectDashboardCommercial(matches[0]);
    }
  }

  onDashboardPartnerInput(value: string): void {
    this.dashboardPartnerReplaceOnType = false;
    this.dashboardPartnerSearch.set(String(value ?? ''));
    this.selectedPartnerCode = '';
    this.openDashboardPartnerSuggestions = true;
  }

  openDashboardPartnerPanel(): void {
    this.dashboardPartnerReplaceOnType = !!this.selectedPartnerCode || this.dashboardPartnerSearch() === 'Tous les partenaires';
    this.openDashboardCommercialSuggestions = false;
    this.openDashboardPartnerSuggestions = true;
    this.openPartnerDebtSuggestions = false;
    this.openPartnerDebtCommercialSuggestions = false;
  }

  selectDashboardPartner(partner: PartnerRow | null, reload = true): void {
    this.selectedPartnerCode = partner ? this.partnerCode(partner) : '';
    this.dashboardPartnerSearch.set(partner ? this.partnerName(partner) : 'Tous les partenaires');
    this.openDashboardPartnerSuggestions = false;
    if (reload) this.load();
  }

  selectDashboardPartnerIfUnique(event: Event): void {
    const matches = this.dashboardPartnerSuggestions();
    if (matches.length === 1) {
      event.preventDefault();
      this.selectDashboardPartner(matches[0]);
    }
  }

  replaceDashboardSearchOnTyping(event: KeyboardEvent, field: 'commercial' | 'partner'): void {
    const shouldReplace = field === 'commercial'
      ? this.dashboardCommercialReplaceOnType
      : this.dashboardPartnerReplaceOnType;
    if (!shouldReplace || event.ctrlKey || event.metaKey || event.altKey) return;

    if (event.key === 'Backspace' || event.key === 'Delete') {
      event.preventDefault();
      if (field === 'commercial') this.onDashboardCommercialInput('');
      else this.onDashboardPartnerInput('');
      return;
    }

    if (event.key.length !== 1) return;
    event.preventDefault();
    if (field === 'commercial') this.onDashboardCommercialInput(event.key);
    else this.onDashboardPartnerInput(event.key);
  }

  onPartnerDebtSearchInput(value: string): void {
    this.partnerDebtSearch.set(String(value ?? ''));
    this.openPartnerDebtSuggestions = true;
    this.schedulePartnerDebtsReload();
  }

  openPartnerDebtPanel(): void {
    this.openDashboardCommercialSuggestions = false;
    this.openPartnerDebtSuggestions = true;
    this.openPartnerDebtCommercialSuggestions = false;
  }

  selectPartnerDebt(value: string): void {
    this.partnerDebtSearch.set(String(value ?? ''));
    this.openPartnerDebtSuggestions = false;
    this.loadPartnerDebts();
  }

  selectPartnerDebtIfUnique(event: Event): void {
    const matches = this.partnerDebtSuggestions();
    if (matches.length === 1) {
      event.preventDefault();
      this.selectPartnerDebt(matches[0]);
    }
  }

  onPartnerDebtCommercialInput(value: string): void {
    if (!this.isAdminMode()) { this.partnerDebtCommercialSearch.set(''); return; }
    this.partnerDebtCommercialSearch.set(String(value ?? ''));
    this.openPartnerDebtCommercialSuggestions = true;
    this.schedulePartnerDebtsReload();
  }

  openPartnerDebtCommercialPanel(): void {
    this.openDashboardCommercialSuggestions = false;
    this.openDashboardPartnerSuggestions = false;
    this.openPartnerDebtSuggestions = false;
    this.openPartnerDebtCommercialSuggestions = true;
  }

  selectPartnerDebtCommercial(sp: ReportingSalesPersonInfo): void {
    this.partnerDebtCommercialSearch.set(sp.salesPersonName || String(sp.salesPersonCode));
    this.openPartnerDebtCommercialSuggestions = false;
    this.loadPartnerDebts();
  }

  selectPartnerDebtCommercialIfUnique(event: Event): void {
    const matches = this.partnerDebtCommercialSuggestions();
    if (matches.length === 1) {
      event.preventDefault();
      this.selectPartnerDebtCommercial(matches[0]);
    }
  }

  private schedulePartnerDebtsReload(): void {
    if (this.partnerDebtSearchTimer) clearTimeout(this.partnerDebtSearchTimer);
    this.partnerDebtSearchTimer = setTimeout(() => {
      this.partnerDebtSearchTimer = null;
      this.loadPartnerDebts();
    }, 250);
  }

  togglePartnerDebtSort(key: PartnerDebtSortKey): void {
    const ck = this.partnerDebtSortKey();
    const cd = this.partnerDebtSortDirection();
    if (ck !== key) { this.partnerDebtSortKey.set(key); this.partnerDebtSortDirection.set('asc'); return; }
    if (cd === 'asc') { this.partnerDebtSortDirection.set('desc'); return; }
    if (cd === 'desc') { this.partnerDebtSortDirection.set('none'); this.partnerDebtSortKey.set(null); return; }
    this.partnerDebtSortDirection.set('asc');
  }

  sortIndicator(key: string): string {
    return this.partnerDebtSortKey() !== key || this.partnerDebtSortDirection() === 'none' ? '' :
      this.partnerDebtSortDirection() === 'asc' ? '\u2191' : '\u2193';
  }

  private comparePartnerDebt(a: PartnerDebtItem, b: PartnerDebtItem, key: PartnerDebtSortKey): number {
    if (key === 'balance') return this.compareBalanceImpact(a.balance, b.balance);
    if (key === 'partnerOwesCompanyAmount' || key === 'companyOwesPartnerAmount') {
      return Number((a as any)[key] || 0) - Number((b as any)[key] || 0);
    }
    const av = String(key === 'salesPersonName' ? (a.salesPersonName || `#${a.salesPersonCode}`) : (a as any)[key] || '').toLowerCase();
    const bv = String(key === 'salesPersonName' ? (b.salesPersonName || `#${b.salesPersonCode}`) : (b as any)[key] || '').toLowerCase();
    return av.localeCompare(bv);
  }

  private compareBalanceImpact(a: number, b: number): number {
    const absA = Math.abs(Number(a || 0));
    const absB = Math.abs(Number(b || 0));
    if (absA === 0 && absB > 0) return 1;
    if (absB === 0 && absA > 0) return -1;
    if (absA !== absB) return absB - absA;
    return Number(b || 0) - Number(a || 0);
  }

  balanceBarWidth(row: PartnerDebtItem): number {
    const max = Math.max(...this.topPartnerBalances().map(item => Math.abs(Number(item.balance || 0))), 1);
    return Math.max(4, Math.min(100, Math.abs(Number(row.balance || 0)) * 100 / max));
  }

  topClientBarWidth(value: number): number {
    const max = Math.max(...(this.report()?.topClients ?? []).slice(0, 5).map(item => Number(item.revenue || 0)), 1);
    return Math.max(4, Math.min(100, Number(value || 0) * 100 / max));
  }

  partnerCode(row: PartnerRow): string {
    return String(row.CardCode ?? (row as any).cardCode ?? '').trim();
  }

  partnerName(row: PartnerRow): string {
    const code = this.partnerCode(row);
    const name = String(row.CardName ?? (row as any).cardName ?? code).trim();
    return code ? `${name} (${code})` : name;
  }

  private partnerSalesPersonCode(row: PartnerRow): number {
    return Number((row as any).SalesPersonCode ?? (row as any).salesPersonCode ?? (row as any).SlpCode ?? (row as any).slpCode ?? 0);
  }

  private normalizeSearch(value: unknown): string {
    return String(value ?? '').trim().toLowerCase();
  }

  private buildReportingParams(options: { includeTeamPerformance?: boolean } = {}): {
    periodType: 'week' | 'month' | 'quarter' | 'year' | 'custom';
    month?: string; quarter?: number; year?: number;
    startDate?: string; endDate?: string;
    salesPersonCode?: number;
    cardCode?: string;
    includeRecentDocuments?: boolean;
    includeTeamPerformance?: boolean;
  } {
    const sp = this.isAdminMode() && this.selectedSalesPersonCode > 0 ? this.selectedSalesPersonCode : undefined;
    const cardCode = this.selectedPartnerCode || undefined;
    const common = { salesPersonCode: sp, cardCode, includeRecentDocuments: false, includeTeamPerformance: options.includeTeamPerformance !== false };
    switch (this.periode) {
      case 'week': {
        const range = this.isoWeekRange(this.selectedWeek);
        return { periodType: 'week', startDate: range.start, endDate: range.end, ...common };
      }
      case 'month':
        return { periodType: 'month', month: this.selectedMonth || this.defaultMonth(), ...common };
      case 'quarter':
        return { periodType: 'quarter', quarter: this.selectedQuarter, year: this.selectedYear, ...common };
      case 'year':
        return { periodType: 'year', year: this.selectedYear, ...common };
      case 'custom':
        return { periodType: 'custom', startDate: this.dateDebut, endDate: this.dateFin, ...common };
      default:
        return { periodType: 'month', month: this.selectedMonth || this.defaultMonth(), ...common };
    }
  }

  private buildEvolutionParams(): {
    periodType: 'week' | 'month' | 'quarter' | 'year' | 'custom';
    month?: string; quarter?: number; year?: number;
    startDate?: string; endDate?: string;
  } {
    return { periodType: 'year', year: this.evolutionYear };
  }

  private evolutionPeriodKey(): string {
    const params = this.buildEvolutionParams();
    return JSON.stringify(params);
  }

  private defaultMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  private defaultWeek(): string {
    const now = new Date();
    const thursday = new Date(now);
    thursday.setDate(now.getDate() + 3 - ((now.getDay() + 6) % 7));
    const firstThursday = new Date(thursday.getFullYear(), 0, 4);
    const week = 1 + Math.round(((thursday.getTime() - firstThursday.getTime()) / 86400000 - 3 + ((firstThursday.getDay() + 6) % 7)) / 7);
    return `${thursday.getFullYear()}-W${String(week).padStart(2, '0')}`;
  }

  private isoWeekRange(weekValue: string): { start: string; end: string } {
    const match = /^(\d{4})-W(\d{2})$/.exec(weekValue || this.defaultWeek());
    const year = match ? Number(match[1]) : new Date().getFullYear();
    const week = match ? Number(match[2]) : 1;
    const fourthJan = new Date(year, 0, 4);
    const monday = new Date(fourthJan);
    monday.setDate(fourthJan.getDate() - ((fourthJan.getDay() + 6) % 7) + ((week - 1) * 7));
    const sunday = new Date(monday);
    sunday.setDate(monday.getDate() + 6);
    return { start: this.toDateStr(monday), end: this.toDateStr(sunday) };
  }

  private buildAvailableYears(): number[] {
    const current = new Date().getFullYear();
    return Array.from({ length: 9 }, (_, i) => current - 4 + i);
  }

  private firstDayOfMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-01`;
  }

  private todayIso(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private toDateStr(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
