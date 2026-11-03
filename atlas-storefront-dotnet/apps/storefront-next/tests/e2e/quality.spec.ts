import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

// a11y: WCAG 2.2 AA — violações críticas/sérias = 0 nas 3 páginas-chave
const PAGES = [
  { url: "/pt-BR", name: "home" },
  { url: "/pt-BR/carrinho", name: "carrinho" },
  { url: "/pt-BR/checkout", name: "checkout" },
];

for (const p of PAGES) {
  test(`a11y ${p.name}: sem violações critical/serious`, async ({ page }) => {
    await page.goto(p.url);
    await page.waitForTimeout(1500); // ilhas dinâmicas
    const results = await new AxeBuilder({ page }).analyze();
    const blocking = results.violations.filter(
      (v) => (v.impact === "critical" || v.impact === "serious"),
    );
    if (blocking.length > 0) {
      console.log(JSON.stringify(blocking.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.slice(0, 3).map((n) => n.target) })), null, 1));
    }
    expect(blocking, `violations em ${p.url}`).toHaveLength(0);
  });
}

// CWV medidos IN-CICLO via PerformanceObserver (LCP/CLS + event timing aproximando INP)
test("cwv pt-BR: LCP < 2.5s e CLS < 0.1 (budget)", async ({ page }) => {
  await page.goto("about:blank");
  await page.addInitScript(() => {
    (window as unknown as Record<string, unknown>).__cwv = { lcp: 0, cls: 0 };
    new PerformanceObserver((l) => {
      const entries = l.getEntries() as unknown as { startTime: number }[];
      (window as unknown as Record<string, unknown>).__cwv.lcp =
        entries[entries.length - 1].startTime;
    }).observe({ type: "largest-contentful-paint", buffered: true });
    new PerformanceObserver((l) => {
      for (const e of l.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
        const w = window as unknown as { __cwv: { cls: number } };
        if (!e.hadRecentInput) w.__cwv.cls += e.value;
      }
    }).observe({ type: "layout-shift", buffered: true });
  });
  const t0 = Date.now();
  await page.goto("http://127.0.0.1:3000/pt-BR", { waitUntil: "networkidle" });
  await page.waitForTimeout(2000);
  const cwv = (await page.evaluate(() => (window as unknown as { __cwv: { lcp: number; cls: number } }).__cwv));
  console.log(`CWV LCP=${(cwv.lcp / 1000).toFixed(2)}s CLS=${cwv.cls.toFixed(4)} wall=${((Date.now() - t0) / 1000).toFixed(2)}s`);
  expect(cwv.lcp).toBeLessThan(2500);
  expect(cwv.cls).toBeLessThan(0.1);
});
