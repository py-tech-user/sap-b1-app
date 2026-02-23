# 🏗️ SAP Business One Integration App — Architecture Complète

## Stack Technique
| Couche | Technologie |
|--------|------------|
| Frontend | Angular 21.1.4 (Standalone Components, Signals) |
| Backend | .NET 10 Web API |
| Base de données | SQL Server Express |
| ORM | Entity Framework Core 9 |
| Auth | JWT Bearer Tokens |
| CSS | Angular Material + SCSS |
| Docs API | Swagger / OpenAPI |
| Logs | Serilog |

---

## 📁 Structure Complète du Projet

```
sap-b1-app/
├── 📁 backend/
│   └── SapB1App/
│       ├── SapB1App.csproj          ← Packages NuGet
│       ├── Program.cs               ← Point d'entrée, DI, Middleware pipeline
│       ├── appsettings.json         ← Config: BD, JWT, SAP B1 URL
│       ├── 📁 Controllers/
│       │   └── Controllers.cs       ← AuthController, CustomersController, 
│       │                               OrdersController, ProductsController, 
│       │                               DashboardController
│       ├── 📁 Models/
│       │   └── Models.cs            ← Customer, Product, Order, OrderLine, AppUser
│       ├── 📁 DTOs/
│       │   └── Dtos.cs              ← Tous les DTOs Request/Response + pagination
│       ├── 📁 Data/
│       │   ├── AppDbContext.cs      ← EF Core DbContext + configuration Fluent API
│       │   └── DbSeeder.cs          ← Données initiales (admin + produits demo)
│       ├── 📁 Services/
│       │   ├── CustomerService.cs   ← CRUD + sync SAP
│       │   ├── OrderService.cs      ← CRUD commandes + calcul totaux
│       │   └── Services.cs          ← ProductService, AuthService (JWT), SapB1Service
│       ├── 📁 Interfaces/
│       │   └── IServices.cs         ← Contrats des services (IoC)
│       └── 📁 Middleware/
│           └── ExceptionMiddleware.cs ← Gestion globale des erreurs HTTP
│
└── 📁 frontend/
    ├── package.json                 ← Dépendances Angular 21 + Material
    ├── angular.json                 ← Config workspace Angular CLI
    ├── tsconfig.json                ← TypeScript strict mode
    ├── proxy.conf.json              ← Dev proxy: /api → https://localhost:7000
    └── src/
        ├── main.ts                  ← Bootstrap Angular
        ├── index.html               ← HTML root + fonts
        ├── styles.scss              ← Theme Material + styles globaux
        ├── environments/
        │   ├── environment.ts       ← Dev: apiUrl = '/api' (proxied)
        │   └── environment.prod.ts  ← Prod: URL réelle de l'API
        └── app/
            ├── app.component.ts     ← Root component (juste <router-outlet>)
            ├── app.config.ts        ← ApplicationConfig: router, http, toastr
            ├── app.routes.ts        ← Routes lazy-loaded par feature
            │
            ├── 📁 core/             ← Singleton services, guards, interceptors
            │   ├── models/models.ts      ← Interfaces TypeScript (= DTOs .NET)
            │   ├── services/
            │   │   ├── auth.service.ts   ← Login/logout, gestion JWT localStorage
            │   │   └── api.services.ts   ← CustomerApiService, OrderApiService, etc.
            │   ├── interceptors/
            │   │   └── interceptors.ts   ← authInterceptor (Bearer) + errorInterceptor
            │   └── guards/
            │       └── auth.guard.ts     ← Route guard (redirige vers /login)
            │
            ├── 📁 shared/           ← Composants réutilisables
            │   └── components/
            │       └── shell/            ← Layout principal: sidebar + toolbar
            │
            └── 📁 features/         ← Modules fonctionnels (lazy loaded)
                ├── auth/
                │   └── login/            ← Page de connexion JWT
                ├── dashboard/            ← KPIs + commandes récentes + top produits
                ├── customers/
                │   ├── customer-list/    ← Tableau paginé + recherche + sync SAP
                │   ├── customer-form/    ← Formulaire création/édition
                │   └── customer-detail/  ← Fiche client + historique commandes
                ├── orders/
                │   ├── order-list/       ← Liste commandes + filtres statut
                │   ├── order-form/       ← Saisie commande avec lignes dynamiques
                │   └── order-detail/     ← Détail + changement statut + sync SAP
                └── products/
                    ├── product-list/     ← Catalogue articles + stock
                    └── product-form/     ← Création/édition article
```

