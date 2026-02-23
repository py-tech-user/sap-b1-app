import { Component, OnInit, ChangeDetectorRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ServiceTicketApiService } from '../../../core/services/service-ticket-api.service';

@Component({
  selector: 'app-ticket-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      @if (item) {
        <div class="header">
          <h1>🔧 Ticket {{ item.ticketNumber }}</h1>
          <div class="actions">
            @if (item.status === 'Open') {
              <button class="btn-info" (click)="scheduleDialog = true">📅 Planifier</button>
            }
            @if (item.status === 'Scheduled' || item.status === 'InProgress') {
              <button class="btn-success" (click)="completeDialog = true">✅ Terminer</button>
            }
            <a routerLink="/service-tickets" class="btn-back">Retour</a>
          </div>
        </div>

        <div class="info-grid">
          <div class="info-card"><label>Client</label><span>{{ item.customerName }}</span></div>
          <div class="info-card"><label>Produit</label><span>{{ item.productName || '-' }}</span></div>
          <div class="info-card"><label>Statut</label><span [class]="'status ' + item.status.toLowerCase()">{{ item.status }}</span></div>
          <div class="info-card"><label>Planifié le</label><span>{{ item.scheduledDate ? (item.scheduledDate | date:'dd/MM/yyyy') : 'Non planifié' }}</span></div>
          <div class="info-card"><label>Technicien</label><span>{{ item.technicianName || 'Non assigné' }}</span></div>
          <div class="info-card"><label>Coût total</label><span class="total">{{ item.totalCost | currency:'MAD':'symbol':'1.2-2' }}</span></div>
        </div>

        <div class="section"><h3>Description</h3><p>{{ item.description }}</p></div>

        @if (item.resolution) {
          <div class="section resolution"><h3>Résolution</h3><p>{{ item.resolution }}</p></div>
        }

        <h3>Pièces utilisées</h3>
        <table>
          <thead><tr><th>Produit</th><th>Qté</th><th>Prix unit.</th><th>Total</th></tr></thead>
          <tbody>
            @for (p of item.parts ?? []; track p.id) {
              <tr>
                <td>{{ p.productName }}</td>
                <td>{{ p.quantity }}</td>
                <td>{{ p.unitPrice | currency:'MAD':'symbol':'1.2-2' }}</td>
                <td>{{ p.lineTotal | currency:'MAD':'symbol':'1.2-2' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="empty">Aucune pièce</td></tr>
            }
          </tbody>
        </table>

        <div class="add-part">
          <h4>Ajouter une pièce</h4>
          <div class="part-row">
            <input type="number" [(ngModel)]="newPart.productId" placeholder="ID Produit" />
            <input type="number" [(ngModel)]="newPart.quantity" placeholder="Qté" min="1" />
            <input type="number" [(ngModel)]="newPart.unitPrice" placeholder="Prix" step="0.01" />
            <button class="btn-primary" (click)="addPart()">Ajouter</button>
          </div>
        </div>

        @if (scheduleDialog) {
          <div class="modal-overlay" (click)="scheduleDialog = false">
            <div class="modal" (click)="$event.stopPropagation()">
              <h3>Planifier l'intervention</h3>
              <div class="form-group"><label>Date</label><input type="date" [(ngModel)]="scheduleDate" /></div>
              <div class="modal-actions">
                <button class="btn-back" (click)="scheduleDialog = false">Annuler</button>
                <button class="btn-info" (click)="scheduleTicket()">Planifier</button>
              </div>
            </div>
          </div>
        }

        @if (completeDialog) {
          <div class="modal-overlay" (click)="completeDialog = false">
            <div class="modal" (click)="$event.stopPropagation()">
              <h3>Résolution du ticket</h3>
              <textarea [(ngModel)]="resolutionText" rows="4" placeholder="Décrivez la résolution..."></textarea>
              <div class="modal-actions">
                <button class="btn-back" (click)="completeDialog = false">Annuler</button>
                <button class="btn-success" (click)="complete()">Confirmer</button>
              </div>
            </div>
          </div>
        }
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
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; border: none; cursor: pointer; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1.2rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 6px; }
    .info-card label { display: block; font-size: .8rem; color: #888; margin-bottom: .25rem; }
    .total { font-weight: 700; color: #667eea; }
    .status { padding: .25rem .6rem; border-radius: 20px; font-size: .8rem; font-weight: 600; }
    .status.open { background: #ffeaa7; color: #d35400; }
    .status.scheduled { background: #e2e3f1; color: #4a4e88; }
    .status.inprogress { background: #d1ecf1; color: #0c5460; }
    .status.completed { background: #d4edda; color: #155724; }
    .section { background: #f8f9fa; padding: 1.2rem; border-radius: 6px; margin-bottom: 1.5rem; }
    .resolution { border-left: 4px solid #00b894; }
    h3, h4 { margin-bottom: .8rem; color: #2d3436; }
    table { width: 100%; border-collapse: collapse; margin-bottom: 1.5rem; }
    th, td { padding: .75rem; text-align: left; border-bottom: 1px solid #f1f1f1; }
    th { background: #f8f9fa; font-weight: 600; color: #555; font-size: .85rem; }
    .empty { text-align: center; color: #999; }
    .add-part { background: #f8f9fa; padding: 1.2rem; border-radius: 6px; }
    .part-row { display: flex; gap: .5rem; }
    .part-row input { flex: 1; padding: .5rem; border: 1px solid #ddd; border-radius: 6px; }
    .form-group { margin-bottom: 1rem; }
    .form-group label { display: block; font-weight: 600; margin-bottom: .3rem; }
    .form-group input { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; }
    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal { background: white; padding: 2rem; border-radius: 8px; width: 90%; max-width: 500px; }
    .modal textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; margin: 1rem 0; }
    .modal-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .loading { text-align: center; padding: 3rem; color: #999; }
  `]
})
export class TicketDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  item: any;
  scheduleDialog = false;
  completeDialog = false;
  scheduleDate = '';
  resolutionText = '';
  newPart = { productId: null as any, quantity: 1, unitPrice: null as any };

  constructor(private api: ServiceTicketApiService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.params['id'];
    this.api.getById(id).subscribe({ next: (res) => { this.item = res.data ?? res; this.cdr.markForCheck(); } });
  }

  addPart(): void {
    if (!this.newPart.productId) return;
    this.api.addPart(this.item.id, this.newPart).subscribe({
      next: () => { this.newPart = { productId: null, quantity: 1, unitPrice: null }; this.ngOnInit(); }
    });
  }

  scheduleTicket(): void {
    if (!this.scheduleDate) return;
    this.api.schedule(this.item.id, this.scheduleDate).subscribe({
      next: () => { this.scheduleDialog = false; this.ngOnInit(); }
    });
  }

  complete(): void {
    this.api.complete(this.item.id, this.resolutionText).subscribe({
      next: () => { this.completeDialog = false; this.ngOnInit(); }
    });
  }
}
