export type AppRole = 'Admin' | 'Manager' | 'Commercial';

export const ALL_ROLES: AppRole[] = ['Admin', 'Manager', 'Commercial'];
export const MANAGER_UP: AppRole[] = ['Admin', 'Manager'];

export interface RoleNavItem {
  label: string;
  icon: string;
  route: string;
  roles: AppRole[];
}

export const ROLE_NAV_ITEMS: RoleNavItem[] = [
  { label: 'Tableau de bord', icon: '', route: '/dashboard', roles: ALL_ROLES },
  { label: 'Partenaires', icon: '', route: '/customers', roles: ALL_ROLES },
  { label: 'Devis', icon: '', route: '/quotes', roles: ALL_ROLES },
  { label: 'Commandes', icon: '', route: '/orders', roles: ALL_ROLES },
  { label: 'BL', icon: '', route: '/deliverynotes', roles: ALL_ROLES },
  { label: 'Factures', icon: '', route: '/factures', roles: ALL_ROLES },
  { label: 'Encaissement', icon: '', route: '/encaissement', roles: ALL_ROLES },
  { label: 'Avoirs', icon: '', route: '/creditnotes', roles: ALL_ROLES },
  { label: 'Retours', icon: '', route: '/returns', roles: ALL_ROLES },
  { label: 'Catalogue', icon: '', route: '/products', roles: ALL_ROLES },
  { label: 'Reporting', icon: '', route: '/reporting', roles: MANAGER_UP }
];


