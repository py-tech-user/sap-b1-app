# API Documentation - Mode Hybride Invoices

## Overview

L'API SAP B1 utilise un **mode hybride optimisé**:
- ✅ **Lectures Invoices**: SQL uniquement (OINV) - performance
- ✅ **Écritures Invoices**: Service Layer - cohérence
- ❌ **Pas de fallback silencieux** - erreurs explicites

---

## Endpoints - Invoices

### GET /api/sap/invoices

Récupère la liste de toutes les factures (Open + Closed) depuis SQL.

**Mode:** ✅ SQL STRICT (pas de fallback)

**Query Parameters:**
```
GET /api/sap/invoices?page=1&pageSize=50&openOnly=false&search=&customer=&status=&dateFrom=&dateTo=
```

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Numéro de page (1-based) |
| `pageSize` | int | 50 | Résultats par page (1-200) |
| `openOnly` | bool | false | Seulement factures ouvertes |
| `search` | string | "" | Recherche CardCode/CardName/DocNum |
| `customer` | string | "" | Filtre client (CardCode ou CardName) |
| `status` | string | "" | "open", "closed", "cancelled" |
| `dateFrom` | datetime | null | Date minimum (YYYY-MM-DD) |
| `dateTo` | datetime | null | Date maximum (YYYY-MM-DD) |

**Response - 200 OK:**
```json
{
  "success": true,
  "message": null,
  "data": [
    {
      "docEntry": 1,
      "docNum": 1001,
      "cardCode": "C001",
      "cardName": "Customer Name",
      "total": 1000.50,
      "date": "2025-03-15T00:00:00",
      "status": "Open",
      "docStatus": "O",
      "isCancelled": false
    }
  ],
  "count": 150
}
```

**Response - 500 Internal Server Error (Config SQL manquante):**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Configuration SQL SAP manquante. Impossible de charger les factures. Veuillez contacter l'administrateur."
}
```

**Response - 500 Internal Server Error (Erreur SQL):**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Erreur lors du chargement des données."
}
```

**Logs:**
```
[HYBRID-MODE] Factures chargées depuis SQL OINV avec succès. Count=50, TotalCount=150, Page=1, PageSize=50
```

---

### GET /api/sap/invoices/{docEntry}

Récupère une facture spécifique par DocEntry (via Service Layer pour données fraiches).

**Path Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `docEntry` | int | Identifiant de la facture |

**Response - 200 OK:**
```json
{
  "success": true,
  "message": null,
  "data": {
    "docEntry": 1,
    "docNum": 1001,
    "cardCode": "C001",
    "cardName": "Customer Name",
    "total": 1000.50,
    "date": "2025-03-15T00:00:00",
    "status": "Open",
    "documentLines": [
      {
        "itemCode": "ITEM001",
        "quantity": 10,
        "warehouseCode": "WH1",
        "unitPrice": 100.05
      }
    ]
  }
}
```

**Response - 400 Bad Request:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "DocEntry invalide."
}
```

---

### POST /api/sap/invoices

Crée une nouvelle facture via Service Layer.

**Mode:** ✅ Service Layer (écriture)

**Request Body:**
```json
{
  "cardCode": "C001",
  "docDate": "2025-03-15",
  "docDueDate": "2025-04-15",
  "requiredDate": null,
  "comments": "Optional comment",
  "salesPersonCode": null,
  "series": null,
  "docObjectCode": null,
  "docType": null,
  "docRate": null,
  "userSign": null,
  "docStatus": null,
  "documentLines": [
    {
      "itemCode": "ITEM001",
      "quantity": 5,
      "warehouseCode": "WH1",
      "unitPrice": 100.00,
      "price": 100.00,
      "discountPercent": 0,
      "vatPercent": 20
    }
  ]
}
```

**Response - 200 OK:**
```json
{
  "success": true,
  "message": "Création réussie.",
  "data": {
    "docEntry": 99,
    "docNum": 1099,
    "cardCode": "C001",
    "cardName": "Customer Name",
    "status": "Open"
  }
}
```

**Response - 400 Bad Request (Validation):**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "CardCode est obligatoire."
}
```

