# Guide Frontend - Mode Hybride Invoices

## Résumé des changements côté Backend

### 1. **Lectures Factures (GET /api/sap/invoices)**
- **Source**: SQL uniquement (table OINV)
- **Portée**: Toutes les factures (Open + Closed)
- **Fallback**: ❌ AUCUN - Erreur 500 si SQL échoue

### 2. **Écritures Factures**
- **Création** (POST /api/sap/invoices): Via Service Layer
- **Suppression** (DELETE /api/sap/invoices/{docEntry}): Via Service Layer  
- **Paiements** (POST /api/sap/invoices/{docEntry}/payments): Via Service Layer

### 3. **Logging amélioré**
Tous les logs contiennent `[HYBRID-MODE]` pour traçabilité

---

## Gestion des erreurs Frontend

### ❌ Erreur HTTP 500
**Cause:** Configuration SQL manquante ou erreur SQL

**Réponse API:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Configuration SQL SAP manquante. Impossible de charger les factures. Veuillez contacter l'administrateur."
}
// OU
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Erreur lors du chargement des données."
}
```

**À afficher à l'utilisateur:**
```
⚠️  Erreur lors du chargement des données.

Veuillez contacter votre administrateur et vérifier:
- La connexion à la base de données SAP B1
- La configuration des paramètres SQL
```

### ✅ Succès
**Réponse API:**
```json
{
  "success": true,
  "message": null,
  "data": [
    { "docEntry": 1, "docNum": 1001, "status": "Open", ... },
    { "docEntry": 2, "docNum": 1002, "status": "Closed", ... }
  ],
  "count": 150
}
```

**À afficher:**
Afficher normalement la liste des factures avec tous les filtres appliqués.

---

## Composant Angular - Patterns à appliquer

### Pattern: Gestion d'erreur stricte

```typescript
// ❌ AVANT: Fallback silencieux
export class InvoiceListComponent {
  loadInvoices() {
    this.api.getInvoices().subscribe({
      next: (response) => {
        if (response.success) {
          this.invoices = response.data; // OK
        } else {
          // Avant: on pouvait afficher une liste vide ou partielle
          this.invoices = [];
        }
      },
      error: (err) => {
        // Avant: on pouvait essayer un fallback
        this.showWarning('Impossible de charger');
      }
    });
  }
}

// ✅ APRÈS: Erreur explicite
export class InvoiceListComponent implements OnInit {
  invoices: Invoice[] = [];
  isLoading = false;
  hasError = false;
  errorMessage = '';

  constructor(private api: CommercialApiService) {}

  ngOnInit() {
    this.loadInvoices();
  }

  loadInvoices() {
    this.isLoading = true;
    this.hasError = false;
    this.errorMessage = '';

    this.api.getInvoices().subscribe({
      next: (response) => {
        if (response.success) {
          this.invoices = response.data ?? [];
          // C'est OK même si [] - aucune facture ne correspond aux filtres
        } else {
          // Erreur API
          this.hasError = true;
          this.errorMessage = 'Erreur lors du chargement des données.';
        }
        this.isLoading = false;
      },
      error: (err) => {
        // Erreur HTTP ou réseau
        this.hasError = true;
        this.errorMessage = 'Erreur lors du chargement des données.';
        this.isLoading = false;
      }
    });
  }
}
```

### Template: Affichage des états

```html
<!-- État loading -->
<div *ngIf="isLoading" class="alert alert-info">
  <i class="spinner"></i> Chargement des factures...
</div>

<!-- État erreur -->
<div *ngIf="hasError && !isLoading" class="alert alert-danger">
  <strong>⚠️ Erreur</strong>
  <p>{{ errorMessage }}</p>
  <p class="text-muted small">
    Veuillez vérifier votre connexion et contacter votre administrateur si le problème persiste.
  </p>
  <button class="btn btn-primary" (click)="loadInvoices()">
    Réessayer
  </button>
</div>

<!-- État succès: liste vide (OK)-->
<div *ngIf="!isLoading && !hasError && invoices.length === 0" class="alert alert-info">
  Aucune facture ne correspond à vos critères de recherche.
</div>

<!-- État succès: données -->
<div *ngIf="!isLoading && !hasError && invoices.length > 0">
  <table class="table">
    <thead>
      <tr>
        <th>Numéro</th>
        <th>Client</th>
        <th>Total</th>
        <th>Statut</th>
        <th>Date</th>
      </tr>
    </thead>
    <tbody>
      <tr *ngFor="let invoice of invoices">
        <td>{{ invoice.docNum }}</td>
        <td>{{ invoice.cardName }}</td>
        <td>{{ invoice.total | currency }}</td>
        <td>
          <span [ngClass]="getStatusClass(invoice.status)">
            {{ invoice.status }}
          </span>
        </td>
        <td>{{ invoice.date | date: 'short' }}</td>
      </tr>
    </tbody>
  </table>
</div>
```

### Service: Type-safe

```typescript
// models.ts
export interface ApiResponse<T> {
  success: boolean;
  message?: string | null;
  error?: string;
  data?: T | null;
  count?: number;
}

export interface Invoice {
  docEntry: number;
  docNum: number;
  cardCode: string;
  cardName: string;
  total: number;
  date: Date | null;
  status: 'Open' | 'Closed' | 'Cancelled';
  docStatus: string;
  isCancelled: boolean;
}

// commercial-api.service.ts
export class CommercialApiService {
  constructor(private http: HttpClient) {}

