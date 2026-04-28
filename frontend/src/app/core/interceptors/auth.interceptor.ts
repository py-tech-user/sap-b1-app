import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError, timeout } from 'rxjs';
import { AuthService } from '../services/auth.service';

const MOCK_TOKEN = 'mock-jwt-token-dev-only';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.token();

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
          error: { message: 'Le serveur ne repond pas (timeout 15s).' },
          url: req.url
        }));
      }

      if (err.status === 401 && !req.url.toLowerCase().includes('/auth/login') && token !== MOCK_TOKEN) {
        console.warn('Token rejete (401) -> deconnexion automatique.');
        authService.logout();
      }

      return throwError(() => err);
    })
  );
};
