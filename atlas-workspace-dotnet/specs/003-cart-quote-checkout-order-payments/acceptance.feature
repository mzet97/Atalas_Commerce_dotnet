# SPEC-003 — acceptance.feature (DRAFT — TEST-003-*)

Feature: Compra de ponta a ponta com PSP fake determinístico
  # [M3] TEST-003-001 — REQ-CHK-002/003
  Scenario: Checkout aprovado cria exatamente um pedido confirmado
    Given carrinho com 1 SKU disponível e quote vigente
    When o cliente submete checkout com idempotency_key K e PSP cenário APPROVED
    Then a resposta é CONFIRMED com número de pedido
    And o snapshot do pedido é imutável e igual à quote
    E reserva confirmada e pagamento capturado existem uma única vez

  # [M3] TEST-003-002 — UC003-T02 · INV-PAYMENT-001
  Scenario: Retry com a mesma idempotency key não cobra duas vezes
    Given checkout K já confirmado
    When o cliente reenvia SubmitCheckout com a mesma K
    Then o resultado anterior é retornado sem nova reserva/autorização

  # [M3] TEST-003-003 — UC003-T03 · ADR-0006
  Scenario: Timeout do PSP vira estado pendente reconciliável
    Given PSP cenário TIMEOUT
    When o checkout é submetido
    Then a resposta é PENDING com referência de pedido
    E o reconciliador confirma ou compensa sem perda silenciosa

  # [M3] TEST-003-004 — UC003-T05
  Scenario: Webhook duplicado e fora de ordem não duplica efeitos
    Given intenção com captura já reconhecida
    When webhooks duplicados e um evento antigo chegam
    Then nenhuma transição inválida é aplicada
    E nenhum reembolso/captura extra é gerado

  # [M3] TEST-003-005 — UC003-T01/INV-STOCK-001 (E2E)
  Scenario: Dois clientes compram a última unidade simultaneamente
    Given on_hand 1 e duas sessões distintas
    When ambos submetem checkout concorrentemente
    Then um pedido é CONFIRMED e o outro REJECTED com requote
    E oversell = 0 e duplicate_paid_order = 0

Feature: Quote vigente é obrigatória
  # [M3] TEST-003-006 — INV-QUOTE-001 · COM-08
  Scenario: Cotação expirada exige re-cotação
    Given quote expirada por TTL
    When o cliente confirma checkout
    Then a operação é recusada com exigência de requote
    E a diferença de valores é apresentada após nova cotação

Feature: Compensação coerente
  # [M3] TEST-003-007 — ADR-0006
  Scenario: Recusa de pagamento libera reserva e registra falha
    Given PSP cenário DECLINED
    When o checkout avança até a autorização
    Then a reserva é liberada e o pedido vai a PAYMENT_FAILED
    E o cliente vê resultado terminal com motivo

  # [M3] TEST-003-008 — matriz de falhas §Falhas
  Scenario: Worker reinicia entre commit e ack
    Given saga em AUTHORIZING com commit realizado
    When o processo cai e reinicia
    Then a saga retoma do último estado persistido
    E nenhuma etapa é executada duas vezes
