import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Ofertas (preço por SKU × mercado): grade, criar, reprice (CAS) e desativar. */
@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
  <section class="page">
    <div class="page-head">
      <h1>Ofertas</h1>
    </div>
    <p class="sub">Preço por SKU × mercado × canal. Efeito imediato na vitrine; tudo auditado.</p>
    <div class="row">
      <label>SKU <input [(ngModel)]="sku" (change)="load()" placeholder="filtrar por SKU"></label>
      <label>Mercado
        <select [(ngModel)]="market" (change)="load()">
          <option value="">todos</option>
          <option value="BR">BR</option>
          <option value="US">US</option>
          <option value="ES">ES</option>
        </select>
      </label>
    </div>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (ok()) { <p class="ok">{{ ok() }}</p> }
    @if (loading()) { <p>Carregando…</p> } @else {
    <table>
      <thead><tr><th>SKU</th><th>Mercado</th><th>Canal</th><th>Preço</th><th>Lista</th><th>Versão</th><th></th></tr></thead>
      <tbody>
        @for (o of items(); track o['offerId']) {
        <tr>
          <td class="mono">{{ o['skuId'] }}</td>
          <td>{{ o['marketId'] }}</td>
          <td>{{ o['channel'] }}</td>
          <td class="mono">{{ money(o) }}</td>
          <td class="mono">{{ moneyList(o) }}</td>
          <td class="mono">{{ o['version'] }}</td>
          <td>
            <button class="link" (click)="startReprice(o)" [disabled]="busy()">reprecar</button>
            <button class="link danger-text" (click)="deactivate(o)" [disabled]="busy()">desativar</button>
          </td>
        </tr>
        } @empty { <tr><td colspan="7">Nenhuma oferta ativa.</td></tr> }
      </tbody>
    </table>
    <p class="muted">Total: {{ total() }}</p>
    }
    @if (repricing()) {
    <div class="card">
      <h2>Reprecar {{ repricing()!['skuId'] }} ({{ repricing()!['marketId'] }}) — CAS v{{ repricing()!['version'] }}</h2>
      <div class="row">
        <label>Novo preço (centavos) <input type="number" [(ngModel)]="newPrice" min="1"></label>
      </div>
      <div class="row">
        <button (click)="confirmReprice()" [disabled]="busy()">Confirmar</button>
        <button (click)="repricing.set(null)" [disabled]="busy()">Cancelar</button>
      </div>
    </div>
    }
    <div class="card">
      <h2>Nova oferta</h2>
      <div class="grid">
        <label>SKU <input [(ngModel)]="form.skuId" placeholder="SKU-1"></label>
        <label>Mercado
          <select [(ngModel)]="form.marketId"><option value="BR">BR</option><option value="US">US</option><option value="ES">ES</option></select>
        </label>
        <label>Moeda <input [(ngModel)]="form.currency" placeholder="BRL" maxlength="3"></label>
        <label>Preço (centavos) <input type="number" [(ngModel)]="form.priceMinor" min="1"></label>
        <label>Lista (centavos) <input type="number" [(ngModel)]="form.listPriceMinor" min="1"></label>
      </div>
      <div class="row"><button (click)="create()" [disabled]="busy()">Criar oferta ativa</button></div>
    </div>
  </section>`,
})
export class OffersComponent implements OnInit {
  private api = inject(ControlApiService);
  sku = '';
  market = '';
  items = signal<Record<string, unknown>[]>([]);
  total = signal(0);
  loading = signal(true);
  busy = signal(false);
  error = signal('');
  ok = signal('');
  repricing = signal<Record<string, unknown> | null>(null);
  newPrice = 0;
  form = { skuId: '', marketId: 'BR', currency: 'BRL', priceMinor: 0, listPriceMinor: 0 };

  async ngOnInit(): Promise<void> { await this.load(); }

  money(o: Record<string, unknown>): string {
    return `${(Number(o['priceMinor']) / 100).toFixed(2)} ${o['currency']}`;
  }
  moneyList(o: Record<string, unknown>): string {
    return `${(Number(o['listPriceMinor']) / 100).toFixed(2)} ${o['currency']}`;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const r = await this.api.listOffers({ market: this.market || undefined, sku: this.sku || undefined });
      this.items.set(r.items);
      this.total.set(r.total);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.loading.set(false); }
  }

  async create(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      const r = await this.api.createOffer({
        skuId: this.form.skuId, marketId: this.form.marketId,
        currency: this.form.currency.toUpperCase(), priceMinor: Number(this.form.priceMinor),
        listPriceMinor: Number(this.form.listPriceMinor) || Number(this.form.priceMinor),
      });
      this.ok.set(`Oferta criada (${String(r.offerId).slice(0, 8)}).`);
      this.form = { skuId: '', marketId: 'BR', currency: 'BRL', priceMinor: 0, listPriceMinor: 0 };
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  startReprice(o: Record<string, unknown>): void {
    this.repricing.set(o);
    this.newPrice = Number(o['priceMinor']);
    this.error.set('');
    this.ok.set('');
  }

  async confirmReprice(): Promise<void> {
    const o = this.repricing();
    if (!o) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.repriceOffer(String(o['offerId']), Number(o['version']), Number(this.newPrice));
      this.ok.set('Preço atualizado.');
      this.repricing.set(null);
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async deactivate(o: Record<string, unknown>): Promise<void> {
    if (!confirm(`Desativar oferta ${o['skuId']} (${o['marketId']})? Sai da vitrine.`)) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.deactivateOffer(String(o['offerId']), Number(o['version']));
      this.ok.set('Oferta desativada.');
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
