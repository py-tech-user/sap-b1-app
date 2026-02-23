import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DeliveryNoteApiService } from '../../../core/services/delivery-note-api.service';

@Component({
  selector: 'app-delivery-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      @if (item) {
        <div class="header">
          <h1>🚚 BL {{ item.deliveryNumber }}</h1>
          <div class="actions">
            @if (item.status === 'Draft') {
              <button class="btn-info" (click)="confirm()">✔️ Confirmer</button>
            }
            @if (item.status === 'Confirmed') {
              <button class="btn-primary" (click)="ship()">📤 Expédier</button>
            }
            @if (item.status === 'Shipped') {
              <button class="btn-success" (click)="deliver()">✅ Livré</button>
            }
            <a routerLink="/delivery-notes" class="btn-back">Retour</a>
          </div>
        </div>

        <div class="info-grid">
          <div class="info-card"><label>Client</label><span>{{ item.customerName }}</span></div>
          <div class="info-card"><label>Commande</label><span>{{ item.orderDocNum }}</span></div>
          <div class="info-card"><label>Date livraison</label><span>{{ item.deliveryDate | date:'dd/MM/yyyy' }}</span></div>
          <div class="info-card"><label>Statut</label><span [class]="'status ' + item.status.toLowerCase()">{{ item.status }}</span></div>
          <div class="info-card"><label>Transporteur</label><span>{{ item.carrierName || '-' }}</span></div>
          <div class="info-card"><label>N° Suivi</label><span>{{ item.trackingNumber || '-' }}</span></div>
          <div class="info-card"><label>Adresse livraison</label><span>{{ item.shippingAddress || '-' }}</span></div>
          <div class="info-card"><label>Reçu par</label><span>{{ item.receivedBy || '-' }}</span></div>
        </div>

        @if (item.notes) {
          <div class="section"><h3>Notes</h3><p>{{ item.notes }}</p></div>
        }

        <h3>Lignes</h3>
        <table>
          <thead><tr><th>Produit</th><th>Qté commandée</th><th>Qté livrée</th></tr></thead>
          <tbody>
            @for (l of item.lines; track l.id) {
              <tr>
                <td>{{ l.productName }}</td>
                <td>{{ l.orderedQuantity }}</td>
                <td>{{ l.deliveredQuantity }}</td>
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <div class="loading">Chargement...</div>
      }
    </div>
  `,
  styles: [`
    .page { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; flex-wrap: wrap; gap: .5rem; }
    .actions { display: flex; gap: .5rem; flex-wrap: wrap; }
    .btn-success { background: #00b894; color: white; border: none; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-info { background: #0984e3; color: white; border: none; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-primary { background: #667eea; color: white; border: none; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1.2rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 6px; }
    .info-card label { display: block; font-size: .8rem; color: #888; margin-bottom: .25rem; }
    .status { padding: .25rem .6rem; border-radius: 20px; font-size: .8rem; font-weight: 600; }
    .status.draft { background: #dfe6e9; color: #636e72; }
    .status.confirmed { background: #e2e3f1; color: #4a4e88; }
    .status.shipped { background: #d1ecf1; color: #0c5460; }
    .status.delivered { background: #d4edda; color: #155724; }
    .section { background: #f8f9fa; padding: 1.2rem; border-radius: 6px; margin-bottom: 1.5rem; }
    h3 { margin-bottom: .8rem; color: #2d3436; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: .75rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: .85rem; }
    .loading { text-align: center; padding: 3rem; color: #999; }
  `]
})
export class DeliveryDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  item: any;

  constructor(private api: DeliveryNoteApiService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.params['id'];
    this.api.getById(id).subscribe({ next: (res) => { this.item = res.data ?? res; this.cdr.markForCheck(); } });
  }

  confirm(): void { this.api.confirm(this.item.id).subscribe({ next: () => this.ngOnInit() }); }
  ship(): void { this.api.ship(this.item.id).subscribe({ next: () => this.ngOnInit() }); }
  deliver(): void { this.api.deliver(this.item.id).subscribe({ next: () => this.ngOnInit() }); }
}
