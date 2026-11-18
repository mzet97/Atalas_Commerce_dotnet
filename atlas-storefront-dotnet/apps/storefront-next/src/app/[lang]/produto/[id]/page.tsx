import { Suspense } from "react";
import Link from "next/link";
import { notFound } from "next/navigation";
import { isLocale, translator, formatMoney, type Locale } from "@atlas/lib/i18n";
import { getProduct } from "@atlas/lib/storefront";
import { AddToCart } from "@atlas/components/AddToCart";
import { ProductThumb } from "@atlas/components/ProductThumb";

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
  const inStock = (hint ?? 0) > 0;

  return (
    <div data-testid="pdp">
      <nav className="crumb" aria-label="breadcrumb">
        <Link href={`/${locale}`}>{t("nav.home")}</Link> · {product.marketId} ·{" "}
        <span data-testid="pdp-title">{product.title}</span>
      </nav>
      <div className="pdp">
        <div className="pdp-main">
          <div className="pdp-gallery">
            <ProductThumb big label={product.title} />
          </div>
          <div className="pdp-buybox">
            <p className="pdp-market">
              {t("product.market")}: {product.marketId}
            </p>
            <h1>{product.title}</h1>
            {bestOffer ? (
              <>
                <p className="price" data-testid="pdp-price">
                  <strong>{formatMoney(bestOffer.priceMinor, bestOffer.currency, locale)}</strong>
                  <small className="currency-tag"> {bestOffer.currency}</small>
                </p>
                <p className="installments">
                  {t("pdp.upTo12")} {formatMoney(Math.round(bestOffer.priceMinor / 12), bestOffer.currency, locale)}
                </p>
              </>
            ) : (
              <p data-testid="no-offer">{t("product.noOffer")}</p>
            )}
            {hint !== null && (
              <p data-testid="pdp-hint" className={inStock ? "stock in" : "stock out"}>
                {t("product.availability")}:{" "}
                {hint > 0 ? `${hint} ${t("product.units")}` : t("product.outOfStock")}
              </p>
            )}
            {bestOffer && (
              <AddToCart
                locale={locale}
                labels={{ add: t("product.addToCart"), added: t("product.added") }}
                line={{ skuId: bestOffer.skuId, title: product.title, unitPriceMinor: bestOffer.priceMinor, currency: bestOffer.currency }}
              />
            )}
            <p className="quote-note">{t("pdp.quoteNote")}</p>
          </div>
        </div>
        <div className="pdp-details">
          <h2>{t("pdp.details")}</h2>
          <table>
            <tbody>
              <tr>
                <th scope="row">{t("pdp.sku")}</th>
                <td>{product.skus.join(", ")}</td>
              </tr>
              <tr>
                <th scope="row">{t("product.market")}</th>
                <td>{product.marketId}</td>
              </tr>
              <tr>
                <th scope="row">{t("pdp.sourceVersion")}</th>
                <td>{product.sourceVersion}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
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
    <div className="page pdp-page">
      <Suspense fallback={<p data-testid="loading">…</p>}>
        <ProductDetails params={params} />
      </Suspense>
    </div>
  );
}
