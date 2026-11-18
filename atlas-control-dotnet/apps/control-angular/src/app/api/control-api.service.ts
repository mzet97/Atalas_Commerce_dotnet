import { Injectable, inject } from '@angular/core';
import { KeycloakService } from '../auth/keycloak.service';

export type ApprovalState = 'Requested' | 'Approved' | 'Rejected' | 'Dispatched' | 'Executed' | 'Failed';
export interface PublishApproval {
  id: string; requestedBy: string; productId: string; expectedVersion: number;
  marketId: string; justification: string; state: ApprovalState; decidedBy?: string;
  rejectionReason?: string; createdAtUtc: string; decidedAtUtc?: string;
}
export interface OperationApproval {
  id: string; requestedBy: string; action: string; subject: string;
  justification: string; state: ApprovalState; decidedBy?: string;
  approvalToken?: string; createdAtUtc: string; decidedAtUtc?: string; dispatchedAtUtc?: string;
}
export interface Page<T> { items: T[]; total: number; page: number; pageSize: number }
export interface Grant {
  id: string; subject: string; action: string; warehouseId: string;
  state: 'Active' | 'Revoked'; grantedBy: string; revokedBy?: string;
  expiresAtUtc: string; createdAtUtc: string;
}
export interface AuditEntry {
  seq: number; occurredAtUtc: string; actorSub: string; action: string;
  decision: string; correlationId: string; prevHash: string; hash: string;
}
export interface ModuleStatus { module: string; [k: string]: unknown }

const STATE_NAMES = ['Requested', 'Approved', 'Rejected', 'Dispatched', 'Executed', 'Failed'] as const;

/** Cliente tipado do Atlas.Control.Host (mesma origem via /api → nginx → control-host). */
@Injectable({ providedIn: 'root' })
export class ControlApiService {
  private kc = inject(KeycloakService);

