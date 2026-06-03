import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
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
    catchError(err => {
      if (err.status === 401 && !req.url.toLowerCase().includes('/auth/login') && token !== MOCK_TOKEN) {
        console.warn('Token rejete (401) -> deconnexion automatique.');
        authService.logout();
      }

      return throwError(() => err);
    })
  );
};


