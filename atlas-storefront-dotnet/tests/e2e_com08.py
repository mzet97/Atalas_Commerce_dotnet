#!/usr/bin/env python3
"""E2E COM-08: quote expirada rejeita + vigente confirma. Os hosts já devem estar no ar."""
import json, time, urllib.request, urllib.error, sys

BASE = "http://127.0.0.1:5297"

def post(url, data):
    req = urllib.request.Request(url, data=json.dumps(data).encode(), headers={"Content-Type": "application/json"})
    try:
        resp = urllib.request.urlopen(req)
        return resp.status, json.load(resp)
    except urllib.error.HTTPError as e:
        body = e.read().decode()
        try:
            return e.code, json.loads(body)
        except json.JSONDecodeError:
            return e.code, {"raw": body}

# 1) emite quote com TTL 1 segundo
resp_code, q_expired = post(f"{BASE}/quotes", {"cartId": "c-exp", "currency": "BRL", "ttlSeconds": 1,
    "lines": [{"skuId": "sku-com08", "quantity": 1, "unitPriceMinor": 2500}]})
print(f"QUOTE_EXPIRED: code={resp_code} id={q_expired.get('quoteId')}")
assert resp_code == 200, f"POST /quotes falhou: {q_expired}"

# 2) aguarda expirar
time.sleep(2)

# 3) checkout com quote EXPIRADA → deve REJEITAR (409)
code, result = post(f"{BASE}/checkout", {"idempotencyKey": "com08-exp", "customerId": "c1",
    "quoteId": q_expired["quoteId"],
    "lines": [{"skuId": "sku-com08", "quantity": 1, "unitPriceMinor": 2500}],
    "totalMinor": 2500, "currency": "BRL",
    "shipping": {"country": "BR", "postalCode": "01310", "line1": "Av", "city": "SP"}})
print(f"COM08_EXPIRED: code={code} status={result.get('status')} reason={result.get('reason')}")
assert code == 409, f"esperado 409 para quote expirada, recebido {code}: {result}"
assert "expirada" in str(result.get("reason", "")).lower() or "quote" in str(result.get("reason", "")).lower()
print("✓ COM-08 EXPIRADA: rejeitada com 409 ✓")

# 4) emite quote NOVA (vigente) e faz checkout → CONFIRMED
resp_code, q_valid = post(f"{BASE}/quotes", {"cartId": "c-vig", "currency": "BRL", "ttlSeconds": 600,
    "lines": [{"skuId": "sku-com08", "quantity": 1, "unitPriceMinor": 2500}]})
assert resp_code == 200, f"POST /quotes falhou: {q_valid}"

code, result = post(f"{BASE}/checkout", {"idempotencyKey": "com08-vig", "customerId": "c1",
    "quoteId": q_valid["quoteId"],
    "lines": [{"skuId": "sku-com08", "quantity": 1, "unitPriceMinor": 2500}],
    "totalMinor": 2500, "currency": "BRL",
    "shipping": {"country": "BR", "postalCode": "01310", "line1": "Av", "city": "SP"}})
print(f"COM08_VIGENTE: code={code} status={result.get('status')} orderId={result.get('orderId')}")
assert code == 200 and result.get("status") == 0, f"esperado CONFIRMED, recebido: {code} {result}"
print("✓ COM-08 VIGENTE: CONFIRMED ✓")

print("\n=== E2E COM-08 CONCLUÍDO COM SUCESSO ===")