**Response - 500 Internal Server Error:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Impossible de récupérer la devise du client."
}
```

**Logs:**
```
[HYBRID-MODE][WRITE] Création de facture via Service Layer. CardCode=C001, DocDate=2025-03-15
[HYBRID-MODE][WRITE-SUCCESS] Facture créée avec succès. DocEntry=99
```

---

### DELETE /api/sap/invoices/{docEntry}

Supprime une facture via Service Layer.

**Mode:** ✅ Service Layer (écriture)

**Path Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `docEntry` | int | Identifiant de la facture |

**Response - 200 OK:**
```json
{
  "success": true,
  "message": "Suppression réussie.",
  "data": { ... }
}
```

**Response - 400 Bad Request:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "DocEntry invalide."
}
```

**Response - 500 Internal Server Error:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Impossible de supprimer la facture."
}
```

**Logs:**
```
[HYBRID-MODE][WRITE] Suppression de facture via Service Layer. DocEntry=99
```

---

### POST /api/sap/invoices/{invoiceDocEntry}/payments

Enregistre un paiement pour une facture via Service Layer.

**Mode:** ✅ Service Layer (écriture)

**Path Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `invoiceDocEntry` | int | Identifiant de la facture |

**Request Body:**
```json
{
  "cardCode": "C001",
  "paymentMethodCode": "PM_CASH",
  "cashSum": 1000.00,
  "creditSum": 0.00
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cardCode` | string | non | Override CardCode (sinon du header) |
| `paymentMethodCode` | string | ✅ oui | Code méthode paiement SAP |
| `cashSum` | decimal | ✅ oui | Montant cash payé (>= 0) |
| `creditSum` | decimal | non | Montant crédit (>= 0) |

**Response - 200 OK:**
```json
{
  "success": true,
  "message": "Encaissement enregistré.",
  "data": {
    "payment": {
      "docEntry": 50,
      "docNum": 1050,
      "cardCode": "C001"
    },
    "invoice": {
      "docEntry": 1,
      "docNum": 1001,
      "paidToDate": 1000.00,
      "status": "Closed"
    }
  }
}
```

**Response - 400 Bad Request:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "PaymentMethodCode est obligatoire."
}
```

**Response - 500 Internal Server Error:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Impossible de charger la facture."
}
```

**Logs:**
```
[HYBRID-MODE][WRITE] Début de la création d'un paiement de facture. InvoiceDocEntry=1, CardCode=C001
[HYBRID-MODE][WRITE-SUCCESS] Paiement de facture créé avec succès. InvoiceDocEntry=1
```

---

### POST /api/sap/encaissement

Enregistre des paiements pour plusieurs factures via Service Layer (paiement global).

**Mode:** ✅ Service Layer (écriture)

**Request Body:**
```json
{
  "cardCode": "C001",
  "paymentMethodCode": "PM_CASH",
  "cashSum": 5000.00,
  "creditSum": 0.00,
  "invoices": [
    {
      "docEntry": 1,
      "sumApplied": 1000.00
    },
    {
      "docEntry": 2,
      "sumApplied": 2000.00
    },
    {
      "docEntry": 3,
      "sumApplied": 2000.00
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cardCode` | string | ✅ oui | Code client |
| `paymentMethodCode` | string | ✅ oui | Code méthode paiement |
| `cashSum` | decimal | ✅ oui | Montant total cash |
| `creditSum` | decimal | non | Montant total crédit |
| `invoices` | array | ✅ oui | Liste des factures |
| `invoices[].docEntry` | int | ✅ oui | DocEntry de la facture |
| `invoices[].sumApplied` | decimal | oui | Montant appliqué à la facture |

**Response - 200 OK:**
```json
{
  "success": true,
  "message": "Encaissement enregistré.",
  "data": {
    "payment": {
      "docEntry": 51,
      "docNum": 1051
    },
    "invoices": [
      {
        "docEntry": 1,
        "status": "Closed",
        "paidToDate": 1000.00
      },
      {
        "docEntry": 2,
        "status": "Closed",
        "paidToDate": 2000.00
      },
      {
        "docEntry": 3,
        "status": "Closed",
        "paidToDate": 2000.00
      }
    ],
    "totalSelected": 5000.00,
    "cashSumApplied": 5000.00
  }
}
```

**Response - 400 Bad Request:**
```json
{
  "success": false,
  "message": "Erreur SAP",
  "error": "CardCode est obligatoire."
}
```

**Logs:**
```
[HYBRID-MODE] Start payment registration. CardCode=C001, InvoiceCount=3
[ENCAISSEMENT][TRACE][BEFORE] DocEntry=1, DocTotal=1000.00, PaidToDate=0.00, OpenBal=1000.00
[ENCAISSEMENT][TRACE][AFTER] DocEntry=1, DocTotal=1000.00, PaidToDate=1000.00, OpenBal=0.00
[ENCAISSEMENT] Payment registration succeeded. CardCode=C001, InvoiceCount=3
```

---

## Status Codes

| Code | Meaning | Description |
|------|---------|-------------|
| **200 OK** | Success | Requête réussie |
| **201 Created** | Created | Ressource créée (alternative à 200) |
| **400 Bad Request** | Invalid Input | Données invalides ou manquantes |
| **404 Not Found** | Not Found | Ressource inexistante |
| **500 Internal Server Error** | Server Error | Configuration SQL manquante ou erreur SQL |

---

## Error Handling

### Frontend - Gestion d'erreur recommandée

```typescript
// ✅ Bon
getInvoices() {
  return this.http.get<ApiResponse<Invoice[]>>('/api/sap/invoices')
    .pipe(
      tap(response => {
        if (!response.success) {
          throw new Error(response.error || 'Erreur inconnue');
        }
      }),
      catchError(err => {
        // Erreur explicite affichée
        this.showError('Erreur lors du chargement des données.');
        return throwError(() => err);
      })
    );
}
```

### Messages standardisés

| Scénario | Message affiché |
|----------|-----------------|
| Config SQL manquante | "Erreur lors du chargement des données." |
| Erreur SQL timeout | "Erreur lors du chargement des données." |
| Erreur Service Layer | Détail de l'erreur SAP |
| Données vides | "Aucune facture ne correspond à vos critères." |

---

## Logging

Tous les logs critiques contiennent `[HYBRID-MODE]`:

```
[HYBRID-MODE] Configuration SQL SAP incomplète pour OINV
[HYBRID-MODE] Erreur critique SQL SAP lors du chargement
[HYBRID-MODE] Factures chargées depuis SQL OINV avec succès
[HYBRID-MODE][READ] ...
[HYBRID-MODE][WRITE] Création de facture via Service Layer
[HYBRID-MODE][WRITE-SUCCESS] Facture créée avec succès
[HYBRID-MODE][WRITE-ERROR] Échec de la création
```

**Logs détaillés (traces):**
```
[ENCAISSEMENT][TRACE][BEFORE] DocEntry=1, DocTotal=1000, PaidToDate=0, OpenBal=1000
[ENCAISSEMENT][TRACE][AFTER] DocEntry=1, DocTotal=1000, PaidToDate=1000, OpenBal=0
[ENCAISSEMENT][TRACE][SQL] DocTotal=1000, PaidToDate=1000, OpenBal=0
```

---

## Configuration requise

```json
{
  "SapB1": {
    "Server": "SAP_SERVER_IP",
    "CompanyDB": "DATABASE_NAME",
    "DbUserName": "sql_user",
    "DbPassword": "sql_password",
    "UseTrusted": false,
    "SqlCommandTimeoutSeconds": 30
  },
  "SapB1ServiceLayer": {
    "CompanyDB": "DATABASE_NAME",
    "LocalCurrency": "EUR"
  }
}
```

---

## Rate Limiting

Aucun rate limiting spécifique pour mode hybride.
Respecter les timings SAP B1 Service Layer (30-60s par requête).

---

## Examples

### Example 1: Lister factures avec statut Open

```bash
curl -X GET "http://localhost:5000/api/sap/invoices?status=open&pageSize=25" \
  -H "Content-Type: application/json"
```

### Example 2: Créer une facture

```bash
curl -X POST "http://localhost:5000/api/sap/invoices" \
  -H "Content-Type: application/json" \
  -d '{
    "cardCode": "C001",
    "docDueDate": "2025-04-15",
    "documentLines": [
      {
        "itemCode": "ITEM001",
        "quantity": 5,
        "warehouseCode": "WH1",
        "unitPrice": 100
      }
    ]
  }'
```

### Example 3: Enregistrer un paiement

```bash
curl -X POST "http://localhost:5000/api/sap/invoices/1/payments" \
  -H "Content-Type: application/json" \
  -d '{
    "paymentMethodCode": "PM_CASH",
    "cashSum": 1000,
    "creditSum": 0
  }'
```

---

**Version:** 1.0  
**Date:** Mars 2025  
**Mode:** Hybrid SQL + Service Layer
