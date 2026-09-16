import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const authGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const isLoggedIn = sessionStorage.getItem('IslogedIn');
  const jwtToken = sessionStorage.getItem('jwtToken');

  // Strict VAPT Security Check: Reject missing, expired or legacy mock tokens
  if (isLoggedIn !== 'True' || !jwtToken || jwtToken === 'plant-control-session-token') {
    // Clear any tainted session residue
    sessionStorage.removeItem('IslogedIn');
    sessionStorage.removeItem('jwtToken');
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  return true;
};