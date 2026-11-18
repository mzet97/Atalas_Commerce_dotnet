import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { KeycloakService } from './keycloak.service';

/** Exige login; `data.roles` restringe por role do realm (ex.: ['approver']). */
export const authGuard: CanActivateFn = (route) => {
  const kc = inject(KeycloakService);
  const router = inject(Router);
  if (!kc.authenticated()) {
    router.navigate(['/login']);
    return false;
  }
  const roles = (route.data?.['roles'] ?? []) as string[];
  if (roles.length > 0 && !roles.some((r) => kc.hasRole(r))) {
    router.navigate(['/']);
    return false;
  }
  return true;
};
