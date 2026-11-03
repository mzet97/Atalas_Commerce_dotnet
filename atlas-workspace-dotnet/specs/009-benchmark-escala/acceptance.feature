# SPEC-009 — acceptance.feature (TEST-009-*)

Feature: Benchmarks são reproduzíveis e honestos
  Scenario: Run L1 é reproduzível por comando documentado
    Given o ambiente L1 no commit S com seed versionada
    When dois runs independentes executam
    Then os percentis ficam dentro do intervalo declarado
    E o relatório contém hardware, parâmetros e artefatos brutos

  Scenario: Invariantes valem sob carga
    Given um run de checkout concorrente no estágio ativo
    When a carga atinge o pico do gate
    Then oversell = 0 e duplicate_paid_order = 0
    E nenhuma projeção duplicada é observada

  Scenario: Spike não derruba invariantes nem perde pedidos
    Given steady state estável em L2
    When um spike 10x chega
    Then o sistema degrada latência sem violar correção
    E se recupera ao steady state após o pico
