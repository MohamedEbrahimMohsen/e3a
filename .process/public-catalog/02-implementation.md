# Implementation — Anonymous Public Catalog

## Files created

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Application/Options/CatalogOptions.cs` | 12 | `SectionName = "Catalog"` + the five catalog tunables (DefaultPageSize, MaxPageSize, SearchTextMaxLength, MaxTagFilters, TagFilterMaxLength) |
| `api/E3A.Application/Catalog/Shared/CatalogSort.cs` | 3 | `enum CatalogSort { MostInstalled, Newest }` |
| `api/E3A.Application/Catalog/Shared/CatalogEngineerResult.cs` | 3 | List-card result record |
| `api/E3A.Application/Catalog/Shared/CatalogEngineerDetailResult.cs` | 5 | Detail result record; reuses `E3A.Application.Engineers.Shared.HookWarningResult` |
| `api/E3A.Application/Catalog/Shared/CatalogTagResult.cs` | 3 | `(string Tag, int Count)` |
| `api/E3A.Application/Catalog/Shared/CatalogEngineerResultGenerator.cs` | 20 | `Generate` + `GenerateDetail` (hook warnings deserialized from `DraftManifestJson`) |
| `api/E3A.Application/Catalog/GetCatalog/GetCatalogQuery.cs` | 7 | `sealed record … : IRequest<PageData<CatalogEngineerResult>>` |
| `api/E3A.Application/Catalog/GetCatalog/GetCatalogQueryValidator.cs` | 33 | Six rules; raw `.Must` for the nullable page size |
| `api/E3A.Application/Catalog/GetCatalog/GetCatalogQueryHandler.cs` | 65 | Published fetch → in-memory filter/sort → hand-built `PageData` + two private static predicates |
| `api/E3A.Application/Catalog/GetCatalogEngineer/GetCatalogEngineerQuery.cs` | 6 | `sealed record GetCatalogEngineerQuery(string Slug)` |
| `api/E3A.Application/Catalog/GetCatalogEngineer/GetCatalogEngineerQueryValidator.cs` | 13 | `ValidateRequired(ErrorCodes.CatalogSlugRequired)` |
| `api/E3A.Application/Catalog/GetCatalogEngineer/GetCatalogEngineerQueryHandler.cs` | 22 | Published-only slug lookup, `NotFoundCoreException(EngineerNotFound)` on miss |
| `api/E3A.Application/Catalog/GetCatalogTags/GetCatalogTagsQuery.cs` | 6 | Parameterless query, no validator |
| `api/E3A.Application/Catalog/GetCatalogTags/GetCatalogTagsQueryHandler.cs` | 21 | Lowercased, engineer-level tag counts, count desc then tag ordinal asc |
| `api/E3A.Api/Controllers/Catalog/CatalogController.cs` | 36 | `[AllowAnonymous]` class-level, three thin actions on `api/catalog` |
| `postman/e3a.postman_collection.json` | 291 | v2.1 collection, bearer `{{token}}`, Engineers (7) + Catalog (3 × `noauth`) |
| `postman/e3a.local.postman_environment.json` | 25 | `baseUrl`, secret `token`, `engineerId` |
| `api/E3A.Tests/Catalog/Shared/CatalogEngineerResultGeneratorTests.cs` | 40 | Tests 3–4 |
| `api/E3A.Tests/Catalog/GetCatalog/GetCatalogQueryValidatorTests.cs` | 97 | Tests 5–13 |
| `api/E3A.Tests/Catalog/GetCatalog/GetCatalogQueryHandlerFilterTests.cs` | 98 | Tests 14–21 |
| `api/E3A.Tests/Catalog/GetCatalog/GetCatalogQueryHandlerPagingTests.cs` | 89 | Tests 22–26 |
| `api/E3A.Tests/Catalog/GetCatalogEngineer/GetCatalogEngineerQueryValidatorTests.cs` | 28 | Tests 27–28 |
| `api/E3A.Tests/Catalog/GetCatalogEngineer/GetCatalogEngineerQueryHandlerTests.cs` | 68 | Tests 29–31 |
| `api/E3A.Tests/Catalog/GetCatalogTags/GetCatalogTagsQueryHandlerTests.cs` | 85 | Tests 32–36 |

17 production/tooling files + 7 test files = 24 created, exactly the plan's list.

## Files modified

| Path | Change |
|------|--------|
| `api/E3A.Domain/Engineers/Engineer.cs` | Added `RecordInstallCount(int installCount)` between `MarkPublished` and `ReplaceDraftManifest`, body verbatim per plan |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | Removed the `ListEngineers` action and the `using E3A.Application.Engineers.ListEngineers;` line; nothing else touched |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Appended a `// Catalog` group with the 7 constants |
| `api/E3A.Application/DependencyInjection.cs` | Added `services.Configure<CatalogOptions>(configuration.GetSection(CatalogOptions.SectionName));` after the `AzureOptions` line |
| `api/E3A.Api/appsettings.json` | Added the `"Catalog"` section after `"Engineers"` (see Deviations — this file is git-ignored) |
| `api/E3A.Api/Resources/Messages.en.resx` | Appended the 7 English strings before `</root>` |
| `api/E3A.Api/Resources/Messages.ar.resx` | Appended the 7 Arabic strings before `</root>` |
| `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` | Appended `Published(...)` exactly as specified |
| `api/E3A.Tests/Engineers/EngineerTests.cs` | Appended the two `RecordInstallCount` tests |
| `docs/implementation-plan.md` | API-surface sentence replaced with the plan's exact text (Decision 13a) |
| `docs/architecture.md` | Principles clause replaced with the plan's exact text (Decision 13b) |

