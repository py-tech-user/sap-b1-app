import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VisitApiService } from '../../core/services/visit-api.service';
import { AuthService } from '../../core/services/auth.service';
import { Visit, CreateVisit } from '../../core/models/models';
import { VisitCheckinButtonComponent } from '../tracking/visit-checkin-button/visit-checkin-button.component';
import { VisitCheckoutButtonComponent } from '../tracking/visit-checkout-button/visit-checkout-button.component';

@Component({
  selector: 'app-visits',
  standalone: true,
  imports: [CommonModule, FormsModule, VisitCheckinButtonComponent, VisitCheckoutButtonComponent],
  template: `
    <div class="visits-page">
      <!-- ── Header ── -->
      <div class="header">
        <h1>📋 Visites</h1>
        <button class="btn-primary" (click)="openForm()">+ Nouvelle visite</button>
      </div>

      <!-- ── Messages ── -->
      @if (successMsg) {
        <div class="alert alert-success">✅ {{ successMsg }}</div>
      }
      @if (errorMsg) {
        <div class="alert alert-error">❌ {{ errorMsg }}</div>
      }

      <!-- ── Filters ── -->
      <div class="filters">
        <div class="filter-group">
          <label>Statut</label>
          <select [(ngModel)]="filterStatus" (ngModelChange)="loadVisits()">
            <option value="">Tous</option>
            <option value="Planned">Planifié</option>
            <option value="InProgress">En cours</option>
            <option value="Completed">Terminé</option>
            <option value="Cancelled">Annulé</option>
          </select>
        </div>
        <div class="filter-group">
          <label>ID Client</label>
          <input type="number" [(ngModel)]="filterCustomerId" placeholder="ID client..."
                 (keyup.enter)="loadVisits()" />
        </div>
        <button class="btn-filter" (click)="loadVisits()">🔍 Filtrer</button>
      </div>

      <!-- ── Table ── -->
      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>Client</th>
            <th>Date</th>
            <th>Objet</th>
            <th>Lieu</th>
            <th>Statut</th>
            <th>SAP</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (visit of visits; track visit.id) {
            <tr>
              <td>{{ visit.id }}</td>
              <td>{{ visit.customerName ?? ('Client #' + visit.customerId) }}</td>
              <td>{{ visit.visitDate | date:'dd/MM/yyyy' }}</td>
              <td>{{ visit.subject }}</td>
              <td>{{ visit.location ?? '—' }}</td>
              <td>
                <span [class]="'badge badge-' + visit.status.toLowerCase()">{{ visit.status }}</span>
              </td>
              <td>
                @if (visit.sapDocEntry) {
                  <span class="sap-synced">✅ {{ visit.sapDocEntry }}</span>
                } @else {
                  <span class="sap-not-synced">—</span>
                }
              </td>
              <td class="actions">
                @if (visit.status === 'Planned') {
                  <app-visit-checkin-button [visitId]="visit.id" (checkedIn)="onCheckInDone(visit)" />
                }
                @if (visit.status === 'InProgress') {
                  <app-visit-checkout-button [visitId]="visit.id" (checkedOut)="onCheckOutDone(visit)" />
                }
                <button class="btn-sm btn-edit" (click)="editVisit(visit)" title="Modifier">✏️</button>
                <button class="btn-sm btn-sync" (click)="syncSap(visit)" title="Sync SAP">🔄</button>
                @if (canDelete) {
                  <button class="btn-sm btn-delete" (click)="deleteVisit(visit)" title="Supprimer">🗑️</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="empty-row">Aucune visite trouvée.</td></tr>
          }
        </tbody>
      </table>

      <!-- ── Pagination ── -->
      <div class="pagination">
        <button [disabled]="currentPage <= 1" (click)="goToPage(currentPage - 1)">← Précédent</button>
        <span>Page {{ currentPage }} / {{ totalPages || 1 }}</span>
        <button [disabled]="currentPage >= totalPages" (click)="goToPage(currentPage + 1)">Suivant →</button>
      </div>

      <!-- ── Form Modal ── -->
      @if (showForm) {
        <div class="modal-overlay" (click)="closeForm()">
          <div class="modal" (click)="$event.stopPropagation()">
            <div class="modal-header">
              <h2>{{ editingVisit ? 'Modifier la visite' : 'Nouvelle visite' }}</h2>
              <button class="close-btn" (click)="closeForm()">✕</button>
            </div>

            <form (ngSubmit)="saveVisit()" class="visit-form">
              @if (formError) {
                <div class="alert alert-error" style="margin: 0 0 1rem 0;">❌ {{ formError }}</div>
              }
              <div class="form-grid">
                <div class="form-group">
                  <label>Client ID *</label>
                  <input type="number" [(ngModel)]="form.customerId" name="customerId" required />
                </div>
                <div class="form-group">
                  <label>Date de visite *</label>
                  <input type="date" [(ngModel)]="form.visitDate" name="visitDate" required />
                </div>
                <div class="form-group full-width">
                  <label>Objet *</label>
                  <input type="text" [(ngModel)]="form.subject" name="subject" required
                         placeholder="Objet de la visite..." />
                </div>
                <div class="form-group">
                  <label>Lieu</label>
                  <input type="text" [(ngModel)]="form.location" name="location"
                         placeholder="Lieu de la visite..." />
                </div>
                <div class="form-group">
                  <label>Statut</label>
                  <select [(ngModel)]="form.status" name="status">
                    <option value="Planned">Planifié</option>
                    <option value="InProgress">En cours</option>
                    <option value="Completed">Terminé</option>
                    <option value="Cancelled">Annulé</option>
                  </select>
                </div>
                <div class="form-group full-width">
                  <label>Notes</label>
                  <textarea [(ngModel)]="form.notes" name="notes" rows="3"
                            placeholder="Notes supplémentaires..."></textarea>
                </div>
              </div>

              <div class="form-actions">
                <button type="button" class="btn-secondary" (click)="closeForm()">Annuler</button>
                <button type="submit" class="btn-primary" [disabled]="saving">
                  {{ saving ? 'Enregistrement...' : (editingVisit ? 'Mettre à jour' : 'Créer') }}
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .visits-page { max-width: 1200px; }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0; }

    /* ── Alerts ── */
    .alert { padding: 0.75rem 1rem; border-radius: 8px; margin-bottom: 1rem; font-size: 0.9rem; }
    .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
    .alert-error   { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

    /* ── Filters ── */
    .filters { display: flex; gap: 1rem; align-items: flex-end; margin-bottom: 1.5rem; background: white; padding: 1rem; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,0.06); }
    .filter-group { display: flex; flex-direction: column; gap: 4px; }
    .filter-group label { font-size: 0.8rem; font-weight: 600; color: #555; }
    .filter-group select, .filter-group input { padding: 0.5rem 0.75rem; border: 1px solid #ddd; border-radius: 6px; font-size: 0.9rem; min-width: 160px; }
    .btn-filter { padding: 0.5rem 1rem; background: #667eea; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 0.9rem; }
    .btn-filter:hover { background: #5a6fd6; }

    /* ── Table ── */
    table { width: 100%; background: white; border-radius: 8px; border-collapse: collapse; box-shadow: 0 1px 4px rgba(0,0,0,0.06); }
    th, td { padding: 0.875rem 1rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f8f9fa; font-weight: 600; font-size: 0.85rem; color: #555; }
    td { font-size: 0.9rem; }
    .empty-row { text-align: center; color: #999; padding: 2rem; }

    /* ── Badges ── */
    .badge { padding: 0.25rem 0.6rem; border-radius: 20px; font-size: 0.78rem; font-weight: 500; }
    .badge-planned     { background: #e3f2fd; color: #1565c0; }
    .badge-inprogress  { background: #fff3e0; color: #e65100; }
    .badge-completed   { background: #e8f5e9; color: #2e7d32; }
    .badge-cancelled   { background: #fce4ec; color: #c62828; }
    .sap-synced     { color: #2e7d32; font-size: 0.85rem; }
    .sap-not-synced { color: #999; }

    /* ── Actions ── */
    .actions { display: flex; gap: 4px; }
    .btn-sm { padding: 0.3rem 0.5rem; border: none; border-radius: 4px; cursor: pointer; font-size: 0.85rem; }
    .btn-edit   { background: #e3f2fd; }
    .btn-edit:hover { background: #bbdefb; }
    .btn-sync   { background: #fff3e0; }
    .btn-sync:hover { background: #ffe0b2; }
    .btn-delete { background: #fce4ec; }
    .btn-delete:hover { background: #f8bbd0; }

    /* ── Pagination ── */
    .pagination { display: flex; justify-content: center; align-items: center; gap: 1rem; margin-top: 1rem; padding: 1rem; }
    .pagination button { padding: 0.5rem 1rem; border: 1px solid #ddd; background: white; border-radius: 6px; cursor: pointer; }
    .pagination button:disabled { opacity: 0.5; cursor: not-allowed; }
    .pagination span { font-size: 0.9rem; color: #555; }

    /* ── Buttons ── */
    .btn-primary   { background: #667eea; color: white; padding: 0.6rem 1.25rem; border: none; border-radius: 6px; cursor: pointer; font-size: 0.9rem; font-weight: 500; }
    .btn-primary:hover { background: #5a6fd6; }
    .btn-secondary { background: #f0f0f0; color: #333; padding: 0.6rem 1.25rem; border: none; border-radius: 6px; cursor: pointer; font-size: 0.9rem; }
    .btn-secondary:hover { background: #e0e0e0; }

    /* ── Modal ── */
    .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.5); display: flex; justify-content: center; align-items: center; z-index: 1000; }
    .modal { background: white; border-radius: 12px; width: 600px; max-width: 95vw; max-height: 90vh; overflow-y: auto; }
    .modal-header { display: flex; justify-content: space-between; align-items: center; padding: 1.25rem 1.5rem; border-bottom: 1px solid #eee; }
    .modal-header h2 { margin: 0; font-size: 1.2rem; }
    .close-btn { background: none; border: none; font-size: 1.2rem; cursor: pointer; color: #999; }
    .close-btn:hover { color: #333; }

    /* ── Form ── */
    .visit-form { padding: 1.5rem; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; font-weight: 600; color: #555; }
    .form-group input, .form-group select, .form-group textarea {
      padding: 0.6rem 0.75rem; border: 1px solid #ddd; border-radius: 6px; font-size: 0.9rem;
    }
    .form-group textarea { resize: vertical; }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid #eee; }
  `]
})
export class VisitsComponent implements OnInit {
  private visitApi = inject(VisitApiService);
  private auth     = inject(AuthService);
  private cdr      = inject(ChangeDetectorRef);

