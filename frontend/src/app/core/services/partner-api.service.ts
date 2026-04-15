import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, switchMap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PartnerRow {
  CardCode: string;
  CardName: string;
  CardType?: string;
  Currency?: string;
  Phone1?: string;
  Phone2?: string;
  Cellular?: string;
  MobilePhone?: string;
  EmailAddress?: string;
  E_Mail?: string;
  E_MailL?: string;
  ContactEmployees?: Array<{
    E_Mail?: string;
    E_MailL?: string;
    Email?: string;
    Phone1?: string;
    Phone2?: string;
    MobilePhone?: string;
    Cellular?: string;
  }>;
  [key: string]: unknown;
}

export interface PartnerListResult {
  items: PartnerRow[];
  totalCount: number;
}

@Injectable({ providedIn: 'root' })
export class PartnerApiService {
  private readonly http = inject(HttpClient);
  private readonly endpoint = `${environment.apiUrl}/sap/partners`;
  private readonly clientsEndpoint = `${environment.apiUrl}/sap/clients`;

  getAll(page = 1, pageSize = 15): Observable<PartnerListResult> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));

    return this.http.get<any>(this.endpoint, { params }).pipe(
      map((res) => {
        const items = this.extractRows(res);
        const totalCount = Number(
          res?.totalCount ?? res?.TotalCount ??
          res?.data?.totalCount ?? res?.data?.TotalCount ??
          res?.Data?.totalCount ?? res?.Data?.TotalCount ??
          items.length
        );
        return {
          items,
          totalCount: Number.isFinite(totalCount) && totalCount > 0 ? totalCount : items.length
        };
      }),
      switchMap((result) => {
        if (result.items.length > 0) return of(result);
        return this.http.get<any>(this.clientsEndpoint, { params }).pipe(
          map((res) => {
            const items = this.extractRows(res);
            const totalCount = Number(
              res?.totalCount ?? res?.TotalCount ??
              res?.data?.totalCount ?? res?.data?.TotalCount ??
              res?.Data?.totalCount ?? res?.Data?.TotalCount ??
              items.length
            );
            return { items, totalCount: Number.isFinite(totalCount) && totalCount > 0 ? totalCount : items.length };
          })
        );
      }),
      catchError(() => this.http.get<any>(this.clientsEndpoint, { params }).pipe(
        map((res) => {
          const items = this.extractRows(res);
          const totalCount = Number(
            res?.totalCount ?? res?.TotalCount ??
            res?.data?.totalCount ?? res?.data?.TotalCount ??
            res?.Data?.totalCount ?? res?.Data?.TotalCount ??
            items.length
          );
          return { items, totalCount: Number.isFinite(totalCount) && totalCount > 0 ? totalCount : items.length };
        }),
        catchError(() => of({ items: [], totalCount: 0 }))
      ))
    );
  }

  private extractRows(res: any): PartnerRow[] {
    if (!res) return [];
    if (Array.isArray(res)) return res.map((row) => this.normalizePartnerRow(row));
    if (Array.isArray(res.value)) return res.value.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Value)) return res.Value.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.data)) return res.data.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Data)) return res.Data.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.data?.value)) return res.data.value.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Data?.value)) return res.Data.value.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Data?.Value)) return res.Data.Value.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.data?.items)) return res.data.items.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Data?.items)) return res.Data.items.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Data?.Items)) return res.Data.Items.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.data?.data?.items)) return res.data.data.items.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.data?.data?.value)) return res.data.data.value.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.result)) return res.result.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.result?.items)) return res.result.items.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.items)) return res.items.map((row: unknown) => this.normalizePartnerRow(row));
    if (Array.isArray(res.Items)) return res.Items.map((row: unknown) => this.normalizePartnerRow(row));
    return [];
  }

  private normalizePartnerRow(raw: unknown): PartnerRow {
    const row = (raw ?? {}) as PartnerRow;

    const clean = (value: unknown): string => String(value ?? '').trim();

    const pick = (...values: Array<unknown>): string | undefined => {
      for (const value of values) {
        const text = clean(value);
        if (text && text !== '-' && text.toLowerCase() !== 'null' && text.toLowerCase() !== 'undefined') {
          return text;
        }
      }
      return undefined;
    };

    const contact = Array.isArray(row.ContactEmployees) ? row.ContactEmployees[0] : undefined;
    const normalizedEmail = pick(
      row.EmailAddress,
      row.E_Mail,
      row.E_MailL,
      (row as any).emailAddress,
      (row as any).email,
      (row as any).Email,
      contact?.E_Mail,
      contact?.E_MailL,
      contact?.Email
    );
    const normalizedPhone = pick(
      row.Cellular,
      row.MobilePhone,
      row.Phone1,
      row.Phone2,
      (row as any).cellular,
      (row as any).phone,
      (row as any).phone1,
      contact?.MobilePhone,
      contact?.Cellular,
      contact?.Phone1,
      contact?.Phone2
    );

    return {
      ...row,
      EmailAddress: normalizedEmail,
      Cellular: normalizedPhone,
      Phone1: row.Phone1 ?? row.Phone2 ?? normalizedPhone
    };
  }
}
