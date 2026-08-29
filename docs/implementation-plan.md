# e3a — Engineer as an Agent: v0.1 Implementation Plan

## Context

e3a is a free community product + portfolio piece for a solo senior .NET/Azure engineer. Developers compose **AI engineers** (skills + persona → one Claude Code plugin) and **teams** (bundles of pinned engineer versions → one plugin) on a website, publish them publicly with versioning, and anyone installs them into Claude Code natively — no login needed to browse/install. Origin: the builder once had to rebuild a colleague's AI environment from scratch because no tool could share it; e3a makes working setups shareable and teams reproducible.

**Verified Claude Code facts the design relies on** (official docs): `/plugin marketplace add <https-URL-to-marketplace.json>` is natively supported; plugin `source` type `archive` (HTTPS zip + sha256) works with URL-added marketplaces (relative paths do NOT); per-entry `version` field drives update propagation.

**Locked scope (v0.1)**: engineer creation is **UPLOAD-ONLY** (revised 2026-08-23 — creator uploads their whole `.claude` folder; e3a sanitizes, scans, and normalizes it into a plugin with an import manifest showing imported/converted/skipped items — see docs/plugin-spec.md); team composer (pinned snapshots, merge rules in plugin-spec); publish + version-on-Publish-only (limits: 50 engineers, 10 teams per creator, 50 versions per item); anonymous public catalog with install counts (progressive sparkline) + report button + copy-install commands + per-version pinned marketplaces. Hooks are imported with warnings (script-tier scan + loud detail-page warning). **Out**: workflow builder (v0.2), skill-picking composer (deferred), GitHub-URL import (deferred), private items, export-to-my-GitHub, MCP server, eval scoring, likes (replaced by install counts).

**Locked stack (revised 2026-08-27)**: React 18 + TS + Vite on Azure Static Web Apps (free) · **E3A ASP.NET Core API (.NET 10, AppTemplate scaffold + vendored core-libraries, MediatR 14, controllers) on Azure Container Apps scale-to-zero** · publish pipeline as an isolated Azure Functions worker (`E3A.Jobs`, .NET 10 v4) reading Storage Queue `publish-jobs` · Azure Blob · Azure SQL Basic (~$5/mo) + EF Core · GitHub OAuth (creators only) · Cloudflare CDN/rate-limit. Total ≈ $5–8/mo. The Functions-only design (§2.1–2.2 below) is SUPERSEDED — kept for history; the engine components (PluginBuilder, scanner, composer, generator) await recreation inside the new solution. Backend patterns: `.claude/skills/dotnet-feature/SKILL.md`; features go through the feature pipeline with artifacts in `.process/`.

## Key architecture decisions

### Superseded (kept for history — see the Locked stack paragraph above for the current design)

1. **Free SWA + standalone Function App** (not SWA Standard, not managed API): SPA on free SWA; separate .NET 10 Function App at `api.<domain>` with CORS, its own GitHub OAuth code exchange, and self-issued JWTs (HS256). Managed API is disqualified because the publish pipeline needs a **queue trigger**.
   - Superseded 2026-08-27: the API is E3A.Api on Azure Container Apps; E3A.Jobs is the only Functions host and carries the queue trigger.
2. **Backend = vertical slices without MediatR**: 3 projects — `E3a.Functions` (thin HTTP/queue triggers), `E3a.Core` (`Features/<Area>/<UseCase>/{Command,Handler,Validator,Response}`, `Domain/`, `Infrastructure/`), `E3a.Core.Tests` (xUnit; heaviest on PluginBuilder, SecurityScanner, MarketplaceGenerator). FluentValidation; EF Core directly (no repo layer).
   - Superseded 2026-08-27: five projects (E3A.Api / E3A.Application / E3A.Domain / E3A.Infrastructure / E3A.Jobs) with MediatR 14 and a repository layer.

### Current

3. **Serving**: `marketplace.json` + zips live in Blob; a free-tier **Cloudflare Worker** proxies `<domain>/marketplace.json` and `<domain>/z/*` to Blob. freshness comes from cache headers written at blob write time — zips `public, max-age=31536000, immutable` (versioned URLs), `marketplace.json` `public, max-age=60`. No cache purge step.
4. **Votes need login** (one switchable ±1 row per user/item, denormalized counts shown anonymously); reports allowed anonymous behind Cloudflare rate limiting.

## Repo layout (D:\Personal\_e3a, monorepo)

```
web/            Vite + React 18 + TS strict (+ staticwebapp.config.json)
api/            E3a.sln → src/E3a.Functions, src/E3a.Core, tests/E3a.Core.Tests
infra/          main.bicep + modules/{swa,functions,storage,sql}.bicep
seed/           flagship engineers as source folders + seed script
docs/           architecture.md, plugin-spec.md, security-scan.md
.github/workflows/  web.yml, api.yml, infra.yml (OIDC federated creds, no secrets)
```

