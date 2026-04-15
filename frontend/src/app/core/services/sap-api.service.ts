import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SapApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/sap`;
  private readonly jsonHeaders = new HttpHeaders({
    'Content-Type': 'application/json'
  });

  get<T>(path: string, params?: HttpParams): Observable<T> {
    const url = this.buildUrl(path);
    this.logRequest('GET', url);
    return this.http.get<T>(url, { params }).pipe(
      tap((response) => this.logResponse('GET', url, response)),
      catchError((err) => this.handleError('GET', url, err))
    );
  }

  post<T>(path: string, body: unknown): Observable<T> {
    const url = this.buildUrl(path);
    this.logRequest('POST', url, body);
    return this.http.post<T>(url, body, { headers: this.jsonHeaders }).pipe(
      tap((response) => this.logResponse('POST', url, response)),
      catchError((err) => this.handleError('POST', url, err, body))
    );
  }

  put<T>(path: string, body: unknown): Observable<T> {
    const url = this.buildUrl(path);
    this.logRequest('PUT', url, body);
    return this.http.put<T>(url, body, { headers: this.jsonHeaders }).pipe(
      tap((response) => this.logResponse('PUT', url, response)),
      catchError((err) => this.handleError('PUT', url, err, body))
    );
  }

  patch<T>(path: string, body: unknown): Observable<T> {
    const url = this.buildUrl(path);
    this.logRequest('PATCH', url, body);
    return this.http.patch<T>(url, body, { headers: this.jsonHeaders }).pipe(
      tap((response) => this.logResponse('PATCH', url, response)),
      catchError((err) => this.handleError('PATCH', url, err, body))
    );
  }

  delete<T>(path: string): Observable<T> {
    const url = this.buildUrl(path);
    this.logRequest('DELETE', url);
    return this.http.delete<T>(url).pipe(
      tap((response) => this.logResponse('DELETE', url, response)),
      catchError((err) => this.handleError('DELETE', url, err))
    );
  }

  private buildUrl(path: string): string {
    const clean = (path || '').replace(/^\/+/, '');
    return `${this.baseUrl}/${clean}`;
  }

  private logRequest(method: string, url: string, payload?: unknown): void {
    if (payload === undefined) {
      console.debug(`[SAP API] ${method} ${url}`);
      return;
    }
    console.debug(`[SAP API] ${method} ${url} payload`, payload);
  }

  private logResponse(method: string, url: string, response: unknown): void {
    console.debug(`[SAP API] ${method} ${url} response`, response);
  }

  private handleError(method: string, url: string, err: any, payload?: unknown): Observable<never> {
    const status = Number(err?.status ?? 0);

    if (status === 400) {
      console.error(`[SAP API] 400 Bad Request on ${method} ${url}`, { payload, error: err?.error ?? err });
    } else if (status === 401) {
      console.error(`[SAP API] 401 Unauthorized on ${method} ${url}`, { payload, error: err?.error ?? err });
    } else if (status === 500) {
      console.error(`[SAP API] 500 Server Error on ${method} ${url}`, { payload, error: err?.error ?? err });
    } else {
      console.error(`[SAP API] ${method} ${url} failed`, { status, payload, error: err?.error ?? err });
    }

    return throwError(() => err);
  }
}
