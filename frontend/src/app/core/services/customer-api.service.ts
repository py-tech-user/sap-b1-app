import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse, PagedResult,
  Customer, CreateCustomer, UpdateCustomer
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class CustomerApiService {
  private readonly http = inject(HttpClient);
  private readonly url  = `${environment.apiUrl}/customers`;

  getAll(
    page     = 1,
    pageSize = 20,
    search?:   string,
    isActive?: boolean
  ): Observable<ApiResponse<PagedResult<Customer>>> {
    let params = new HttpParams()
      .set('page',     page)
      .set('pageSize', pageSize);

    if (search   !== undefined && search !== '')
      params = params.set('search', search);
    if (isActive !== undefined)
      params = params.set('isActive', isActive);

    return this.http.get<ApiResponse<PagedResult<Customer>>>(
      this.url, { params });
  }

  getById(id: number): Observable<ApiResponse<Customer>> {
    return this.http.get<ApiResponse<Customer>>(`${this.url}/${id}`);
  }

  create(dto: CreateCustomer): Observable<ApiResponse<Customer>> {
    return this.http.post<ApiResponse<Customer>>(this.url, dto);
  }

  update(id: number, dto: UpdateCustomer): Observable<ApiResponse<Customer>> {
    return this.http.put<ApiResponse<Customer>>(`${this.url}/${id}`, dto);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.url}/${id}`);
  }

  syncToSap(id: number): Observable<ApiResponse<Customer>> {
    return this.http.post<ApiResponse<Customer>>(
      `${this.url}/${id}/sync-sap`, {});
  }
}
