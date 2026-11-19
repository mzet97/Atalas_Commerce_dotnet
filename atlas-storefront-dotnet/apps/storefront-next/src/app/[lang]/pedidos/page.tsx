import { isLocale, translator, type Locale } from "@atlas/lib/i18n";
import { OrdersView } from "@atlas/components/OrdersView";

export const metadata = { title: "Pedidos" };

export default async function OrdersPage({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params;
  if (!isLocale(lang)) return null;
  const locale = lang as Locale;
  const t = translator(locale);
  return (
    <div className="page">
      <section data-testid="orders-page">
        <h1>{t("orders.title")}</h1>
        <OrdersView
          locale={locale}
          labels={{
            loading: t("state.loading"),
            empty: t("orders.empty"),
            login: t("orders.login"),
            items: t("orders.items"),
            view: t("orders.view"),
          }}
        />
      </section>
    </div>
  );
}
