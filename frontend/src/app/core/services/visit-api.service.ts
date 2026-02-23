import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Visit, CreateVisit, UpdateVisit, PagedResult } from '../models/models';

@Injectable({ providedIn: 'root' })
export class VisitApiService {
  private readonly apiUrl = `${environment.apiUrl}/visits`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 10, status?: string, customerId?: number): Observable<PagedResult<Visit>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    if (status) params = params.set('status', status);
    if (customerId) params = params.set('customerId', customerId.toString());
    return this.http.get<PagedResult<Visit>>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Visit> {
    return this.http.get<Visit>(`${this.apiUrl}/${id}`);
  }

  create(visit: CreateVisit): Observable<Visit> {
    return this.http.post<Visit>(this.apiUrl, visit);
  }

  update(id: number, visit: UpdateVisit): Observable<Visit> {
    return this.http.put<Visit>(`${this.apiUrl}/${id}`, visit);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  syncToSap(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/sync-sap`, {});
  }
}
