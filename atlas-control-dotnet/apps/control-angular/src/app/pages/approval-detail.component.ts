import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService, OperationApproval } from '../api/control-api.service';
import { KeycloakService } from '../auth/keycloak.service';

/** Detalhe da aprovação: decidir (approver ≠ requester) e despachar ao owner. */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page">
    <p><a routerLink="/approvals">← voltar para a fila</a></p>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (approval(); as a) {
    <h1>Aprovação <span class="mono">{{ a.id.slice(0, 8) }}</span>
      <span class="pill" [class]="a.state === 'Approved' || a.state === 'Executed' ? 'ok' : (a.state === 'Rejected' || a.state === 'Failed' ? 'bad' : 'warn')">{{ a.state }}</span>
    </h1>
    <dl class="facts">
      <dt>Ação</dt><dd class="mono">{{ a.action }}</dd>
      <dt>Assunto</dt><dd class="mono">{{ a.subject }}</dd>
      <dt>Solicitado por</dt><dd>{{ a.requestedBy }}</dd>
      <dt>Decidido por</dt><dd>{{ a.decidedBy ?? '—' }}</dd>
      <dt>Justificativa</dt><dd>{{ a.justification }}</dd>
    </dl>
    @if (a.state === 'Requested' && kc.hasRole('approver')) {
    <div class="card">
      <h2>Decidir (four-eyes: você não pode aprovar o próprio pedido)</h2>
      <textarea [(ngModel)]="reason" rows="2" placeholder="Motivo (obrigatório para rejeitar)"></textarea>
      <div class="row">
        <button (click)="decide('APPROVED')" [disabled]="busy()">Aprovar</button>
        <button class="danger" (click)="decide('REJECTED')" [disabled]="busy()">Rejeitar</button>
      </div>
    </div>
    }
    @if (a.state === 'Approved' && kc.hasRole('approver')) {
    <div class="card">
      <h2>Despachar ao owner</h2>
      <p class="muted">O owner revalida token, grant e estado. Despacho é idempotente por nonce.</p>
      <div class="row">
        <label>Request ID <input [(ngModel)]="requestId" class="mono"></label>
        <label>Delta <input type="number" [(ngModel)]="delta"></label>
        <button (click)="dispatch()" [disabled]="busy()">Despachar ajuste</button>
      </div>
    </div>
    }
    @if (result()) { <p class="ok">{{ result() }}</p> }
    }
  </section>`,
})
export class ApprovalDetailComponent implements OnInit {
  private api = inject(ControlApiService);
  private route = inject(ActivatedRoute);
  kc = inject(KeycloakService);
  approval = signal<OperationApproval | null>(null);
  error = signal('');
  result = signal('');
  busy = signal(false);
  reason = '';
  requestId = `web-${Date.now().toString(36)}`;
  delta = 0;

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    try {
      this.approval.set(await this.api.getApproval(id));
    } catch (e) { this.error.set((e as Error).message); }
  }

  async decide(decision: 'APPROVED' | 'REJECTED'): Promise<void> {
    const a = this.approval();
    if (!a) return;
    this.busy.set(true);
    this.error.set('');
    this.result.set('');
    try {
      const r = await this.api.decide(a.id, decision, this.reason || undefined);
      this.result.set(`Decisão registrada: ${r.state}`);
      this.approval.set(await this.api.getApproval(a.id));
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async dispatch(): Promise<void> {
    const a = this.approval();
    if (!a) return;
    this.busy.set(true);
    this.error.set('');
    this.result.set('');
    try {
      const r = await this.api.dispatch(a.id, { requestId: this.requestId, delta: this.delta });
      this.result.set(`Despacho concluído: ${r.state}`);
      this.approval.set(await this.api.getApproval(a.id));
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
