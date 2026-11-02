# Technology verification matrix — snapshot 2026-10-27

This is a **dated snapshot**, not a promise that the same versions remain current. Implementation AI MUST independently check official NuGet/releases/CLI in its environment and keep a pinned `docs/dependency-matrix.md` with links, compatibility, vulnerabilities, licensing and lockfile evidence.

| Component | Verification / status on specification date | Primary official source |
|---|---|---|
| .NET 10 | LTS, supported to Nov 14, 2028; latest runtime 10.0.14 (Oct 25, 2026), SDK 10.0.403 | https://dotnet.microsoft.com/en-us/platform/support/policy ; https://dotnet.microsoft.com/en-us/download/dotnet/10.0 |
| C# 14 | .NET 10 SDK language support | https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/ |
| MAF core | `Microsoft.Agents.AI` 1.26.0 (Oct 23, 2026) | https://www.nuget.org/packages/Microsoft.Agents.AI |
| MAF workflows | `Microsoft.Agents.AI.Workflows` 1.26.0 (Oct 23, 2026) | https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows |
| MAF workflows .NET | graph-based `WorkflowBuilder`, typed executors, checkpoint semantics | https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/ |
| MAF providers | `IChatClient` adapters incl. Ollama; verify function calling with actual model | https://learn.microsoft.com/en-us/agent-framework/agents/providers/ |
| .NET Aspire | CLI `aspire init`, `aspire run`, optional developer composition | https://learn.microsoft.com/en-us/dotnet/aspire/cli/overview |
| ASP.NET Core/gRPC | .NET 10 & .proto schema-first | https://learn.microsoft.com/en-us/aspnet/core/grpc/?view=aspnetcore-10.0 |

**UNVERIFIED HERE — implementation AI must inspect:** EF Core 10 patch, MongoDB.Driver, Npgsql, Dapper, Confluent.Kafka, RabbitMQ.Client, StackExchange.Redis, Keycloak, OTel .NET, Testcontainers for .NET, xUnit v3, Next.js, Angular, Node, pnpm, Redis, PostgreSQL, MongoDB, Kafka, RabbitMQ, Helm, K8s, Skaffold, Grafana Stack and container base digests. Use compatible latest stable versions when creating actual app. Do not claim evaluated license compatibility without checking.

**License investigation required:** MassTransit, MediatR, FluentAssertions and any commercial extensions. A license risk should cause library selection ADR with alternatives and consequence, not unapproved adoption. `Microsoft.Agents.AI` NuGet package lists MIT in the reviewed snapshot; recheck dependencies/terms when pinning.

**Skaffold is not project code scaffolding.** Use `dotnet new`, `pnpm create next-app`, Angular CLI, custom `dotnet new` templates; Skaffold handles K8s dev workflows, Docker Compose handles local full lifecycle. Aspire complements development only.
