# SPEC-010 — spec.md (esqueleto DEFERRED — detalhar somente quando ativado)

**Gatilho de ativação:** decisão expressa do proprietário + ADR de tenancy/settlement aprovada + baseline 1P estável (M3+ PASS).

## Requisitos condicionais (rascunho)

1. **Seller onboarding:** verificação, contratos, escopo `seller_id` em toda a cadeia (IAM ABAC já prevê).
2. **Ofertas concorrentes:** buy-box policy por seller/market com regras auditáveis.
3. **Tenancy:** isolamento total de dados/acesso (zero vazamento — teste obrigatório de tenant isolation).
4. **Settlement/split:** ledger financeiro por seller, ciclos de repasse, estornos proporcionais; conciliação com PSP real.
5. **Antifraude básico:** score de pedido/pagamento (regra local), fila de revisão.
6. **Contratos:** `seller_id` em eventos/commands/projeções (evolução backward-compatible do envelope).

## Gates (quando ativo)

Zero violação de tenant/escopo em testes automatizados; settlement correto em cenários de compra/refund parcial; regressão 1P verde.

## Arquivos restantes

`design/tasks/acceptance/traceability/evidence/risks/audit` serão criados **na ativação** — manter este esqueleto é a posição correta do contrato (mestre §12-M10: "não antecipar implementação no MVP").
