import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Novo pedido de ajuste de estoque (operador; exige grant ativo no warehouse). */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page narrow">
    <p><a routerLink="/approvals">← voltar para a fila</a></p>
    <h1>Solicitar ajuste de estoque</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (created()) { <p class="ok">Pedido criado: <span class="mono">{{ created() }}</span> — aguarde decisão de um approver distinto.</p> }
    <div class="card">
      <div class="grid">
        <label>Warehouse <input [(ngModel)]="form.warehouseId" class="mono" placeholder="wh-1"></label>
        <label>SKU <input [(ngModel)]="form.skuId" class="mono"></label>
        <label>Delta (+/−) <input type="number" [(ngModel)]="form.delta"></label>
        <label>Motivo <input [(ngModel)]="form.reason"></label>
      </div>
      <button (click)="submit()" [disabled]="busy()">Solicitar (vai para four-eyes)</button>
    </div>
  </section>`,
})
export class RequestComponent {
  private api = inject(ControlApiService);
  error = signal('');
  created = signal('');
  busy = signal(false);
  form = { warehouseId: '', skuId: '', delta: 0, reason: '' };

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.created.set('');
    try {
      const r = await this.api.requestAdjust({ ...this.form });
      this.created.set(r.approvalId);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