**Deleted** (per Decision 1): `api/E3A.Application/Engineers/ListEngineers/` (`ListEngineersQuery.cs`, `ListEngineersQueryHandler.cs`) and `api/E3A.Tests/Engineers/ListEngineers/` (`ListEngineersQueryHandlerTests.cs`).

## Deviations

| Plan said | Reality | What I did |
|-----------|---------|------------|
| Modify `api/E3A.Api/appsettings.json` to add the `Catalog` section, and DoD requires "`appsettings.json` gains the `Catalog` section verbatim". | `/api/E3A.Api/appsettings.json` is listed in `.gitignore` line 23 and is **not tracked by git** (only `appsettings.Development.json`, which carries no option sections, is tracked). Same is already true of the `Engineers` and `Uploads` sections from slices ①/②. | Applied the edit on disk exactly as specified — the local run binds `CatalogOptions` correctly. Flagging that the change will **not** appear in the dev's commit, so the `Catalog` section must also be provisioned wherever the deployed config lives (Azure App Configuration). This is a pre-existing repo condition, not something this slice introduced; I did not touch `.gitignore`. |

No other deviations. Every signature, name, file path and test method name is as the plan specified.

Notable non-deviations that could look like ones on review:

- **No `[Authorize(Policy = DefaultCodes.X)]` on `CatalogController`.** Plan Decision 14, and skill §7.3 explicitly blesses `[AllowAnonymous]` for public catalog reads. Verified there is still no `DefaultCodes` class anywhere in `api/` and `Program.cs` registers no named policies.
- **No `ICurrentUserService` guard in any of the three handlers.** All are anonymous by design (Decision 14); the "null-check `UserId` into `UnauthorizedCoreException`" rule has no subject here.
- **No `SaveChangesAsync`.** Pure reads.
- **No `LocalizedText` / `.Localized()`.** e3a is EN-only (slice ① Decision 11); the plan's result shapes are plain strings.
- **§8 catalog:** caps live in `CatalogOptions` (§8.1, no inline magic values), no hand-rolled generator (§8.2, N/A), no slug minting (§8.3, N/A), no `Removed` status (§8.4, N/A), and no ad-hoc `IsDeleted` predicate anywhere — both queries filter on `Status == EngineerStatus.Published` only (§8.5).