---

## 🔌 Endpoints API REST

### Auth
| Méthode | URL | Description |
|---------|-----|-------------|
| POST | /api/auth/login | Connexion → JWT token |
| GET  | /api/auth/me | Profil utilisateur connecté |

### Clients
| Méthode | URL | Description |
|---------|-----|-------------|
| GET  | /api/customers?page=1&pageSize=20&search=... | Liste paginée |
| GET  | /api/customers/{id} | Détail client |
| POST | /api/customers | Créer un client |
| PUT  | /api/customers/{id} | Modifier un client |
| DELETE | /api/customers/{id} | Soft delete (Admin/Manager) |
| POST | /api/customers/{id}/sync-sap | Sync vers SAP B1 |

### Commandes
| Méthode | URL | Description |
|---------|-----|-------------|
| GET  | /api/orders?status=Confirmed&customerId=5 | Liste filtrée |
| GET  | /api/orders/{id} | Détail avec lignes |
| POST | /api/orders | Créer une commande |
| PATCH | /api/orders/{id}/status | Changer le statut |
| DELETE | /api/orders/{id} | Supprimer (brouillon only) |
| POST | /api/orders/{id}/sync-sap | Sync vers SAP B1 |

### Articles
| Méthode | URL | Description |
|---------|-----|-------------|
| GET  | /api/products | Liste articles actifs |
| POST | /api/products | Créer article |
| PUT  | /api/products/{id} | Modifier article |
| DELETE | /api/products/{id} | Désactiver |

### Dashboard
| Méthode | URL | Description |
|---------|-----|-------------|
| GET  | /api/dashboard | KPIs + données graphiques |

---

## 🚀 Démarrage Rapide

### Prérequis
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- SQL Server Express (local) ou connexion réseau SAP B1

### 1. Backend
```bash
cd backend/SapB1App

# Restaurer les packages
dotnet restore

# Créer la migration initiale
dotnet ef migrations add InitialCreate

# Appliquer la migration + seeder auto au démarrage
dotnet run
# → API disponible sur https://localhost:7000
# → Swagger: https://localhost:7000/swagger
```

### 2. Frontend
```bash
cd frontend

npm install

# Démarrer le serveur de dev (proxy vers https://localhost:7000)
npm start
# → App disponible sur http://localhost:4200
```

### 3. Connexion initiale
- Utilisateur : **admin**
- Mot de passe : **Admin@123**

---

## 🔧 Configuration SAP Business One

Dans `appsettings.json`, renseignez votre Service Layer :
```json
"SapB1": {
  "ServiceLayerUrl": "https://votre-serveur-sap:50000/b1s/v1",
  "CompanyDB": "SBO_DEMO",
  "UserName": "manager",
  "Password": "votre_mot_de_passe"
}
```

Le bouton **Sync SAP B1** sur chaque client/commande appelle `SapB1Service`
qui envoie les données au format JSON attendu par la Service Layer SAP B1.

---

## 🔐 Sécurité & Rôles

| Rôle | Droits |
|------|--------|
| User | Lecture seule |
| Manager | CRUD + Sync SAP |
| Admin | Tout + suppression |

Les routes Angular sont protégées par `authGuard`.  
Les endpoints .NET sont protégés par `[Authorize(Roles = "...")]`.

---

## 📋 Prochaines étapes (modules à ajouter)

1. **Fournisseurs** — BusinessPartners SAP de type fournisseur
2. **Factures** — AR Invoices depuis les commandes confirmées
3. **Avoirs** — Credit Memos
4. **Stocks** — Mouvements de stock, réceptions
5. **Rapports** — Export PDF/Excel des commandes
6. **Multi-société** — Sélection de la compagnie SAP B1 au login
