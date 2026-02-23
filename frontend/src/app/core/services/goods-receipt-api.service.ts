import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, GoodsReceipt, CreateGoodsReceipt } from '../models/models';

@Injectable({ providedIn: 'root' })
export class GoodsReceiptApiService {
  private readonly api = `${environment.apiUrl}/goods-receipts`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, supplierId?: number): Observable<ApiResponse<PagedResult<GoodsReceipt>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (supplierId) p = p.set('supplierId', supplierId);
    return this.http.get<ApiResponse<PagedResult<GoodsReceipt>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<GoodsReceipt>> {
    return this.http.get<ApiResponse<GoodsReceipt>>(`${this.api}/${id}`);
  }
  create(data: CreateGoodsReceipt): Observable<ApiResponse<GoodsReceipt>> {
    return this.http.post<ApiResponse<GoodsReceipt>>(this.api, data);
  }
  createFromPO(poId: number, lines: any[]): Observable<ApiResponse<GoodsReceipt>> {
    return this.http.post<ApiResponse<GoodsReceipt>>(`${this.api}/from-purchase-order/${poId}`, lines);
  }
  confirm(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/confirm`, {});
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
