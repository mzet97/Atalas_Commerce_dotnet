// M9: dataset 10^6 SKUs — gera títulos/SKUs determinísticos (seed versionado em git)
import { writeFileSync } from "fs";
const N = 1_000_000;
const lines = [];
for (let i = 0; i < N; i++) {
  const sku = `SKU-BENCH-${String(i).padStart(7, "0")}`;
  const mkt = ["BR", "US", "UE"][i % 3];
  lines.push(`${sku},${mkt},Produto Bench ${i % 100},${1000 + (i % 5000) * 10}`);
}
writeFileSync("bench/dataset-skus.csv", "sku,market,title_base,price_minor\n" + lines.join("\n"));
console.log(`dataset: ${N} skus`);
