# EVD-001-007 — TASK-001-008: smoke OIDC real (PKCE + MFA + JWKS)

**Data UTC:** 2026-10-29 · **Resultado:** PASS · **Alvo:** Keycloak 26.0.7 real (EVD-001-006), realm `atlas`

## 1) Workload interna (client_credentials) — atlas-services

```text
POST /realms/atlas/protocol/openid-connect/token (grant_type=client_credentials, secret)
→ access_token OK | iss: http://127.0.0.1:8090/realms/atlas | exp 300s
→ JWKS: kid do token presente no /protocol/openid-connect/certs = True  (assinatura validável offline)
```

## 2) Cliente final — Authorization Code + PKCE S256 (storefront-next / customer-smoke)

Fluxo completo executado por script (cookie jar + form POST):

```text
GET  /auth?...&code_challenge=S256...           → 200 login form
POST /login-actions/authenticate (user/pass)     → 302 → http://localhost:3000/callback?code=f6f78663-...
POST /token (grant_type=authorization_code + code + code_verifier)
→ access_token OK | azp: storefront-next | iss: .../realms/atlas | exp 300s
→ id_token: True | refresh_token: True
```

## 3) Admin exige MFA (INV/REQ-IAM: MFA para operadores privilegiados)

```text
Login de atlas-admin → 302 para:
  /login-actions/required-action?execution=CONFIGURE_TOTP&client_id=storefront-next...
Página renderizada: "Authenticator Setup — Authenticator to activate your account."
→ Sem TOTP configurado o admin NÃO recebe code/token: política MFA enforced pelo realm.
```

## Limitações honestas

- Enriquecimento do TOTP (scan de QR/código) é passo interativo de primeiro acesso — não automatizado no smoke; a exigência da required-action é a evidência da política.
- `direct access grants` desligados nos clients públicos (só PKCE); `atlas-services` confidential com service account.
- Tokens de exemplo não persistidos (efêmeros, credenciais dev-only nomeadas).
