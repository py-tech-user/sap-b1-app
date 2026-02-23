import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, Supplier, CreateSupplier } from '../models/models';

@Injectable({ providedIn: 'root' })
export class SupplierApiService {
  private readonly api = `${environment.apiUrl}/suppliers`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, search?: string): Observable<ApiResponse<PagedResult<Supplier>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) p = p.set('search', search);
    return this.http.get<ApiResponse<PagedResult<Supplier>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<Supplier>> {
    return this.http.get<ApiResponse<Supplier>>(`${this.api}/${id}`);
  }
  create(data: CreateSupplier): Observable<ApiResponse<Supplier>> {
    return this.http.post<ApiResponse<Supplier>>(this.api, data);
  }
  update(id: number, data: any): Observable<ApiResponse<Supplier>> {
    return this.http.put<ApiResponse<Supplier>>(`${this.api}/${id}`, data);
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
