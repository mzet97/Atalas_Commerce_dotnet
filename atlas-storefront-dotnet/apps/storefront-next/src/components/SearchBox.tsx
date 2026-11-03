"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function SearchBox({
  locale,
  labels,
  initialQuery,
  initialMarket,
  facets,
}: {
  locale: string;
  labels: { placeholder: string; button: string; all: string };
  initialQuery: string;
  initialMarket: string;
  facets: Record<string, number>;
}) {
  const router = useRouter();
  const [q, setQ] = useState(initialQuery);
  const [market, setMarket] = useState(initialMarket);

  const navigate = (nextMarket: string) => {
    const params = new URLSearchParams();
    if (q) params.set("q", q);
    if (nextMarket) params.set("market", nextMarket);
    router.push(`/${locale}?${params.toString()}`);
  };

  return (
    <form
      role="search"
      onSubmit={(e) => {
        e.preventDefault();
        navigate(market);
      }}
      className="searchbar"
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
      <div className="facets" role="group" aria-label={labels.button} data-testid="facets">
        <button type="button" onClick={() => { setMarket(""); navigate(""); }} aria-pressed={market === ""} data-testid="facet-all">
          {labels.all}
        </button>
        {Object.entries(facets).map(([m, count]) => (
          <button
            key={m}
            type="button"
            onClick={() => { setMarket(m); navigate(m); }}
            aria-pressed={market === m}
            data-testid={`facet-${m}`}
          >
            {m} ({count})
          </button>
        ))}
      </div>
    </form>
  );
}
