import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService, OperationApproval } from '../api/control-api.service';
import { KeycloakService } from '../auth/keycloak.service';

/** Ficha do produto: ver, editar, submeter, apagar (rascunho) e despublicar (four-eyes inline). */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page">
    <p><a routerLink="/products">← voltar para produtos</a></p>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (ok()) { <p class="ok">{{ ok() }}</p> }
    @if (item(); as p) {
    <h1>Ficha <span class="mono">{{ id().slice(0, 8) }}</span>
      <span class="pill">{{ p['status'] }}</span>
      <span class="muted">v{{ p['version'] }}</span>
    </h1>
    <dl class="facts">
      @for (t of titles(p); track t.locale) {
      <dt>{{ t.locale }}</dt><dd>{{ t.title }}</dd>
      }
      <dt>SKUs</dt><dd class="mono">{{ skuCodes(p) }}</dd>
    </dl>
    @if (editable()) {
    <div class="card">
      <h2>Editar rascunho (CAS v{{ p['version'] }})</h2>
      <div class="grid">
        <label>Título (pt-BR) <input [(ngModel)]="form.ptBR"></label>
        <label>Título (en-US) <input [(ngModel)]="form.enUS"></label>
        <label>Título (es-ES) <input [(ngModel)]="form.esES"></label>
      </div>
      <div class="row">
        <button (click)="save()" [disabled]="busy()">Salvar</button>
        <button (click)="submit()" [disabled]="busy()">Submeter para aprovação</button>
        <button class="danger" (click)="remove()" [disabled]="busy()">Apagar rascunho</button>
      </div>
    </div>
    }
    @if (p['status'] === 'PendingApproval') {
    <p class="muted">Aguardando decisão na fila de <a routerLink="/publications">Publicações</a> (four-eyes).</p>
    }
    @if (p['status'] === 'Published') {
    <div class="card">
      <h2>Tirar da venda (four-eyes)</h2>
      @for (u of unpublishQueue(); track u.id) {
      <div class="unpublish-row"><span class="mono">{{ u.id.slice(0, 8) }}</span> — {{ u.state }}, solicitado por {{ u.requestedBy }}@if (u.decidedBy) {<span>, decidido por {{ u.decidedBy }}</span>}.</div>
      @if (u.state === 'Requested' && kc.hasRole('approver')) {
      <div class="row">
        <button (click)="decideUnpublish(u.id, 'APPROVED')" [disabled]="busy()">Aprovar retirada</button>
        <button class="danger" (click)="decideUnpublish(u.id, 'REJECTED')" [disabled]="busy()">Rejeitar retirada</button>
      </div>
      }
      @if (u.state === 'Approved' && kc.hasRole('approver')) {
      <div class="row"><button (click)="dispatchUnpublish(u.id)" [disabled]="busy()">Despachar despublicação</button></div>
      }
      }
      <textarea [(ngModel)]="unpublishReason" rows="2" placeholder="Motivo da retirada (auditoria)"></textarea>
      <div class="row"><button (click)="requestUnpublish()" [disabled]="busy()">Solicitar retirada</button></div>
    </div>
    }
    }
  </section>`,
})
export class ProductDetailComponent implements OnInit {
  private api = inject(ControlApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  kc = inject(KeycloakService);
  id = signal('');
  item = signal<Record<string, unknown> | null>(null);
  error = signal('');
  ok = signal('');
  busy = signal(false);
  unpublishQueue = signal<OperationApproval[]>([]);
  form = { ptBR: '', enUS: '', esES: '' };
  unpublishReason = '';

  async ngOnInit(): Promise<void> {
    this.id.set(this.route.snapshot.paramMap.get('id') ?? '');
    await this.reload();
  }

  titles(p: Record<string, unknown>): { locale: string; title: string }[] {
    return (p['titles'] as { locale: string; title: string }[] | undefined) ?? [];
  }
  skuCodes(p: Record<string, unknown>): string {
    const skus = (p['skus'] as { skuCode?: string; code?: string }[] | undefined) ?? [];
    return skus.map((s) => s.skuCode ?? s.code ?? '').filter(Boolean).join(', ') || '—';
  }
  editable(): boolean {
    const s = this.item()?.['status'];
    return s === 'Draft' || s === 'Unpublished';
  }

  async reload(): Promise<void> {
    this.error.set('');
    try {
      const p = await this.api.getProduct(this.id());
      this.item.set(p);
      for (const t of this.titles(p)) {
        if (t.locale === 'pt-BR') this.form.ptBR = t.title;
        if (t.locale === 'en-US') this.form.enUS = t.title;
        if (t.locale === 'es-ES') this.form.esES = t.title;
      }
      if (p['status'] === 'Published') await this.loadPendingUnpublish();
    } catch (e) { this.error.set((e as Error).message); }
  }

  async loadPendingUnpublish(): Promise<void> {
    const out: OperationApproval[] = [];
    for (const st of ['Requested', 'Approved']) {
      const page = await this.api.listCatalogApprovals(st, this.id());
      out.push(...page.items.filter((a) => a.action === 'catalog:unpublish'));
    }
    this.unpublishQueue.set(out);
  }

  private titlesPayload(): { locale: string; title: string }[] {
    return [
      { locale: 'pt-BR', title: this.form.ptBR },
      ...(this.form.enUS ? [{ locale: 'en-US', title: this.form.enUS }] : []),
      ...(this.form.esES ? [{ locale: 'es-ES', title: this.form.esES }] : []),
    ].filter((t) => t.title.trim().length > 0);
  }

  async save(): Promise<void> {
    const p = this.item();
    if (!p) return;
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      await this.api.updateProduct(this.id(), { expectedVersion: Number(p['version']), titles: this.titlesPayload() });
      this.ok.set('Rascunho atualizado.');
      await this.reload();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async submit(): Promise<void> {
    const p = this.item();
    if (!p) return;
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      await this.api.submitProduct(this.id(), Number(p['version']));
      this.ok.set('Submetido — aguarde decisão na fila de Publicações.');
      await this.reload();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async remove(): Promise<void> {
    if (!confirm('Apagar este rascunho? (só rascunhos nunca publicados)')) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.deleteProduct(this.id());
      await this.router.navigate(['/products']);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async requestUnpublish(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    this.ok.set('');
    try {
      await this.api.requestUnpublish(this.id(), this.unpublishReason);
      this.ok.set('Retirada solicitada — aguarde decisão de um approver distinto.');
      this.unpublishReason = '';
      await this.loadPendingUnpublish();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async decideUnpublish(approvalId: string, decision: 'APPROVED' | 'REJECTED'): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      await this.api.decideCatalog(approvalId, decision, decision === 'REJECTED' ? 'rejeitado no console' : undefined);
      this.ok.set(`Decisão registrada: ${decision}.`);
      await this.loadPendingUnpublish();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }

  async dispatchUnpublish(approvalId: string): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const r = await this.api.dispatchCatalog(approvalId);
      this.ok.set(`Despublicação concluída: ${r.state}.`);
      await this.reload();
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
