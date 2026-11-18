import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { KeycloakService } from './auth/keycloak.service';

@Component({
  imports: [RouterOutlet, RouterLink],
  selector: 'app-root',
  template: `
  <header class="topbar">
    <span class="brand">Atlas Control</span>
    @if (kc.authenticated()) {
    <nav>
      <a routerLink="/products">Produtos</a>
      <a routerLink="/approvals">Aprovações</a>
      <a routerLink="/approvals-new">Solicitar ajuste</a>
      <a routerLink="/publications">Publicações</a>
      <a routerLink="/audit">Auditoria</a>
      @if (kc.hasRole('iam_admin')) { <a routerLink="/grants">Grants</a> }
      @if (kc.hasRole('approver')) { <a routerLink="/refunds">Reembolsos</a> }
      <a routerLink="/ops">Operação</a>
    </nav>
    <span class="user">{{ kc.username() }} <button class="link" (click)="kc.logout()">sair</button></span>
    } @else if (kc.ready()) {
    <nav><a routerLink="/login">entrar</a></nav>
    }
  </header>
  <main><router-outlet /></main>`,
})
export class App {
  kc = inject(KeycloakService);
}
