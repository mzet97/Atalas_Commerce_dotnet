# SPEC-011 — design.md

- **Idiomas:** documentação técnica pública em EN; internos pt-BR; ADRs ganham versão EN ao serem `ACCEPTED` (arquivo `ADR-NNNN-en.md` junto ao original).
- **Quickstart por repo:** `README.md` com pré-requisitos exatos (versões), `make up`/script único de demo (Compose profiles), `make e2e`, mapa de portas/credenciais locais e troubleshooting.
- **Matriz projetado×implementado×medido:** tabela raiz no repo de coordenação (e resumida em cada README): colunas requisito/alegação, status (PROPOSED/IMPLEMENTED/MEASURED/BLOCKED), link da EVD/commit, limitação.
- **Demo:** roteiros scripts `demo/*.sh` (cenários: compra feliz; PSP timeout→reconciliação; publicação admin→projeção com lag visível; assistente com consent + injection attempt) + gravações curtas por cenário publicadas em `docs/demo/`.
- **CI público:** GitHub Actions badges; upload de artefatos (relatórios de teste, traces Playwright, resultados de benchmark) por release tag.

## Testes
Execução do quickstart em ambiente limpo (container de CI como "terceiro"); verificador de links da matriz apontando para arquivos existentes.
