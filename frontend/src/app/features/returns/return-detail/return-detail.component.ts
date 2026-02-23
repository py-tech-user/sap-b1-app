import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink, Router } from '@angular/router';
import { ReturnApiService } from '../../../core/services/return-api.service';

@Component({
  selector: 'app-return-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="page">
      @if (item) {
        <div class="header">
          <h1>📦 Retour {{ item.returnNumber }}</h1>
          <div class="actions">
            @if (item.status === 'Pending') {
              <button class="btn-success" (click)="approve(true)">✅ Approuver</button>
              <button class="btn-danger" (click)="approve(false)">❌ Rejeter</button>
            }
            @if (item.status === 'Approved') {
              <button class="btn-info" (click)="receive()">📥 Réceptionner</button>
            }
            @if (item.status === 'Received') {
              <button class="btn-primary" (click)="process()">⚙️ Traiter</button>
            }
            <a routerLink="/returns" class="btn-back">Retour à la liste</a>
          </div>
        </div>

        <div class="info-grid">
          <div class="info-card"><label>Client</label><span>{{ item.customerName }}</span></div>
          <div class="info-card"><label>Commande</label><span>{{ item.orderDocNum }}</span></div>
          <div class="info-card"><label>Raison</label><span>{{ item.reason }}</span></div>
          <div class="info-card"><label>Date</label><span>{{ item.returnDate | date:'dd/MM/yyyy' }}</span></div>
          <div class="info-card"><label>Statut</label><span [class]="'status ' + item.status.toLowerCase()">{{ item.status }}</span></div>
          <div class="info-card"><label>Total</label><span class="total">{{ item.totalAmount | currency:'MAD':'symbol':'1.2-2' }}</span></div>
        </div>

        @if (item.comments) {
          <div class="section"><h3>Commentaires</h3><p>{{ item.comments }}</p></div>
        }

        <h3>Lignes de retour</h3>
        <table>
          <thead><tr><th>Produit</th><th>Qté</th><th>Prix unit.</th><th>Total</th><th>Motif</th></tr></thead>
          <tbody>
            @for (l of item.lines; track l.id) {
              <tr>
                <td>{{ l.productName }}</td>
                <td>{{ l.quantity }}</td>
                <td>{{ l.unitPrice | currency:'MAD':'symbol':'1.2-2' }}</td>
                <td>{{ l.lineTotal | currency:'MAD':'symbol':'1.2-2' }}</td>
                <td>{{ l.reason }}</td>
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
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; flex-wrap: wrap; gap: 0.5rem; }
    .actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .btn-success { background: #00b894; color: white; border: none; padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-danger { background: #e17055; color: white; border: none; padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-info { background: #0984e3; color: white; border: none; padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-primary { background: #667eea; color: white; border: none; padding: 0.5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-back { padding: 0.5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1.2rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 6px; }
    .info-card label { display: block; font-size: 0.8rem; color: #888; margin-bottom: .25rem; }
    .info-card span { font-size: 1rem; color: #2d3436; }
    .total { font-weight: 700; color: #667eea; }
    .status { padding: 0.25rem 0.6rem; border-radius: 20px; font-size: 0.8rem; font-weight: 600; }
    .status.pending { background: #ffeaa7; color: #d35400; }
    .status.approved { background: #d4edda; color: #155724; }
    .status.rejected { background: #f8d7da; color: #721c24; }
    .status.received { background: #d1ecf1; color: #0c5460; }
    .status.processed { background: #e2e3f1; color: #4a4e88; }
    .section { background: #f8f9fa; padding: 1rem; border-radius: 6px; margin-bottom: 1.5rem; }
    h3 { margin-bottom: 1rem; color: #2d3436; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: 0.85rem; }
    .loading { text-align: center; padding: 3rem; color: #999; }
  `]
})
export class ReturnDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  item: any;

  constructor(private api: ReturnApiService, private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.params['id'];
    this.api.getById(id).subscribe({ next: (res) => { this.item = res.data ?? res; this.cdr.markForCheck(); } });
  }

  approve(approved: boolean): void {
    this.api.approve(this.item.id, approved).subscribe({ next: () => { this.ngOnInit(); } });
  }
  receive(): void {
    this.api.receive(this.item.id).subscribe({ next: () => { this.ngOnInit(); } });
  }
  process(): void {
    this.api.process(this.item.id).subscribe({ next: () => { this.ngOnInit(); } });
  }
}