  // ── Data ──
  visits: Visit[] = [];
  currentPage = 1;
  pageSize    = 10;
  totalPages  = 1;

  // ── Filters ──
  filterStatus     = '';
  filterCustomerId: number | null = null;

  // ── Messages ──
  successMsg = '';
  errorMsg   = '';

  // ── Form ──
  showForm = false;
  editingVisit: Visit | null = null;
  form: CreateVisit = this.emptyForm();
  formError = '';
  saving = false;

  // ── Role check ──
  get canDelete(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'Manager';
  }

  ngOnInit(): void {
    this.loadVisits();
  }

  loadVisits(): void {
    this.clearMessages();
    this.visitApi.getAll(
      this.currentPage,
      this.pageSize,
      this.filterStatus || undefined,
      this.filterCustomerId ?? undefined
    ).subscribe({
      next: (res: any) => {
        const payload = res.data ?? res;
        this.visits     = payload.items;
        this.totalPages = payload.totalPages;
        this.cdr.markForCheck();
      },
      error: () => { this.showError('Impossible de charger les visites.'); }
    });
  }

  goToPage(page: number): void {
    this.currentPage = page;
    this.loadVisits();
  }

  // ── CRUD ──
  openForm(): void {
    this.editingVisit = null;
    this.form = this.emptyForm();
    this.showForm = true;
  }

