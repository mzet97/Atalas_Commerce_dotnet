import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

const STATES = ['', 'Draft', 'PendingApproval', 'Published', 'Unpublished'];

/** Grade de produtos (master data): busca, filtro por estado e atalho de criação. */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page">
    <div class="page-head">
      <h1>Produtos</h1>
      <a routerLink="/products-new">Novo produto</a>
    </div>
    <div class="row">
      <label>Estado
        <select [(ngModel)]="state" (change)="load()">
          @for (s of states; track s) { <option [value]="s">{{ s || 'todos' }}</option> }
        </select>
      </label>
      <label>Busca <input [(ngModel)]="search" (change)="load()" placeholder="título ou SKU"></label>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (loading()) { <p>Carregando…</p> } @else {
    <table>
      <thead><tr><th>ID</th><th>Título</th><th>Mercado</th><th>SKUs</th><th>Estado</th><th>Versão</th><th></th></tr></thead>
      <tbody>
        @for (p of items(); track p['productId']) {
        <tr>
          <td class="mono">{{ short(p['productId']) }}</td>
          <td>{{ title(p) }}</td>
          <td>{{ p['marketId'] }}</td>
          <td class="mono">{{ p['skuCount'] }}</td>
          <td><span class="pill" [class]="pill(p['status'])">{{ p['status'] }}</span></td>
          <td class="mono">{{ p['version'] }}</td>
          <td><a [routerLink]="['/products', p['productId']]">abrir</a></td>
        </tr>
        } @empty { <tr><td colspan="7">Nenhum produto.</td></tr> }
      </tbody>
    </table>
    <p class="muted">Total: {{ total() }}</p>
    }
  </section>`,
})
export class ProductsComponent implements OnInit {
  private api = inject(ControlApiService);
  states = STATES;
  state = '';
  search = '';
  items = signal<Record<string, unknown>[]>([]);
  total = signal(0);
  loading = signal(true);
  error = signal('');

  async ngOnInit(): Promise<void> { await this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const r = await this.api.listProducts({ status: this.state || undefined, search: this.search || undefined });
      this.items.set(r.items);
      this.total.set(r.total);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.loading.set(false); }
  }

  short(id: unknown): string { return String(id).slice(0, 8); }
  title(p: Record<string, unknown>): string {
    const titles = p['titles'] as { locale?: string; title?: string }[] | undefined;
    return titles?.find((t) => t.locale === 'pt-BR')?.title ?? titles?.[0]?.title ?? '—';
  }
  pill(s: unknown): string {
    return { Published: 'ok', Executed: 'ok', Unpublished: '', Rejected: 'bad', Failed: 'bad', Dispatched: 'warn', Requested: 'warn', PendingApproval: 'warn', Draft: '' }[String(s)] ?? '';
  }
}
