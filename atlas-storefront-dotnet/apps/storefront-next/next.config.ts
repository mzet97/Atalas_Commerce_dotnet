import type { NextConfig } from "next";

// Rewrites: /api/** da vitrine → BFF (borda única; evita CORS do fetch client-side)
const bffUrl = process.env.BFF_URL ?? "http://127.0.0.1:5180";

const nextConfig: NextConfig = {
  cacheComponents: true,
  partialPrefetching: true,
  async rewrites() {
    return [
      { source: "/api/:path*", destination: `${bffUrl}/api/:path*` },
    ];
  },
};

export default nextConfig;
