import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const roleGuard: CanActivateFn = route => {
  const authService = inject(AuthService);
  const requiredRoles = (route.data['roles'] as string[] | undefined) ?? [];

  if (authService.hasAnyRole(requiredRoles)) {
    return true;
  }

  return inject(Router).createUrlTree(['/dashboard']);
};
