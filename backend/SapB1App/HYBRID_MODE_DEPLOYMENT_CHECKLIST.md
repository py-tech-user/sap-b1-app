# Checklist - Mode Hybride Invoices ✅

## Configuration Backend

### appsettings.json ✅
```json
{
  "SapB1": {
    "Server": "YOUR_SAP_SERVER",
    "CompanyDB": "YOUR_COMPANY_DB",
    "DbUserName": "sql_user",
    "DbPassword": "sql_password",
    "UseTrusted": false,
    "SqlCommandTimeoutSeconds": 30
  },
  "SapB1ServiceLayer": {
    "BaseUrl": "https://YOUR_SAP_SERVER:50000/b1s/v1",
    "CompanyDB": "YOUR_COMPANY_DB",
    "UserName": "your_sap_user",
    "Password": "your_sap_password",
    "LocalCurrency": "EUR"
  }
}
```

**Vérification:**
- [ ] `SapB1:Server` rempli et correct (IP ou hostname)
- [ ] `SapB1ServiceLayer:CompanyDB` rempli (ou `SapB1:CompanyDB`)
- [ ] `SapB1:DbUserName` rempli
- [ ] `SapB1:DbPassword` rempli
- [ ] Authentification SQL testée
- [ ] Service Layer accessible

### Code Backend ✅

#### SapB1Controller.cs
- [ ] `GetInvoices()` appelle `GetDocumentsViaSqlAsync("OINV", ...)`
- [ ] `GetDocumentsViaSqlAsync()` a la logique `if (isInvoiceTable)` pour refuser fallback
- [ ] `CreateInvoice()` log `[HYBRID-MODE][WRITE]`
- [ ] `DeleteInvoice()` log `[HYBRID-MODE][WRITE]`
- [ ] `RegisterInvoicePayment()` log `[HYBRID-MODE][WRITE]` et `[HYBRID-MODE][WRITE-SUCCESS]`
- [ ] Tous les logs contiennent `[HYBRID-MODE]` pour traçabilité
- [ ] Messages d'erreur explicites (pas de cache-erreur)

#### Logs de diagnostic
- [ ] Erreur SQL → `[HYBRID-MODE] Erreur critique SQL SAP...`
- [ ] Config manquante → `[HYBRID-MODE] Configuration SQL SAP incomplète...`
- [ ] Succès → `[HYBRID-MODE] Factures chargées depuis SQL OINV avec succès...`
- [ ] Écriture → `[HYBRID-MODE][WRITE] ...`
- [ ] Erreur écriture → `[HYBRID-MODE][WRITE-ERROR] ...`
- [ ] Succès écriture → `[HYBRID-MODE][WRITE-SUCCESS] ...`

---

## Tests API Manuels

