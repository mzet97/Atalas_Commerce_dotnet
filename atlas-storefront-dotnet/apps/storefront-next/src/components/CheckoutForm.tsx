"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { clearCart, useCart } from "@atlas/lib/cart";
import { formatMoney } from "@atlas/lib/i18n";
import { submitCheckout, type CheckoutOutcome } from "@atlas/lib/storefront";

type StoredQuote = { quoteId: string; totalMinor: number; currency: string };

export function CheckoutForm({
  locale,
  labels,
}: {
  locale: string;
  labels: Record<string, string>;
}) {
  const { lines } = useCart();
  const [quote, setQuote] = useState<StoredQuote | null>(null);
  const [outcome, setOutcome] = useState<CheckoutOutcome | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [form, setForm] = useState({
    customerId: "guest",
    country: "BR",
    postalCode: "",
    addressLine1: "",
    city: "",
  });

  useEffect(() => {
    const raw = window.sessionStorage.getItem("atlas.quote");
    if (raw) setQuote(JSON.parse(raw) as StoredQuote);
  }, []);

  if (outcome) {
    const confirmed = outcome.status === 200;
    const pending = outcome.status === 202;
    return (
      <div data-testid={`checkout-outcome-${confirmed ? "confirmed" : pending ? "pending" : "rejected"}`}>
        <h2>{confirmed ? labels.confirmed : pending ? labels.pending : labels.rejected}</h2>
        {outcome.orderId && (
          <p>
            {labels.orderId}: <code data-testid="order-id">{outcome.orderId}</code>
          </p>
        )}
        {outcome.reason && confirmed === false && !pending && <p data-testid="outcome-reason">{outcome.reason}</p>}
        {confirmed && (
          <p>
            {labels.thanks} {formatMoney(quote?.totalMinor ?? 0, quote?.currency ?? "BRL", locale)}
          </p>
        )}
        <button
          type="button"
          data-testid="new-order"
          onClick={() => {
            clearCart();
            window.sessionStorage.removeItem("atlas.quote");
            window.location.href = `/${locale}`;
          }}
        >
          {labels.newOrder}
        </button>
      </div>
    );
  }

  if (!quote) {
    return (
      <p data-testid="no-quote">
        {labels.noQuote} <Link href={`/${locale}/cart`}>{labels.backToCart}</Link>
      </p>
    );
  }

  return (
    <form
      className="checkout-form"
      data-testid="checkout-form"
      onSubmit={async (e) => {
        e.preventDefault();
        setSubmitting(true);
        const result = await submitCheckout({
          idempotencyKey: `web-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
          customerId: form.customerId,
          quoteId: quote.quoteId,
          lines: lines.map((l) => ({ skuId: l.skuId, quantity: l.quantity, unitPriceMinor: l.unitPriceMinor })),
          totalMinor: quote.totalMinor,
          currency: quote.currency,
          shipping: { country: form.country, postalCode: form.postalCode, line1: form.addressLine1, city: form.city },
        });
        setSubmitting(false);
        setOutcome(result);
      }}
    >
      <p data-testid="checkout-total">
        {labels.total}: <strong>{formatMoney(quote.totalMinor, quote.currency, locale)}</strong>
      </p>
      <label>
        {labels.customer}
        <input value={form.customerId} onChange={(e) => setForm({ ...form, customerId: e.target.value })} data-testid="customer" />
      </label>
      <label>
        {labels.country}
        <input value={form.country} onChange={(e) => setForm({ ...form, country: e.target.value })} data-testid="country" />
      </label>
      <label>
        {labels.postalCode}
        <input value={form.postalCode} onChange={(e) => setForm({ ...form, postalCode: e.target.value })} data-testid="postal-code" required />
      </label>
      <label>
        {labels.addressLine1}
        <input value={form.addressLine1} onChange={(e) => setForm({ ...form, addressLine1: e.target.value })} data-testid="line1" required />
      </label>
      <label>
        {labels.city}
        <input value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} data-testid="city" required />
      </label>
      <button type="submit" className="primary" disabled={submitting} data-testid="place-order">
        {submitting ? labels.submitting : labels.placeOrder}
      </button>
    </form>
  );
}
