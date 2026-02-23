import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PaymentApiService } from '../../core/services/payment-api.service';
import { AuthService } from '../../core/services/auth.service';
import { Payment, CreatePayment } from '../../core/models/models';

@Component({
  selector: 'app-payments',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="payments-page">
      <!-- ── Header ── -->
      <div class="header">
        <h1>💰 Encaissements</h1>
        <button class="btn-primary" (click)="openForm()">+ Nouvel encaissement</button>
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
          <label>ID Client</label>
          <input type="number" [(ngModel)]="filterCustomerId" placeholder="ID client..."
                 (keyup.enter)="loadPayments()" />
        </div>
        <div class="filter-group">
          <label>ID Commande</label>
          <input type="number" [(ngModel)]="filterOrderId" placeholder="ID commande..."
                 (keyup.enter)="loadPayments()" />
        </div>
        <button class="btn-filter" (click)="loadPayments()">🔍 Filtrer</button>
      </div>

      <!-- ── Table ── -->
      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>Client</th>
            <th>Commande</th>
            <th>Date</th>
            <th>Montant</th>
            <th>Méthode</th>
            <th>Référence</th>
            <th>Statut</th>
            <th>SAP</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (payment of payments; track payment.id) {
            <tr>
              <td>{{ payment.id }}</td>
              <td>{{ payment.customerName ?? ('Client #' + payment.customerId) }}</td>
              <td>{{ payment.orderDocNum ?? (payment.orderId ? '#' + payment.orderId : '—') }}</td>
              <td>{{ payment.paymentDate | date:'dd/MM/yyyy' }}</td>
              <td class="amount">{{ payment.amount | currency:payment.currency:'symbol':'1.2-2' }}</td>
              <td>{{ payment.paymentMethod }}</td>
              <td>{{ payment.reference ?? '—' }}</td>
              <td>
                <span [class]="'badge badge-' + payment.status.toLowerCase()">{{ payment.status }}</span>
              </td>
              <td>
                @if (payment.sapDocEntry) {
                  <span class="sap-synced">✅ {{ payment.sapDocEntry }}</span>
                } @else {
                  <span class="sap-not-synced">—</span>
                }
              </td>
              <td class="actions">
                <button class="btn-sm btn-edit" (click)="editPayment(payment)" title="Modifier">✏️</button>
                <button class="btn-sm btn-sync" (click)="syncSap(payment)" title="Sync SAP">🔄</button>
                @if (canDelete) {
                  <button class="btn-sm btn-delete" (click)="deletePayment(payment)" title="Supprimer">🗑️</button>
                }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="10" class="empty-row">Aucun encaissement trouvé.</td></tr>
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
              <h2>{{ editingPayment ? 'Modifier l\'encaissement' : 'Nouvel encaissement' }}</h2>
              <button class="close-btn" (click)="closeForm()">✕</button>
            </div>

            <form (ngSubmit)="savePayment()" class="payment-form">
              @if (formError) {
                <div class="alert alert-error" style="margin: 0 0 1rem 0;">❌ {{ formError }}</div>
              }
              <div class="form-grid">
                <div class="form-group">
                  <label>Client ID *</label>
                  <input type="number" [(ngModel)]="form.customerId" name="customerId" required />
                </div>
                <div class="form-group">
                  <label>Commande ID</label>
                  <input type="number" [(ngModel)]="form.orderId" name="orderId" />
                </div>
                <div class="form-group">
                  <label>Date de paiement *</label>
                  <input type="date" [(ngModel)]="form.paymentDate" name="paymentDate" required />
                </div>
                <div class="form-group">
                  <label>Montant *</label>
                  <input type="number" [(ngModel)]="form.amount" name="amount" required step="0.01" min="0" />
                </div>
                <div class="form-group">
                  <label>Devise</label>
                  <select [(ngModel)]="form.currency" name="currency">
                    <option value="EUR">EUR</option>
                    <option value="USD">USD</option>
                    <option value="MAD">MAD</option>
                    <option value="GBP">GBP</option>
                  </select>
                </div>
                <div class="form-group">
                  <label>Méthode de paiement *</label>
                  <select [(ngModel)]="form.paymentMethod" name="paymentMethod" required>
                    <option value="">-- Choisir --</option>
                    <option value="Cash">Espèces</option>
                    <option value="Check">Chèque</option>
                    <option value="Transfer">Virement</option>
                    <option value="CreditCard">Carte bancaire</option>
                  </select>
                </div>
                <div class="form-group full-width">
                  <label>Référence</label>
                  <input type="text" [(ngModel)]="form.reference" name="reference"
                         placeholder="N° chèque, référence virement..." />
                </div>
              </div>

              <div class="form-actions">
                <button type="button" class="btn-secondary" (click)="closeForm()">Annuler</button>
                <button type="submit" class="btn-primary" [disabled]="saving">
                  {{ saving ? 'Enregistrement...' : (editingPayment ? 'Mettre à jour' : 'Créer') }}
                </button>
              </div>
            </form>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .payments-page { max-width: 1300px; }
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
    .amount { font-weight: 600; color: #2e7d32; }
    .empty-row { text-align: center; color: #999; padding: 2rem; }

    /* ── Badges ── */
    .badge { padding: 0.25rem 0.6rem; border-radius: 20px; font-size: 0.78rem; font-weight: 500; }
    .badge-pending   { background: #fff3e0; color: #e65100; }
    .badge-completed { background: #e8f5e9; color: #2e7d32; }
    .badge-cancelled { background: #fce4ec; color: #c62828; }
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
    .payment-form { padding: 1.5rem; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form-group { display: flex; flex-direction: column; gap: 4px; }
    .form-group.full-width { grid-column: 1 / -1; }
    .form-group label { font-size: 0.85rem; font-weight: 600; color: #555; }
    .form-group input, .form-group select {
      padding: 0.6rem 0.75rem; border: 1px solid #ddd; border-radius: 6px; font-size: 0.9rem;
    }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid #eee; }
  `]
})
export class PaymentsComponent implements OnInit {
  private paymentApi = inject(PaymentApiService);
  private auth       = inject(AuthService);
  private cdr        = inject(ChangeDetectorRef);

  // ── Data ──
  payments: Payment[] = [];
  currentPage = 1;
  pageSize    = 10;
  totalPages  = 1;

  // ── Filters ──
  filterCustomerId: number | null = null;
  filterOrderId:    number | null = null;

  // ── Messages ──
  successMsg = '';
  errorMsg   = '';

  // ── Form ──
  showForm = false;
  editingPayment: Payment | null = null;
  form: CreatePayment = this.emptyForm();
  formError = '';
  saving = false;

  // ── Role check ──
  get canDelete(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'Manager';
  }

  ngOnInit(): void {
    this.loadPayments();
  }

  loadPayments(): void {
    this.clearMessages();
    this.paymentApi.getAll(
      this.currentPage,
      this.pageSize,
      this.filterCustomerId ?? undefined,
      this.filterOrderId ?? undefined
    ).subscribe({
      next: (res: any) => {
        const payload = res.data ?? res;
        this.payments   = payload.items;
        this.totalPages = payload.totalPages;
        this.cdr.markForCheck();
      },
      error: () => { this.showError('Impossible de charger les encaissements.'); }
    });
  }

  goToPage(page: number): void {
    this.currentPage = page;
    this.loadPayments();
  }

  // ── CRUD ──
  openForm(): void {
    this.editingPayment = null;
    this.form = this.emptyForm();
    this.showForm = true;
  }

  editPayment(payment: Payment): void {
    this.editingPayment = payment;
    this.form = {
      customerId:    payment.customerId,
      orderId:       payment.orderId,
      paymentDate:   payment.paymentDate?.substring(0, 10),
      amount:        payment.amount,
      currency:      payment.currency,
      paymentMethod: payment.paymentMethod,
      reference:     payment.reference ?? ''
    };
    this.showForm = true;
  }

  savePayment(): void {
    this.clearMessages();
    this.formError = '';
    this.saving = true;
    if (this.editingPayment) {
      this.paymentApi.update(this.editingPayment.id, this.form).subscribe({
        next: (res: any) => {
          this.saving = false;
          if (res.success === false) {
            this.formError = res.message || 'Erreur lors de la mise à jour.';
            this.cdr.markForCheck();
            return;
          }
          this.showSuccess(res.message || 'Encaissement mis \u00e0 jour avec succ\u00e8s.');
          this.closeForm();
          this.loadPayments();
        },
        error: (err) => {
          this.saving = false;
          console.error('Erreur encaissement:', err);
          this.formError = err.status === 0
            ? 'Impossible de contacter le serveur. V\u00e9rifiez que le backend est d\u00e9marr\u00e9.'
            : (err.error?.message || 'Erreur lors de la mise \u00e0 jour.');
          this.cdr.markForCheck();
        }
      });
    } else {
      this.paymentApi.create(this.form).subscribe({
        next: (res: any) => {
          this.saving = false;
          if (res.success === false) {
            this.formError = res.message || 'Erreur lors de la création.';
            this.cdr.markForCheck();
            return;
          }
          this.showSuccess(res.message || 'Encaissement cr\u00e9\u00e9 avec succ\u00e8s.');
          this.closeForm();
          this.loadPayments();
        },
        error: (err) => {
          this.saving = false;
          console.error('Erreur encaissement:', err);
          this.formError = err.status === 0
            ? 'Impossible de contacter le serveur. V\u00e9rifiez que le backend est d\u00e9marr\u00e9.'
            : (err.error?.message || 'Erreur lors de la cr\u00e9ation.');
          this.cdr.markForCheck();
        }
      });
    }
  }

  deletePayment(payment: Payment): void {
    if (!confirm(`Supprimer l'encaissement #${payment.id} ?`)) return;
    this.clearMessages();
    this.paymentApi.delete(payment.id).subscribe({
      next: (res: any) => {
        if (res.success === false) { this.showError(res.message || 'Erreur lors de la suppression.'); return; }
        this.showSuccess(res.message || 'Encaissement supprimé.');
        this.loadPayments();
      },
      error: () => this.showError('Erreur lors de la suppression.')
    });
  }

  syncSap(payment: Payment): void {
    this.clearMessages();
    this.paymentApi.syncToSap(payment.id).subscribe({
      next: (res: any) => {
        if (res.success === false) { this.showError(res.message || 'Erreur lors de la synchronisation SAP.'); return; }
        this.showSuccess(res.message || `Encaissement #${payment.id} synchronisé avec SAP.`);
        this.loadPayments();
      },
      error: () => this.showError('Erreur lors de la synchronisation SAP.')
    });
  }

  closeForm(): void {
    this.showForm = false;
    this.editingPayment = null;
    this.formError = '';
    this.saving = false;
  }

  // ── Helpers ──
  private emptyForm(): CreatePayment {
    return { customerId: 0, paymentDate: '', amount: 0, currency: 'EUR', paymentMethod: '', reference: '' };
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
