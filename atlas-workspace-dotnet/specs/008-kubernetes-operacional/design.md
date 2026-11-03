# SPEC-008 — design.md

- **Charts:** `deploy/helm/{storefront,control,workers,data,messaging,observability}`; values `dev/nonprod`; imagens pinadas por digest; `helm lint`+`template` gates no CI (GitHub Actions).
- **Skaffold:** `skaffold dev` apontando ao cluster local; profiles dev/nonprod.
- **KEDA ScaledObject** para projection/task workers por `consumer_lag` (Kafka) e fila Rabbit; HPA CPU p/ APIs.
- **Dados:** por padrão StatefulSet+PV com backup scripts; operadores (bitnami/cloudnative-pg/mongo-operator) conforme disponibilidade do cluster — decisão registrada em execução com trade-off documentado; **não chamar pod único de HA**.
- **Restore drill:** job que restaura PG/Mongo em namespace `restore-test` e executa smoke de leitura de pedido + healthcheck; saída anexada à EVD.
- **Observabilidade:** cluster próprio (kube-prometheus-stack) + OTel Collector como gateway de telemetria dos namespaces Atlas.

## Testes
`helm lint/template` no CI; smoke pós-deploy (health de todos os componentes); teste de rollout (imagem anterior) e rollback; restore drill; carga sintética curta para HPA/KEDA reagirem (evidenciada em eventos de escala).
