TRIAGE: 7 to implement, 10 rejected, 0 dev-decisions

# CodeRabbit Triage - Public Catalog (PR #2)

Deciding reviewer: feature-reviewer (Stage 4). Every claim verified against the working tree before classification. No comment was labelled Critical by CodeRabbit, so no Critical downgrade requires dev veto; Major rejections are called out explicitly below.

Owners: RC2-RC6 target the API slice (internally APPROVED in 03-review.md); RC1 targets `.claude/` tooling; RC7/RC8 target `/docs`; RC9-RC15 target `web/` (orchestrator-owned frontend integration, not the reviewed slice). PC1 is the PR-level summary.

## IMPLEMENT

### 1. (RC4, Major) Add a deterministic final sort key in GetCatalogQueryHandler
**Where:** `api/E3A.Application/Catalog/GetCatalog/GetCatalogQueryHandler.cs:31-32`
**Verified:** `CatalogSort.Newest` orders by `CreationDate` only; the default branch by `InstallCount` then `CreationDate`. All `InstallCount` values are 0 today (no ingestion path), so ordering rests almost entirely on `CreationDate`; ties fall through to the repository enumeration order, and EF Core issues no ORDER BY - SQL Server row order is not guaranteed across requests. `Enumerable.OrderBy` is stable, but stable relative to an unstable source, so a tied engineer can move between pages (duplicate/omitted rows while paging).
**Why not re-litigation:** plan Decision 10 states its own rationale as "Tie-break makes ordering deterministic" - appending a final key completes that intent; it does not change the approved sort semantics.
**Fix:** append `.ThenBy(x => x.Id)` to BOTH switch branches. No new test required - `Engineer.Create` assigns `Guid.NewGuid()` internally, so the Id tie-break is not deterministically assertable through `EngineerFactory`; existing paging/sort tests (distinct dates/counts) stay green.

### 2. (Reviewer-found while verifying RC5/RC10) Web client sends pageNumber; the API binds page - pagination from the web is broken
**Where:** `web/src/lib/api.ts:78` vs `api/E3A.Api/Controllers/Catalog/CatalogController.cs:17`
**Verified:** `getCatalog` does `parameters.set('pageNumber', String(query.pageNumber))`, but the action binds `[FromQuery(Name = "page")] int pageNumber = 1` - ASP.NET Core will only bind from `page`, so `?pageNumber=2` is silently ignored and every page click returns page 1 content (`CatalogPage.tsx:38` passes `pageNumber: page`; the UI highlights page 2 while showing page 1 items). `q`, `tag`, `sort`, `pageSize` all match; only this one diverges.
**Fix:** `web/src/lib/api.ts:78` -> `parameters.set('page', String(query.pageNumber));`. One-token change; the API wire contract (`q`/`tag`/`sort`/`page`/`pageSize`) is gate-approved (plan Decision 16) and stays as-is.

### 3. (RC10, Major) Retry on CatalogPage can hang on "Loading..." forever
**Where:** `web/src/features/catalog/CatalogPage.tsx:95`
**Verified:** effect deps are `[segment, query, searchParams, sort, page]`. Retry runs `setLoadFailed(false); setPage(1); setQuery(query => query);` - when the failure happened on page 1 with an unchanged query, no dependency changes, the effect never re-runs, `data` stays null, and with `loadFailed` cleared the render falls through to the "Loading..." branch permanently.
**Fix:** add a `reloadToken` state (number), include it in the fetch effect dependency array, and increment it in the Retry handler alongside clearing `loadFailed`.

### 4. (RC9, Major) CatalogPage interactive controls are not keyboard-accessible
**Where:** `web/src/features/catalog/CatalogPage.tsx:71` (segment tabs), `:78` (tag chips), `:82` (sort toggle), `:104-108` (pagination)
**Verified:** all are `span`/`div` with `onClick` only - not focusable, not activatable by keyboard.
**Fix:** replace with `<button type="button">` preserving styles/handlers; add `aria-pressed={active}` on tag chips (and segment tabs, same pattern, same fix).

