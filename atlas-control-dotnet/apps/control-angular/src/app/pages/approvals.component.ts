import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService, OperationApproval } from '../api/control-api.service';

const STATES = ['', 'Requested', 'Approved', 'Rejected', 'Dispatched', 'Executed', 'Failed'];

/** Fila de aprovações four-eyes (SPEC-004 req.1). */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page">
    <div class="page-head">
      <h1>Aprovações</h1>
      <label>Estado
        <select [(ngModel)]="state" (change)="load()">
          @for (s of states; track s) { <option [value]="s">{{ s || 'todos' }}</option> }
        </select>
      </label>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (loading()) { <p>Carregando…</p> } @else {
    <table>
      <thead><tr><th>ID</th><th>Ação</th><th>Assunto</th><th>Solicitado por</th><th>Estado</th><th></th></tr></thead>
      <tbody>
        @for (a of items(); track a.id) {
        <tr>
          <td class="mono">{{ short(a.id) }}</td>
          <td class="mono">{{ a.action }}</td>
          <td class="mono">{{ a.subject }}</td>
          <td>{{ a.requestedBy }}</td>
          <td><span class="pill" [class]="pill(a.state)">{{ a.state }}</span></td>
          <td><a [routerLink]="['/approvals', a.id]">abrir</a></td>
        </tr>
        } @empty { <tr><td colspan="6">Nenhuma aprovação neste estado.</td></tr> }
      </tbody>
    </table>
    <p class="muted">Total: {{ total() }}</p>
    }
  </section>`,
})
export class ApprovalsComponent implements OnInit {
  private api = inject(ControlApiService);
  states = STATES;
  state = '';
  items = signal<OperationApproval[]>([]);
  total = signal(0);
  loading = signal(true);
  error = signal('');

  async ngOnInit(): Promise<void> { await this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const p = await this.api.listApprovals(this.state || undefined);
      this.items.set(p.items);
      this.total.set(p.total);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.loading.set(false); }
  }

  short(id: unknown): string { return String(id).slice(0, 8); }
  pill(state: unknown): string {
    return { Approved: 'ok', Executed: 'ok', Rejected: 'bad', Failed: 'bad', Dispatched: 'warn', Requested: 'warn' }[String(state)] ?? '';
  }
}
