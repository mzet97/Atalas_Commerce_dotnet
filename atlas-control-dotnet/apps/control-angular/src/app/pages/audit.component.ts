import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ControlApiService, AuditEntry } from '../api/control-api.service';

/** Trilha de auditoria encadeada (REQ-ADM-003): consulta, verificação e exportação. */
@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
  <section class="page">
    <div class="page-head">
      <h1>Auditoria</h1>
      <div class="row">
        <button (click)="verify()" [disabled]="busy()">Verificar cadeia</button>
        <a [href]="csvUrl()" download>Exportar CSV</a>
      </div>
    </div>
    @if (verdict()) { <p [class]="verdict()?.valid ? 'ok' : 'error'">{{ verdictText() }}</p> }
    @if (error()) { <p class="error">{{ error() }}</p> }
    <label>Filtrar por ação <input [(ngModel)]="action" (change)="load()" placeholder="ex.: approval.decide"></label>
    <table>
      <thead><tr><th>Seq</th><th>Quando (UTC)</th><th>Ator</th><th>Ação</th><th>Decisão</th><th>Hash</th></tr></thead>
      <tbody>
        @for (e of items(); track e.seq) {
        <tr>
          <td class="mono">{{ e.seq }}</td>
          <td class="mono">{{ e.occurredAtUtc }}</td>
          <td>{{ e.actorSub }}</td>
          <td class="mono">{{ e.action }}</td>
          <td><span class="pill">{{ e.decision }}</span></td>
          <td class="mono">{{ e.hash.slice(0, 12) }}…</td>
        </tr>
        } @empty { <tr><td colspan="6">Sem entradas.</td></tr> }
      </tbody>
    </table>
    <p class="muted">Total: {{ total() }}</p>
  </section>`,
})
export class AuditComponent implements OnInit {
  private api = inject(ControlApiService);
  items = signal<AuditEntry[]>([]);
  total = signal(0);
  error = signal('');
  busy = signal(false);
  action = '';
  verdict = signal<{ valid: boolean; count: number; brokenAtSeq?: number; reason?: string } | null>(null);

  csvUrl(): string { return this.api.exportCsvUrl(); }

  async ngOnInit(): Promise<void> { await this.load(); }

  async load(): Promise<void> {
    this.error.set('');
    try {
      const p = await this.api.listAudit(this.action || undefined);
      this.items.set(p.items);
      this.total.set(p.total);
    } catch (e) { this.error.set((e as Error).message); }
  }

  async verify(): Promise<void> {
    this.busy.set(true);
    try {
      this.verdict.set(await this.api.verifyAudit());
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  verdictText(): string {
    const v = this.verdict();
    if (!v) return '';
    return v.valid ? `Cadeia íntegra — ${v.count} entradas verificadas.` : `Cadeia QUEBRADA em seq=${v.brokenAtSeq}: ${v.reason ?? ''}`;
  }
}
