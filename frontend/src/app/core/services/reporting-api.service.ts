import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SapApiService } from './sap-api.service';

export interface ReportingKpis {
  quotesCount: number;
  quotesAmount: number;
  ordersCount: number;
  ordersAmount: number;
  deliveryNotesCount: number;
  deliveryNotesAmount: number;
  invoicesCount: number;
  invoicesAmount: number;
  unpaidInvoicesCount: number;
  unpaidInvoicesAmount: number;
  quoteToOrderRate: number;
  orderToDeliveryRate: number;
  deliveryToInvoiceRate: number;
  conversionRate: number;
  creditNotesAmount: number;
  netRevenue: number;
  pendingRevenue: number;
  activePartnersCount: number;
  inactivePartnersCount: number;
}

export interface ReportingSalesPerson {
  salesPersonCode: number;
  salesPersonName: string;
  quotesCount: number;
  quotesAmount: number;
  ordersCount: number;
  ordersAmount: number;
  deliveryNotesCount: number;
  deliveryNotesAmount: number;
  invoicesCount: number;
  invoicesAmount: number;
  creditNotesCount: number;
  creditNotesAmount: number;
  netRevenue: number;
  pendingRevenue: number;
  unpaidInvoicesCount: number;
  unpaidInvoicesAmount: number;
  quoteToOrderRate: number;
  orderToDeliveryRate: number;
  deliveryToInvoiceRate: number;
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
  status: string;
}

export interface PartnerActivityItem {
  cardCode: string;
  cardName: string;
  salesPersonCode: number;
  quotesCount: number;
  quotesAmount: number;
  ordersCount: number;
  ordersAmount: number;
  deliveryNotesCount: number;
  deliveryNotesAmount: number;
  invoicesCount: number;
  invoicesAmount: number;
  creditNotesCount: number;
  creditNotesAmount: number;
  netRevenue: number;
  isActive: boolean;
}

export interface PartnerDebtItem {
  cardCode: string;
  cardName: string;
  salesPersonCode: number;
  salesPersonName: string;
  partnerOwesCompanyAmount: number;
  companyOwesPartnerAmount: number;
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

export interface AdvancedReportingMonthlyRevenuePoint {
  monthKey: string;
  revenue: number;
}

export interface AdvancedReportingTopProduct {
  itemCode: string;
  itemName: string;
  quantitySold: number;
  revenue: number;
  salesCount: number;
  clientsCount: number;
  salesPeopleCount: number;
  mainClientName: string;
}

export interface AdvancedReportingTopClient {
  cardCode: string;
  cardName: string;
  revenue: number;
  paidAmount: number;
  pendingAmount: number;
  contractsCount: number;
  productsCount: number;
  mainSalesPersonName: string;
}

export interface AdvancedReportingTopPartner {
  partnerCode: string;
  partnerName: string;
  revenue: number;
  quotesCount: number;
  productsCount: number;
  salesPeopleCount: number;
}

export interface AdvancedReportingUnpaid {
  cardCode: string;
  cardName: string;
  itemCode: string;
  itemName: string;
  dueAmount: number;
  salesPersonCode: number;
  salesPersonName: string;
  dueDate: string;
  overdueDays: number;
}

export interface AdvancedReportingProductDetail {
  itemCode: string;
  itemName: string;
  quantitySold: number;
  revenue: number;
  clientsCount: number;
  mainClientName: string;
  trendPercent: number;
}

export interface AdvancedReportingClientDetail {
  cardCode: string;
  cardName: string;
  revenue: number;
  paidAmount: number;
  pendingAmount: number;
  paymentRate: number;
  contractsCount: number;
  productsCount: number;
  favoriteItemName: string;
}

export interface AdvancedReportingPartnerDetail {
  partnerCode: string;
  partnerName: string;
  revenue: number;
  quotesCount: number;
  productsCount: number;
  salesPeopleCount: number;
  trendPercent: number;
  roiPercent: number;
}

export interface AdvancedReportingPayload {
  mode: 'Commercial' | 'Admin';
  periodType: 'month' | 'quarter' | 'year' | 'custom';
  periodLabel: string;
  periodStart: string;
  periodEnd: string;
  selectedSalesPersonCode?: number;
  selectedSalesPersonName?: string;
  kpis: ReportingKpis;
  previousKpis: ReportingKpis;
  teamMembers: ReportingSalesPersonInfo[];
  monthlyRevenue: AdvancedReportingMonthlyRevenuePoint[];
  monthlyRevenuePreviousYear: AdvancedReportingMonthlyRevenuePoint[];
  topProducts: AdvancedReportingTopProduct[];
  topClients: AdvancedReportingTopClient[];
  topPartners: AdvancedReportingTopPartner[];
  topCommercials: ReportingSalesPerson[];
  unpaidItems: AdvancedReportingUnpaid[];
  productDetails: AdvancedReportingProductDetail[];
  clientDetails: AdvancedReportingClientDetail[];
  partnerDetails: AdvancedReportingPartnerDetail[];
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

  getPartnersActivity(
    month: string,
    activity: 'all' | 'active' | 'inactive',
    search?: string,
    salesPersonCode?: number,
    startDate?: string,
    endDate?: string
  ): Observable<ApiResponse<PartnerActivityItem[]>> {
    const query = new URLSearchParams();
    if (startDate && endDate) {
      query.set('startDate', startDate);
      query.set('endDate', endDate);
    } else {
      query.set('month', month);
    }
    query.set('activity', activity);
    if (search && search.trim() !== '') query.set('search', search.trim());
    if (salesPersonCode && salesPersonCode > 0) query.set('salesPersonCode', String(salesPersonCode));
    return this.api.get<ApiResponse<PartnerActivityItem[]>>(`reporting/partners-activity?${query.toString()}`);
  }

  getAdvancedReporting(params: {
    periodType: 'month' | 'quarter' | 'year' | 'custom';
    month?: string;
    quarter?: number;
    year?: number;
    startDate?: string;
    endDate?: string;
    salesPersonCode?: number;
    itemCode?: string;
    cardCode?: string;
  }): Observable<ApiResponse<AdvancedReportingPayload>> {
    const query = new URLSearchParams();
    query.set('periodType', params.periodType);
    if (params.month) query.set('month', params.month);
    if (params.quarter) query.set('quarter', String(params.quarter));
    if (params.year) query.set('year', String(params.year));
    if (params.startDate) query.set('startDate', params.startDate);
    if (params.endDate) query.set('endDate', params.endDate);
    if (params.salesPersonCode && params.salesPersonCode > 0) query.set('salesPersonCode', String(params.salesPersonCode));
    if (params.itemCode && params.itemCode.trim()) query.set('itemCode', params.itemCode.trim());
    if (params.cardCode && params.cardCode.trim()) query.set('cardCode', params.cardCode.trim());
    return this.api.get<ApiResponse<AdvancedReportingPayload>>(`reporting/advanced?${query.toString()}`);
  }

  getPartnerDebts(salesPersonCode?: number): Observable<ApiResponse<PartnerDebtItem[]>> {
    const query = new URLSearchParams();
    if (salesPersonCode && salesPersonCode > 0) query.set('salesPersonCode', String(salesPersonCode));
    return this.api.get<ApiResponse<PartnerDebtItem[]>>(`reporting/partner-debts?${query.toString()}`);
  }
}
