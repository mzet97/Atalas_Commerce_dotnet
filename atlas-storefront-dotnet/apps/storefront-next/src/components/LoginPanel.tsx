"use client";

import { useEffect, useState } from "react";
import {
  beginLogin,
  handleCallback,
  logout,
  readSession,
  type BuyerSession,
} from "@atlas/lib/auth";

export function LoginPanel({
  locale,
  labels,
}: {
  locale: string;
  labels: Record<string, string>;
}) {
  const [session, setSession] = useState<BuyerSession | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    handleCallback(locale)
      .then((s) => {
        setSession(s);
        setReady(true);
      })
      .catch(() => setReady(true));
    const sync = () => setSession(readSession());
    window.addEventListener("atlas:buyer", sync);
    return () => window.removeEventListener("atlas:buyer", sync);
  }, [locale]);

  if (!ready) return <p data-testid="login-loading">…</p>;

  if (session) {
    return (
      <div data-testid="login-hello">
        <p>
          {labels.hello}, <strong data-testid="buyer-name">{session.username}</strong> ({session.sub})
        </p>
        <button type="button" data-testid="logout" onClick={logout}>
          {labels.logout}
        </button>
      </div>
    );
  }

  return (
    <div data-testid="login-form">
      <p>{labels.blurb}</p>
      <button type="button" data-testid="login" onClick={() => beginLogin(locale)}>
        {labels.button}
      </button>
    </div>
  );
}
