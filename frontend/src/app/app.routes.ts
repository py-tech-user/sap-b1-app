import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

// Shorthand role sets
const ALL = ['Admin', 'Manager', 'Commercial'];
const MGR = ['Admin', 'Manager'];

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },

  // ── Public ────────────────────────────────────────────────────────────────
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component')
        .then(m => m.LoginComponent)
  },

  // ── Protected (Shell layout) ──────────────────────────────────────────────
  {
    path: '',
    loadComponent: () =>
      import('./shared/components/shell/shell.component')
        .then(m => m.ShellComponent),
    canActivate: [authGuard],
    canActivateChild: [roleGuard],
    children: [
      // Dashboard
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component')
            .then(m => m.DashboardComponent),
        data: { roles: ALL }
      },
      {
        path: 'commercial-dashboard',
        loadComponent: () =>
          import('./features/commercial/commercial-dashboard/commercial-dashboard.component')
            .then(m => m.CommercialDashboardComponent),
        data: { roles: ALL }
      },

      // ── Customers ──────────────────────────────────────────────────────
      {
        path: 'customers',
        loadComponent: () =>
          import('./features/customers/customer-list/customer-list.component')
            .then(m => m.CustomerListComponent),
        data: { roles: ALL }
      },
      {
        path: 'partners',
        loadComponent: () =>
          import('./features/partners/partner-list/partner-list.component')
            .then(m => m.PartnerListComponent),
        data: { roles: ALL }
      },
      {
        path: 'customers/new',
        loadComponent: () =>
          import('./features/customers/customer-form/customer-form.component')
            .then(m => m.CustomerFormComponent),
        data: { roles: ALL }
      },
      {
        path: 'customers/:id',
        loadComponent: () =>
          import('./features/customers/customer-detail/customer-detail.component')
            .then(m => m.CustomerDetailComponent),
        data: { roles: ALL }
      },
      {
        path: 'customers/:id/edit',
        loadComponent: () =>
          import('./features/customers/customer-form/customer-form.component')
            .then(m => m.CustomerFormComponent),
        data: { roles: ALL }
      },

      // ── Documents commerciaux ──────────────────────────────────────────
      {
        path: 'quotes',
        loadChildren: () =>
          import('./features/commercial/commercial.routes')
            .then(m => m.QUOTES_ROUTES),
        data: { roles: ALL }
      },
      {
        path: 'orders',
        loadChildren: () =>
          import('./features/commercial/commercial.routes')
            .then(m => m.ORDERS_ROUTES),
        data: { roles: ALL }
      },
      {
        path: 'deliverynotes',
        loadChildren: () =>
          import('./features/commercial/commercial.routes')
            .then(m => m.DELIVERY_NOTES_ROUTES),
        data: { roles: ALL }
      },
      {
        path: 'factures',
        loadChildren: () =>
          import('./features/invoices/invoices.routes')
            .then(m => m.FACTURES_ROUTES),
        data: { roles: ALL }
      },
      {
        path: 'encaissement',
        loadComponent: () =>
          import('./features/encaissement/encaissement.component')
            .then(m => m.EncaissementComponent),
        data: { roles: ALL }
      },
      {
        path: 'creditnotes',
        loadChildren: () =>
          import('./features/commercial/commercial.routes')
            .then(m => m.CREDIT_NOTES_ROUTES),
        data: { roles: ALL }
      },
      {
        path: 'returns',
        loadChildren: () =>
          import('./features/commercial/commercial.routes')
            .then(m => m.RETURNS_ROUTES),
        data: { roles: ALL }
      },

      // ── Products (Catalogue - Lecture seule) ───────────────────────────
      {
        path: 'products',
        loadComponent: () =>
          import('./features/products/product-list/product-list.component')
            .then(m => m.ProductListComponent),
        data: { roles: ALL }
      },

      // ── Visits (Visites clients) ───────────────────────────────────────
      {
        path: 'visits',
        loadComponent: () =>
          import('./features/visits/visits.component')
            .then(m => m.VisitsComponent),
        data: { roles: ALL }
      },

      // ── Reporting (Rapports) ───────────────────────────────────────────
      {
        path: 'reporting',
        loadComponent: () =>
          import('./features/reporting/reporting-dashboard/reporting-dashboard.component')
            .then(m => m.ReportingDashboardComponent),
        data: { roles: MGR }
      },
      {
        path: 'reporting/pending-payments',
        loadComponent: () =>
          import('./features/reporting/pending-payments/pending-payments.component')
            .then(m => m.PendingPaymentsComponent),
        data: { roles: MGR }
      },
      {
        path: 'reporting/late-orders',
        loadComponent: () =>
          import('./features/reporting/late-orders/late-orders.component')
            .then(m => m.LateOrdersComponent),
        data: { roles: MGR }
      }
    ]
  },

  { path: '**', redirectTo: '/dashboard' }
];
