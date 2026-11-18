import { notFound } from "next/navigation";
import { isLocale, translator as getTranslator, type Locale } from "@atlas/lib/i18n";
import { LoginPanel } from "@atlas/components/LoginPanel";

export default async function LoginPage({
  params,
}: {
  params: Promise<{ lang: string }>;
}) {
  const { lang } = await params;
  if (!isLocale(lang)) notFound();
  const locale = lang as Locale;
  const t = getTranslator(locale);
  const labels: Record<string, string> = {
    hello: t("login.hello"),
    logout: t("login.logout"),
    blurb: t("login.blurb"),
    button: t("login.button"),
  };
  return (
    <section data-testid="login-page">
      <h1>{t("login.title")}</h1>
      <LoginPanel locale={locale} labels={labels} />
    </section>
  );
}
