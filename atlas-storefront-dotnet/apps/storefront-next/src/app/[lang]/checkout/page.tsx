import { notFound } from "next/navigation";
import { isLocale, translator, type Locale } from "@atlas/lib/i18n";
import { CheckoutForm } from "@atlas/components/CheckoutForm";

export const metadata = { title: "Checkout" };

export default async function CheckoutPage({ params }: { params: Promise<{ lang: string }> }) {
  const { lang } = await params;
  if (!isLocale(lang)) notFound();
  const locale = lang as Locale;
  const t = translator(locale);

  return (
    <div className="page">
      <h1 data-testid="checkout-title">{t("checkout.title")}</h1>
      <CheckoutForm
        locale={locale}
        labels={{
          total: t("cart.total"),
          customerName: t("checkout.customer"),
          country: t("checkout.country"),
          postalCode: t("checkout.postalCode"),
          addressLine1: t("checkout.addressLine1"),
          city: t("checkout.city"),
          submitting: t("checkout.submitting"),
          placeOrder: t("checkout.placeOrder"),
          confirmed: t("checkout.confirmed"),
          pending: t("checkout.pending"),
          rejected: t("checkout.rejected"),
          orderId: t("checkout.orderId"),
          thanks: t("checkout.thanks"),
          newOrder: t("checkout.newOrder"),
          noQuote: t("checkout.noQuote"),
          backToCart: t("checkout.backToCart"),
        }}
      />
    </div>
  );
}
