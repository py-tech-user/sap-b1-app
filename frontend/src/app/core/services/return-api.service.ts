import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, Return, CreateReturn } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ReturnApiService {
  private readonly api = `${environment.apiUrl}/returns`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, customerId?: number): Observable<ApiResponse<PagedResult<Return>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (customerId) p = p.set('customerId', customerId);
    return this.http.get<ApiResponse<PagedResult<Return>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<Return>> {
    return this.http.get<ApiResponse<Return>>(`${this.api}/${id}`);
  }
  create(data: CreateReturn): Observable<ApiResponse<Return>> {
    return this.http.post<ApiResponse<Return>>(this.api, data);
  }
  update(id: number, data: any): Observable<ApiResponse<Return>> {
    return this.http.put<ApiResponse<Return>>(`${this.api}/${id}`, data);
  }
  approve(id: number, approved: boolean, comments?: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/approve`, { approved, comments });
  }
  receive(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/receive`, {});
  }
  process(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/process`, {});
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
