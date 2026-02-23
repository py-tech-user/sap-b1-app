import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

// Shorthand role sets
const ALL = ['Admin', 'Manager', 'Commercial'];
const MGR = ['Admin', 'Manager'];
const ADM = ['Admin'];

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

      // ── Customers ──────────────────────────────────────────────────────
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
        data: { roles: MGR }
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
        data: { roles: MGR }
      },

      // ── Orders ─────────────────────────────────────────────────────────
      {
        path: 'orders',
        loadComponent: () =>
          import('./features/orders/order-list/order-list.component')
            .then(m => m.OrderListComponent),
        data: { roles: ALL }
      },
      {
        path: 'orders/new',
        loadComponent: () =>
          import('./features/orders/order-form/order-form.component')
            .then(m => m.OrderFormComponent),
        data: { roles: ALL }
      },
      {
        path: 'orders/:id',
        loadComponent: () =>
          import('./features/orders/order-detail/order-detail.component')
            .then(m => m.OrderDetailComponent),
        data: { roles: ALL }
      },

      // ── Products ────────────────────────────────────────────────────────
      {
        path: 'products',
        loadComponent: () =>
          import('./features/products/product-list/product-list.component')
            .then(m => m.ProductListComponent),
        data: { roles: ALL }
      },
      {
        path: 'products/new',
        loadComponent: () =>
          import('./features/products/product-form/product-form.component')
            .then(m => m.ProductFormComponent),
        data: { roles: MGR }
      },
      {
        path: 'products/:id/edit',
        loadComponent: () =>
          import('./features/products/product-form/product-form.component')
            .then(m => m.ProductFormComponent),
        data: { roles: MGR }
      },

      // ── Visits ──────────────────────────────────────────────────────────
      {
        path: 'visits',
        loadComponent: () =>
          import('./features/visits/visits.component')
            .then(m => m.VisitsComponent),
        data: { roles: ALL }
      },

      // ── Payments (Encaissements) ────────────────────────────────────────
      {
        path: 'payments',
        loadComponent: () =>
          import('./features/payments/payments.component')
            .then(m => m.PaymentsComponent),
        data: { roles: MGR }
      },

      // ── Tracking (Suivi terrain GPS) ──────────────────────────────────
      {
        path: 'tracking',
        loadComponent: () =>
          import('./features/tracking/tracking-dashboard/tracking-dashboard.component')
            .then(m => m.TrackingDashboardComponent),
        data: { roles: MGR }
      },
      {
        path: 'tracking/map',
        loadComponent: () =>
          import('./features/tracking/live-map/live-map.component')
            .then(m => m.LiveMapComponent),
        data: { roles: MGR }
      },
      {
        path: 'tracking/history/:userId',
        loadComponent: () =>
          import('./features/tracking/track-history/track-history.component')
            .then(m => m.TrackHistoryComponent),
        data: { roles: MGR }
      },

      // ── Reporting ───────────────────────────────────────────────────────
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
      },

      // ── Returns (Retours) ──────────────────────────────────────────────
      {
        path: 'returns',
        loadComponent: () =>
          import('./features/returns/returns-list/returns-list.component')
            .then(m => m.ReturnsListComponent),
        data: { roles: MGR }
      },
      {
        path: 'returns/new',
        loadComponent: () =>
          import('./features/returns/return-form/return-form.component')
            .then(m => m.ReturnFormComponent),
        data: { roles: MGR }
      },
      {
        path: 'returns/:id',
        loadComponent: () =>
          import('./features/returns/return-detail/return-detail.component')
            .then(m => m.ReturnDetailComponent),
        data: { roles: MGR }
      },

      // ── Claims (Réclamations) ──────────────────────────────────────────
      {
        path: 'claims',
        loadComponent: () =>
          import('./features/claims/claims-list/claims-list.component')
            .then(m => m.ClaimsListComponent),
        data: { roles: ALL }
      },
      {
        path: 'claims/new',
        loadComponent: () =>
          import('./features/claims/claim-form/claim-form.component')
            .then(m => m.ClaimFormComponent),
        data: { roles: ALL }
      },
      {
        path: 'claims/:id',
        loadComponent: () =>
          import('./features/claims/claim-detail/claim-detail.component')
            .then(m => m.ClaimDetailComponent),
        data: { roles: ALL }
      },

      // ── Service Tickets (SAV) ──────────────────────────────────────────
      {
        path: 'service-tickets',
        loadComponent: () =>
          import('./features/service-tickets/tickets-list/tickets-list.component')
            .then(m => m.TicketsListComponent),
        data: { roles: MGR }
      },
      {
        path: 'service-tickets/new',
        loadComponent: () =>
          import('./features/service-tickets/ticket-form/ticket-form.component')
            .then(m => m.TicketFormComponent),
        data: { roles: MGR }
      },
      {
        path: 'service-tickets/:id',
        loadComponent: () =>
          import('./features/service-tickets/ticket-detail/ticket-detail.component')
            .then(m => m.TicketDetailComponent),
        data: { roles: MGR }
      },

      // ── Delivery Notes (Bons de livraison) ─────────────────────────────
      {
        path: 'delivery-notes',
        loadComponent: () =>
          import('./features/delivery-notes/delivery-list/delivery-list.component')
            .then(m => m.DeliveryListComponent),
        data: { roles: MGR }
      },
      {
        path: 'delivery-notes/new',
        loadComponent: () =>
          import('./features/delivery-notes/delivery-form/delivery-form.component')
            .then(m => m.DeliveryFormComponent),
        data: { roles: MGR }
      },
      {
        path: 'delivery-notes/:id',
        loadComponent: () =>
          import('./features/delivery-notes/delivery-detail/delivery-detail.component')
            .then(m => m.DeliveryDetailComponent),
        data: { roles: MGR }
      },

      // ── Suppliers (Fournisseurs) ───────────────────────────────────────
      {
        path: 'suppliers',
        loadComponent: () =>
          import('./features/suppliers/suppliers-list/suppliers-list.component')
            .then(m => m.SuppliersListComponent),
        data: { roles: ADM }
      },
      {
        path: 'suppliers/new',
        loadComponent: () =>
          import('./features/suppliers/supplier-form/supplier-form.component')
            .then(m => m.SupplierFormComponent),
        data: { roles: ADM }
      },
      {
        path: 'suppliers/edit/:id',
        loadComponent: () =>
          import('./features/suppliers/supplier-form/supplier-form.component')
            .then(m => m.SupplierFormComponent),
        data: { roles: ADM }
      },

      // ── Purchase Orders (Bons de commande) ─────────────────────────────
      {
        path: 'purchase-orders',
        loadComponent: () =>
          import('./features/purchase-orders/po-list/po-list.component')
            .then(m => m.PoListComponent),
        data: { roles: MGR }
      },
      {
        path: 'purchase-orders/new',
        loadComponent: () =>
          import('./features/purchase-orders/po-form/po-form.component')
            .then(m => m.PoFormComponent),
        data: { roles: MGR }
      },
      {
        path: 'purchase-orders/:id',
        loadComponent: () =>
          import('./features/purchase-orders/po-detail/po-detail.component')
            .then(m => m.PoDetailComponent),
        data: { roles: MGR }
      },

      // ── Credit Notes (Avoirs) ──────────────────────────────────────────
      {
        path: 'credit-notes',
        loadComponent: () =>
          import('./features/credit-notes/credit-notes-list/credit-notes-list.component')
            .then(m => m.CreditNotesListComponent),
        data: { roles: MGR }
      },
      {
        path: 'credit-notes/new',
        loadComponent: () =>
          import('./features/credit-notes/credit-note-form/credit-note-form.component')
            .then(m => m.CreditNoteFormComponent),
        data: { roles: MGR }
      },
      {
        path: 'credit-notes/:id',
        loadComponent: () =>
          import('./features/credit-notes/credit-note-detail/credit-note-detail.component')
            .then(m => m.CreditNoteDetailComponent),
        data: { roles: MGR }
      },

      // ── Goods Receipts (Réceptions marchandises) ───────────────────────
      {
        path: 'goods-receipts',
        loadComponent: () =>
          import('./features/goods-receipts/receipts-list/receipts-list.component')
            .then(m => m.ReceiptsListComponent),
        data: { roles: MGR }
      },
      {
        path: 'goods-receipts/new',
        loadComponent: () =>
          import('./features/goods-receipts/receipt-form/receipt-form.component')
            .then(m => m.ReceiptFormComponent),
        data: { roles: MGR }
      }
    ]
  },

  { path: '**', redirectTo: '/dashboard' }
];
