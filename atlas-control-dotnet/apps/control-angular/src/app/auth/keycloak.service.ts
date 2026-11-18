import { Injectable, signal } from '@angular/core';
import Keycloak from 'keycloak-js';

const KEYCLOAK_URL = 'https://keycloak.home.arpa';
const REALM = 'atlas';
const CLIENT_ID = 'control-angular';

function rolesFromToken(token: string): string[] {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    return payload?.realm_access?.roles ?? [];
  } catch {
    return [];
  }
}

/** Sessão OIDC (PKCE) contra o realm `atlas`. O host valida o JWT e as roles. */
@Injectable({ providedIn: 'root' })
export class KeycloakService {
  private kc = new Keycloak({ url: KEYCLOAK_URL, realm: REALM, clientId: CLIENT_ID });
  readonly ready = signal(false);
  readonly authenticated = signal(false);
  readonly username = signal('');
  readonly roles = signal<string[]>([]);

  async init(): Promise<void> {
    try {
      const ok = await this.kc.init({
        onLoad: 'check-sso',
        pkceMethod: 'S256',
        checkLoginIframe: false,
        redirectUri: window.location.origin + '/',
      });
      this.apply(ok);
      if (ok) this.scheduleRefresh();
    } finally {
      this.ready.set(true);
    }
  }

  login(): void {
    this.kc.login({ redirectUri: window.location.origin + '/' });
  }

  logout(): void {
    this.kc.logout({ redirectUri: window.location.origin + '/login' });
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  async token(): Promise<string> {
    try {
      await this.kc.updateToken(30);
    } catch {
      this.login();
      throw new Error('sessão expirada — redirecionando para login');
    }
    return this.kc.token ?? '';
  }

  private apply(ok: boolean): void {
    this.authenticated.set(ok);
    if (ok && this.kc.token) {
      this.roles.set(rolesFromToken(this.kc.token));
      this.username.set(this.kc.tokenParsed?.['preferred_username'] ?? this.kc.subject ?? '');
    }
  }

  private scheduleRefresh(): void {
    setInterval(() => this.kc.updateToken(60).catch(() => undefined), 45000);
  }
}
