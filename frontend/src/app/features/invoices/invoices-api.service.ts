import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { SapApiService } from '../../core/services/sap-api.service';

export interface InvoiceListFilters {
  page: number;
  pageSize: number;
  search?: string;
  customer?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface InvoiceListItem {
  id: number;
  docNum: string;
  cardCode: string;
  cardName: string;
  docDate?: string;
  docTotal: number;
  status: string;
}

export interface InvoiceListResult {
  items: InvoiceListItem[];
  totalCount: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class InvoicesApiService {
  private readonly sapApi = inject(SapApiService);

  getList(filters: InvoiceListFilters): Observable<InvoiceListResult> {
    let params = new HttpParams()
      .set('page', String(filters.page))
      .set('pageSize', String(filters.pageSize));

    if (filters.search) params = params.set('search', filters.search);
    if (filters.customer) params = params.set('customer', filters.customer);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.dateFrom) params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo) params = params.set('dateTo', filters.dateTo);

    return this.sapApi.get<any>('factures', params).pipe(
      map((res) => this.normalizeList(res))
    );
  }

  private normalizeList(res: any): InvoiceListResult {
    const data = res?.data ?? res?.Data ?? {};
    const source = Array.isArray(data)
      ? data
      : Array.isArray(data?.items)
        ? data.items
        : Array.isArray(data?.Items)
          ? data.Items
          : [];

    const items = source.map((row: any) => this.toInvoiceListItem(row));
    const page = this.resolvePositiveInt(
      [res?.page, res?.Page, data?.page, data?.Page, data?.data?.page, data?.data?.Page],
      1
    );
    const pageSize = this.resolvePositiveInt(
      [res?.pageSize, res?.PageSize, data?.pageSize, data?.PageSize, data?.data?.pageSize, data?.data?.PageSize],
      15
    );
    const totalPages = this.resolvePositiveInt(
      [
        res?.totalPages,
        res?.TotalPages,
        res?.pageCount,
        res?.PageCount,
        data?.totalPages,
        data?.TotalPages,
        data?.pageCount,
        data?.PageCount,
        data?.data?.totalPages,
        data?.data?.TotalPages
      ],
      Math.max(1, Math.ceil(items.length / Math.max(1, pageSize)))
    );
    const totalCountFromPayload = this.resolveTotalCount(
      [
        res?.totalCount,
        res?.TotalCount,
        res?.count,
        res?.Count,
        res?.total,
        res?.Total,
        data?.totalCount,
        data?.TotalCount,
        data?.count,
        data?.Count,
        data?.total,
        data?.Total,
        data?.data?.totalCount,
        data?.data?.TotalCount
      ],
      items.length
    );
    const totalCount = this.promoteTotalCount(totalCountFromPayload, totalPages, pageSize, page, items.length);

    return {
      items,
      totalCount: Number.isFinite(totalCount) ? totalCount : items.length,
      totalPages
    };
  }

  private resolvePositiveInt(candidates: unknown[], fallback: number): number {
    for (const candidate of candidates) {
      const parsed = Number(candidate);
      if (Number.isFinite(parsed) && parsed > 0) {
        return Math.floor(parsed);
      }
    }

    return fallback;
  }

  private promoteTotalCount(
    totalCount: number,
    totalPages: number,
    pageSize: number,
    page: number,
    itemCount: number
  ): number {
    const minFromLoadedRows = itemCount > 0
      ? ((Math.max(1, page) - 1) * pageSize) + itemCount
      : 0;
    const minFromTotalPages = totalPages > 1
      ? ((totalPages - 1) * pageSize) + 1
      : 0;

    return Math.max(totalCount, minFromLoadedRows, minFromTotalPages);
  }

  private toInvoiceListItem(row: any): InvoiceListItem {
    return {
      id: Number(row?.docEntry ?? row?.DocEntry ?? row?.id ?? 0),
      docNum: String(row?.docNum ?? row?.DocNum ?? row?.documentNumber ?? ''),
      cardCode: String(row?.cardCode ?? row?.CardCode ?? ''),
      cardName: String(row?.cardName ?? row?.CardName ?? ''),
      docDate: row?.date ?? row?.Date ?? row?.docDate ?? row?.DocDate,
      docTotal: Number(row?.total ?? row?.Total ?? row?.docTotal ?? row?.DocTotal ?? 0),
      status: String(row?.status ?? row?.Status ?? row?.docStatus ?? row?.DocStatus ?? row?.documentStatus ?? row?.DocumentStatus ?? '')
    };
  }

  private resolveTotalCount(candidates: unknown[], fallback: number): number {
    let foundZero = false;

    for (const candidate of candidates) {
      const parsed = Number(candidate);
      if (!Number.isFinite(parsed) || parsed < 0) {
        continue;
      }

      if (parsed > 0) {
        return Math.floor(parsed);
      }

      foundZero = true;
    }

    if (foundZero) {
      return 0;
    }

    return Math.max(0, Math.floor(Number.isFinite(fallback) ? fallback : 0));
  }
}
