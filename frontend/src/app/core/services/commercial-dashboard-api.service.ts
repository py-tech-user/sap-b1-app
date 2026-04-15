import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { ApiResponse, CommercialDashboard } from '../models/models';
import { CommercialApiService } from './commercial-api.service';

@Injectable({ providedIn: 'root' })
export class CommercialDashboardApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getDashboard(): Observable<ApiResponse<CommercialDashboard>> {
    return this.commercialApi.getCommercialDashboard().pipe(
      catchError(() => this.buildFallbackDashboard())
    );
  }

  private buildFallbackDashboard(): Observable<ApiResponse<CommercialDashboard>> {
    return forkJoin({
      quotes: this.commercialApi.getList('quotes', { page: 1, pageSize: 200 }).pipe(map((r) => r.data?.items ?? []), catchError(() => of([]))),
      orders: this.commercialApi.getList('orders', { page: 1, pageSize: 200 }).pipe(map((r) => r.data?.items ?? []), catchError(() => of([]))),
      delivery: this.commercialApi.getList('deliverynotes', { page: 1, pageSize: 200 }).pipe(map((r) => r.data?.items ?? []), catchError(() => of([]))),
      invoices: this.commercialApi.getList('invoices', { page: 1, pageSize: 200 }).pipe(map((r) => r.data?.items ?? []), catchError(() => of([]))),
      creditNotes: this.commercialApi.getList('creditnotes', { page: 1, pageSize: 200 }).pipe(map((r) => r.data?.items ?? []), catchError(() => of([]))),
      returns: this.commercialApi.getList('returns', { page: 1, pageSize: 200 }).pipe(map((r) => r.data?.items ?? []), catchError(() => of([])))
    }).pipe(
      map((sets) => ({
        success: true,
        data: {
          pendingQuotes: sets.quotes.length,
          ordersInPreparation: sets.orders.length,
          deliveryInProgress: sets.delivery.length,
          unpaidInvoices: sets.invoices.length,
          pendingReturns: sets.returns.length,
          totalCreditNotes: sets.creditNotes.length,
          amounts: {
            quotes: this.sumTotals(sets.quotes),
            orders: this.sumTotals(sets.orders),
            deliveryNotes: this.sumTotals(sets.delivery),
            invoices: this.sumTotals(sets.invoices),
            creditNotes: this.sumTotals(sets.creditNotes),
            returns: this.sumTotals(sets.returns),
            unpaidInvoices: this.sumTotals(sets.invoices)
          }
        }
      }))
    );
  }

  private sumTotals(items: Array<{ docTotal?: number; totalAmount?: number }>): number {
    return items.reduce((acc, item) => acc + Number(item.docTotal ?? item.totalAmount ?? 0), 0);
  }
}
