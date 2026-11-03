# SPEC-001 — risks.md

| Risco | P | I | Mitigação |
|---|---|---|---|
| Docker ausente bloqueia Compose/Keycloak/Testcontainers/OTel-local (EVD-000-003) | Alta (constatado) | Alto | Instalação pelo proprietário (exige root); tarefas sem Docker avançam primeiro; gates parciais marcados BLOCKED — nunca simulados |
| GitHub Actions sem remoto configurado | Média | Médio | Workflows commitados e validados por `act` local se disponível; push exige remoto do proprietário |
| `.slnx`/templates imaturos no SDK 10 | Baixa | Médio | Fallback documentado para `.sln` se `dotnet sln` não suportar; teste desde pasta vazia no CI |
| Versões de pacote (Keycloak/Postgres/Mongo) mudarem desde o snapshot | Média | Baixo | dependency-matrix com pin por digest; revalidação no bootstrap |
| Scope creep (criar serviços além do mínimo) | Média | Médio | ADR-0013; revisão de PR contra SPEC-001 |
