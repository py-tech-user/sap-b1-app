import { Component, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TrackingApiService } from '../../../core/services/tracking-api.service';
import { GeolocationService } from '../../../core/services/geolocation.service';
import { switchMap } from 'rxjs';

@Component({
  selector: 'app-visit-checkin-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button class="btn-checkin" [disabled]="loading()" (click)="doCheckIn()">
      @if (loading()) {
        ⏳ Localisation...
      } @else {
        📍 Check-in
      }
    </button>
    @if (error()) {
      <small class="checkin-error">{{ error() }}</small>
    }
  `,
  styles: [`
    :host { display: inline-flex; align-items: center; gap: 0.5rem; }
    .btn-checkin {
      padding: 0.4rem 0.9rem; background: #27ae60; color: white; border: none;
      border-radius: 6px; cursor: pointer; font-size: 0.85rem; font-weight: 500;
    }
    .btn-checkin:hover:not(:disabled) { background: #219a52; }
    .btn-checkin:disabled { opacity: 0.6; cursor: not-allowed; }
    .checkin-error { color: #e74c3c; font-size: 0.78rem; }
  `]
})
export class VisitCheckinButtonComponent {
  private trackingApi = inject(TrackingApiService);
  private geo = inject(GeolocationService);

  /** ID de la visite (input signal) */
  visitId = input.required<number>();

  /** Événement émis après un check-in réussi */
  checkedIn = output<void>();

  loading = signal(false);
  error = signal('');

  doCheckIn(): void {
    this.loading.set(true);
    this.error.set('');

    this.geo.getCurrentPosition().pipe(
      switchMap(pos =>
        this.trackingApi.checkIn({
          visitId: this.visitId(),
          latitude: pos.latitude,
          longitude: pos.longitude
        })
      )
    ).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success !== false) {
          this.checkedIn.emit();
        } else {
          this.error.set(res.message ?? 'Erreur lors du check-in.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        const msg = err?.error?.message || err?.message || (typeof err === 'string' ? err : 'Erreur serveur lors du check-in.');
        this.error.set(msg);
      }
    });
  }
}
