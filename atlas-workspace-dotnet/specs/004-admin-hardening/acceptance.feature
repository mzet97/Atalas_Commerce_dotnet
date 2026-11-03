# SPEC-004 — acceptance.feature (TEST-004-*)

Feature: Segregação de deveres administrativa
  Scenario: Solicitante tenta aprovar o próprio ajuste de alto impacto
    Given um operador solicitou ajuste de estoque no warehouse W1
    When o mesmo operador tenta aprovar a própria solicitação
    Then a ação é negada pelo domínio e auditada
    And o agregado de inventário permanece inalterado

  Scenario: Aprovação revogada antes do dispatch
    Given uma aprovação concedida e depois revogada por um iam_admin
    When o comando é despachado ao owner
    Then o owner nega por token revogado/expirado
    And a negação é registrada na auditoria

Feature: Auditoria é imutável e verificável
  Scenario: Cadeia de hashes da auditoria
    Given N ações administrativas registradas
    When o verificador reconstrói a cadeia prev_hash/self_hash
    Then a cadeia é válida do primeiro ao último registro
    And qualquer alteração retroativa em um registro invalida a cadeia

Feature: Escopos ABAC são dinâmicos
  Scenario: Operador fora do escopo recebe 403 auditado
    Given um catalog_editor chamando ajuste de estoque via API
    Then o owner responde HTTP 403
    And a tentativa é registrada na trilha de auditoria

Feature: UI expõe defasagem CQRS
  Scenario: Dashboard mostra freshness das projeções
    Given um comando de publicação recém-executado
    When o operador abre o dashboard administrativo
    Then o estado exibe PUBLISHING com source_version alvo
    E muda para SYNCED quando a projeção alcança a versão
