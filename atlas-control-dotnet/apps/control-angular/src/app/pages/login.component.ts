import { Component, OnInit, inject } from '@angular/core';
import { KeycloakService } from '../auth/keycloak.service';

/** Entrada: tenta o redirect OIDC sozinha (SSO); cai no botão se precisar de interação. */
@Component({
  standalone: true,
  template: `
  <div class="login-split">
    <div class="login-brand">
      <strong>Atlas Control</strong>
      <p>Console operacional do e-commerce — master data e aprovações four-eyes.</p>
      <ul>
        <li>Produtos, publicações e despublicações</li>
        <li>Ajustes de estoque com aprovação dupla</li>
        <li>Trilha de auditoria encadeada</li>
      </ul>
    </div>
    <div class="login-box">
      <h1>Entrar</h1>
      <p class="muted">Identidade do realm <span class="mono">atlas</span> (MFA para administradores).</p>
      <button (click)="manual()">Entrar com Atlas ID</button>
    </div>
  </div>`,
})
export class LoginComponent implements OnInit {
  kc = inject(KeycloakService);

  ngOnInit(): void {
    // Só retoma sozinho quando um guard interrompeu uma navegação (destino pendente);
    // visita manual (ex.: pós-logout) fica no botão — sem risco de loop.
    let pending: string | null = null;
    try {
      pending = sessionStorage.getItem('postLoginRedirect');
      sessionStorage.removeItem('postLoginRedirect');
    } catch { /* noop */ }
    if (pending && !this.kc.authenticated()) this.kc.login(pending);
  }

  manual(): void {
    try { sessionStorage.removeItem('postLoginRedirect'); } catch { /* noop */ }
    this.kc.login('/');
  }
}
