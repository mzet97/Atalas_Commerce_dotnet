import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService, PublishApproval } from '../api/control-api.service';
import { KeycloakService } from '../auth/keycloak.service';

/** Detalhe da publicação: decidir e despachar ao owner Catalog (revalida token). */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page">
    <p><a routerLink="/publications">← voltar para publicações</a></p>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (item(); as a) {
    <h1>Publicação <span class="mono">{{ id().slice(0, 8) }}</span>
      <span class="pill" [class]="pill(state())">{{ state() }}</span>
    </h1>
    <dl class="facts">
      <dt>Produto</dt><dd class="mono">{{ a.productId }}</dd>
      <dt>Versão esperada</dt><dd class="mono">{{ a.expectedVersion }}</dd>
      <dt>Mercado</dt><dd class="mono">{{ a.marketId }}</dd>
      <dt>Solicitado por</dt><dd>{{ a.requestedBy }}</dd>
      <dt>Decidido por</dt><dd>{{ a.decidedBy ?? '—' }}</dd>
      <dt>Justificativa</dt><dd>{{ a.justification }}</dd>
      @if (a.rejectionReason) { <dt>Motivo da rejeição</dt><dd>{{ a.rejectionReason }}</dd> }
    </dl>
    @if (state() === 'Requested' && kc.hasRole('approver')) {
    <div class="card">
      <h2>Decidir (four-eyes)</h2>
      <textarea [(ngModel)]="reason" rows="2" placeholder="Motivo (obrigatório para rejeitar)"></textarea>
      <div class="row">
        <button (click)="decide('APPROVED')" [disabled]="busy()">Aprovar</button>
        <button class="danger" (click)="decide('REJECTED')" [disabled]="busy()">Rejeitar</button>
      </div>
    </div>
    }
    @if (state() === 'Approved' && kc.hasRole('approver')) {
    <div class="card">
      <h2>Despachar ao owner Catalog</h2>
      <p class="muted">O owner revalida token HMAC, versão e autorização antes de publicar.</p>
      <button (click)="dispatch()" [disabled]="busy()">Despachar publicação</button>
    </div>
    }
    @if (result()) { <p class="ok">{{ result() }}</p> }
    }
  </section>`,
})
export class PublicationDetailComponent implements OnInit {
  private api = inject(ControlApiService);
  private route = inject(ActivatedRoute);
  kc = inject(KeycloakService);
  id = signal('');
  item = signal<PublishApproval | null>(null);
  error = signal('');
  result = signal('');
  busy = signal(false);
  reason = '';

  state(): string { return this.item()?.state ?? 'Requested'; }

  async ngOnInit(): Promise<void> {
    this.id.set(this.route.snapshot.paramMap.get('id') ?? '');
    await this.reload();
  }

  async reload(): Promise<void> {
    try {
      this.item.set(await this.api.getPublication(this.id()));
    } catch (e) { this.error.set((e as Error).message); }
  }

  async decide(decision: 'APPROVED' | 'REJECTED'): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.result.set('');
    try {
      const r = await this.api.decidePublish(this.id(), decision, this.reason || undefined);
      this.result.set(`Decisão registrada: ${r.state}`);
      await this.reload();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async dispatch(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.result.set('');
    try {
      const r = await this.api.dispatchPublish(this.id());
      this.result.set(`Despacho concluído: ${r.state}`);
      await this.reload();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  pill(s: string): string {
    return { Approved: 'ok', Executed: 'ok', Rejected: 'bad', Failed: 'bad', Dispatched: 'warn', Requested: 'warn' }[s] ?? '';
  }
}
