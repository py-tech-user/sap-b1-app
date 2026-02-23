import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, DeliveryNote, CreateDeliveryNote } from '../models/models';

@Injectable({ providedIn: 'root' })
export class DeliveryNoteApiService {
  private readonly api = `${environment.apiUrl}/delivery-notes`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, customerId?: number): Observable<ApiResponse<PagedResult<DeliveryNote>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (customerId) p = p.set('customerId', customerId);
    return this.http.get<ApiResponse<PagedResult<DeliveryNote>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<DeliveryNote>> {
    return this.http.get<ApiResponse<DeliveryNote>>(`${this.api}/${id}`);
  }
  create(data: CreateDeliveryNote): Observable<ApiResponse<DeliveryNote>> {
    return this.http.post<ApiResponse<DeliveryNote>>(this.api, data);
  }
  update(id: number, data: any): Observable<ApiResponse<DeliveryNote>> {
    return this.http.put<ApiResponse<DeliveryNote>>(`${this.api}/${id}`, data);
  }
  confirm(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/confirm`, {});
  }
  ship(id: number, trackingNumber?: string): Observable<ApiResponse<any>> {
    let p = new HttpParams();
    if (trackingNumber) p = p.set('trackingNumber', trackingNumber);
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/ship`, {}, { params: p });
  }
  deliver(id: number, receivedBy?: string): Observable<ApiResponse<any>> {
    let p = new HttpParams();
    if (receivedBy) p = p.set('receivedBy', receivedBy);
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/deliver`, {}, { params: p });
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
