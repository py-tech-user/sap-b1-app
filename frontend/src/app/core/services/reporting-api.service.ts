import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SapApiService } from './sap-api.service';

export interface ReportingKpis {
  quotesCount: number;
  quotesAmount: number;
  ordersCount: number;
  ordersAmount: number;
  invoicesCount: number;
  invoicesAmount: number;
  unpaidInvoicesCount: number;
  unpaidInvoicesAmount: number;
  conversionRate: number;
}

export interface ReportingSalesPerson {
  salesPersonCode: number;
  salesPersonName: string;
  quotesCount: number;
  quotesAmount: number;
  ordersCount: number;
  ordersAmount: number;
  invoicesCount: number;
  invoicesAmount: number;
  unpaidInvoicesCount: number;
  unpaidInvoicesAmount: number;
  conversionRate: number;
}

export interface ReportingRecentDocument {
  type: string;
  docEntry: number;
  docNum: number;
  cardCode: string;
  cardName: string;
  total: number;
  date?: string;
  salesPersonCode: number;
}

export interface ReportingSalesPersonInfo {
  salesPersonCode: number;
  salesPersonName: string;
}

export interface CommercialReportingPayload {
  mode: 'Commercial' | 'Admin';
  periodLabel: string;
  selectedSalesPersonCode?: number;
  selectedSalesPersonName?: string;
  kpis: ReportingKpis;
  teamPerformances: ReportingSalesPerson[];
  recentDocuments: ReportingRecentDocument[];
  teamMembers: ReportingSalesPersonInfo[];
  inactiveSalesPersons: ReportingSalesPersonInfo[];
  topSalesPerson?: ReportingSalesPerson;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string | null;
  data: T;
}

@Injectable({ providedIn: 'root' })
export class ReportingApiService {
  private readonly api = inject(SapApiService);

  getCommercialReporting(month: string, salesPersonCode?: number): Observable<ApiResponse<CommercialReportingPayload>> {
    const query = new URLSearchParams();
    query.set('month', month);
    if (salesPersonCode && salesPersonCode > 0) {
      query.set('salesPersonCode', String(salesPersonCode));
    }

    return this.api.get<ApiResponse<CommercialReportingPayload>>(`reporting/commercial?${query.toString()}`);
  }
}
