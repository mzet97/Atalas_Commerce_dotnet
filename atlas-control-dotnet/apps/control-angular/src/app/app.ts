import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { KeycloakService } from './auth/keycloak.service';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  template: `
  <div class="shell">
    <aside class="side">
      <div class="side-brand"><strong>Atlas Control</strong><small>console operacional</small></div>
      @if (kc.authenticated()) {
      <nav>
        <div class="side-sec"><span>VISÃO GERAL</span>
          <a routerLink="/dashboard" routerLinkActive="active">Painel</a>
        </div>
        <div class="side-sec"><span>CATÁLOGO</span>
          <a routerLink="/products" routerLinkActive="active">Produtos</a>
          <a routerLink="/products-new" routerLinkActive="active">Novo produto</a>
          <a routerLink="/publications" routerLinkActive="active">Publicações</a>
          <a routerLink="/publications-new" routerLinkActive="active">Solicitar publicação</a>
          <a routerLink="/offers" routerLinkActive="active">Ofertas</a>
        </div>
        <div class="side-sec"><span>APROVAÇÕES</span>
          <a routerLink="/approvals" routerLinkActive="active">Ajustes de estoque</a>
          <a routerLink="/approvals-new" routerLinkActive="active">Solicitar ajuste</a>
          <a routerLink="/receive-new" routerLinkActive="active">Receber mercadoria</a>
          @if (kc.hasRole('approver')) { <a routerLink="/refunds" routerLinkActive="active">Reembolsos</a> }
        </div>
        <div class="side-sec"><span>PEDIDOS</span>
          <a routerLink="/orders" routerLinkActive="active">Pedidos</a>
        </div>
        <div class="side-sec"><span>SISTEMA</span>
          <a routerLink="/audit" routerLinkActive="active">Auditoria</a>
          @if (kc.hasRole('iam_admin')) { <a routerLink="/grants" routerLinkActive="active">Grants</a> }
          <a routerLink="/ops" routerLinkActive="active">Operação</a>
        </div>
      </nav>
      <div class="side-user">{{ kc.username() }} <button class="link" (click)="kc.logout()">sair</button></div>
      } @else if (kc.ready()) {
      <nav><div class="side-sec"><a routerLink="/login" routerLinkActive="active">entrar</a></div></nav>
      }
    </aside>
    <div class="content">
      <main><router-outlet /></main>
    </div>
  </div>`,
})
export class App {
  kc = inject(KeycloakService);
}
