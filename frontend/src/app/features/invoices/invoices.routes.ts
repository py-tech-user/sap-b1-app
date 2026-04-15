import { Routes } from '@angular/router';
import { InvoicesPageComponent } from './invoices-page.component';
import { DocumentFormComponent } from '../commercial/document-form-page.component';
import { DocumentDetailComponent } from '../commercial/document-detail-page.component';

export const FACTURES_ROUTES: Routes = [
  {
    path: '',
    data: { resource: 'invoices' },
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'en-attente'
      },
      {
        path: 'en-attente',
        component: InvoicesPageComponent,
        data: { docPhase: 'open' }
      },
      {
        path: 'cloturees',
        component: InvoicesPageComponent,
        data: { docPhase: 'closed' }
      },
      {
        path: 'new',
        component: DocumentFormComponent
      },
      {
        path: ':id/edit',
        component: DocumentFormComponent
      },
      {
        path: ':id',
        component: DocumentDetailComponent
      }
    ]
  }
];
