import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/** Pages that need an account. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);

  return auth.token
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Login / register: no point showing them to someone already logged in. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.token ? inject(Router).createUrlTree(['/dashboard']) : true;
};
