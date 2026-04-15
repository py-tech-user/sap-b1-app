import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommercialApiService } from '../../core/services/commercial-api.service';
import { COMMERCIAL_META, STATUS_ACTIONS } from './commercial-meta';
import { CommercialDocument, CommercialResource, SaveCommercialDocumentDto } from '../../core/models/models';

@Component({
  selector: 'app-document-detail',
  imports: [CommonModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <div class="page">
      <a [routerLink]="backRoute()" class="btn-sm">← Retour</a>

      @if (loading()) {
        <div class="loading">Chargement...</div>
      } @else if (error()) {
        <div class="error">{{ error() }}</div>
      } @else if (doc()) {
        <div class="header">
          <h1>{{ meta().icon }} {{ meta().singular | titlecase }} {{ numberOf(doc()!) }}</h1>
          <div class="header-actions">
            @if (resource() === 'quotes' || resource() === 'orders') {
              <a class="btn-primary" [routerLink]="['/', resource(), id(), 'edit']" [queryParams]="detailQueryParams()" [class.disabled-link]="!canEditDocument()">Modifier</a>
              <button type="button" class="btn-primary btn-danger" [class.btn-disabled]="!canCancelDocument()" (click)="cancelDocument()" [disabled]="!canCancelDocument()">Annuler</button>
            }
            @if (resource() === 'invoices') {
              <button type="button" class="btn-primary" (click)="goToEncaissement()" [disabled]="!canEncaisser()">Encaisser</button>
            }
            @if (canShowCloseButton()) {
              <button type="button" class="btn-primary" (click)="closeDocument()">Clôturer</button>
            }
            @for (a of allowedActions(doc()!.status); track a.label) {
              <button type="button" class="btn-primary" (click)="changeStatus(a.to)">{{ a.label }}</button>
            }
            @if (toast()) {
              <span class="action-feedback success">{{ toast() }}</span>
            }
          </div>
        </div>

        <div class="info-grid">
          <div class="card"><label>Client</label><strong>{{ doc()!.customerName || '-' }}</strong></div>
          <div class="card"><label>Date</label><strong>{{ dateOf(doc()!) | date:'dd/MM/yyyy' }}</strong></div>
          <div class="card">
            <label>Statut</label>
            <strong class="status-badge" [class.open]="isOpenStatus(doc()!.status)" [class.closed]="!isOpenStatus(doc()!.status)">
              {{ statusPhase(doc()!.status) }}
            </strong>
          </div>
          <div class="card"><label>Total</label><strong>{{ totalOf(doc()!) | number:'1.2-2' }}</strong></div>
        </div>

        <div class="card">
          <h3>Lignes produits</h3>
          <table>
            <thead>
              <tr>
                @if (canSelectLinesForGeneration()) {
                  <th>Sel.</th>
                }
                <th>ItemCode</th>
                <th>Nom</th>
                <th>Statut ligne</th>
                <th>Quantite</th>
                <th>Prix</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              @for (line of doc()!.lines; track $index) {
                <tr>
                  @if (canSelectLinesForGeneration()) {
                    <td>
                      <input
                        type="checkbox"
                        [checked]="isLineSelected(line, $index)"
                        [disabled]="!isOpenLineStatus(line)"
                        (change)="toggleLineSelection(line, $index, $event)" />
                    </td>
                  }
                  <td>{{ line.itemCode || '-' }}</td>
                  <td>{{ line.itemName || '-' }}</td>
                  <td>
                    <span class="status-badge" [class.open]="isOpenLineStatus(line)" [class.closed]="!isOpenLineStatus(line)">
                      {{ lineStatusLabel(line) }}
                    </span>
                  </td>
                  <td>{{ line.quantity }}</td>
                  <td>{{ line.unitPrice | number:'1.2-2' }}</td>
                  <td>{{ line.lineTotal ?? (line.quantity * line.unitPrice) | number:'1.2-2' }}</td>
                </tr>
              } @empty {
                <tr><td [attr.colspan]="canSelectLinesForGeneration() ? 7 : 6" class="empty">Aucune ligne</td></tr>
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

      }

    </div>
  `,
  styles: [`
    .page { display: flex; flex-direction: column; gap: 1rem; }
    .header { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
    .header-actions { display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap; }
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
    .disabled-link { pointer-events: none; opacity: 0.5; }
    .btn-danger { background: #c62828; border-color: #c62828; }
    .btn-disabled { opacity: 0.5; cursor: not-allowed; }
    h4 { margin-top: 0.5rem; margin-bottom: 0.4rem; }
    .links { margin: 0; padding-left: 1.1rem; }
    .action-feedback { font-weight: 700; font-size: 0.9rem; }
    .action-feedback.success { color: #1b5e20; }
    .error { color: #b00020; }
    .empty { color: #888; }
    @media (max-width: 1024px) {
      .info-grid { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class DocumentDetailComponent {
  private readonly api = inject(CommercialApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly resource = signal<CommercialResource>(this.resolveResource());
  readonly id = signal(Number(this.route.snapshot.paramMap.get('id')));
  readonly returnTo = signal(this.route.snapshot.queryParamMap.get('returnTo') ?? '');
  readonly meta = computed(() => COMMERCIAL_META[this.resource()]);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly toast = signal('');
  readonly doc = signal<CommercialDocument | null>(null);
  readonly selectedGenerationLines = signal<number[]>([]);

  constructor() {
    this.load();
  }

  canSelectLinesForGeneration(): boolean {
    const d = this.doc();
    if (!d) return false;
    if (!this.isOpenStatus(d.status)) return false;
    return this.resource() === 'quotes' || this.resource() === 'orders' || this.resource() === 'deliverynotes';
  }

  isLineSelected(line: any, index: number): boolean {
    const lineNum = this.getLineNum(line, index);
    return this.selectedGenerationLines().includes(lineNum);
  }

  toggleLineSelection(line: any, index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const lineNum = this.getLineNum(line, index);
    const next = new Set(this.selectedGenerationLines());
    if (input.checked) next.add(lineNum);
    else next.delete(lineNum);
    this.selectedGenerationLines.set(Array.from(next));
  }

  canEditDocument(): boolean {
    const d = this.doc();
    if (!d) return false;
    if (this.resource() !== 'quotes' && this.resource() !== 'orders') return false;
    return this.isOpenStatus(d.status);
  }

  canCancelDocument(): boolean {
    const d = this.doc();
    if (!d) return false;
    return this.canEditDocument() && !this.hasClosedLine(d);
  }

  backRoute(): string {
    const target = this.returnTo().trim();
    return target.startsWith('/') ? target : `/${this.resource()}`;
  }

  detailQueryParams(): { returnTo: string } {
    return { returnTo: this.backRoute() };
  }

  cancelDocument(): void {
    if (!this.canCancelDocument()) {
      if (this.doc() && this.hasClosedLine(this.doc()!)) {
        this.error.set('Annulation impossible: ce document contient au moins une ligne clôturée.');
        return;
      }

      this.error.set('Annulation autorisee uniquement pour un devis/BC en statut Open.');
      return;
    }

    const d = this.doc();
    if (!d) return;
    if (!confirm(`Confirmer l'annulation de ${this.numberOf(d)} ?`)) return;

    this.api.delete(this.resource(), this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Annulation impossible.');
            return;
          }
          this.toast.set('Document annule.');
          this.clearToastLater();
          this.router.navigateByUrl(this.backRoute());
        },
        error: (err) => {
          this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors de l\'annulation.');
        }
      });
  }

  lineStatusLabel(line: any): string {
    return this.isOpenLineStatus(line) ? 'En attente' : 'Clôturée';
  }

  isOpenLineStatus(line: any): boolean {
    const token = this.normalizeLineStatusToken(line?.lineStatus ?? line?.LineStatus ?? 'Open');
    return !(token === 'c' || token.includes('close') || token.includes('ferme') || token.includes('clotur') || token === 'bostclose');
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

  statusPhase(status: string): 'En attente' | 'Clôturé' {
    return this.isOpenStatus(status) ? 'En attente' : 'Clôturé';
  }

  isOpenStatus(status: string): boolean {
    const s = (status || '').trim().toLowerCase();
    const compact = s.replace(/[\s_-]/g, '');
    return s === 'open'
      || s === 'o'
      || s === 'en attente'
      || compact === 'bostopen'
      || compact === 'enattente'
      || compact === 'unpaid'
      || compact === 'partiallypaid'
      || compact === 'partialpaid'
      || compact === 'overdue'
      || (compact.includes('open') && !compact.includes('close'));
  }

  isEditable(status: string): boolean {
    return this.isOpenStatus(status);
  }

  allowedActions(status: string): { from: string; to: string; label: string }[] {
    if (this.resource() !== 'quotes' && this.resource() !== 'orders') return [];
    if (!this.isOpenStatus(status)) return [];
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
    if (!d) return [];

    const currentResource = this.resource();
    const result: Array<{ route: string[]; label: string }> = [];
    const seen = new Set<string>();

    for (const linked of d.linkedDocuments ?? []) {
      const mapped = this.resolveLinkedRoute(linked.type, linked.id);
      if (!mapped) continue;
      if (mapped[0] === '/' + currentResource && linked.id === d.id) continue;

      const key = `${mapped[0]}::${mapped[1]}`;
      if (seen.has(key)) continue;
      seen.add(key);

      result.push({
        route: mapped,
        label: `${linked.type} ${linked.docNum || '#' + linked.id} (${linked.status || '-'})`
      });
    }

    const fallbackGenerated: Array<{ type: string; id?: number }> = [
      { type: 'order', id: d.orderId },
      { type: 'deliverynote', id: d.deliveryNoteId },
      { type: 'invoice', id: d.invoiceId },
      { type: 'creditnote', id: d.creditNoteId },
      { type: 'return', id: d.returnId }
    ];

    for (const candidate of fallbackGenerated) {
      if (!candidate.id) continue;
      const mapped = this.resolveLinkedRoute(candidate.type, candidate.id);
      if (!mapped) continue;
      if (mapped[0] === '/' + currentResource && candidate.id === d.id) continue;

      const key = `${mapped[0]}::${mapped[1]}`;
      if (seen.has(key)) continue;
      seen.add(key);

      result.push({
        route: mapped,
        label: `${candidate.type} #${candidate.id}`
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

  canEncaisser(): boolean {
    const d = this.doc();
    if (!d || this.resource() !== 'invoices') return false;
    return !!String(d.cardCode ?? '').trim() && (d.id ?? 0) > 0;
  }

  canShowCloseButton(): boolean {
    const d = this.doc();
    if (!d) return false;
    if (!this.isOpenStatus(d.status)) return false;

    if (this.resource() === 'quotes') {
      return this.hasGeneratedType('orders') || (d.orderId ?? 0) > 0;
    }

    if (this.resource() === 'orders') {
      return this.hasGeneratedType('deliverynotes') || (d.deliveryNoteId ?? 0) > 0;
    }

    if (this.resource() === 'deliverynotes') {
      return this.hasGeneratedType('invoices') || (d.invoiceId ?? 0) > 0;
    }

    return false;
  }

  closeDocument(): void {
    if (!this.canShowCloseButton()) {
      this.error.set('Cloture indisponible: document non encore transforme.');
      return;
    }

    const d = this.doc();
    if (!d) return;
    if (!confirm(`Confirmer la cloture de ${this.numberOf(d)} ?`)) return;

    this.api.close(this.resource(), this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de cloture.');
            return;
          }
          this.toast.set('Document cloture dans SAP.');
          this.load();
          this.clearToastLater();
        },
        error: (err) => this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors de la cloture.')
      });
  }

  goToEncaissement(): void {
    const d = this.doc();
    if (!d) return;

    const cardCode = String(d.cardCode ?? '').trim();
    const invoiceDocEntry = Number(d.id ?? 0);
    if (!cardCode || !Number.isFinite(invoiceDocEntry) || invoiceDocEntry <= 0) {
      this.error.set('Impossible d\'ouvrir l\'encaissement: CardCode ou DocEntry manquant.');
      return;
    }

    this.router.navigate(['/encaissement'], {
      queryParams: {
        cardCode,
        invoiceDocEntry
      }
    });
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
    const selectedLineNums = this.selectedLineNumsForGeneration();
    if (selectedLineNums.length === 0) {
      this.error.set('Aucune ligne en attente sélectionnée pour la génération.');
      return;
    }

    this.api.generateOrderFromQuote(this.id(), selectedLineNums)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de génération BC.');
            return;
          }
          const created = res.data;
          this.toast.set('BC créé depuis devis.');
          this.selectedGenerationLines.set([]);
          this.clearToastLater();
          if (created?.id) {
            this.router.navigate(['/orders', created.id]);
            return;
          }
          this.load();
        },
        error: (err) => {
          if (err?.status === 404 || err?.status === 405) {
            this.generateByCreateFallback('orders', 'BC créé depuis devis.', 'Erreur lors de la génération BC.');
            return;
          }
          this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors de la génération BC.');
        }
      });
  }

  generateDelivery(): void {
    const selectedLineNums = this.selectedLineNumsForGeneration();
    if (selectedLineNums.length === 0) {
      this.error.set('Aucune ligne en attente sélectionnée pour la génération.');
      return;
    }

    this.api.generateDeliveryNoteFromOrder(this.id(), selectedLineNums)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de génération BL.');
            return;
          }
          const created = res.data;
          this.toast.set('BL créé depuis BC.');
          this.selectedGenerationLines.set([]);
          this.clearToastLater();
          if (created?.id) {
            this.router.navigate(['/deliverynotes', created.id]);
            return;
          }
          this.load();
        },
        error: (err) => {
          if (err?.status === 404 || err?.status === 405) {
            this.generateByCreateFallback('deliverynotes', 'BL créé depuis BC.', 'Erreur lors de la génération BL.');
            return;
          }
          this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors de la génération BL.');
        }
      });
  }

  generateInvoice(): void {
    const selectedLineNums = this.selectedLineNumsForGeneration();
    if (selectedLineNums.length === 0) {
      this.error.set('Aucune ligne en attente sélectionnée pour la génération.');
      return;
    }

    this.api.generateInvoiceFromDelivery(this.id(), selectedLineNums)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false) {
            this.error.set(res.message || 'Echec de génération facture.');
            return;
          }
          const created = res.data;
          this.toast.set('Facture créée depuis BL.');
          this.selectedGenerationLines.set([]);
          this.clearToastLater();
          if (created?.id) {
            this.router.navigate(['/invoices', created.id]);
            return;
          }
          this.load();
        },
        error: (err) => {
          if (err?.status === 404 || err?.status === 405) {
            this.generateByCreateFallback('invoices', 'Facture créée depuis BL.', 'Erreur lors de la génération facture.');
            return;
          }
          this.error.set(err?.error?.error || err?.error?.message || 'Erreur lors de la génération facture.');
        }
      });
  }

  private generateByCreateFallback(target: CommercialResource, successMessage: string, defaultErrorMessage: string): void {
    const payload = this.buildCreatePayloadFromCurrentDoc();
    if (!payload) {
      this.error.set('Impossible de générer: données source incomplètes (client/lignes).');
      return;
    }

    this.api.create(target, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          if (res.success === false || !res.data) {
            this.error.set(res.message || defaultErrorMessage);
            return;
          }

          this.toast.set(successMessage);
          this.clearToastLater();
          const created = res.data;
          if (created?.id) {
            this.router.navigate(['/', target, created.id]);
            return;
          }
          this.load();
        },
        error: (err) => {
          this.error.set(err?.error?.error || err?.error?.message || defaultErrorMessage);
        }
      });
  }

  private buildCreatePayloadFromCurrentDoc(): SaveCommercialDocumentDto | null {
    const d = this.doc();
    if (!d) return null;

    const cardCode = String(d.cardCode ?? '').trim();
    if (!cardCode) return null;

    const sourceLines = Array.isArray(d.lines) ? d.lines : [];
    const lines = sourceLines
      .map((line) => ({
        itemCode: String(line.itemCode ?? '').trim(),
        quantity: Number(line.quantity ?? 0),
        unitPrice: Number(line.unitPrice ?? 0),
        warehouseCode: String(line.warehouseCode ?? '01').trim() || '01',
        discountPct: Number(line.discountPct ?? 0),
        vatPct: Number(line.vatPct ?? 0)
      }))
      .filter((line) => line.itemCode !== '' && line.quantity > 0);

    if (lines.length === 0) return null;

    const today = new Date().toISOString().slice(0, 10);
    return {
      cardCode,
      docDate: (d.docDate || d.postingDate || today).slice(0, 10),
      dueDate: (d.dueDate || today).slice(0, 10),
      comments: d.comments,
      lines
    };
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.getById(this.resource(), this.id())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.doc.set(res.data ?? null);
          this.selectedGenerationLines.set([]);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('Impossible de charger le détail.');
          this.loading.set(false);
        }
      });
  }

  private selectedLineNumsForGeneration(): number[] {
    const d = this.doc();
    if (!d) return [];

    const openLineNums = d.lines
      .map((line, index) => ({ line, index }))
      .filter(({ line }) => this.isOpenLineStatus(line))
      .map(({ line, index }) => this.getLineNum(line, index));

    const selected = this.selectedGenerationLines();
    if (selected.length === 0) return openLineNums;

    return openLineNums.filter((n) => selected.includes(n));
  }

  private getLineNum(line: any, index: number): number {
    const n = Number(line?.lineNum ?? line?.LineNum ?? line?.id ?? line?.Id ?? index);
    return Number.isFinite(n) ? n : index;
  }

  private hasClosedLine(doc: CommercialDocument): boolean {
    return (doc.lines ?? []).some((line) => !this.isOpenLineStatus(line));
  }

  private normalizeLineStatusToken(value: unknown): string {
    return String(value ?? '')
      .trim()
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[\s_-]/g, '');
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
