import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApiResponse,
  CommercialDocument,
  CommercialListFilters,
  SaveCommercialDocumentDto,
  PagedResult
} from '../models/models';
import { CommercialApiService } from './commercial-api.service';

@Injectable({ providedIn: 'root' })
export class DeliveryNoteApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getList(filters: CommercialListFilters): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    return this.commercialApi.getList('deliverynotes', filters);
  }

  getById(id: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.getById('deliverynotes', id);
  }

  create(dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.create('deliverynotes', dto);
  }

  update(id: number, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.update('deliverynotes', id, dto);
  }

  updateStatus(id: number, status: string): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.updateStatus('deliverynotes', id, status);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.commercialApi.delete('deliverynotes', id);
  }

  fromOrder(orderId: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.generateDeliveryNoteFromOrder(orderId);
  }
}