### Test 1: Configuration OK + Données
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Get
$response | ConvertTo-Json
```

**Attendu:**
- Status: 200 OK
- `success = true`
- `data` contient liste de factures
- `count > 0`

✅ **Résultat:** _____________

### Test 2: Configuration manquante
```powershell
# Modifier appsettings.json: set SapB1:Server = $null
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Get -ErrorAction SilentlyContinue
$response | ConvertTo-Json
```

**Attendu:**
- Status: 500 Internal Server Error
- `success = false`
- `error` contient "Configuration SQL SAP manquante"

✅ **Résultat:** _____________

### Test 3: Création de facture
```powershell
$body = @{
    cardCode = "C001"
    docDate = "2025-03-15"
    docDueDate = "2025-04-15"
    documentLines = @(
        @{
            itemCode = "ITEM001"
            quantity = 1
            warehouseCode = "WH1"
            unitPrice = 100
            price = 100
        }
    )
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Post -Body $body -ContentType "application/json"
$response | ConvertTo-Json
```

**Attendu:**
- Status: 200 OK
- `success = true`
- `data.docEntry` présent
- Log: `[HYBRID-MODE][WRITE-SUCCESS] Facture créée avec succès`

✅ **Résultat:** _____________

### Test 4: Paiement de facture
```powershell
$body = @{
    paymentMethodCode = "PM_CASH"
    cashSum = 1000
    creditSum = 0
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices/1/payments" -Method Post -Body $body -ContentType "application/json"
$response | ConvertTo-Json
```

**Attendu:**
- Status: 200 OK
- `success = true`
- `data.payment` présent
- `data.invoice.paidToDate` mis à jour

✅ **Résultat:** _____________

### Test 5: Suppression de facture
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices/99" -Method Delete
$response | ConvertTo-Json
```

**Attendu:**
- Status: 200 OK
- `success = true`
- Log: `[HYBRID-MODE][WRITE] Suppression de facture`

✅ **Résultat:** _____________

### Test 6: Filtres et pagination
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices?page=1&pageSize=25&status=open&customer=C001" -Method Get
$response | ConvertTo-Json
```

**Attendu:**
- Status: 200 OK
- `success = true`
- Factures filtrées par status=open
- Factures filtrées par customer=C001
- Max 25 résultats

✅ **Résultat:** _____________

---

## Tests Frontend

### Test 1: Page /invoices charge les factures

**Scénario:**
1. Naviguer vers `/invoices`
2. Vérifier que le spinner s'affiche
3. Vérifier que les factures s'affichent après chargement

**Attendu:**
- ✅ Spinner visible pendant le chargement
- ✅ Liste de factures affichée
- ✅ Colonnes: DocNum, ClientName, Total, Status, Date
- ✅ Pagination fonctionne

✅ **Résultat:** _____________

### Test 2: Gestion d'erreur - Configuration SQL manquante

**Scénario:**
1. Modifier `appsettings.json`: `SapB1:Server = null`
2. Redémarrer l'application
3. Naviguer vers `/invoices`
4. Vérifier l'affichage d'erreur

**Attendu:**
- ✅ Pas de crash frontend
- ✅ Alert rouge visible
- ✅ Message: "Erreur lors du chargement des données."
- ✅ Bouton "Réessayer" disponible
- ✅ Aucune facture affichée partiellement

✅ **Résultat:** _____________

### Test 3: Gestion d'erreur - Erreur SQL

**Scénario:**
1. Arrêter le serveur SQL SAP B1
2. Naviguer vers `/invoices`
3. Vérifier l'affichage d'erreur

**Attendu:**
- ✅ Alert rouge visible
- ✅ Message: "Erreur lors du chargement des données."
- ✅ Pas de fallback silencieux
- ✅ Bouton "Réessayer" fonctionne

✅ **Résultat:** _____________

### Test 4: Création de facture

**Scénario:**
1. Cliquer sur "Créer facture"
2. Remplir le formulaire
3. Cliquer "Enregistrer"
4. Vérifier le résultat

**Attendu:**
- ✅ Loading spinner visible
- ✅ Facture créée et affichée en haut de la liste
- ✅ DocEntry présent
- ✅ Status = "Open"

✅ **Résultat:** _____________

### Test 5: Suppression de facture

**Scénario:**
1. Sélectionner une facture
2. Cliquer sur "Supprimer"
3. Confirmer

**Attendu:**
- ✅ Loading spinner visible
- ✅ Facture disparaît de la liste
- ✅ Message de succès affiché
- ✅ Pas de silencieux fallback

✅ **Résultat:** _____________

### Test 6: Paiement de facture

**Scénario:**
1. Sélectionner une facture avec status="Open"
2. Cliquer "Enregistrer paiement"
3. Saisir le montant
4. Valider

**Attendu:**
- ✅ Loading spinner visible
- ✅ Paiement créé
- ✅ Facture mise à jour (PaidToDate, Status)
- ✅ Message de succès

✅ **Résultat:** _____________

### Test 7: Liste vide (OK)

**Scénario:**
1. Naviguer vers `/invoices`
2. Appliquer filtre: `search=XYZABC123` (aucun match)
3. Vérifier l'affichage

**Attendu:**
- ✅ Pas d'erreur affichée
- ✅ Message: "Aucune facture ne correspond à vos critères"
- ✅ Pas d'alert rouge
- ✅ Pas de fallback

✅ **Résultat:** _____________

---

## Tests de Performance

### Test 1: Temps de réponse SQL
**Target:** < 2 secondes pour 1000 factures

```powershell
$startTime = Get-Date
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Get
$elapsed = (Get-Date) - $startTime
Write-Host "Temps de réponse: $($elapsed.TotalMilliseconds) ms"
```

✅ **Résultat:** _____________

### Test 2: Paginiation
**Target:** 25-50 factures par page

```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices?page=1&pageSize=25" -Method Get
Write-Host "Factures retournées: $($response.data.Count)"
Write-Host "Total: $($response.count)"
```

✅ **Résultat:** _____________

---

## Vérification des Logs

### Logs Backend - Application Event Viewer

**Rechercher:**
```
[HYBRID-MODE] Factures chargées depuis SQL OINV avec succès
```

✅ **Trouvé:** _____ **Timestamp:** _____________

### Logs Frontend - Browser DevTools

**Console Network:**
```
GET /api/sap/invoices → 200 OK
POST /api/sap/invoices → 200 OK
DELETE /api/sap/invoices/123 → 200 OK
```

✅ **Vérifiés:** _____________

**Console Logs:**
```
"Factures chargées: 150"
"Facture créée: DocEntry=99"
```

✅ **Vérifiés:** _____________

---

## Checklist Finale

### Backend
- [ ] Build réussit sans erreurs (`dotnet build`)
- [ ] Tous les endpoints répondent
- [ ] Logs `[HYBRID-MODE]` présents
- [ ] Erreurs explicites (pas de fallback silencieux)
- [ ] Configuration SQL requise

### Frontend
- [ ] Composant InvoiceList affiche les données
- [ ] Gestion d'erreur affiche alert rouge
- [ ] Spinner visible pendant chargement
- [ ] Bouton "Réessayer" fonctionne
- [ ] Pagination fonctionne
- [ ] Filtres fonctionnent
- [ ] Création/Suppression/Paiement fonctionnent

### Documentation
- [ ] `HYBRID_MODE_ARCHITECTURE.md` créé ✅
- [ ] `HYBRID_MODE_TEST_CASES.md` créé ✅
- [ ] `FRONTEND_HYBRID_MODE_GUIDE.md` créé ✅
- [ ] `HYBRID_MODE_DEPLOYMENT_CHECKLIST.md` complété

### Déploiement
- [ ] Configuration SQL testée en production
- [ ] Service Layer accessible
- [ ] Table OINV contient les données
- [ ] Monitoring des logs en place

---

## Signature de Validation

**Validé par:** ________________  
**Date:** ________________  
**Environnement:** ✅ DEV / ✅ STAGING / ✅ PROD

**Commentaires:** _________________________________________________

---

## Notes

- Mode hybride pour invoices: SQL STRICT (pas de fallback)
- Autres documents: SQL + fallback Service Layer
- Tous les logs contiennent `[HYBRID-MODE]` pour traçabilité
- Erreurs explicites côté client (pas de cache silencieux)
- Performance: SQL << Service Layer pour invoices
