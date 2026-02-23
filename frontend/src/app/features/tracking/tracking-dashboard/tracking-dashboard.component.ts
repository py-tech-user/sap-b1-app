import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TrackingApiService } from '../../../core/services/tracking-api.service';
import { GeolocationService } from '../../../core/services/geolocation.service';
import { UserTrackingStats } from '../../../core/models/models';

@Component({
  selector: 'app-tracking-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="tracking-dashboard">
      <div class="header">
        <h1>📡 Suivi terrain</h1>
        <div class="header-actions">
          <a routerLink="/tracking/map" class="btn-primary">🗺️ Carte en direct</a>
          <button class="btn-track" [class.active]="geo.isTracking()" (click)="toggleTracking()">
            {{ geo.isTracking() ? '⏹ Arrêter mon suivi' : '▶ Démarrer mon suivi' }}
          </button>
        </div>
      </div>

      @if (geo.error()) {
        <div class="alert alert-error">⚠️ {{ geo.error() }}</div>
      }

      @if (loading()) {
        <div class="loading">Chargement des statistiques...</div>
      } @else {
        <!-- GPS Status Card -->
        <div class="gps-status-card">
          <div class="gps-icon">{{ geo.isTracking() ? '🟢' : '🔴' }}</div>
          <div class="gps-info">
            <strong>GPS {{ geo.isTracking() ? 'Actif' : 'Inactif' }}</strong>
            @if (geo.currentPosition(); as pos) {
              <small>📍 {{ pos.latitude.toFixed(5) }}, {{ pos.longitude.toFixed(5) }} (±{{ pos.accuracy.toFixed(0) }}m)</small>
            } @else {
              <small>Position non disponible</small>
            }
          </div>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
          <div class="stat-card">
            <div class="stat-icon">👥</div>
            <div class="stat-value">{{ stats().length }}</div>
            <div class="stat-label">Commerciaux suivis</div>
          </div>
          <div class="stat-card">
            <div class="stat-icon">📋</div>
            <div class="stat-value">{{ totalVisits() }}</div>
            <div class="stat-label">Visites totales</div>
          </div>
          <div class="stat-card">
            <div class="stat-icon">✅</div>
            <div class="stat-value">{{ completedVisits() }}</div>
            <div class="stat-label">Visites terminées</div>
          </div>
          <div class="stat-card">
            <div class="stat-icon">📏</div>
            <div class="stat-value">{{ totalDistance().toFixed(1) }} km</div>
            <div class="stat-label">Distance totale</div>
          </div>
        </div>

        <!-- Users Stats Table -->
        <div class="table-container">
          <h3>📊 Statistiques par commercial</h3>
          <table>
            <thead>
              <tr>
                <th>Commercial</th>
                <th>Visites</th>
                <th>Terminées</th>
                <th>Distance (km)</th>
                <th>Durée moy. (min)</th>
                <th>Dernière activité</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (stat of stats(); track stat.userId) {
                <tr>
                  <td><strong>{{ stat.userName }}</strong></td>
                  <td>{{ stat.totalVisits }}</td>
                  <td>{{ stat.completedVisits }}</td>
                  <td>{{ stat.totalDistanceKm.toFixed(1) }}</td>
                  <td>{{ stat.avgVisitDurationMin.toFixed(0) }}</td>
                  <td>{{ stat.lastActivity ? (stat.lastActivity | date:'dd/MM HH:mm') : '—' }}</td>
                  <td>
                    <a [routerLink]="'/tracking/history/' + stat.userId" class="btn-sm btn-view">
                      📍 Historique
                    </a>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="empty-row">Aucune donnée de tracking disponible.</td></tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .tracking-dashboard { display: flex; flex-direction: column; gap: 1.25rem; max-width: 1200px; }
    .header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem; }
    .header h1 { font-size: 1.5rem; font-weight: 700; color: #1e2a3a; margin: 0; }
    .header-actions { display: flex; gap: 0.5rem; }

    .btn-primary {
      padding: 0.5rem 1rem; background: #667eea; color: white; border: none; border-radius: 6px;
      cursor: pointer; font-size: 0.9rem; font-weight: 500; text-decoration: none;
    }
    .btn-primary:hover { background: #5a6fd6; }

    .btn-track {
      padding: 0.5rem 1rem; border: none; border-radius: 6px; cursor: pointer;
      font-size: 0.9rem; font-weight: 500; background: #27ae60; color: white;
    }
    .btn-track:hover { background: #219a52; }
    .btn-track.active { background: #e74c3c; }
    .btn-track.active:hover { background: #c0392b; }

    .alert { padding: 0.75rem 1rem; border-radius: 8px; font-size: 0.9rem; }
    .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
    .loading { text-align: center; padding: 2rem; color: #999; }

    /* GPS Status */
    .gps-status-card {
      display: flex; align-items: center; gap: 1rem; background: white;
      padding: 1rem 1.25rem; border-radius: 12px; box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .gps-icon { font-size: 2rem; }
    .gps-info { display: flex; flex-direction: column; gap: 2px; }
    .gps-info strong { font-size: 1rem; color: #1e2a3a; }
    .gps-info small { font-size: 0.85rem; color: #666; }

    /* Stats Grid */
    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; }
    .stat-card {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06); text-align: center;
    }
    .stat-icon { font-size: 2rem; margin-bottom: 0.5rem; }
    .stat-value { font-size: 1.75rem; font-weight: 700; color: #1e2a3a; }
    .stat-label { font-size: 0.85rem; color: #888; margin-top: 0.25rem; }

    /* Table */
    .table-container {
      background: white; border-radius: 12px; padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
    }
    .table-container h3 { margin: 0 0 1rem; font-size: 1.1rem; color: #333; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.75rem 1rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f8f9fa; font-weight: 600; font-size: 0.85rem; color: #555; }
    td { font-size: 0.9rem; }
    .empty-row { text-align: center; color: #999; padding: 2rem; }
    .btn-sm { padding: 0.3rem 0.75rem; border: none; border-radius: 4px; cursor: pointer; font-size: 0.85rem; text-decoration: none; }
    .btn-view { background: #e3f2fd; color: #1565c0; }
    .btn-view:hover { background: #bbdefb; }
  `]
})
export class TrackingDashboardComponent implements OnInit {
  private trackingApi = inject(TrackingApiService);
  geo = inject(GeolocationService);

  stats = signal<UserTrackingStats[]>([]);
  loading = signal(true);

  totalVisits = signal(0);
  completedVisits = signal(0);
  totalDistance = signal(0);

  ngOnInit(): void {
    this.loadStats();
  }

  toggleTracking(): void {
    if (this.geo.isTracking()) {
      this.geo.stopAutoTracking();
    } else {
      this.geo.startAutoTracking();
    }
  }

  private loadStats(): void {
    this.loading.set(true);
    this.trackingApi.getStats().subscribe({
      next: (res) => {
        const data = res.data ?? [];
        this.stats.set(data);
        this.totalVisits.set(data.reduce((sum, s) => sum + s.totalVisits, 0));
        this.completedVisits.set(data.reduce((sum, s) => sum + s.completedVisits, 0));
        this.totalDistance.set(data.reduce((sum, s) => sum + s.totalDistanceKm, 0));
        this.loading.set(false);
      },
      error: () => {
        this.stats.set([]);
        this.loading.set(false);
      }
    });
  }
}
