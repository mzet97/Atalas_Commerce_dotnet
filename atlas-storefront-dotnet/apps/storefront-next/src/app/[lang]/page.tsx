import { Suspense } from "react";
import Link from "next/link";
import { isLocale, translator, type Locale } from "@atlas/lib/i18n";
import { searchProducts, type SearchItem } from "@atlas/lib/storefront";
import { SearchBox } from "@atlas/components/SearchBox";

export const metadata = { title: "Home" };

// Busca + facets no MESMO bloco dinâmico (uma única leitura da view; cacheComponents
// exige leitura dinâmica dentro de Suspense)
async function Catalog({
  locale,
  searchParams,
}: {
  locale: Locale;
  searchParams: Promise<{ q?: string; market?: string; page?: string }>;
}) {
  const t = translator(locale);
  const sp = await searchParams;
  const q = sp.q;
  const market = sp.market;
  const page = Number(sp.page ?? 1);
  const result = await searchProducts(q, market, page);

  if (result === null) return <p data-testid="offline-state">{t("state.offline")}</p>;

  return (
    <>
      <SearchBox
        locale={locale}
        labels={{ placeholder: t("search.placeholder"), button: t("search.button"), all: t("search.facets.all") }}
        initialQuery={q ?? ""}
        initialMarket={market ?? ""}
        facets={result.facets}
      />
      {result.items.length === 0 && <p data-testid="empty-state">{t("search.empty")}</p>}
      <ul className="grid" data-testid="product-list">
        {result.items.map((item: SearchItem) => (
          <li key={item.productId} className="card" data-testid="product-item">
            <Link href={`/${locale}/produto/${item.productId}`}>
              <strong>{item.title}</strong>
            </Link>
            <small>
              {t("product.market")}: {item.marketId}
            </small>
          </li>
        ))}
      </ul>
      <div className="foot-note" data-testid="projection-note">
        {Object.keys(result.facets).length > 0 && (
          <span>
            {t("search.facets.title")}:{" "}
            {Object.entries(result.facets)
              .map(([k, v]) => `${k} (${v})`)
              .join(" · ")}
          </span>
        )}
      </div>
    </>
  );
}

export default function HomePage({
  params,
  searchParams,
}: {
  params: Promise<{ lang: string }>;
  searchParams: Promise<{ q?: string; market?: string; page?: string }>;
}) {
  return (
    <div className="page">
      <Suspense fallback={<p data-testid="loading">…</p>}>
        <Home params={params} searchParams={searchParams} />
      </Suspense>
    </div>
  );
}

async function Home({
  params,
  searchParams,
}: {
  params: Promise<{ lang: string }>;
  searchParams: Promise<{ q?: string; market?: string; page?: string }>;
}) {
  const { lang } = await params;
  if (!isLocale(lang)) return null;
  const locale = lang as Locale;
  const t = translator(locale);

  return (
    <div className="page">
      <h1>{t("store.tagline")}</h1>
      <Suspense fallback={<p data-testid="loading">…</p>}>
        <Catalog locale={locale} searchParams={searchParams} />
      </Suspense>
    </div>
  );
}