## Build & test

Run from `D:\Personal\_e3a\api`:

```
dotnet build E3A.slnx
```
> `Build succeeded.` — `9 Warning(s)`, `0 Error(s)`. All 9 warnings are pre-existing and originate in `core-libraries` (`Core.Validation/Extensions/RequiredValidationExtensions.cs` CS8602 ×2, `Core.OTP/Entities/OTP.cs` CS8618 ×2, `Core.Notifications` CS8618 ×5). **Zero warnings from any `E3A.*` project** — no new warnings introduced.

```
dotnet test E3A.Tests/E3A.Tests.csproj --no-build
```
> `Passed!  - Failed: 0, Passed: 166, Skipped: 0, Total: 166, Duration: 231 ms - E3A.Tests.dll (net10.0)`

The 36 planned tests all exist with the exact class and method names (34 in `E3A.Tests/Catalog`, 2 appended to `EngineerTests`); verified by enumerating declared test methods. The 3 deleted `ListEngineersQueryHandlerTests` cases are the only suite removals.

Postman files validated as parseable JSON (`json.load` on both) — collection carries the v2.1 schema URL, collection-level bearer `{{token}}`, and all 10 requests.

## Notes for review

1. **`GetCatalogQueryHandlerFilterTests` restructure.** My first pass was 123 lines. To respect the ≤ ~100-line rule I hoisted the two fixture engineers and the `FindAsync` stub into fields/constructor. Test names and count are unchanged (8), still exactly tests 14–21. Final: 98 lines. As a side effect, `Handle_ShouldReturnAllPublishedMapped_WhenNoFiltersProvided` now selects the asserted item by `Id` rather than relying on `Items[0]` ordering — this makes it independent of the install-count tie-break, which felt more honest for a mapping test.
2. **Fixture wording differs slightly from the plan's prose** so each search test isolates one field: the backend fixture's description is `"Vertical slices and clean error contracts."` (so `"BACKEND"` matches display name *only*) and the frontend fixture is tagged `["typescript"]` (so the tag test matches a tag *only*, not the `"React Engineer"` display name). Behaviour asserted is exactly what the plan's rows describe.
3. **`EngineerTests.cs` is now 151 lines**, over the ~100 guidance. It was already 130 before this slice; the plan mandates appending the two `RecordInstallCount` tests to it. I did not split the file — splitting an existing test class was not in the plan's file list. Call it if you want it split.
4. **`GenerateDetail` JSON round-trip.** It uses `JsonSerializer.Deserialize<ImportManifestResult>(json)!` with default options, mirroring `GetImportManifestQueryHandler` verbatim as the plan required. A malformed or non-manifest `DraftManifestJson` would therefore throw out of a public anonymous endpoint (500) rather than degrade to `[]`. The plan explicitly forbade a try/catch here and the write path is `UploadEngineerDraftHandler`'s own serializer, so the shape is controlled — but it is the one unguarded input path in the slice, and slice ③'s switch to `FrozenManifestJson` is the natural place to revisit.
5. **Route precedence** (`catalog/tags` vs `catalog/{slug}`) is left to ASP.NET Core's literal-over-parameter ranking per Decision 15; no constraint added, no test (controllers are out of test scope).
6. **`InstallCount` has no write path yet**, so `MostInstalled` (the default sort) always degenerates to the `CreationDate` tie-break in production until the ingestion worker exists. Intentional per Decisions 10/17; `RecordInstallCount` exists solely to make the state domain-reachable.
7. **`PageData<T>` members are `long`** (except `Items`); `TotalItems` is assigned from an `int` `matched.Count` and `TotalPages` from a cast `Math.Ceiling`. Both widen implicitly/explicitly as the plan wrote them — no precision concern at v0.1 scale.
