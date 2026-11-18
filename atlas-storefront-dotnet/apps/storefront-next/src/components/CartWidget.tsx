"use client";

import Link from "next/link";
import { useCart } from "@atlas/lib/cart";
import type { Locale } from "@atlas/lib/i18n";

export function CartWidget({ locale, label }: { locale: Locale; label: string }) {
  const { lines, hydrated } = useCart();
  const count = lines.reduce((acc, l) => acc + l.quantity, 0);
  return (
    <Link href={`/${locale}/carrinho`} className="cart-link" data-testid="cart-link">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
        <path d="M3 4h2l2.4 12h10.4l2-8H7" strokeLinecap="round" strokeLinejoin="round" />
        <circle cx="9.5" cy="19.5" r="1.4" />
        <circle cx="16.5" cy="19.5" r="1.4" />
      </svg>
      {label}
      <span aria-live="polite" data-testid="cart-count" className="cart-count">
        {hydrated ? count : 0}
      </span>
    </Link>
  );
}
