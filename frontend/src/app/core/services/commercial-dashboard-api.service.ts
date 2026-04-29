import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiResponse, CommercialDashboard } from '../models/models';
import { CommercialApiService } from './commercial-api.service';

@Injectable({ providedIn: 'root' })
export class CommercialDashboardApiService {
  private readonly commercialApi = inject(CommercialApiService);

  getDashboard(): Observable<ApiResponse<CommercialDashboard>> {
    return this.commercialApi.getCommercialDashboard();
  }
}


