import { Suspense } from "react";
import Link from "next/link";
import { isLocale, translator, type Locale } from "@atlas/lib/i18n";
import { searchProducts, type SearchItem } from "@atlas/lib/storefront";
import { ProductThumb } from "@atlas/components/ProductThumb";

export const metadata = { title: "Home" };

function PerkIcon({ d }: { d: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
      <path d={d} strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

// Busca no MESMO bloco dinâmico (uma única leitura da view; cacheComponents
// exige leitura dinâmica dentro de Suspense). O cabeçalho da seção é estático
// (q vem dos searchParams, conhecidos no servidor); só a grade faz streaming,
// com esqueleto de mesma altura (sem CLS).
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
      {result.items.length === 0 && <p data-testid="empty-state">{t("search.empty")}</p>}
      <ul className="grid" data-testid="product-list">
        {result.items.map((item: SearchItem) => (
          <li key={item.productId} className="card" data-testid="product-item">
            <Link href={`/${locale}/produto/${item.productId}`}>
              {item.coverUrl ? (
                <span className="thumb photo">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={item.coverUrl} alt={item.title} loading="lazy" />
                </span>
              ) : (
                <ProductThumb label={item.title} />
              )}
              <span className="card-info">
                <span className="card-market">{item.marketId}</span>
                <strong className="card-title">{item.title}</strong>
                {item.status !== "Published" && <span className="status-flag">{item.status}</span>}
                <span className="card-cta">{t("home.viewOffer")}</span>
              </span>
            </Link>
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

// Esqueleto com a mesma estrutura/altura dos cards reais (thumb 4:3 + linhas).
function GridSkeleton() {
  return (
    <ul className="grid" aria-hidden="true">
      {Array.from({ length: 8 }, (_, i) => (
        <li key={i} className="card">
          <div className="thumb" />
          <span className="card-info">
            <span className="skel-line short" />
            <span className="skel-line tall" />
            <span className="skel-line cta" />
          </span>
        </li>
      ))}
    </ul>
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
  const q = (await searchParams).q;

  return (
    <div className="page">
      <div className="perks">
        <div className="perk">
          <PerkIcon d="M3 7h13v9H3zM16 10h4l3 3v3h-7zM7 19a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Zm11 0a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Z" />
          <div><strong>{t("home.perkQuote")}</strong><span>{t("home.perkQuoteSub")}</span></div>
        </div>
        <div className="perk">
          <PerkIcon d="M12 3 3 8l9 5 9-5-9-5ZM3 12l9 5 9-5M3 16l9 5 9-5" />
          <div><strong>{t("home.perkMarket")}</strong><span>{t("home.perkMarketSub")}</span></div>
        </div>
        <div className="perk">
          <PerkIcon d="M12 3l7 3v5c0 5-3.5 8-7 10-3.5-2-7-5-7-10V6l7-3Z" />
          <div><strong>{t("home.perkStock")}</strong><span>{t("home.perkStockSub")}</span></div>
        </div>
      </div>
      <div className="section-head">
        <h2>{q ? t("search.resultsFor") : t("home.featured")}</h2>
      </div>
      <Suspense fallback={<GridSkeleton />}>
        <Catalog locale={locale} searchParams={searchParams} />
      </Suspense>
    </div>
  );
}