  private async call<T>(path: string, init?: RequestInit): Promise<T> {
    const token = await this.kc.token();
    const res = await fetch('/api' + path, {
      ...init,
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...(init?.headers ?? {}) },
    });
    if (res.status === 401) {
      this.kc.login();
      throw new Error('não autenticado — redirecionando para login');
    }
    const text = await res.text();
    const body = text ? JSON.parse(text) : null;
    if (!res.ok) throw new Error(body?.error ?? `erro ${res.status}`);
    return body as T;
  }

  // ---- aprovações four-eyes generalizadas ----
  async listApprovals(state?: string, page = 1): Promise<Page<OperationApproval>> {
    const q = new URLSearchParams({ page: String(page), pageSize: '20' });
    if (state) q.set('state', state);
    const p = await this.call<Page<Record<string, unknown>>>(`/modules/approvals?${q}`);
    return { ...p, items: p.items.map((i) => this.mapApproval(i)) };
  }
  getApproval(id: string): Promise<OperationApproval> {
    return this.call(`/modules/approvals/${id}`).then((d) => this.mapApproval(d as Record<string, unknown>));
  }
  requestAdjust(input: { warehouseId: string; skuId: string; delta: number; reason: string }): Promise<{ approvalId: string }> {
    return this.call('/modules/inventory-operations/adjust/requests', { method: 'POST', body: JSON.stringify(input) });
  }
  decide(id: string, decision: 'APPROVED' | 'REJECTED', reason?: string): Promise<{ state: string; approvalToken?: string }> {
    return this.call(`/modules/approvals/${id}/decide`, { method: 'POST', body: JSON.stringify({ decision, reason }) });
  }
  dispatch(id: string, input: { requestId: string; delta: number; reason?: string }): Promise<{ state: string }> {
    return this.call(`/modules/approvals/${id}/dispatch`, { method: 'POST', body: JSON.stringify(input) });
  }

  // ---- publicações de catálogo (four-eyes M2) ----
  async listPublications(state?: string, page = 1): Promise<Page<PublishApproval>> {
    const q = new URLSearchParams({ page: String(page), pageSize: '20' });
    if (state) q.set('state', state);
    const p = await this.call<Page<Record<string, unknown>>>(`/approvals?${q}`);
    return { ...p, items: p.items.map((i) => this.mapPublish(i)) };
  }
  async getPublication(id: string): Promise<PublishApproval> {
    return this.mapPublish(await this.call<Record<string, unknown>>(`/approvals/${id}`));
  }
  private mapPublish(d: Record<string, unknown>): PublishApproval {
    const state = typeof d['state'] === 'number' ? (STATE_NAMES[d['state'] as number] ?? 'Requested') : (d['state'] as ApprovalState ?? 'Requested');
    return {
      id: String(d['id'] ?? ''), requestedBy: String(d['requestedBy'] ?? ''),
      productId: String(d['productId'] ?? ''), expectedVersion: Number(d['expectedVersion'] ?? 0),
      marketId: String(d['marketId'] ?? ''), justification: String(d['justification'] ?? ''),
      state, decidedBy: d['decidedBy'] as string | undefined,
      rejectionReason: d['rejectionReason'] as string | undefined,
      createdAtUtc: String(d['createdAtUtc'] ?? ''), decidedAtUtc: d['decidedAtUtc'] as string | undefined,
    };
  }
  requestPublish(input: { productId: string; expectedVersion: number; marketId: string; justification: string }): Promise<{ approvalId: string }> {
    return this.call('/approvals/requests', { method: 'POST', body: JSON.stringify(input) });
  }
  decidePublish(id: string, decision: 'APPROVED' | 'REJECTED', reason?: string): Promise<{ state: string; approvalToken?: string }> {
    return this.call(`/approvals/${id}/decide`, { method: 'POST', body: JSON.stringify({ decision, reason }) });
  }
  dispatchPublish(id: string): Promise<{ state: string }> {
    return this.call(`/approvals/${id}/dispatch`, { method: 'POST', body: JSON.stringify({}) });
  }
  publishStateName(n: unknown): string {
    return typeof n === 'number' ? (STATE_NAMES[n] ?? 'Requested') : String(n ?? 'Requested');
  }

  // ---- produtos (master data do catálogo via owner) ----
  listProducts(filter: { status?: string; market?: string; search?: string; page?: number } = {}): Promise<{ items: Record<string, unknown>[]; total: number }> {
    const q = new URLSearchParams({ page: String(filter.page ?? 1), pageSize: '20' });
    if (filter.status) q.set('status', filter.status);
    if (filter.market) q.set('market', filter.market);
    if (filter.search) q.set('q', filter.search);
    return this.call(`/modules/catalog/products?${q}`);
  }
  getProduct(id: string): Promise<Record<string, unknown>> {
    return this.call(`/modules/catalog/products/${id}`);
  }
  createProduct(input: { marketId: string; titles: { locale: string; title: string }[]; skuCodes: string[] }): Promise<{ productId: string; version: number }> {
    return this.call('/modules/catalog/products', { method: 'POST', body: JSON.stringify(input) });
  }
  updateProduct(id: string, input: { expectedVersion: number; titles: { locale: string; title: string }[]; attributes?: Record<string, string> }): Promise<unknown> {
    return this.call(`/modules/catalog/products/${id}`, { method: 'PUT', body: JSON.stringify(input) });
  }
  deleteProduct(id: string): Promise<unknown> {
    return this.call(`/modules/catalog/products/${id}`, { method: 'DELETE' });
  }
  submitProduct(id: string, expectedVersion: number): Promise<unknown> {
    return this.call(`/modules/catalog/products/${id}/submit`, { method: 'POST', body: JSON.stringify({ expectedVersion }) });
  }
  requestUnpublish(productId: string, reason: string): Promise<{ approvalId: string }> {
    return this.call('/modules/catalog/unpublish/requests', { method: 'POST', body: JSON.stringify({ productId, reason }) });
  }
  listCatalogApprovals(state?: string, subject?: string): Promise<Page<OperationApproval>> {
    const q = new URLSearchParams({ page: '1', pageSize: '50' });
    if (state) q.set('state', state);
    if (subject) q.set('subject', subject);
    return this.call(`/modules/approvals?${q}`).then((p) => this.mapCatalogApprovalPage(p as Page<Record<string, unknown>>));
  }
  decideCatalog(id: string, decision: 'APPROVED' | 'REJECTED', reason?: string): Promise<{ state: string }> {
    return this.call(`/modules/catalog/approvals/${id}/decide`, { method: 'POST', body: JSON.stringify({ decision, reason }) });
  }
  dispatchCatalog(id: string): Promise<{ state: string }> {
    return this.call(`/modules/catalog/approvals/${id}/dispatch`, { method: 'POST', body: JSON.stringify({}) });
  }
  private mapCatalogApprovalPage(p: Page<Record<string, unknown>>): Page<OperationApproval> {
    return { ...p, items: p.items.map((i) => this.mapApproval(i)) };
  }

  // ---- auditoria (REQ-ADM-003) ----
  listAudit(action?: string, page = 1): Promise<Page<AuditEntry>> {
    const q = new URLSearchParams({ page: String(page), pageSize: '20' });
    if (action) q.set('action', action);
    return this.call(`/modules/audit?${q}`);
  }
  verifyAudit(): Promise<{ valid: boolean; count: number; brokenAtSeq?: number; reason?: string }> {
    return this.call('/modules/audit/verify');
  }
  exportCsvUrl(): string { return '/api/modules/audit/export?format=csv'; }

  // ---- grants ABAC ----
  async listGrants(subject?: string): Promise<{ grants: Grant[] }> {
    const q = subject ? `?subject=${encodeURIComponent(subject)}` : '';
    const r = await this.call<{ grants: Record<string, unknown>[] }>(`/modules/iam/grants${q}`);
    return { grants: r.grants.map((g) => this.mapGrant(g)) };
  }
  createGrant(input: { subject: string; action: string; warehouseId: string; ttlMinutes: number }): Promise<unknown> {
    return this.call('/modules/iam/grants', { method: 'POST', body: JSON.stringify(input) });
  }
  revokeGrant(id: string): Promise<unknown> {
    return this.call(`/modules/iam/grants/${id}`, { method: 'DELETE' });
  }

  // ---- reembolsos four-eyes ----
  refundToken(input: { requesterSub: string; orderId: string; amountMinor: number; rmaId: string }): Promise<{ approvalToken: string; approver: string }> {
    return this.call('/modules/payments-refunds/token', { method: 'POST', body: JSON.stringify(input) });
  }

  // ---- operação ----
  opsOverview(): Promise<Record<string, unknown>> {
    return this.call('/modules/ops/overview');
  }
  positions(sku?: string): Promise<{ items: Record<string, unknown>[]; total: number }> {
    const q = sku ? `?skuContains=${encodeURIComponent(sku)}` : '';
    return this.call(`/modules/inventory-operations/positions${q}`);
  }
  cqrsLag(warehouseId: string, skuId: string): Promise<Record<string, unknown>> {
    return this.call(`/modules/inventory-operations/cqrs-lag?warehouseId=${encodeURIComponent(warehouseId)}&skuId=${encodeURIComponent(skuId)}`);
  }
  moduleStatus(path: string): Promise<ModuleStatus> {
    return this.call(path);
  }

  private mapApproval(d: Record<string, unknown>): OperationApproval {
    const state = typeof d['state'] === 'number' ? (STATE_NAMES[d['state'] as number] ?? 'Requested') : (d['state'] as ApprovalState ?? 'Requested');
    return {
      id: String(d['id'] ?? d['approvalId'] ?? ''), requestedBy: String(d['requestedBy'] ?? ''),
      action: String(d['action'] ?? ''), subject: String(d['subject'] ?? ''),
      justification: String(d['justification'] ?? ''), state,
      decidedBy: d['decidedBy'] as string | undefined,
      approvalToken: d['approvalToken'] as string | undefined,
      createdAtUtc: String(d['createdAtUtc'] ?? ''),
      decidedAtUtc: d['decidedAtUtc'] as string | undefined,
      dispatchedAtUtc: d['dispatchedAtUtc'] as string | undefined,
    };
  }
  private mapGrant(g: Record<string, unknown>): Grant {
    return {
      id: String(g['id'] ?? ''), subject: String(g['subject'] ?? ''), action: String(g['action'] ?? ''),
      warehouseId: String(g['warehouseId'] ?? ''),
      state: g['state'] === 1 || g['state'] === 'Revoked' ? 'Revoked' : 'Active',
      grantedBy: String(g['grantedBy'] ?? ''), revokedBy: g['revokedBy'] as string | undefined,
      expiresAtUtc: String(g['expiresAtUtc'] ?? ''), createdAtUtc: String(g['createdAtUtc'] ?? ''),
    };
  }
}
