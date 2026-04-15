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
export class ReturnApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getList(filters: CommercialListFilters): Observable<ApiResponse<PagedResult<CommercialDocument>>> {
    return this.commercialApi.getList('returns', filters);
  }

  getById(id: number): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.getById('returns', id);
  }

  create(dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.create('returns', dto);
  }

  update(id: number, dto: SaveCommercialDocumentDto): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.update('returns', id, dto);
  }

  updateStatus(id: number, status: string): Observable<ApiResponse<CommercialDocument>> {
    return this.commercialApi.updateStatus('returns', id, status);
  }

  delete(id: number): Observable<ApiResponse<void>> {
    return this.commercialApi.delete('returns', id);
  }
}
