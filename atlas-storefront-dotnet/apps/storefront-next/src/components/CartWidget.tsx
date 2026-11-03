"use client";

import Link from "next/link";
import { useCart } from "@atlas/lib/cart";
import type { Locale } from "@atlas/lib/i18n";

export function CartWidget({ locale, label }: { locale: Locale; label: string }) {
  const { lines, hydrated } = useCart();
  const count = lines.reduce((acc, l) => acc + l.quantity, 0);
  return (
    <Link href={`/${locale}/carrinho`} className="cart-link" data-testid="cart-link">
      🛒 {label}
      <span aria-live="polite" data-testid="cart-count" className="cart-count">
        {hydrated ? count : 0}
      </span>
    </Link>
  );
}
