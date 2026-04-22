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
        component: InvoicesPageComponent
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
