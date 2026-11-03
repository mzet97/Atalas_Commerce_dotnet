import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import "@atlas/app/globals.css";
import { isLocale, LOCALES, translator as getTranslator, type Locale } from "@atlas/lib/i18n";
import { CartWidget } from "@atlas/components/CartWidget";

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

  return (
    <html lang={locale}>
      <body>
        <header className="topbar">
          <Link href={`/${locale}`} className="brand" data-testid="brand">
            {t("store.name")}
          </Link>
          <nav aria-label={t("nav.cart")}>
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
            <CartWidget locale={locale} label={t("nav.cart")} />
          </nav>
        </header>
        <main id="main">{children}</main>
        <footer className="footer">
          <small>{t("footer.evd")}</small>
        </footer>
      </body>
    </html>
  );
}