  editVisit(visit: Visit): void {
    this.editingVisit = visit;
    this.form = {
      customerId: visit.customerId,
      visitDate:  visit.visitDate?.substring(0, 10),
      subject:    visit.subject,
      notes:      visit.notes ?? '',
      status:     visit.status,
      location:   visit.location ?? ''
    };
    this.showForm = true;
  }

  saveVisit(): void {
    this.clearMessages();
    this.formError = '';
    this.saving = true;
    if (this.editingVisit) {
      this.visitApi.update(this.editingVisit.id, this.form).subscribe({
        next: (res: any) => {
          this.saving = false;
          if (res.success === false) {
            this.formError = res.message || 'Erreur lors de la mise à jour.';
            this.cdr.markForCheck();
            return;
          }
          this.showSuccess(res.message || 'Visite mise \u00e0 jour avec succ\u00e8s.');
          this.closeForm();
          this.loadVisits();
        },
        error: (err) => {
          this.saving = false;
          console.error('Erreur visite:', err);
          this.formError = err.status === 0
            ? 'Impossible de contacter le serveur. V\u00e9rifiez que le backend est d\u00e9marr\u00e9.'
            : (err.error?.message || 'Erreur lors de la mise \u00e0 jour.');
          this.cdr.markForCheck();
        }
      });
    } else {
      this.visitApi.create(this.form).subscribe({
        next: (res: any) => {
          this.saving = false;
          if (res.success === false) {
            this.formError = res.message || 'Erreur lors de la création.';
            this.cdr.markForCheck();
            return;
          }
          this.showSuccess(res.message || 'Visite cr\u00e9\u00e9e avec succ\u00e8s.');
          this.closeForm();
          this.loadVisits();
        },
        error: (err) => {
          this.saving = false;
          console.error('Erreur visite:', err);
          this.formError = err.status === 0
            ? 'Impossible de contacter le serveur. V\u00e9rifiez que le backend est d\u00e9marr\u00e9.'
            : (err.error?.message || 'Erreur lors de la cr\u00e9ation.');
          this.cdr.markForCheck();
        }
      });
    }
  }

