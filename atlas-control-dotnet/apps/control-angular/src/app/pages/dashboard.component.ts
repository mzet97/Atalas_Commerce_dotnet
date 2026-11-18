import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ControlApiService, type OperationApproval, type PublishApproval } from '../api/control-api.service';

/** Painel: fila de trabalho do operador (pendências four-eyes + estado do catálogo). */
@Component({
  standalone: true,
  imports: [RouterLink],
  template: `
  <section class="page">
    <h1>Painel</h1>
    <p class="sub">Fila de trabalho operacional — tudo que aguarda decisão ou despacho.</p>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (loading()) { <p>Carregando…</p> } @else {
    <div class="kpis">
      <div class="kpi"><div class="n">{{ approvals }}</div><div class="l">ajustes aguardando decisão</div><a routerLink="/approvals">abrir fila</a></div>
      <div class="kpi"><div class="n">{{ publications }}</div><div class="l">publicações aguardando</div><a routerLink="/publications">abrir fila</a></div>
      <div class="kpi"><div class="n">{{ unpublishes }}</div><div class="l">despublicações aguardando</div><a routerLink="/products">ver produtos</a></div>
      <div class="kpi"><div class="n">{{ published }}</div><div class="l">produtos publicados (de {{ products }})</div><a routerLink="/products">ver catálogo</a></div>
      <div class="kpi"><div class="n">{{ audit() }}</div><div class="l">cadeia de auditoria</div><a routerLink="/audit">verificar</a></div>
    </div>
    <div class="dash-cols">
      <div class="panel">
        <h2>Ajustes pendentes <a routerLink="/approvals">ver todos</a></h2>
        @if (pendingApprovals().length === 0) { <p class="more">Nenhum ajuste pendente.</p> }
        <ul>
          @for (a of pendingApprovals(); track a.id) {
          <li><a [routerLink]="['/approvals', a.id]">{{ a.action || 'ajuste' }}</a>
            <span class="muted"> · {{ a.subject }} · {{ a.requestedBy }}</span></li>
          }
        </ul>
      </div>
      <div class="panel">
        <h2>Publicações pendentes <a routerLink="/publications">ver todas</a></h2>
        @if (pendingPublications().length === 0) { <p class="more">Nenhuma publicação pendente.</p> }
        <ul>
          @for (p of pendingPublications(); track p.id) {
          <li><a [routerLink]="['/publications', p.id]">{{ short(p.productId) }}</a>
            <span class="muted"> · {{ p.marketId }} · {{ p.requestedBy }}</span></li>
          }
        </ul>
      </div>
    </div>
    }
  </section>`,
})
export class DashboardComponent implements OnInit {
  private api = inject(ControlApiService);
  approvals = 0;
  publications = 0;
  unpublishes = 0;
  published = 0;
  products = 0;
  pendingApprovals = signal<OperationApproval[]>([]);
  pendingPublications = signal<PublishApproval[]>([]);
  audit = signal('—');
  loading = signal(true);
  error = signal('');

  async ngOnInit(): Promise<void> {
    try {
      const [ap, pub, unp, all, pubd] = await Promise.all([
        this.api.listApprovals('Requested').catch(() => null),
        this.api.listPublications('Requested').catch(() => null),
        this.api.listCatalogApprovals('Requested').catch(() => null),
        this.api.listProducts({}).catch(() => null),
        this.api.listProducts({ status: 'Published' }).catch(() => null),
      ]);
      this.approvals = ap?.total ?? 0;
      this.pendingApprovals.set((ap?.items ?? []).slice(0, 5));
      this.publications = pub?.total ?? 0;
      this.pendingPublications.set((pub?.items ?? []).slice(0, 5));
      this.unpublishes = unp?.total ?? 0;
      this.products = all?.total ?? 0;
      this.published = pubd?.total ?? 0;
      const v = await this.api.verifyAudit().catch(() => null);
      this.audit.set(v ? (v.valid ? `válida (${v.count})` : 'QUEBRADA') : '—');
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.loading.set(false); }
  }

  short(id: string): string { return id.slice(0, 8); }
}
