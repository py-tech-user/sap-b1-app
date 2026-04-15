import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

const GROUP_LABELS: { [key: string]: string } = {
  'Etranger': 'Etranger',
  'GroupeScolaire': 'Groupe scolaire',
  'LesParticuliersGP': 'Les particuliers GP',
  'LesRevendeurs': 'Les revendeurs',
  'LesSallesDeSports': 'Les salles de sports',
  'Locaux': 'Locaux',
  'OrganismePublic': 'Organisme public'
};

const CURRENCY_LABELS: { [key: string]: string } = {
  'CHF': 'CHF', 'DKK': 'DKK', 'EUR': 'Euro', 'GBP': 'GBP', 'JPY': 'JPY',
  'MAD': 'MAD', 'NOK': 'NOK', 'SEK': 'SEK', 'USD': 'USD', 'ToutesDevises': 'Toutes devises'
};

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="customer-detail">
      @if (customer) {
        <div class="header">
          <h1>{{ customer.cardName }}</h1>
          <div class="actions">
            <a [routerLink]="['/customers', customer.id, 'edit']" class="btn-edit">Modifier</a>
            <a routerLink="/customers" class="btn-back">Retour</a>
          </div>
        </div>
        
        <div class="info-grid">
          <div class="info-card">
            <label>Code</label>
            <span>{{ customer.cardCode }}</span>
          </div>
          <div class="info-card">
            <label>Type</label>
            <span [class]="'badge badge-' + customer.partnerType.toLowerCase()">
              {{ customer.partnerType }}
            </span>
          </div>
          <div class="info-card">
            <label>Nom</label>
            <span>{{ customer.cardName }}</span>
          </div>
          <div class="info-card">
            <label>Nom etranger</label>
            <span>{{ customer.foreignName || '-' }}</span>
          </div>
          <div class="info-card">
            <label>Groupe</label>
            <span>{{ getGroupLabel(customer.groupCode) }}</span>
          </div>
          <div class="info-card">
            <label>Devise</label>
            <span>{{ getCurrencyLabel(customer.currency) }}</span>
          </div>
          <div class="info-card">
            <label>N Identification entreprise</label>
            <span>{{ customer.federalTaxId || '-' }}</span>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .customer-detail { background: white; padding: 2rem; border-radius: 8px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .actions { display: flex; gap: 0.5rem; }
    .btn-edit { padding: 0.5rem 1rem; background: #667eea; color: white; border-radius: 4px; text-decoration: none; }
    .btn-back { padding: 0.5rem 1rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1.5rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 4px; }
    .info-card label { display: block; font-size: 0.85rem; color: #666; margin-bottom: 0.25rem; }
    .info-card span { font-size: 1.1rem; color: #333; }
    .badge { padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.9rem; }
    .badge-prospect { background: #fff3cd; color: #856404; }
    .badge-client { background: #d4edda; color: #155724; }
  `]
})
export class CustomerDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  customer: any;

  constructor(private http: HttpClient, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    this.http.get<any>(`${environment.apiUrl}/sap/clients`).subscribe({
      next: (res) => {
        const rows = this.extractRows(res);
        const byId = rows.find((row: any) => Number(row?.id ?? row?.DocEntry ?? 0) === Number(id));
        this.customer = byId ?? rows[Number(id) - 1] ?? null;
        this.cdr.markForCheck();
      }
    });
  }

  private extractRows(res: any): any[] {
    if (!res) return [];
    if (Array.isArray(res)) return res;
    if (Array.isArray(res.value)) return res.value;
    if (Array.isArray(res.data)) return res.data;
    if (Array.isArray(res.data?.value)) return res.data.value;
    if (Array.isArray(res.data?.items)) return res.data.items;
    if (Array.isArray(res.items)) return res.items;
    return [];
  }

  getGroupLabel(code: string): string {
    return GROUP_LABELS[code] || code;
  }

  getCurrencyLabel(code: string): string {
    return CURRENCY_LABELS[code] || code;
  }
}
