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
export class QuotesApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getList(filters: CommercialListFilters): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    return this.commercialApi.getList('quotes', filters);
  }

  getById(id: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.getById('quotes', id);
  }

  create(dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.create('quotes', dto);
  }

  update(id: number, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.update('quotes', id, dto);
  }

  updateStatus(id: number, status: string): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.updateStatus('quotes', id, status);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.commercialApi.delete('quotes', id);
  }
}
