import { Suspense } from "react";
import { searchProducts } from "@atlas/lib/storefront";
import { HeaderSearch } from "@atlas/components/HeaderSearch";

// Facets globais para a navegação persistente do header (best-effort).
// Fetch sem cache ISOLADO em Suspense: não bloqueia o prerender do layout.
async function HeaderFacets({
  locale,
  labels,
}: {
  locale: string;
  labels: { placeholder: string; button: string; all: string };
}) {
  const facets = (await searchProducts())?.facets ?? {};
  return <HeaderSearch locale={locale} labels={labels} facets={facets} />;
}

export function HeaderSearchSlot(props: {
  locale: string;
  labels: { placeholder: string; button: string; all: string };
}) {
  // Esqueleto com a mesma altura da busca real (evita CLS enquanto os facets carregam).
  return (
    <Suspense
      fallback={
        <div className="ml-search" aria-hidden="true">
          <div className="searchbar">
            <input type="search" disabled placeholder={props.labels.placeholder} tabIndex={-1} />
            <button type="button" disabled tabIndex={-1}>
              {props.labels.button}
            </button>
          </div>
          <div className="facets" aria-hidden="true" />
        </div>
      }
    >
      <HeaderFacets {...props} />
    </Suspense>
  );
}
