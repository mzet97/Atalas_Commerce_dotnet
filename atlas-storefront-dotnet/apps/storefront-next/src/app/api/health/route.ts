import { NextResponse } from "next/server";

// Health para probes do K8s (liveness/readiness da vitrine).
export async function GET() {
  return NextResponse.json({ ok: true, service: "storefront-next" });
}