  deleteVisit(visit: Visit): void {
    if (!confirm(`Supprimer la visite #${visit.id} ?`)) return;
    this.clearMessages();
    this.visitApi.delete(visit.id).subscribe({
      next: (res: any) => {
        if (res.success === false) { this.showError(res.message || 'Erreur lors de la suppression.'); return; }
        this.showSuccess(res.message || 'Visite supprimée.');
        this.loadVisits();
      },
      error: () => this.showError('Erreur lors de la suppression.')
    });
  }

  syncSap(visit: Visit): void {
    this.clearMessages();
    this.visitApi.syncToSap(visit.id).subscribe({
      next: (res: any) => {
        if (res.success === false) { this.showError(res.message || 'Erreur lors de la synchronisation SAP.'); return; }
        this.showSuccess(res.message || `Visite #${visit.id} synchronisée avec SAP.`);
        this.loadVisits();
      },
      error: () => this.showError('Erreur lors de la synchronisation SAP.')
    });
  }

  // ── Check-in / Check-out GPS ──
  onCheckInDone(visit: Visit): void {
    this.showSuccess(`Check-in effectué pour la visite #${visit.id} 📍`);
    this.loadVisits();
  }

  onCheckOutDone(visit: Visit): void {
    this.showSuccess(`Check-out effectué pour la visite #${visit.id} 🏁`);
    this.loadVisits();
  }

  closeForm(): void {
    this.showForm = false;
    this.editingVisit = null;
    this.formError = '';
    this.saving = false;
  }

  // ── Helpers ──
  private emptyForm(): CreateVisit {
    return { customerId: 0, visitDate: '', subject: '', notes: '', status: 'Planned', location: '' };
  }

  private showSuccess(msg: string): void {
    this.successMsg = msg;
    this.cdr.markForCheck();
    setTimeout(() => { this.successMsg = ''; this.cdr.markForCheck(); }, 4000);
  }

  private showError(msg: string): void {
    this.errorMsg = msg;
    this.cdr.markForCheck();
    setTimeout(() => { this.errorMsg = ''; this.cdr.markForCheck(); }, 5000);
  }

  private clearMessages(): void {
    this.successMsg = '';
    this.errorMsg = '';
  }
}
