# Plan — Anonymous Public Catalog

## Goal
Anyone — no login — can browse the public catalog of PUBLISHED engineers over HTTP: search free text across display name, description and tags; filter by one or more tags; sort by newest or most installed; page through results as a `PageData<T>`; open one engineer by slug for a detail view (metadata, install count, owner attribution placeholder, hook warnings derived from the stored import manifest); and fetch the real tag list with per-tag counts to drive the filter chips. Today the only anonymous list is the unfiltered, unpaginated `GET /api/engineers` (slice ①) — this slice replaces it with a proper catalog area and removes the duplicate. Additionally, the repo gains a standing Postman collection covering every existing endpoint plus the new catalog ones.

## Scope
**In:** three query use cases in a new `E3A.Application/Catalog/` area (`GetCatalog`, `GetCatalogEngineer`, `GetCatalogTags`); `Catalog/Shared` results + generator + `CatalogSort` enum; `CatalogOptions` bound from configuration; one new domain method `Engineer.RecordInstallCount`; new `CatalogController` (all anonymous); **removal of the superseded `ListEngineers` use case, controller action and its tests**; 7 new error codes + ar/en resource strings; `Catalog` appsettings section; `postman/e3a.postman_collection.json` + `postman/e3a.local.postman_environment.json`; two small `/docs` sync edits; full unit tests per `conventions/dotnet-testing.md`.

**Out:** publishing/versions (slice ③) — no version rows, no semver labels, no zip/sha data; teams and the catalog `type` segment (④); install-count ingestion (the Cloudflare-worker write path — the column and the new domain mutator exist, but no endpoint writes it); reports/votes; GitHub OAuth or any `User` change; frontend code; seeding; caching; any change to `Program.cs`, middleware order, `E3A.Infrastructure/**`, migrations, any `.csproj`, `Directory.Packages.props`, or `core-libraries/**`.

**Deferred:**

