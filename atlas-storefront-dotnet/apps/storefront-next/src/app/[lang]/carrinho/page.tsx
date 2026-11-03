import { notFound } from "next/navigation";
import { isLocale, translator, type Locale } from "@atlas/lib/i18n";
import { CartView } from "@atlas/components/CartView";

export const metadata = { title: "Carrinho" };

export default async function CartPage({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params;
  if (!isLocale(lang)) notFound();
  const locale = lang as Locale;
  const t = translator(locale);

  return (
    <div className="page">
      <h1 data-testid="cart-title">{t("cart.title")}</h1>
      <CartView
        locale={locale}
        labels={{
          title: t("cart.title"),
          item: t("cart.item"),
          quantity: t("cart.quantity"),
          unitPrice: t("cart.unitPrice"),
          remove: t("cart.remove"),
          total: t("cart.total"),
          quote: t("cart.quote"),
          quoting: t("cart.quoting"),
          quoteReady: t("cart.quoteReady"),
          quoteExpires: t("cart.quoteExpires"),
          quoteError: t("cart.quoteError"),
          checkout: t("cart.checkout"),
          empty: t("cart.empty"),
          backToStore: t("nav.home"),
        }}
      />
    </div>
  );
}
