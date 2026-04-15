// Test cases pour valider le mode hybride

// ============================================================================
// SCENARIO 1: Configuration SQL OK + Données disponibles
// ============================================================================
// GET /api/sap/invoices?page=1&pageSize=50
// Expected: 200 OK avec liste des factures

// Configuration requise:
// {
//   "SapB1:Server": "SAP_SERVER",
//   "SapB1ServiceLayer:CompanyDB": "DB_NAME",
//   "SapB1:DbUserName": "db_user",
//   "SapB1:DbPassword": "db_pass"
// }

// Response:
// {
//   "success": true,
//   "message": null,
//   "data": [
//     {
//       "docEntry": 1,
//       "docNum": 1001,
//       "cardCode": "C001",
//       "cardName": "Customer 1",
//       "total": 1000.00,
//       "date": "2025-03-15T00:00:00",
//       "status": "Open",
//       "isCancelled": false
//     }
//   ],
//   "count": 150
// }

// Validation:
// ✅ Toutes les factures sont retournées (Open + Closed)
// ✅ Status reflète l'état réel (Open/Closed/Cancelled)
// ✅ Count correct
// ✅ Pagination respectée

// ============================================================================
// SCENARIO 2: Configuration SQL OK + Aucune facture trouvée
// ============================================================================
// GET /api/sap/invoices?cardCode=UNKNOWN
// Expected: 200 OK avec liste vide

// Response:
// {
//   "success": true,
//   "message": null,
//   "data": [],
//   "count": 0
// }

// Validation:
// ✅ Retour OK (pas d'erreur)
// ✅ Données vides
// ✅ Count = 0

// ============================================================================
// SCENARIO 3: Configuration SQL MANQUANTE
// ============================================================================
// Configuration:
// {
//   "SapB1:Server": null,  // ❌ Manquant
//   "SapB1ServiceLayer:CompanyDB": null  // ❌ Manquant
// }

// GET /api/sap/invoices
// Expected: 500 Internal Server Error

// Response:
// {
//   "success": false,
//   "message": "Erreur SAP",
//   "error": "Configuration SQL SAP manquante. Impossible de charger les factures. Veuillez contacter l'administrateur."
// }

// Validation:
// ✅ Erreur 500 (pas de fallback)
// ✅ Message d'erreur explicite
// ✅ Pas de tentative Service Layer
// ✅ Frontend affiche: "Erreur lors du chargement des données."

// ============================================================================
// SCENARIO 4: Erreur SQL (timeout, connexion refusée, etc.)
// ============================================================================
// Configuration SQL OK, mais serveur indisponible

// GET /api/sap/invoices
// Expected: 500 Internal Server Error

// Response:
// {
//   "success": false,
//   "message": "Erreur SAP",
//   "error": "Erreur lors du chargement des données."
// }

// Validation:
// ✅ Erreur 500 (pas de fallback)
// ✅ Message d'erreur générique (sécurité)
// ✅ Log serveur: "[HYBRID-MODE] Erreur critique SQL SAP..."
// ✅ Pas de tentative Service Layer
// ✅ Frontend affiche: "Erreur lors du chargement des données."

// ============================================================================
// SCENARIO 5: Création de facture
// ============================================================================
// POST /api/sap/invoices
// Body:
// {
//   "cardCode": "C001",
//   "docDate": "2025-03-15",
//   "docDueDate": "2025-04-15",
//   "documentLines": [
//     {
//       "itemCode": "ITEM001",
//       "quantity": 5,
//       "warehouseCode": "WH1",
//       "unitPrice": 100,
//       "price": 100
//     }
//   ]
// }

// Expected: 201 Created (ou 200 OK selon convention)
// Response:
// {
//   "success": true,
//   "message": "Création réussie.",
//   "data": {
//     "docEntry": 99,
//     "docNum": 1099,
//     "cardCode": "C001",
//     ...
//   }
// }

// Validation:
// ✅ Via Service Layer
// ✅ DocEntry retourné
// ✅ Facture créée en SQL OINV
// ✅ Log: "[HYBRID-MODE][WRITE-SUCCESS] Facture créée avec succès"

// ============================================================================
// SCENARIO 6: Suppression de facture
// ============================================================================
// DELETE /api/sap/invoices/99
// Expected: 200 OK

// Response:
// {
//   "success": true,
//   "message": "Suppression réussie.",
//   "data": { ... }
// }

// Validation:
// ✅ Via Service Layer
// ✅ Facture disparait de SQL OINV
// ✅ Log: "[HYBRID-MODE][WRITE] Suppression de facture"

