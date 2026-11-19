"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { authHeader } from "@atlas/lib/auth";
import { formatMoney, type Locale } from "@atlas/lib/i18n";

export type BuyerOrder = {
  orderId: string;
  status: string;
  totalMinor: number;
  currency: string;
  sourceVersion: number;
};

// Meus pedidos: leitura da view via /api/orders/mine com Bearer do buyer
// (sessionStorage). Sem sessão → convite para login.
export function OrdersView({
  locale,
  labels,
}: {
  locale: Locale;
  labels: { loading: string; empty: string; login: string; items: string; view: string };
}) {
  const [orders, setOrders] = useState<BuyerOrder[] | null>(null);
  const [authed, setAuthed] = useState(true);

  useEffect(() => {
    const headers = authHeader();
    if (!headers.Authorization) {
      setAuthed(false);
      setOrders([]);
      return;
    }
    fetch(`/api/orders/mine`, { headers })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((j) => setOrders(j.items ?? []))
      .catch(() => setOrders([]));
  }, []);

  if (orders === null) return <p data-testid="loading">…</p>;
  if (!authed)
    return (
      <p data-testid="orders-login">
        <Link href={`/${locale}/login`}>{labels.login}</Link>
      </p>
    );
  if (orders.length === 0) return <p data-testid="empty-state">{labels.empty}</p>;

  return (
    <>
      <p className="muted">
        {orders.length} {labels.items}
      </p>
      <ul className="grid" data-testid="order-list">
        {orders.map((o) => (
          <li key={o.orderId} className="card" data-testid="order-item">
            <Link href={`/${locale}/pedidos/${o.orderId}`}>
              <span className="card-info">
                <span className="card-market mono">{o.orderId.slice(0, 8)}</span>
                <strong className="card-title">{o.status}</strong>
                <span className="price">
                  <strong>{formatMoney(o.totalMinor, o.currency, locale)}</strong>
                  <small className="currency-tag"> {o.currency}</small>
                </span>
                <span className="card-cta">{labels.view}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </>
  );
}
