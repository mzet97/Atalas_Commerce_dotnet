# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: purchase.spec.ts >> compra completa em en-US
- Location: tests/e2e/purchase.spec.ts:11:7

# Error details

```
Test timeout of 60000ms exceeded.
```

```
Error: locator.waitFor: Test timeout of 60000ms exceeded.
Call log:
  - waiting for getByTestId('search-input') to be visible

```

# Page snapshot

```yaml
- generic [active] [ref=e1]:
  - banner [ref=e2]:
    - link "Atlas Storefront" [ref=e3] [cursor=pointer]:
      - /url: /en-US
    - navigation "Cart" [ref=e4]:
      - list [ref=e5]:
        - listitem [ref=e6]:
          - link "PT" [ref=e7] [cursor=pointer]:
            - /url: /pt-BR
        - listitem [ref=e8]:
          - link "EN" [ref=e9] [cursor=pointer]:
            - /url: /en-US
        - listitem [ref=e10]:
          - link "ES" [ref=e11] [cursor=pointer]:
            - /url: /es-ES
      - link "🛒 Cart0" [ref=e12] [cursor=pointer]:
        - /url: /en-US/carrinho
  - main [ref=e13]:
    - generic [ref=e15]:
      - heading "International commerce with a trustworthy checkout" [level=1] [ref=e16]
      - paragraph [ref=e17]: Storefront unavailable — try again
  - contentinfo [ref=e18]: M5 · CQRS projections · checkout with saga and simulated PSP
  - alert [ref=e19]
```

# Test source

```ts
  1  | import { test, expect } from "@playwright/test";
  2  | 
  3  | // E2E M5: jornada de compra completa nos 3 idiomas (UC-001→UC-003)
  4  | const JOURNEYS = [
  5  |   { lang: "pt-BR", search: "Câmera", productText: "Câmera Atlas Pro X200" },
  6  |   { lang: "en-US", search: "Headphones", productText: "Atlas Wireless Headphones H50" },
  7  |   { lang: "es-ES", search: "Atlas", productText: "Atlas Watch W10" },
  8  | ];
  9  | 
  10 | for (const j of JOURNEYS) {
  11 |   test(`compra completa em ${j.lang}`, async ({ page }) => {
  12 |     await page.goto(`/${j.lang}`);
  13 | 
  14 |     // home: busca filtra a vitrine (aguarda o item FILTRADO — evita clicar na lista velha)
> 15 |     await page.getByTestId("search-input").waitFor();
     |                                            ^ Error: locator.waitFor: Test timeout of 60000ms exceeded.
  16 |     await page.getByTestId("search-input").fill(j.search);
  17 |     await page.getByTestId("search-button").click();
  18 |     const target = page
  19 |       .getByTestId("product-item")
  20 |       .filter({ hasText: j.productText })
  21 |       .first()
  22 |       .locator("a");
  23 |     await expect(target).toBeVisible({ timeout: 15_000 });
  24 |     await target.click();
  25 |     await page.waitForURL("**/produto/**");
  26 | 
  27 |     // PDP: preço da oferta + add ao carrinho
  28 |     await expect(page.getByTestId("pdp-price")).toBeVisible();
  29 |     await page.getByTestId("add-to-cart").click();
  30 |     await expect(page.getByTestId("cart-count")).toHaveText("1");
  31 | 
  32 |     // carrinho: emite quote (UC-002)
  33 |     await page.getByTestId("cart-link").click();
  34 |     await page.locator('[data-testid^="cart-line-"]').first().waitFor();
  35 |     await page.getByTestId("request-quote").click();
  36 |     await expect(page.getByTestId("quote-result")).toBeVisible({ timeout: 15_000 });
  37 | 
  38 |     // checkout: submete com PSP APPROVED → Confirmed (UC-003)
  39 |     await page.getByTestId("go-checkout").click();
  40 |     await page.getByTestId("customer").fill("comprador-e2e");
  41 |     await page.getByTestId("postal-code").fill("01310-100");
  42 |     await page.getByTestId("line1").fill("Av. Paulista 1000");
  43 |     await page.getByTestId("city").fill("São Paulo");
  44 |     await page.getByTestId("place-order").click();
  45 |     await expect(page.getByTestId("checkout-outcome-confirmed")).toBeVisible({ timeout: 30_000 });
  46 |     await expect(page.getByTestId("order-id")).toBeVisible();
  47 |   });
  48 | }
  49 | 
  50 | // fluxo alternativo: busca sem resultado → estado vazio (UC-001 alt)
  51 | test("busca sem resultado exibe estado vazio", async ({ page }) => {
  52 |   await page.goto("/pt-BR");
  53 |   await page.getByTestId("search-input").waitFor();
  54 |   await page.getByTestId("search-input").fill("produto-inexistente-xyz");
  55 |   await page.getByTestId("search-button").click();
  56 |   await expect(page.getByTestId("empty-state")).toBeVisible();
  57 | });
  58 | 
  59 | // redirect sem locale → locale padrão (proxy)
  60 | test("raiz redireciona para locale padrão", async ({ page }) => {
  61 |   await page.goto("/");
  62 |   await expect(page).toHaveURL(/\/(pt-BR|en-US|es-ES)/);
  63 | });
  64 | 
```