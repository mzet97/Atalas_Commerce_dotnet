# ADR BASE — Edição .NET 10: substituições e precedência

**Status:** CONFIRMED by owner's latest instruction (scope/technology); details open through ADRs.  
**Date:** 2026-10-27.

## Precedence

1. Owner's latest explicit instructions.
2. `PROMPT_MASTER_ATLAS_COMMERCE_DOTNET10_MAF_SDD.md`.
3. `references/dotnet/*.md` and architecture diagram files.
4. Domain-invariant and business-process docs from `atlas-commerce-discovery`.
5. Accepted ADRs inside the target .NET repositories.
6. Prior architecture documentation written for Python — **LEGACY TECHNOLOGY REFERENCE**.

## Conversions table

| Previous edition | New edition | Status |
|---|---|---|
| FastAPI + Python microservices | ASP.NET Core 10 + C# 14 microservices | CONFIRMED |
| FastAPI modular monolith | ASP.NET Core 10 modular monolith | CONFIRMED |
| Python Workers/asyncio | .NET Worker Services, async I/O, CancellationToken | CONFIRMED |
| uv/pyproject/uv.lock | dotnet SDK, NuGet Central Package Management/lock | CONFIRMED |
| Copier/FastAPI templates | `dotnet new` official and tested custom templates | CONFIRMED |
| LangChain + LangGraph in Python | Microsoft Agent Framework in C# | CONFIRMED |
| Model inference runtime | Ollama / compatible endpoint optional (provider adapter) | PROPOSED |
| `MediatR` by default | explicit handlers; mediation lib requires ADR/license | PROPOSED |
| MongoDB write side | MongoDB write side | CONFIRMED |
| PostgreSQL read side | PostgreSQL read projections | CONFIRMED |
| CQRS+outbox+Kafka | same semantic guarantees, C# implementations | CONFIRMED |
| RabbitMQ, Redis, gRPC | same tools, .NET SDKs | CONFIRMED |
| Keycloak OIDC, RBAC/ABAC | same tools and policies in ASP.NET Core | CONFIRMED |
| Next.js public site, Angular admin | unchanged | CONFIRMED |
| Compose, Skaffold, K8s | unchanged; Aspire optional for DX | CONFIRMED |
| Grafana Stack/OpenTelemetry | unchanged, .NET instrumentation | CONFIRMED |
| Pytest/pytest-asyncio | xUnit or approved .NET runner, Testcontainers for .NET | CONFIRMED |
| Playwright | maintained for E2E and cross-app journeys | CONFIRMED |

**Business invariants do not change when language changes.** Order snapshot, money with currency, stock non-negativity, owner authorization, transactional outbox, idempotency, dual-approval and LLM non-authority remain in force.

## Explicit exclusions

- Do not produce Python services, FastAPI endpoints, LangChain, LangGraph, or Semantic Kernel as agent orchestration; do not use the legacy Python SDD prompt.
- Avoid dependency on Microsoft Foundry/Azure for local operation; model API access remains provider-configurable.
- Do not promise 1M purchases/sec without hardware, contract and payment-capacity proof.
- Do not use shared `DbContext`, `MongoDatabase`, cross-repository assemblies or entities across service ownership boundaries.
- Do not create a microservice for every bounded context on day 1; grow by meaningful vertical slices.

## Engineering principle

Current business specifications describe **what must be true**. This document and the new master describe **how to implement it using .NET 10**. The receiving implementation AI should map any conflict with IDs, source files and an ADR, rather than silently replacing the input.
