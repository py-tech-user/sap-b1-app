import { Component, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TrackingApiService } from '../../../core/services/tracking-api.service';
import { GeolocationService } from '../../../core/services/geolocation.service';
import { switchMap } from 'rxjs';

@Component({
  selector: 'app-visit-checkout-button',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (!showNotes()) {
      <button class="btn-checkout" [disabled]="loading()" (click)="showNotes.set(true)">
        🏁 Check-out
      </button>
    } @else {
      <div class="checkout-form">
        <input type="text" [(ngModel)]="notes" placeholder="Notes de fin de visite..." class="notes-input" />
        <button class="btn-checkout-confirm" [disabled]="loading()" (click)="doCheckOut()">
          @if (loading()) {
            ⏳ Envoi...
          } @else {
            ✅ Confirmer
          }
        </button>
        <button class="btn-cancel" (click)="showNotes.set(false)">✕</button>
      </div>
    }
    @if (error()) {
      <small class="checkout-error">{{ error() }}</small>
    }
  `,
  styles: [`
    :host { display: inline-flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .btn-checkout {
      padding: 0.4rem 0.9rem; background: #e67e22; color: white; border: none;
      border-radius: 6px; cursor: pointer; font-size: 0.85rem; font-weight: 500;
    }
    .btn-checkout:hover:not(:disabled) { background: #d35400; }
    .btn-checkout:disabled { opacity: 0.6; cursor: not-allowed; }

    .checkout-form { display: flex; gap: 0.4rem; align-items: center; }
    .notes-input {
      padding: 0.35rem 0.6rem; border: 1px solid #ddd; border-radius: 4px;
      font-size: 0.85rem; width: 200px;
    }
    .btn-checkout-confirm {
      padding: 0.35rem 0.7rem; background: #27ae60; color: white; border: none;
      border-radius: 4px; cursor: pointer; font-size: 0.85rem;
    }
    .btn-checkout-confirm:hover:not(:disabled) { background: #219a52; }
    .btn-cancel {
      padding: 0.35rem 0.5rem; background: #f0f0f0; border: none;
      border-radius: 4px; cursor: pointer; font-size: 0.85rem;
    }
    .btn-cancel:hover { background: #e0e0e0; }
    .checkout-error { color: #e74c3c; font-size: 0.78rem; width: 100%; }
  `]
})
export class VisitCheckoutButtonComponent {
  private trackingApi = inject(TrackingApiService);
  private geo = inject(GeolocationService);

  /** ID de la visite (input signal) */
  visitId = input.required<number>();

  /** Événement émis après un check-out réussi */
  checkedOut = output<void>();

  loading = signal(false);
  error = signal('');
  showNotes = signal(false);
  notes = '';

  doCheckOut(): void {
    this.loading.set(true);
    this.error.set('');

    this.geo.getCurrentPosition().pipe(
      switchMap(pos =>
        this.trackingApi.checkOut({
          visitId: this.visitId(),
          latitude: pos.latitude,
          longitude: pos.longitude,
          notes: this.notes || undefined
        })
      )
    ).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success !== false) {
          this.showNotes.set(false);
          this.notes = '';
          this.checkedOut.emit();
        } else {
          this.error.set(res.message ?? 'Erreur lors du check-out.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message || err?.message || (typeof err === 'string' ? err : 'Erreur serveur lors du check-out.');
        this.error.set(msg);
      }
    });
  }
}
