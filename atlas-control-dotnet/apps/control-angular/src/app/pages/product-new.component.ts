import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ControlApiService } from '../api/control-api.service';

/** Novo produto (rascunho): mercado + títulos + SKUs; publicar é four-eyes depois. */
@Component({
  standalone: true,
  imports: [RouterLink, FormsModule],
  template: `
  <section class="page narrow">
    <p><a routerLink="/products">← voltar para produtos</a></p>
    <h1>Novo produto</h1>
    @if (error()) { <p class="error">{{ error() }}</p> }
    @if (created()) { <p class="ok">Rascunho criado: <span class="mono">{{ created() }}</span></p> }
    <div class="card">
      <div class="grid">
        <label>Mercado <input [(ngModel)]="form.marketId" class="mono" placeholder="BR"></label>
        <label>Título (pt-BR) <input [(ngModel)]="form.ptBR"></label>
        <label>Título (en-US) <input [(ngModel)]="form.enUS"></label>
        <label>Título (es-ES) <input [(ngModel)]="form.esES"></label>
        <label>SKUs (vírgula) <input [(ngModel)]="form.skus" class="mono" placeholder="SKU-1, SKU-2"></label>
      </div>
      <button (click)="submit()" [disabled]="busy()">Criar rascunho</button>
    </div>
  </section>`,
})
export class ProductNewComponent {
  private api = inject(ControlApiService);
  private router = inject(Router);
  error = signal('');
  busy = signal(false);
  created = signal('');
  form = { marketId: 'BR', ptBR: '', enUS: '', esES: '', skus: '' };

  async submit(): Promise<void> {
    this.busy.set(true);
    this.error.set('');
    try {
      const titles = [
        { locale: 'pt-BR', title: this.form.ptBR },
        ...(this.form.enUS ? [{ locale: 'en-US', title: this.form.enUS }] : []),
        ...(this.form.esES ? [{ locale: 'es-ES', title: this.form.esES }] : []),
      ].filter((t) => t.title.trim().length > 0);
      const skus = this.form.skus.split(',').map((s) => s.trim()).filter((s) => s.length > 0);
      const r = await this.api.createProduct({ marketId: this.form.marketId || 'BR', titles, skuCodes: skus });
      this.created.set(r.productId);
      await this.router.navigate(['/products', r.productId]);
    } catch (e) { this.error.set((e as Error).message); }
    finally { this.busy.set(false); }
  }
}
