# SPEC-000 — risks.md (específicos da modelagem)

Ver matriz global em `sdd/risks.md`. Riscos específicos do business discovery:

| Risco | Impacto na modelagem | Mitigação aplicada |
|---|---|---|
| Interpretação divergente das fontes (blueprint vs mestre) | Modelo inconsistente | Precedência aplicada (mestre > references > discovery); divergências D-1..D-4 registradas e resolvidas |
| Invariante sem dono de verificação | Gap de teste futuro | Toda INV mapeada a TEST/milestone em `traceability.md` |
| Caso de uso crítico sem cenário executável | Gate sem prova | 12 cenários Gherkin cobrindo os 5 críticos exigidos + extras |
| Escopo do Assistente IA ambíguo ("compra assistida") | Risco de violar INV-AI-001 | ADR-0009 define boundary de consentimento; matriz de tools em ADR-0015 |
| NFRs tratados como resultados | Expectativa irreal | Todos marcados PROPOSED; gates L0–L4 congelados apenas antes de cada benchmark |
| Modelagem 1P contaminada por 3P antecipado | Complexidade indevida | Seller modelado mas M10 DEFERRED; slice inaugural 1P |
