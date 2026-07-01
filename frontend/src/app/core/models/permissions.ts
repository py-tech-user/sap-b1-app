export type AppRole = 'Admin' | 'Manager' | 'Commercial';

export const ALL_ROLES: AppRole[] = ['Admin', 'Manager', 'Commercial'];
export const MANAGER_UP: AppRole[] = ['Admin', 'Manager'];

export interface RoleNavItem {
  label: string;
  icon: string;
  route?: string;
  roles: AppRole[];
  children?: RoleNavItem[];
}

export const ROLE_NAV_ITEMS: RoleNavItem[] = [
  { label: 'Tableau de bord', icon: '', route: '/dashboard', roles: ALL_ROLES },
  { label: 'Partenaires', icon: '', route: '/customers', roles: ALL_ROLES },
  { label: 'Devis', icon: '', route: '/quotes', roles: ALL_ROLES },
  { label: 'Bon de commande', icon: '', route: '/orders', roles: ALL_ROLES },
  { label: 'Bon de livraison', icon: '', route: '/deliverynotes', roles: ALL_ROLES },
  { label: 'Facture', icon: '', route: '/factures', roles: ALL_ROLES },
  { label: 'Encaissement', icon: '', route: '/encaissement', roles: ALL_ROLES },
  { label: 'Avoir', icon: '', route: '/creditnotes', roles: ALL_ROLES },
  { label: 'Retour', icon: '', route: '/returns', roles: ALL_ROLES },
  { label: 'Catalogue', icon: '', route: '/products', roles: ALL_ROLES },
  {
    label: 'Reporting',
    icon: '',
    roles: ALL_ROLES,
    children: [
      { label: 'CA par famille', icon: '', route: '/reporting/ca-par-famille', roles: ALL_ROLES },
      { label: 'CA par article', icon: '', route: '/reporting/ca-par-article', roles: ALL_ROLES },
      { label: 'CA par client', icon: '', route: '/reporting/ca-par-client', roles: ALL_ROLES }
    ]
  }
];