### 5. (RC12, Major) EngineerDetailPage report and hook-warning controls are not keyboard-accessible
**Where:** `web/src/features/detail/EngineerDetailPage.tsx:64` (Report span), `:68` (hook-warnings toggle div)
**Verified:** pointer-only; a keyboard user cannot report an engineer or inspect hook warnings - the hook-warning panel is a security-relevant disclosure, so this one matters more than cosmetics.
**Fix:** `button type="button"` for both; expose the panel state with `aria-expanded={hooksOpen}` on the toggle.

### 6. (RC13, Minor) HomePage renders fabricated zero statistics on API failure
**Where:** `web/src/features/home/HomePage.tsx:17-22`
**Verified:** both `.catch` handlers write empty-catalog values (`setEngineers([])`, `setTagCount(0)`); the page then asserts "0 engineers / 0 tags / 0 installs" as fact. `CatalogPage` and `EngineerDetailPage` both have explicit unreachable states; home is the only surface presenting failure as data.
**Fix:** add a `loadFailed` state set in the catch handlers; when set, render a "catalog stats unavailable" note in place of the stats row (featured grid may simply stay empty).

### 7. (RC1, Minor) .claude/launch.json hardcodes a machine-specific absolute path
**Where:** `.claude/launch.json:7` - the `--prefix D:\Personal\_e3a\web` runtime argument
**Verified:** the absolute path breaks the launch profile on any other clone location or machine. Owner: orchestrator tooling, not the slice.
**Fix:** drop the `--prefix` pair from `runtimeArgs` and add `"cwd": "web"` to the configuration, keeping `run dev -- --port 5174 --strictPort`.

## REJECT

### RC2 (Major) - "Verify deployed CatalogOptions before merge / add tracked provisioning"
Rejected as a code change. `docs/constitution.md` par.2 (dated dev decision 2026-08-27) makes config deploy-time-only: `appsettings.json` is deliberately git-ignored, no configuration file is committed, and "CI and fresh clones have NO defaults (options bind to 0/empty)" is a stated, accepted consequence - with the mandated mitigation being "new options sections must be announced to the dev", which `02-implementation.md` (Deviations) already does explicitly for the `Catalog` section, and `03-review.md` verified. Tracked provisioning files would violate the constitution. The residual "confirm the five keys exist in Azure App Configuration" is an ops action for the dev, already surfaced; not an implementer work item.

### RC6 (Major) - "Provide safe defaults and startup validation on CatalogOptions"
Rejected. Same constitution par.2 policy as RC2: options binding to 0 without committed defaults is the repo's chosen model, and `CatalogOptions` (`api/E3A.Application/Options/CatalogOptions.cs:7-11`) exactly mirrors `EngineersOptions`/`UploadsOptions`/`AzureOptions` - none of which have defaults or `ValidateOnStart`. Mirror-don't-modernize (constitution par.0.2): compiled defaults or validation on one options class would make it the odd one out. A repo-wide options-validation pass is already recorded as a non-blocking follow-up in `03-review.md`; doing it piecemeal here is out of slice scope. Explicitly noting this Major was not implemented, for dev visibility.

### RC5 (Major) - "PageNumber overflow makes Skip throw ArgumentOutOfRangeException"
Rejected - the central claim is factually wrong. `matched.Skip(...)` at `GetCatalogQueryHandler.cs:39` is LINQ-to-objects (`matched` is a `List<Engineer>`), and `Enumerable.Skip` is documented to yield all elements when count <= 0 - it never throws for a negative count; the multiplication wraps silently (unchecked context), it does not raise `OverflowException` either. Actual behavior for the adversarial input `page=1073741825&pageSize=2`: negative wrapped offset -> `Skip` no-ops -> first-page items returned labelled with the absurd page number. No crash, no 500, no data exposure - an anonymous prober gets mislabeled page-1 data instead of an empty page. Plan Decision 12 (gate-approved) scopes validation to "only what protects the server"; nothing here harms the server. Not worth a `PageNumber` upper bound or long-math offset.

### RC3 (Minor) - "Reserve the tags slug or change the detail route"
Rejected - direct re-litigation of gate-approved plan Decision 15, which names this exact edge case ("an engineer whose slug is exactly `tags` is unreachable by detail URL. Accepted for v0.1") and accepts it. CodeRabbit's routing analysis is correct and the plan already agrees with it; the product call was made at the gate.

