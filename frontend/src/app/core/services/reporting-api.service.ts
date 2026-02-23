import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  AdvancedDashboard,
  DailyKpis,
  TopCustomer,
  TopProduct,
  RevenueEvolution,
  PendingPayment,
  LateOrder
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class ReportingApiService {
  private readonly api = `${environment.apiUrl}/reporting`;

  constructor(private http: HttpClient) {}

  /** GET /reporting/dashboard — Dashboard complet */
  getDashboard(): Observable<ApiResponse<AdvancedDashboard>> {
    return this.http.get<ApiResponse<AdvancedDashboard>>(`${this.api}/dashboard`);
  }

  /** GET /reporting/kpis — KPIs simplifiés du jour */
  getKpis(): Observable<ApiResponse<DailyKpis>> {
    return this.http.get<ApiResponse<DailyKpis>>(`${this.api}/kpis`);
  }

  /** GET /reporting/top-customers */
  getTopCustomers(limit = 10): Observable<ApiResponse<TopCustomer[]>> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<ApiResponse<TopCustomer[]>>(`${this.api}/top-customers`, { params });
  }

  /** GET /reporting/top-products */
  getTopProducts(limit = 10): Observable<ApiResponse<TopProduct[]>> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<ApiResponse<TopProduct[]>>(`${this.api}/top-products`, { params });
  }

  /** GET /reporting/revenue/monthly */
  getRevenueMonthly(months = 12): Observable<ApiResponse<RevenueEvolution[]>> {
    const params = new HttpParams().set('months', months.toString());
    return this.http.get<ApiResponse<RevenueEvolution[]>>(`${this.api}/revenue/monthly`, { params });
  }

  /** GET /reporting/revenue/daily */
  getRevenueDaily(days = 30): Observable<ApiResponse<RevenueEvolution[]>> {
    const params = new HttpParams().set('days', days.toString());
    return this.http.get<ApiResponse<RevenueEvolution[]>>(`${this.api}/revenue/daily`, { params });
  }

  /** GET /reporting/pending-payments */
  getPendingPayments(): Observable<ApiResponse<PendingPayment[]>> {
    return this.http.get<ApiResponse<PendingPayment[]>>(`${this.api}/pending-payments`);
  }

  /** GET /reporting/late-orders */
  getLateOrders(daysThreshold = 7): Observable<ApiResponse<LateOrder[]>> {
    const params = new HttpParams().set('daysThreshold', daysThreshold.toString());
    return this.http.get<ApiResponse<LateOrder[]>>(`${this.api}/late-orders`, { params });
  }
}
