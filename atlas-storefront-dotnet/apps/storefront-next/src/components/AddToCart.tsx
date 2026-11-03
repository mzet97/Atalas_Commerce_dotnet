"use client";

import { useState } from "react";
import { addToCart } from "@atlas/lib/cart";

export function AddToCart({
  locale,
  labels,
  line,
}: {
  locale: string;
  labels: { add: string; added: string };
  line: { skuId: string; title: string; unitPriceMinor: number; currency: string };
}) {
  const [added, setAdded] = useState(false);
  return (
    <button
      type="button"
      className="primary"
      data-testid="add-to-cart"
      onClick={() => {
        addToCart(line);
        setAdded(true);
        setTimeout(() => setAdded(false), 2000);
      }}
    >
      {added ? labels.added : labels.add}
    </button>
  );
}
