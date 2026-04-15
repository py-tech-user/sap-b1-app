import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  LocationTrack,
  CreateLocationTrack,
  UserLivePosition,
  CheckInRequest,
  CheckOutRequest,
  UserTrackingStats,
  TrackPoint
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class TrackingApiService {
  private readonly api = `${environment.apiUrl}/tracking`;

  constructor(private http: HttpClient) {}

  /** POST /tracking/position - envoyer la position GPS */
  sendLocation(data: CreateLocationTrack): Observable<ApiResponse<LocationTrack>> {
    // Le backend utilise /position et non /location
    return this.http.post<ApiResponse<LocationTrack>>(`${this.api}/position`, data);
  }

  /** GET /tracking/live - positions en temps reel de tous les commerciaux */
  getLivePositions(): Observable<ApiResponse<UserLivePosition[]>> {
    return this.http.get<ApiResponse<UserLivePosition[]>>(`${this.api}/live`);
  }

  /** POST /tracking/check-in - check-in visite avec coordonnees GPS */
  checkIn(data: CheckInRequest): Observable<ApiResponse<LocationTrack>> {
    return this.http.post<ApiResponse<LocationTrack>>(`${this.api}/check-in`, data);
  }

  /** POST /tracking/check-out - check-out visite avec coordonnees GPS */
  checkOut(data: CheckOutRequest): Observable<ApiResponse<LocationTrack>> {
    return this.http.post<ApiResponse<LocationTrack>>(`${this.api}/check-out`, data);
  }

  /** GET /tracking/history/:userId/:date - historique des positions d un utilisateur */
  getHistory(userId: number, from?: string, to?: string): Observable<ApiResponse<TrackPoint[]>> {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http.get<ApiResponse<TrackPoint[]>>(`${this.api}/history/${userId}`, { params });
  }

  /** GET /tracking/stats - statistiques de tracking de tous les utilisateurs */
  getStats(): Observable<ApiResponse<UserTrackingStats[]>> {
    return this.http.get<ApiResponse<UserTrackingStats[]>>(`${this.api}/stats`);
  }

  /** GET /tracking/stats/:userId - statistiques de tracking d un utilisateur */
  getUserStats(userId: number): Observable<ApiResponse<UserTrackingStats>> {
    return this.http.get<ApiResponse<UserTrackingStats>>(`${this.api}/stats/${userId}`);
  }
}