### RC7 (Minor) - split the architecture Principles bullet into plugin-reads vs website-reads
Rejected. The line (`docs/architecture.md:23`) is the exact replacement text from gate-approved plan Decision 13(b), and the sentence itself already draws the distinction CodeRabbit wants ("marketplace.json and plugin zips are served from Blob via Cloudflare cache; the API handles ... the website's catalog browse - ... irrelevant for plugin consumers"). No code/doc divergence exists - the doc agrees with the code; the bolded lead-in is stylistic shorthand. Rewording gate-approved doc text for polish is not this stage's mandate.

### RC8 (Major) - "/catalog should be /api/catalog; type parameter is unimplemented"
Rejected on both halves. (a) The sentence lives under the heading "API surface (`/api/*`)" (`docs/implementation-plan.md:54`), so `/catalog` reads as `/api/catalog` - the same shorthand the section applies to `GET login`/`GET callback`; no divergence. (b) `type` is explicitly target-state: the plan's Deferred table and Decision 13 record "`type` staying in the doc's catalog line is target-state (teams, slice 4) - untouched", and `.claude/rules/docs-sync.md` classifies docs describing planned-but-unbuilt work as incompleteness, never a finding. Explicitly noting this Major was not implemented.

### RC11 (Minor) - "Do not fabricate a pinned version (?? 'v1.0.0')"
Rejected. `TeamComposerPage` is a mock-data prototype for slice 4: `memberSearchPool` (`web/src/lib/catalog.ts:109`) is built from static fixtures whose factory takes `version: string` as a required parameter - every candidate has a real version, so the `?? 'v1.0.0'` fallback at `TeamComposerPage.tsx:75` is dead code today, and the adjacent version picker is an acknowledged stub ("Version picker is stubbed in this prototype", line 72). Nothing composer-related touches the API. The concern becomes real when teams go API-backed in slice 4 - address it there.

### RC14 (Major) - "Do not present a page subtotal as total installs"
Rejected for v0.1. The premise (later engineers' install counts excluded) is currently unobservable: `InstallCount` has no write path - the plan (Deferred: "Install-count write endpoint / worker") ships `RecordInstallCount` with no caller, so every engineer's count is 0 and the sum is 0 regardless of fetch size. `STATS_FETCH_SIZE = 50` (`HomePage.tsx:10`) equals `MaxPageSize` and the seed catalog holds 6 engineers. A catalog-wide aggregate needs an API stats surface that is outside the gate-approved scope. Revisit in the install-ingestion slice, when the number can first be non-zero. Explicitly noting this Major was not implemented.

### RC15 (Major) - "Expose and use the actual package install identity"
Rejected - this asks for data the backend cannot provide yet, colliding with a gate-approved decision on the do-not-relitigate list: OwnerUserId-only attribution ("Verified `User.cs` is the untouched template `IdentityUser<Guid>` - no `GitHubLogin` ... the OAuth slice replaces it with real attribution", plan Deferred). `installCommand('creator', ...)` (`EngineerDetailPage.tsx:66`) and `installCommand('mohamed', ...)` (`HomePage.tsx:39`) are placeholders for a command that cannot work anyway until publishing (slice 3) produces installable zips and marketplace.json entries. The OAuth slice must sweep these literals when the author field becomes real. Explicitly noting this Major was not implemented.

### PC1 - Docstring Coverage pre-merge check (0% vs 80%)
Rejected. The repo's no-comments rule is an absolute: skill par.1 "No Comments - zero comments unless the WHY is a hidden invariant" and constitution par.1.4; docstrings on 80 functions would be a mass style violation. The CodeRabbit threshold does not apply to this repo. The rest of PC1 is a summary; its "merge risk" items are the RCs triaged above.

## Notes for the implementer

- Item 1 is API-slice code: keep the skill's absolutes (one operator per line in the LINQ chain, no comments, build + full test suite green afterward).
- Items 2-6 are `web/` (React 18 + TS strict, constitution par.5): preserve existing inline-style patterns; buttons should carry the same classNames/styles as the spans they replace.
- Item 7 is `.claude/launch.json` only - do not touch other tooling files.
- Re-run `dotnet build api/E3A.slnx` + `dotnet test` after item 1, and `npm run build` under `web/` after items 2-6.
