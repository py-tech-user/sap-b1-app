import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Functional route guard that checks the user's role against
 * the `data.roles` array defined on each route.
 *
 * Usage in routes:
 *   { path: 'xxx', loadComponent: ..., canActivate: [roleGuard], data: { roles: ['Admin','Manager'] } }
 *
 * If `data.roles` is absent or empty the route is accessible to all authenticated users.
 */
export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth   = inject(AuthService);
  const router = inject(Router);

  const requiredRoles = route.data['roles'] as string[] | undefined;

  // No role restriction → allow
  if (!requiredRoles || requiredRoles.length === 0) {
    return true;
  }

  if (auth.hasRole(requiredRoles)) {
    return true;
  }

  // Insufficient role → redirect to dashboard
  return router.createUrlTree(['/dashboard']);
};
