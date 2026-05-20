import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Product {
  id: number;
  itemCode: string;
  itemName: string;
  imageUrl?: string;
  price: number;
  category?: string;
  stock: number;
  unit?: string;
  isActive: boolean;
  warehouseCode?: string;
  groupCode?: number;
  groupName?: string;
}

export interface ProductGroup {
  groupCode: number;
  groupName: string;
  itemsCount: number;
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
  private readonly apiUrl = `${environment.apiUrl}/sap/items`;
  private readonly apiRoot = environment.apiUrl.replace(/\/api\/?$/i, '');

  constructor(private http: HttpClient) {}

  getAll(page = 1, pageSize = 50): Observable<{ items: Product[]; totalCount: number }> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<any>(this.apiUrl, { params }).pipe(
      map((res) => this.normalizeList(res, page, pageSize))
    );
  }

  getByGroup(groupCode: number): Observable<Product[]> {
    const params = new HttpParams().set('groupCode', String(groupCode));
    return this.http.get<any>(this.apiUrl, { params }).pipe(
      map((res) => this.normalizeList(res, 1, 0).items)
    );
  }

  getGroups(): Observable<ProductGroup[]> {
    return this.http.get<any>(`${environment.apiUrl}/sap/item-groups`).pipe(
      map((res) => {
        const rows = this.extractRows(res);
        return rows.map((row: any) => ({
          groupCode: Number(row?.groupCode ?? row?.GroupCode ?? 0),
          groupName: String(row?.groupName ?? row?.GroupName ?? '').trim() || 'Sans categorie',
          itemsCount: Number(row?.itemsCount ?? row?.ItemsCount ?? 0)
        }));
      })
    );
  }

  getById(id: number): Observable<Product> {
    return this.http.get<any>(this.apiUrl).pipe(
      map((res) => {
        const rows = this.extractRows(res);
        const normalized = rows.map((row, index) => this.normalizeProduct(row, index));
        return normalized.find((p) => p.id === id) ?? normalized[0];
      })
    );
  }

  create(product: CreateProductDto): Observable<Product> {
    return this.http.post<Product>(`${environment.apiUrl}/products`, product);
  }

  update(id: number, product: CreateProductDto): Observable<void> {
    return this.http.put<void>(`${environment.apiUrl}/products/${id}`, product);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/products/${id}`);
  }

  private normalizeList(res: any, page: number, pageSize: number): { items: Product[]; totalCount: number } {
    if (res?.success === false) {
      return { items: [], totalCount: 0 };
    }

    const rows = this.extractRows(res);
    const items = rows.map((row, index) => this.normalizeProduct(row, index));

    // Keep client-side pagination safe if backend ignores query params.
    const start = Math.max(0, (page - 1) * pageSize);
    const paged = items.slice(start, start + pageSize);
    return { items: pageSize > 0 ? paged : items, totalCount: items.length };
  }

  private extractRows(res: any): any[] {
    if (!res) return [];
    if (Array.isArray(res)) return res;
    if (Array.isArray(res.value)) return res.value;
    if (Array.isArray(res.data)) return res.data;
    if (Array.isArray(res.data?.items)) return res.data.items;
    if (Array.isArray(res.data?.value)) return res.data.value;
    if (Array.isArray(res.items)) return res.items;
    if (Array.isArray(res.result)) return res.result;
    return [];
  }

  private normalizeProduct(row: any, index: number): Product {
    const warehouse = row?.warehouseCode ?? row?.WarehouseCode ?? row?.whsCode ?? row?.WhsCode;
    const itemCode = String(row?.itemCode ?? row?.ItemCode ?? row?.code ?? '').trim();
    const price = Number(row?.price ?? row?.Price ?? row?.UnitPrice ?? row?.AvgPrice ?? 0);
    const rawImageUrl = String(
      row?.imageUrl
      ?? row?.ImageUrl
      ?? row?.pictureUrl
      ?? row?.PictureUrl
      ?? row?.photoUrl
      ?? row?.PhotoUrl
      ?? row?.Picture
      ?? row?.PicturName
      ?? row?.U_ImageUrl
      ?? row?.U_Image
      ?? row?.image
      ?? row?.Image
      ?? ''
    ).trim();
    const resolvedImageUrl = this.resolveImageUrl(rawImageUrl);

    return {
      id: Number(row?.id ?? row?.itemId ?? row?.ItemId ?? index + 1),
      itemCode,
      itemName: String(row?.itemName ?? row?.ItemName ?? row?.name ?? '').trim(),
      imageUrl: resolvedImageUrl,
      price,
      category: row?.category ?? row?.ItmsGrpNam ?? row?.ItemGroup,
      groupCode: Number(row?.groupCode ?? row?.GroupCode ?? row?.ItmsGrpCod ?? 0),
      groupName: String(row?.groupName ?? row?.GroupName ?? row?.ItmsGrpNam ?? row?.ItemGroup ?? '').trim() || undefined,
      stock: Number(row?.stock ?? row?.Stock ?? row?.stockTotal ?? row?.StockTotal ?? row?.OnHand ?? row?.InStock ?? 0),
      unit: row?.unit ?? row?.InventoryUOM ?? row?.UoM,
      isActive: String(row?.validFor ?? row?.ValidFor ?? 'Y').toUpperCase() !== 'N',
      warehouseCode: warehouse ? String(warehouse).trim() : undefined
    };
  }

  private resolveImageUrl(rawImageUrl: string): string | undefined {
    if (!rawImageUrl) return undefined;

    if (/^https?:\/\//i.test(rawImageUrl) || /^data:/i.test(rawImageUrl)) {
      return rawImageUrl;
    }

    if (rawImageUrl.startsWith('/')) {
      return `${this.apiRoot}${rawImageUrl}`;
    }

    if (/^api\//i.test(rawImageUrl)) {
      return `${this.apiRoot}/${rawImageUrl}`;
    }

    const normalizedPath = rawImageUrl.replace(/\\/g, '/');
    const fileName = normalizedPath.split('/').filter(Boolean).pop();
    if (!fileName) return undefined;

    return `${environment.apiUrl}/sap/item-images/${encodeURIComponent(fileName)}`;
  }
}


