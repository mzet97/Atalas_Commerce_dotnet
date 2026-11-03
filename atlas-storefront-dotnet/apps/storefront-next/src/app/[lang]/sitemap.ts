import type { MetadataRoute } from "next";
import { LOCALES } from "@atlas/lib/i18n";

// SEO (M5/M8): sitemap com alternates por locale — ids dinâmicos via BFF seriam RSC runtime;
// sitemap estático das rotas + entry por locale (honesto: produtos via metadata dinâmico na PDP)
export default function sitemap(): MetadataRoute.Sitemap {
  const base = process.env.STOREFRONT_URL ?? "http://localhost:3000";
  const entries: MetadataRoute.Sitemap = [];
  for (const lang of LOCALES) {
    entries.push({ url: `${base}/${lang}`, changeFrequency: "daily", priority: 1 });
    entries.push({ url: `${base}/${lang}/carrinho`, changeFrequency: "weekly", priority: 0.3 });
    entries.push({ url: `${base}/${lang}/checkout`, changeFrequency: "weekly", priority: 0.3 });
  }
  return entries;
}