## Data model (SQL, EF Core)

- `users`: Id, GitHubId (unique), GitHubLogin, DisplayName, AvatarUrl, IsBlocked
- `engineers` / `teams` (separate tables, near-identical shape — `teams` carries no `DraftManifestJson` and no `InstallCount`, because a team has no upload draft and install counting is not built yet): Id, OwnerUserId, Slug (unique, creator-typed kebab-case `^[a-z0-9]+(-[a-z0-9]+)*$`, 3–`SlugMaxLength` characters, reserved words rejected from a config list, auto-suffixed via IGenerator only as a collision race guard; editable while `LatestVersionId` is null and frozen after the first publish; the slug is the entire plugin name — `e3a-{slug}` for engineers and `e3a-team-{slug}` for teams, so the two slug namespaces cannot collide and a team may reuse an engineer's slug; GitHub login is not part of the plugin identity), DisplayName, Description, Tags(json), Status(Draft|Published|Unlisted|Deleted), DraftManifestJson, LatestVersionId, InstallCount. Schema/business caps (lengths, tag counts, per-creator limits) live in `[Area]Options` bound from appsettings — never as entity constants.
- `team_members`: Id (PK), TeamId, EngineerId, EngineerSlug, PinnedVersionId, PinnedSemanticVersion, SortOrder; unique index on (TeamId, EngineerId) filtered on `IsDeleted = 0`. `EngineerId` and `PinnedVersionId` carry no foreign key, so a member engineer can be deleted without breaking an already-published team.
- `versions`: Id, ItemType, ItemId, VersionNumber, SemanticVersion, FrozenManifestJson, ZipBlobPath, ZipSha256, SizeBytes, Status(Queued|Building|Published|Rejected|Failed), FailureReason; unique (ItemType, ItemId, VersionNumber). `ScanReportJson` arrives with the `security-scan` slice, which owns its shape.
- `likes`: (UserId, ItemType, ItemId) PK, Value ±1
- `reports`: Id, ItemType, ItemId, ReporterUserId?, Reason, Details, Status

Limits enforced in handlers: ≤50 engineers per creator (any status, non-deleted), ≤10 teams/user, ≤10 members/team, ≤50 versions/item. Drafts reference assets in private blob `drafts/{userId}/{itemId}/...`; Publish freezes into `FrozenManifestJson`.

## Plugin build spec

- **Naming**: `e3a-{slug}` for engineers and `e3a-team-{slug}` for teams — the creator-typed slug is the plugin name; uniqueness enforced by the DB index, attribution via `author` (GitHub login).
- **Engineer zip**: `.claude-plugin/plugin.json` (author = @login + GitHub URL), `agents/{engineer}.md` (persona; default generated if omitted), `skills/{slug}/SKILL.md`, `commands/{engineer}.md`.
- **Team zip**: one plugin merging each member's `agents/`, `skills/` and `commands/` only. Skills are **always** namespaced `skills/{member-slug}--{skill-slug}/` (double-hyphen); colliding `agents/` and `commands/` file names are prefixed `{member-slug}--` on **every** colliding member, not just the later one. Hooks, `.mcp.json`, `.lsp.json` and the remaining roots are **deferred to the `team-compile-merge` slice** and are not carried today. Built from **snapshots** stored at engineer-publish time — never live drafts → immutable teams; the roster is frozen into the team version, so republish the team to adopt newer member versions.
- **Upload normalization** (upload-only since 2026-08-23): whole `.claude` folder → sanitize (strip settings.local.json/.env/memory) → map per the imported/converted/skipped table in docs/plugin-spec.md (CLAUDE.md+rules → generated house-rules skill + CLAUDE.md snippet; hooks imported with script-tier scan + warnings); skills keep `SKILL.md`-at-root validation, kebab-case slugs; path-traversal-safe extraction.
- **marketplace.json**: regenerated whole from DB each publish, atomic Blob write; entries use `source: {type: "archive", url: "https://<domain>/z/{name}/{semver}.zip", sha256}` with `version` bumped per release; only latest versions listed, old zips stay at immutable URLs.

## Security scan (publish pipeline)

Regex rule engine over all text files, categories: credential exfiltration (reads of ~/.ssh, .env, .aws + network sends; posts to webhook.site/pastebin/ngrok/raw IPs), encoded payloads (base64→shell, Invoke-Expression, long base64 walls), dangerous commands (rm -rf /, fork bombs, Defender disabling, curl|sh), instruction-injection markers, hygiene blocks (binaries, oversize, absolute paths). Block → version `Rejected` + per-file reasons shown to creator; Warn tier for ambiguous hits; corpus fixtures in tests. Report button = human backstop.

## API surface (`/api/*`)

Auth: `GET login`, `GET callback` (code→JWT), `GET me`. Catalog (anon): `GET /catalog?type&q&tag&sort&page&pageSize` (PageData), `GET /catalog/{slug}`, `GET /catalog/tags` (tags with counts). Engineers: `GET /api/engineers/{id}` is anonymous (published to anyone; drafts owner-only: 401 anonymous / 403 non-owner); the anonymous published list lives on `/catalog` — while `GET /api/engineers/mine`, `GET /api/engineers/slug-availability?slug=` and all mutations are [auth/owner]: CRUD + upload + `POST {id}/publish → 202` + `POST {id}/unlist` + `POST {id}/relist`. Teams: mirror + members with pinned versions. `GET /publish/{versionId}/status` (poll, owner-only). Social: `POST report` (anon OK). Worker: queue `publish-jobs`.

**Publish pipeline**: dequeue → ignore unless the version is Queued/Building → Building → freeze drafts into `snapshots/{versionId}` → assemble the tree from the snapshot + the frozen manifest → validate structure → *(security scan — next slice)* → deterministic zip + sha256 → upload `public/z/...` → pinned `public/m/{name}/{semanticVersion}/marketplace.json` → persist Published + LatestVersionId → regenerate the root marketplace.json. Poison queue after `maxDequeueCount` (5) total attempts, including the first.

## Build phases (each demoable, solo part-time ~3–6 weeks)

- **P0 Skeleton (2–3 eve)**: monorepo scaffold, Bicep (RG/Storage/SQL/Functions/SWA), 3 OIDC workflows. ✅ push-to-main deploys hello SPA + `/api/ping`.
- **P1 Marketplace proof (1–2 eve, BEFORE any product code)**: hand-built engineer zip + hand-written marketplace.json in Blob + Cloudflare Worker + domain. ✅ real Claude Code session: marketplace add → install → agent/skills load; version bump propagates.
- **P2 Auth + upload pipeline (~1 wk)**: OAuth+JWT, EF model/migrations, draft CRUD, `.claude`-folder upload with sanitize step + normalizer (mapping table in plugin-spec) + import-manifest UI. ✅ upload a real .claude folder; manifest shows imported/converted/skipped; local files stripped; anonymous rejected on gated endpoints.
- **P3 Publish pipeline (~1 wk)**: split into two slices — `publish-pipeline` (queue worker, structure validator, PluginBuilder, MarketplaceGenerator, unlist/relist, status poll) then `security-scan` (scanner engine, rule tiers, corpus fixtures, the `Rejected` path). The scanner MUST land before the first real publish. ✅ end-to-end publish → install in Claude Code; malicious fixture rejected with readable report.
- **P4 Public catalog (~4–5 d)**: anon browse/search/detail (contents tree, versions, attribution, copy-command buttons), votes, report modal. ✅ incognito → copy commands → install works.
- **P5 Teams (~4–5 d)**: split into two slices — `teams` (team CRUD, membership pinned to an exact `ItemVersion`, per-creator and per-team limits, publish through the existing worker, `e3a-team-{slug}` naming, teams listed in `marketplace.json`) then `team-compile-merge` (hook concatenation with per-member attribution, `.mcp.json`/`.lsp.json` merge-by-server-name, the "newer member versions" republish prompt). ✅ 3-engineer team installs, 3 agents resolve; member republish doesn't mutate published team.
- **P6 Hardening + launch (~4–5 d)**: limits, Cloudflare rate rules, seed content published through the real pipeline, empty/error states, README + demo GIF.

## Seed content (dogfooded through the product)

`dive-backend-engineer` (.NET DDD/CQRS — signature style), `azure-infra-engineer`, `react-frontend-engineer`, `sql-tuning-engineer`, `pr-review-engineer`, and flagship team **`dotnet-product-squad`** (backend+frontend+infra+reviewer).

## Verification / launch checklist

Fresh Claude Code session installs engineer + team from the live domain; malicious publish rejected readably; limits enforced; old-zip sha256 immutability spot-check; 6 seed items live; README with hero GIF + architecture diagram + ~$5/mo cost note; docs/plugin-spec.md + security-scan.md public; App Insights + poison-queue alert; announce (r/ClaudeAI, X/LinkedIn, Anthropic Discord).

## Critical files

- `api/src/E3a.Core/Infrastructure/Plugins/PluginBuilder.cs` — engineer/team zip composition (product core)
- `api/src/E3a.Core/Infrastructure/Plugins/MarketplaceGenerator.cs` — marketplace.json Claude Code consumes
- `api/src/E3a.Functions/Workers/PublishWorker.cs` — pipeline orchestration
- `api/src/E3a.Core/Infrastructure/Data/E3aDbContext.cs` — index data model
- `infra/main.bicep` — all Azure resources
