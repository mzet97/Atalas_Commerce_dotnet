# Secrets do namespace `dotnet-atlas` (.51)

Fonte de verdade dos NOMES/CHAVES: `atlas-data.example.yaml` e `atlas-hmac.example.yaml`
neste diretório. Os valores reais **nunca** entram no git (repo público) — os Secrets
vivos hoje no cluster foram criados por comando imperativo; para recriar, usar os
comandos documentados nos cabeçalhos dos `.example.yaml`.

| Secret | Chaves | Consumidores |
|---|---|---|
| `atlas-data` | `mongo-connstr`, `pg-connstr`, `redis-connstr`, `rabbit-url` | todos os serviços via `secretKeyRef` |
| `atlas-hmac` | `consent-secret`, `approval-token-secret`, `psp-webhook-secret` | assistant, payments, psp-fake, control |

Regra: os três segredos HMAC devem ser idênticos entre os serviços que os validam
(emissor × validador com valor diverso = 403 legítimo, não bug).
