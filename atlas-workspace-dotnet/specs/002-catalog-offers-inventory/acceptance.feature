# SPEC-002 — acceptance.feature (DRAFT — TEST-002-*)

Feature: Publicação de produto é de ponta a ponta e auditável
  # [M2] TEST-002-001 — REQ-CAT-001/002 · UC-005
  Scenario: Editor publica produto aprovado e a vitrine reflete
    Given editor com escopo market BR e produto em rascunho com traduções e SKU
    When um approver distinto aprova a publicação no Control
    Then o Catalog owner persiste agregado e outbox em uma transação
    And a projeção catalog_product_search eventualmente contém o produto
    And a vitrine Next.js o exibe com source_version visível no admin
    And a auditoria registra actor, before/after hash e correlation id

  # [M2] TEST-002-002 — ADM-05 · INV-SEC-001
  Scenario: Operador sem escopo recebe 403 auditado
    Given um catalog_editor tentando ajustar estoque do warehouse W1
    When a chamada passa pelo Control e chega ao Inventory owner
    Then o comando é negado com 403 no owner
    And a tentativa fica registrada na trilha de auditoria

Feature: Oferta única ativa por escopo
  # [M2] TEST-002-003 — REQ-OFF-001
  Scenario: Segunda oferta ativa no mesmo escopo é rejeitada
    Given uma Offer ativa para SKU X, seller 1P, market BR, canal web
    When outra Offer é publicada com a mesma chave de escopo simultaneamente
    Then exatamente uma permanece ativa e a outra é rejeitada com motivo

Feature: Projeção é idempotente e reconstrutível
  # [M2] TEST-002-004 — INV-EVT-001 · ADR-0003
  Scenario: Evento duplicado não duplica projeção
    Given o evento ProductPublished v3 já aplicado na projeção
    When o mesmo event_id é entregue novamente pelo Kafka
    Then a projeção permanece idêntica (inbox pula o evento)
    And a entrega duplicada é contabilizada em métrica

  # [M2] TEST-002-005 — INV-EVT-001
  Scenario: Replay do tópico reconstrói a projeção fielmente
    Given a projeção catalog_product_search vazia intencionalmente
    When o worker consome o tópico desde o offset inicial
    Then o conteúdo final é idêntico à projeção prévia
    E o relatório de rebuild mostra eventos aplicados e pulados

Feature: Estoque não permite oversell sob concorrência
  # [M2] TEST-002-006 — INV-STOCK-001/003
  Scenario: Múltiplas reservas concorrentes pela última unidade
    Given on_hand 1, reserved 0 para o SKU Y no warehouse W1
    When 10 reservas concorrentes são solicitadas
    Then exatamente 1 fica ACTIVE e 9 recebem OUT_OF_STOCK
    E on_hand permanece 1 e reserved 1

  # [M2] TEST-002-007 — INV-STOCK-003
  Scenario: Ajuste de estoque exige motivo e ledger
    Given um inventory_operator com escopo W1
    When executa AdjustStock -2 com justificativa
    Then a posição é atualizada por CAS e um movement é gravado
    And reason, actor e correlation_id estão presentes no ledger

Feature: Limites do Control respeitados
  # [M2] TEST-002-008 — ADR-0001
  Scenario: Control não escreve direto no Mongo dos owners
    Given os módulos Control em execução com credenciais apenas do próprio banco
    When qualquer fluxo administrativo de produto/oferta/estoque executa
    Then toda mutação ocorre por comando gRPC autenticado ao owner
    E o teste de arquitetura prova ausência de cliente Mongo dos owners no Control
