import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Product {
  id: number;
  itemCode: string;
  itemName: string;
  price: number;
  category?: string;
  stock: number;
  unit?: string;
  isActive: boolean;
}

export interface CreateProductDto {
  itemCode: string;
  itemName: string;
  price: number;
  category?: string;
  stock: number;
  unit?: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private readonly apiUrl = `${environment.apiUrl}/products`;

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 50): Observable<{ items: Product[]; totalCount: number }> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());
    return this.http.get<{ items: Product[]; totalCount: number }>(this.apiUrl, { params });
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/${id}`);
  }

  create(product: CreateProductDto): Observable<Product> {
    return this.http.post<Product>(this.apiUrl, product);
  }

  update(id: number, product: CreateProductDto): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, product);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
