// Auth do buyer (fatia 9): OIDC Authorization Code + PKCE direto contra o Keycloak
// (client público `storefront`; redirect precisa estar registrado no client do realm).
// Sessão em memória + sessionStorage (aba); sub do `sub` do access token — nunca
// auto-assertado: o BFF valida a assinatura e injeta X-Authenticated-Subject (INV-SEC-001).
"use client";

export type BuyerSession = {
  sub: string;
  username: string;
  accessToken: string;
  expiresAt: number;
};

const LS_KEY = "atlas.buyer.v1";
const LS_VERIFIER = "atlas.pkce.v1";

function keycloakBase(): string {
  return process.env.NEXT_PUBLIC_KEYCLOCK_URL ?? "http://127.0.0.1:8080";
}
function realm(): string {
  return process.env.NEXT_PUBLIC_KEYCLOCK_REALM ?? "atlas";
}
function clientId(): string {
  return process.env.NEXT_PUBLIC_KEYCLOCK_CLIENT_ID ?? "storefront";
}

function randomVerifier(): string {
  const bytes = new Uint8Array(48);
  crypto.getRandomValues(bytes);
  return btoa(String.fromCharCode(...bytes))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

async function challengeS256(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier));
  return btoa(String.fromCharCode(...new Uint8Array(digest)))
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

export async function beginLogin(locale: string): Promise<void> {
  const verifier = randomVerifier();
  const challenge = await challengeS256(verifier);
  sessionStorage.setItem(LS_VERIFIER, verifier);
  const redirect = `${window.location.origin}/${locale}/login`;
  const url =
    `${keycloakBase()}/realms/${realm()}/protocol/openid-connect/auth` +
    `?response_type=code&client_id=${encodeURIComponent(clientId())}` +
    `&redirect_uri=${encodeURIComponent(redirect)}` +
    `&scope=${encodeURIComponent("openid profile")}` +
    `&code_challenge=${challenge}&code_challenge_method=S256`;
  window.location.href = url;
}

export async function handleCallback(locale: string): Promise<BuyerSession | null> {
  const params = new URLSearchParams(window.location.search);
  const code = params.get("code");
  if (!code) return readSession();
  const verifier = sessionStorage.getItem(LS_VERIFIER);
  if (!verifier) return null;
  const redirect = `${window.location.origin}/${locale}/login`;
  const res = await fetch(
    `${keycloakBase()}/realms/${realm()}/protocol/openid-connect/token`,
    {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "authorization_code",
        code,
        redirect_uri: redirect,
        client_id: clientId(),
        code_verifier: verifier,
      }),
    }
  );
  if (!res.ok) return null;
  const body = (await res.json()) as { access_token?: string };
  if (!body.access_token) return null;
  const payload = JSON.parse(atob(body.access_token.split(".")[1])) as {
    sub?: string;
    preferred_username?: string;
    exp?: number;
  };
  if (!payload.sub) return null;
  const session: BuyerSession = {
    sub: payload.sub,
    username: payload.preferred_username ?? payload.sub,
    accessToken: body.access_token,
    expiresAt: (payload.exp ?? 0) * 1000,
  };
  sessionStorage.setItem(LS_KEY, JSON.stringify(session));
  sessionStorage.removeItem(LS_VERIFIER);
  window.history.replaceState({}, "", `/${locale}/login`);
  window.dispatchEvent(new Event("atlas:buyer"));
  return session;
}

export function readSession(): BuyerSession | null {
  if (typeof window === "undefined") return null;
  try {
    const raw = sessionStorage.getItem(LS_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw) as BuyerSession;
    if (!s.accessToken || !s.sub || Date.now() > s.expiresAt) {
      sessionStorage.removeItem(LS_KEY);
      return null;
    }
    return s;
  } catch {
    return null;
  }
}

export function logout(): void {
  sessionStorage.removeItem(LS_KEY);
  sessionStorage.removeItem(LS_VERIFIER);
  window.dispatchEvent(new Event("atlas:buyer"));
}

export function authHeader(): Record<string, string> {
  const s = readSession();
  return s ? { Authorization: `Bearer ${s.accessToken}` } : {};
}
