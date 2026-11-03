import { Suspense } from "react";
import { notFound } from "next/navigation";
import { isLocale, translator, formatMoney, type Locale } from "@atlas/lib/i18n";
import { getProduct } from "@atlas/lib/storefront";
import { AddToCart } from "@atlas/components/AddToCart";

// título estático no metadata (a leitura dinâmica fica no corpo, dentro de Suspense)
export const metadata = { title: "Produto" };


async function ProductDetails({ params }: { params: Promise<{ lang: string; id: string }> }) {
  const { lang, id } = await params;
  if (!isLocale(lang)) notFound();
  const locale = lang as Locale;
  const t = translator(locale);
  const product = await getProduct(id);
  if (product === null) {
    return <p data-testid="empty-state">{t("search.empty")}</p>;
  }
  const bestOffer = product.offers[0] ?? null;
  const hint = product.availabilityHint.find((h) => h.skuId === bestOffer?.skuId)?.availableHint ?? null;

  return (
    <div data-testid="pdp">
      <h1 data-testid="pdp-title">{product.title}</h1>
      <p>
        <small>
          {t("product.market")}: {product.marketId} · SKU: {product.skus.join(", ")}
        </small>
      </p>
      {bestOffer ? (
        <p className="price" data-testid="pdp-price">
          <strong>{formatMoney(bestOffer.priceMinor, bestOffer.currency, locale)}</strong>
          <small className="currency-tag"> {bestOffer.currency}</small>
        </p>
      ) : (
        <p data-testid="no-offer">{t("product.noOffer")}</p>
      )}
      {hint !== null && (
        <p data-testid="pdp-hint">
          <small>
            {t("product.availability")}:{" "}
            {hint > 0 ? `${hint} ${t("product.units")}` : t("product.outOfStock")}
          </small>
        </p>
      )}
      {bestOffer && (
        <AddToCart
          locale={locale}
          labels={{ add: t("product.addToCart"), added: t("product.added") }}
          line={{ skuId: bestOffer.skuId, title: product.title, unitPriceMinor: bestOffer.priceMinor, currency: bestOffer.currency }}
        />
      )}
    </div>
  );
}

// params são RUNTIME (sem generateStaticParams): a promise desce DENTRO do Suspense
export default function ProductPage({
  params,
}: {
  params: Promise<{ lang: string; id: string }>;
}) {
  return (
    <div className="page pdp">
      <Suspense fallback={<p data-testid="loading">…</p>}>
        <ProductDetails params={params} />
      </Suspense>
    </div>
  );
}
