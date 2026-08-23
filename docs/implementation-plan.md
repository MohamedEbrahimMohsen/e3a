# e3a — Engineer as an Agent: v0.1 Implementation Plan

## Context

e3a is a free community product + portfolio piece for a solo senior .NET/Azure engineer. Developers compose **AI engineers** (skills + persona → one Claude Code plugin) and **teams** (bundles of pinned engineer versions → one plugin) on a website, publish them publicly with versioning, and anyone installs them into Claude Code natively — no login needed to browse/install. Origin: the builder once had to rebuild a colleague's AI environment from scratch because no tool could share it; e3a makes working setups shareable and teams reproducible.

**Verified Claude Code facts the design relies on** (official docs): `/plugin marketplace add <https-URL-to-marketplace.json>` is natively supported; plugin `source` type `archive` (HTTPS zip + sha256) works with URL-added marketplaces (relative paths do NOT); per-entry `version` field drives update propagation.

**Locked scope (v0.1)**: engineer composer (skills from catalog / GitHub links / upload), team composer (pinned snapshots), publish + version-on-Publish-only (limits: 50 engineers, 10 teams per creator, 50 versions per item), anonymous public catalog with likes (login to vote) + report button + copy-install commands. **Out**: workflow builder (v0.2), private items, export-to-my-GitHub, MCP server, eval scoring.

**Locked stack**: React 18 + TS + Vite on Azure Static Web Apps (free) · .NET 10 isolated Azure Functions (consumption) · Azure Blob + Storage Queue · Azure SQL Basic (~$5/mo) + EF Core · GitHub OAuth (creators only) · Cloudflare CDN/cache/rate-limit. Total ≈ $5–7/mo.

## Key architecture decisions

1. **Free SWA + standalone Function App** (not SWA Standard, not managed API): SPA on free SWA; separate .NET 10 Function App at `api.<domain>` with CORS, its own GitHub OAuth code exchange, and self-issued JWTs (HS256). Managed API is disqualified because the publish pipeline needs a **queue trigger**.
2. **Backend = vertical slices without MediatR**: 3 projects — `E3a.Functions` (thin HTTP/queue triggers), `E3a.Core` (`Features/<Area>/<UseCase>/{Command,Handler,Validator,Response}`, `Domain/`, `Infrastructure/`), `E3a.Core.Tests` (xUnit; heaviest on PluginBuilder, SecurityScanner, MarketplaceGenerator). FluentValidation; EF Core directly (no repo layer).
3. **Serving**: `marketplace.json` + zips live in Blob; a free-tier **Cloudflare Worker** proxies `<domain>/marketplace.json` and `<domain>/z/*` to Blob. `/z/*` immutable 1-year cache (versioned URLs); marketplace.json cached + purged via Cloudflare API on every publish.
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
- `engineers` / `teams` (separate tables, same shape): Id, OwnerUserId, Slug (unique `{githublogin}-{name}`), DisplayName, Description, Tags(json), Status(Draft|Published|Removed), DraftManifestJson, LatestVersionId, LikeCount, DislikeCount
- `team_members`: (TeamId, EngineerId) PK, PinnedVersionId, SortOrder
- `versions`: Id, ItemType, ItemId, VersionNumber, SemVer, FrozenManifestJson, ZipBlobPath, ZipSha256, SizeBytes, Status(Queued|Building|Published|Rejected|Failed), ScanReportJson; unique (ItemType, ItemId, VersionNumber)
- `likes`: (UserId, ItemType, ItemId) PK, Value ±1
- `reports`: Id, ItemType, ItemId, ReporterUserId?, Reason, Details, Status

Limits enforced in handlers: ≤50 published engineers, ≤10 teams/user, ≤50 versions/item. Drafts reference assets in private blob `drafts/{userId}/{itemId}/...`; Publish freezes into `FrozenManifestJson`.

## Plugin build spec

