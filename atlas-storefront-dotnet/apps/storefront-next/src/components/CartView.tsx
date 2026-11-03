"use client";

import Link from "next/link";
import { useState } from "react";
import { cartTotalMinor, removeFromCart, setQuantity, useCart } from "@atlas/lib/cart";
import { requestQuote, type QuoteResponse } from "@atlas/lib/storefront";
import { formatMoney, type Locale } from "@atlas/lib/i18n";

export function CartView({
  locale,
  labels,
}: {
  locale: Locale;
  labels: Record<string, string>;
}) {
  const { lines, hydrated } = useCart();
  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [quoting, setQuoting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const totalMinor = cartTotalMinor(lines);
  const currency = lines[0]?.currency ?? "BRL";

  async function emitQuote(): Promise<void> {
    setQuoting(true);
    setError(null);
    const result = await requestQuote(
      `cart-${locale}`,
      currency,
      lines.map((l) => ({ skuId: l.skuId, quantity: l.quantity, unitPriceMinor: l.unitPriceMinor })),
    );
    setQuoting(false);
    if (result === null) setError(labels.quoteError);
    else {
      setQuote(result);
      window.sessionStorage.setItem(
        "atlas.quote",
        JSON.stringify({ quoteId: result.quoteId, totalMinor: result.totalMinor, currency: result.currency }),
      );
    }
  }

  if (!hydrated) return <p data-testid="cart-loading">…</p>;
  if (lines.length === 0)
    return (
      <p data-testid="cart-empty">
        {labels.empty} <Link href={`/${locale}`}>{labels.backToStore}</Link>
      </p>
    );

  return (
    <div data-testid="cart-view">
      <table>
        <caption className="sr-only">{labels.title}</caption>
        <thead>
          <tr>
            <th scope="col">{labels.item}</th>
            <th scope="col">{labels.quantity}</th>
            <th scope="col">{labels.unitPrice}</th>
            <th scope="col">{labels.remove}</th>
          </tr>
        </thead>
        <tbody>
          {lines.map((l) => (
            <tr key={l.skuId} data-testid={`cart-line-${l.skuId}`}>
              <td>{l.title}</td>
              <td>
                <input
                  type="number"
                  min={1}
                  width={3}
                  value={l.quantity}
                  aria-label={`${labels.quantity} ${l.title}`}
                  data-testid={`qty-${l.skuId}`}
                  onChange={(e) => setQuantity(l.skuId, Number(e.target.value))}
                />
              </td>
              <td>{formatMoney(l.unitPriceMinor, l.currency, locale)}</td>
              <td>
                <button type="button" onClick={() => removeFromCart(l.skuId)} data-testid={`remove-${l.skuId}`}>
                  ✕
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="total" data-testid="cart-total">
        {labels.total}: <strong>{formatMoney(totalMinor, currency, locale)}</strong>
      </p>

      <div className="quotebox">
        {quote ? (
          <div data-testid="quote-result">
            <p>
              {labels.quoteReady}: <strong>{formatMoney(quote.totalMinor, quote.currency, locale)}</strong>
            </p>
            <p>
              <small>{labels.quoteExpires}: {new Date(quote.expiresAtUtc).toLocaleTimeString(locale)}</small>
            </p>
            <Link href={`/${locale}/checkout`} className="primary" data-testid="go-checkout">
              {labels.checkout}
            </Link>
          </div>
        ) : (
          <button type="button" className="primary" onClick={emitQuote} disabled={quoting} data-testid="request-quote">
            {quoting ? labels.quoting : labels.quote}
          </button>
        )}
        {error && (
          <p role="alert" data-testid="quote-error">
            {error}
          </p>
        )}
      </div>
    </div>
  );
}
