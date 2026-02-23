import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Order {
  id: number;
  docNum: string;
  customerId: number;
  customerName: string;
  docDate: string;
  deliveryDate?: string;
  status: string;
  docTotal: number;
  vatTotal: number;
  currency: string;
  comments?: string;
  lines: OrderLine[];
}

export interface OrderLine {
  id: number;
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  vatPct: number;
  lineTotal: number;
}

export interface CreateOrderDto {
  customerId: number;
  docDate: string;
  deliveryDate?: string;
  comments?: string;
  lines: { productId: number; quantity: number; unitPrice: number }[];
}

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly apiUrl = `${environment.apiUrl}/orders`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10): Observable<{ items: Order[]; totalCount: number }> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<{ items: Order[]; totalCount: number }>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Order> {
    return this.http.get<Order>(`${this.apiUrl}/${id}`);
  }

  create(order: CreateOrderDto): Observable<Order> {
    return this.http.post<Order>(this.apiUrl, order);
  }

  updateStatus(id: number, status: string): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/status`, { status });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  syncToSap(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/sync-sap`, {});
  }
}
