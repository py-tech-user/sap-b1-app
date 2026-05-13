import { Injectable } from '@angular/core';

export interface CatalogCartLine {
  itemCode: string;
  itemName: string;
  quantity: number;
  unitPrice: number;
  warehouseCode?: string;
}

@Injectable({ providedIn: 'root' })
export class CatalogCartService {
  private readonly storageKey = 'catalog-cart-lines-v1';

  getLines(): CatalogCartLine[] {
    if (typeof window === 'undefined') return [];
    try {
      const raw = window.sessionStorage.getItem(this.storageKey);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      return parsed
        .map((line) => this.normalizeLine(line))
        .filter((line): line is CatalogCartLine => line !== null);
    } catch {
      return [];
    }
  }

  addLine(input: CatalogCartLine): void {
    const line = this.normalizeLine(input);
    if (!line) return;

    const existing = this.getLines();
    const key = line.itemCode.trim().toLowerCase();
    const index = existing.findIndex((l) => l.itemCode.trim().toLowerCase() === key);
    if (index >= 0) {
      const updatedQty = Math.max(1, Number(existing[index].quantity ?? 1)) + line.quantity;
      existing[index] = { ...existing[index], quantity: updatedQty };
    } else {
      existing.push(line);
    }
    this.save(existing);
  }

  updateQuantity(itemCode: string, quantity: number): void {
    const code = String(itemCode ?? '').trim().toLowerCase();
    if (!code) return;
    const lines = this.getLines();
    const index = lines.findIndex((l) => l.itemCode.trim().toLowerCase() === code);
    if (index < 0) return;

    const normalizedQty = Math.max(1, Math.floor(Number(quantity) || 1));
    lines[index] = { ...lines[index], quantity: normalizedQty };
    this.save(lines);
  }

  removeLine(itemCode: string): void {
    const code = String(itemCode ?? '').trim().toLowerCase();
    if (!code) return;
    const next = this.getLines().filter((line) => line.itemCode.trim().toLowerCase() !== code);
    this.save(next);
  }

  clear(): void {
    if (typeof window === 'undefined') return;
    window.sessionStorage.removeItem(this.storageKey);
  }

  private save(lines: CatalogCartLine[]): void {
    if (typeof window === 'undefined') return;
    window.sessionStorage.setItem(this.storageKey, JSON.stringify(lines));
  }

  private normalizeLine(raw: any): CatalogCartLine | null {
    const itemCode = String(raw?.itemCode ?? '').trim();
    const itemName = String(raw?.itemName ?? '').trim();
    if (!itemCode || !itemName) return null;

    const quantity = Math.max(1, Math.floor(Number(raw?.quantity) || 1));
    const unitPrice = Math.max(0, Number(raw?.unitPrice ?? 0));
    const warehouseCode = String(raw?.warehouseCode ?? '').trim() || undefined;

    return { itemCode, itemName, quantity, unitPrice, warehouseCode };
  }
}

