# SPEC-006 — acceptance.feature (TEST-006-*)

Feature: Entrega e devolução coerentes
  Scenario: Pedido confirmado gera fulfillment e entrega rastreável
    Given um pedido CONFIRMED com 2 itens
    When o fulfillment processa picking/packing e expede em 2 remessas
    Then cada Shipment tem etiqueta e tracking do carrier fake
    E a timeline do cliente mostra os eventos em ordem
    E o pedido vai a DELIVERED apenas com proof-of-delivery

  Scenario: Devolução aprovada gera exatamente um refund
    Given entrega concluída dentro da janela de política
    When o cliente solicita devolução e um approver distinto aprova
    Then o RMA avança e o Payments executa refund único com idempotency
    E o estorno aparece na projeção de reconciliação

  Scenario: RMA reprovado não gera refund
    Given inspeção reprovando a devolução por condição do item
    Then o fluxo termina REJECTED com auditoria
    E nenhuma chamada de refund ocorre

Feature: Notificações são exatamente-uma-percebidas
  Scenario: Mensagem venenosa vai para DLQ e é replayável
    Given um template de notificação falhando repetidamente
    When o worker excede o limite de retries
    Then a mensagem vai à DLQ sem bloquear a fila
    E o replay via runbook entrega sem duplicar para o cliente

  Scenario: Broker reinicia durante consumo
    Given consumer processando lote com ack manual
    When o RabbitMQ reinicia no meio
    Then nenhuma notificação é perdida nem entregue duas vezes
