// ── Role Types ──────────────────────────────────────────────────────────────

export type AppRole = 'Admin' | 'Manager' | 'Commercial';

// ── Predefined role sets (used in routes & nav) ─────────────────────────────

export const ALL_ROLES: AppRole[] = ['Admin', 'Manager', 'Commercial'];
export const MANAGER_UP: AppRole[] = ['Admin', 'Manager'];

// ── Navigation item with role restriction ───────────────────────────────────

export interface RoleNavItem {
  label: string;
  icon:  string;
  route: string;
  roles: AppRole[];
}

// ── Sidebar navigation – Application commerciale simplifiée ─────────────────

export const ROLE_NAV_ITEMS: RoleNavItem[] = [
  { label: 'Tableau de bord', icon: '📊', route: '/dashboard',   roles: ALL_ROLES  },
  { label: 'Dashboard commercial', icon: '📌', route: '/commercial-dashboard', roles: ALL_ROLES },
  { label: 'Partenaires',         icon: '👥', route: '/customers',   roles: ALL_ROLES  },
  { label: 'Devis en attente', icon: '🧾', route: '/quotes/en-attente', roles: ALL_ROLES  },
  { label: 'Devis clôturés', icon: '🧾', route: '/quotes/cloturees', roles: ALL_ROLES  },
  { label: 'Commandes en attente', icon: '🛒', route: '/orders/en-attente', roles: ALL_ROLES  },
  { label: 'Commandes clôturées', icon: '🛒', route: '/orders/cloturees', roles: ALL_ROLES  },
  { label: 'BL en attente', icon: '🚚', route: '/deliverynotes/en-attente', roles: ALL_ROLES },
  { label: 'BL clôturés', icon: '🚚', route: '/deliverynotes/cloturees', roles: ALL_ROLES },
  { label: 'Factures en attente', icon: '🧮', route: '/factures/en-attente', roles: ALL_ROLES  },
  { label: 'Factures clôturées', icon: '🧮', route: '/factures/cloturees', roles: ALL_ROLES  },
  { label: 'Encaissement',    icon: '💳', route: '/encaissement', roles: ALL_ROLES },
  { label: 'Avoirs en attente', icon: '↩️', route: '/creditnotes/en-attente', roles: ALL_ROLES  },
  { label: 'Avoirs clôturés', icon: '↩️', route: '/creditnotes/cloturees', roles: ALL_ROLES  },
  { label: 'Retours en attente', icon: '📦', route: '/returns/en-attente', roles: ALL_ROLES  },
  { label: 'Retours clôturés', icon: '📦', route: '/returns/cloturees', roles: ALL_ROLES  },
  { label: 'Catalogue',       icon: '🏷️', route: '/products',    roles: ALL_ROLES  },
  { label: 'Reporting',       icon: '📈', route: '/reporting',   roles: MANAGER_UP },
];
