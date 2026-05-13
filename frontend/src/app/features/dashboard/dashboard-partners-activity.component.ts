import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ReportingApiService, PartnerActivityItem } from '../../core/services/reporting-api.service';

type PartnerDocFilter = 'all' | 'quotes' | 'orders' | 'deliverynotes' | 'invoices' | 'creditnotes' | 'net';

@Component({
  selector: 'app-dashboard-partners-activity',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <a routerLink="/dashboard" class="back">Retour dashboard</a>
      <h1>Activite partenaires</h1>

      <div class="filters">
        <label>Debut (date et heure) <input type="datetime-local" [(ngModel)]="startDateTime" (change)="load()" /></label>
        <label>Fin (date et heure) <input type="datetime-local" [(ngModel)]="endDateTime" (change)="load()" /></label>
        <label>Etat
          <select [(ngModel)]="activity" (change)="load()">
            <option value="all">Tous</option>
            <option value="active">Actifs</option>
            <option value="inactive">Inactifs</option>
          </select>
        </label>
        <label>Document
          <select [(ngModel)]="docFilter" (change)="noop()">
            <option value="all">Tous</option>
            <option value="quotes">Devis</option>
            <option value="orders">Commandes</option>
            <option value="deliverynotes">BL</option>
            <option value="invoices">Factures</option>
            <option value="creditnotes">Avoirs</option>
            <option value="net">CA net</option>
          </select>
        </label>
        @if (isAdminMode() && teamMembers().length > 0) {
          <label>Commercial
            <select [(ngModel)]="salesPersonCode" (change)="load()">
              <option [ngValue]="0">Toute l'equipe</option>
              @for (sp of teamMembers(); track sp.salesPersonCode) {
                <option [ngValue]="sp.salesPersonCode">{{ sp.salesPersonName }}</option>
              }
            </select>
          </label>
        }
        <label>Recherche
          <input list="partner-suggestions" [(ngModel)]="search" (input)="onSearchInput()" placeholder="Code ou nom client" />
          <datalist id="partner-suggestions">
            @for (s of suggestions(); track s) {
              <option [value]="s"></option>
            }
          </datalist>
        </label>
      </div>

      @if (loading()) {
        <p>Chargement...</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>Partenaire</th>
              <th>Etat</th>
              <th *ngIf="showQuotes()">Devis Nb</th><th *ngIf="showQuotes()">Devis Montant</th>
              <th *ngIf="showOrders()">BC Nb</th><th *ngIf="showOrders()">BC Montant</th>
              <th *ngIf="showDelivery()">BL Nb</th><th *ngIf="showDelivery()">BL Montant</th>
              <th *ngIf="showInvoices()">Factures Nb</th><th *ngIf="showInvoices()">Factures Montant</th>
              <th *ngIf="showCredit()">Avoirs Nb</th><th *ngIf="showCredit()">Avoirs Montant</th>
              <th *ngIf="showNet()">CA net</th>
            </tr>
          </thead>
          <tbody>
            @for (p of rows(); track p.cardCode) {
              <tr>
                <td data-label="Partenaire">{{ p.cardCode }} - {{ p.cardName }}</td>
                <td data-label="Etat">{{ p.isActive ? 'Actif' : 'Inactif' }}</td>
                <td *ngIf="showQuotes()" data-label="Devis Nb">{{ p.quotesCount }}</td>
                <td *ngIf="showQuotes()" data-label="Devis Montant">{{ money(p.quotesAmount) }}</td>
                <td *ngIf="showOrders()" data-label="BC Nb">{{ p.ordersCount }}</td>
                <td *ngIf="showOrders()" data-label="BC Montant">{{ money(p.ordersAmount) }}</td>
                <td *ngIf="showDelivery()" data-label="BL Nb">{{ p.deliveryNotesCount }}</td>
                <td *ngIf="showDelivery()" data-label="BL Montant">{{ money(p.deliveryNotesAmount) }}</td>
                <td *ngIf="showInvoices()" data-label="Factures Nb">{{ p.invoicesCount }}</td>
                <td *ngIf="showInvoices()" data-label="Factures Montant">{{ money(p.invoicesAmount) }}</td>
                <td *ngIf="showCredit()" data-label="Avoirs Nb">{{ p.creditNotesCount }}</td>
                <td *ngIf="showCredit()" data-label="Avoirs Montant">{{ money(p.creditNotesAmount) }}</td>
                <td *ngIf="showNet()" data-label="CA net">{{ money(p.netRevenue) }}</td>
              </tr>
            } @empty {
              <tr><td colspan="13">Aucun partenaire pour ce filtre.</td></tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
  styles: [`
    .page { display: grid; gap: 1rem; }
    .back { text-decoration: none; color: #1d4ed8; }
    .filters { display: grid; grid-template-columns: repeat(4, minmax(190px, 1fr)); gap: .7rem; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; padding: .8rem; }
    label { display: grid; gap: .3rem; font-weight: 600; }
    input, select { border: 1px solid #d1d5db; border-radius: 8px; padding: .4rem .55rem; }
    table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e5e7eb; border-radius: 10px; overflow: hidden; font-size: .92rem; }
    th, td { padding: .5rem; border-bottom: 1px solid #edf0f4; text-align: left; }
    th { background: #f8fafc; color: #475569; white-space: nowrap; }
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
export class DashboardPartnersActivityComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ReportingApiService);
  private readonly auth = inject(AuthService);

  readonly loading = signal(false);
  readonly rows = signal<PartnerActivityItem[]>([]);
  readonly teamMembers = signal<Array<{ salesPersonCode: number; salesPersonName: string }>>([]);

  month = this.defaultMonth();
  startDateTime = this.defaultStartDateTime();
  endDateTime = this.defaultEndDateTime();
  activity: 'all' | 'active' | 'inactive' = 'all';
  salesPersonCode = 0;
  search = '';
  docFilter: PartnerDocFilter = 'all';

  readonly suggestions = computed(() =>
    this.rows().flatMap(r => [`${r.cardCode}`, `${r.cardName}`, `${r.cardCode} - ${r.cardName}`]).slice(0, 120)
  );
  readonly isAdminMode = signal(false);

  ngOnInit(): void {
    this.isAdminMode.set(['Admin', 'Manager'].includes(this.auth.role()));
    const qMonth = this.route.snapshot.queryParamMap.get('month');
    const qStartDate = this.route.snapshot.queryParamMap.get('startDate');
    const qEndDate = this.route.snapshot.queryParamMap.get('endDate');
    const qActivity = this.route.snapshot.queryParamMap.get('activity') as 'all' | 'active' | 'inactive' | null;
    const qSalesCode = Number(this.route.snapshot.queryParamMap.get('salesPersonCode') ?? 0);
    if (qMonth) this.month = qMonth;
    if (qMonth && !qStartDate && !qEndDate) {
      const [yy, mm] = qMonth.split('-').map(Number);
      if (Number.isFinite(yy) && Number.isFinite(mm)) {
        const start = new Date(yy, mm - 1, 1, 0, 0, 0, 0);
        const end = new Date(yy, mm, 1, 0, 0, 0, 0);
        this.startDateTime = this.asDateTimeLocal(start.toISOString());
        this.endDateTime = this.asDateTimeLocal(end.toISOString());
      }
    }
    if (qStartDate) this.startDateTime = this.asDateTimeLocal(qStartDate);
    if (qEndDate) this.endDateTime = this.asDateTimeLocal(qEndDate);
    if (qActivity && ['all', 'active', 'inactive'].includes(qActivity)) this.activity = qActivity;
    if (Number.isFinite(qSalesCode) && qSalesCode > 0) this.salesPersonCode = qSalesCode;
    this.load();
  }

  onSearchInput(): void {
    this.load();
  }

  noop(): void {}

  load(): void {
    this.loading.set(true);
    const scopedSales = this.isAdminMode() && this.salesPersonCode > 0 ? this.salesPersonCode : undefined;
    const startIso = this.startDateTime ? new Date(this.startDateTime).toISOString() : undefined;
    const endIso = this.endDateTime ? new Date(this.endDateTime).toISOString() : undefined;
    this.api.getPartnersActivity(this.month, this.activity, this.search, scopedSales, startIso, endIso).subscribe({
      next: (res) => {
        this.rows.set(res.data ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });

    if (this.isAdminMode()) {
      this.api.getCommercialReporting(this.monthKeyFromStartDate()).subscribe({
        next: (res) => {
          const filtered = (res.data?.teamMembers ?? []).filter(sp => {
            const name = String(sp.salesPersonName ?? '').trim().toLowerCase();
            return name !== 'administrateur';
          });
          this.teamMembers.set(filtered);
        },
        error: () => {}
      });
    }
  }

  showQuotes(): boolean { return this.docFilter === 'all' || this.docFilter === 'quotes'; }
  showOrders(): boolean { return this.docFilter === 'all' || this.docFilter === 'orders'; }
  showDelivery(): boolean { return this.docFilter === 'all' || this.docFilter === 'deliverynotes'; }
  showInvoices(): boolean { return this.docFilter === 'all' || this.docFilter === 'invoices'; }
  showCredit(): boolean { return this.docFilter === 'all' || this.docFilter === 'creditnotes'; }
  showNet(): boolean { return this.docFilter === 'all' || this.docFilter === 'net'; }

  money(value: number): string {
    return `${new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 0 }).format(Number(value || 0))} MAD`;
  }

  private defaultMonth(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }

  private defaultStartDateTime(): string {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return this.asDateTimeLocal(d.toISOString());
  }

  private defaultEndDateTime(): string {
    const d = new Date();
    return this.asDateTimeLocal(d.toISOString());
  }

  private asDateTimeLocal(value: string): string {
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const hour = String(d.getHours()).padStart(2, '0');
    const minute = String(d.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}T${hour}:${minute}`;
  }

  private monthKeyFromStartDate(): string {
    const d = new Date(this.startDateTime || new Date().toISOString());
    if (Number.isNaN(d.getTime())) return this.defaultMonth();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }
}
