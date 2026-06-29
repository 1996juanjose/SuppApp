import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_, state) => {
  const authService = inject(AuthService);

  if (authService.isAuthenticated()) {
    return true;
  }

  return inject(Router).createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url }
  });
};
