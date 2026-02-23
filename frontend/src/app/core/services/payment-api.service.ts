import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Payment, CreatePayment, UpdatePayment, PagedResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class PaymentApiService {
  private readonly apiUrl = `${environment.apiUrl}/payments`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, customerId?: number, orderId?: number): Observable<PagedResult<Payment>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (customerId) params = params.set('customerId', customerId.toString());
    if (orderId) params = params.set('orderId', orderId.toString());
    return this.http.get<PagedResult<Payment>>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Payment> {
    return this.http.get<Payment>(`${this.apiUrl}/${id}`);
  }

  create(payment: CreatePayment): Observable<Payment> {
    return this.http.post<Payment>(this.apiUrl, payment);
  }

  update(id: number, payment: UpdatePayment): Observable<Payment> {
    return this.http.put<Payment>(`${this.apiUrl}/${id}`, payment);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  syncToSap(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/sync-sap`, {});
  }
}
