import { Component, inject } from '@angular/core';
import { KeycloakService } from '../auth/keycloak.service';

/** Entrada: mostra o botão de login quando a sessão ainda não existe. */
@Component({
  standalone: true,
  template: `
  <section class="page narrow center">
    <h1>Atlas Control</h1>
    <p class="muted">Console administrativo — entrada com identidade do realm <span class="mono">atlas</span> (MFA para administradores).</p>
    <button (click)="kc.login()">Entrar com Atlas ID</button>
  </section>`,
})
export class LoginComponent {
  kc = inject(KeycloakService);
}