  getInvoices(
    page: number = 1,
    pageSize: number = 50,
    filters?: InvoiceFilters
  ): Observable<ApiResponse<Invoice[]>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (filters?.openOnly) params = params.set('openOnly', 'true');
    if (filters?.search) params = params.set('search', filters.search);
    if (filters?.customer) params = params.set('customer', filters.customer);
    if (filters?.status) params = params.set('status', filters.status);
    if (filters?.dateFrom) params = params.set('dateFrom', filters.dateFrom.toISOString());
    if (filters?.dateTo) params = params.set('dateTo', filters.dateTo.toISOString());

    return this.http.get<ApiResponse<Invoice[]>>('/api/sap/invoices', { params });
  }

  createInvoice(invoice: CreateInvoiceRequest): Observable<ApiResponse<Invoice>> {
    return this.http.post<ApiResponse<Invoice>>('/api/sap/invoices', invoice);
  }

  deleteInvoice(docEntry: number): Observable<ApiResponse<any>> {
    return this.http.delete<ApiResponse<any>>(`/api/sap/invoices/${docEntry}`);
  }

  registerPayment(
    docEntry: number,
    payment: RegisterPaymentRequest
  ): Observable<ApiResponse<any>> {
    return this.http.post<ApiResponse<any>>(
      `/api/sap/invoices/${docEntry}/payments`,
      payment
    );
  }
}

export interface InvoiceFilters {
  openOnly?: boolean;
  search?: string;
  customer?: string;
  status?: 'open' | 'closed' | 'cancelled';
  dateFrom?: Date;
  dateTo?: Date;
}

export interface CreateInvoiceRequest {
  cardCode: string;
  docDate?: string;
  docDueDate?: string;
  documentLines: InvoiceLine[];
  comments?: string;
}

export interface InvoiceLine {
  itemCode: string;
  quantity: number;
  warehouseCode: string;
  unitPrice: number;
  price?: number;
  discountPercent?: number;
}

export interface RegisterPaymentRequest {
  cardCode?: string;
  paymentMethodCode: string;
  cashSum: number;
  creditSum?: number;
}
```

---

## Checklist Frontend Implementation

- [ ] Composant InvoiceList
  - [ ] État `isLoading` boolean
  - [ ] État `hasError` boolean
  - [ ] État `errorMessage` string
  - [ ] Template: loader visible pendant chargement
  - [ ] Template: alert d'erreur affiche message générique
  - [ ] Template: bouton "Réessayer" fonctionne
  - [ ] Template: liste vide = pas d'erreur (si count = 0)

- [ ] Composant InvoiceDetail
  - [ ] Gestion d'erreur sur création
  - [ ] Gestion d'erreur sur suppression
  - [ ] Gestion d'erreur sur paiement
  - [ ] Affiche message d'erreur explicite

- [ ] Service CommercialApiService
  - [ ] Type ApiResponse<Invoice[]> utilisé
  - [ ] Paramètres querystring valides
  - [ ] Gestion des erreurs HTTP

- [ ] Styling/UX
  - [ ] Spinner visible pendant chargement
  - [ ] Alert rouge avec ⚠️ pour erreurs
  - [ ] Message d'erreur lisible
  - [ ] Bouton "Réessayer" visible

---

## Tests Manuels Frontend

### Test 1: Chargement normal
```
1. Ouvrir /invoices
2. Vérifier que les factures s'affichent (SQL OK)
3. Pagination fonctionne
4. Filtres fonctionnent
```

### Test 2: Erreur SQL simulée
```
1. Arrêter le serveur SQL SAP B1
2. Ouvrir /invoices
3. Vérifier: Erreur 500 reçue
4. Vérifier: Alert rouge affiche "Erreur lors du chargement des données."
5. Bouton "Réessayer" disponible
```

### Test 3: Création de facture
```
1. Formulaire rempli
2. Click créer
3. Vérifier: Loading spinner visible
4. Vérifier: Succès ou erreur affichée
5. Vérifier: Liste mis à jour si succès
```

### Test 4: Paiement
```
1. Facture sélectionnée
2. Click "Enregistrer paiement"
3. Montant saisi
4. Vérifier: Loading spinner
5. Vérifier: Facture mise à jour (status, montant payé)
```

---

## Logs à vérifier (Browser DevTools)

```
Console Network:
✅ GET /api/sap/invoices → 200 OK (SQL fonctionnelle)
✅ POST /api/sap/invoices → 200 OK (Création réussie)
✅ DELETE /api/sap/invoices/123 → 200 OK (Suppression réussie)
❌ GET /api/sap/invoices → 500 Error (SQL échouée)
```

```
Console Logs:
✅ "Factures chargées: 150 factures"
✅ "Facture créée: DocEntry=99"
❌ "Erreur lors du chargement des données."
```

---

## Migration depuis l'ancienne implémentation

Si vous aviez du fallback silencieux avant:

**AVANT:**
```typescript
// ❌ Mauvais: silence sur erreur SQL
getInvoices() {
  return this.api.getInvoices().catch(err => {
    console.error(err); // Seulement en log
    return []; // Retour vide silencieux
  });
}
```

**APRÈS:**
```typescript
// ✅ Bon: Erreur explicite
getInvoices() {
  this.api.getInvoices().subscribe({
    next: (response) => {
      if (response.success) {
        this.invoices = response.data ?? [];
      } else {
        throw new Error(response.error);
      }
    },
    error: (err) => {
      this.hasError = true;
      this.errorMessage = 'Erreur lors du chargement des données.';
    }
  });
}
```

---

## Support

Pour toute question sur l'implémentation:
1. Vérifier les logs serveur `[HYBRID-MODE]`
2. Consulter `HYBRID_MODE_TEST_CASES.md` pour les scénarios
3. Vérifier la configuration `appsettings.json`
4. Contacter l'administrateur système
