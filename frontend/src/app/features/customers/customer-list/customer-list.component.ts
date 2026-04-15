import { Component, OnDestroy, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface SapCustomerResponse {
  code?: string;
  name?: string;
  cardCode?: string;
  CardCode?: string;
  cardName?: string;
  CardName?: string;
  phone1?: string;
  Phone1?: string;
  cellular?: string;
  Cellular?: string;
  mobilePhone?: string;
  MobilePhone?: string;
  emailAddress?: string;
  EmailAddress?: string;
  email?: string;
  E_Mail?: string;
  currency?: string;
  Currency?: string;
  creditLimit?: number | string;
  CreditLimit?: number | string;
  CreditLine?: number | string;
  cardType?: string;
  CardType?: string;
  type?: string;
  U_BPType?: string;
  groupCode?: string | number;
  GroupCode?: string | number;
  GroupName?: string;
  country?: string;
  Country?: string;
  city?: string;
  City?: string;
  address?: string;
  Address?: string;
  contactPerson?: string;
  contact?: string;
  ContactPerson?: string;
  openOrdersBalance?: number | string;
  OpenOrdersBalance?: number | string;
  debitorAccount?: string;
  DebitorAccount?: string;
  peymentMethodCode?: string;
  paymentMethodCode?: string;
  status?: string;
  total?: number | string;
  Total?: number | string;
  [key: string]: unknown;
}

interface CustomerTableRow {
  code: string;
  name: string;
  phone1: string;
  cellular: string;
  email: string;
  currency: string;
  creditLimit: string;
  cardType: string;
  groupCode: string;
  country: string;
  city: string;
  raw: SapCustomerResponse;
}

const toDisplayValue = (value: unknown): string => {
  if (value === null || value === undefined) return '-';
  if (typeof value === 'string') {
    const trimmed = value.trim();
    return trimmed === '' ? '-' : trimmed;
  }
  return String(value);
};

const hasRealValue = (value: unknown): boolean => {
  if (value === null || value === undefined) return false;
  if (typeof value === 'string') return value.trim() !== '';
  return true;
};

const pickValue = (row: SapCustomerResponse, keys: string[]): unknown => {
  for (const key of keys) {
    const value = row[key as keyof SapCustomerResponse];
    if (hasRealValue(value)) return value;
  }
  return undefined;
};

const mapCustomer = (row: SapCustomerResponse): CustomerTableRow => {
  const normalizeCardType = (value: unknown): string => {
    const v = String(value ?? '').trim().toLowerCase();
    if (v === 'clid' || v === 'lead' || v === 'prospect' || v === 'l') return 'Prospect';
    if (v === 'ccustomer' || v === 'customer' || v === 'client' || v === 'c') return 'Client';
    if (v === 'csupplier' || v === 'supplier' || v === 'vendor' || v === 'fournisseur' || v === 's') return 'Fournisseur';
    return toDisplayValue(value);
  };

  return {
    code: toDisplayValue(pickValue(row, ['code', 'cardCode', 'CardCode'])),
    name: toDisplayValue(pickValue(row, ['name', 'cardName', 'CardName'])),
    phone1: toDisplayValue(pickValue(row, ['phone1', 'Phone1'])),
    cellular: toDisplayValue(pickValue(row, ['cellular', 'Cellular', 'mobilePhone', 'MobilePhone'])),
    email: toDisplayValue(pickValue(row, ['emailAddress', 'EmailAddress', 'email', 'E_Mail', 'EMailAddress'])),
    currency: toDisplayValue(pickValue(row, ['currency', 'Currency'])),
    creditLimit: toDisplayValue(pickValue(row, ['creditLimit', 'CreditLimit', 'CreditLine', 'total', 'Total'])),
    cardType: normalizeCardType(pickValue(row, ['cardType', 'CardType', 'type', 'U_BPType', 'cardTypeName'])),
    groupCode: toDisplayValue(pickValue(row, ['groupCode', 'GroupCode', 'GroupName', 'groupName'])),
    country: toDisplayValue(pickValue(row, ['country', 'Country', 'CountryCode'])),
    city: toDisplayValue(pickValue(row, ['city', 'City', 'CityName'])),
    raw: row
  };
};

const SAP_REFRESH_EVENT = 'sapCustomers:updated';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="customer-list">
        <div class="header">
          <div>
            <h1>Partenaires</h1>
          </div>
          <div class="header-actions">
            <a routerLink="/customers/new" class="btn-primary">+ Créer partenaire</a>
          </div>
        </div>

      @if (connectionMsg()) {
        <div class="alert alert-success">{{ connectionMsg() }}</div>
      }

      @if (errorMsg()) {
        <div class="alert alert-error">{{ errorMsg() }}</div>
      }

      @if (loading()) {
        <p class="status">Chargement des partenaires...</p>
      } @else {
        <form class="filters" (ngSubmit)="applyFilters()">
          <input
            type="text"
            name="searchInput"
            placeholder="Recherche (code, nom, email, téléphone...)"
            [(ngModel)]="searchInput"
            (ngModelChange)="onFilterInputChange()"
          />
          <select name="typeInput" [(ngModel)]="typeInput" (ngModelChange)="onFilterInputChange()">
            <option value="">Tous les types</option>
            <option value="client">Client</option>
            <option value="prospect">Prospect</option>
          </select>
          <button type="submit" class="btn-filter">Filtrer</button>
        </form>

        <div class="table-wrapper">
          <table>
          <thead>
            <tr>
              <th>Détails</th>
              <th>Code</th>
              <th>Raison social</th>
              <th>Téléphone</th>
              <th>Email</th>
              <th>Devise</th>
              <th>Type</th>
              <th>Pays</th>
            </tr>
          </thead>
          <tbody>
            @if (filteredCustomers().length === 0) {
              <tr>
                <td colspan="8" class="empty">Aucun partenaire disponible.</td>
              </tr>
            } @else {
              @for (customer of pagedCustomers(); track customer.code + '-' + $index) {
                <tr>
                  <td>
                    <button type="button" class="btn-detail" (click)="openDetails(customer)">Détails</button>
                  </td>
                  <td>{{ customer.code }}</td>
                  <td>{{ customer.name }}</td>
                  <td>{{ customer.phone1 }}</td>
                  <td>{{ customer.email }}</td>
                  <td>{{ customer.currency }}</td>
                  <td><span class="type-chip">{{ customer.cardType }}</span></td>
                  <td>{{ customer.country }}</td>
                </tr>
              }
            }
          </tbody>
          </table>
        </div>

        <div class="pager">
          <button type="button" class="btn-secondary" (click)="prev()" [disabled]="page() <= 1">← Précédent</button>
          <span>Page {{ page() }} / {{ totalPages() }}</span>
          <button type="button" class="btn-secondary" (click)="next()" [disabled]="page() >= totalPages()">Suivant →</button>
        </div>
      }

      @if (selectedCustomer()) {
        <div class="drawer-backdrop" (click)="closeDetails()"></div>
        <aside class="drawer" role="dialog" aria-modal="true" aria-label="Détails client">
          <div class="drawer-header">
            <h3>Détails client {{ selectedCustomerName() }}</h3>
            <button type="button" class="btn-close" (click)="closeDetails()">Fermer</button>
          </div>
          <div class="drawer-body">
            @if (selectedAdditionalDetails().length === 0) {
              <p class="empty">Aucun champ additionnel.</p>
            } @else {
              <dl class="details-list">
                @for (entry of selectedAdditionalDetails(); track entry.key) {
                  <div class="details-row">
                    <dt>{{ entry.key }}</dt>
                    <dd>{{ entry.value }}</dd>
                  </div>
                }
              </dl>
            }
          </div>
        </aside>
      }
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: flex-start; gap: 1rem; margin-bottom: 1rem; }
    .subtitle { margin: 0.2rem 0 0; color: #6b7280; font-size: 0.95rem; }
    .header-actions { display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; justify-content: flex-end; }
    .btn-primary { background: #2563eb; color: white; padding: 0.75rem 1.5rem; border-radius: 6px; text-decoration: none; font-weight: 600; }
    .btn-secondary, .btn-tertiary { padding: 0.65rem 1.25rem; border-radius: 6px; border: 1px solid #d1d5db; background: white; cursor: pointer; font-weight: 500; }
    .btn-secondary:disabled, .btn-tertiary:disabled { opacity: 0.6; cursor: not-allowed; }
    .btn-detail { padding: 0.45rem 0.7rem; border-radius: 6px; border: 1px solid #1d4ed8; background: #eff6ff; color: #1d4ed8; cursor: pointer; font-weight: 600; }
    .btn-filter { border: 1px solid #1976d2; background: #1976d2; color: #fff; border-radius: 6px; padding: 0.45rem 0.8rem; cursor: pointer; }
    .filters { display: grid; grid-template-columns: 1fr 220px auto; gap: 0.6rem; margin-bottom: 0.75rem; }
    .filters input, .filters select { width: 100%; border: 1px solid #d0d7de; border-radius: 6px; padding: 0.45rem 0.6rem; }
    .pager { display: flex; justify-content: space-between; align-items: center; margin-top: 0.75rem; }
    .table-wrapper { width: 100%; overflow-x: hidden; }
    table { width: 100%; background: white; border-radius: 10px; border-collapse: collapse; box-shadow: 0 12px 32px rgba(15, 23, 42, 0.08); overflow: hidden; }
    th, td { padding: 1rem; text-align: left; border-bottom: 1px solid #eef2ff; vertical-align: top; white-space: normal; word-break: break-word; }
    th { background: #eef2ff; font-weight: 600; text-transform: uppercase; font-size: 0.85rem; letter-spacing: 0.02em; }
    .type-chip { display: inline-block; background: #eff6ff; color: #1d4ed8; border: 1px solid #bfdbfe; border-radius: 999px; padding: 0.2rem 0.6rem; font-size: 0.78rem; font-weight: 600; white-space: nowrap; }
    .status { padding: 1rem; color: #4b5563; }
    .alert { padding: 0.9rem 1rem; border-radius: 6px; margin-bottom: 1rem; border: 1px solid transparent; }
    .alert-success { background: #ecfdf5; color: #065f46; border-color: #a7f3d0; }
    .alert-error { background: #fef2f2; color: #b91c1c; border-color: #fecaca; }
    .empty { text-align: center; color: #6b7280; font-style: italic; }
    .drawer-backdrop { position: fixed; inset: 0; background: rgba(15, 23, 42, 0.35); z-index: 30; }
    .drawer { position: fixed; top: 0; right: 0; height: 100vh; width: min(460px, 100vw); background: #fff; box-shadow: -8px 0 24px rgba(15, 23, 42, 0.18); z-index: 31; display: flex; flex-direction: column; }
    .drawer-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; padding: 1rem; border-bottom: 1px solid #e5e7eb; }
    .btn-close { padding: 0.45rem 0.75rem; border-radius: 6px; border: 1px solid #d1d5db; background: #fff; cursor: pointer; }
    .drawer-body { padding: 1rem; overflow: auto; }
    .details-list { margin: 0; }
    .details-row { display: grid; grid-template-columns: minmax(120px, 1fr) 2fr; gap: 0.75rem; padding: 0.5rem 0; border-bottom: 1px dashed #e5e7eb; }
    .details-row dt { color: #374151; font-weight: 600; }
    .details-row dd { margin: 0; color: #111827; word-break: break-word; }
    @media (max-width: 900px) { .filters { grid-template-columns: 1fr; } }
  `]
})
export class CustomerListComponent implements OnInit, OnDestroy {
  customers = signal<CustomerTableRow[]>([]);
  page = signal(1);
  pageSize = signal(15);
  totalCount = signal(0);
  searchInput = '';
  typeInput = '';
  private readonly pageCache = new Map<number, CustomerTableRow[]>();
  appliedSearch = signal('');
  appliedType = signal('');
  filteredCustomers = computed(() => {
    const query = this.appliedSearch().trim().toLowerCase();
    const type = this.appliedType().trim().toLowerCase();

    return this.customers().filter((customer) => {
      const customerType = (customer.cardType || '').toLowerCase();
      if (type && !customerType.includes(type)) return false;

      if (!query) return true;
      return [customer.code, customer.name, customer.phone1, customer.cellular, customer.email, customer.currency, customer.country]
        .join(' ')
        .toLowerCase()
        .includes(query);
    });
  });
  totalPages = computed(() => {
    const fromApi = Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
    const fromLoaded = Math.max(1, Math.ceil(this.filteredCustomers().length / this.pageSize()));
    return Math.max(fromApi, fromLoaded);
  });
  pagedCustomers = computed(() => {
    const start = (this.page() - 1) * this.pageSize();
    return this.filteredCustomers().slice(start, start + this.pageSize());
  });
  selectedCustomer = signal<SapCustomerResponse | null>(null);
  loading = signal(false);
  errorMsg = signal('');
  checkingConnection = signal(false);
  connectionMsg = signal('');

  private readonly sapEndpoint = `${environment.apiUrl}/sap/clients`;
  private readonly sessionEndpoint = `${environment.apiUrl}/sap/test`;
  private readonly refreshListener: EventListener = (event: Event) => {
    const detail = (event as CustomEvent<any>).detail;
    if (Array.isArray(detail)) {
      const mappedData = (detail as SapCustomerResponse[]).map((row) => mapCustomer(row));
      console.log(mappedData);
      this.customers.set(mappedData);
    } else {
      this.loadCustomers();
    }
  };

  constructor(private http: HttpClient) {}

  applyFilters(): void {
    this.appliedSearch.set(this.searchInput);
    this.appliedType.set(this.typeInput);
    this.page.set(1);
  }

  onFilterInputChange(): void {
    this.applyFilters();
  }

  ngOnInit(): void {
    this.applyFilters();
    this.loadCustomers();
    if (typeof window !== 'undefined') {
      window.addEventListener(SAP_REFRESH_EVENT, this.refreshListener);
    }
  }

  ngOnDestroy(): void {
    if (typeof window !== 'undefined') {
      window.removeEventListener(SAP_REFRESH_EVENT, this.refreshListener);
    }
  }

  loadCustomers(): void {
    this.loading.set(true);
    this.errorMsg.set('');
    this.connectionMsg.set('');
    this.pageCache.clear();
    this.customers.set([]);
    this.totalCount.set(0);
    this.page.set(1);
    this.requestCustomersPage(1, false);
  }

  testConnection(): void {
    this.checkingConnection.set(true);
    this.connectionMsg.set('');
    this.errorMsg.set('');
    this.http.get<any>(this.sessionEndpoint).subscribe({
      next: () => {
        this.checkingConnection.set(false);
        this.connectionMsg.set('Connexion OK. Chargement des clients...');
        this.loadCustomers();
      },
      error: (err) => {
        this.checkingConnection.set(false);
        this.errorMsg.set(err.status === 0
          ? 'Impossible de contacter le backend.'
          : (err.error?.message || 'Echec du test de connexion.'));
      }
    });
  }

  private requestCustomersPage(page: number, hasRetriedAfterWarmup: boolean): void {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(this.pageSize()));

    this.http.get<any>(this.sapEndpoint, { params }).subscribe({
      next: (res) => {
        const payload = this.extractResponseValue(res);
        const mappedData = payload.map((row) => mapCustomer(row));
        this.pageCache.set(page, mappedData);

        const totalCount = Number(
          res?.totalCount ?? res?.TotalCount ??
          res?.data?.totalCount ?? res?.data?.TotalCount ??
          res?.Data?.totalCount ?? res?.Data?.TotalCount ??
          mappedData.length
        );
        this.totalCount.set(Number.isFinite(totalCount) && totalCount >= 0 ? totalCount : mappedData.length);

        if (page === 1) {
          this.customers.set(mappedData);
          this.loading.set(false);
          this.prefetchNextPages(2);
        } else {
          this.mergeCustomers(mappedData);
        }
      },
      error: (err) => {
        if (err.status === 0 && !hasRetriedAfterWarmup) {
          this.http.get<any>(this.sessionEndpoint).subscribe({
            next: () => this.requestCustomersPage(page, true),
            error: () => {
              this.errorMsg.set('Impossible de contacter le backend.');
              this.loading.set(false);
            }
          });
          return;
        }
        this.errorMsg.set(err.status === 0
          ? 'Impossible de contacter le backend.'
          : (err.error?.message || 'Echec du chargement des clients.'));
        this.loading.set(false);
      }
    });
  }

  private prefetchNextPages(startPage: number): void {
    const maxPage = Math.max(1, Math.ceil(this.totalCount() / this.pageSize()));
    if (startPage > maxPage) return;

    this.requestCustomersPage(startPage, true);
    setTimeout(() => this.prefetchNextPages(startPage + 1), 0);
  }

  private mergeCustomers(newRows: CustomerTableRow[]): void {
    if (newRows.length === 0) return;

    const merged = new Map<string, CustomerTableRow>();
    for (const row of this.customers()) {
      const key = (row.code || '').trim();
      if (key) merged.set(key, row);
    }
    for (const row of newRows) {
      const key = (row.code || '').trim();
      if (!key) continue;
      merged.set(key, row);
    }

    this.customers.set(Array.from(merged.values()));
  }

  prev(): void {
    if (this.page() <= 1) return;
    this.page.update((p) => p - 1);
  }

  next(): void {
    if (this.page() >= this.totalPages()) return;
    this.page.update((p) => p + 1);
  }

  openDetails(customer: CustomerTableRow): void {
    this.selectedCustomer.set(customer.raw);
  }

  closeDetails(): void {
    this.selectedCustomer.set(null);
  }

  selectedCustomerName(): string {
    const selected = this.selectedCustomer();
    if (!selected) return '-';
    return toDisplayValue(pickValue(selected, ['name', 'cardName', 'CardName', 'code', 'cardCode', 'CardCode']));
  }

  selectedAdditionalDetails(): Array<{ key: string; value: string }> {
    const selected = this.selectedCustomer();
    if (!selected) return [];

    return [
      { key: 'address', value: toDisplayValue(pickValue(selected, ['address', 'Address', 'Street', 'AddressName'])) },
      { key: 'contactPerson', value: toDisplayValue(pickValue(selected, ['contactPerson', 'contact', 'ContactPerson', 'CntctPrsn'])) },
      { key: 'cardCode', value: toDisplayValue(pickValue(selected, ['code', 'cardCode', 'CardCode'])) },
      { key: 'cardName', value: toDisplayValue(pickValue(selected, ['name', 'cardName', 'CardName'])) },
      { key: 'status', value: toDisplayValue(pickValue(selected, ['status'])) },
      { key: 'mobile', value: toDisplayValue(pickValue(selected, ['cellular', 'Cellular', 'mobilePhone', 'MobilePhone'])) },
      { key: 'groupCode', value: toDisplayValue(pickValue(selected, ['groupCode', 'GroupCode', 'GroupName', 'groupName'])) },
      { key: 'city', value: toDisplayValue(pickValue(selected, ['city', 'City', 'CityName'])) },
      { key: 'openOrdersBalance', value: toDisplayValue(pickValue(selected, ['openOrdersBalance', 'OpenOrdersBalance'])) },
      { key: 'debitorAccount', value: toDisplayValue(pickValue(selected, ['debitorAccount', 'DebitorAccount'])) },
      { key: 'paymentMethodCode', value: toDisplayValue(pickValue(selected, ['peymentMethodCode', 'paymentMethodCode'])) },
      { key: 'creditLimit', value: toDisplayValue(pickValue(selected, ['creditLimit', 'CreditLimit', 'CreditLine', 'total', 'Total'])) },
      { key: 'total', value: toDisplayValue(pickValue(selected, ['total', 'Total'])) }
    ];
  }

  private extractResponseValue(res: any): SapCustomerResponse[] {
    if (!res) return [];
    if (Array.isArray(res)) return res as SapCustomerResponse[];
    if (Array.isArray(res.value)) return res.value as SapCustomerResponse[];
    if (Array.isArray(res.Value)) return res.Value as SapCustomerResponse[];
    if (Array.isArray(res.data)) return res.data as SapCustomerResponse[];
    if (Array.isArray(res.Data)) return res.Data as SapCustomerResponse[];
    if (Array.isArray(res.data?.value)) return res.data.value as SapCustomerResponse[];
    if (Array.isArray(res.Data?.value)) return res.Data.value as SapCustomerResponse[];
    if (Array.isArray(res.Data?.Value)) return res.Data.Value as SapCustomerResponse[];
    if (Array.isArray(res.data?.items)) return res.data.items as SapCustomerResponse[];
    if (Array.isArray(res.Data?.items)) return res.Data.items as SapCustomerResponse[];
    if (Array.isArray(res.Data?.Items)) return res.Data.Items as SapCustomerResponse[];
    if (Array.isArray(res.items)) return res.items as SapCustomerResponse[];
    if (Array.isArray(res.Items)) return res.Items as SapCustomerResponse[];
    return [];
  }

}
