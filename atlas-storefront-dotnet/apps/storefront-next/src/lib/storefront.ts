// Client do BFF (borda única da vitrine): leituras das views + checkout via forwarder.
// INV-CQRS-001: preços/disponibilidade exibidos são HINT — o checkout reserva no owner.

export type SearchItem = {
  productId: string;
  title: string;
  marketId: string;
  status: string;
  sourceVersion: number;
};

export type SearchResult = {
  items: SearchItem[];
  total: number;
  page: number;
  pageSize: number;
  facets: Record<string, number>;
};

export type OfferView = {
  skuId: string;
  marketId: string;
  currency: string;
  priceMinor: number;
  status: string;
};

export type ProductDetail = {
  productId: string;
  title: string;
  marketId: string;
  status: string;
  sourceVersion: number;
  skus: string[];
  offers: OfferView[];
  availabilityHint: { skuId: string; availableHint: number }[];
};

// client (carrinho/checkout): caminho RELATIVO — o rewrite do Next encaminha ao BFF
// server (RSC): BFF_URL absoluto
const bffUrl = () =>
  typeof window === "undefined"
    ? process.env.BFF_URL ?? "http://127.0.0.1:5180"
    : "";

async function getJson<T>(path: string): Promise<T | null> {
  try {
    const res = await fetch(`${bffUrl()}${path}`, { cache: "no-store" });
    if (!res.ok) return null;
    return (await res.json()) as T;
  } catch {
    return null; // estado partial: página não derruba por indisponibilidade do BFF
  }
}

export function searchProducts(q?: string, market?: string, page = 1): Promise<SearchResult | null> {
  const params = new URLSearchParams();
  if (q) params.set("q", q);
  if (market) params.set("market", market);
  params.set("page", String(page));
  return getJson<SearchResult>(`/api/catalog/search?${params.toString()}`);
}

export function getProduct(productId: string): Promise<ProductDetail | null> {
  return getJson<ProductDetail>(`/api/catalog/products/${productId}`);
}

// ---- carrinho/checkout (client-side; forwarder /api/checkout/** no BFF) ----

export type CartLine = { skuId: string; quantity: number; unitPriceMinor: number };

export type QuoteResponse = {
  quoteId: string;
  totalMinor: number;
  currency: string;
  expiresAtUtc: string;
};

export type CheckoutOutcome = {
  status: number; // 200 Confirmed | 202 Pending | 409 Rejected
  checkoutId?: string;
  orderId?: string;
  reason?: string;
};

export async function requestQuote(cartId: string, currency: string, lines: CartLine[]): Promise<QuoteResponse | null> {
  try {
    const res = await fetch(`${bffUrl()}/api/checkout/quotes`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ cartId, currency, ttlSeconds: 600, lines }),
      cache: "no-store",
    });
    if (!res.ok) return null;
    return (await res.json()) as QuoteResponse;
  } catch {
    return null;
  }
}

export async function submitCheckout(input: {
  idempotencyKey: string;
  customerId: string;
  quoteId: string;
  lines: CartLine[];
  totalMinor: number;
  currency: string;
  shipping: { country: string; postalCode: string; line1: string; city: string };
}): Promise<CheckoutOutcome> {
  try {
    const res = await fetch(`${bffUrl()}/api/checkout/checkout`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
      cache: "no-store",
    });
    const body = res.headers.get("content-type")?.includes("json")
      ? ((await res.json()) as Record<string, unknown>)
      : {};
    return {
      status: res.status,
      checkoutId: body.checkoutId as string | undefined,
      orderId: body.orderId as string | undefined,
      reason: body.reason as string | undefined,
    };
  } catch {
    return { status: 0, reason: "BFF indisponível — tente novamente" };
  }
}
