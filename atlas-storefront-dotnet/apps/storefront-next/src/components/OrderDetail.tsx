"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { authHeader } from "@atlas/lib/auth";
import { formatMoney, type Locale } from "@atlas/lib/i18n";
import type { BuyerOrder } from "@atlas/components/OrdersView";

// Detalhe do pedido: mesma API com ownership no SQL (alheio = 404).
export function OrderDetail({
  locale,
  orderId,
  labels,
}: {
  locale: Locale;
  orderId: string;
  labels: { loading: string; notFound: string; back: string; status: string; total: string; version: string };
}) {
  const [order, setOrder] = useState<BuyerOrder | null>(null);
  const [missing, setMissing] = useState(false);

  useEffect(() => {
    const headers = authHeader();
    if (!headers.Authorization) {
      setMissing(true);
      return;
    }
    fetch(`/api/orders/${orderId}`, { headers })
      .then((r) => {
        if (r.status === 404) {
          setMissing(true);
          return null;
        }
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.json();
      })
      .then((j) => {
        if (j) setOrder(j);
      })
      .catch(() => setMissing(true));
  }, [orderId]);

  if (missing) return <p data-testid="empty-state">{labels.notFound}</p>;
  if (!order) return <p data-testid="loading">…</p>;

  return (
    <div data-testid="order-detail">
      <p>
        <Link href={`/${locale}/pedidos`}>{labels.back}</Link>
      </p>
      <dl className="facts">
        <dt>ID</dt>
        <dd className="mono">{order.orderId}</dd>
        <dt>{labels.status}</dt>
        <dd>
          <span className="pill">{order.status}</span>
        </dd>
        <dt>{labels.total}</dt>
        <dd className="price">
          <strong>{formatMoney(order.totalMinor, order.currency, locale)}</strong>
        </dd>
        <dt>{labels.version}</dt>
        <dd className="mono">{order.sourceVersion}</dd>
      </dl>
    </div>
  );
}
