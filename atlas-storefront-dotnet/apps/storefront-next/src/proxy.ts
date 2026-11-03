import { NextResponse, type NextRequest } from "next/server";
import { DEFAULT_LOCALE, LOCALES } from "@atlas/lib/i18n";

// Detecção de locale (guia oficial Next 16): middleware renomeado para proxy.ts.
// Rotas vivem sob /{lang}/ — sem locale, redireciona com base em Accept-Language.
export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  if (LOCALES.some((l) => pathname === `/${l}` || pathname.startsWith(`/${l}/`)))
    return NextResponse.next();

  const accept = request.headers.get("accept-language") ?? "";
  const detected = LOCALES.find((l) => accept.toLowerCase().startsWith(l.toLowerCase().slice(0, 2))) ?? DEFAULT_LOCALE;
  const url = request.nextUrl.clone();
  url.pathname = `/${detected}${pathname === "/" ? "" : pathname}`;
  return NextResponse.redirect(url);
}

export const config = {
  matcher: ["/((?!_next|favicon.ico|api).*)"],
};
