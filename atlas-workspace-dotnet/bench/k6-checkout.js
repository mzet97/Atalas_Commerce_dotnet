// M9 L0/L1 (SPEC-009): mix 80 browse / 12 search / 5 PDP / 2 cart / 0.7 checkout / 0.3 AI
// executa contra o BFF :5180 e vitrine :3000 — gates congelados em gates.json (por run)
import http from "k6/http";
import { check, sleep } from "k6";
export const options = {
  scenarios: {
    constant: { executor: "constant-arrival-rate", rate: __ENV.RATE ? Number(__ENV.RATE) : 50,
                timeUnit: "1s", duration: __ENV.DURATION || "10m", preAllocatedVUs: 100, maxVUs: 400 },
  },
  thresholds: { http_req_failed: ["rate<0.001"], "http_req_duration": ["p(95)<1500"] },
};
const BFF = __ENV.BFF || "http://127.0.0.1:5180";
export default function () {
  const r = Math.random();
  if (r < 0.80) { http.get(`${BFF}/api/catalog/search`); }
  else if (r < 0.92) { http.get(`${BFF}/api/catalog/search?q=camera`); }
  else if (r < 0.97) { http.get(`${BFF}/api/catalog/products`); }
  else {
    const q = http.post(`${BFF}/api/checkout/quotes`, JSON.stringify({
      cartId: `k6-${__VU}`, currency: "BRL", ttlSeconds: 600,
      lines: [{ skuId: "SKU-CAMERA-BR", quantity: 1, unitPriceMinor: 249900 }] }),
      { headers: { "Content-Type": "application/json" } });
    check(q, { "quote 200": (res) => res.status === 200 });
  }
  sleep(0.1);
}
