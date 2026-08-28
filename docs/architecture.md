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
   │                                      └── EF Core ──► Azure SQL Basic (catalog index)
   │
   │                                    E3A.Jobs (Azure Functions v4 isolated, .NET 10)
   │                                      ◄── Storage Queue publish-jobs
   │
   └──► Cloudflare Worker ──► Blob Storage: public/ (zips + marketplace.json),
        (/marketplace.json, /z/*)          drafts/, snapshots/ (private)
```

## Principles

- **Reads never hit the API.** `marketplace.json` and plugin zips are served from Blob; freshness is governed by cache headers written at blob write time — `marketplace.json` gets `public, max-age=60` and zips get `public, max-age=31536000, immutable`. The API handles auth, drafts, publishing, and the website's catalog browse — so scale-to-zero cold starts are irrelevant for plugin consumers.
- **Versions are immutable.** A published zip at `/z/{name}/{semver}.zip` never changes; sha256 recorded in the DB and in the marketplace entry.
- **Public-only in v0.1.** No private items, no multi-tenancy. Login (GitHub OAuth) is required only to create/publish/vote.
- **Limits**: 50 engineers + 10 teams per creator; 50 versions per item; version created only on explicit Publish.

## Publish pipeline (queue worker)

dequeue → ignore unless the version is `Queued`/`Building` → mark Building →
freeze drafts into `snapshots/{versionId}` → assemble the plugin tree from the snapshot +
the frozen import manifest → validate structure → *(security scan — next slice)* →
deterministic zip + sha256 → upload `public/z/{pluginName}/{semanticVersion}.zip` →
write the pinned `public/m/{pluginName}/{semanticVersion}/marketplace.json` →
persist Published + set the engineer's `LatestVersionId` →
regenerate the root `marketplace.json`.

Blob artefacts are written before `Published` is persisted, so a failed artefact write leaves the
version `Building` and the queue retry re-runs the whole tail — the zip upload is skipped when the
exact blob name already exists and the pinned marketplace is overwritten idempotently.

Poison queue after `maxDequeueCount` (5) total attempts, including the first.

## Backend style

DDD/CQRS vertical slices on the AppTemplate: `E3A.Api` (thin controllers + resx resources)
→ `E3A.Application` (`{Area}/{UseCase}/{Command,Handler,Validator}` via MediatR 14 +
Core.CQRS pipeline) → `E3A.Domain` (Core.DDD aggregates, repository interfaces) →
`E3A.Infrastructure` (single `AppDbContext`, repositories, Blob/Queue/GitHub clients,
scanner) · `E3A.Jobs` (isolated Functions worker whose functions are thin `mediator.Send`
shells) · `E3A.Tests`
(xUnit + NSubstitute + FluentAssertions per `conventions/dotnet-testing.md`). The plugin
builder and the marketplace generator are pure units in `E3A.Application/Publishing/Shared`,
so the worker's whole pipeline is testable without a Functions host. Full
patterns: `.claude/skills/dotnet-feature/SKILL.md`; features are built through the
feature pipeline (`.process/{feature}/` plan → implementation → review artifacts).
