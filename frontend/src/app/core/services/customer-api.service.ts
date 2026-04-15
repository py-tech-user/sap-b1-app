import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse, PagedResult,
  Customer, CreateCustomer, UpdateCustomer
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class CustomerApiService {
  private readonly http = inject(HttpClient);
  private readonly sapUrl = `${environment.apiUrl}/sap/clients`;
  private readonly partnersUrl = `${environment.apiUrl}/sap/partners`;

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

    return this.http.get<any>(this.sapUrl, { params }).pipe(
      map((res) => this.toPagedResponse(res, page, pageSize)),
      switchMap((result) => {
        const empty = (result.data?.items?.length ?? 0) === 0;
        if (!empty) return of(result);

        // Some SAP backends ignore/forbid extra query params; retry plain GET.
        return this.http.get<any>(this.sapUrl).pipe(
          map((res) => this.toPagedResponse(res, page, pageSize)),
          switchMap((retryResult) => {
            const retryEmpty = (retryResult.data?.items?.length ?? 0) === 0;
            if (!retryEmpty) return of(retryResult);
            return this.http.get<any>(this.partnersUrl).pipe(
              map((res) => this.toPagedResponse(res, page, pageSize)),
              catchError(() => of(retryResult))
            );
          }),
          catchError(() => of(result))
        );
      }),
      catchError(() => this.http.get<any>(this.sapUrl).pipe(
        map((res) => this.toPagedResponse(res, page, pageSize)),
        switchMap((result) => {
          const empty = (result.data?.items?.length ?? 0) === 0;
          if (!empty) return of(result);
          return this.http.get<any>(this.partnersUrl).pipe(
            map((res) => this.toPagedResponse(res, page, pageSize)),
            catchError(() => of(result))
          );
        }),
        catchError(() => of({
          success: true,
          data: {
            items: [],
            totalCount: 0,
            page,
            pageSize,
            totalPages: 1
          }
        } as ApiResponse<PagedResult<Customer>>))
      ))
    );
  }

  getById(id: number): Observable<ApiResponse<Customer>> {
    return this.http.get<any>(this.sapUrl).pipe(
      map((res) => {
        const rows = this.extractRows(res);
        const found = rows.find((row) => Number(row?.id ?? row?.DocEntry ?? 0) === id) ?? rows[id - 1] ?? null;
        return {
          success: !!found,
          data: found ? this.normalizeCustomer(found, id) : undefined,
          message: found ? undefined : 'Client introuvable'
        } as ApiResponse<Customer>;
      })
    );
  }

  create(dto: CreateCustomer): Observable<ApiResponse<Customer>> {
    return this.http.post<any>(this.sapUrl, {
      cardCode: dto.cardCode,
      cardName: dto.cardName,
      currency: dto.isActive ? (dto as any).currency ?? 'MAD' : 'MAD'
    }).pipe(
      map((res) => {
        const payload = res?.data ?? res;
        return {
          success: res?.success ?? true,
          message: res?.message,
          data: this.normalizeCustomer(payload, 1)
        } as ApiResponse<Customer>;
      })
    );
  }

  update(id: number, dto: UpdateCustomer): Observable<ApiResponse<Customer>> {
    return this.http.put<ApiResponse<Customer>>(`/api/customers/${id}`, dto);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`/api/customers/${id}`);
  }

  syncToSap(id: number): Observable<ApiResponse<Customer>> {
    return this.http.post<ApiResponse<Customer>>(
      `/api/customers/${id}/sync-sap`, {});
  }

  private extractRows(res: any): any[] {
    if (!res) return [];
    if (Array.isArray(res)) return res;
    if (Array.isArray(res.value)) return res.value;
    if (Array.isArray(res.data)) return res.data;
    if (Array.isArray(res.data?.value)) return res.data.value;
    if (Array.isArray(res.data?.items)) return res.data.items;
    if (Array.isArray(res.data?.result)) return res.data.result;
    if (Array.isArray(res.data?.result?.items)) return res.data.result.items;
    if (Array.isArray(res.result)) return res.result;
    if (Array.isArray(res.result?.items)) return res.result.items;
    if (Array.isArray(res.items)) return res.items;
    return [];
  }

  private normalizeCustomer(row: any, index: number): Customer {
    const pickText = (...values: Array<unknown>): string => {
      for (const value of values) {
        const text = String(value ?? '').trim();
        if (text) return text;
      }
      return '';
    };

    return {
      id: Number(row?.id ?? row?.DocEntry ?? index + 1),
      cardCode: pickText(row?.cardCode, row?.CardCode, row?.CustomerCode, row?.code),
      cardName: pickText(row?.cardName, row?.CardName, row?.CustomerName, row?.name),
      email: row?.email ?? row?.EmailAddress,
      phone: row?.phone ?? row?.Cellular ?? row?.Phone1,
      address: row?.address,
      isActive: true,
      sapBpCode: row?.CardCode
    };
  }

  private toPagedResponse(res: any, page: number, pageSize: number): ApiResponse<PagedResult<Customer>> {
    if (res?.success !== undefined && res?.data?.items) {
      return res as ApiResponse<PagedResult<Customer>>;
    }

    const rows = this.extractRows(res);
    const items = rows.map((row, index) => this.normalizeCustomer(row, index));
    return {
      success: true,
      data: {
        items,
        totalCount: items.length,
        page,
        pageSize,
        totalPages: Math.max(1, Math.ceil(items.length / pageSize))
      }
    } as ApiResponse<PagedResult<Customer>>;
  }
}
