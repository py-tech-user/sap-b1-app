import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResponse,
  CommercialDocument,
  CommercialListFilters,
  SaveCommercialDocumentDto,
  PagedResult,
  InvoicePaymentDto
} from '../models/models';
import { CommercialApiService } from './commercial-api.service';

@Injectable({ providedIn: 'root' })
export class InvoiceApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getList(filters: CommercialListFilters): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    return this.commercialApi.getList('invoices', filters);
  }

  getById(id: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.getById('invoices', id);
  }

  create(dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.create('invoices', dto);
  }

  update(id: number, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.update('invoices', id, dto);
  }

  updateStatus(id: number, status: string): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.updateStatus('invoices', id, status);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.commercialApi.delete('invoices', id);
  }

  fromDeliveryNote(deliveryNoteId: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.generateInvoiceFromDelivery(deliveryNoteId);
  }

  addPayment(invoiceId: number, dto: InvoicePaymentDto): Observable<ApiResponse<unknown>> {
    return this.commercialApi.addInvoicePayment(invoiceId, dto);
  }
}
