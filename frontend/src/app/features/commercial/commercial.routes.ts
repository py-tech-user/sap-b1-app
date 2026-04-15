import { Routes } from '@angular/router';
import { DocumentListComponent } from './document-list/document-list.component';
import { DocumentFormComponent } from './document-form-page.component';
import { DocumentDetailComponent } from './document-detail-page.component';

export const COMMERCIAL_DOCUMENT_CHILDREN: Routes = [
  {
    path: '',
    component: DocumentListComponent
  },
  {
    path: 'en-attente',
    component: DocumentListComponent,
    data: { docPhase: 'open' }
  },
  {
    path: 'cloturees',
    component: DocumentListComponent,
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
];

export const QUOTES_ROUTES: Routes = [
  {
    path: '',
    data: { resource: 'quotes' },
    children: COMMERCIAL_DOCUMENT_CHILDREN
  }
];

export const ORDERS_ROUTES: Routes = [
  {
    path: '',
    data: { resource: 'orders' },
    children: COMMERCIAL_DOCUMENT_CHILDREN
  }
];

export const DELIVERY_NOTES_ROUTES: Routes = [
  {
    path: '',
    data: { resource: 'deliverynotes' },
    children: COMMERCIAL_DOCUMENT_CHILDREN
  }
];

export const CREDIT_NOTES_ROUTES: Routes = [
  {
    path: '',
    data: { resource: 'creditnotes' },
    children: COMMERCIAL_DOCUMENT_CHILDREN
  }
];

export const RETURNS_ROUTES: Routes = [
  {
    path: '',
    data: { resource: 'returns' },
    children: COMMERCIAL_DOCUMENT_CHILDREN
  }
];
