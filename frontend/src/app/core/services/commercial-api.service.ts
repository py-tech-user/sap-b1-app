import { Injectable, inject } from '@angular/core';
import { HttpParams } from '@angular/common/http';
import { Observable, forkJoin, of, throwError } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import {
  ApiResponse,
  PagedResult,
  CommercialDashboard,
  CommercialDocument,
  CommercialListFilters,
  CommercialResource,
  InvoicePaymentDto,
  SaveCommercialDocumentDto
} from '../models/models';
import { SapApiService } from './sap-api.service';

@Injectable({ providedIn: 'root' })
export class CommercialApiService {
  private readonly sapApi = inject(SapApiService);

  getList(resource: CommercialResource, filters: CommercialListFilters): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    let params = new HttpParams()
      .set('page', String(filters.page ?? 1))
      .set('pageSize', String(filters.pageSize ?? 20));

    if (filters.search) params = params.set('search', filters.search);
    if (filters.customer) params = params.set('customer', filters.customer);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.dateFrom) params = params.set('dateFrom', filters.dateFrom);
    if (filters.dateTo) params = params.set('dateTo', filters.dateTo);

    const endpoints = this.getResourceEndpoints(resource);

    if (resource === 'invoices') {
      return this.getInvoicesListByDocEntry(endpoints, params, filters);
    }

    return this.getListWithFallback(endpoints, 0, params, filters);
  }

  private getInvoicesListByDocEntry(
    endpoints: string[],
    params: HttpParams,
    filters: CommercialListFilters
  ): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    // [HYBRID-MODE] Mode hybride strict: lecture SQL uniquement, pas de fallback
    const endpoint = endpoints[0];
    if (!endpoint) {
      return throwError(() => new Error('Configuration endpoint manquante pour invoices.'));
    }

    return this.sapApi
      .get<any>(endpoint, params)
      .pipe(
        map((res) => this.normalizeListResponse(res, filters)),
        switchMap((res) => this.hydrateInvoicesByDocEntry(endpoints, res, filters)),
        catchError((err) => {
          // [HYBRID-MODE] Pas de fallback silencieux - retourner l'erreur explicitement
          console.error('[HYBRID-MODE-FRONTEND] Erreur lecture invoices (SQL strict):', err);
          return throwError(() => err);
        })
      );
  }

  private hydrateInvoicesByDocEntry(
    endpoints: string[],
    res: ApiResponse<PagedResult<CommercialDocument>>,
    filters: CommercialListFilters
  ): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    const page = Number(res.data?.page ?? filters.page ?? 1);
    const pageSize = Number(res.data?.pageSize ?? filters.pageSize ?? 20);
    const sourceItems = Array.isArray(res.data?.items) ? res.data!.items : [];

    if (sourceItems.length === 0) {
      return of({
        success: res.success,
        message: res.message,
        data: {
          items: [],
          totalCount: 0,
          page,
          pageSize,
          totalPages: 1
        }
      });
    }

    const detailCalls = sourceItems.map((invoice) =>
      this.getByIdWithFallback(endpoints, 0, invoice.id).pipe(
        map((detail) => detail.data ?? invoice),
        catchError(() => of(invoice))
      )
    );

    return forkJoin(detailCalls).pipe(
      map((hydratedItems) => {
        const filteredItems = hydratedItems;
        const totalCount = Number(res.data?.totalCount ?? filteredItems.length);

        return {
          success: res.success,
          message: res.message,
          data: {
            items: filteredItems,
            totalCount,
            page,
            pageSize,
            totalPages: Math.max(1, Math.ceil(totalCount / pageSize))
          }
        } as ApiResponse<PagedResult<CommercialDocument>>;
      })
    );
  }

  private getListWithFallback(
    endpoints: string[],
    index: number,
    params: HttpParams,
    filters: CommercialListFilters
  ): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    const endpoint = endpoints[index];
    if (!endpoint) {
      return of(this.emptyListResponse(filters));
    }

    return this.sapApi
      .get<any>(endpoint, params)
      .pipe(
        map((res) => this.normalizeListResponse(res, filters)),
        catchError((err) => {
          // Retry next endpoint alias for path mismatches (e.g. deliverynotes vs delivery-notes).
          if ((err?.status === 404 || err?.status === 405) && index + 1 < endpoints.length) {
            return this.getListWithFallback(endpoints, index + 1, params, filters);
          }

          // Some SAP adapters return HTTP 400 with a business message when no readable data is available.
          if (err?.status === 400 && (err?.error?.message || '').toLowerCase().includes('service layer')) {
            return of(this.emptyListResponse(filters));
          }

          // If we exhausted endpoint aliases and still get a client-side HTTP error, show an empty list
          // rather than blocking the whole screen with a generic error banner.
          if ((err?.status === 400 || err?.status === 404 || err?.status === 405) && index + 1 >= endpoints.length) {
            return of(this.emptyListResponse(filters));
          }

          return throwError(() => err);
        })
      );
  }

  getById(resource: CommercialResource, id: number): Observable<ApiResponse<CommercialDocument>> {
    const endpoints = this.getResourceEndpoints(resource);
    return this.getByIdWithFallback(endpoints, 0, id);
  }

  private getByIdWithFallback(
    endpoints: string[],
    index: number,
    id: number
  ): Observable<ApiResponse<CommercialDocument>> {
    const endpoint = endpoints[index];
    if (!endpoint) {
      return throwError(() => new Error('Document introuvable.'));
    }

    return this.sapApi
      .get<any>(`${endpoint}/${id}`)
      .pipe(
        map((res) => this.normalizeByIdResponse(res)),
        catchError((err) => {
          if ((err?.status === 404 || err?.status === 405) && index + 1 < endpoints.length) {
            return this.getByIdWithFallback(endpoints, index + 1, id);
          }
          return throwError(() => err);
        })
      );
  }

  create(resource: CommercialResource, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    const sapPayload = this.toSapPayload(dto, false);
    if (resource === 'orders') {
      return this.sapApi
        .post<any>('orders', sapPayload)
        .pipe(
          map((res) => this.normalizeByIdResponse(res)),
          catchError((err) => {
            if (err?.status === 404 || err?.status === 405) {
              return this.sapApi
                .post<any>('bc', sapPayload)
                .pipe(map((res) => this.normalizeByIdResponse(res)));
            }
            return throwError(() => err);
          })
        );
    }

    const endpoint = this.getPrimaryEndpoint(resource);
    return this.sapApi
      .post<any>(endpoint, sapPayload)
      .pipe(map((res) => this.normalizeByIdResponse(res)));
  }

  update(resource: CommercialResource, id: number, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    const endpoint = this.getPrimaryEndpoint(resource);
    const sapPayload = this.toSapPayload(dto, true);
    return this.sapApi
      .put<any>(`${endpoint}/${id}`, sapPayload)
      .pipe(map((res) => this.normalizeByIdResponse(res)));
  }

  delete(resource: CommercialResource, id: number): Observable<ApiResponse<void>> {
    const endpoint = this.getPrimaryEndpoint(resource);
    return this.sapApi
      .delete<any>(`${endpoint}/${id}`)
      .pipe(map((res) => this.normalizeDeleteResponse(res)));
  }

  close(resource: CommercialResource, id: number): Observable<ApiResponse<CommercialDocument>> {
    const endpoints = this.getResourceEndpoints(resource);
    const attempts = endpoints.map((endpoint) => () =>
      this.sapApi
        .post<any>(`${endpoint}/${id}/close`, {})
        .pipe(map((res) => this.normalizeByIdResponse(res)))
    );

    return this.tryCloseAttempts(attempts, 0);
  }

  updateStatus(resource: CommercialResource, id: number, status: string): Observable<ApiResponse<CommercialDocument>> {
    const endpoint = this.getPrimaryEndpoint(resource);
    const statusUrl = `${endpoint}/${id}/status`;
    const fallbackUrl = `${endpoint}/${id}`;
    const normalized = (status || '').trim();
    const lower = normalized.toLowerCase();
    const upper = normalized.toUpperCase();
    const payload = {
      status: lower,
      newStatus: lower,
      statusCode: lower,
      statusId: lower,
      statusValue: lower,
      statusName: lower,
      documentStatus: lower,
      document_state: lower,
      state: lower,
      nextStatus: lower,
      statusText: upper,
      statusUpper: upper
    };

    const attempts: Array<() => Observable<ApiResponse<CommercialDocument>>> = [
      () => this.sapApi.patch<any>(statusUrl, payload).pipe(map((res) => this.normalizeByIdResponse(res))),
      () => this.sapApi.put<any>(statusUrl, payload).pipe(map((res) => this.normalizeByIdResponse(res))),
      () => this.sapApi.post<any>(statusUrl, payload).pipe(map((res) => this.normalizeByIdResponse(res))),
      () => this.sapApi.post<any>(`${statusUrl}/${status}`, {}).pipe(map((res) => this.normalizeByIdResponse(res))),
      () => this.sapApi.patch<any>(fallbackUrl, payload).pipe(map((res) => this.normalizeByIdResponse(res))),
      () => this.sapApi.put<any>(fallbackUrl, payload).pipe(map((res) => this.normalizeByIdResponse(res)))
    ];

    return this.tryStatusAttempts(attempts, 0);
  }

  private tryStatusAttempts(
    attempts: Array<() => Observable<ApiResponse<CommercialDocument>>>,
    index: number
  ): Observable<ApiResponse<CommercialDocument>> {
    const exec = attempts[index];
    if (!exec) {
      return throwError(() => new Error('Impossible de modifier le statut: aucune route compatible.'));
    }

    return exec().pipe(
      catchError(err => {
        if (!this.shouldFallbackStatus(err) || index + 1 >= attempts.length) {
          return throwError(() => err);
        }
        return this.tryStatusAttempts(attempts, index + 1);
      })
    );
  }

  private shouldFallbackStatus(err: any): boolean {
    const status = err?.status;
    return status === 404 || status === 405 || status === 400 || status === 500;
  }

  private tryCloseAttempts(
    attempts: Array<() => Observable<ApiResponse<CommercialDocument>>>,
    index: number
  ): Observable<ApiResponse<CommercialDocument>> {
    const exec = attempts[index];
    if (!exec) {
      return throwError(() => new Error('Impossible de clôturer le document: aucune route compatible.'));
    }

    return exec().pipe(
      catchError(err => {
        const status = Number(err?.status ?? 0);
        if ((status === 404 || status === 405) && index + 1 < attempts.length) {
          return this.tryCloseAttempts(attempts, index + 1);
        }
        return throwError(() => err);
      })
    );
  }

  generateOrderFromQuote(quoteId: number, selectedLineNums?: number[]): Observable<ApiResponse<CommercialDocument>> {
    const payload = {
      DocObjectCode: '17',
      TargetDocObjectCode: '17',
      targetDocObjectCode: '17',
      selectedLineNums: (selectedLineNums ?? []).filter(n => Number.isFinite(n) && n >= 0)
    };

    return this.sapApi
      .post<any>(`orders/from-quote/${quoteId}`, payload)
      .pipe(map((res) => this.normalizeByIdResponse(res)));
  }

  generateDeliveryNoteFromOrder(orderId: number, selectedLineNums?: number[]): Observable<ApiResponse<CommercialDocument>> {
    const payload = {
      DocObjectCode: '15',
      TargetDocObjectCode: '15',
      targetDocObjectCode: '15',
      selectedLineNums: (selectedLineNums ?? []).filter(n => Number.isFinite(n) && n >= 0)
    };

    return this.sapApi
      .post<any>(`delivery-notes/from-order/${orderId}`, payload)
      .pipe(map((res) => this.normalizeByIdResponse(res)));
  }

  generateInvoiceFromDelivery(deliveryNoteId: number, selectedLineNums?: number[]): Observable<ApiResponse<CommercialDocument>> {
    const payload = {
      DocObjectCode: '13',
      TargetDocObjectCode: '13',
      targetDocObjectCode: '13',
      selectedLineNums: (selectedLineNums ?? []).filter(n => Number.isFinite(n) && n >= 0)
    };

    return this.sapApi
      .post<any>(`factures/from-delivery-note/${deliveryNoteId}`, payload)
      .pipe(map((res) => this.normalizeByIdResponse(res)));
  }

  addInvoicePayment(invoiceId: number, dto: InvoicePaymentDto): Observable<ApiResponse<unknown>> {
    const amount = Number(dto.amount ?? 0);
    const paymentDate = String(dto.paymentDate ?? '').slice(0, 10);
    const paymentMethod = String(dto.paymentMethod ?? '').trim() || 'Virement';
    const reference = dto.reference ? String(dto.reference).trim() : undefined;

    const payload = {
      invoiceId,
      amount,
      paymentDate,
      paymentMethod,
      reference,
      Amount: amount,
      PaymentDate: paymentDate,
      PaymentMethod: paymentMethod,
      Reference: reference,
      DocEntry: invoiceId,
      InvoiceId: invoiceId,
      SumApplied: amount
    };

    const attempts: Array<() => Observable<ApiResponse<unknown>>> = [
      () => this.sapApi.post<ApiResponse<unknown>>(`factures/${invoiceId}/payments`, payload),
      () => this.sapApi.post<ApiResponse<unknown>>(`factures/${invoiceId}/payment`, payload),
      () => this.sapApi.post<ApiResponse<unknown>>(`factures/${invoiceId}/pay`, payload),
      () => this.sapApi.post<ApiResponse<unknown>>('invoice-payments', payload),
      () => this.sapApi.post<ApiResponse<unknown>>('payments', payload)
    ];

    return this.tryInvoicePaymentAttempts(attempts, 0);
  }

  private tryInvoicePaymentAttempts(
    attempts: Array<() => Observable<ApiResponse<unknown>>>,
    index: number
  ): Observable<ApiResponse<unknown>> {
    const exec = attempts[index];
    if (!exec) {
      return throwError(() => new Error('Impossible d\'enregistrer le paiement: aucune route compatible.'));
    }

    return exec().pipe(
      catchError(err => {
        if (!this.shouldFallbackInvoicePayment(err) || index + 1 >= attempts.length) {
          return throwError(() => err);
        }
        return this.tryInvoicePaymentAttempts(attempts, index + 1);
      })
    );
  }

  private shouldFallbackInvoicePayment(err: any): boolean {
    const status = err?.status;
    return status === 400 || status === 404 || status === 405 || status === 500;
  }

  getCommercialDashboard(): Observable<ApiResponse<CommercialDashboard>> {
    return this.sapApi.get<ApiResponse<CommercialDashboard>>('dashboard/commercial');
  }

  private getPrimaryEndpoint(resource: CommercialResource): string {
    return this.getResourceEndpoints(resource)[0];
  }

  private toSapPayload(dto: SaveCommercialDocumentDto, includeLineMetadata: boolean): Record<string, unknown> {
    const lines = (dto.lines ?? [])
      .map((line) => {
        const itemCode = String(line.itemCode ?? '').trim();
        const quantity = Number(line.quantity ?? 0);
        const warehouseCode = String(line.warehouseCode ?? '').trim();
        const unitPrice = Number(line.unitPrice ?? 0);
        const discountPct = Number(line.discountPct ?? 0);
        const vatPct = Number(line.vatPct ?? 0);

        const mapped: Record<string, unknown> = {
          ItemCode: itemCode,
          Quantity: quantity,
          WarehouseCode: warehouseCode
        };

        if (includeLineMetadata) {
          const lineNum = Number(line.lineNum ?? line.id ?? NaN);
          if (Number.isFinite(lineNum) && lineNum >= 0) {
            mapped['LineNum'] = lineNum;
          }
        }

        if (Number.isFinite(unitPrice) && unitPrice > 0) {
          mapped['UnitPrice'] = unitPrice;
          mapped['Price'] = unitPrice;
        }

        if (Number.isFinite(discountPct) && discountPct > 0) {
          mapped['DiscountPercent'] = discountPct;
        }

        if (Number.isFinite(vatPct) && vatPct >= 0) {
          mapped['VatPercent'] = vatPct;
        }

        return mapped;
      })
      .filter((line) => String(line['ItemCode'] ?? '') !== '' && Number(line['Quantity'] ?? 0) > 0 && String(line['WarehouseCode'] ?? '') !== '');

    const payload: Record<string, unknown> = {
      CardCode: String(dto.cardCode ?? '').trim(),
      DocumentLines: lines
    };

    const docDate = this.normalizeDate(dto.docDate);
    const dueDate = this.normalizeDate(dto.dueDate);
    if (docDate) payload['DocDate'] = docDate;
    if (dueDate) {
      payload['DocDueDate'] = dueDate;
      payload['RequiredDate'] = dueDate;
    }
    if (dto.currency?.trim()) payload['DocCurrency'] = dto.currency.trim();
    if (dto.comments?.trim()) payload['Comments'] = dto.comments.trim();

    return payload;
  }

  private normalizeDate(value?: string): string | undefined {
    if (!value) return undefined;
    const date = value.slice(0, 10);
    if (/^\d{4}-\d{2}-\d{2}$/.test(date)) return date;
    return undefined;
  }

  private normalizeListResponse(
    res: any,
    filters: CommercialListFilters
  ): ApiResponse<PagedResult<CommercialDocument>> {
    if (res?.success !== undefined && res?.data) {
      const rawItems = Array.isArray(res.data.items)
        ? res.data.items
        : this.extractArray(res.data);
      const items = rawItems.map((row: any) => this.normalizeDocument(row));
      const page = this.toPositiveInt(res.data.page ?? res.data.Page ?? filters.page ?? 1, 1);
      const pageSize = this.toPositiveInt(res.data.pageSize ?? res.data.PageSize ?? filters.pageSize ?? 20, 20);
      const totalPagesFromPayload = this.resolvePositiveInt(
        [
          res.data.totalPages,
          res.data.TotalPages,
          res.totalPages,
          res.TotalPages,
          res.pageCount,
          res.PageCount
        ],
        0
      );
      const totalCountFromPayload = this.resolveTotalCount(
        [
          res.data.totalCount,
          res.data.TotalCount,
          res.totalCount,
          res.TotalCount,
          res.count,
          res.Count,
          res.total,
          res.Total
        ],
        items.length
      );
      const totalCount = this.promoteTotalCount(totalCountFromPayload, totalPagesFromPayload, pageSize, page, items.length);
      const totalPages = Math.max(totalPagesFromPayload, Math.max(1, Math.ceil(totalCount / pageSize)));

      return {
        success: res.success !== false,
        message: res.message,
        data: {
          items,
          totalCount,
          page,
          pageSize,
          totalPages
        }
      };
    }

    const source = this.extractArray(res);
    const items = source.map((row) => this.normalizeDocument(row));
    const page = this.toPositiveInt(res?.page ?? res?.Page ?? res?.data?.page ?? res?.data?.Page ?? filters.page ?? 1, 1);
    const pageSize = this.toPositiveInt(res?.pageSize ?? res?.PageSize ?? res?.data?.pageSize ?? res?.data?.PageSize ?? filters.pageSize ?? 20, 20);
    const totalPagesFromPayload = this.resolvePositiveInt(
      [
        res?.totalPages,
        res?.TotalPages,
        res?.pageCount,
        res?.PageCount,
        res?.data?.totalPages,
        res?.data?.TotalPages,
        res?.data?.pageCount,
        res?.data?.PageCount
      ],
      0
    );
    const totalCountFromPayload = this.resolveTotalCount(
      [
        res?.totalCount,
        res?.TotalCount,
        res?.count,
        res?.Count,
        res?.total,
        res?.Total,
        res?.data?.totalCount,
        res?.data?.TotalCount,
        res?.data?.count,
        res?.data?.Count,
        res?.data?.total,
        res?.data?.Total
      ],
      items.length
    );
    const totalCount = this.promoteTotalCount(totalCountFromPayload, totalPagesFromPayload, pageSize, page, items.length);
    const totalPages = Math.max(totalPagesFromPayload, Math.max(1, Math.ceil(totalCount / pageSize)));

    return {
      success: true,
      data: {
        items,
        totalCount,
        page,
        pageSize,
        totalPages
      }
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

  private toPositiveInt(value: unknown, fallback: number): number {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) return fallback;
    const rounded = Math.floor(parsed);
    return rounded > 0 ? rounded : fallback;
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

  private normalizeByIdResponse(res: any): ApiResponse<CommercialDocument> {
    if (res?.success !== undefined && res?.data) {
      return {
        ...res,
        data: this.normalizeDocument(res.data)
      } as ApiResponse<CommercialDocument>;
    }

    const source = res?.data ?? res?.value ?? res;
    return {
      success: true,
      data: this.normalizeDocument(source)
    };
  }

  private normalizeDeleteResponse(res: any): ApiResponse<void> {
    if (res?.success !== undefined) {
      return res as ApiResponse<void>;
    }
    return { success: true };
  }

  private emptyListResponse(filters: CommercialListFilters): ApiResponse<PagedResult<CommercialDocument>> {
    const page = filters.page ?? 1;
    const pageSize = filters.pageSize ?? 20;
    return {
      success: true,
      data: {
        items: [],
        totalCount: 0,
        page,
        pageSize,
        totalPages: 1
      }
    };
  }

  private getResourceEndpoints(resource: CommercialResource): string[] {
    switch (resource) {
      case 'invoices':
        return ['factures'];
      case 'deliverynotes':
        return ['delivery-notes', 'deliverynotes', 'deliverynote'];
      case 'creditnotes':
        return ['credit-notes', 'creditnotes', 'creditnote'];
      case 'returns':
        return ['returns', 'return', 'return-notes'];
      default:
        return [resource];
    }
  }

  private extractArray(res: any): any[] {
    if (Array.isArray(res)) return res;
    if (Array.isArray(res?.value)) return res.value;
    if (Array.isArray(res?.data)) return res.data;
    if (Array.isArray(res?.items)) return res.items;
    if (Array.isArray(res?.data?.items)) return res.data.items;
    if (Array.isArray(res?.data?.value)) return res.data.value;
    if (Array.isArray(res?.data?.data?.items)) return res.data.data.items;
    if (Array.isArray(res?.data?.data?.value)) return res.data.data.value;
    if (Array.isArray(res?.result)) return res.result;
    if (Array.isArray(res?.result?.items)) return res.result.items;
    return [];
  }

  private normalizeDocument(raw: any): CommercialDocument {
    const doc = raw ?? {};
    const id = Number(doc.docEntry ?? doc.DocEntry ?? doc.id ?? doc.Id ?? 0);
    const postingDate = doc.docDate
      ?? doc.DocDate
      ?? doc.postingDate
      ?? doc.PostingDate
      ?? doc.date
      ?? doc.Date;
    const dueDate = doc.dueDate
      ?? doc.DocDueDate
      ?? doc.DueDate
      ?? doc.due_date
      ?? doc.requiredDate
      ?? doc.RequiredDate
      ?? postingDate;
    const status = this.normalizeStatus(
      doc.status
      ?? doc.Status
      ?? doc.DocumentStatus
      ?? doc.documentStatus
      ?? doc.DocStatus
      ?? doc.docStatus
    );
    const total = Number(
      doc.docTotal
      ?? doc.DocTotal
      ?? doc.totalAmount
      ?? doc.total
      ?? doc.Total
      ?? 0
    );

    const sourceLines = Array.isArray(doc.lines)
      ? doc.lines
      : (Array.isArray(doc.Lines)
        ? doc.Lines
        : (Array.isArray(doc.documentLines)
          ? doc.documentLines
          : (Array.isArray(doc.DocumentLines)
            ? doc.DocumentLines
            : [])));

    const sourceLinked = Array.isArray(doc.linkedDocuments)
      ? doc.linkedDocuments
      : (Array.isArray(doc.LinkedDocuments)
        ? doc.LinkedDocuments
        : (Array.isArray(doc.generatedDocuments)
          ? doc.generatedDocuments
          : []));

    const linkedDocuments = sourceLinked
      .map((linked: any) => ({
        type: String(linked?.type ?? linked?.Type ?? linked?.documentType ?? '').trim(),
        id: Number(linked?.id ?? linked?.Id ?? linked?.docEntry ?? linked?.DocEntry ?? 0),
        docNum: String(linked?.docNum ?? linked?.DocNum ?? linked?.documentNumber ?? '').trim() || undefined,
        status: String(linked?.status ?? linked?.Status ?? linked?.documentStatus ?? '').trim() || undefined
      }))
      .filter((linked: any) => linked.type !== '' && linked.id > 0);

    const sourceRaw = doc.sourceDocument ?? doc.SourceDocument ?? doc.baseDocument ?? doc.BaseDocument;
    const sourceDocument = sourceRaw
      ? {
          type: String(sourceRaw?.type ?? sourceRaw?.Type ?? sourceRaw?.documentType ?? '').trim(),
          id: Number(sourceRaw?.id ?? sourceRaw?.Id ?? sourceRaw?.docEntry ?? sourceRaw?.DocEntry ?? 0),
          docNum: String(sourceRaw?.docNum ?? sourceRaw?.DocNum ?? sourceRaw?.documentNumber ?? '').trim() || undefined,
          status: String(sourceRaw?.status ?? sourceRaw?.Status ?? sourceRaw?.documentStatus ?? '').trim() || undefined
        }
      : undefined;

    return {
      id,
      docNum: String(doc.docNum ?? doc.DocNum ?? doc.documentNumber ?? id ?? ''),
      customerId: Number(doc.customerId ?? 0),
      cardCode: doc.cardCode ?? doc.CardCode ?? '',
      customerName: doc.cardName ?? doc.CardName ?? doc.customerName ?? doc.CustomerName ?? '-',
      status: String(status),
      docDate: postingDate,
      postingDate,
      dueDate,
      currency: doc.currency ?? doc.DocCurrency ?? 'MAD',
      comments: doc.comments ?? doc.Comments,
      paymentMethod: doc.paymentMethod ?? doc.PaymentMethod,
      docTotal: total,
      totalAmount: total,
      lines: sourceLines.map((line: any, index: number) => this.normalizeLine(line, index)),
      linkedDocuments,
      sourceDocument: sourceDocument && sourceDocument.type !== '' && sourceDocument.id > 0 ? sourceDocument : undefined,
      quoteId: Number(doc.quoteId ?? doc.QuoteId ?? doc.baseQuoteId ?? 0) || undefined,
      orderId: Number(doc.orderId ?? doc.OrderId ?? doc.baseOrderId ?? 0) || undefined,
      deliveryNoteId: Number(doc.deliveryNoteId ?? doc.DeliveryNoteId ?? doc.deliveryId ?? doc.DeliveryId ?? 0) || undefined,
      invoiceId: Number(doc.invoiceId ?? doc.InvoiceId ?? 0) || undefined,
      returnId: Number(doc.returnId ?? doc.ReturnId ?? 0) || undefined,
      creditNoteId: Number(doc.creditNoteId ?? doc.CreditNoteId ?? 0) || undefined
    };
  }

  private normalizeLine(line: any, index: number) {
    const rawLineStatus = String(
      line?.lineStatus
      ?? line?.LineStatus
      ?? line?.documentLineStatus
      ?? line?.DocumentLineStatus
      ?? ''
    ).trim();
    const compactLineStatus = rawLineStatus.toLowerCase().replace(/[\s_-]/g, '');
    const lineStatus = compactLineStatus === 'c'
      || compactLineStatus.includes('close')
      || compactLineStatus === 'bostclose'
      ? 'Closed'
      : 'Open';

    return {
      id: Number(line?.id ?? line?.Id ?? line?.lineNum ?? line?.LineNum ?? index),
      lineNum: Number(line?.lineNum ?? line?.LineNum ?? index),
      itemCode: line?.itemCode ?? line?.ItemCode ?? line?.itemNo ?? line?.ItemNo ?? '',
      itemName: line?.itemName ?? line?.ItemName ?? line?.ItemDescription ?? line?.Dscription ?? '',
      warehouseCode: line?.warehouseCode ?? line?.WarehouseCode ?? line?.whsCode ?? line?.WhsCode ?? '',
      quantity: Number(line?.quantity ?? line?.Quantity ?? line?.qty ?? line?.Qty ?? 0),
      unitPrice: Number(line?.unitPrice ?? line?.UnitPrice ?? line?.Price ?? line?.price ?? line?.UnitPriceAfVAT ?? 0),
      vatPct: Number(line?.vatPct ?? line?.VatPercent ?? line?.TaxRate ?? 0),
      lineTotal: Number(line?.lineTotal ?? line?.LineTotal ?? line?.total ?? line?.Total ?? 0),
      lineStatus
    };
  }

  private normalizeStatus(value: unknown): string {
    const raw = String(value ?? '').trim();
    if (!raw) return 'Open';

    const lower = raw.toLowerCase();
    const compact = lower.replace(/[\s_-]/g, '');

    // Business rule: status phase is derived only from SAP DocStatus.
    if (
      lower === 'o'
      || lower === 'open'
      || compact === 'bostopen'
      || (compact.includes('open') && !compact.includes('close'))
    ) {
      return 'Open';
    }

    if (
      lower === 'c'
      || lower === 'closed'
      || lower === 'close'
      || compact === 'bostclose'
      || compact === 'bostclosed'
      || compact.includes('close')
      || compact.includes('cancel')
    ) {
      return 'Closed';
    }

    return raw;
  }
}
