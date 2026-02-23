import {
  Component, OnInit, OnDestroy, inject, signal,
  ElementRef, viewChild, afterNextRender, ChangeDetectorRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription, interval, switchMap, of } from 'rxjs';
import { TrackingApiService } from '../../../core/services/tracking-api.service';
import { GeolocationService } from '../../../core/services/geolocation.service';
import { UserLivePosition } from '../../../core/models/models';
import * as L from 'leaflet';

@Component({
  selector: 'app-live-map',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="live-map-page">
      <div class="header">
        <h1>🗺️ Carte en temps réel</h1>
        <div class="header-actions">
          <button class="btn-track" [class.active]="geo.isTracking()" (click)="toggleTracking()">
            {{ geo.isTracking() ? '⏹ Arrêter le suivi' : '▶ Démarrer le suivi' }}
          </button>
          <button class="btn-refresh" (click)="refreshPositions()">🔄 Actualiser</button>
        </div>
      </div>

      @if (geo.error()) {
        <div class="alert alert-error">⚠️ {{ geo.error() }}</div>
      }

      <div class="map-container">
        <div #mapEl class="map"></div>
      </div>

      <!-- Légende / liste commerciaux -->
      <div class="user-list">
        <h3>👥 Commerciaux en ligne ({{ positions().length }})</h3>
        @for (pos of positions(); track pos.userId) {
          <div class="user-card" (click)="centerOnUser(pos)">
            <div class="user-avatar">📍</div>
            <div class="user-info">
              <strong>{{ pos.userName }}</strong>
              <small>
                @if (pos.currentCustomerName) {
                  Chez {{ pos.currentCustomerName }}
                } @else {
                  En déplacement
                }
              </small>
              <small class="text-muted">Dernière MAJ : {{ pos.lastUpdate | date:'HH:mm:ss' }}</small>
            </div>
          </div>
        } @empty {
          <p class="no-users">Aucun commercial en ligne.</p>
        }
      </div>
    </div>
  `,
  styles: [`
    .live-map-page { display: flex; flex-direction: column; gap: 1rem; }
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0; }
    .header-actions { display: flex; gap: 0.5rem; }

    .btn-track {
      padding: 0.5rem 1rem; border: none; border-radius: 6px; cursor: pointer;
      font-size: 0.9rem; font-weight: 500; background: #667eea; color: white;
      transition: background 0.2s;
    }
    .btn-track:hover { background: #5a6fd6; }
    .btn-track.active { background: #e74c3c; }
    .btn-track.active:hover { background: #c0392b; }
    .btn-refresh {
      padding: 0.5rem 1rem; border: 1px solid #ddd; border-radius: 6px; cursor: pointer;
      font-size: 0.9rem; background: white;
    }
    .btn-refresh:hover { background: #f5f5f5; }

    .alert { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; }
    .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }

    .map-container {
      border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1);
      border: 1px solid #e0e0e0;
    }
    .map { height: 500px; width: 100%; }

    .user-list {
      background: white; border-radius: 12px; padding: 1rem 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .user-list h3 { margin: 0 0 0.75rem; font-size: 1rem; color: #333; }

    .user-card {
      display: flex; align-items: center; gap: 0.75rem; padding: 0.75rem;
      border-radius: 8px; cursor: pointer; transition: background 0.2s;
    }
    .user-card:hover { background: #f0f4ff; }
    .user-avatar { font-size: 1.5rem; }
    .user-info { display: flex; flex-direction: column; gap: 2px; }
    .user-info strong { font-size: 0.95rem; color: #1e2a3a; }
    .user-info small { font-size: 0.8rem; color: #666; }
    .text-muted { color: #999 !important; }
    .no-users { color: #999; font-size: 0.9rem; padding: 0.5rem; }
  `]
})
export class LiveMapComponent implements OnInit, OnDestroy {
  private trackingApi = inject(TrackingApiService);
  geo = inject(GeolocationService);
  private cdr = inject(ChangeDetectorRef);

  mapEl = viewChild<ElementRef<HTMLDivElement>>('mapEl');

  positions = signal<UserLivePosition[]>([]);
  private map!: L.Map;
  private markers = new Map<number, L.Marker>();
  private refreshSub: Subscription | null = null;
  private mapReady = false;

  constructor() {
    // Leaflet needs DOM — use afterNextRender for SSR safety
    afterNextRender(() => this.initMap());
  }

  ngOnInit(): void {
    this.refreshPositions();
    // Rafraîchir les positions toutes les 15 secondes
    this.refreshSub = interval(15_000).pipe(
      switchMap(() => this.trackingApi.getLivePositions())
    ).subscribe({
      next: (res) => {
        if (res.data) {
          this.positions.set(res.data);
          this.updateMarkers(res.data);
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
    if (this.map) this.map.remove();
  }

  toggleTracking(): void {
    if (this.geo.isTracking()) {
      this.geo.stopAutoTracking();
    } else {
      this.geo.startAutoTracking();
    }
  }

  refreshPositions(): void {
    this.trackingApi.getLivePositions().subscribe({
      next: (res) => {
        if (res.data) {
          this.positions.set(res.data);
          if (this.mapReady) this.updateMarkers(res.data);
        }
      }
    });
  }

  centerOnUser(pos: UserLivePosition): void {
    if (this.map) {
      this.map.setView([pos.latitude, pos.longitude], 15);
      const marker = this.markers.get(pos.userId);
      if (marker) marker.openPopup();
    }
  }

  private initMap(): void {
    const el = this.mapEl()?.nativeElement;
    if (!el) return;

    // Fix Leaflet default icon path issue
    delete (L.Icon.Default.prototype as any)._getIconUrl;
    L.Icon.Default.mergeOptions({
      iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon-2x.png',
      iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png',
      shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png'
    });

    this.map = L.map(el).setView([33.5933, -7.6035], 12); // Casablanca

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors'
    }).addTo(this.map);

    this.mapReady = true;

    // Placer les marqueurs déjà chargés
    if (this.positions().length > 0) {
      this.updateMarkers(this.positions());
    }
  }

  private updateMarkers(positions: UserLivePosition[]): void {
    if (!this.mapReady) return;

    // Supprimer les marqueurs des utilisateurs absents
    const activeIds = new Set(positions.map(p => p.userId));
    for (const [id, marker] of this.markers) {
      if (!activeIds.has(id)) {
        marker.remove();
        this.markers.delete(id);
      }
    }

    // Ajouter / mettre à jour les marqueurs
    for (const pos of positions) {
      const existing = this.markers.get(pos.userId);
      const popupContent = `<strong>${pos.userName}</strong><br/>` +
        (pos.currentCustomerName ? `Chez ${pos.currentCustomerName}` : 'En déplacement');

      if (existing) {
        existing.setLatLng([pos.latitude, pos.longitude]);
        existing.setPopupContent(popupContent);
      } else {
        const marker = L.marker([pos.latitude, pos.longitude])
          .bindPopup(popupContent)
          .addTo(this.map);
        this.markers.set(pos.userId, marker);
      }
    }

    // Centrer la carte pour tout voir
    if (positions.length > 0) {
      const bounds = L.latLngBounds(positions.map(p => [p.latitude, p.longitude] as L.LatLngTuple));
      this.map.fitBounds(bounds, { padding: [50, 50], maxZoom: 15 });
    }
  }
}
