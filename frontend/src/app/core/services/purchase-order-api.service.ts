import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, PurchaseOrder, CreatePurchaseOrder } from '../models/models';

@Injectable({ providedIn: 'root' })
export class PurchaseOrderApiService {
  private readonly api = `${environment.apiUrl}/purchase-orders`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, supplierId?: number): Observable<ApiResponse<PagedResult<PurchaseOrder>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (supplierId) p = p.set('supplierId', supplierId);
    return this.http.get<ApiResponse<PagedResult<PurchaseOrder>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<PurchaseOrder>> {
    return this.http.get<ApiResponse<PurchaseOrder>>(`${this.api}/${id}`);
  }
  create(data: CreatePurchaseOrder): Observable<ApiResponse<PurchaseOrder>> {
    return this.http.post<ApiResponse<PurchaseOrder>>(this.api, data);
  }
  update(id: number, data: any): Observable<ApiResponse<PurchaseOrder>> {
    return this.http.put<ApiResponse<PurchaseOrder>>(`${this.api}/${id}`, data);
  }
  send(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/send`, {});
  }
  confirm(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/confirm`, {});
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
