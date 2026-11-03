import { test, expect } from "@playwright/test";

// E2E M5: jornada de compra completa nos 3 idiomas (UC-001→UC-003)
const JOURNEYS = [
  { lang: "pt-BR", search: "Câmera", productText: "Câmera Atlas Pro X200" },
  { lang: "en-US", search: "Headphones", productText: "Atlas Wireless Headphones H50" },
  { lang: "es-ES", search: "Atlas", productText: "Atlas Watch W10" },
];

for (const j of JOURNEYS) {
  test(`compra completa em ${j.lang}`, async ({ page }) => {
    await page.goto(`/${j.lang}`);

    // home: busca filtra a vitrine (aguarda o item FILTRADO — evita clicar na lista velha)
    await page.getByTestId("search-input").waitFor();
    await page.getByTestId("search-input").fill(j.search);
    await page.getByTestId("search-button").click();
    const target = page
      .getByTestId("product-item")
      .filter({ hasText: j.productText })
      .first()
      .locator("a");
    await expect(target).toBeVisible({ timeout: 15_000 });
    await target.click();
    await page.waitForURL("**/produto/**");

    // PDP: preço da oferta + add ao carrinho
    await expect(page.getByTestId("pdp-price")).toBeVisible();
    await page.getByTestId("add-to-cart").click();
    await expect(page.getByTestId("cart-count")).toHaveText("1");

    // carrinho: emite quote (UC-002)
    await page.getByTestId("cart-link").click();
    await page.locator('[data-testid^="cart-line-"]').first().waitFor();
    await page.getByTestId("request-quote").click();
    await expect(page.getByTestId("quote-result")).toBeVisible({ timeout: 15_000 });

    // checkout: submete com PSP APPROVED → Confirmed (UC-003)
    await page.getByTestId("go-checkout").click();
    await page.getByTestId("customer").fill("comprador-e2e");
    await page.getByTestId("postal-code").fill("01310-100");
    await page.getByTestId("line1").fill("Av. Paulista 1000");
    await page.getByTestId("city").fill("São Paulo");
    await page.getByTestId("place-order").click();
    await expect(page.getByTestId("checkout-outcome-confirmed")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("order-id")).toBeVisible();
  });
}

// fluxo alternativo: busca sem resultado → estado vazio (UC-001 alt)
test("busca sem resultado exibe estado vazio", async ({ page }) => {
  await page.goto("/pt-BR");
  await page.getByTestId("search-input").waitFor();
  await page.getByTestId("search-input").fill("produto-inexistente-xyz");
  await page.getByTestId("search-button").click();
  await expect(page.getByTestId("empty-state")).toBeVisible();
});

// redirect sem locale → locale padrão (proxy)
test("raiz redireciona para locale padrão", async ({ page }) => {
  await page.goto("/");
  await expect(page).toHaveURL(/\/(pt-BR|en-US|es-ES)/);
});
