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
export class CreditNoteApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getList(filters: CommercialListFilters): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    return this.commercialApi.getList('creditnotes', filters);
  }

  getById(id: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.getById('creditnotes', id);
  }

  create(dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.create('creditnotes', dto);
  }

  update(id: number, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.update('creditnotes', id, dto);
  }

  updateStatus(id: number, status: string): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.updateStatus('creditnotes', id, status);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.commercialApi.delete('creditnotes', id);
  }
}
