# SPEC-001 — acceptance.feature (gate M1)

Feature: Platform Foundation é reproduzível e observável
  # [M1] TEST-001-001 — REQ-PLT-100/104
  Scenario: Build determinístico dos três repositórios
    Given os três repositórios clonados no commit de referência
    When o CI do GitHub Actions executa build, test e format em runners limpos
    Then todos os jobs terminam verde com o mesmo conjunto de avisos
    And o restore usa lockfile/CPM sem resolução flutuante
    E o rebuild local do mesmo commit reproduz os binários

  # [M1] TEST-001-002 — REQ-PLT-102
  Scenario: Serviço e host admin respondem health com telemetria
    Given o Catalog Api e o Control Host iniciados localmente
    When um cliente consulta /health de cada um
    Then ambos respondem 200 com status detalhado
    E cada requisição gera um span OpenTelemetry exportado ao collector

  # [M1] TEST-001-003 — REQ-PLT-107 (BLOCKED até Docker)
  Scenario: Autenticação real cliente e admin via Keycloak
    Given o Compose profile core+data ativo com o realm importado
    When o script de smoke executa Authorization Code + PKCE para cliente e admin
    Then ambos obtêm tokens válidos com issuer/audience corretos
    E o admin é exigido MFA conforme configuração do realm

  # [M1] TEST-001-004 — REQ-PLT-106 (BLOCKED até Docker)
  Scenario: Primeiro trace cross-process
    Given Catalog Api e Control Host instrumentados com OTLP
    When uma requisição atravessa os dois hosts no smoke
    Then o trace aparece no Grafana/Tempo com spans dos dois processos
    E o correlation id propaga intacto entre eles

  # [M1] TEST-001-005 — REQ-PLT-101
  Scenario: Template de microserviço gera projeto que compila
    Given o template aspnet-microservice instalado a partir do repo de contratos
    When um novo serviço é gerado em pasta vazia
    Then o projeto gerado builda e passa os testes mínimos
    E o namespacing e a estrutura de camadas seguem o padrão aprovado
