import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, ServiceTicket, CreateServiceTicket } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ServiceTicketApiService {
  private readonly api = `${environment.apiUrl}/service-tickets`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, customerId?: number): Observable<ApiResponse<PagedResult<ServiceTicket>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (customerId) p = p.set('customerId', customerId);
    return this.http.get<ApiResponse<PagedResult<ServiceTicket>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<ServiceTicket>> {
    return this.http.get<ApiResponse<ServiceTicket>>(`${this.api}/${id}`);
  }
  create(data: CreateServiceTicket): Observable<ApiResponse<ServiceTicket>> {
    return this.http.post<ApiResponse<ServiceTicket>>(this.api, data);
  }
  update(id: number, data: any): Observable<ApiResponse<ServiceTicket>> {
    return this.http.put<ApiResponse<ServiceTicket>>(`${this.api}/${id}`, data);
  }
  addPart(id: number, part: { productId: number; quantity: number; unitPrice?: number }): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/parts`, part);
  }
  removePart(id: number, partId: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}/parts/${partId}`);
  }
  schedule(id: number, scheduledDate: string, technicianId?: number): Observable<ApiResponse<any>> {
    let p = new HttpParams().set('scheduledDate', scheduledDate);
    if (technicianId) p = p.set('technicianId', technicianId);
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/schedule`, {}, { params: p });
  }
  complete(id: number, resolution: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/complete`, JSON.stringify(resolution), {
      headers: { 'Content-Type': 'application/json' }
    });
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
