import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, CreditNote, CreateCreditNote } from '../models/models';

@Injectable({ providedIn: 'root' })
export class CreditNoteApiService {
  private readonly api = `${environment.apiUrl}/credit-notes`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, customerId?: number): Observable<ApiResponse<PagedResult<CreditNote>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (customerId) p = p.set('customerId', customerId);
    return this.http.get<ApiResponse<PagedResult<CreditNote>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<CreditNote>> {
    return this.http.get<ApiResponse<CreditNote>>(`${this.api}/${id}`);
  }
  create(data: CreateCreditNote): Observable<ApiResponse<CreditNote>> {
    return this.http.post<ApiResponse<CreditNote>>(this.api, data);
  }
  createFromReturn(returnId: number): Observable<ApiResponse<CreditNote>> {
    return this.http.post<ApiResponse<CreditNote>>(`${this.api}/from-return/${returnId}`, {});
  }
  update(id: number, data: any): Observable<ApiResponse<CreditNote>> {
    return this.http.put<ApiResponse<CreditNote>>(`${this.api}/${id}`, data);
  }
  confirm(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/confirm`, {});
  }
  apply(id: number, invoiceId: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/apply`, {}, { params: new HttpParams().set('invoiceId', invoiceId) });
  }
  refund(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/refund`, {});
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
