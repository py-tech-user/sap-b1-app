import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="product-form">
      <h1>{{ isEdit ? 'Modifier le produit' : 'Nouveau produit' }}</h1>

      @if (successMsg) {
        <div class="alert alert-success">✅ {{ successMsg }}</div>
      }
      @if (errorMsg) {
        <div class="alert alert-error">❌ {{ errorMsg }}</div>
      }

      <form (ngSubmit)="onSubmit()">
        <div class="form-row">
          <div class="form-group">
            <label>Code produit</label>
            <input [(ngModel)]="product.itemCode" name="itemCode" required [disabled]="isEdit" />
          </div>
          <div class="form-group">
            <label>Nom</label>
            <input [(ngModel)]="product.itemName" name="itemName" required />
          </div>
        </div>
        
        <div class="form-row">
          <div class="form-group">
            <label>Prix</label>
            <input type="number" [(ngModel)]="product.price" name="price" step="0.01" required />
          </div>
          <div class="form-group">
            <label>Stock</label>
            <input type="number" [(ngModel)]="product.stock" name="stock" />
          </div>
        </div>
        
        <div class="form-row">
          <div class="form-group">
            <label>Catégorie</label>
            <input [(ngModel)]="product.category" name="category" />
          </div>
          <div class="form-group">
            <label>Unité</label>
            <input [(ngModel)]="product.unit" name="unit" placeholder="ex: pièce, kg, litre" />
          </div>
        </div>
        
        <div class="form-group">
          <label>
            <input type="checkbox" [(ngModel)]="product.isActive" name="isActive" />
            Produit actif
          </label>
        </div>
        
        <div class="actions">
          <a routerLink="/products" class="btn-cancel">Annuler</a>
          <button type="submit" class="btn-submit" [disabled]="loading">
            {{ loading ? 'Enregistrement...' : 'Enregistrer' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .product-form { max-width: 800px; background: white; padding: 2rem; border-radius: 8px; }
    h1 { margin-bottom: 1rem; }
    .alert { padding: 0.75rem 1rem; border-radius: 6px; margin-bottom: 1rem; font-size: 0.9rem; }
    .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
    .alert-error   { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form-group { margin-bottom: 1rem; }
    label { display: block; margin-bottom: 0.5rem; color: #555; }
    input { width: 100%; padding: 0.75rem; border: 1px solid #ddd; border-radius: 4px; box-sizing: border-box; }
    input:focus { outline: none; border-color: #667eea; }
    .actions { display: flex; gap: 1rem; margin-top: 2rem; }
    .btn-cancel { padding: 0.75rem 1.5rem; background: #eee; border-radius: 4px; text-decoration: none; color: #333; }
    .btn-submit { padding: 0.75rem 1.5rem; background: #667eea; color: white; border: none; border-radius: 4px; cursor: pointer; }
  `]
})
export class ProductFormComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  product: any = { itemCode: '', itemName: '', price: 0, stock: 0, category: '', unit: '', isActive: true };
  isEdit = false;
  loading = false;
  successMsg = '';
  errorMsg = '';

  constructor(private http: HttpClient, private router: Router, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    if (id) {
      this.isEdit = true;
      this.http.get<any>(`${environment.apiUrl}/products/${id}`).subscribe({
        next: (res) => {
          this.product = res.data ?? res;
          this.cdr.markForCheck();
        },
        error: () => { this.errorMsg = 'Impossible de charger le produit.'; this.cdr.markForCheck(); }
      });
    }
  }

  onSubmit(): void {
    this.loading = true;
    this.successMsg = '';
    this.errorMsg = '';

    const req = this.isEdit
      ? this.http.put<any>(`${environment.apiUrl}/products/${this.product.id}`, this.product)
      : this.http.post<any>(`${environment.apiUrl}/products`, this.product);

    req.subscribe({
      next: (res) => {
        if (res.success === false) {
          this.errorMsg = res.message || 'Erreur lors de l\'opération.';
          this.loading = false;
          this.cdr.markForCheck();
          return;
        }
        this.successMsg = res.message
          || (this.isEdit ? 'Produit modifié avec succès.' : 'Produit créé avec succès.');
        this.loading = false;
        this.cdr.markForCheck();
        setTimeout(() => this.router.navigate(['/products']), 1200);
      },
      error: (err) => {
        console.error('Erreur produit:', err);
        if (err.status === 0) {
          this.errorMsg = 'Impossible de contacter le serveur. Vérifiez que le backend est démarré.';
        } else {
          this.errorMsg = err.error?.message || `Erreur ${err.status} lors de ${this.isEdit ? 'la modification' : 'la création'}.`;
        }
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
