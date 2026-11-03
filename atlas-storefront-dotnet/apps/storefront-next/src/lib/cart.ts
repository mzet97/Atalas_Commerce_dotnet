// Carrinho CLIENT-side (UC-002): localStorage por aba do navegador; a quote é a verdade
// congelada e o checkout revalida no owner (INV-CQRS-001/INV-QUOTE-001).
"use client";

import { useEffect, useState } from "react";

export type CartLine = { skuId: string; title: string; quantity: number; unitPriceMinor: number; currency: string };

const STORAGE_KEY = "atlas.cart.v1";

export function readCart(): CartLine[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as CartLine[]) : [];
  } catch {
    return [];
  }
}

function writeCart(lines: CartLine[]): void {
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
  window.dispatchEvent(new Event("atlas:cart"));
}

export function useCart() {
  const [lines, setLines] = useState<CartLine[]>([]);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    setLines(readCart());
    setHydrated(true);
    const sync = () => setLines(readCart());
    window.addEventListener("atlas:cart", sync);
    window.addEventListener("storage", sync);
    return () => {
      window.removeEventListener("atlas:cart", sync);
      window.removeEventListener("storage", sync);
    };
  }, []);

  return { lines, hydrated };
}

export function addToCart(line: Omit<CartLine, "quantity"> & { quantity?: number }): void {
  const lines = readCart();
  const existing = lines.find((l) => l.skuId === line.skuId);
  if (existing) existing.quantity += line.quantity ?? 1;
  else lines.push({ ...line, quantity: line.quantity ?? 1 });
  writeCart(lines);
}

export function removeFromCart(skuId: string): void {
  writeCart(readCart().filter((l) => l.skuId !== skuId));
}

export function setQuantity(skuId: string, quantity: number): void {
  const lines = readCart();
  const target = lines.find((l) => l.skuId === skuId);
  if (!target) return;
  if (quantity <= 0) removeFromCart(skuId);
  else {
    target.quantity = quantity;
    writeCart(lines);
  }
}

export function cartTotalMinor(lines: CartLine[]): number {
  return lines.reduce((acc, l) => acc + l.unitPriceMinor * l.quantity, 0);
}

export function clearCart(): void {
  writeCart([]);
}
