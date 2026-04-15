# Angular Static Files

Ce dossier contient les fichiers statiques de l'application Angular compilée.

## Déploiement

1. Compilez l'application Angular :
   ```bash
   cd frontend
   ng build --configuration=production
   ```

2. Copiez le contenu de `frontend/dist/frontend/browser/` vers ce dossier `wwwroot/`.

## Structure attendue

```
wwwroot/
├── index.html
├── main.js
├── polyfills.js
├── styles.css
├── assets/
│   └── ...
└── ...
```

Le backend servira automatiquement ces fichiers et redirigera les routes Angular vers `index.html`.
