import {
  Component, OnInit, OnDestroy, inject, signal,
  ElementRef, viewChild, afterNextRender, ChangeDetectorRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TrackingApiService } from '../../../core/services/tracking-api.service';
import { TrackPoint, UserTrackingStats } from '../../../core/models/models';
import * as L from 'leaflet';

@Component({
  selector: 'app-track-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="track-history-page">
      <div class="header">
        <div>
          <a routerLink="/tracking" class="back-link">← Retour au suivi</a>
          <h1>📍 Historique de {{ userName() }}</h1>
        </div>
      </div>

      <!-- Filtres dates -->
      <div class="filters">
        <div class="filter-group">
          <label>Date début</label>
          <input type="date" [(ngModel)]="dateFrom" />
        </div>
        <div class="filter-group">
          <label>Date fin</label>
          <input type="date" [(ngModel)]="dateTo" />
        </div>
        <button class="btn-filter" (click)="loadHistory()">🔍 Filtrer</button>
      </div>

      <!-- Stats utilisateur -->
      @if (userStats(); as s) {
        <div class="stats-row">
          <div class="mini-stat">📋 {{ s.totalVisits }} visites</div>
          <div class="mini-stat">✅ {{ s.completedVisits }} terminées</div>
          <div class="mini-stat">📏 {{ s.totalDistanceKm.toFixed(1) }} km</div>
          <div class="mini-stat">⏱ {{ s.avgVisitDurationMin.toFixed(0) }} min/visite</div>
        </div>
      }

      @if (loading()) {
        <div class="loading">Chargement de l'historique...</div>
      } @else {
        <!-- Carte -->
        <div class="map-container">
          <div #mapEl class="map"></div>
        </div>

        <!-- Tableau des points -->
        <div class="table-container">
          <h3>📋 Points de passage ({{ points().length }})</h3>
          <table>
            <thead>
              <tr>
                <th>#</th>
                <th>Heure</th>
                <th>Type</th>
                <th>Latitude</th>
                <th>Longitude</th>
              </tr>
            </thead>
            <tbody>
              @for (pt of points(); track $index; let i = $index) {
                <tr [class.highlight]="pt.eventType === 'check-in' || pt.eventType === 'check-out'">
                  <td>{{ i + 1 }}</td>
                  <td>{{ pt.timestamp | date:'dd/MM HH:mm:ss' }}</td>
                  <td>
                    <span [class]="'badge badge-' + pt.eventType">
                      @switch (pt.eventType) {
                        @case ('check-in')  { 📍 Check-in }
                        @case ('check-out') { 🏁 Check-out }
                        @default            { 🔵 Auto }
                      }
                    </span>
                  </td>
                  <td>{{ pt.latitude.toFixed(5) }}</td>
                  <td>{{ pt.longitude.toFixed(5) }}</td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="empty-row">Aucun point pour cette période.</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .track-history-page { display: flex; flex-direction: column; gap: 1rem; max-width: 1200px; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0.25rem 0 0; }
    .back-link { color: #667eea; text-decoration: none; font-size: 0.9rem; }
    .back-link:hover { text-decoration: underline; }

    /* Filters */
    .filters {
      display: flex; gap: 1rem; align-items: flex-end; background: white;
      padding: 1rem; border-radius: 8px; box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .filter-group { display: flex; flex-direction: column; gap: 4px; }
    .filter-group label { font-size: 0.8rem; font-weight: 600; color: #555; }
    .filter-group input {
      padding: 0.5rem 0.75rem; border: 1px solid #ddd; border-radius: 6px; font-size: 0.9rem;
    }
    .btn-filter {
      padding: 0.5rem 1rem; background: #667eea; color: white;
      border: none; border-radius: 6px; cursor: pointer; font-size: 0.9rem;
    }
    .btn-filter:hover { background: #5a6fd6; }

    /* Stats */
    .stats-row { display: flex; gap: 1rem; flex-wrap: wrap; }
    .mini-stat {
      background: white; padding: 0.75rem 1rem; border-radius: 8px;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06); font-size: 0.9rem; font-weight: 500;
    }

    .loading { text-align: center; padding: 2rem; color: #999; }

    /* Map */
    .map-container {
      border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1);
      border: 1px solid #e0e0e0;
    }
    .map { height: 450px; width: 100%; }

    /* Table */
    .table-container {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .table-container h3 { margin: 0 0 0.75rem; font-size: 1.1rem; color: #333; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.65rem 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f8f9fa; font-weight: 600; font-size: 0.85rem; color: #555; }
    td { font-size: 0.9rem; }
    .empty-row { text-align: center; color: #999; padding: 2rem; }
    tr.highlight { background: #fffde7; }

    .badge { padding: 0.2rem 0.5rem; border-radius: 12px; font-size: 0.78rem; font-weight: 500; }
    .badge-check-in  { background: #e8f5e9; color: #2e7d32; }
    .badge-check-out { background: #fff3e0; color: #e65100; }
    .badge-auto      { background: #e3f2fd; color: #1565c0; }
  `]
})
export class TrackHistoryComponent implements OnInit, OnDestroy {
  private trackingApi = inject(TrackingApiService);
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);

  mapEl = viewChild<ElementRef<HTMLDivElement>>('mapEl');

  points = signal<TrackPoint[]>([]);
  userStats = signal<UserTrackingStats | null>(null);
  userName = signal('...');
  loading = signal(true);

  dateFrom = '';
  dateTo = '';

  private userId = 0;
  private map!: L.Map;
  private polyline: L.Polyline | null = null;
  private markersLayer: L.LayerGroup | null = null;
  private mapReady = false;

  constructor() {
    afterNextRender(() => this.initMap());
  }

  ngOnInit(): void {
    this.userId = Number(this.route.snapshot.paramMap.get('userId') ?? 0);

    // Pré-remplir les dates (7 derniers jours)
    const today = new Date();
    const weekAgo = new Date(today);
    weekAgo.setDate(today.getDate() - 7);
    this.dateTo = today.toISOString().substring(0, 10);
    this.dateFrom = weekAgo.toISOString().substring(0, 10);

    // Charger stats utilisateur
    this.trackingApi.getUserStats(this.userId).subscribe({
      next: (res) => {
        if (res.data) {
          this.userStats.set(res.data);
          this.userName.set(res.data.userName);
        }
      }
    });

    this.loadHistory();
  }

  ngOnDestroy(): void {
    if (this.map) this.map.remove();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.trackingApi.getHistory(
      this.userId,
      this.dateFrom || undefined,
      this.dateTo || undefined
    ).subscribe({
      next: (res) => {
        this.points.set(res.data ?? []);
        this.loading.set(false);
        if (this.mapReady) this.drawRoute(res.data ?? []);
      },
      error: () => {
        this.points.set([]);
        this.loading.set(false);
      }
    });
  }

  private initMap(): void {
    const el = this.mapEl()?.nativeElement;
    if (!el) return;

    delete (L.Icon.Default.prototype as any)._getIconUrl;
    L.Icon.Default.mergeOptions({
      iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon-2x.png',
      iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-icon.png',
      shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png'
    });

    this.map = L.map(el).setView([33.5933, -7.6035], 12);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors'
    }).addTo(this.map);

    this.markersLayer = L.layerGroup().addTo(this.map);
    this.mapReady = true;

    if (this.points().length > 0) {
      this.drawRoute(this.points());
    }
  }

  private drawRoute(points: TrackPoint[]): void {
    if (!this.mapReady || points.length === 0) return;

    // Nettoyer
    if (this.polyline) { this.polyline.remove(); this.polyline = null; }
    this.markersLayer?.clearLayers();

    const latlngs: L.LatLngTuple[] = points.map(p => [p.latitude, p.longitude]);

    // Dessiner la polyline
    this.polyline = L.polyline(latlngs, {
      color: '#667eea', weight: 3, opacity: 0.8
    }).addTo(this.map);

    // Marqueurs spéciaux pour check-in / check-out
    const checkinIcon = L.divIcon({
      className: 'custom-icon',
      html: '<div style="background:#27ae60;color:white;border-radius:50%;width:28px;height:28px;display:flex;align-items:center;justify-content:center;font-size:14px;box-shadow:0 2px 4px rgba(0,0,0,0.3);">📍</div>',
      iconSize: [28, 28],
      iconAnchor: [14, 14]
    });

    const checkoutIcon = L.divIcon({
      className: 'custom-icon',
      html: '<div style="background:#e67e22;color:white;border-radius:50%;width:28px;height:28px;display:flex;align-items:center;justify-content:center;font-size:14px;box-shadow:0 2px 4px rgba(0,0,0,0.3);">🏁</div>',
      iconSize: [28, 28],
      iconAnchor: [14, 14]
    });

    for (const pt of points) {
      if (pt.eventType === 'check-in') {
        L.marker([pt.latitude, pt.longitude], { icon: checkinIcon })
          .bindPopup(`<strong>Check-in</strong><br/>${new Date(pt.timestamp).toLocaleString()}`)
          .addTo(this.markersLayer!);
      } else if (pt.eventType === 'check-out') {
        L.marker([pt.latitude, pt.longitude], { icon: checkoutIcon })
          .bindPopup(`<strong>Check-out</strong><br/>${new Date(pt.timestamp).toLocaleString()}`)
          .addTo(this.markersLayer!);
      }
    }

    // Ajuster la vue
    const bounds = L.latLngBounds(latlngs);
    this.map.fitBounds(bounds, { padding: [40, 40], maxZoom: 16 });
  }
}
