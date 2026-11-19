import { Suspense } from "react";
import { notFound } from "next/navigation";
import { isLocale, translator, type Locale } from "@atlas/lib/i18n";
import { OrderDetail } from "@atlas/components/OrderDetail";

export const metadata = { title: "Pedido" };

async function OrderBody({ params }: { params: Promise<{ lang: string; id: string }> }) {
  const { lang, id } = await params;
  if (!isLocale(lang)) notFound();
  const locale = lang as Locale;
  const t = translator(locale);
  return (
    <div className="page">
      <h1>{t("orders.detailTitle")}</h1>
      <OrderDetail
        locale={locale}
        orderId={id}
        labels={{
          loading: t("state.loading"),
          notFound: t("orders.notFound"),
          back: t("orders.back"),
          status: t("orders.status"),
          total: t("orders.total"),
          version: t("orders.version"),
        }}
      />
    </div>
  );
}

// params são RUNTIME: a promise desce DENTRO do Suspense (mesmo padrão da PDP).
export default function OrderPage({ params }: { params: Promise<{ lang: string; id: string }> }) {
  return (
    <Suspense fallback={<p data-testid="loading">…</p>}>
      <OrderBody params={params} />
    </Suspense>
  );
}
