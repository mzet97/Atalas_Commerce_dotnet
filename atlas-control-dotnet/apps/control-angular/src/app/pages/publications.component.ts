import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService, PublishApproval } from '../api/control-api.service';

/** Publicações de catálogo (four-eyes M2): solicitar → aprovar → despachar ao owner. */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page">
    <div class="page-head">
      <h1>Publicações</h1>
      <div class="row">
        <label>Estado
          <select [(ngModel)]="state" (change)="load()">
            @for (s of states; track s) { <option [value]="s">{{ s || 'todos' }}</option> }
          </select>
        </label>
        <a routerLink="/publications-new">Solicitar publicação</a>
      </div>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (loading()) { <p>Carregando…</p> } @else {
    <table>
      <thead><tr><th>ID</th><th>Produto</th><th>Versão</th><th>Mercado</th><th>Solicitado por</th><th>Estado</th><th></th></tr></thead>
      <tbody>
        @for (a of items(); track a.id) {
        <tr>
          <td class="mono">{{ a.id.slice(0, 8) }}</td>
          <td class="mono">{{ a.productId }}</td>
          <td class="mono">{{ a.expectedVersion }}</td>
          <td class="mono">{{ a.marketId }}</td>
          <td>{{ a.requestedBy }}</td>
          <td><span class="pill" [class]="pill(a.state)">{{ a.state }}</span></td>
          <td><a [routerLink]="['/publications', a.id]">abrir</a></td>
        </tr>
        } @empty { <tr><td colspan="7">Nenhuma publicação neste estado.</td></tr> }
      </tbody>
    </table>
    <p class="muted">Total: {{ total() }}</p>
    }
  </section>`,
})
export class PublicationsComponent implements OnInit {
  private api = inject(ControlApiService);
  states = ['', 'Requested', 'Approved', 'Rejected', 'Dispatched', 'Executed', 'Failed'];
  state = '';
  items = signal<PublishApproval[]>([]);
  total = signal(0);
  loading = signal(true);
  error = signal('');

  async ngOnInit(): Promise<void> { await this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const p = await this.api.listPublications(this.state || undefined);
      this.items.set(p.items);
      this.total.set(p.total);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.loading.set(false); }
  }

  pill(state: unknown): string {
    return { Approved: 'ok', Executed: 'ok', Rejected: 'bad', Failed: 'bad', Dispatched: 'warn', Requested: 'warn' }[String(state)] ?? '';
  }
}
