import { Injectable, signal, OnDestroy, inject } from '@angular/core';
import { Observable, Subject, interval, Subscription, switchMap, of } from 'rxjs';
import { TrackingApiService } from './tracking-api.service';

export interface GeoPosition {
  latitude: number;
  longitude: number;
  accuracy: number;
}

/** Position par defaut - Casablanca */
const DEFAULT_POSITION: GeoPosition = {
  latitude: 33.593283,
  longitude: -7.603535,
  accuracy: 1000
};

@Injectable({ providedIn: 'root' })
export class GeolocationService implements OnDestroy {
  private trackingApi = inject(TrackingApiService);

  /** Position courante (signal pour zoneless) */
  currentPosition = signal<GeoPosition | null>(null);

  /** Erreur eventuelle */
  error = signal<string | null>(null);

  /** Tracking auto actif ? */
  isTracking = signal(false);

  private watchId: number | null = null;
  private autoSendSub: Subscription | null = null;
  private destroy$ = new Subject<void>();

  /** Verifier si la geolocalisation est disponible */
  get isSupported(): boolean {
    return typeof navigator !== 'undefined' && 'geolocation' in navigator;
  }

  /**
   * Obtenir la position actuelle (one-shot).
   * Strategie : GPS haute precision a†’ GPS basse precision a†’ position en cache a†’ position par defaut.
   * Ne produit JAMAIS d'erreur - retourne toujours une position.
   */
  getCurrentPosition(): Observable<GeoPosition> {
    return new Observable(observer => {
      const emit = (geo: GeoPosition) => {
        this.currentPosition.set(geo);
        this.error.set(null);
        observer.next(geo);
        observer.complete();
      };

      const useFallback = () => {
        // Utiliser la derniere position connue ou la position par defaut
        const cached = this.currentPosition();
        if (cached) {
          console.warn('[Geo] GPS echoue, utilisation de la derniere position connue.');
          emit(cached);
        } else {
          console.warn('[Geo] GPS echoue, utilisation de la position par defaut (Casablanca).');
          emit(DEFAULT_POSITION);
        }
      };

      if (!this.isSupported) {
        useFallback();
        return;
      }

      const onSuccess = (pos: GeolocationPosition) => {
        emit({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
          accuracy: pos.coords.accuracy
        });
      };

      // 1ere tentative : haute precision, 15s, cache 60s
      navigator.geolocation.getCurrentPosition(
        onSuccess,
        () => {
          // 2eme tentative : basse precision, 20s, cache 5min
          navigator.geolocation.getCurrentPosition(
            onSuccess,
            () => useFallback(),
            { enableHighAccuracy: false, timeout: 20_000, maximumAge: 300_000 }
          );
        },
        { enableHighAccuracy: true, timeout: 15_000, maximumAge: 60_000 }
      );
    });
  }

  /** Demarrer le tracking GPS automatique (envoi toutes les 30s) */
  startAutoTracking(intervalMs = 30_000): void {
    if (this.isTracking()) return;
    this.isTracking.set(true);

    // Watch position en continu (basse precision pour stabilite, cache 10s)
    if (this.isSupported) {
      this.watchId = navigator.geolocation.watchPosition(
        (pos) => {
          this.currentPosition.set({
            latitude: pos.coords.latitude,
            longitude: pos.coords.longitude,
            accuracy: pos.coords.accuracy
          });
          this.error.set(null);
        },
        (err) => this.error.set(this.formatError(err)),
        { enableHighAccuracy: false, timeout: 30_000, maximumAge: 10_000 }
      );
    }

    // Envoi periodique au serveur
    this.autoSendSub = interval(intervalMs).pipe(
      switchMap(() => {
        const pos = this.currentPosition();
        if (!pos) return of(null);
        return this.trackingApi.sendLocation({
          latitude: pos.latitude,
          longitude: pos.longitude,
          accuracy: pos.accuracy,
          eventType: 'auto'
        });
      })
    ).subscribe();
  }

  /** Arreter le tracking GPS automatique */
  stopAutoTracking(): void {
    if (this.watchId !== null) {
      navigator.geolocation.clearWatch(this.watchId);
      this.watchId = null;
    }
    this.autoSendSub?.unsubscribe();
    this.autoSendSub = null;
    this.isTracking.set(false);
  }

  ngOnDestroy(): void {
    this.stopAutoTracking();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private formatError(err: GeolocationPositionError): string {
    switch (err.code) {
      case err.PERMISSION_DENIED:
        return 'Permission de geolocalisation refusee. Veuillez l\'activer dans les parametres du navigateur.';
      case err.POSITION_UNAVAILABLE:
        return 'Position indisponible. Verifiez votre GPS.';
      case err.TIMEOUT:
        return 'Delai d\'attente de la position GPS depasse.';
      default:
        return 'Erreur de geolocalisation inconnue.';
    }
  }
}


