import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { KeycloakService } from './keycloak.service';

/** Exige login; `data.roles` restringe por role do realm (ex.: ['approver']).
 * Sem sessão (ex.: reload pós-redirect), guarda o destino e manda para /login,
 * que refaz o redirect OIDC — o SSO do Keycloak completa sem interação. */
export const authGuard: CanActivateFn = (route, state) => {
  const kc = inject(KeycloakService);
  const router = inject(Router);
  if (!kc.authenticated()) {
    try { sessionStorage.setItem('postLoginRedirect', state.url); } catch { /* noop */ }
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
