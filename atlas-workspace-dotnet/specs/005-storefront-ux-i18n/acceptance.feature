# SPEC-005 — acceptance.feature (TEST-005-*)

Feature: Jornada de compra internacional
  Scenario: Mesma jornada nos três idiomas com moedas corretas
    Given produtos publicados nos mercados BR/US/UE com ofertas nas moedas locais
    When a jornada busca→PDP→carrinho executa em pt-BR, en-US e es-ES
    Then o conteúdo e a formatação seguem o locale ativo
    And o preço/moeda vêm da oferta do market selecionado
    E nenhuma conversão FX é usada como preço autoritativo

  Scenario: Fallback explícito de tradução
    Given um SKU sem tradução es-ES
    When o cliente navega em espanhol
    Then o conteúdo exibe fallback explícito sem quebrar a jornada nem o checkout

Feature: SEO técnico
  Scenario: hreflang e canonical por região
    Given páginas de produto nos três locales/mercados
    When um crawler inspeciona o head das páginas
    Then hreflang cobre os idiomas com x-default
    E canonical aponta para a URL canônica do market

Feature: Acessibilidade e estados
  Scenario: Auditoria axe nas jornadas principais
    When o axe executa nas páginas de busca, PDP e carrinho
    Then não há violações críticas WCAG 2.2 AA

  Scenario: Estado stale não promete checkout
    Given a projeção de estoque desatualizada mostrando disponível
    When o cliente tenta adicionar ao carrinho
    Then o sistema revalida no owner e informa indisponibilidade sem prometer checkout
