import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Visão operacional: outboxes, posições e lag CQRS (SPEC-004 req.4/req.6). */
@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
  <section class="page">
    <div class="page-head"><h1>Operação</h1><button (click)="load()" [disabled]="busy()">Atualizar</button></div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    <h2>Outboxes (eventos ainda não publicados)</h2>
    <table>
      <thead><tr><th>Fila</th><th>Pendentes</th><th>Mais antigo (s)</th></tr></thead>
      <tbody>
        @for (row of outboxRows(); track row[0]) {
        <tr>
          <td class="mono">{{ row[0] }}</td>
          <td><span class="pill" [class]="(row[1] ?? 0) > 0 ? 'warn' : 'ok'">{{ row[1] ?? '—' }}</span></td>
          <td class="mono">{{ row[2] ?? '—' }}</td>
        </tr>
        } @empty { <tr><td colspan="3">Sem dados.</td></tr> }
      </tbody>
    </table>
    <h2>Lag CQRS (owner × projeção)</h2>
    <div class="row">
      <label>Warehouse <input [(ngModel)]="warehouseId" class="mono"></label>
      <label>SKU <input [(ngModel)]="skuId" class="mono"></label>
      <button (click)="checkLag()">Consultar</button>
    </div>
    @if (lag()) {
    <dl class="facts">
      <dt>Head do owner</dt><dd class="mono">{{ lag()!['ownerHeadVersion'] ?? '—' }}</dd>
      <dt>Versão projetada</dt><dd class="mono">{{ lag()!['projectedSourceVersion'] ?? '—' }}</dd>
      <dt>Lag (versões)</dt><dd class="mono">{{ lag()!['lag'] ?? '—' }}</dd>
    </dl>
    <p class="muted">Lag &gt; 0 = leitura defasada; decisões críticas sempre revalidam no owner.</p>
    }
  </section>`,
})
export class OpsComponent implements OnInit {
  private api = inject(ControlApiService);
  error = signal('');
  busy = signal(false);
  outboxRows = signal<[string, number | null, number | null][]>([]);
  lag = signal<Record<string, unknown> | null>(null);
  warehouseId = '';
  skuId = '';

  async ngOnInit(): Promise<void> { await this.load(); }

  async load(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const snap = await this.api.opsOverview();
      const boxes = (snap['outboxes'] ?? {}) as Record<string, Record<string, number>>;
      this.outboxRows.set(Object.entries(boxes).map(([k, v]) => [k, v?.['pending'] ?? null, v?.['oldestPendingAgeSeconds'] ?? null]));
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async checkLag(): Promise<void> {
    this.error.set('');
    try {
      this.lag.set(await this.api.cqrsLag(this.warehouseId, this.skuId));
    } catch (e) { this.error.set((e as Error).message); }
  }
}
