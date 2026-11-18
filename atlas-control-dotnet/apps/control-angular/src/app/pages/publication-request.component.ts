import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Nova solicitação de publicação (operador com role catalog_editor). */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page narrow">
    <p><a routerLink="/publications">← voltar para publicações</a></p>
    <h1>Solicitar publicação</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (created()) { <p class="ok">Pedido criado: <span class="mono">{{ created() }}</span> — aguarde decisão de um approver distinto.</p> }
    <div class="card">
      <div class="grid">
        <label>Produto (id) <input [(ngModel)]="form.productId" class="mono"></label>
        <label>Versão esperada <input type="number" [(ngModel)]="form.expectedVersion" class="mono"></label>
        <label>Mercado <input [(ngModel)]="form.marketId" class="mono" placeholder="BR"></label>
        <label>Justificativa <input [(ngModel)]="form.justification"></label>
      </div>
      <button (click)="submit()" [disabled]="busy()">Solicitar (vai para four-eyes)</button>
    </div>
  </section>`,
})
export class PublicationRequestComponent {
  private api = inject(ControlApiService);
  error = signal('');
  created = signal('');
  busy = signal(false);
  form = { productId: '', expectedVersion: 1, marketId: 'BR', justification: '' };

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.created.set('');
    try {
      const r = await this.api.requestPublish({ ...this.form });
      this.created.set(r.approvalId);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
