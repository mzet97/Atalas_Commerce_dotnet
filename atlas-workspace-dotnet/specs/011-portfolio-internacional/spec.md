# SPEC-011 — spec.md

**Problema:** um avaliador externo (recrutador/pares) precisa clonar, subir a demo, executar E2E e verificar as alegações arquiteturais — tudo em inglês, sem marketing.

## Requisitos

1. **ADRs em inglês** (`architecture/decisions/en/` ou bilíngue por arquivo) com trade-off essays: modular monolith vs microservices; CQRS poliglota; at-least-once + idempotência; consent boundary da IA.
2. **READMEs** por repo com quickstart real (pré-requisitos, comandos, o que sobe, o que esperar), arquitetura (C4), e matriz **projetado × implementado × medido** com links para EVD.
3. **Demo:** roteiro de demonstração (checkout com falha controlada sem pedido duplicado; publicação admin→projeção; assistente com consent) + vídeo curto por fluxo.
4. **CI público:** badge/status dos pipelines; artefatos de teste (Playwright traces/videos) publicados.
5. **Postmortems reais:** incidentes ocorridos durante o desenvolvimento com lições.
6. **Limitações:** seção obrigária "o que NÃO está implementado/medido" (multi-região, PSP real, HA completo, 3P).

## Critérios de aceite

Terceiro segue o quickstart e sobe o ambiente sem ajuda; E2E roda por comando único; toda alegação do README tem link para evidência; versão EN revisada.

## DoD
Relatório M11 consolidado; checklist "clone→run→verify" executado por alguém que não escreveu o projeto (ou ferramenta equivalente documentada).
