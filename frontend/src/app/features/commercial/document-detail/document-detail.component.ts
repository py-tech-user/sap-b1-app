import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialApiService } from '../../../core/services/commercial-api.service';
import { COMMERCIAL_META, STATUS_ACTIONS } from '../commercial-meta';
import { CommercialDocument, CommercialResource } from '../../../core/models/models';

@Component({
  selector: 'app-document-detail',
  imports: [CommonModule, RouterLink, DatePipe, DecimalPipe, ReactiveFormsModule],
  template: `
    <div class="page">
      <a [routerLink]="['/', resource()]" class="btn-sm">← Retour</a>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else if (error()) {
        <div class="error">{{ error() }}</div>
      } @else if (doc()) {
        <div class="header">
          <h1>{{ meta().icon }} {{ meta().singular | titlecase }} {{ numberOf(doc()!) }}</h1>
          <div class="header-actions">
            @for (a of allowedActions(doc()!.status); track a.label) {
              <button type="button" class="btn-primary" (click)="changeStatus(a.to)">{{ a.label }}</button>
            }
          </div>
        </div>

        <div class="info-grid">
          <div class="card"><label>Client</label><strong>{{ doc()!.customerName || '-' }}</strong></div>
          <div class="card"><label>Date</label><strong>{{ dateOf(doc()!) | date:'dd/MM/yyyy' }}</strong></div>
          <div class="card">
            <label>Statut</label>
            <strong class="status-badge" [class.open]="isOpenStatus(doc()!.status)" [class.closed]="!isOpenStatus(doc()!.status)">
              {{ doc()!.status }} ({{ statusPhase(doc()!.status) }})
            </strong>
          </div>
          <div class="card"><label>Total</label><strong>{{ totalOf(doc()!) | number:'1.2-2' }}</strong></div>
        </div>

        <div class="card">
          <h3>Lignes produits</h3>
          <table>
            <thead>
              <tr>
                <th>ItemCode</th>
                <th>Quantite</th>
                <th>Prix</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              @for (line of doc()!.lines; track $index) {
                <tr>
                  <td>{{ line.itemCode || '-' }}</td>
                  <td>{{ line.quantity }}</td>
                  <td>{{ line.unitPrice | number:'1.2-2' }}</td>
                  <td>{{ line.lineTotal ?? (line.quantity * line.unitPrice) | number:'1.2-2' }}</td>
                </tr>
              } @empty {
                <tr><td colspan="4" class="empty">Aucune ligne</td></tr>
              }
            </tbody>
          </table>
        </div>

        <div class="card">
          <h3>Relations documents</h3>
          @if (sourceDocument()) {
            <div class="source-doc">
              <span class="rel-label">Document source:</span>
              <a class="link-chip" [routerLink]="sourceDocument()!.route">{{ sourceDocument()!.label }}</a>
            </div>
          } @else {
            <div class="empty">Aucun document source.</div>
          }

          <h4>Documents générés</h4>
          @if (generatedDocuments().length > 0) {
            <ul class="links">
              @for (l of generatedDocuments(); track l.label) {
                <li><a [routerLink]="l.route">{{ l.label }}</a></li>
              }
            </ul>
          } @else {
            <div class="empty">Aucun document généré.</div>
          }
        </div>

        @if (resource() === 'quotes') {
          <div class="card action-card">
            <h3>Génération</h3>
            <button class="btn-primary" type="button" (click)="generateOrder()" [disabled]="!canGenerateOrderFromQuote()">Créer BC depuis Devis</button>
            @if (!canGenerateOrderFromQuote()) {
              <div class="action-hint">{{ generationHint('orders') }}</div>
            }
          </div>
        }

        @if (resource() === 'orders') {
          <div class="card action-card">
            <h3>Génération</h3>
            <button class="btn-primary" type="button" (click)="generateDelivery()" [disabled]="!canGenerateDeliveryFromOrder()">Créer BL depuis BC</button>
            @if (!canGenerateDeliveryFromOrder()) {
              <div class="action-hint">{{ generationHint('deliverynotes') }}</div>
            }
          </div>
        }

        @if (resource() === 'deliverynotes') {
          <div class="card action-card">
            <h3>Génération</h3>
            <button class="btn-primary" type="button" (click)="generateInvoice()" [disabled]="!canGenerateInvoiceFromDelivery()">Créer Facture depuis BL</button>
            @if (!canGenerateInvoiceFromDelivery()) {
              <div class="action-hint">{{ generationHint('invoices') }}</div>
            }
          </div>
        }

        @if (resource() === 'invoices') {
          <div class="card action-card">
            <h3>Encaissement</h3>
            <form [formGroup]="paymentForm" class="payment-form" (ngSubmit)="payInvoice()">
              <input type="number" formControlName="amount" min="0.01" step="0.01" placeholder="Montant" />
              <input type="date" formControlName="paymentDate" />
              <input formControlName="paymentMethod" placeholder="Moyen de paiement" />
              <input formControlName="reference" placeholder="Référence" />
              <button class="btn-primary" [disabled]="paymentForm.invalid" type="submit">Enregistrer paiement</button>
            </form>
          </div>
        }
      }

      @if (toast()) {
        <div class="toast">{{ toast() }}</div>
      }
    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .header { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
    .header-actions { display: flex; gap: 0.5rem; }
    .card { background: #fff; border-radius: 8px; padding: 1rem; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }
    .action-hint { margin-top: 0.5rem; color: #6b7280; font-size: 0.85rem; }
    .info-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 0.75rem; }
    .card label { display: block; color: #666; font-size: 0.78rem; margin-bottom: 0.25rem; }
    .status-badge { display: inline-block; border-radius: 999px; padding: 0.2rem 0.55rem; font-size: 0.82rem; }
    .status-badge.open { background: #e8f5e9; color: #1b5e20; }
    .status-badge.closed { background: #f3f4f6; color: #374151; }
    .source-doc { margin-bottom: 0.75rem; }
    .rel-label { color: #666; margin-right: 0.45rem; }
    .link-chip { display: inline-block; border: 1px solid #d0d7de; border-radius: 999px; padding: 0.2rem 0.55rem; text-decoration: none; }
    .link-chip:hover { background: #f6f8fa; text-decoration: none; }
    h4 { margin-top: 0.5rem; margin-bottom: 0.4rem; }
    .links { margin: 0; padding-left: 1.1rem; }
    .payment-form { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 0.5rem; }
    .payment-form input { padding: 0.45rem 0.6rem; border: 1px solid #d7d7d7; border-radius: 6px; }
    .toast { position: fixed; right: 1rem; bottom: 1rem; background: #1b5e20; color: #fff; border-radius: 8px; padding: 0.75rem 1rem; }
    .error { color: #b00020; }
    .empty { color: #888; }
    @media (max-width: 1024px) {
      .info-grid { grid-template-columns: 1fr 1fr; }
      .payment-form { grid-template-columns: 1fr; }
    }
  `]
})
export class DocumentDetailComponent {
  private readonly api = inject(CommercialApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  readonly resource = signal<CommercialResource>(this.resolveResource());
  readonly id = signal(Number(this.route.snapshot.paramMap.get('id')));
  readonly meta = computed(() => COMMERCIAL_META[this.resource()]);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly toast = signal('');
  readonly doc = signal<CommercialDocument | null>(null);

  readonly paymentForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    paymentDate: [new Date().toISOString().slice(0, 10), Validators.required],
    paymentMethod: ['Virement', Validators.required],
    reference: ['']
  });

  constructor() {
    this.load();
  }

  numberOf(doc: CommercialDocument): string {
    return doc.docNum || doc.documentNumber || `#${doc.id}`;
  }

  dateOf(doc: CommercialDocument): string | undefined {
    return doc.docDate || doc.postingDate || doc.dueDate;
  }

  totalOf(doc: CommercialDocument): number {
    return doc.docTotal ?? doc.totalAmount ?? 0;
  }

  statusPhase(status: string): 'Open' | 'Closed' {
    return this.isOpenStatus(status) ? 'Open' : 'Closed';
  }

  isOpenStatus(status: string): boolean {
    const s = (status || '').trim().toLowerCase();
    return ['draft', 'pending', 'accepted', 'confirmed', 'inprogress', 'in_preparation', 'inpreparation', 'unpaid', 'open'].includes(s);
  }

  isEditable(status: string): boolean {
    const s = (status || '').trim().toLowerCase();
    return s === 'draft' || s === 'pending' || s === 'inprogress' || s === 'unpaid';
  }

  allowedActions(status: string): { from: string; to: string; label: string }[] {
    const transitions = STATUS_ACTIONS[this.resource()] ?? [];
    const current = (status || '').trim().toLowerCase();
    return transitions.filter(t => t.from === current);
  }

  generationHint(target: CommercialResource): string {
    const d = this.doc();
    if (!d) return 'Document non chargé.';
    if (!this.isOpenStatus(d.status)) return 'Action indisponible: le document est fermé.';
    if (this.hasGeneratedType(target)) return 'Action déjà effectuée: document cible déjà généré.';
    return 'Action indisponible pour le statut actuel.';
  }

  sourceDocument(): { route: string[]; label: string } | null {
    const d = this.doc();
    if (!d) return null;

    const explicit = d.sourceDocument;
    if (explicit) {
      const resolved = this.resolveLinkedRoute(explicit.type, explicit.id);
      if (resolved) {
        return {
          route: resolved,
          label: `${explicit.type} ${explicit.docNum || '#' + explicit.id}`
        };
      }
    }

    const fallbackCandidates: Array<{ type: string; id?: number }> = [
      { type: 'quote', id: d.quoteId },
      { type: 'order', id: d.orderId },
      { type: 'deliverynote', id: d.deliveryNoteId },
      { type: 'invoice', id: d.invoiceId },
      { type: 'return', id: d.returnId }
    ];

    for (const candidate of fallbackCandidates) {
      if (!candidate.id) continue;
      const route = this.resolveLinkedRoute(candidate.type, candidate.id);
      if (!route) continue;
      return {
        route,
        label: `${candidate.type} #${candidate.id}`
      };
    }

    return null;
  }

  generatedDocuments(): Array<{ route: string[]; label: string }> {
    const d = this.doc();
    if (!d?.linkedDocuments?.length) return [];

    const currentResource = this.resource();
    const result: Array<{ route: string[]; label: string }> = [];

    for (const linked of d.linkedDocuments) {
      const mapped = this.resolveLinkedRoute(linked.type, linked.id);
      if (!mapped) continue;
      if (mapped[0] === '/' + currentResource && linked.id === d.id) continue;
      result.push({
        route: mapped,
        label: `${linked.type} ${linked.docNum || '#' + linked.id} (${linked.status || '-'})`
      });
    }

    return result;
  }

  canGenerateOrderFromQuote(): boolean {
    const d = this.doc();
    if (!d || this.resource() !== 'quotes' || !this.isOpenStatus(d.status)) return false;
    return !this.hasGeneratedType('orders');
  }

  canGenerateDeliveryFromOrder(): boolean {
    const d = this.doc();
    if (!d || this.resource() !== 'orders' || !this.isOpenStatus(d.status)) return false;
    return !this.hasGeneratedType('deliverynotes');
  }

  canGenerateInvoiceFromDelivery(): boolean {
    const d = this.doc();
    if (!d || this.resource() !== 'deliverynotes' || !this.isOpenStatus(d.status)) return false;
    return !this.hasGeneratedType('invoices');
  }

  changeStatus(status: string): void {
    this.api.updateStatus(this.resource(), this.id(), status)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de changement de statut.');
            return;
          }
          this.toast.set('Statut mis à jour.');
          this.load();
          this.clearToastLater();
        },
        error: () => this.error.set('Erreur lors du changement de statut.')
      });
  }

  generateOrder(): void {
    this.api.generateOrderFromQuote(this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de génération BC.');
            return;
          }
          const created = res.data;
          this.toast.set('BC créé depuis devis.');
          this.clearToastLater();
          if (created?.id) {
            this.router.navigate(['/orders', created.id]);
            return;
          }
          this.load();
        },
        error: () => this.error.set('Erreur lors de la génération BC.')
      });
  }

  generateDelivery(): void {
    this.api.generateDeliveryNoteFromOrder(this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de génération BL.');
            return;
          }
          const created = res.data;
          this.toast.set('BL créé depuis BC.');
          this.clearToastLater();
          if (created?.id) {
            this.router.navigate(['/deliverynotes', created.id]);
            return;
          }
          this.load();
        },
        error: () => this.error.set('Erreur lors de la génération BL.')
      });
  }

  generateInvoice(): void {
    this.api.generateInvoiceFromDelivery(this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de génération facture.');
            return;
          }
          const created = res.data;
          this.toast.set('Facture créée depuis BL.');
          this.clearToastLater();
          if (created?.id) {
            this.router.navigate(['/invoices', created.id]);
            return;
          }
          this.load();
        },
        error: () => this.error.set('Erreur lors de la génération facture.')
      });
  }

  payInvoice(): void {
    if (this.paymentForm.invalid) return;

    const value = this.paymentForm.getRawValue();
    this.api.addInvoicePayment(this.id(), {
      amount: value.amount ?? 0,
      paymentDate: value.paymentDate ?? new Date().toISOString().slice(0, 10),
      paymentMethod: value.paymentMethod ?? 'Virement',
      reference: value.reference || undefined
    }).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Encaissement refusé.');
            return;
          }
          this.toast.set('Paiement enregistré.');
          this.clearToastLater();
          this.load();
        },
        error: () => this.error.set('Erreur lors de l\'encaissement.')
      });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.getById(this.resource(), this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.doc.set(res.data ?? null);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Impossible de charger le détail.');
          this.loading.set(false);
        }
      });
  }

  private clearToastLater(): void {
    setTimeout(() => this.toast.set(''), 2500);
  }

  private resolveResource(): CommercialResource {
    const routeData = this.route.snapshot.data['resource'] as CommercialResource | undefined;
    if (routeData) return routeData;
    const parentData = this.route.snapshot.parent?.data['resource'] as CommercialResource | undefined;
    return parentData ?? 'orders';
  }

  private hasGeneratedType(target: CommercialResource): boolean {
    const generated = this.generatedDocuments();
    return generated.some(g => g.route[0] === '/' + target);
  }

  private resolveLinkedRoute(type: string, id: number): string[] | null {
    const normalized = (type || '').toLowerCase().replace(/[^a-z]/g, '');
    if (normalized.includes('quote') || normalized.includes('devis')) return ['/quotes', String(id)];
    if (normalized.includes('order') || normalized.includes('commande') || normalized === 'bc') return ['/orders', String(id)];
    if (normalized.includes('deliverynote') || normalized.includes('bonlivraison') || normalized === 'bl') return ['/deliverynotes', String(id)];
    if (normalized.includes('invoice') || normalized.includes('facture')) return ['/invoices', String(id)];
    if (normalized.includes('creditnote') || normalized.includes('avoir')) return ['/creditnotes', String(id)];
    if (normalized.includes('return') || normalized.includes('retour')) return ['/returns', String(id)];
    return null;
  }
}
