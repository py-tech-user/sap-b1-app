import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

// Shorthand role sets
const ALL = ['Admin', 'Manager', 'Commercial'];
const MGR = ['Admin', 'Manager'];

export const routes: Routes = [
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },

  // a”€a”€ Public a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component')
        .then(m => m.LoginComponent)
  },

  // a”€a”€ Protected (Shell layout) a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€
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
        path: 'dashboard/documents',
        loadComponent: () =>
          import('./features/dashboard/dashboard-documents.component')
            .then(m => m.DashboardDocumentsComponent),
        data: { roles: ALL }
      },
      {
        path: 'dashboard/partners-activity',
        loadComponent: () =>
          import('./features/dashboard/dashboard-partners-activity.component')
            .then(m => m.DashboardPartnersActivityComponent),
        data: { roles: ALL }
      },
      {
        path: 'dashboard/commercials-performance',
        loadComponent: () =>
          import('./features/dashboard/dashboard-commercials-performance.component')
            .then(m => m.DashboardCommercialsPerformanceComponent),
        data: { roles: MGR }
      },
      {
        path: 'reporting',
        loadComponent: () =>
          import('./features/reporting/reporting.component')
            .then(m => m.ReportingComponent),
        data: { roles: ALL }
      },
      // a”€a”€ Customers a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€
      {
        path: 'customers',
        loadComponent: () =>
          import('./features/customers/customer-list/customer-list.component')
            .then(m => m.CustomerListComponent),
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

      // a”€a”€ Documents commerciaux a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€
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

      // a”€a”€ Products (Catalogue - Lecture seule) a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€a”€
      {
        path: 'products',
        loadComponent: () =>
          import('./features/products/product-list/product-list.component')
            .then(m => m.ProductListComponent),
        data: { roles: ALL }
      },

    ]
  },

  { path: '**', redirectTo: '/dashboard' }
];


