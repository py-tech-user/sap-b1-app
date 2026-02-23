import { Component, OnInit, signal, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SupplierApiService } from '../../../core/services/supplier-api.service';

@Component({
  selector: 'app-supplier-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <div class="header">
        <h1>🏭 {{ isEdit ? 'Modifier' : 'Nouveau' }} fournisseur</h1>
        <a routerLink="/suppliers" class="btn-back">Annuler</a>
      </div>

      <form (ngSubmit)="submit()" class="form">
        <div class="row">
          <div class="form-group">
            <label>Code fournisseur</label>
            <input type="text" [(ngModel)]="form.supplierCode" name="supplierCode" required placeholder="FRN-001" />
          </div>
          <div class="form-group">
            <label>Nom</label>
            <input type="text" [(ngModel)]="form.supplierName" name="supplierName" required placeholder="Nom du fournisseur" />
          </div>
        </div>
        <div class="row">
          <div class="form-group">
            <label>Personne de contact</label>
            <input type="text" [(ngModel)]="form.contactPerson" name="contactPerson" placeholder="Nom du contact" />
          </div>
          <div class="form-group">
            <label>Téléphone</label>
            <input type="text" [(ngModel)]="form.phone" name="phone" placeholder="+212 6..." />
          </div>
        </div>
        <div class="row">
          <div class="form-group">
            <label>Email</label>
            <input type="email" [(ngModel)]="form.email" name="email" placeholder="contact&#64;fournisseur.com" />
          </div>
          <div class="form-group">
            <label>Ville</label>
            <input type="text" [(ngModel)]="form.city" name="city" placeholder="Casablanca" />
          </div>
        </div>
        <div class="form-group">
          <label>Adresse</label>
          <textarea [(ngModel)]="form.address" name="address" rows="2" placeholder="Adresse complète"></textarea>
        </div>
        <div class="row">
          <div class="form-group">
            <label>Conditions de paiement</label>
            <input type="text" [(ngModel)]="form.paymentTerms" name="paymentTerms" placeholder="30 jours" />
          </div>
          <div class="form-group">
            <label>Devise</label>
            <input type="text" [(ngModel)]="form.currency" name="currency" placeholder="MAD" />
          </div>
        </div>

        <div class="form-actions">
          <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Enregistrement...' : (isEdit ? 'Mettre à jour' : 'Créer') }}</button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .page { background: white; padding: 2rem; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }
    .header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 2rem; }
    .btn-back { padding: .5rem 1rem; background: #eee; border-radius: 6px; text-decoration: none; color: #333; }
    .form { max-width: 700px; }
    .row { display: flex; gap: 1rem; }
    .row .form-group { flex: 1; }
    .form-group { margin-bottom: 1.2rem; }
    .form-group label { display: block; font-weight: 600; margin-bottom: .4rem; color: #555; font-size: .9rem; }
    .form-group input, .form-group textarea { width: 100%; padding: .6rem; border: 1px solid #ddd; border-radius: 6px; font-size: .95rem; }
    .form-actions { margin-top: 2rem; }
    .btn-primary { background: #667eea; color: white; border: none; padding: .7rem 1.5rem; border-radius: 6px; cursor: pointer; font-weight: 600; font-size: 1rem; }
    .btn-primary:disabled { opacity: .5; }
  `]
})
export class SupplierFormComponent implements OnInit {
  private cdr = inject(ChangeDetectorRef);
  saving = signal(false);
  isEdit = false;
  editId = 0;
  form: any = { supplierCode: '', supplierName: '', contactPerson: '', phone: '', email: '', address: '', city: '', paymentTerms: '', currency: 'MAD' };

  constructor(private api: SupplierApiService, private router: Router, private route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    if (id) {
      this.isEdit = true;
      this.editId = +id;
      this.api.getById(this.editId).subscribe({
        next: (res) => { this.form = { ...(res.data ?? res) }; this.cdr.markForCheck(); }
      });
    }
  }

  submit(): void {
    this.saving.set(true);
    const obs = this.isEdit ? this.api.update(this.editId, this.form) : this.api.create(this.form);
    obs.subscribe({
      next: () => this.router.navigate(['/suppliers']),
      error: () => this.saving.set(false)
    });
  }
}
