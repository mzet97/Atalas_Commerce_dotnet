"use client";

import { useState } from "react";
import { ProductThumb } from "@atlas/components/ProductThumb";

// Galeria da PDP: foto principal + miniaturas (troca sem reload).
export function ProductGallery({ images, title }: { images: string[]; title: string }) {
  const [selected, setSelected] = useState(0);
  if (images.length === 0) return <ProductThumb big label={title} />;
  const current = images[Math.min(selected, images.length - 1)];
  return (
    <div className="gallery-view">
      <div className="thumb big photo">
        {/* eslint-disable-next-line @next/next/no-img-element */}
        <img src={current} alt={title} data-testid="pdp-photo" />
      </div>
      {images.length > 1 && (
        <div className="thumbs" role="group" aria-label="Fotos">
          {images.map((src, i) => (
            <button
              key={src}
              type="button"
              className={i === selected ? "thumb mini active" : "thumb mini"}
              onClick={() => setSelected(i)}
              aria-pressed={i === selected}
              aria-label={`Foto ${i + 1}`}
            >
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={src} alt="" aria-hidden="true" loading="lazy" />
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
