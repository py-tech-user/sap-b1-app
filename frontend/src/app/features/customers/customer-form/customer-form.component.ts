import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

interface OptionDto {
  value: string;
  label: string;
}

interface CustomerOptions {
  partnerTypes: OptionDto[];
  groups: OptionDto[];
  currencies: OptionDto[];
}

const SAP_REFRESH_EVENT = 'sapCustomers:updated';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="customer-form">
      <h1>{{ isEdit ? 'Modifier le partenaire' : 'Nouveau partenaire' }}</h1>

      @if (successMsg) {
        <div class="alert alert-success">{{ successMsg }}</div>
      }
      @if (errorMsg) {
        <div class="alert alert-error">{{ errorMsg }}</div>
      }

      <form (ngSubmit)="onSubmit()" class="form-grid">
        <div class="form-group">
          <label>Code <span class="required">*</span></label>
          <input [(ngModel)]="customer.cardCode" name="cardCode" required [disabled]="isEdit" placeholder="Ex: C001" />
        </div>

        <div class="form-group">
          <label>Type <span class="required">*</span></label>
          <select [(ngModel)]="customer.partnerType" name="partnerType" required>
            @for (type of options.partnerTypes; track type.value) {
              <option [value]="type.value">{{ type.label }}</option>
            }
          </select>
        </div>

        <div class="form-group">
          <label>Raison social <span class="required">*</span></label>
          <input [(ngModel)]="customer.cardName" name="cardName" required placeholder="Raison social du partenaire" />
        </div>

        <div class="form-group">
          <label>Nom etranger</label>
          <input [(ngModel)]="customer.foreignName" name="foreignName" placeholder="Nom en langue etrangere" />
        </div>

        <div class="form-group">
          <label>Groupe <span class="required">*</span></label>
          <select [(ngModel)]="customer.groupCode" name="groupCode" required>
            @for (group of options.groups; track group.value) {
              <option [value]="group.value">{{ group.label }}</option>
            }
          </select>
        </div>

        <div class="form-group">
          <label>Devise <span class="required">*</span></label>
          <select [(ngModel)]="customer.currency" name="currency" required>
            @for (currency of options.currencies; track currency.value) {
              <option [value]="currency.value">{{ currency.label }}</option>
            }
          </select>
        </div>

        <div class="form-group">
          <label>N Identification entreprise</label>
          <input [(ngModel)]="customer.federalTaxId" name="federalTaxId" placeholder="Ex: FR12345678901" />
        </div>

        <div class="form-group">
          <label>Tél. 1</label>
          <input [(ngModel)]="customer.phone1" name="phone1" placeholder="Ex: +212 5 22 00 00 00" />
        </div>

        <div class="form-group">
          <label>Tél. 2</label>
          <input [(ngModel)]="customer.phone2" name="phone2" placeholder="Ex: +212 5 22 11 11 11" />
        </div>

        <div class="form-group">
          <label>Téléphone portable</label>
          <input [(ngModel)]="customer.mobilePhone" name="mobilePhone" placeholder="Ex: +212 6 61 23 45 67" />
        </div>

        <div class="form-group">
          <label>Email</label>
          <input [(ngModel)]="customer.email" name="email" type="email" placeholder="contact@entreprise.com" />
        </div>

        <div class="form-group">
          <label>Adresse</label>
          <input [(ngModel)]="customer.address" name="address" placeholder="Ex: Bd Exemple 123" />
        </div>

        <div class="form-group">
          <label>Ville</label>
          <input [(ngModel)]="customer.city" name="city" placeholder="Ex: Casablanca" />
        </div>

        <div class="form-group">
          <label>Pays</label>
          <input [(ngModel)]="customer.country" name="country" placeholder="Ex: MA" />
        </div>

        <div class="form-group">
          <label>Limite crédit</label>
          <input [(ngModel)]="customer.creditLimit" name="creditLimit" type="number" min="0" step="0.01" placeholder="Ex: 50000" />
        </div>

        <div class="form-group">
          <label>Contact</label>
          <input [(ngModel)]="customer.contact" name="contact" placeholder="Nom du contact principal" />
        </div>

        <div class="form-group">
          <label>N identification supplémentaire</label>
          <input [(ngModel)]="customer.additionalIdentificationNumber" name="additionalIdentificationNumber" placeholder="Ex: RC-123456" />
        </div>

        <div class="form-group">
          <label>N identification fiscale unifié</label>
          <input [(ngModel)]="customer.unifiedTaxIdentificationNumber" name="unifiedTaxIdentificationNumber" placeholder="Ex: IFU-99887766" />
        </div>
        
        <div class="actions full-width">
          <a routerLink="/customers" class="btn-cancel">Annuler</a>
          <button type="submit" class="btn-submit" [disabled]="loading">
            {{ loading ? 'Enregistrement...' : 'Enregistrer' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .customer-form {
      max-width: 980px;
      background: white;
      padding: 2rem;
      border-radius: 10px;
      box-shadow: 0 6px 24px rgba(15, 23, 42, 0.08);
      margin: 0 auto;
    }
    h1 { margin-bottom: 1.5rem; color: #222; }
    .alert { padding: 0.75rem 1rem; border-radius: 6px; margin-bottom: 1rem; font-size: 0.9rem; }
    .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
    .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 1rem 1.25rem;
      align-items: start;
    }
    .form-group { margin-bottom: 0.25rem; }
    label { display: block; margin-bottom: 0.5rem; color: #555; font-weight: 500; }
    .required { color: #dc3545; }
    input, select {
      width: 100%;
      padding: 0.72rem 0.75rem;
      border: 1px solid #d7dce5;
      border-radius: 6px;
      box-sizing: border-box;
      font-size: 0.98rem;
      background: #fff;
    }
    input:focus, select:focus { outline: none; border-color: #667eea; box-shadow: 0 0 0 2px rgba(102,126,234,0.2); }
    input:disabled { background: #f5f5f5; cursor: not-allowed; }
    .full-width { grid-column: 1 / -1; }
    .actions { display: flex; gap: 1rem; margin-top: 1rem; padding-top: 1.2rem; border-top: 1px solid #eee; }
    .btn-cancel { padding: 0.75rem 1.5rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
    .btn-cancel:hover { background: #ddd; }
    .btn-submit { padding: 0.75rem 1.5rem; background: #667eea; color: white; border: none; border-radius: 4px; cursor: pointer; }
    .btn-submit:hover { background: #5a6fd6; }
    .btn-submit:disabled { opacity: 0.7; cursor: not-allowed; }

    @media (max-width: 900px) {
      .customer-form {
        max-width: 100%;
        padding: 1.25rem;
      }
      .form-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class CustomerFormComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  
  customer: any = {
    cardCode: '',
    partnerType: 'Client',
    cardName: '',
    foreignName: '',
    groupCode: 'Locaux',
    currency: 'EUR',
    federalTaxId: '',
    phone1: '',
    phone2: '',
    mobilePhone: '',
    email: '',
    address: '',
    city: '',
    country: 'MA',
    creditLimit: '',
    contact: '',
    additionalIdentificationNumber: '',
    unifiedTaxIdentificationNumber: ''
  };
  
  options: CustomerOptions = {
    partnerTypes: [{ value: 'Client', label: 'Client' }, { value: 'Prospect', label: 'Prospect' }],
    groups: [
      { value: 'Etranger', label: 'Etranger' },
      { value: 'GroupeScolaire', label: 'Groupe scolaire' },
      { value: 'LesParticuliersGP', label: 'Les particuliers GP' },
      { value: 'LesRevendeurs', label: 'Les revendeurs' },
      { value: 'LesSallesDeSports', label: 'Les salles de sports' },
      { value: 'Locaux', label: 'Locaux' },
      { value: 'OrganismePublic', label: 'Organisme public' }
    ],
    currencies: [
      { value: 'CHF', label: 'CHF' }, { value: 'DKK', label: 'DKK' }, { value: 'EUR', label: 'Euro' },
      { value: 'GBP', label: 'GBP' }, { value: 'JPY', label: 'JPY' }, { value: 'MAD', label: 'MAD' },
      { value: 'NOK', label: 'NOK' }, { value: 'SEK', label: 'SEK' }, { value: 'USD', label: 'USD' },
      { value: 'ToutesDevises', label: 'Toutes devises' }
    ]
  };
  
  isEdit = false;
  loading = false;
  successMsg = '';
  errorMsg = '';

  constructor(private http: HttpClient, private router: Router, private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.loadOptions();
    const id = this.route.snapshot.params['id'];
    if (id) {
      this.isEdit = true;
      this.http.get<any>(`${environment.apiUrl}/sap/clients`).subscribe({
        next: (res) => {
          const rows = this.extractResponseValue(res);
          const byId = rows.find((row: any) => Number(row?.id ?? row?.DocEntry ?? 0) === Number(id));
          const selected = byId ?? rows[Number(id) - 1] ?? null;
          if (!selected) {
            this.errorMsg = 'Impossible de charger le partenaire.';
            this.cdr.markForCheck();
            return;
          }
          this.customer = this.withCustomerDefaults({
            cardCode: selected.code ?? selected.cardCode ?? selected.CardCode,
            cardName: selected.name ?? selected.cardName ?? selected.CardName,
            currency: selected.currency ?? selected.Currency ?? 'EUR',
            email: selected.emailAddress ?? selected.email ?? selected.EmailAddress,
            phone1: selected.phone1 ?? selected.Phone1,
            mobilePhone: selected.cellular ?? selected.mobilePhone ?? selected.Cellular,
            address: selected.address ?? selected.Address,
            city: selected.city ?? selected.City,
            country: selected.country ?? selected.Country,
            creditLimit: selected.creditLimit ?? selected.CreditLimit,
            contact: selected.contactPerson ?? selected.contact
          });
          this.cdr.markForCheck();
        },
        error: () => { this.errorMsg = 'Impossible de charger le partenaire.'; this.cdr.markForCheck(); }
      });
    }
  }

  private withCustomerDefaults(raw: any): any {
    return {
      cardCode: '',
      partnerType: 'Client',
      cardName: '',
      foreignName: '',
      groupCode: 'Locaux',
      currency: 'EUR',
      federalTaxId: '',
      phone1: '',
      phone2: '',
      mobilePhone: '',
      email: '',
      address: '',
      city: '',
      country: 'MA',
      creditLimit: '',
      contact: '',
      additionalIdentificationNumber: '',
      unifiedTaxIdentificationNumber: '',
      ...raw
    };
  }

  loadOptions(): void {
    this.http.get<any>(`${environment.apiUrl}/customers/options`).subscribe({
      next: (res) => { if (res.data) { this.options = res.data; this.cdr.markForCheck(); } },
      error: () => {}
    });
  }

  onSubmit(): void {
    this.loading = true;
    this.successMsg = '';
    this.errorMsg = '';

    if (this.isEdit) {
      this.saveLegacyCustomer();
      return;
    }

    if (!this.customer.cardCode?.trim() || !this.customer.cardName?.trim()) {
      this.errorMsg = 'Le code client et le nom client sont obligatoires.';
      this.loading = false;
      this.cdr.markForCheck();
      return;
    }

    const payload = {
      // Backend contract (new SAP clients API)
      code: this.customer.cardCode.trim(),
      name: this.customer.cardName.trim(),
      phone1: (this.customer.phone1 || '').trim(),
      cellular: (this.customer.mobilePhone || '').trim(),
      emailAddress: (this.customer.email || '').trim(),
      currency: (this.customer.currency || 'EUR').trim(),
      cardType: (this.customer.partnerType || 'Client').trim(),
      groupCode: (this.customer.groupCode || '').trim(),
      country: (this.customer.country || '').trim(),
      city: (this.customer.city || '').trim(),
      address: (this.customer.address || '').trim(),
      contactPerson: (this.customer.contact || '').trim(),
      creditLimit: this.customer.creditLimit === '' || this.customer.creditLimit === null || this.customer.creditLimit === undefined
        ? null
        : Number(this.customer.creditLimit),

      // Compatibility aliases for older adapters
      cardCode: this.customer.cardCode.trim(),
      cardName: this.customer.cardName.trim(),
      Phone1: (this.customer.phone1 || '').trim(),
      Cellular: (this.customer.mobilePhone || '').trim(),
      EmailAddress: (this.customer.email || '').trim(),
      Currency: (this.customer.currency || 'EUR').trim(),
      CardType: (this.customer.partnerType || 'Client').trim(),
      GroupCode: (this.customer.groupCode || '').trim(),
      Country: (this.customer.country || '').trim(),
      City: (this.customer.city || '').trim(),
      Address: (this.customer.address || '').trim(),
      ContactPerson: (this.customer.contact || '').trim(),
      CreditLimit: this.customer.creditLimit === '' || this.customer.creditLimit === null || this.customer.creditLimit === undefined
        ? null
        : Number(this.customer.creditLimit),
      CreditLine: this.customer.creditLimit === '' || this.customer.creditLimit === null || this.customer.creditLimit === undefined
        ? null
        : Number(this.customer.creditLimit),
      mobilePhone: (this.customer.mobilePhone || '').trim(),
      email: (this.customer.email || '').trim()
    };

    console.log('[customer-form] create payload', payload);

    this.http.post<any>(`${environment.apiUrl}/sap/clients`, payload).subscribe({
      next: (res) => {
        this.successMsg = res?.message || 'Client créé.';
        this.loading = false;
        this.cdr.markForCheck();
        this.refreshSapCustomers();
      },
      error: (err) => {
        this.errorMsg = err.status === 0
          ? 'Impossible de joindre le service clients.'
          : this.extractSapError(err);
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  private extractSapError(err: any): string {
    const explicit = String(err?.error?.error ?? '').trim();
    if (explicit) return explicit;

    const sapValue = String(err?.error?.sapResponse?.error?.message?.value ?? '').trim();
    if (sapValue) return sapValue;

    const message = String(err?.error?.message ?? '').trim();
    if (message && message.toLowerCase() !== 'erreur sap') return message;

    return 'Création du client impossible.';
  }

  private saveLegacyCustomer(): void {
    const payload = {
      ...this.customer,
      phone: this.customer.phone1 || this.customer.mobilePhone || this.customer.phone || '',
      email: this.customer.email || ''
    };

    this.http.put<any>(`${environment.apiUrl}/customers/${this.customer.id}`, payload).subscribe({
      next: (res) => {
        if (res.success === false) {
          this.errorMsg = res.message || 'Erreur.';
          this.loading = false;
          this.cdr.markForCheck();
          return;
        }
        this.successMsg = res.message || 'Partenaire modifié.';
        this.loading = false;
        this.cdr.markForCheck();
        setTimeout(() => this.router.navigate(['/customers']), 1200);
      },
      error: (err) => {
        this.errorMsg = err.status === 0 ? 'Impossible de contacter le serveur.' : (err.error?.message || 'Erreur ' + err.status);
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  private refreshSapCustomers(): void {
    this.http.get<any>(`${environment.apiUrl}/sap/clients`).subscribe({
      next: (res) => {
        const payload = this.extractResponseValue(res);
        this.emitSapRefresh(payload);
        setTimeout(() => this.router.navigate(['/customers']), 1200);
      },
      error: () => {
        setTimeout(() => this.router.navigate(['/customers']), 1200);
      }
    });
  }

  private emitSapRefresh(rows: any[]): void {
    if (typeof window === 'undefined') return;
    window.dispatchEvent(new CustomEvent(SAP_REFRESH_EVENT, { detail: rows }));
  }

  private extractResponseValue(res: any): any[] {
    if (!res) return [];
    if (Array.isArray(res)) return res;
    if (Array.isArray(res.value)) return res.value;
    if (Array.isArray(res.data)) return res.data;
    if (Array.isArray(res.data?.value)) return res.data.value;
    if (Array.isArray(res.data?.items)) return res.data.items;
    if (Array.isArray(res.items)) return res.items;
    return [];
  }
}
