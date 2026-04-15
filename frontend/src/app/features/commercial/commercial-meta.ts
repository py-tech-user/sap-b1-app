import { CommercialResource } from '../../core/models/models';

export interface CommercialMeta {
  title: string;
  singular: string;
  createLabel: string;
  icon: string;
}

export const COMMERCIAL_META: Record<CommercialResource, CommercialMeta> = {
  quotes: {
    title: 'Devis',
    singular: 'devis',
    createLabel: 'Nouveau devis',
    icon: '🧾'
  },
  orders: {
    title: 'Bons de commande',
    singular: 'bon de commande',
    createLabel: 'Nouveau BC',
    icon: '🛒'
  },
  deliverynotes: {
    title: 'Bons de livraison',
    singular: 'bon de livraison',
    createLabel: 'Nouveau BL',
    icon: '🚚'
  },
  invoices: {
    title: 'Factures',
    singular: 'facture',
    createLabel: 'Nouvelle facture',
    icon: '🧾'
  },
  creditnotes: {
    title: 'Avoirs',
    singular: 'avoir',
    createLabel: 'Nouvel avoir',
    icon: '↩️'
  },
  returns: {
    title: 'Retours',
    singular: 'retour',
    createLabel: 'Nouveau retour',
    icon: '📦'
  }
};

export const STATUS_ACTIONS: Record<CommercialResource, { from: string; to: string; label: string }[]> = {
  quotes: [
    { from: 'pending', to: 'accepted', label: 'Accepter' },
    { from: 'pending', to: 'rejected', label: 'Rejeter' }
  ],
  orders: [
    // Les statuts BC viennent de l'API; on expose des transitions usuelles.
    { from: 'pending', to: 'confirmed', label: 'Confirmer' },
    { from: 'confirmed', to: 'inPreparation', label: 'Mettre en préparation' },
    { from: 'inPreparation', to: 'ready', label: 'Marquer prêt' }
  ],
  deliverynotes: [
    { from: 'inprogress', to: 'delivered', label: 'Marquer livré' }
  ],
  invoices: [
    { from: 'unpaid', to: 'paid', label: 'Marquer payée' }
  ],
  creditnotes: [],
  returns: [
    { from: 'pending', to: 'validated', label: 'Valider retour' }
  ]
};