- **Naming**: `e3a-{githublogin}-{engineer-slug}` — unique + attributable.
- **Engineer zip**: `.claude-plugin/plugin.json` (author = @login + GitHub URL), `agents/{engineer}.md` (persona; default generated if omitted), `skills/{slug}/SKILL.md`, `commands/{engineer}.md`.
- **Team zip**: one plugin; each member materialized as `agents/{member-slug}.md` + `skills/{member-slug}--{skill-slug}/` (double-hyphen namespacing). Built from **snapshots** stored at engineer-publish time — never live drafts → immutable teams; republish team to adopt newer member versions.
- **Skill normalization** (upload / GitHub tarball fetch / catalog reference all converge): `SKILL.md` at root with validated frontmatter; kebab-case slugs; caps 5 MB/skill, 40 files, text+images only; path-traversal-safe extraction.
- **marketplace.json**: regenerated whole from DB each publish, atomic Blob write; entries use `source: {type: "archive", url: "https://<domain>/z/{name}/{semver}.zip", sha256}` with `version` bumped per release; only latest versions listed, old zips stay at immutable URLs.

## Security scan (publish pipeline)

Regex rule engine over all text files, categories: credential exfiltration (reads of ~/.ssh, .env, .aws + network sends; posts to webhook.site/pastebin/ngrok/raw IPs), encoded payloads (base64→shell, Invoke-Expression, long base64 walls), dangerous commands (rm -rf /, fork bombs, Defender disabling, curl|sh), instruction-injection markers, hygiene blocks (binaries, oversize, absolute paths). Block → version `Rejected` + per-file reasons shown to creator; Warn tier for ambiguous hits; corpus fixtures in tests. Report button = human backstop.

## API surface (`/api/*`)

Auth: `GET login`, `GET callback` (code→JWT), `GET me`. Catalog (anon): `GET /catalog?type&q&tag&sort&page`, `GET /catalog/{slug}`. Engineers [auth/owner]: CRUD + `skills/upload|from-github|from-catalog` + `POST {id}/publish → 202`. Teams: mirror + members with pinned versions. `GET /publish/{versionId}/status` (poll). Social: `PUT vote` [auth], `POST report` (anon OK). Worker: queue trigger `publish-jobs`.

**Publish pipeline**: dequeue → Building → assemble tree (draft assets or member snapshots) → validate structure → security scan (fail = Rejected + report) → deterministic zip + sha256 → upload `public/z/...` + snapshot assets → Published + LatestVersionId → regenerate marketplace.json → purge Cloudflare. Poison queue after 3 retries → Failed.

## Build phases (each demoable, solo part-time ~3–6 weeks)

- **P0 Skeleton (2–3 eve)**: monorepo scaffold, Bicep (RG/Storage/SQL/Functions/SWA), 3 OIDC workflows. ✅ push-to-main deploys hello SPA + `/api/ping`.
- **P1 Marketplace proof (1–2 eve, BEFORE any product code)**: hand-built engineer zip + hand-written marketplace.json in Blob + Cloudflare Worker + domain. ✅ real Claude Code session: marketplace add → install → agent/skills load; version bump propagates.
- **P2 Auth + engineer composer (~1 wk)**: OAuth+JWT, EF model/migrations, draft CRUD, 3 skill-ingestion paths, composer UI with structure preview. ✅ draft with uploaded + GitHub-fetched skill; anonymous rejected on gated endpoints.
- **P3 Publish pipeline (~1 wk)**: queue worker, validator, scanner+fixtures, PluginBuilder, MarketplaceGenerator, purge, status-poll UI. ✅ end-to-end publish → install in Claude Code; malicious fixture rejected with readable report.
- **P4 Public catalog (~4–5 d)**: anon browse/search/detail (contents tree, versions, attribution, copy-command buttons), votes, report modal. ✅ incognito → copy commands → install works.
- **P5 Teams (~4–5 d)**: team CRUD, member picker w/ pinned versions, namespaced team compile from snapshots, "newer member versions" republish flow. ✅ 3-engineer team installs, 3 agents resolve; member republish doesn't mutate published team.
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
