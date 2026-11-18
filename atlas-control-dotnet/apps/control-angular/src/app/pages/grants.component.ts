import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ControlApiService, Grant } from '../api/control-api.service';

/** Grants ABAC (SPEC-004 req.2): conceder e revogar acesso por subject/ação/warehouse. */
@Component({
  standalone: true,
  imports: [FormsModule],
  template: `
  <section class="page">
    <h1>Grants (ABAC)</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (ok()) { <p class="ok">{{ ok() }}</p> }
    <div class="card">
      <h2>Conceder acesso</h2>
      <div class="grid">
        <label>Subject (sub) <input [(ngModel)]="form.subject" class="mono"></label>
        <label>Ação <input [(ngModel)]="form.action" class="mono" placeholder="inventory:adjust"></label>
        <label>Warehouse <input [(ngModel)]="form.warehouseId" class="mono" placeholder="wh-1 ou *"></label>
        <label>TTL (min) <input type="number" [(ngModel)]="form.ttlMinutes"></label>
      </div>
      <button (click)="create()" [disabled]="busy()">Conceder</button>
    </div>
    <table>
      <thead><tr><th>Subject</th><th>Ação</th><th>Warehouse</th><th>Estado</th><th>Expira (UTC)</th><th></th></tr></thead>
      <tbody>
        @for (g of grants(); track g.id) {
        <tr>
          <td class="mono">{{ g.subject }}</td>
          <td class="mono">{{ g.action }}</td>
          <td class="mono">{{ g.warehouseId }}</td>
          <td><span class="pill" [class]="g.state === 'Active' ? 'ok' : ''">{{ g.state }}</span></td>
          <td class="mono">{{ g.expiresAtUtc }}</td>
          <td>@if (g.state === 'Active') { <button class="danger" (click)="revoke(g.id)">revogar</button> }</td>
        </tr>
        } @empty { <tr><td colspan="6">Sem grants.</td></tr> }
      </tbody>
    </table>
  </section>`,
})
export class GrantsComponent implements OnInit {
  private api = inject(ControlApiService);
  grants = signal<Grant[]>([]);
  error = signal('');
  ok = signal('');
  busy = signal(false);
  form = { subject: '', action: 'inventory:adjust', warehouseId: '', ttlMinutes: 60 };

  async ngOnInit(): Promise<void> { await this.load(); }

  async load(): Promise<void> {
    try {
      const r = await this.api.listGrants();
      this.grants.set(r.grants);
    } catch (e) { this.error.set((e as Error).message); }
  }

  async create(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      await this.api.createGrant({ ...this.form });
      this.ok.set('Grant criado.');
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async revoke(id: string): Promise<void> {
    this.error.set('');
    this.ok.set('');
    try {
      await this.api.revokeGrant(id);
      this.ok.set('Grant revogado — reflete na próxima chamada do owner.');
      await this.load();
    } catch (e) { this.error.set((e as Error).message); }
  }
}