// ============================================================================
// SCENARIO 7: Paiement de facture
// ============================================================================
// POST /api/sap/invoices/99/payments
// Body:
// {
//   "paymentMethodCode": "PM_CASH",
//   "cashSum": 1000.00,
//   "creditSum": 0
// }

// Expected: 200 OK
// Response:
// {
//   "success": true,
//   "message": "Encaissement enregistré.",
//   "data": {
//     "payment": { ... },
//     "invoice": {
//       "docEntry": 99,
//       "paidToDate": 1000.00,
//       "status": "Closed",  // Peut être Updated
//       ...
//     }
//   }
// }

// Validation:
// ✅ Via Service Layer
// ✅ Paiement créé (IncomingPayments)
// ✅ Facture mise à jour en SQL OINV (PaidToDate, OpenBal, etc.)
// ✅ Log: "[HYBRID-MODE][WRITE-SUCCESS] Paiement de facture créé"

// ============================================================================
// SCENARIO 8: Filtre et pagination
// ============================================================================
// GET /api/sap/invoices?page=2&pageSize=25&status=open&customer=C001&search=1001&dateFrom=2025-03-01&dateTo=2025-03-31

// Expected: 200 OK avec factures filtrées et paginées
// Validation:
// ✅ Lecture SQL avec filtres
// ✅ Status filter fonctionne (open/closed/cancelled)
// ✅ Search sur CardCode, CardName, DocNum
// ✅ Pagination correct
// ✅ Count = total avant pagination

// ============================================================================
// SCENARIO 9: Encaissement (multiple invoices)
// ============================================================================
// POST /api/sap/encaissement
// Body:
// {
//   "cardCode": "C001",
//   "paymentMethodCode": "PM_CASH",
//   "cashSum": 5000.00,
//   "invoices": [
//     { "docEntry": 1, "sumApplied": 1000.00 },
//     { "docEntry": 2, "sumApplied": 2000.00 },
//     { "docEntry": 3, "sumApplied": 2000.00 }
//   ]
// }

// Expected: 200 OK
// Response:
// {
//   "success": true,
//   "message": "Encaissement enregistré.",
//   "data": {
//     "payment": { ... },
//     "invoices": [ ... ],
//     "totalSelected": 5000.00,
//     "cashSumApplied": 5000.00
//   }
// }

// Validation:
// ✅ Via Service Layer
// ✅ Paiement global créé
// ✅ Toutes les factures mises à jour en SQL
// ✅ Log avec traces BEFORE/AFTER

// ============================================================================
// PowerShell Test Script
// ============================================================================

# Test 1: Vérifier la configuration
Write-Host "=== Test 1: Configuration SQL ===" -ForegroundColor Cyan
$config = Get-Content appsettings.json | ConvertFrom-Json
if (-not $config.SapB1.Server) {
    Write-Host "❌ SapB1:Server manquant" -ForegroundColor Red
} else {
    Write-Host "✅ SapB1:Server: $($config.SapB1.Server)" -ForegroundColor Green
}

if (-not $config.SapB1ServiceLayer.CompanyDB) {
    Write-Host "❌ SapB1ServiceLayer:CompanyDB manquant" -ForegroundColor Red
} else {
    Write-Host "✅ SapB1ServiceLayer:CompanyDB: $($config.SapB1ServiceLayer.CompanyDB)" -ForegroundColor Green
}

# Test 2: Appel API
Write-Host "`n=== Test 2: Appel API /invoices ===" -ForegroundColor Cyan
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Get
if ($response.success) {
    Write-Host "✅ Succès: $($response.data.Count) factures trouvées" -ForegroundColor Green
} else {
    Write-Host "❌ Erreur: $($response.error)" -ForegroundColor Red
}

# Test 3: Créer une facture
Write-Host "`n=== Test 3: Création de facture ===" -ForegroundColor Cyan
$newInvoice = @{
    cardCode = "C001"
    docDate = [DateTime]::Now.ToString("yyyy-MM-dd")
    docDueDate = [DateTime]::Now.AddDays(30).ToString("yyyy-MM-dd")
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

$createResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Post -Body $newInvoice -ContentType "application/json"
if ($createResponse.success) {
    Write-Host "✅ Facture créée: DocEntry=$($createResponse.data.docEntry)" -ForegroundColor Green
} else {
    Write-Host "❌ Erreur: $($createResponse.error)" -ForegroundColor Red
}

# Test 4: Lire les factures après création
Write-Host "`n=== Test 4: Relecture après création ===" -ForegroundColor Cyan
$readResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/sap/invoices" -Method Get
Write-Host "✅ Factures actuelles: $($readResponse.count)" -ForegroundColor Green

Write-Host "`n=== Tous les tests complétés ===" -ForegroundColor Yellow
