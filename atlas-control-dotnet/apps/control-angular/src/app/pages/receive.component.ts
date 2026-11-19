import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Recebimento de mercadoria (entrada no estoque via four-eyes: solicita → decide → despacha). */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page narrow">
    <p><a routerLink="/approvals">← voltar para a fila</a></p>
    <h1>Receber mercadoria</h1>
    <p class="sub">Entrada de estoque com four-eyes (solicitante ≠ approver, grant <span class="mono">inventory:receive</span>).</p>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (created()) { <p class="ok">Pedido criado: <span class="mono">{{ created() }}</span> — aguarde decisão de um approver distinto.</p> }
    <div class="card">
      <div class="grid">
        <label>Warehouse <input [(ngModel)]="form.warehouseId" class="mono" placeholder="wh-1"></label>
        <label>SKU <input [(ngModel)]="form.skuId" class="mono"></label>
        <label>Quantidade (+) <input type="number" [(ngModel)]="form.quantity" min="1"></label>
        <label>Motivo <input [(ngModel)]="form.reason" placeholder="nota fiscal / fornecedor"></label>
      </div>
      <button (click)="submit()" [disabled]="busy()">Solicitar (vai para four-eyes)</button>
    </div>
  </section>`,
})
export class ReceiveComponent {
  private api = inject(ControlApiService);
  error = signal('');
  created = signal('');
  busy = signal(false);
  form = { warehouseId: '', skuId: '', quantity: 1, reason: '' };

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.created.set('');
    try {
      const r = await this.api.requestReceive({ ...this.form });
      this.created.set(r.approvalId);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
