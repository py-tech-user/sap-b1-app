import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { timeout, catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

const MOCK_TOKEN = 'mock-jwt-token-dev-only';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.token();

  // N'envoyer le header Authorization que si c'est un vrai JWT (pas le token mock)
  if (token && token !== MOCK_TOKEN) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(req).pipe(
    timeout(15000),
    catchError(err => {
      if (err.name === 'TimeoutError') {
        return throwError(() => ({
          status: 0,
          statusText: 'Timeout',
          error: { message: 'Le serveur ne répond pas (timeout 15s).' },
          url: req.url
        }));
      }
      // Token réel rejeté → déconnexion auto (pas pour le mock)
      if (err.status === 401 && !req.url.toLowerCase().includes('/auth/login') && token !== MOCK_TOKEN) {
        console.warn('Token rejeté (401) → déconnexion automatique.');
        authService.logout();
      }
      return throwError(() => err);
    })
  );
};
