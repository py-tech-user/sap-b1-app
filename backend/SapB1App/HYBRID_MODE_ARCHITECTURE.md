# Mode Hybride - Architecture

## Vue d'ensemble

Le système SAP B1 utilise un **mode hybride clair et fiable**:
- **Lecture**: SQL uniquement pour certaines tables (invoices)
- **Écriture**: Service Layer pour toutes les modifications
- **Fallback**: Limité aux documents non-critiques

---

## INVOICES (Table OINV)

### Lecture - Mode SQL STRICT ✅
- **Source**: Table SQL `OINV` uniquement
- **Portée**: Toutes les factures (Open + Closed)
- **Performance**: Lecture directe SQL (plus rapide que Service Layer)
- **Fallback**: ❌ AUCUN - Erreur explicite si SQL échoue
- **Endpoint**: `GET /api/sap/invoices`

**Scénarios:**
- ✅ Configuration SQL OK + données trouvées → Retour des factures
- ✅ Configuration SQL OK + 0 facture → Retour d'une liste vide
- ❌ Configuration SQL manquante → Erreur 500 "Configuration SQL SAP manquante"
- ❌ Erreur SQL (timeout, connexion, etc.) → Erreur 500 "Erreur lors du chargement des données"

### Écriture - Mode Service Layer ✅
- **Création**: `POST /api/sap/invoices`
- **Suppression**: `DELETE /api/sap/invoices/{docEntry}`
- **Paiements**: `POST /api/sap/invoices/{docEntry}/payments`
- **Encaissement**: `POST /api/sap/encaissement`

Tous les changements passent par **Service Layer de SAP B1** pour la cohérence transactionnelle.

**Processus:**
1. Validation du document
2. Appel Service Layer
3. Récupération du DocEntry créé
4. Retour du document normalisé

---

## AUTRES DOCUMENTS (ORDR, ODLN, OQUT, ORIN, ORDN)

### Lecture - Mode SQL avec Fallback 📊
- **Source**: SQL en priorité, Service Layer en fallback
- **Portée**: Selon la table
- **Performance**: SQL si disponible, sinon Service Layer
- **Fallback**: ✅ OUI - Bascule automatique si SQL échoue
- **Endpoints**: `/api/sap/orders`, `/api/sap/delivery-notes`, `/api/sap/quotes`, etc.

**Scénarios:**
- ✅ Configuration SQL OK → Lecture SQL
- ✅ Configuration SQL manquante → Fallback Service Layer
- ✅ Erreur SQL → Fallback Service Layer
- ✅ Données SQL vides → Tentative fallback Service Layer

### Écriture - Mode Service Layer ✅
Même que les invoices - toujours via Service Layer.

---

## Configuration Requise

```json
{
  "SapB1": {
    "Server": "SAP_SERVER_IP_OR_HOSTNAME",
    "CompanyDB": "DATABASE_NAME",
    "DbUserName": "database_user",
    "DbPassword": "database_password",
    "UseTrusted": false,
    "SqlCommandTimeoutSeconds": 30
  },
  "SapB1ServiceLayer": {
    "CompanyDB": "DATABASE_NAME",
    "LocalCurrency": "EUR"
  }
}
```

**Pour le mode hybride invoices:**
- ✅ `SapB1:Server` (obligatoire)
- ✅ `SapB1ServiceLayer:CompanyDB` ou `SapB1:CompanyDB` (obligatoire)
- ✅ `SapB1:DbUserName` + `SapB1:DbPassword` (sauf si `UseTrusted: true`)

---

## Logging - Tags pour traçabilité

### Logs Hybride Invoices
```
[HYBRID-MODE] Configuration requise OK
[HYBRID-MODE][READ] Factures chargées depuis SQL
[HYBRID-MODE][WRITE] Paiement créé via Service Layer
[HYBRID-MODE][WRITE-ERROR] Échec de création
[HYBRID-MODE][WRITE-SUCCESS] Opération réussie
```

### Logs Fallback
```
[FALLBACK] Basculement Service Layer
```

---

## Comportement Frontend Attendu

### Cas 1: Données disponibles
```
GET /api/sap/invoices → 200 OK
{
  "success": true,
  "message": null,
  "data": [
    { "docEntry": 1, "docNum": 1, "cardCode": "C001", ... },
    { "docEntry": 2, "docNum": 2, "cardCode": "C002", ... }
  ],
  "count": 2
}
```

### Cas 2: Configuration SQL manquante
```
GET /api/sap/invoices → 500 Internal Server Error
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Configuration SQL SAP manquante. Impossible de charger les factures. Veuillez contacter l'administrateur."
}
```

### Cas 3: Erreur SQL (timeout, connexion, etc.)
```
GET /api/sap/invoices → 500 Internal Server Error
{
  "success": false,
  "message": "Erreur SAP",
  "error": "Erreur lors du chargement des données."
}
```

**Frontend:** Afficher "Erreur lors du chargement des données." à l'utilisateur.

### Cas 4: Aucune facture trouvée
```
GET /api/sap/invoices → 200 OK
{
  "success": true,
  "message": null,
  "data": [],
  "count": 0
}
```

---

## Résumé des Garanties

| Aspect | Invoices | Autres Docs |
|--------|----------|-----------|
| **Lecture** | SQL strict | SQL + fallback |
| **Écriture** | Service Layer | Service Layer |
| **Fallback SQL** | ❌ Non | ✅ Oui |
| **Erreur explicite** | ✅ Oui | ✅ Oui |
| **Toutes les données** | ✅ Oui (SQL OINV) | Selon fallback |
| **Performance** | ⚡ Haute | 📊 Variable |

---

## Checklist de Validation

- [ ] `appsettings.json` contient `SapB1:Server` et `SapB1ServiceLayer:CompanyDB`
- [ ] `SapB1:DbUserName` et `SapB1:DbPassword` sont valides
- [ ] Base de données SAP B1 est accessible
- [ ] Table `OINV` existe et contient des données
- [ ] Service Layer SAP B1 est opérationnel
- [ ] Tests: `/api/sap/invoices` retourne toutes les factures
- [ ] Tests: Erreur explicite si SQL échoue
- [ ] Frontend affiche "Erreur lors du chargement des données." en cas d'erreur
