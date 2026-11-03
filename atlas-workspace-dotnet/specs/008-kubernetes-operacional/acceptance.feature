# SPEC-008 — acceptance.feature (TEST-008-*)

Feature: Cluster operacional verificável
  Scenario: Deploy completo sobe saudável
    Given o cluster não-prod com namespaces Atlas preparados
    When os charts são instalados via Helm
    Then todos os pods ficam Ready com probes verdes
    E o ingress responde TLS nas rotas principais

  Scenario: Escalonamento reage à carga
    When uma carga sintética eleva consumer lag e CPU
    Then KEDA escala os workers e o HPA escala as APIs
    E os eventos de escala ficam registrados

  Scenario: Rollback restaura a versão anterior
    Given um rollout de nova imagem em andamento
    When o rollback é executado
    Then a versão anterior volta a servir sem estado inconsistente

  Scenario: Restore drill recupera dados verificáveis
    Given backups recentes de PostgreSQL e MongoDB
    When o restore executa em ambiente de teste
    Then um pedido confirmado é legível e consistente pós-restore
    E o smoke de checkout funciona contra os dados restaurados
