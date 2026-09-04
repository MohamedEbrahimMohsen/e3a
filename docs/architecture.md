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
- **Auth is a fragment handoff.** Creators sign in with GitHub; the API exchanges the code server-side, issues the same `CoreJwt` HS256 token every endpoint already validates, and hands it to the SPA in the URL fragment — never a cookie, never a query string. The anti-CSRF `state` is a nonce plus an expiry, HMAC-signed with the JWT key, and is bound to the initiating browser: `/api/auth/github/login` also sets a short-lived `Secure`, `HttpOnly`, `SameSite=Lax` cookie holding that nonce, and the callback rejects the state unless the cookie matches, then clears it. The cookie carries the nonce only — never the token — and the server keeps no state, so no cache and no extra Azure resource are needed. The SPA reads the token once from the fragment, strips the fragment with `history.replaceState`, and keeps it in `localStorage`; there is no refresh token, so the JWT's own expiry ends the session and a 401 signs the creator out.
- **Limits**: 50 engineers + 10 teams per creator; 10 members per team; 50 versions per item; version created only on explicit Publish.

## Publish pipeline (queue worker)

dequeue → ignore unless the version is `Queued`/`Building` → mark Building → **branch on
`ItemType` to build the plugin tree** → validate structure → security-scan the composed tree (any Block
finding → version `Rejected`, nothing uploaded) →
deterministic zip + sha256 → upload `public/z/{pluginName}/{semanticVersion}.zip` →
write the pinned `public/m/{pluginName}/{semanticVersion}/marketplace.json` →
persist Published + set the engineer's or team's `LatestVersionId` →
regenerate the root `marketplace.json`.

The two builds differ only in how the tree is produced; everything from validate onwards is shared:

- **Engineer**: freeze the creator's drafts into `snapshots/{versionId}` and assemble the tree from
  that snapshot filtered by the version's frozen import manifest.
- **Team**: read the ordered roster **frozen into the version row** — never from the live
  `TeamMembers` table — and read each pinned member's existing `snapshots/{pinnedVersionId}` prefix
  **read-only**, namespacing `skills/` and prefixing colliding `agents/`/`commands/` as they merge.
  Because a pinned snapshot prefix is never rewritten, a member republishing cannot change an
  already-published team.

Blob artefacts are written before `Published` is persisted, so a failed artefact write leaves the
version `Building` and the queue retry re-runs the whole tail — the zip upload is skipped when the
exact blob name already exists and the pinned marketplace is overwritten idempotently.

At most **two `SaveChangesAsync` calls** happen per job on every path (mark Building, then the
terminal Published-or-Failed write; a queue retry that resumes from `Building` does one). **No write
to the public container happens on any build-failure path**, for either item type: every build failure
returns before the zip is created. A failure *after* the zip upload — in the pinned-marketplace write
or in the terminal save — does leave that zip in the public container, with the version still
`Building`, exactly as described above.

Poison queue after `maxDequeueCount` (5) total attempts, including the first.

## Backend style

DDD/CQRS vertical slices on the AppTemplate: `E3A.Api` (thin controllers + resx resources)
→ `E3A.Application` (`{Area}/{UseCase}/{Command,Handler,Validator}` via MediatR 14 +
Core.CQRS pipeline) → `E3A.Domain` (Core.DDD aggregates, repository interfaces) →
`E3A.Infrastructure` (single `AppDbContext`, repositories, Blob/Queue/GitHub clients) ·
`E3A.Jobs` (isolated Functions worker whose functions are thin `mediator.Send`
shells) · `E3A.Tests`
(xUnit + NSubstitute + FluentAssertions per `conventions/dotnet-testing.md`). The plugin
builder and the marketplace generator are pure units in `E3A.Application/Publishing/Shared`, and
the security scanner is a pure unit in `E3A.Application/Publishing/Security`,
so the worker's whole pipeline is testable without a Functions host. Full
patterns: `.claude/skills/dotnet-feature/SKILL.md`; features are built through the
feature pipeline (`.process/{feature}/` plan → implementation → review artifacts).
