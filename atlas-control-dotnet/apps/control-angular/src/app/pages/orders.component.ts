import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Pedidos: grade da view + transições de fulfillment (avançar/cancelar). */
@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
  <section class="page">
    <div class="page-head"><h1>Pedidos</h1></div>
    <p class="sub">Grade da view order_tracking_view; transições via fulfillment (owner revalida).</p>
    <div class="row">
      <label>Estado
        <select [(ngModel)]="status" (change)="load()">
          <option value="">todos</option>
          <option value="Confirmed">Confirmed</option>
          <option value="Picking">Picking</option>
          <option value="Packed">Packed</option>
          <option value="Shipped">Shipped</option>
          <option value="Delivered">Delivered</option>
          <option value="Cancelled">Cancelled</option>
          <option value="PaymentFailed">PaymentFailed</option>
        </select>
      </label>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (ok()) { <p class="ok">{{ ok() }}</p> }
    @if (loading()) { <p>Carregando…</p> } @else {
    <table>
      <thead><tr><th>Pedido</th><th>Cliente</th><th>Estado</th><th>Total</th><th>Versão</th><th></th></tr></thead>
      <tbody>
        @for (o of items(); track o['orderId']) {
        <tr [class.selected]="selected()?.['orderId'] === o['orderId']">
          <td class="mono">{{ short(o['orderId']) }}</td>
          <td class="mono wrap">{{ o['customerSub'] }}</td>
          <td><span class="pill">{{ o['status'] }}</span></td>
          <td class="mono">{{ money(o) }}</td>
          <td class="mono">{{ o['sourceVersion'] }}</td>
          <td><button class="link" (click)="open(o)">abrir</button></td>
        </tr>
        } @empty { <tr><td colspan="6">Nenhum pedido.</td></tr> }
      </tbody>
    </table>
    }
    @if (selected(); as s) {
    <div class="card">
      <h2>Pedido <span class="mono">{{ s['orderId'] }}</span></h2>
      <dl class="facts">
        <dt>Cliente</dt><dd class="mono wrap">{{ s['customerSub'] }}</dd>
        <dt>Estado (view)</dt><dd>{{ s['status'] }}</dd>
        <dt>Total</dt><dd class="mono">{{ money(s) }} {{ s['currency'] }}</dd>
        <dt>Fulfillment</dt><dd>{{ fulfillment()?.['state'] ?? '—' }} <span class="muted">tracking: {{ fulfillment()?.['tracking'] ?? '—' }}</span></dd>
      </dl>
      @if (fulfillmentError()) { <p class="error">{{ fulfillmentError() }}</p> }
      <div class="row">
        <button (click)="start()" [disabled]="busy()">Iniciar fulfillment</button>
        <label>Avançar para
          <select [(ngModel)]="toState">
            <option value="Picking">Picking</option>
            <option value="Packed">Packed</option>
            <option value="Shipped">Shipped</option>
            <option value="Delivered">Delivered</option>
          </select>
        </label>
        <button (click)="advance()" [disabled]="busy()">Avançar</button>
        <button class="danger" (click)="cancel()" [disabled]="busy()">Cancelar pedido</button>
      </div>
    </div>
    }
  </section>`,
  styles: ['tr.selected td { background: #eef4fa; }'],
})
export class OrdersComponent implements OnInit {
  private api = inject(ControlApiService);
  status = '';
  items = signal<Record<string, unknown>[]>([]);
  selected = signal<Record<string, unknown> | null>(null);
  fulfillment = signal<Record<string, unknown> | null>(null);
  fulfillmentError = signal('');
  toState = 'Picking';
  loading = signal(true);
  busy = signal(false);
  error = signal('');
  ok = signal('');

  async ngOnInit(): Promise<void> { await this.load(); }

  short(id: unknown): string { return String(id).slice(0, 8); }
  money(o: Record<string, unknown>): string {
    return `${(Number(o['totalMinor']) / 100).toFixed(2)}`;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    this.selected.set(null);
    try {
      const r = await this.api.listOrders(this.status || undefined);
      this.items.set(r.items);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.loading.set(false); }
  }

  async open(o: Record<string, unknown>): Promise<void> {
    this.selected.set(o);
    this.fulfillment.set(null);
    this.fulfillmentError.set('');
    try {
      const r = await this.api.getOrder(String(o['orderId']));
      const f = r['fulfillment'] as Record<string, unknown> | null;
      this.fulfillment.set(f);
      if (!f) this.fulfillmentError.set('Sem fulfillment para este pedido (ainda não despachado ao centro).');
    } catch (e) { this.fulfillmentError.set((e as Error).message); }
  }

  async advance(): Promise<void> {
    const s = this.selected();
    if (!s) return;
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      const r = await this.api.advanceOrder(String(s['orderId']), this.toState);
      this.ok.set(`Pedido ${r.state}.`);
      await this.open(this.selected()!);
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async start(): Promise<void> {
    const s = this.selected();
    if (!s) return;
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      const r = await this.api.startOrder(String(s['orderId']));
      this.ok.set(`Fulfillment iniciado: ${r.state}.`);
      await this.open(this.selected()!);
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async cancel(): Promise<void> {
    const s = this.selected();
    if (!s || !confirm('Cancelar este pedido? (só dentro da janela: antes de expedido)')) return;
    this.busy.set(true);
    this.error.set('');
    try {
      const r = await this.api.cancelOrder(String(s['orderId']));
      this.ok.set(`Pedido ${r.state}.`);
      await this.open(this.selected()!);
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
