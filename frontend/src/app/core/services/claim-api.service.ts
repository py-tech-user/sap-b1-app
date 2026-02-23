import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult, Claim, CreateClaim } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ClaimApiService {
  private readonly api = `${environment.apiUrl}/claims`;
  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, priority?: string, customerId?: number): Observable<ApiResponse<PagedResult<Claim>>> {
    let p = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) p = p.set('status', status);
    if (priority) p = p.set('priority', priority);
    if (customerId) p = p.set('customerId', customerId);
    return this.http.get<ApiResponse<PagedResult<Claim>>>(this.api, { params: p });
  }
  getById(id: number): Observable<ApiResponse<Claim>> {
    return this.http.get<ApiResponse<Claim>>(`${this.api}/${id}`);
  }
  create(data: CreateClaim): Observable<ApiResponse<Claim>> {
    return this.http.post<ApiResponse<Claim>>(this.api, data);
  }
  update(id: number, data: any): Observable<ApiResponse<Claim>> {
    return this.http.put<ApiResponse<Claim>>(`${this.api}/${id}`, data);
  }
  addComment(id: number, comment: string, isInternal: boolean): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/comments`, { comment, isInternal });
  }
  assign(id: number, userId: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/assign/${userId}`, {});
  }
  resolve(id: number, resolution: string): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/resolve`, JSON.stringify(resolution), {
      headers: { 'Content-Type': 'application/json' }
    });
  }
  close(id: number): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(`${this.api}/${id}/close`, {});
  }
  delete(id: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`${this.api}/${id}`);
  }
}
