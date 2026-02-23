import { Component, OnInit, ChangeDetectorRef, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ClaimApiService } from '../../../core/services/claim-api.service';

@Component({
  selector: 'app-claim-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  template: `
    <div class="page">
      @if (item) {
        <div class="header">
          <h1>📋 Réclamation {{ item.claimNumber }}</h1>
          <div class="actions">
            @if (item.status === 'Open' || item.status === 'InProgress') {
              <button class="btn-success" (click)="resolveDialog = true">✅ Résoudre</button>
            }
            @if (item.status === 'Resolved') {
              <button class="btn-info" (click)="closeItem()">🔒 Clôturer</button>
            }
            <a routerLink="/claims" class="btn-back">Retour</a>
          </div>
        </div>

        <div class="info-grid">
          <div class="info-card"><label>Client</label><span>{{ item.customerName }}</span></div>
          <div class="info-card"><label>Type</label><span>{{ item.type }}</span></div>
          <div class="info-card"><label>Priorité</label><span [class]="'priority ' + item.priority.toLowerCase()">{{ item.priority }}</span></div>
          <div class="info-card"><label>Statut</label><span [class]="'status ' + item.status.toLowerCase()">{{ item.status }}</span></div>
          <div class="info-card"><label>Créée le</label><span>{{ item.createdAt | date:'dd/MM/yyyy HH:mm' }}</span></div>
          <div class="info-card"><label>Assignée à</label><span>{{ item.assignedToName || 'Non assigné' }}</span></div>
        </div>

        <div class="section">
          <h3>Sujet</h3>
          <p class="subject">{{ item.subject }}</p>
          <p>{{ item.description }}</p>
        </div>

        @if (item.resolution) {
          <div class="section resolution"><h3>Résolution</h3><p>{{ item.resolution }}</p></div>
        }

        <div class="section">
          <h3>Commentaires</h3>
          @for (c of item.comments ?? []; track c.id) {
            <div class="comment" [class.internal]="c.isInternal">
              <div class="comment-header">
                <strong>{{ c.authorName }}</strong>
                <span class="comment-date">{{ c.createdAt | date:'dd/MM/yyyy HH:mm' }}</span>
                @if (c.isInternal) { <span class="internal-badge">Interne</span> }
              </div>
              <p>{{ c.comment }}</p>
            </div>
          } @empty {
            <p class="empty">Aucun commentaire</p>
          }

          <div class="add-comment">
            <textarea [(ngModel)]="newComment" placeholder="Ajouter un commentaire..." rows="2"></textarea>
            <div class="comment-actions">
              <label><input type="checkbox" [(ngModel)]="isInternal" /> Interne</label>
              <button class="btn-primary" (click)="addComment()" [disabled]="!newComment.trim()">Envoyer</button>
            </div>
          </div>
        </div>

        @if (resolveDialog) {
          <div class="modal-overlay" (click)="resolveDialog = false">
            <div class="modal" (click)="$event.stopPropagation()">
              <h3>Résolution</h3>
              <textarea [(ngModel)]="resolutionText" rows="4" placeholder="Décrivez la résolution..."></textarea>
              <div class="modal-actions">
                <button class="btn-back" (click)="resolveDialog = false">Annuler</button>
                <button class="btn-success" (click)="resolve()" [disabled]="!resolutionText.trim()">Confirmer</button>
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
    .actions { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .btn-success { background: #00b894; color: white; border: none; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-info { background: #0984e3; color: white; border: none; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-primary { background: #667eea; color: white; border: none; padding: .5rem 1rem; border-radius: 6px; cursor: pointer; font-weight: 500; }
    .btn-primary:disabled { opacity: .4; }
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; border: none; cursor: pointer; }
    .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1.2rem; margin-bottom: 2rem; }
    .info-card { background: #f8f9fa; padding: 1rem; border-radius: 6px; }
    .info-card label { display: block; font-size: .8rem; color: #888; margin-bottom: .25rem; }
    .priority { padding: .2rem .5rem; border-radius: 4px; font-size: .8rem; font-weight: 600; }
    .priority.critical { background: #e74c3c; color: white; }
    .priority.high { background: #e17055; color: white; }
    .priority.medium { background: #fdcb6e; color: #856404; }
    .priority.low { background: #dfe6e9; color: #636e72; }
    .status { padding: .25rem .6rem; border-radius: 20px; font-size: .8rem; font-weight: 600; }
    .status.open { background: #ffeaa7; color: #d35400; }
    .status.inprogress { background: #d1ecf1; color: #0c5460; }
    .status.resolved { background: #d4edda; color: #155724; }
    .status.closed { background: #e2e3e5; color: #383d41; }
    .section { background: #f8f9fa; padding: 1.2rem; border-radius: 6px; margin-bottom: 1.5rem; }
    .section h3 { margin-bottom: .8rem; }
    .subject { font-weight: 600; font-size: 1.1rem; margin-bottom: .5rem; }
    .resolution { border-left: 4px solid #00b894; }
    .comment { padding: .8rem; border-bottom: 1px solid #eee; }
    .comment.internal { background: #fff3cd; border-radius: 4px; margin-bottom: .5rem; }
    .comment-header { display: flex; gap: .8rem; align-items: center; margin-bottom: .3rem; }
    .comment-date { font-size: .8rem; color: #888; }
    .internal-badge { font-size: .7rem; background: #e17055; color: white; padding: .1rem .4rem; border-radius: 4px; }
    .empty { color: #999; }
    .add-comment { margin-top: 1rem; }
    .add-comment textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; resize: vertical; }
    .comment-actions { display: flex; justify-content: space-between; align-items: center; margin-top: .5rem; }
    .comment-actions label { font-size: .85rem; color: #666; display: flex; align-items: center; gap: .3rem; }
    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,.5); display: flex; align-items: center; justify-content: center; z-index: 1000; }
    .modal { background: white; padding: 2rem; border-radius: 8px; width: 90%; max-width: 500px; }
    .modal textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; margin: 1rem 0; }
    .modal-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .loading { text-align: center; padding: 3rem; color: #999; }
  `]
})
export class ClaimDetailComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  item: any;
  newComment = '';
  isInternal = false;
  resolveDialog = false;
  resolutionText = '';

  constructor(private api: ClaimApiService, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = +this.route.snapshot.params['id'];
    this.api.getById(id).subscribe({ next: (res) => { this.item = res.data ?? res; this.cdr.markForCheck(); } });
  }

  addComment(): void {
    this.api.addComment(this.item.id, this.newComment, this.isInternal).subscribe({
      next: () => { this.newComment = ''; this.isInternal = false; this.ngOnInit(); }
    });
  }

  resolve(): void {
    this.api.resolve(this.item.id, this.resolutionText).subscribe({
      next: () => { this.resolveDialog = false; this.resolutionText = ''; this.ngOnInit(); }
    });
  }

  closeItem(): void {
    this.api.close(this.item.id).subscribe({ next: () => this.ngOnInit() });
  }
}
