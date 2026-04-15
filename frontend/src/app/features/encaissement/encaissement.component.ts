import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { environment } from '../../../environments/environment';

interface ApiResponse<T> {
  success?: boolean;
  message?: string;
  data?: T;
}

interface EncaissementClient {
  cardCode: string;
  cardName: string;
  currency: string;
  creditLimit: number;
  raw: Record<string, unknown>;
}

interface EncaissementInvoice {
  docEntry: number;
  docNum: number;
  docDate?: string;
  docDueDate?: string;
  docCurrency?: string;
  docTotal: number;
  paidToDate: number;
  openAmount: number;
  docStatus: string;
  raw: Record<string, unknown>;
}

interface InvoiceSelection {
  docEntry: number;
  selected: boolean;
}

@Component({
  selector: 'app-encaissement',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="page">
      <h1>Encaissement</h1>

      @if (error()) {
        <div class="alert alert-error">{{ error() }}</div>
      }

      @if (success()) {
        <div class="alert alert-success">{{ success() }}</div>
      }

      <div class="card form-grid">
        <label>
          Partenaire
          <input
            [ngModel]="selectedCardCode()"
            (ngModelChange)="onClientPickerInput($event)"
            list="encaissement-client-options"
            placeholder="Rechercher et sélectionner client" />
          <datalist id="encaissement-client-options">
            @for (client of clients(); track client.cardCode) {
              <option [value]="client.cardCode" [label]="client.cardCode + ' - ' + client.cardName + ' (' + client.currency + ')'">{{ client.cardCode }} - {{ client.cardName }}</option>
            }
          </datalist>
        </label>

        <label>
          Moyen de paiement
          <input [ngModel]="paymentMethodCode()" (ngModelChange)="paymentMethodCode.set($event)" placeholder="Ex: Virement" />
        </label>

        <label>
          CashSum
          <input type="number" min="0" step="0.01" [ngModel]="cashSum()" (ngModelChange)="onCashSumChange($event)" />
        </label>
      </div>

      <div class="card">
        <h3>Factures ouvertes</h3>

        @if (loadingClients()) {
          <p>Chargement des partenaires...</p>
        }

        @if (loadingInvoices()) {
          <p>Chargement des factures...</p>
        } @else if (invoices().length === 0) {
          <p>Aucune facture ouverte.</p>
        } @else {
          <div class="table-actions">
            <button type="button" class="btn-secondary" (click)="selectAllDisplayedInvoices()">Tout sélectionner</button>
            <button type="button" class="btn-secondary" (click)="clearDisplayedSelection()">Tout désélectionner</button>
          </div>

          <table>
            <thead>
              <tr>
                <th></th>
                <th>N° entrée</th>
                <th>N° document</th>
                <th>Date</th>
                <th>Date d'échéance</th>
                <th>Total</th>
                <th>Payé</th>
                <th>Reste</th>
                <th>Statut</th>
                <th>Affecté auto</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              @for (invoice of invoices(); track invoice.docEntry) {
                <tr>
                  <td>
                    <input
                      type="checkbox"
                      [disabled]="!canSelect(invoice)"
                      [checked]="isSelected(invoice.docEntry)"
                      (change)="toggleInvoice(invoice.docEntry, $any($event.target).checked)" />
                  </td>
                  <td>{{ invoice.docEntry }}</td>
                  <td>{{ invoice.docNum }}</td>
                  <td>{{ invoice.docDate ? (invoice.docDate | date:'dd/MM/yyyy') : '-' }}</td>
                  <td>{{ invoice.docDueDate ? (invoice.docDueDate | date:'dd/MM/yyyy') : '-' }}</td>
                  <td>{{ invoice.docTotal | number:'1.2-2' }}</td>
                  <td>{{ invoice.paidToDate | number:'1.2-2' }}</td>
                  <td>{{ invoice.openAmount | number:'1.2-2' }}</td>
                  <td>
                    <span class="status-badge" [class.open]="isOpenDocStatus(invoice.docStatus)" [class.closed]="!isOpenDocStatus(invoice.docStatus)">
                      {{ isOpenDocStatus(invoice.docStatus) ? 'Ouverte' : 'Clôturée' }}
                    </span>
                  </td>
                  <td>
                    {{ allocationOf(invoice.docEntry) | number:'1.2-2' }}
                  </td>
                  <td>
                    <button type="button" class="btn-secondary" (click)="openInvoiceDetails(invoice)">Voir</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>

      @if (selectedInvoice()) {
        <div class="drawer-backdrop" (click)="closeInvoiceDetails()"></div>
        <aside class="drawer" role="dialog" aria-modal="true" aria-label="Détail facture">
          <div class="drawer-header">
            <h3>Facture #{{ selectedInvoice()!.docNum || selectedInvoice()!.docEntry }}</h3>
            <button type="button" class="btn-secondary" (click)="closeInvoiceDetails()">Fermer</button>
          </div>
          <div class="drawer-body">
            <div class="drawer-actions">
              <button type="button" class="btn-primary" (click)="selectFromDrawer()">Sélectionner pour encaissement</button>
            </div>

            <dl class="details-list">
              @for (entry of selectedInvoiceFields(); track entry.key) {
                <div class="details-row">
                  <dt>{{ entry.key }}</dt>
                  <dd>{{ entry.value }}</dd>
                </div>
              }
            </dl>
          </div>
        </aside>
      }

      <div class="actions">
        <button type="button" class="btn-primary" (click)="submit()" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : 'Enregistrer encaissement' }}</button>
        @if (error()) {
          <span class="action-feedback error">{{ error() }}</span>
        }
        @if (success()) {
          <span class="action-feedback success">{{ success() }}</span>
        }
      </div>
    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .card { background: #fff; border-radius: 8px; padding: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }
    .form-grid { display: grid; grid-template-columns: repeat(4, minmax(180px, 1fr)); gap: 0.75rem; }
    label { display: flex; flex-direction: column; gap: 0.4rem; color: #374151; font-size: 0.9rem; }
    input, select { border: 1px solid #d1d5db; border-radius: 6px; padding: 0.45rem 0.6rem; }
    table { width: 100%; border-collapse: collapse; }
    th, td { border-bottom: 1px solid #eef2ff; padding: 0.55rem; text-align: left; }
    .table-actions { display: flex; gap: 0.5rem; margin-bottom: 0.6rem; }
    .actions { display: flex; justify-content: flex-end; align-items: center; gap: 0.6rem; flex-wrap: wrap; }
    .action-feedback { font-weight: 700; font-size: 0.9rem; }
    .action-feedback.error { color: #b91c1c; }
    .action-feedback.success { color: #065f46; }
    .btn-primary { background: #2563eb; color: #fff; border: 0; border-radius: 6px; padding: 0.65rem 1rem; cursor: pointer; }
    .btn-secondary { background: #fff; color: #1d4ed8; border: 1px solid #93c5fd; border-radius: 6px; padding: 0.45rem 0.7rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
    .status-badge { display: inline-block; border-radius: 999px; padding: 0.2rem 0.55rem; font-size: 0.78rem; }
    .status-badge.open { background: #e8f5e9; color: #1b5e20; }
    .status-badge.closed { background: #f3f4f6; color: #374151; }
    .alert { border-radius: 6px; padding: 0.75rem 1rem; }
    .alert-error { background: #fef2f2; color: #b91c1c; border: 1px solid #fecaca; }
    .alert-success { background: #ecfdf5; color: #065f46; border: 1px solid #a7f3d0; }
    .drawer-backdrop { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.35); z-index: 30; }
    .drawer { position: fixed; top: 0; right: 0; height: 100vh; width: min(520px, 100vw); background: #fff; box-shadow: -8px 0 24px rgba(15, 23, 42, 0.18); z-index: 31; display: flex; flex-direction: column; }
    .drawer-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: 1rem; border-bottom: 1px solid #e5e7eb; }
    .drawer-body { padding: 1rem; overflow: auto; }
    .drawer-actions { margin-bottom: 1rem; }
    .details-list { margin: 0; }
    .details-row { display: grid; grid-template-columns: minmax(140px, 1fr) 2fr; gap: 0.75rem; padding: 0.5rem 0; border-bottom: 1px dashed #e5e7eb; }
    .details-row dt { color: #374151; font-weight: 600; }
    .details-row dd { margin: 0; color: #111827; word-break: break-word; }
    @media (max-width: 1024px) {
      .form-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class EncaissementComponent {
  private readonly baseUrl = `${environment.apiUrl}/sap/encaissement`;
  private prefillCardCode = '';
  private prefillInvoiceDocEntry: number | null = null;

  readonly clients = signal<EncaissementClient[]>([]);
  readonly invoices = signal<EncaissementInvoice[]>([]);
  readonly selections = signal<InvoiceSelection[]>([]);

  readonly selectedCardCode = signal('');
  readonly paymentMethodCode = signal('Virement');
  readonly cashSum = signal(0);
  readonly totalSelectedAmount = computed(() =>
    this.selectedInvoicesOrdered().reduce((sum, invoice) => sum + invoice.openAmount, 0));

  readonly loadingInvoices = signal(false);
  readonly saving = signal(false);
  readonly loadingClients = signal(false);
  readonly selectedInvoice = signal<EncaissementInvoice | null>(null);
  readonly error = signal('');
  readonly success = signal('');

  constructor(private readonly http: HttpClient, private readonly route: ActivatedRoute) {
    this.readPrefillFromQuery();
    this.loadClients();
  }

  private readPrefillFromQuery(): void {
    const query = this.route.snapshot.queryParamMap;

    this.prefillCardCode = String(query.get('cardCode') ?? '').trim();

    const invoiceParam =
      query.get('invoiceDocEntry') ??
      query.get('docEntry') ??
      query.get('invoiceId') ??
      '';

    const parsedInvoiceDocEntry = Number(invoiceParam);
    this.prefillInvoiceDocEntry = Number.isFinite(parsedInvoiceDocEntry) && parsedInvoiceDocEntry > 0
      ? Math.trunc(parsedInvoiceDocEntry)
      : null;
  }

  toNumber(value: unknown): number {
    const n = Number(value);
    return Number.isFinite(n) ? n : 0;
  }

  toDisplay(value: unknown): string {
    if (value === null || value === undefined) return '-';
    const text = String(value).trim();
    return text === '' ? '-' : text;
  }

  onClientChange(cardCode: string): void {
    this.selectedCardCode.set(cardCode ?? '');
    this.cashSum.set(0);
    this.error.set('');
    this.success.set('');
    console.log('[encaissement] selected partenaire', this.selectedCardCode());
    this.loadOpenInvoices();
  }

  onClientPickerInput(value: string): void {
    const typed = String(value ?? '').trim();
    if (!typed) {
      this.selectedCardCode.set('');
      this.invoices.set([]);
      this.selections.set([]);
      return;
    }

    const exact = this.clients().find(c => c.cardCode.toLowerCase() === typed.toLowerCase());
    if (!exact) {
      this.selectedCardCode.set(typed);
      return;
    }

    this.onClientChange(exact.cardCode);
  }

  onCashSumChange(value: unknown): void {
    this.cashSum.set(Math.max(0, this.toNumber(value)));
  }

  isSelected(docEntry: number): boolean {
    return this.selections().some(x => x.docEntry === docEntry && x.selected);
  }

  allocationOf(docEntry: number): number {
    return this.computeAllocations().get(docEntry) ?? 0;
  }

  toggleInvoice(docEntry: number, selected: boolean): void {
    if (!Number.isFinite(docEntry) || docEntry <= 0) {
      console.warn('[encaissement] ignored selection for invalid docEntry', docEntry);
      return;
    }

    const next = this.selections().filter(x => x.docEntry !== docEntry);
    next.push({ docEntry, selected });
    this.selections.set(next);
    this.syncCashSumFromSelection();
    console.log('[encaissement] invoice selection changed', { docEntry, selected });
  }

  selectAllDisplayedInvoices(): void {
    const next = this.invoices()
      .filter((invoice) => this.canSelect(invoice))
      .map((invoice) => ({ docEntry: invoice.docEntry, selected: true }));

    this.selections.set(next);
    this.syncCashSumFromSelection();
  }

  clearDisplayedSelection(): void {
    const next = this.invoices().map((invoice) => ({ docEntry: invoice.docEntry, selected: false }));
    this.selections.set(next);
    this.syncCashSumFromSelection();
  }

  canSelect(invoice: EncaissementInvoice): boolean {
    return Number.isFinite(invoice.docEntry)
      && invoice.docEntry > 0
      && Number.isFinite(invoice.openAmount)
      && invoice.openAmount > 0;
  }

  isOpenDocStatus(status: string): boolean {
    const s = String(status ?? '').trim().toLowerCase();
    const compact = s.replace(/[\s_-]/g, '');
    return s === 'open' || s === 'o' || compact === 'bostopen' || (compact.includes('open') && !compact.includes('close'));
  }

  openInvoiceDetails(invoice: EncaissementInvoice): void {
    this.selectedInvoice.set(invoice);
  }

  closeInvoiceDetails(): void {
    this.selectedInvoice.set(null);
  }

  selectedInvoiceFields(): Array<{ key: string; value: string }> {
    const invoice = this.selectedInvoice();
    if (!invoice) return [];

    return Object.entries(invoice.raw)
      .map(([key, value]) => ({ key, value: this.toDisplay(value) }));
  }

  selectFromDrawer(): void {
    const invoice = this.selectedInvoice();
    if (!invoice) return;
    if (!this.canSelect(invoice)) {
      this.error.set('Facture invalide: DocEntry manquant.');
      return;
    }
    this.toggleInvoice(invoice.docEntry, true);
  }

  submit(): void {
    this.error.set('');
    this.success.set('');

    const cardCode = this.selectedCardCode().trim();
    if (!cardCode) {
      this.error.set('Partenaire obligatoire.');
      return;
    }

    const paymentMethodCode = this.paymentMethodCode().trim();
    if (!paymentMethodCode) {
      this.error.set('Mode de paiement obligatoire.');
      return;
    }

    const selectedInvoices = this.selectedInvoicesOrdered();
    if (selectedInvoices.length === 0) {
      this.error.set('Sélectionnez au moins une facture.');
      return;
    }

    const cashSum = this.cashSum();

    if (cashSum <= 0) {
      this.error.set('CashSum doit être supérieur à 0.');
      return;
    }

    const allocations = this.computeAllocations();

    const payload = {
      cardCode,
      paymentMethodCode,
      cashSum,
      invoices: selectedInvoices.map(x => ({
        docEntry: x.docEntry,
        sumApplied: allocations.get(x.docEntry) ?? 0
      }))
    };

    console.log('[encaissement] submit payload', payload);

    this.saving.set(true);
    this.http.post<ApiResponse<unknown>>(this.baseUrl, payload).subscribe({
      next: (res) => {
        this.saving.set(false);
        if (res?.success === false) {
          this.error.set(res.message || 'Encaissement refusé.');
          console.error('[encaissement] payment failed', res);
          return;
        }

        this.success.set('Encaissement enregistré avec succès.');
        console.log('[encaissement] payment success', res);
        this.loadOpenInvoices();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.error || err?.error?.message || err?.error?.title || err?.message || 'Erreur lors de l\'encaissement.');
        console.error('[encaissement] payment error', err);
      }
    });
  }

  private loadClients(): void {
    this.loadingClients.set(true);
    this.http.get<ApiResponse<EncaissementClient[]>>(`${this.baseUrl}/clients`).subscribe({
      next: (res) => {
        const rows = this.extractArray(res);
        const normalized = rows.map((row) => this.normalizeClient(row));
        this.clients.set(normalized);
        this.loadingClients.set(false);
        console.log('[encaissement] partenaires loaded', normalized);

        if (this.prefillCardCode) {
          this.selectedCardCode.set(this.prefillCardCode);
          this.loadOpenInvoices();
        }
      },
      error: (err) => {
        this.loadingClients.set(false);
        this.error.set(err?.error?.error || err?.error?.message || err?.error?.title || err?.message || 'Impossible de charger les partenaires.');
        console.error('[encaissement] clients loading failed', err);
      }
    });
  }

  private loadOpenInvoices(): void {
    const cardCode = this.selectedCardCode().trim();
    if (!cardCode) {
      this.invoices.set([]);
      this.selections.set([]);
      this.selectedInvoice.set(null);
      return;
    }

    this.loadingInvoices.set(true);
    this.http.get<ApiResponse<EncaissementInvoice[]>>(`${this.baseUrl}/clients/${encodeURIComponent(cardCode)}/open-invoices`).subscribe({
      next: (res) => {
        const rows = this.extractArray(res);
        rows.forEach((row, index) => {
          console.log('[encaissement] raw open-invoice payload', { index, row });
        });

        const normalized = rows
          .map((row) => this.normalizeInvoice(row))
          .filter((invoice) => this.canSelect(invoice))
          .sort((a, b) => this.compareInvoicesChronologically(a, b));

        const ignoredCount = rows.length - normalized.length;
        if (ignoredCount > 0) {
          console.warn('[encaissement] ignored invalid invoices', { ignoredCount, total: rows.length });
        }

        this.invoices.set(normalized);
        this.selections.set(normalized.map(x => ({ docEntry: x.docEntry, selected: false })));
        this.selectedInvoice.set(null);
        this.syncCashSumFromSelection();

        if (this.prefillInvoiceDocEntry) {
          const prefilledInvoice = normalized.find(x => x.docEntry === this.prefillInvoiceDocEntry);
          if (prefilledInvoice) {
            this.toggleInvoice(prefilledInvoice.docEntry, true);
          }
          this.prefillInvoiceDocEntry = null;
        }

        this.loadingInvoices.set(false);
        console.log('[encaissement] open invoices loaded', { cardCode, rows: normalized });
      },
      error: (err) => {
        this.loadingInvoices.set(false);
        this.invoices.set([]);
        this.selections.set([]);
        this.selectedInvoice.set(null);
        this.error.set(err?.error?.error || err?.error?.message || err?.error?.title || err?.message || 'Impossible de charger les factures ouvertes.');
        console.error('[encaissement] open invoices loading failed', { cardCode, err });
      }
    });
  }

  private extractArray<T>(res: ApiResponse<T[]> | any): any[] {
    if (Array.isArray(res)) return res;
    if (Array.isArray(res?.data)) return res.data;
    if (Array.isArray(res?.value)) return res.value;
    if (Array.isArray(res?.items)) return res.items;
    if (Array.isArray(res?.data?.items)) return res.data.items;
    if (Array.isArray(res?.data?.value)) return res.data.value;
    return [];
  }

  private normalizeClient(row: any): EncaissementClient {
    const cardCode = String(row?.cardCode ?? row?.CardCode ?? row?.code ?? '').trim();
    const cardName = String(row?.cardName ?? row?.CardName ?? row?.name ?? '').trim();
    const currency = String(row?.currency ?? row?.Currency ?? 'MAD').trim();
    const creditLimit = this.toNumber(row?.creditLimit ?? row?.CreditLimit ?? row?.CreditLine ?? 0);

    return {
      cardCode,
      cardName,
      currency,
      creditLimit,
      raw: row ?? {}
    };
  }

  private normalizeInvoice(row: any): EncaissementInvoice {
    const docEntry = this.toNumber(row?.docEntry ?? row?.DocEntry ?? row?.invoiceId ?? row?.InvoiceId ?? row?.id ?? row?.Id);
    const docNum = this.toNumber(row?.docNum ?? row?.DocNum ?? docEntry);
    const docDate = row?.docDate ?? row?.DocDate;
    const docDueDate = row?.docDueDate ?? row?.DocDueDate;
    const docCurrency = row?.docCurrency ?? row?.DocCurrency ?? row?.currency ?? row?.Currency;
    const docTotal = this.toNumber(row?.docTotal ?? row?.DocTotal ?? row?.total ?? 0);
    const paidToDate = this.toNumber(row?.paidToDate ?? row?.PaidToDate ?? 0);
    const computedOpenAmount = Math.max(0, docTotal - paidToDate);
    const openAmountValue = row?.openAmount ?? row?.OpenAmount;
    const openAmount = openAmountValue !== undefined && openAmountValue !== null
      ? Math.max(this.toNumber(openAmountValue), computedOpenAmount)
      : computedOpenAmount;
    const safeOpenAmount = Math.max(0, openAmount);
    const rawStatus = row?.docStatus ?? row?.DocStatus;
    const docStatus = this.isOpenDocStatus(rawStatus)
      ? 'Open'
      : safeOpenAmount > 0 ? 'Open' : 'Closed';

    return {
      docEntry,
      docNum,
      docDate,
      docDueDate,
      docCurrency,
      docTotal,
      paidToDate,
      openAmount: safeOpenAmount,
      docStatus,
      raw: row ?? {}
    };
  }

  private selectedInvoicesOrdered(): EncaissementInvoice[] {
    const selectedDocEntries = new Set(
      this.selections().filter(x => x.selected).map(x => x.docEntry)
    );

    return this.invoices()
      .filter(x => selectedDocEntries.has(x.docEntry))
      .sort((a, b) => this.compareInvoicesChronologically(a, b));
  }

  private computeAllocations(): Map<number, number> {
    const allocations = new Map<number, number>();
    let remaining = Math.max(0, this.cashSum());

    for (const invoice of this.selectedInvoicesOrdered()) {
      if (remaining <= 0) {
        allocations.set(invoice.docEntry, 0);
        continue;
      }

      const applied = Math.min(invoice.openAmount, remaining);
      allocations.set(invoice.docEntry, applied);
      remaining -= applied;
    }

    return allocations;
  }

  private compareInvoicesChronologically(a: EncaissementInvoice, b: EncaissementInvoice): number {
    const da = a.docDate ? new Date(a.docDate).getTime() : Number.MAX_SAFE_INTEGER;
    const db = b.docDate ? new Date(b.docDate).getTime() : Number.MAX_SAFE_INTEGER;
    if (da !== db) return da - db;
    return a.docEntry - b.docEntry;
  }

  private applyLocalPaymentResult(allocations: Map<number, number>): void {
    const updated = this.invoices()
      .map(invoice => {
        const applied = allocations.get(invoice.docEntry) ?? 0;
        if (applied <= 0) return invoice;

        const nextOpenAmount = Math.max(0, invoice.openAmount - applied);
        const nextPaidToDate = invoice.paidToDate + applied;

        return {
          ...invoice,
          paidToDate: nextPaidToDate,
          openAmount: nextOpenAmount,
          docStatus: nextOpenAmount > 0 ? 'Open' : 'Closed'
        };
      })
      .filter(invoice => invoice.openAmount > 0)
      .sort((a, b) => this.compareInvoicesChronologically(a, b));

    this.invoices.set(updated);
    this.selections.set(updated.map(x => ({ docEntry: x.docEntry, selected: false })));
    this.cashSum.set(0);
  }

  private syncCashSumFromSelection(): void {
    this.cashSum.set(this.totalSelectedAmount());
  }
}
