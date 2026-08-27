# e3a Architecture

## Overview

> Revised 2026-08-27: backend is the **E3A ASP.NET Core API** (scaffolded from the
> AppTemplate with vendored `core-libraries`), hosted on **Azure Container Apps**
> (scale-to-zero). The earlier Functions-only design is superseded.

```
Browser ──► Azure Static Web Apps (React SPA, free tier)
   │
   ├──► Cloudflare ──► api.<domain> ──► E3A.Api (ASP.NET Core, .NET 10, Container Apps)
   │                                      ├── Controllers → MediatR slices (Core.CQRS)
   │                                      ├── BackgroundService ◄── Storage Queue (publish pipeline)
   │                                      └── EF Core ──► Azure SQL Basic (catalog index)
   │
   └──► Cloudflare Worker ──► Blob Storage: public/ (zips + marketplace.json),
        (/marketplace.json, /z/*)          drafts/, snapshots/ (private)
```

## Principles

- **Reads never hit the API.** `marketplace.json` and plugin zips are served from Blob via Cloudflare cache; the API only handles auth, drafts, and publishing — so scale-to-zero cold starts are irrelevant for consumers.
- **Versions are immutable.** A published zip at `/z/{name}/{semver}.zip` never changes; sha256 recorded in the DB and in the marketplace entry.
- **Public-only in v0.1.** No private items, no multi-tenancy. Login (GitHub OAuth) is required only to create/publish/vote.
- **Limits**: 50 engineers + 10 teams per creator; 50 versions per item; version created only on explicit Publish.

## Publish pipeline (queue worker)

dequeue → mark Building → assemble plugin tree (draft assets or member snapshots) →
validate structure → security scan (fail = Rejected + per-file report) →
deterministic zip + sha256 → upload to Blob → snapshot assets (engineers) →
mark Published → regenerate marketplace.json → purge Cloudflare cache.

Poison queue after 3 retries → version marked Failed.

## Backend style

DDD/CQRS vertical slices on the AppTemplate: `E3A.Api` (thin controllers + resx resources)
→ `E3A.Application` (`{Area}/{UseCase}/{Command,Handler,Validator}` via MediatR 14 +
Core.CQRS pipeline) → `E3A.Domain` (Core.DDD aggregates, repository interfaces) →
`E3A.Infrastructure` (single `AppDbContext`, repositories, Blob/Queue/GitHub clients,
scanner, plugin builder, marketplace generator, Cloudflare purger) · `E3A.Tests`
(xUnit + NSubstitute + FluentAssertions per `conventions/dotnet-testing.md`). Full
patterns: `.claude/skills/dotnet-feature/SKILL.md`; features are built through the
feature pipeline (`.process/{feature}/` plan → implementation → review artifacts).
