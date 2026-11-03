# SPEC-011 — acceptance.feature (TEST-011-*)

Feature: Portfólio é verificável por terceiros
  Scenario: Avaliador clona e sobe a demo
    Given uma máquina limpa com apenas os pré-requisitos documentados
    When o avaliador executa o quickstart do README
    Then o ambiente sobe com health verde
    E o smoke de checkout de ponta a ponta passa por comando único

  Scenario: Toda alegação tem prova ou limitação declarada
    When o avaliador inspeciona qualquer afirmação de capacidade no portfólio
    Then existe evidência datada vinculada (EVD/commit)
    Or a limitação está explicitamente declarada na matriz
