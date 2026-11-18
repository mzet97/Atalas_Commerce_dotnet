"use client";

import { Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";

// Linha de busca do header (input + botão). O termo inicial vem da URL
// (o layout server não enxerga searchParams).
function SearchFieldInner({ locale, labels }: { locale: string; labels: { placeholder: string; button: string } }) {
  const router = useRouter();
  const sp = useSearchParams();
  const [q, setQ] = useState(sp.get("q") ?? "");
  return (
    <form
      role="search"
      className="searchbar"
      onSubmit={(e) => {
        e.preventDefault();
        const params = new URLSearchParams();
        if (q) params.set("q", q);
        const market = sp.get("market");
        if (market) params.set("market", market);
        router.push(`/${locale}?${params.toString()}`);
      }}
    >
      <input
        type="search"
        name="q"
        value={q}
        placeholder={labels.placeholder}
        onChange={(e) => setQ(e.target.value)}
        data-testid="search-input"
        aria-label={labels.placeholder}
      />
      <button type="submit" data-testid="search-button">
        {labels.button}
      </button>
    </form>
  );
}

// Navegação por mercado (chips). Preserva o termo buscado ao trocar de mercado.
function MarketChipsInner({
  locale,
  labels,
  facets,
}: {
  locale: string;
  labels: { all: string };
  facets: Record<string, number>;
}) {
  const router = useRouter();
  const sp = useSearchParams();
  const q = sp.get("q") ?? "";
  const market = sp.get("market") ?? "";
  const navigate = (nextMarket: string) => {
    const params = new URLSearchParams();
    if (q) params.set("q", q);
    if (nextMarket) params.set("market", nextMarket);
    router.push(`/${locale}?${params.toString()}`);
  };
  return (
    <div className="facets" role="group" aria-label={labels.all} data-testid="facets">
      <button type="button" onClick={() => navigate("")} aria-pressed={market === ""} data-testid="facet-all">
        {labels.all}
      </button>
      {Object.entries(facets).map(([m, count]) => (
        <button
          key={m}
          type="button"
          onClick={() => navigate(m)}
          aria-pressed={market === m}
          data-testid={`facet-${m}`}
        >
          {m} ({count})
        </button>
      ))}
    </div>
  );
}

export function HeaderSearch(props: {
  locale: string;
  labels: { placeholder: string; button: string; all: string };
  facets: Record<string, number>;
}) {
  return (
    <div className="ml-search">
      <Suspense>
        <SearchFieldInner locale={props.locale} labels={props.labels} />
      </Suspense>
      <Suspense fallback={<div className="facets" aria-hidden="true" />}>
        <MarketChipsInner locale={props.locale} labels={props.labels} facets={props.facets} />
      </Suspense>
    </div>
  );
}