| Item | Why |
|------|-----|
| SQL-side search/tag filtering + `FindPaginatedAsync` | `Engineer.Tags` is a value-converted JSON `nvarchar(400)` column (verified in `AppDbContext.ConfigureEngineers`); EF Core cannot translate `Contains`/`Any` over a value-converted collection, so the required q-over-tags and tag-filter predicates would throw at runtime inside `FindPaginatedAsync`'s expression filter. See Decision 2. Revisit when the catalog outgrows in-memory filtering (needs remapping `Tags` as a native primitive collection + a migration — a schema change this read-only slice must not make). |
| Versions list on the detail page | Versions do not exist (③). Inventing an empty `CatalogVersionResult` now means guessing ③'s shape (semver, size, sha). The detail result carries `LatestVersionId` (always null today); ③ adds the versions collection. |
| Version label on catalog cards | Same reason — nothing version-shaped exists to label. |
| Owner GitHub login / display name attribution | Verified `E3A.Domain/Identity/User.cs` is the untouched template `IdentityUser<Guid>` — no `GitHubLogin`, no `DisplayName`, no `AvatarUrl`. Detail returns `OwnerUserId` only; the OAuth slice replaces it with real attribution. |
| Hook warnings from the frozen (published) manifest | `FrozenManifestJson` arrives in ③. Until then detail derives `HookWarnings` from `DraftManifestJson` (Decision 8) — ③ must switch the source. |
| Catalog `type` query parameter (`?type=engineers\|teams`) | Teams are ④; a one-valued discriminator today is dead surface. The doc keeps `type` as target — incompleteness, not divergence. |
| Install-count write endpoint / worker | Out of scope by the brief; `RecordInstallCount` exists so the state is domain-reachable (tests + future ingestion). |
| Caching / Cloudflare rules for catalog reads | Hardening slice (P6). |

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Fate of slice ①'s anonymous `GET /api/engineers` (`ListEngineers`). | **Remove it — the catalog supersedes it.** Delete the use-case folder, the controller action + its `using`, and `ListEngineersQueryHandlerTests`. `GET /api/engineers/{id}` (anonymous, published-to-anyone) and `GET /api/engineers/mine` stay untouched. | Two anonymous published lists answering the same question with different shapes is a divergence factory. Nothing consumes it: the web app is still mock-data-driven (verified — no `fetch`/API call anywhere under `web/src`). `docs/implementation-plan.md` API surface is updated in the same change per `.claude/rules/docs-sync.md` (Decision 13). |
| 2 | `FindPaginatedAsync` (SQL paging) or in-memory filter + hand-built `PageData`? | **Fetch all published rows via `FindAsync(x => x.Status == EngineerStatus.Published, …, asNoTracking: true)`, then filter/sort/page in memory and construct `PageData<CatalogEngineerResult>` manually.** No new repository members. | Verified `IRepository<T>.FindPaginatedAsync(int pageNumber, int pageSize, ct, Expression<Func<T,bool>>? filter, include, orderBy, asNoTracking)` and `PageData<T> { Items, PageNumber, PageSize, TotalItems, TotalPages }` (all `long` except `Items`). The filter expression runs through EF — and `Tags` is value-converted JSON, so `x.Tags.Contains(tag)` / `x.Tags.Any(...)` cannot translate (runtime `InvalidOperationException`), which kills both q-over-tags and the tag filter. Splitting into a SQL path (no tags) + memory path (tags) doubles the branches for no v0.1 gain: the catalog is a small community set (50-per-creator cap, 6 seed items at launch). The `PageData` response contract is preserved, so moving filtering into SQL later is non-breaking. Bonus: in-memory logic is fully assertable through a substituted repository, unlike an `orderBy` delegate that never executes in tests. |
| 3 | One slice, three endpoints? | Yes — one vertical slice. | They share the result records, generator, options, error-code group, controller and test factory. The genuinely separable work (versions, teams, ingestion) is Deferred. |
| 4 | Tags endpoint — distinct list or tags-with-counts? | **`GET /api/catalog/tags` returning `List<CatalogTagResult(string Tag, int Count)>`.** | Counts are free once the published set is in memory (same `FindAsync` the list uses), and counts let the UI rank filter chips instead of hardcoding `filterTagNames`. |
| 5 | Tag aggregation semantics. | Group case-insensitively on `tag.ToLowerInvariant()`; **count = number of published engineers carrying the tag** (dedupe within one engineer via `Distinct()` after lowercasing); order `Count` desc, then `Tag` ascending ordinal. Returned tag text is the lower-invariant form. | Slugs and UI chips are lowercase; occurrences-per-engineer would double-count an engineer that repeats a tag with different casing. Deterministic ordering per conventions §8. |
| 6 | Tag filter matching semantics on the list. | **ANY-of (OR), case-insensitive, exact tag match**: `engineer.Tags.Any(tag => request.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))`. | Mirrors the frontend prototype exactly (`item.tags.some(tag => activeTags.includes(tag))` in `CatalogPage.tsx`). |
| 7 | Search semantics. | `SearchText` trimmed once in the handler; a row matches when `DisplayName`, `Description` (null-safe) **or any tag** `Contains` it with `StringComparison.OrdinalIgnoreCase`. Blank/whitespace search = no filter. | The brief fixes the fields (displayName/description/tags). Substring + case-insensitive mirrors the prototype's behaviour. Author search is impossible until OAuth (no login data). |
| 8 | Hook warnings on detail. | Derive from `Engineer.DraftManifestJson`: deserialize `ImportManifestResult` (the shape slice ② persists) and expose its `HookWarnings`; `null` manifest → `[]`. Done in `CatalogEngineerResultGenerator.GenerateDetail`, no try/catch, `Deserialize<…>(json)!` — mirrors `GetImportManifestQueryHandler` verbatim. **Reuse the existing `HookWarningResult` record from `E3A.Application.Engineers.Shared` — do not duplicate it.** | The brief asks for it "if cheaply derivable" — it is one deserialize. Caveat accepted and recorded: until ③, the draft manifest *is* the published content's manifest; after ③ the source switches to `FrozenManifestJson` (Deferred). Same-assembly record reuse beats a copy. |
| 9 | 404 semantics on detail. | `GET /api/catalog/{slug}` matches **published only**: `FirstOrDefaultAsync(x => x.Slug == request.Slug && x.Status == EngineerStatus.Published, …)`; miss → `NotFoundCoreException(ErrorCodes.EngineerNotFound)` (reused code). A draft's slug 404s — the catalog never confirms a draft exists. | The brief mandates "published only, 404 otherwise". Reusing `ENGINEER_NOT_FOUND` (already in both resx files) over minting a near-identical code. |
| 10 | Sorting. | `CatalogSort { MostInstalled, Newest }`, default **`MostInstalled`** (the prototype's default). `Newest` → `OrderByDescending(CreationDate)`; `MostInstalled` → `OrderByDescending(InstallCount).ThenByDescending(CreationDate)`. Applied via a switch expression. | Tie-break makes ordering deterministic (all install counts are 0 until ingestion exists). Query-string binding accepts the enum name (`?sort=Newest`); an unknown name is the framework's 400 (same accepted behaviour as slice ① Decision 23); a numeric out-of-range value is caught by the validator's `IsInEnum` → 422. |
| 11 | Page-size defaults and caps — where? | **`CatalogOptions`** (`SectionName = "Catalog"`): `DefaultPageSize` (9 — the UI's 3×3 grid), `MaxPageSize` (50), `SearchTextMaxLength` (100), `MaxTagFilters` (10), `TagFilterMaxLength` (30). Query record carries `int? PageSize = null`; the handler resolves `request.PageSize ?? options.DefaultPageSize`. `PageNumber = 1` default stays in the record (1-based paging is an invariant, not a tunable). | Dev-review rule: caps/tunables live in `[Area]Options`, never inline (skill §8.1, memory). `int?` avoids baking the default into the record while keeping the record's shape honest. `TagFilterMaxLength` duplicates `EngineersOptions.TagMaxLength`'s value deliberately — coupling the catalog validator to the Engineers options section for a request-shape cap would tangle two areas' configuration. |
| 12 | Validator coverage on a read-only anonymous endpoint. | Validate only what protects the server: search length, tag-filter count + length, sort `IsInEnum`, `PageNumber` positive, `PageSize` in `[1, MaxPageSize]` when supplied. No per-tag "required" rule — an empty tag string just matches nothing. `PageSize` uses a raw `.Must(pageSize => pageSize == null \|\| (pageSize.Value >= 1 && pageSize.Value <= options.MaxPageSize))` because the vendored numeric extensions constrain `TNumber : INumber<TNumber>`, which `int?` does not satisfy (verified `NumericValidationExtensions.cs`). | Minimal rules, each with a real failure mode; raw `.Must` + `WithErrorCode` is the established fallback when Core.Validation has no fitting extension (slice ① Decision 26 precedent). |
| 13 | Docs sync. | Two edits, same change: (a) `docs/implementation-plan.md` API-surface bullet — drop the now-removed anonymous `GET /api/engineers` list, add `GET /catalog/tags` and the `pageSize` parameter (exact replacement text in **Existing code touched**); (b) `docs/architecture.md` Principles — the clause "the API only handles auth, drafts, and publishing" now also names catalog browse. | Both are divergence, not incompleteness: after this slice the code and the docs would answer "where does the anonymous list live / what does the API serve" differently. `type` staying in the doc's catalog line is target-state (teams, ④) — untouched. |
| 14 | Controller authorization shape. | `CatalogController` carries `[AllowAnonymous]` at class level, no `[Authorize]`, no policy. No `DefaultCodes` — verified the class does not exist anywhere in `api/` and `AddCoreIdentity` registers zero named policies (slice ① Decision 12 stands). Handlers take no `ICurrentUserService`. | Every action is public; per-action `[AllowAnonymous]` noise serves nothing. Skill §7.3 explicitly blesses `[AllowAnonymous]` for public catalog reads. |
| 15 | Route collision `catalog/tags` vs `catalog/{slug}`. | Both exist; ASP.NET Core attribute routing ranks literal segments above parameters, so `/api/catalog/tags` always hits the tags action. Consequence: an engineer whose slug is exactly `tags` is unreachable by detail URL. Accepted for v0.1. | Framework-guaranteed precedence; the edge case is cosmetic and self-inflicted by a creator naming an engineer "tags". |
| 16 | Query-string parameter names. | `q`, `tag` (repeatable), `sort`, `page`, `pageSize` — bound via `[FromQuery(Name = "q")] string? searchText`, `[FromQuery(Name = "tag")] List<string>? tags`, `[FromQuery] CatalogSort sort = CatalogSort.MostInstalled`, `[FromQuery(Name = "page")] int pageNumber = 1`, `[FromQuery] int? pageSize = null`. | The public URL contract follows `docs/implementation-plan.md` (`?q&tag&sort&page`); the C# identifiers stay unabbreviated (memory: no abbreviated names — `q` is a wire name, never a variable). `tags ?? []` at the controller mirrors `CreateEngineer`'s `request.Tags ?? []` null-at-the-boundary pattern. |
| 17 | `MostInstalled` is untestable — `InstallCount` has no mutator. | **Add `Engineer.RecordInstallCount(int installCount)`** (sets the value, stamps `UpdationDate`, no guard). | Conventions §4: reflection is prohibited and an unreachable state is a domain design finding — fix the domain. The method is the exact mutator the install-count ingestion path will need. No guard: mirrors the entity's other guard-free mutators (slice ① Decision 3 — no domain-side error registry exists), and the only caller today is trusted test/ingestion code. |
| 18 | Result shapes — one or two? | Two: `CatalogEngineerResult` (list card) and `CatalogEngineerDetailResult` (detail). Neither exposes `Status` (always `Published` by construction) nor `DraftManifestJson`. Both carry `Guid? LatestVersionId` (null until ③) as the only version-ish datum that exists. Detail adds `OwnerUserId` + `HookWarnings`. No `LocalizedText` anywhere → no `.Localized()`, no admin variant (e3a is EN-only, slice ① Decision 11). | Card fields track what `CatalogPage.tsx` renders; detail adds exactly the brief's extras. Reusing `EngineerResult` would leak `Status` and invite the owner-facing and public shapes to drift in lockstep. |
| 19 | Where does the filtering logic live? | Private static helpers on `GetCatalogQueryHandler` (`MatchesSearchText`, `MatchesAnyTag`). No new service, no extension class, no interface. | Pure two-liner predicates used by exactly one handler; skill's no-new-abstractions rule. |
| 20 | Slug case handling on detail. | Compare as-is, no normalization. | Slugs are generated lower-case; SQL's CI collation forgives case in production, and tests use exact-case fixtures. Adding `.ToLowerInvariant()` would be untranslatable-safe but is unnecessary chrome with no precedent in the codebase. |
| 21 | Postman scope + auth model. | Collection v2.1 with collection-level `bearer {{token}}` auth; the three catalog requests override with `noauth`. Two folders: **Engineers** (7 requests — the removed `GET /api/engineers` is deliberately absent) and **Catalog** (3 requests). Environment `e3a.local` defines `baseUrl = https://localhost:62935` (verified from `launchSettings.json`), `token` (empty, type `secret`), `engineerId` (empty). Files live at repo root `postman/` as mandated — they are tooling, not docs, so the `/docs`-only convention is not violated. | One switchable token; anonymous endpoints demonstrably work logged-out. Port from the repo's own launch profile, not invented. |
| 22 | Tests for the Postman files. | None — they are data files. Reviewer checks them by shape (valid JSON, v2.1 schema URL, all 10 requests present). | Nothing executable to unit-test. |

## Existing code touched

| File | Change |
|------|--------|
| `api/E3A.Domain/Engineers/Engineer.cs` | Add `RecordInstallCount` (exact body in **Domain behaviour**) after `MarkPublished`, before `ReplaceDraftManifest`. Nothing else changes. |
| `api/E3A.Application/Engineers/ListEngineers/` | **Delete the folder** (`ListEngineersQuery.cs`, `ListEngineersQueryHandler.cs`). |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | Remove the `ListEngineers` action (lines with `[HttpGet]` + `[AllowAnonymous]` returning `ListEngineersQuery`) and the `using E3A.Application.Engineers.ListEngineers;` line. All other actions byte-identical. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Append a new `// Catalog` comment-separated group with the 7 constants from **Error codes**. Existing groups untouched. |
| `api/E3A.Application/DependencyInjection.cs` | Add, after the `AzureOptions` line: `services.Configure<CatalogOptions>(configuration.GetSection(CatalogOptions.SectionName));`. No using changes (`E3A.Application.Options` already imported). |
| `api/E3A.Api/appsettings.json` | Add a top-level `"Catalog"` section (**Configuration**). |
| `api/E3A.Api/Resources/Messages.en.resx` / `Messages.ar.resx` | Append the 7 keys from **Error codes**, existing `<data name="…" xml:space="preserve"><value>…</value></data>` element shape, before `</root>`. |
| `api/E3A.Tests/Engineers/ListEngineers/` | **Delete the folder** (`ListEngineersQueryHandlerTests.cs`). |
| `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` | Append the `Published(...)` factory method (exact body in **Test plan**). `Draft` and `CreateEngineersOptions` untouched. |
| `api/E3A.Tests/Engineers/EngineerTests.cs` | Append the two `RecordInstallCount` tests. |
| `docs/implementation-plan.md` | In the **API surface** section, replace the two sentences `Catalog (anon): \`GET /catalog?type&q&tag&sort&page\`, \`GET /catalog/{slug}\`.` and `Engineers: **GET reads are anonymous** — \`GET /api/engineers\` (published only) and \`GET /api/engineers/{id}\` (published to anyone; drafts owner-only: 401 anonymous / 403 non-owner) — while \`GET /api/engineers/mine\` and all mutations are [auth/owner]: CRUD + upload + \`POST {id}/publish → 202\`.` with: `Catalog (anon): \`GET /catalog?type&q&tag&sort&page&pageSize\` (PageData), \`GET /catalog/{slug}\`, \`GET /catalog/tags\` (tags with counts).` and `Engineers: \`GET /api/engineers/{id}\` is anonymous (published to anyone; drafts owner-only: 401 anonymous / 403 non-owner); the anonymous published list lives on \`/catalog\` — while \`GET /api/engineers/mine\` and all mutations are [auth/owner]: CRUD + upload + \`POST {id}/publish → 202\`.` Nothing else in the file changes. |
| `docs/architecture.md` | In **Principles**, replace `the API only handles auth, drafts, and publishing — so scale-to-zero cold starts are irrelevant for consumers.` with `the API handles auth, drafts, publishing, and the website's catalog browse — so scale-to-zero cold starts are irrelevant for plugin consumers.` Nothing else changes. |

Untouched: `Program.cs`, `E3A.Infrastructure/**` (no repository change — verified `IEngineerRepository`'s base methods cover every query here), all migrations, every `.csproj`, `core-libraries/**`, all other Engineers use cases and their tests.

## Files to create

All paths relative to `D:/Personal/_e3a/`. Every file: file-scoped namespace matching folder, one-line type declarations, braces on every `if`, `DateTimeOffset` only, `[]` collections, `.ConfigureAwait(false)` on every await outside controllers/test bodies, no comments.

### Application — Options

| # | Path | Contract |
|---|------|----------|
| 1 | `api/E3A.Application/Options/CatalogOptions.cs` | `public sealed class CatalogOptions` · `public const string SectionName = "Catalog";` · properties (all `{ get; set; }`): `int DefaultPageSize` · `int MaxPageSize` · `int SearchTextMaxLength` · `int MaxTagFilters` · `int TagFilterMaxLength`. Nothing else. |

### Application — Catalog/Shared

Namespace for all: `E3A.Application.Catalog.Shared;`

| # | Path | Contract |
|---|------|----------|
| 2 | `api/E3A.Application/Catalog/Shared/CatalogSort.cs` | `public enum CatalogSort { MostInstalled, Newest }` — no extensions class (nothing consumes one). |
| 3 | `api/E3A.Application/Catalog/Shared/CatalogEngineerResult.cs` | `public sealed record CatalogEngineerResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, int InstallCount, Guid? LatestVersionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);` All fields client-facing; no `LocalizedText`. |
| 4 | `api/E3A.Application/Catalog/Shared/CatalogEngineerDetailResult.cs` | `public sealed record CatalogEngineerDetailResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, int InstallCount, Guid OwnerUserId, Guid? LatestVersionId, List<HookWarningResult> HookWarnings, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);` — `using E3A.Application.Engineers.Shared;` for `HookWarningResult`. |
| 5 | `api/E3A.Application/Catalog/Shared/CatalogTagResult.cs` | `public sealed record CatalogTagResult(string Tag, int Count);` |
| 6 | `api/E3A.Application/Catalog/Shared/CatalogEngineerResultGenerator.cs` | `public static class CatalogEngineerResultGenerator` — usings `E3A.Application.Engineers.Shared`, `E3A.Domain.Engineers`, `System.Text.Json`. Two methods:<br>`public static CatalogEngineerResult Generate(Engineer engineer)` → `return new CatalogEngineerResult(engineer.Id, engineer.Slug, engineer.DisplayName, engineer.Description, engineer.Tags, engineer.InstallCount, engineer.LatestVersionId, engineer.CreationDate, engineer.UpdationDate);`<br>`public static CatalogEngineerDetailResult GenerateDetail(Engineer engineer)` → `var hookWarnings = engineer.DraftManifestJson == null ? [] : JsonSerializer.Deserialize<ImportManifestResult>(engineer.DraftManifestJson)!.HookWarnings;` then `return new CatalogEngineerDetailResult(engineer.Id, engineer.Slug, engineer.DisplayName, engineer.Description, engineer.Tags, engineer.InstallCount, engineer.OwnerUserId, engineer.LatestVersionId, hookWarnings, engineer.CreationDate, engineer.UpdationDate);` (declare `hookWarnings` as `List<HookWarningResult>` so `[]` binds). |

### Application — GetCatalog use case

Namespace: `E3A.Application.Catalog.GetCatalog;`

| # | Path | Contract |
|---|------|----------|
| 7 | `.../GetCatalog/GetCatalogQuery.cs` | `public sealed record GetCatalogQuery(string? SearchText, List<string> Tags, CatalogSort Sort = CatalogSort.MostInstalled, int PageNumber = 1, int? PageSize = null) : IRequest<PageData<CatalogEngineerResult>>;` — usings `Core.DDD.Models`, `E3A.Application.Catalog.Shared`, `MediatR`. |
| 8 | `.../GetCatalog/GetCatalogQueryValidator.cs` | `public sealed class GetCatalogQueryValidator : AbstractValidator<GetCatalogQuery>` — ctor `(IOptions<CatalogOptions> catalogOptions)`, read `.Value` once into `options`. Rules, in order:<br>`RuleFor(x => x.SearchText).ValidateMaxLength(options.SearchTextMaxLength, ErrorCodes.CatalogSearchTextTooLong);`<br>`RuleFor(x => x.Tags).ValidateListMaxItems(options.MaxTagFilters, ErrorCodes.CatalogTooManyTagFilters);`<br>`RuleForEach(x => x.Tags).ValidateMaxLength(options.TagFilterMaxLength, ErrorCodes.CatalogTagFilterTooLong);`<br>`RuleFor(x => x.Sort).IsInEnum().WithMessage("{PropertyName} must be a known sort option.").WithErrorCode(ErrorCodes.CatalogSortInvalid);`<br>`RuleFor(x => x.PageNumber).ValidatePositive(ErrorCodes.CatalogPageNumberInvalid);`<br>`RuleFor(x => x.PageSize).Must(pageSize => pageSize == null \|\| (pageSize.Value >= 1 && pageSize.Value <= options.MaxPageSize)).WithMessage($"{{PropertyName}} must be between 1 and {options.MaxPageSize}.").WithErrorCode(ErrorCodes.CatalogPageSizeInvalid);` |
| 9 | `.../GetCatalog/GetCatalogQueryHandler.cs` | `public sealed class GetCatalogQueryHandler(IEngineerRepository engineerRepository, IOptions<CatalogOptions> catalogOptions) : IRequestHandler<GetCatalogQuery, PageData<CatalogEngineerResult>>` — steps in **Handlers**, plus the two private static predicate helpers. |

### Application — GetCatalogEngineer use case

Namespace: `E3A.Application.Catalog.GetCatalogEngineer;`

| # | Path | Contract |
|---|------|----------|
| 10 | `.../GetCatalogEngineer/GetCatalogEngineerQuery.cs` | `public sealed record GetCatalogEngineerQuery(string Slug) : IRequest<CatalogEngineerDetailResult>;` |
| 11 | `.../GetCatalogEngineer/GetCatalogEngineerQueryValidator.cs` | `public sealed class GetCatalogEngineerQueryValidator : AbstractValidator<GetCatalogEngineerQuery>` — single rule: `RuleFor(x => x.Slug).ValidateRequired(ErrorCodes.CatalogSlugRequired);` |
| 12 | `.../GetCatalogEngineer/GetCatalogEngineerQueryHandler.cs` | `public sealed class GetCatalogEngineerQueryHandler(IEngineerRepository engineerRepository) : IRequestHandler<GetCatalogEngineerQuery, CatalogEngineerDetailResult>` — steps in **Handlers**. |

### Application — GetCatalogTags use case

Namespace: `E3A.Application.Catalog.GetCatalogTags;`

| # | Path | Contract |
|---|------|----------|
| 13 | `.../GetCatalogTags/GetCatalogTagsQuery.cs` | `public sealed record GetCatalogTagsQuery : IRequest<List<CatalogTagResult>>;` (no parameters, **no validator** — slice ① Decision 18 precedent: an empty validator is a registered no-op). |
| 14 | `.../GetCatalogTags/GetCatalogTagsQueryHandler.cs` | `public sealed class GetCatalogTagsQueryHandler(IEngineerRepository engineerRepository) : IRequestHandler<GetCatalogTagsQuery, List<CatalogTagResult>>` — steps in **Handlers**. |

### API

| # | Path | Contract |
|---|------|----------|
| 15 | `api/E3A.Api/Controllers/Catalog/CatalogController.cs` | See **API surface**. No `Requests.cs` — every input is a query-string primitive. |

### Postman

| # | Path | Contract |
|---|------|----------|
| 16 | `postman/e3a.postman_collection.json` | Collection v2.1 — structure in **Postman collection**. |
| 17 | `postman/e3a.local.postman_environment.json` | Environment — structure in **Postman collection**. |

### Tests (`api/E3A.Tests/Catalog/…`, namespaces mirror folders: `E3A.Tests.Catalog.GetCatalog`, …)

| # | Path |
|---|------|
| 18 | `api/E3A.Tests/Catalog/Shared/CatalogEngineerResultGeneratorTests.cs` |
| 19 | `api/E3A.Tests/Catalog/GetCatalog/GetCatalogQueryValidatorTests.cs` |
| 20 | `api/E3A.Tests/Catalog/GetCatalog/GetCatalogQueryHandlerFilterTests.cs` |
| 21 | `api/E3A.Tests/Catalog/GetCatalog/GetCatalogQueryHandlerPagingTests.cs` |
| 22 | `api/E3A.Tests/Catalog/GetCatalogEngineer/GetCatalogEngineerQueryValidatorTests.cs` |
| 23 | `api/E3A.Tests/Catalog/GetCatalogEngineer/GetCatalogEngineerQueryHandlerTests.cs` |
| 24 | `api/E3A.Tests/Catalog/GetCatalogTags/GetCatalogTagsQueryHandlerTests.cs` |

Usings required across Application files: `Core.DDD.Models`, `Core.Errors`, `Core.Validation.Extensions`, `E3A.Application.Catalog.Shared`, `E3A.Application.Engineers.Shared`, `E3A.Application.Exceptions`, `E3A.Application.Options`, `E3A.Domain.Engineers`, `FluentValidation`, `MediatR`, `Microsoft.Extensions.Options`, `System.Text.Json`.

## Error codes

Append to `ErrorCodes.cs` under a new `// Catalog` separator:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `CatalogSearchTextTooLong` | `CATALOG_SEARCH_TEXT_TOO_LONG` | `GetCatalogQueryValidator` | `ValidationBehaviourException` | 422 |
| `CatalogTooManyTagFilters` | `CATALOG_TOO_MANY_TAG_FILTERS` | validator | `ValidationBehaviourException` | 422 |
| `CatalogTagFilterTooLong` | `CATALOG_TAG_FILTER_TOO_LONG` | validator | `ValidationBehaviourException` | 422 |
| `CatalogSortInvalid` | `CATALOG_SORT_INVALID` | validator | `ValidationBehaviourException` | 422 |
| `CatalogPageNumberInvalid` | `CATALOG_PAGE_NUMBER_INVALID` | validator | `ValidationBehaviourException` | 422 |
| `CatalogPageSizeInvalid` | `CATALOG_PAGE_SIZE_INVALID` | validator | `ValidationBehaviourException` | 422 |
| `CatalogSlugRequired` | `CATALOG_SLUG_REQUIRED` | `GetCatalogEngineerQueryValidator` | `ValidationBehaviourException` | 422 |

Reused (already in `ErrorCodes` + both resx — do not duplicate): `EngineerNotFound` (`ENGINEER_NOT_FOUND`, `NotFoundCoreException`, 404), thrown by `GetCatalogEngineerQueryHandler`.

Resource strings (key = code value; Arabic without tashkeel):

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|-----|--------------------|--------------------|
| `CATALOG_SEARCH_TEXT_TOO_LONG` | `The search text is too long.` | `نص البحث طويل جدا.` |
| `CATALOG_TOO_MANY_TAG_FILTERS` | `Too many tag filters were provided.` | `تم تحديد عدد كبير جدا من الوسوم للتصفية.` |
| `CATALOG_TAG_FILTER_TOO_LONG` | `A tag filter is too long.` | `وسم التصفية طويل جدا.` |
| `CATALOG_SORT_INVALID` | `The sort option is not recognized.` | `خيار الترتيب غير معروف.` |
| `CATALOG_PAGE_NUMBER_INVALID` | `The page number must be a positive number.` | `يجب ان يكون رقم الصفحة رقما موجبا.` |
| `CATALOG_PAGE_SIZE_INVALID` | `The page size is out of the allowed range.` | `حجم الصفحة خارج النطاق المسموح به.` |
| `CATALOG_SLUG_REQUIRED` | `An engineer slug is required.` | `المعرف النصي للمهندس مطلوب.` |

## Domain behaviour

Append to `Engineer` (between `MarkPublished` and `ReplaceDraftManifest`), exact body:

```csharp
public void RecordInstallCount(int installCount)
{
    InstallCount = installCount;
    UpdationDate = DateTimeOffset.UtcNow;
}
```

No guard (Decision 17). Handlers never assign `InstallCount` directly — this method is the only mutator. Nothing else on the entity changes; no `BusinessRuleViolationException` anywhere in this slice.

## Handlers

No current-user guard anywhere in this slice — all three handlers are anonymous by design and take no `ICurrentUserService`. No `SaveChangesAsync` anywhere — pure reads.

**`GetCatalogQueryHandler.Handle`** (returns `Task<PageData<CatalogEngineerResult>>`)

1. `var options = catalogOptions.Value;`
2. `var engineers = await engineerRepository.FindAsync(x => x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
3. `var searchText = request.SearchText?.Trim();` · `IEnumerable<Engineer> filtered = engineers;`
4. `if (!string.IsNullOrEmpty(searchText)) { filtered = filtered.Where(x => MatchesSearchText(x, searchText)); }`
5. `if (request.Tags.Count > 0) { filtered = filtered.Where(x => MatchesAnyTag(x, request.Tags)); }`
6. ```csharp
   var ordered = request.Sort switch
   {
       CatalogSort.Newest => filtered.OrderByDescending(x => x.CreationDate),
       _ => filtered.OrderByDescending(x => x.InstallCount).ThenByDescending(x => x.CreationDate),
   };
   ```
7. `var matched = ordered.ToList();` · `var pageSize = request.PageSize ?? options.DefaultPageSize;`
8. ```csharp
   var items = matched
       .Skip((request.PageNumber - 1) * pageSize)
       .Take(pageSize)
       .Select(CatalogEngineerResultGenerator.Generate)
       .ToList();
   ```
9. `return new PageData<CatalogEngineerResult> { Items = items, PageNumber = request.PageNumber, PageSize = pageSize, TotalItems = matched.Count, TotalPages = (long)Math.Ceiling(matched.Count / (double)pageSize) };`

Private helpers on the same class:

```csharp
private static bool MatchesSearchText(Engineer engineer, string searchText)
{
    return engineer.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
        || (engineer.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
        || engineer.Tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase));
}

private static bool MatchesAnyTag(Engineer engineer, List<string> tags)
{
    return engineer.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
}
```

**`GetCatalogEngineerQueryHandler.Handle`** (returns `Task<CatalogEngineerDetailResult>`)

1. `var engineer = await engineerRepository.FirstOrDefaultAsync(x => x.Slug == request.Slug && x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
2. `if (engineer == null) { throw new NotFoundCoreException(ErrorCodes.EngineerNotFound); }`
3. `return CatalogEngineerResultGenerator.GenerateDetail(engineer);`

**`GetCatalogTagsQueryHandler.Handle`** (returns `Task<List<CatalogTagResult>>`)

1. `var engineers = await engineerRepository.FindAsync(x => x.Status == EngineerStatus.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
2. ```csharp
   return engineers
       .SelectMany(x => x.Tags.Select(tag => tag.ToLowerInvariant()).Distinct())
       .GroupBy(tag => tag)
       .Select(group => new CatalogTagResult(group.Key, group.Count()))
       .OrderByDescending(x => x.Count)
       .ThenBy(x => x.Tag, StringComparer.Ordinal)
       .ToList();
   ```

## API surface

New `api/E3A.Api/Controllers/Catalog/CatalogController.cs`:

```csharp
namespace E3A.Api.Controllers.Catalog;

[ApiController]
[Route("api/catalog")]
[AllowAnonymous]
public class CatalogController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> GetCatalog([FromQuery(Name = "q")] string? searchText, [FromQuery(Name = "tag")] List<string>? tags, [FromQuery] CatalogSort sort = CatalogSort.MostInstalled, [FromQuery(Name = "page")] int pageNumber = 1, [FromQuery] int? pageSize = null, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetCatalogQuery(searchText, tags ?? [], sort, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("tags")]
    public async Task<ActionResult> GetCatalogTags(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCatalogTagsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult> GetCatalogEngineer([FromRoute] string slug, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCatalogEngineerQuery(slug), cancellationToken);
        return Ok(result);
    }
}
```

| Method | Route | Auth | Output |
|---|---|---|---|
| GET | `api/catalog?q&tag&sort&page&pageSize` | anonymous | `Ok(PageData<CatalogEngineerResult>)` |
| GET | `api/catalog/tags` | anonymous | `Ok(List<CatalogTagResult>)` |
| GET | `api/catalog/{slug}` | anonymous | `Ok(CatalogEngineerDetailResult)` · 404 when not published |

And on `EngineersController`: the `ListEngineers` action is **removed** (Decision 1).

## Configuration (appsettings.json addition)

```json
"Catalog": {
  "DefaultPageSize": 9,
  "MaxPageSize": 50,
  "SearchTextMaxLength": 100,
  "MaxTagFilters": 10,
  "TagFilterMaxLength": 30
}
```

Place it after the `"Engineers"` section.

## Postman collection

**`postman/e3a.postman_collection.json`** — top level:

```json
{
  "info": {
    "_postman_id": "e3a00000-0000-4000-8000-000000000001",
    "name": "e3a",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "auth": { "type": "bearer", "bearer": [ { "key": "token", "value": "{{token}}", "type": "string" } ] },
  "item": [ { "name": "Engineers", "item": [ … ] }, { "name": "Catalog", "item": [ … ] } ]
}
```

Every request `url` uses the object form: `{ "raw": "{{baseUrl}}/api/…", "host": [ "{{baseUrl}}" ], "path": [ "api", … ], "query": [ … ] }`. JSON bodies use `"body": { "mode": "raw", "raw": "…", "options": { "raw": { "language": "json" } } }`.

**Engineers folder** (inherits collection bearer auth) — 7 requests in this order:

| Name | Method · URL | Body |
|---|---|---|
| Create Engineer | POST `{{baseUrl}}/api/engineers` | raw JSON `{ "displayName": "Dive Backend Engineer", "description": "Senior .NET backend engineer — CQRS vertical slices, EF Core, clean error contracts.", "tags": ["dotnet", "cqrs", "api"] }` |
| List My Engineers | GET `{{baseUrl}}/api/engineers/mine` | — |
| Get Engineer | GET `{{baseUrl}}/api/engineers/{{engineerId}}` | — |
| Update Engineer | PUT `{{baseUrl}}/api/engineers/{{engineerId}}` | raw JSON `{ "displayName": "Dive Backend Engineer", "description": "Updated description.", "tags": ["dotnet", "ddd"] }` |
| Upload Engineer Draft | POST `{{baseUrl}}/api/engineers/{{engineerId}}/upload` | `"body": { "mode": "formdata", "formdata": [ { "key": "file", "type": "file", "src": [] } ] }` |
| Get Import Manifest | GET `{{baseUrl}}/api/engineers/{{engineerId}}/import-manifest` | — |
| Delete Engineer | DELETE `{{baseUrl}}/api/engineers/{{engineerId}}` | — |

**Catalog folder** — 3 requests, each with `"auth": { "type": "noauth" }` on the request:

| Name | Method · URL |
|---|---|
| Browse Catalog | GET `{{baseUrl}}/api/catalog?q=backend&tag=dotnet&sort=MostInstalled&page=1&pageSize=9` — `query` array carries all five entries (`q`, `tag`, `sort`, `page`, `pageSize`) with these example values |
| Get Catalog Tags | GET `{{baseUrl}}/api/catalog/tags` |
| Get Catalog Engineer | GET `{{baseUrl}}/api/catalog/dive-backend-engineer` |

The removed `GET /api/engineers` list is deliberately absent.

**`postman/e3a.local.postman_environment.json`**:

```json
{
  "id": "e3a00000-0000-4000-8000-000000000002",
  "name": "e3a local",
  "values": [
    { "key": "baseUrl", "value": "https://localhost:62935", "type": "default", "enabled": true },
    { "key": "token", "value": "", "type": "secret", "enabled": true },
    { "key": "engineerId", "value": "", "type": "default", "enabled": true }
  ],
  "_postman_variable_scope": "environment"
}
```

## Test plan

xUnit + NSubstitute + FluentAssertions **6.12.2** (repo-pinned — do not upgrade). Entities only via `EngineerFactory`; exception asserts bind `.Where(x => x.ErrorCode == ErrorCodes.X)`; `CancellationToken.None` in acts, `Arg.Any<CancellationToken>()` in setups; substitute stubs mirror the exact call shape used by the codebase today: `_engineerRepository.FindAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns(…)` and `_engineerRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Engineer, bool>>>(), Arg.Any<CancellationToken>(), asNoTracking: true).Returns(…)`.

Append to `EngineerFactory` (after `Draft`):

```csharp
public static Engineer Published(Guid ownerUserId, string slug = DefaultSlug, string displayName = DefaultDisplayName, string? description = "A backend engineer.", List<string>? tags = null, int installCount = 0, DateTimeOffset? creationDate = null)
{
    var engineer = Engineer.Create(ownerUserId, slug, displayName, description, tags ?? ["dotnet", "ddd"]);
    engineer.MarkPublished(Guid.NewGuid());
    engineer.RecordInstallCount(installCount);

    if (creationDate != null)
    {
        engineer.CreationDate = creationDate.Value;
    }

    return engineer;
}
```

Handler test classes build `Options.Create(new CatalogOptions { DefaultPageSize = 2, MaxPageSize = 50, SearchTextMaxLength = 100, MaxTagFilters = 10, TagFilterMaxLength = 30 })` (small default page size makes the default-paging test meaningful); validator tests use the committed appsettings values via the same inline construction with `DefaultPageSize = 9`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `EngineerTests` (append) | `RecordInstallCount_ShouldSetInstallCount_WhenCalled` | `InstallCount` equals input |
| 2 | | `RecordInstallCount_ShouldAdvanceUpdationDate_WhenCalled` | `UpdationDate.Should().BeOnOrAfter(before)` |
| 3 | `CatalogEngineerResultGeneratorTests` | `GenerateDetail_ShouldReturnEmptyHookWarnings_WhenDraftManifestIsNull` | `HookWarnings` empty; `OwnerUserId`, `Slug`, `InstallCount` mapped |
| 4 | | `GenerateDetail_ShouldReturnHookWarnings_WhenDraftManifestContainsThem` | engineer given `ReplaceDraftManifest(JsonSerializer.Serialize(manifest))` where manifest has one `HookWarningResult("PreToolUse", "Bash", "check.sh")` → warning round-trips |
| 5 | `GetCatalogQueryValidatorTests` | `Validate_ShouldPass_WhenQueryIsValid` | defaults + `SearchText "backend"`, one tag, `PageSize 9` → `IsValid` true |
| 6 | | `Validate_ShouldPass_WhenPageSizeIsNull` | true |
| 7 | | `Validate_ShouldFail_WhenSearchTextExceedsMaxLength` | 101 chars → `CatalogSearchTextTooLong` |
| 8 | | `Validate_ShouldFail_WhenTagFiltersExceedMaxCount` | 11 tags → `CatalogTooManyTagFilters` |
| 9 | | `Validate_ShouldFail_WhenTagFilterExceedsMaxLength` | one 31-char tag → `CatalogTagFilterTooLong` |
| 10 | | `Validate_ShouldFail_WhenSortIsNotDefined` | `(CatalogSort)99` → `CatalogSortInvalid` |
| 11 | | `Validate_ShouldFail_WhenPageNumberIsNotPositive` | `[Theory]` 0, -1 → `CatalogPageNumberInvalid` |
| 12 | | `Validate_ShouldFail_WhenPageSizeIsNotPositive` | 0 → `CatalogPageSizeInvalid` |
| 13 | | `Validate_ShouldFail_WhenPageSizeExceedsMax` | 51 → `CatalogPageSizeInvalid` |
| 14 | `GetCatalogQueryHandlerFilterTests` | `Handle_ShouldQueryOnlyPublished_WhenCalled` | `Received(1).FindAsync` with predicate that compiles true for `EngineerFactory.Published(...)` and false for `EngineerFactory.Draft(...)` (mirror the deleted `FilterMatchesOnlyPublished` helper pattern) |
| 15 | | `Handle_ShouldReturnAllPublishedMapped_WhenNoFiltersProvided` | 2 engineers → `Items` count 2; one item's `Id/Slug/DisplayName/Description/Tags/InstallCount/LatestVersionId/CreatedAt/UpdatedAt` all mapped |
| 16 | | `Handle_ShouldFilterBySearchText_WhenItMatchesDisplayName` | search `"BACKEND"` matches `DisplayName "Dive Backend Engineer"`, excludes non-matching engineer |
| 17 | | `Handle_ShouldFilterBySearchText_WhenItMatchesDescription` | match on description only |
| 18 | | `Handle_ShouldFilterBySearchText_WhenItMatchesTags` | match on a tag only |
| 19 | | `Handle_ShouldTrimSearchText_WhenItHasSurroundingWhitespace` | `"  backend  "` still matches |
| 20 | | `Handle_ShouldFilterByTags_WhenAnyTagMatchesCaseInsensitively` | filter `["DOTNET", "sql"]` keeps engineer tagged `dotnet`, drops engineer tagged `react` |
| 21 | | `Handle_ShouldReturnEmptyPage_WhenNothingMatches` | `Items` empty, `TotalItems` 0, `TotalPages` 0 |
| 22 | `GetCatalogQueryHandlerPagingTests` | `Handle_ShouldOrderByCreationDateDescending_WhenSortIsNewest` | distinct `creationDate` values; newest first |
| 23 | | `Handle_ShouldOrderByInstallCountDescending_WhenSortIsMostInstalled` | `installCount` 5 before 1 |
| 24 | | `Handle_ShouldBreakInstallCountTiesByCreationDate_WhenSortIsMostInstalled` | equal counts → newer first |
| 25 | | `Handle_ShouldReturnRequestedPage_WhenPageNumberBeyondFirst` | 3 engineers, `PageSize 2`, `PageNumber 2` → 1 item, `TotalItems` 3, `TotalPages` 2, `PageNumber` 2, `PageSize` 2 |
| 26 | | `Handle_ShouldUseDefaultPageSize_WhenPageSizeIsNull` | options `DefaultPageSize 2`, 3 engineers, `PageSize null` → 2 items, `PageSize` 2 |
| 27 | `GetCatalogEngineerQueryValidatorTests` | `Validate_ShouldPass_WhenQueryIsValid` | true |
| 28 | | `Validate_ShouldFail_WhenSlugIsEmpty` | `""` → `CatalogSlugRequired` |
| 29 | `GetCatalogEngineerQueryHandlerTests` | `Handle_ShouldReturnDetail_WhenPublishedEngineerExists` | detail fields incl. `OwnerUserId` and empty `HookWarnings` |
| 30 | | `Handle_ShouldThrowNotFound_WhenNoPublishedEngineerMatchesSlug` | `FirstOrDefaultAsync` returns null → `NotFoundCoreException` · `EngineerNotFound` |
| 31 | | `Handle_ShouldMatchOnlyPublishedWithSlug_WhenQueryingTheRepository` | `Received(1).FirstOrDefaultAsync` with predicate compiling true for published+slug, false for draft with same slug, false for published with other slug |
| 32 | `GetCatalogTagsQueryHandlerTests` | `Handle_ShouldReturnTagsWithEngineerCounts_WhenPublishedEngineersExist` | two engineers sharing `dotnet` → `("dotnet", 2)` present |
| 33 | | `Handle_ShouldGroupTagsCaseInsensitively_WhenCasingDiffers` | `"DotNet"` + `"dotnet"` across two engineers → one entry `Tag "dotnet"`, `Count` 2 |
| 34 | | `Handle_ShouldCountEngineersNotOccurrences_WhenAnEngineerRepeatsATag` | one engineer tagged `["dotnet", "DotNet"]` → `Count` 1 |
| 35 | | `Handle_ShouldOrderByCountThenTag_WhenCountsTie` | higher count first; ties alphabetical ordinal |
| 36 | | `Handle_ShouldReturnEmptyList_WhenNothingIsPublished` | `[]` from repo → empty result |

Deleted with the `ListEngineers` use case: `ListEngineersQueryHandlerTests` (its published-only-predicate coverage is inherited by test 14). No repository, controller, EF-configuration, DI or Postman tests (conventions §5, Decision 22).

## Definition of done

- [ ] `Engineer.RecordInstallCount(int)` exists exactly as specified, sets `UpdationDate`; no other domain change.
- [ ] `E3A.Application/Engineers/ListEngineers/` and `E3A.Tests/Engineers/ListEngineers/` are deleted; `EngineersController` no longer has a `ListEngineers` action nor its `using`; every other Engineers file is byte-identical except the listed appends.
- [ ] 14 new production files (+1 controller) exist at the exact paths with the exact type names and signatures; no additional production files; no new interfaces, services, exception types, or repository members (`IEngineerRepository` unchanged).
- [ ] `GetCatalogQueryHandler` fetches published rows once via `FindAsync(asNoTracking: true)`, filters/sorts in memory per the specified predicates and switch expression, and returns a hand-built `PageData<CatalogEngineerResult>` with correct `TotalItems`/`TotalPages` math; no `SaveChangesAsync`, no `ICurrentUserService` anywhere in the Catalog area.
- [ ] Detail endpoint 404s (`ENGINEER_NOT_FOUND`) for missing **and** unpublished slugs; drafts are never disclosed.
- [ ] `GenerateDetail` derives `HookWarnings` from `DraftManifestJson` reusing `E3A.Application.Engineers.Shared.HookWarningResult`, `[]` when null.
- [ ] Tags endpooint returns lowercase tags with engineer-level counts, ordered count desc then tag asc.
- [ ] All five catalog tunables live in `CatalogOptions` bound in `AddApplication`; `appsettings.json` gains the `Catalog` section verbatim; zero inline magic values.
- [ ] The 7 new `ErrorCodes` constants exist with the exact values, present in **both** resx files; `ENGINEER_NOT_FOUND` reused, not duplicated.
- [ ] `CatalogController`: `[AllowAnonymous]` class-level, three thin actions, wire names `q`/`tag`/`sort`/`page`/`pageSize`, `tags ?? []` at the boundary, `CancellationToken` passed to every `Send`; `Program.cs` and middleware order untouched.
- [ ] `postman/e3a.postman_collection.json` (v2.1 schema URL, collection-level bearer `{{token}}`, Engineers folder with the 7 listed requests, Catalog folder with 3 `noauth` requests) and `postman/e3a.local.postman_environment.json` (`baseUrl = https://localhost:62935`, secret `token`, `engineerId`) exist and parse as valid JSON.
- [ ] `docs/implementation-plan.md` and `docs/architecture.md` carry exactly the two replacement edits specified — no other doc lines change.
- [ ] All 36 tests above exist with the exact names and pass; `EngineerFactory.Published` added as specified; no existing test breaks other than the intentional `ListEngineers` deletion; FluentAssertions stays 6.12.2.
- [ ] `dotnet build` zero new warnings; `dotnet test` green; file-scoped namespaces; every file ≤ ~100 lines; `.ConfigureAwait(false)` on every await outside controllers/test bodies.
