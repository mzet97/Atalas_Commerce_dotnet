# Imagens do Atlas no k8s .51 (ns dotnet-atlas)

Convenções (contrato com os charts): repositórios `atlas/<svc>`, porta 8080
(3000 na vitrine), contexto de build = **raiz do monorepo** (para resolver a
ref cross-repo `atlas-storefront-dotnet → atlas-platform-contracts-dotnet`;
ver `.dockerignore` na raiz).

## Serviços .NET (`Dockerfile.service` — parametrizado)

```bash
# Exemplo (Gateway); repetir por serviço com seu CSPROJ/ASSEMBLY:
docker build -f atlas-storefront-dotnet/deploy/docker/Dockerfile.service \
  --build-arg REPO=atlas-storefront-dotnet \
  --build-arg CSPROJ=src/Gateway/Atlas.Gateway/Atlas.Gateway.csproj \
  --build-arg ASSEMBLY=Atlas.Gateway \
  -t atlas/gateway:TAG .
```

## Vitrine Next (`Dockerfile.next`)

```bash
# BFF_URL é assado no build (rewrites do Next); para o cluster:
docker build -f atlas-storefront-dotnet/deploy/docker/Dockerfile.next \
  --build-arg BFF_URL=http://atlas-gateway \
  -t atlas/storefront-web:TAG .
```

## Angular control (`atlas-control-dotnet/deploy/docker/`) — mesmo padrão.

## Publicar no cluster

Imagens `local-*` vão para o containerd do nó (ex.: salvar/carregar tarball e
`ctr images import` no `.51`, ou push ao Harbor `harbor.home.arpa`). Tags são
`IfNotPresent`: **nunca reutilizar uma tag para um build novo** (o CRI não
puxa de novo — ver tag `local-f3-takeover1` no `values-prod.yaml`).
Depois: `helm upgrade -f values-prod.yaml` no release correspondente.
