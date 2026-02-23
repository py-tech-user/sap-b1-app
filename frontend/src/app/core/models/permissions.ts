// ── Role Types ──────────────────────────────────────────────────────────────

export type AppRole = 'Admin' | 'Manager' | 'Commercial';

// ── Predefined role sets (used in routes & nav) ─────────────────────────────

export const ALL_ROLES: AppRole[] = ['Admin', 'Manager', 'Commercial'];
export const MANAGER_UP: AppRole[] = ['Admin', 'Manager'];
export const ADMIN_ONLY: AppRole[] = ['Admin'];

// ── Navigation item with role restriction ───────────────────────────────────

export interface RoleNavItem {
  label: string;
  icon:  string;
  route: string;
  roles: AppRole[];
}

// ── Sidebar navigation – only items the role can see are shown ──────────────

export const ROLE_NAV_ITEMS: RoleNavItem[] = [
  { label: 'Tableau de bord', icon: '📊', route: '/dashboard',        roles: ALL_ROLES  },
  { label: 'Clients',         icon: '👥', route: '/customers',        roles: ALL_ROLES  },
  { label: 'Commandes',       icon: '🛒', route: '/orders',           roles: ALL_ROLES  },
  { label: 'Articles',        icon: '🏷️', route: '/products',         roles: ALL_ROLES  },
  { label: 'Visites',         icon: '📋', route: '/visits',           roles: ALL_ROLES  },
  { label: 'Encaissements',   icon: '💰', route: '/payments',         roles: MANAGER_UP },
  { label: 'Retours',         icon: '📦', route: '/returns',          roles: MANAGER_UP },
  { label: 'Réclamations',    icon: '📝', route: '/claims',           roles: ALL_ROLES  },
  { label: 'SAV',             icon: '🔧', route: '/service-tickets',  roles: MANAGER_UP },
  { label: 'Bons livraison',  icon: '🚚', route: '/delivery-notes',   roles: MANAGER_UP },
  { label: 'Fournisseurs',    icon: '🏭', route: '/suppliers',        roles: ADMIN_ONLY },
  { label: 'Achats',          icon: '🛍️', route: '/purchase-orders',  roles: MANAGER_UP },
  { label: 'Avoirs',          icon: '💳', route: '/credit-notes',     roles: MANAGER_UP },
  { label: 'Réceptions',      icon: '📥', route: '/goods-receipts',   roles: MANAGER_UP },
  { label: 'Suivi terrain',   icon: '📡', route: '/tracking',         roles: MANAGER_UP },
  { label: 'Reporting',       icon: '📈', route: '/reporting',        roles: MANAGER_UP },
];
