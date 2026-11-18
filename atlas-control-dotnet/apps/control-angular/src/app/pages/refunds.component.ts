import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Reembolsos four-eyes (SPEC-006 req.2): approver distinto emite o token HMAC. */
@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
  <section class="page">
    <h1>Reembolsos</h1>
    <p class="muted">O token só é emitido se você (approver) for distinto do solicitante. Válido por 10 minutos, só para intenções CAPTURED.</p>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <div class="card">
      <div class="grid">
        <label>Solicitante (sub) <input [(ngModel)]="form.requesterSub" class="mono"></label>
        <label>Pedido (order id) <input [(ngModel)]="form.orderId" class="mono"></label>
        <label>Valor (minor units) <input type="number" [(ngModel)]="form.amountMinor" class="mono"></label>
        <label>RMA <input [(ngModel)]="form.rmaId" class="mono"></label>
      </div>
      <button (click)="issue()" [disabled]="busy()">Emitir token de reembolso</button>
    </div>
    @if (token()) {
    <div class="card">
      <h2>Token emitido por {{ approver() }}</h2>
      <p class="mono wrap">{{ token() }}</p>
    </div>
    }
  </section>`,
})
export class RefundsComponent {
  private api = inject(ControlApiService);
  error = signal('');
  busy = signal(false);
  token = signal('');
  approver = signal('');
  form = { requesterSub: '', orderId: '', amountMinor: 0, rmaId: '' };

  async issue(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.token.set('');
    try {
      const r = await this.api.refundToken({ ...this.form });
      this.token.set(r.approvalToken);
      this.approver.set(r.approver);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
