import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import "@atlas/app/globals.css";
import { isLocale, LOCALES, translator as getTranslator, type Locale } from "@atlas/lib/i18n";
import { CartWidget } from "@atlas/components/CartWidget";
import { HeaderSearchSlot } from "@atlas/components/HeaderSearchSlot";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.STOREFRONT_URL ?? "http://localhost:3000"),
  title: { default: "Atlas Storefront", template: "%s · Atlas Storefront" },
  alternates: { languages: { "pt-BR": "/pt-BR", "en-US": "/en-US", "es-ES": "/es-ES" } },
  openGraph: { siteName: "Atlas Storefront", type: "website" },
};

// Pré-render por locale (App Router: i18n manual via segmento [lang] — guia oficial)
export function generateStaticParams() {
  return LOCALES.map((lang) => ({ lang }));
}

export default async function LangLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ lang: string }>;
}) {
  const { lang } = await params;
  if (!isLocale(lang)) notFound();
  const locale = lang as Locale;
  const t = getTranslator(locale);

  // URL pública do Keycloak para o browser (runtime por request; sem rebuild por ambiente).
  const keycloakUrl = process.env.KEYCLOAK_URL ?? "https://keycloak.home.arpa";
  return (
    <html lang={locale}>
      <body>
        <script
          dangerouslySetInnerHTML={{
            __html: `window.__KEYCLOAK_URL=${JSON.stringify(keycloakUrl)};`,
          }}
        />
        <header className="ml-top">
          <div className="ml-top-inner">
            <Link href={`/${locale}`} className="brand" data-testid="brand">
              Atlas<small>{t("store.tagline")}</small>
            </Link>
            <HeaderSearchSlot
              locale={locale}
              labels={{ placeholder: t("search.placeholder"), button: t("search.button"), all: t("search.facets.all") }}
            />
            <nav className="ml-account" aria-label={t("nav.cart")}>
              <Link href={`/${locale}/pedidos`} data-testid="nav-orders">
                {t("nav.orders")}
              </Link>
              <Link href={`/${locale}/login`} data-testid="nav-login">
                {t("nav.login")}
              </Link>
              <CartWidget locale={locale} label={t("nav.cart")} />
            </nav>
          </div>
          <div className="ml-locale">
            <div className="ml-locale-inner">
              <ul className="locale-switch" data-testid="locale-switch">
                {LOCALES.map((l) => (
                  <li key={l}>
                    <Link
                      href={`/${l}`}
                      aria-current={l === locale ? "page" : undefined}
                      data-testid={`lang-${l}`}
                    >
                      {l.toUpperCase().slice(0, 2)}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </header>
        <main id="main">{children}</main>
        <footer className="ml-footer">
          <div className="ml-footer-inner">
            <div>
              <h3>{t("footer.colShop")}</h3>
              <p>{t("footer.colShopText")}</p>
            </div>
            <div>
              <h3>{t("footer.colQuote")}</h3>
              <p>{t("footer.colQuoteText")}</p>
            </div>
            <div>
              <h3>{t("footer.colOps")}</h3>
              <p>{t("footer.colOpsText")}</p>
            </div>
          </div>
          <div className="ml-evd">
            <small>{t("footer.evd")}</small>
          </div>
        </footer>
      </body>
    </html>
  );
}
