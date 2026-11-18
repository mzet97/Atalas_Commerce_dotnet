import { test, expect } from "@playwright/test";

// fatia 9: página de login renderiza nos 3 locales sem Keycloak (sessão ausente
// → formulário de login; login real OIDC exige o Keycloak do deploy).
const LOCALES = [
  { lang: "pt-BR", button: "Entrar com Atlas ID" },
  { lang: "en-US", button: "Sign in with Atlas ID" },
  { lang: "es-ES", button: "Entrar con Atlas ID" },
];

for (const l of LOCALES) {
  test(`login ${l.lang}: formulário visível sem sessão`, async ({ page }) => {
    await page.goto(`/${l.lang}/login`);
    await expect(page.getByTestId("login-page")).toBeVisible();
    await expect(page.getByTestId("login")).toContainText(l.button);
  });
}

test("nav: link de login presente na home pt-BR", async ({ page }) => {
  await page.goto("/pt-BR");
  await expect(page.getByTestId("nav-login")).toBeVisible();
});
