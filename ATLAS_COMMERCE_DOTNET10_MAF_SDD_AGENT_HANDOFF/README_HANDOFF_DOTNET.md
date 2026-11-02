# Atlas Commerce .NET 10 / Microsoft Agent Framework — Agent Handoff

**Release of the specification:** 2026-10-27 · **Status:** design/implementation contract; no software implementation is claimed.

## Start here

1. Read `PROMPT_MASTER_ATLAS_COMMERCE_DOTNET10_MAF_SDD.md` in full.
2. Read `references/dotnet/00-ADAPTATION-AND-PRECEDENCE.md` before the original blueprint.
3. Audit the provided 8 business docs + 12 diagram source files in `atlas-commerce-discovery/`.
4. Read all the `references/dotnet/` documents and diagrams.
5. On the first execution perform **M0 recon/business modeling only** and prepare the foundation specs. **No `dotnet new`, Docker boot, GPU/cloud spend or code scaffolding in M0.**

## Target structure

- `atlas-storefront-dotnet`: Next.js international storefront + ASP.NET Core 10 microservices + C# MAF shopping assistant.
- `atlas-control-dotnet`: Angular administrative portal + ASP.NET Core 10 modular monolith.
- `atlas-platform-contracts-dotnet`: independent contracts/templates/infra repo.

**Important:** The original blueprint was authored for the **Python/FastAPI + LangGraph** edition. Its *business entities, workflows and diagrams* are valid inputs; its *technology stack* is superseded in this edition by .NET 10 and MAF. The provided original files are retained as provenance, not automatically rewritten.

## Contents

- Master prompt: SDD execution contract in Markdown.
- Business blueprint and 8 discovery Markdown docs (read as business references).
- 12 original edit-source diagrams (Mermaid / PlantUML).
- 5 adaptation documents for .NET architecture, MAF, roadmap and dependencies.
- 3 new edit-source .NET diagrams.
- `SOURCE_MANIFEST_SHA256.json` for integrity.

## No claims of implementation

This ZIP is a design handoff, not a compiled application. The receiving AI must run tools and report actual build/test/benchmark evidence before declaring milestones PASS.
