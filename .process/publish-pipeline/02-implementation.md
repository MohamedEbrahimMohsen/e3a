# Implementation — Publish Pipeline

## Pass 1 — build steps 1–6

Scope executed: plan "Build order" steps 1 through 6, and test-plan items **1–6, 9–46, 60–79**.
Steps 7–9 (`GetPublishStatus`, `PublishController`, unlist/relist handlers + actions, Postman, docs sync)
and tests 7–8, 47–59, 80–87 are **not** implemented — see "What pass 2 still owes" at the end.

### Files created

| Path | Lines | Purpose |
|---|---|---|
| `api/E3A.Domain/Publishing/ItemType.cs` | 7 | `Engineer` / `Team` polymorphic item discriminator |
| `api/E3A.Domain/Publishing/ItemVersionStatus.cs` | 10 | `Queued`/`Building`/`Published`/`Rejected`/`Failed` |
| `api/E3A.Domain/Publishing/VersionIncrement.cs` | 8 | `Patch`/`Minor`/`Major` |
| `api/E3A.Domain/Publishing/PublishRequestedDomainEvent.cs` | 5 | Domain event; also the queue message payload |
| `api/E3A.Domain/Publishing/ItemVersion.cs` | 61 | Aggregate: `Create`, `MarkBuilding`, `MarkPublished`, `MarkFailed`, `IsTerminal` |
| `api/E3A.Domain/Publishing/IItemVersionRepository.cs` | 5 | Empty marker over `IRepository<ItemVersion>` |
| `api/E3A.Domain/Identity/IUserRepository.cs` | 5 | Empty marker over `IRepository<User>` (D9) |
| `api/E3A.Application/Options/PublishingOptions.cs` | 21 | All 14 publishing tunables, bound from `Publishing` section |
| `api/E3A.Application/Publishing/Shared/PluginFile.cs` | 3 | `(Path, Content)` |
| `api/E3A.Application/Publishing/Shared/PluginName.cs` | 12 | `e3a-` prefix, WHY-commented |
| `api/E3A.Application/Publishing/Shared/PublishBlobPaths.cs` | 37 | Every blob path/prefix/content type in one place |
| `api/E3A.Application/Publishing/Shared/SemanticVersionCalculator.cs` | 48 | `Next(previous, increment)`, invariant culture, no throw |
| `api/E3A.Application/Publishing/Shared/PluginJsonSerializer.cs` | 20 | One camelCase/indented/skip-null policy for every artefact |
| `api/E3A.Application/Publishing/Shared/PluginManifest.cs` | 5 | `PluginManifest` + `PluginAuthor` |
| `api/E3A.Application/Publishing/Shared/PluginJsonGenerator.cs` | 19 | Emits `.claude-plugin/plugin.json` |
| `api/E3A.Application/Publishing/Shared/PluginTreeAssembler.cs` | 19 | Manifest-target filter + plugin.json + ordinal order (D11) |
| `api/E3A.Application/Publishing/Shared/PluginStructureValidator.cs` | 73 | Six rules → `ErrorCodes` list |
| `api/E3A.Application/Publishing/Shared/DeterministicZipper.cs` | 34 | Ordinal entry order + 1980 epoch stamp + sha256 |
| `api/E3A.Application/Publishing/Shared/DraftSnapshotFreezer.cs` | 37 | drafts → `snapshots/{versionId}/**`, returns in-memory bytes (D12) |
| `api/E3A.Application/Publishing/Shared/MarketplaceDocument.cs` | 9 | D18 wrapper records |
| `api/E3A.Application/Publishing/Shared/MarketplaceDocumentGenerator.cs` | 24 | `GeneratePlugin` + `Generate` |
| `api/E3A.Application/Publishing/Shared/PublishStatusResult.cs` | 3 | Client-facing status result |
| `api/E3A.Application/Publishing/Shared/PublishStatusResultGenerator.cs` | 15 | Maps version → result |
| `api/E3A.Application/Engineers/PublishEngineer/PublishEngineerCommand.cs` | 7 | |
| `api/E3A.Application/Engineers/PublishEngineer/PublishEngineerValidator.cs` | 14 | |
| `api/E3A.Application/Engineers/PublishEngineer/PublishEngineerHandler.cs` | 66 | Six guards, one `SaveChangesAsync` |
| `api/E3A.Application/Publishing/PublishRequested/PublishRequestedEventHandler.cs` | 19 | Enqueue with configured visibility timeout |
| `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobCommand.cs` | 5 | |
| `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobValidator.cs` | 14 | |
| `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs` | 106 | 14-step worker; no try/catch; ≤2 saves on every path |
| `api/E3A.Application/Publishing/RegenerateMarketplace/RegenerateMarketplaceCommand.cs` | 5 | |
| `api/E3A.Application/Publishing/RegenerateMarketplace/RegenerateMarketplaceHandler.cs` | 66 | Bounded paging (D7) + single atomic PUT (D6) |
| `api/E3A.Infrastructure/Publishing/ItemVersionRepository.cs` | 7 | |
| `api/E3A.Infrastructure/Identity/UserRepository.cs` | 7 | |
| `api/E3A.Infrastructure/Data/Migrations/20260828115939_versions002.cs` | 61 | Generated — creates `ItemVersions` + both indexes |
| `api/E3A.Infrastructure/Data/Migrations/20260828115939_versions002.Designer.cs` | 833 | Generated |
| `api/E3A.Jobs/E3A.Jobs.csproj` | 36 | Functions v4 isolated |
| `api/E3A.Jobs/Program.cs` | 32 | Composition mirroring the API's `Core.*` wiring |
| `api/E3A.Jobs/host.json` | 18 | `batchSize: 1`, `newBatchThreshold: 0` (D5) |
| `api/E3A.Jobs/Functions/ProcessPublishJobFunction.cs` | 22 | Two `Send` calls, no branches, no try/catch |

Tests created (all under `api/E3A.Tests/`):

| Path | Lines | Test-plan items |
|---|---|---|
| `Publishing/Shared/ItemVersionFactory.cs` | 42 | shared factory |
| `Publishing/Shared/PluginFileFactory.cs` | 26 | shared factory |
| `Publishing/Shared/PublishingOptionsFactory.cs` | 32 | shared factory (see Deviations) |
| `Publishing/ItemVersionTests.cs` | 87 | 1–6 |
| `Publishing/Shared/SemanticVersionCalculatorTests.cs` | 42 | 9–10 |
| `Publishing/Shared/PluginJsonGeneratorTests.cs` | 40 | 11–12 |
| `Publishing/Shared/PluginTreeAssemblerTests.cs` | 51 | 13–15 |
| `Publishing/Shared/PluginStructureValidatorTests.cs` | 86 | 16–22 |
| `Publishing/Shared/DeterministicZipperTests.cs` | 62 | 23–26 |
| `Publishing/Shared/MarketplaceDocumentGeneratorTests.cs` | 52 | 27–29 |
| `Publishing/Shared/DraftSnapshotFreezerTests.cs` | 70 | 30–32 |
| `Publishing/Shared/PublishStatusResultGeneratorTests.cs` | 37 | 33–34 |
| `Engineers/PublishEngineer/PublishEngineerValidatorTests.cs` | 38 | 35–37 |
| `Engineers/PublishEngineer/PublishEngineerHandlerTests.cs` | 78 | 38–40 |
| `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests.cs` | 100 | 41–46 |
| `Publishing/PublishRequested/PublishRequestedEventHandlerTests.cs` | 46 | 60–61 |
| `Publishing/ProcessPublishJob/ProcessPublishJobValidatorTests.cs` | 26 | 62–63 |
| `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests.cs` | 102 | 64–68 |
| `Publishing/ProcessPublishJob/ProcessPublishJobHandlerGuardTests.cs` | 58 | 69–70 |
| `Publishing/ProcessPublishJob/ProcessPublishJobHandlerFailureTests.cs` | 93 | 71–73 |
| `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests.cs` | 137 | 74–79 |

All 64 planned pass-1 test methods exist with the exact names from the plan.

### Files modified

| Path | Change |
|---|---|
| `api/core-libraries/Core.Azure/Clients/StorageBlobClient.cs` | +3 additive members on interface and class, +`using Azure;`. The two pre-existing signatures and bodies are byte-identical to `main` (verified with `git diff`). |
| `api/E3A.Domain/Engineers/EngineerStatus.cs` | `Unlisted` added between `Published` and `Deleted` |
| `api/E3A.Domain/Engineers/Engineer.cs` | `Unlist()` / `Relist()` added |
| `api/E3A.Application/Options/AzureOptions.cs` | +`StorageAccountQueueUrl`, `SnapshotsBlobContainerName`, `PublicBlobContainerName`, `PublishQueueName` |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | +2 under `// Engineers`, +13 under new `// Publishing` group |
| `api/E3A.Application/DependencyInjection.cs` | `Configure<PublishingOptions>` |
| `api/E3A.Infrastructure/DependencyInjection.cs` | `IItemVersionRepository`, `IUserRepository` registered |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | ctor gains `IOptions<PublishingOptions>`; `DbSet<ItemVersion>`; `ConfigureItemVersions`; `ItemVersion` added to `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`; new `Sha256HexLength` const with WHY comment |
| `api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef` |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | `PublishEngineer` action only (unlist/relist are pass 2) |
| `api/E3A.Api/Controllers/Engineers/Requests.cs` | +`PublishEngineerRequest` |
| `api/E3A.Api/Resources/Messages.en.resx` / `Messages.ar.resx` | +15 keys each, same order, Arabic without tashkeel (both files now 53 keys) |
| `api/E3A.Api/appsettings.json` | +`Publishing` section (14 keys) and `Azure:PublishQueueName`. Git-ignored, so it does not appear in the diff. |
| `api/E3a.slnx` | `E3A.Jobs` registered after `E3A.Infrastructure` |
| `api/.editorconfig` | +1 analyzer configuration line — **not in the plan's touched-file list**, see Deviations #1 |

## Deviations

| # | Plan said | Reality | What I did |
|---|---|---|---|
| 1 | File #33 is `PublishRequestedEventHandler` (mirroring Morabh). `.editorconfig` is not in "Existing code touched". | `CA1711` ("do not suffix a type with `EventHandler`") is a **build error** here — `TreatWarningsAsErrors` is on. The name cannot compile as written. | Kept the plan's type name and added `dotnet_code_quality.CA1711.allowed_suffixes = EventHandler` to `api/.editorconfig` with a WHY comment. This mirrors the repo's own precedent for analyzer/convention conflicts (`CA1716` for `Shared`, `CA1707` for tests). The alternative — renaming — would also have forced renaming test classes 60–61 away from the plan's exact names. **The reviewer should confirm this trade is acceptable.** |
| 2 | `PublishEngineerHandler` step 9 passes `engineer.DraftManifestJson!`. | Sonar `S8969` errors: the compiler already proves it non-null after the `string.IsNullOrWhiteSpace` guard. | Dropped the `!`. Behaviour identical. |
| 3 | File #46 `E3A.Jobs.csproj` lists eight `PackageReference`s and three `ProjectReference`s (`E3A.Application`, `E3A.Domain`, `E3A.Infrastructure`). | `Program.cs` (file #47) calls `AddCoreCQRS()`, which lives in `Core.CQRS`. `Core.CQRS` is **not** reachable transitively — `E3A.Api` references it directly and `E3A.Application` does not. Build error `CS0234`. | Added `<ProjectReference Include="../core-libraries/Core.CQRS/Core.CQRS.csproj" />`. No `Directory.Packages.props` edit was needed, as instructed. |
| 4 | File #47 `Program.cs` ends `builder.Build().Run();`. | Analyzers prefer the awaitable form, and `E3A.Api/Program.cs` already uses `await app.RunAsync()`. | `await builder.Build().RunAsync();` — mirrors the API. |
| 5 | File #49 `ProcessPublishJobFunction` body: "one `logger.LogInformation` naming `publishRequested.VersionId`". | `CA1848` + `CA1873` error on a direct `LogInformation` call under `TreatWarningsAsErrors`. | Same single log line, emitted through a `static readonly Action<ILogger, Guid, Exception?>` built by `LoggerMessage.Define<Guid>`. Message text and level unchanged; still no branches and no try/catch. |
| 6 | `PublishEngineerRequest(VersionIncrement Increment)` (plain positional record). | Sonar `S6964`: a non-nullable value-type property on a controller input must be nullable, `required`, or `[JsonRequired]` (under-posting guard). Build error. | `public sealed record PublishEngineerRequest([property: JsonRequired] VersionIncrement Increment);` — keeps the type and makes a missing `increment` a 400 rather than a silent `Patch`. |
| 7 | Test plan names exactly two new shared factories (`ItemVersionFactory`, `PluginFileFactory`). | Fourteen `PublishingOptions` properties are needed by ~12 test classes. | Added a third, `api/E3A.Tests/Publishing/Shared/PublishingOptionsFactory.cs`, mirroring the existing `Engineers/Shared/UploadsOptionsFactory.cs`. Also added one extra method `ConvertingManifest(...)` to `PluginFileFactory` (same file, no new file) because test 14 needs a manifest with a `Converted` entry and the plan's `Manifest(...)` only produces `Imported`. |
| 8 | `PluginStructureValidator` rule spec names the literals `agents/`, `skills/`, `commands/`, `SKILL.md`, `..`. The DoD's "only permitted literals" list does not include them. | They are unavoidable and are Claude Code protocol facts, not product tunables. | Declared as private named constants with WHY comments (`InstallableRoots`, `SkillsRoot`, `SkillFileName`, `ParentDirectorySegment`), per §8.1's "true invariants stay as a named constant WITH a WHY comment". Flagging so the reviewer does not read them as unlisted magic values. |
| 9 | "No file over ~100 lines." | `ProcessPublishJobHandler.cs` is **106** lines and `RegenerateMarketplaceHandlerTests.cs` is **137**. | Left both. The handler is the plan's 14-step orchestrator specified as one method with a single extracted `FailAsync` helper; further extraction would only move lines. The test class carries the six behaviours the plan assigned to that one class name, and the length is NSubstitute matcher verbosity; splitting would require inventing a test class name the plan does not contain. Declared rather than silently split. |
| 10 | Orchestrator brief: "The solution file is `api/E3A.slnx` (capital A)." | Git tracks it as **`api/E3a.slnx`** (lower-case `a`); Windows resolves either. | Edited in place — no rename is staged (`git status` shows ` M api/E3a.slnx`). Worth a follow-up decision by the dev, but out of scope here. |
| 11 | Global contract: Postman stays in sync with every endpoint change. | This pass adds `POST /api/engineers/{id}/publish` but the orchestrator explicitly assigned Postman to pass 2 ("no Postman"). | Followed the orchestrator's split. **`postman/e3a.postman_collection.json` is currently out of sync with the branch and must be fixed by pass 2 before review.** Listed below. |

Nothing else in `api/core-libraries/` was touched.

## Build & test

Commands run from `D:\Personal\_e3a`:

```
dotnet build api/E3A.slnx
    9 Warning(s)
    0 Error(s)
```
All 9 warnings are the pre-existing `core-libraries` ones (`Core.Validation` ×2, `Core.OTP` ×2, `Core.Notifications` ×5) — identical to the baseline. **Zero new warnings**, `TreatWarningsAsErrors` is on.

```
dotnet test api/E3A.Tests/E3A.Tests.csproj
Passed!  - Failed: 0, Passed: 324, Skipped: 0, Total: 324, Duration: 426 ms
```
Baseline was 236/236. +88 test cases = the 64 planned pass-1 test methods, of which five are `[Theory]` (15 + 4 + 5 + 3 + 2 rows).

Migration generated with the tool, not hand-written:
```
dotnet tool install --global dotnet-ef --version 10.0.5      (was not installed; installed successfully)
dotnet ef migrations add versions002 --project api/E3A.Infrastructure --startup-project api/E3A.Api
Build succeeded. ... Done.
```
`20260828115939_versions002.cs` creates `ItemVersions` with `IX_ItemVersions_ItemId` and the unique
`IX_ItemVersions_ItemType_ItemId_VersionNumber` filtered on `[IsDeleted] = 0`. The migration was **not**
applied to a database.

`E3A.Jobs` compiles as part of `api/E3A.slnx`; it was not run against a Functions host or a real queue.

## Notes for review

1. **Deviation #1 is the one I am least sure about.** Adding a line to `api/.editorconfig` is outside the plan's touched-file list. I judged it better than renaming a type the plan and two test classes name explicitly, but it is a judgement call and easy to reverse.
2. **`DeterministicZipper` determinism is genuinely proven.** Test 24 reverses the input list and asserts byte-for-byte equality of the archive plus equal sha256; test 23 asserts repeat-invocation equality. Both pass. The 1980-01-01 stamp is a `static readonly DateTimeOffset` (a `DateTimeOffset` cannot be `const`) with the WHY comment the plan asked for.
3. **`ProcessPublishJobHandler` save counts are asserted, not assumed**: 2 on the success path (test 64), 1 when resuming from `Building` (test 68), 1 when the engineer is missing (test 71, before the checkpoint), 2 on the snapshot-empty failure (test 72). No path exceeds two.
4. **Queue payload round-trip is untested.** `StorageQueueClient` serializes with default `JsonSerializer` options → `{"VersionId":"…"}` (PascalCase). The isolated-worker `QueueTrigger` binder deserializes case-insensitively by default, so this should bind, but nothing in this repo proves it and Functions are out of test scope. Worth one manual smoke test before the first real publish.
5. **`RegenerateMarketplaceHandler` unlist exclusion** (test 75) asserts on the *predicate itself* — it compiles the `Expression<Func<Engineer,bool>>` passed to `FindPaginatedAsync` and proves it accepts a published engineer and rejects an unlisted one. That is stronger than only checking the substituted page contents, which would be tautological.
6. **`ItemVersion.Id` is publicly settable** (`Core.DDD.Entity.Id { get; set; }`). I did not use that; `RegenerateMarketplaceHandlerTests` links engineer→version by calling `engineer.MarkPublished(version.Id)`, so no reflection and no property poking, per `conventions/dotnet-testing.md` §4.
7. **`DraftSnapshotFreezer` deletes the snapshot prefix before listing drafts**, so a retry of the same version cannot collide with the no-overwrite default of the existing 6-arg `UploadAsync`. The plan describes the same behaviour in a different sentence order; sequencing is irrelevant (different containers).
8. **`appsettings.json` is git-ignored**, so the new `Publishing` section and `Azure:PublishQueueName` will not show in the PR diff. They are present locally and are required for the app to start. Someone must add them to the deployed configuration / Azure App Configuration.
9. **Tests 7–8 (`Engineers/EngineerListingTests`) are not written** even though `Engineer.Unlist()`/`Relist()` ship in this pass — the orchestrator assigned those two tests to pass 2. The methods are therefore currently untested domain code on this branch.

## What pass 2 still owes

Production:
1. **Step 7** — `api/E3A.Application/Publishing/GetPublishStatus/`: `GetPublishStatusQuery.cs` (#39), `GetPublishStatusQueryValidator.cs` (#40), `GetPublishStatusQueryHandler.cs` (#41); and `api/E3A.Api/Controllers/Publishing/PublishController.cs` (#45).
2. **Step 8** — `api/E3A.Application/Engineers/UnlistEngineer/` (#27–29) and `.../RelistEngineer/` (#30–32); `UnlistEngineer` / `RelistEngineer` actions on `EngineersController`.
3. **Step 9** — `postman/e3a.postman_collection.json`: "Publish Engineer" (**already owed by pass 1's endpoint — do this one first**), "Unlist Engineer", "Relist Engineer" in the `Engineers` folder, plus a new `Publishing` folder with "Get Publish Status".
4. **Step 9** — docs sync exactly as the plan's "Docs sync" section specifies: `docs/architecture.md`, `docs/implementation-plan.md`, `docs/plugin-spec.md`. `docs/security-scan.md` stays untouched.

Tests: **7–8** (`Engineers/EngineerListingTests`), **47–59** (unlist/relist validators + handlers), **80–87** (`GetPublishStatus` validator + handler).

Everything those slices depend on already exists and is green: `ErrorCodes.EngineerNotPublished` /
`EngineerNotUnlisted` / `PublishVersionNotFound` / `PublishVersionIdRequired` / `PublishAlreadyInProgress`
are declared and present in both resx files; `Engineer.Unlist()` / `Relist()` and `EngineerStatus.Unlisted`
exist; `PublishStatusResultGenerator`, `IItemVersionRepository`, `IUserRepository` and
`RegenerateMarketplaceCommand` (which the unlist/relist handlers must `ISender.Send`) are all wired and
registered in DI.

---

## Pass 2 — build steps 7–9

Scope executed: plan build steps **7, 8, 9** and test-plan items **7–8, 47–59, 80–87**.
Nothing from pass 1 was modified.

### Files created

| Path | Lines | Purpose |
|---|---|---|
| `api/E3A.Application/Publishing/GetPublishStatus/GetPublishStatusQuery.cs` | 6 | #39 |
| `api/E3A.Application/Publishing/GetPublishStatus/GetPublishStatusQueryValidator.cs` | 13 | #40 |
| `api/E3A.Application/Publishing/GetPublishStatus/GetPublishStatusQueryHandler.cs` | 45 | #41 — user → version → engineer → owner guards, no save |
| `api/E3A.Api/Controllers/Publishing/PublishController.cs` | 19 | #45 — `GET api/publish/{versionId:guid}/status` |
| `api/E3A.Application/Engineers/UnlistEngineer/UnlistEngineerCommand.cs` | 6 | #27 |
| `api/E3A.Application/Engineers/UnlistEngineer/UnlistEngineerValidator.cs` | 13 | #28 |
| `api/E3A.Application/Engineers/UnlistEngineer/UnlistEngineerHandler.cs` | 55 | #29 — 5 guards, one save, then `ISender.Send(RegenerateMarketplaceCommand)` (D21) |
| `api/E3A.Application/Engineers/RelistEngineer/RelistEngineerCommand.cs` | 6 | #30 |
| `api/E3A.Application/Engineers/RelistEngineer/RelistEngineerValidator.cs` | 13 | #31 |
| `api/E3A.Application/Engineers/RelistEngineer/RelistEngineerHandler.cs` | 55 | #32 |

Tests created (all under `api/E3A.Tests/`):

| Path | Lines | Test-plan items |
|---|---|---|
| `Engineers/EngineerListingTests.cs` | 35 | 7–8 |
| `Engineers/UnlistEngineer/UnlistEngineerValidatorTests.cs` | 28 | 47–48 |
| `Engineers/UnlistEngineer/UnlistEngineerHandlerTests.cs` | 101 | 49–54 |
| `Engineers/RelistEngineer/RelistEngineerValidatorTests.cs` | 28 | 55–56 |
| `Engineers/RelistEngineer/RelistEngineerHandlerTests.cs` | 74 | 57–59 |
| `Publishing/GetPublishStatus/GetPublishStatusQueryValidatorTests.cs` | 28 | 80–81 |
| `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests.cs` | 94 | 82–87 |

All 23 planned pass-2 test methods exist with the exact names from the plan.

### Files modified

| Path | Change |
|---|---|
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | +`UnlistEngineer` / `RelistEngineer` actions (+2 usings). File is now exactly 100 lines. |
| `postman/e3a.postman_collection.json` | +"Publish Engineer", "Unlist Engineer", "Relist Engineer" in `Engineers` (between "Get Import Manifest" and "Delete Engineer"); + new `Publishing` folder with "Get Publish Status". All four inherit the collection-level bearer auth, matching every other authenticated request. JSON re-parsed clean. |
| `docs/architecture.md` | Diagram: `BackgroundService` box replaced by a sibling `E3A.Jobs (Azure Functions v4 isolated, .NET 10) ◄── Storage Queue publish-jobs`. "Reads never hit the API" bullet now states the cache-header policy instead of purging. "Publish pipeline" section rewritten to the implemented step order with the scanner marked as the next slice; "purge Cloudflare cache" deleted; poison after `maxDequeueCount` (5). "Backend style" adds `E3A.Jobs`, drops `Cloudflare purger`, and notes the plugin builder / marketplace generator live in `E3A.Application/Publishing/Shared`. |
| `docs/implementation-plan.md` | Locked-stack line → isolated Functions worker on queue `publish-jobs`, Cloudflare reduced to CDN/rate-limit. Decision 3 → cache-header policy, "No cache purge step." `versions` row → `SemanticVersion`, `FailureReason`, `ScanReportJson` deferred to the scanner slice. `engineers`/`teams` row → `Status(Draft|Published|Unlisted|Deleted)`. API surface → `POST {id}/unlist`, `POST {id}/relist`, `GET /publish/{versionId}/status` (owner-only). Publish-pipeline line rewritten, purge removed. P3 bullet records the `publish-pipeline` → `security-scan` split and that the scanner must land before the first real publish. |
| `docs/plugin-spec.md` | `marketplace.json` section now shows the D18 wrapper (`name` / `owner` / `plugins`), notes unlisted engineers drop out of the root document while zips and pinned marketplaces keep resolving, and that the pinned per-version file uses the identical wrapper. New attribution paragraph: `author.name` = Identity `UserName` with the engineer-slug fallback, `author.url` = `https://<domain>/e/{slug}`, GitHub URL arrives with the OAuth slice. Plugin-layout line's stale `author { name: "@login", url }` comment corrected (see Deviations #2). |
| `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` | +`Unlisted(...)` factory built from `Published(...)` + `Unlist()` (see Deviations #1). |

`docs/security-scan.md` untouched, as instructed. Nothing under `api/core-libraries/` touched.

### Deviations

| # | Plan said | Reality | What I did |
|---|---|---|---|
| 1 | The test plan names shared factories only under `Publishing/Shared`. | Tests 8 and 57–59 need an `Unlisted` engineer, and `conventions/dotnet-testing.md` §4 forbids `new`-ing entities in tests and prefers `[Entity]Factory`. | Added one method, `EngineerFactory.Unlisted(...)`, to the existing test factory — no new file, no reflection, built purely through `Published()` + `Unlist()`. Mirrors pass 1's `PluginFileFactory.ConvertingManifest` precedent. |
| 2 | Docs sync lists two `plugin-spec.md` bullets (wrapper shape, attribution). | Line 60 of the same file also documented the old attribution shape — `.claude-plugin/plugin.json # … author { name: "@login", url }` — which contradicts the paragraph the plan told me to add. Leaving it would be exactly the divergence `.claude/rules/docs-sync.md` calls blocking. | Changed that one comment to `author { name, url } — see marketplace.json`. One line beyond the plan's letter, inside the plan's intent and inside a file the plan authorises me to touch. |
| 3 | Skill: "no file over ~100 lines." | `UnlistEngineerHandlerTests.cs` is **101** lines. | Left it. The plan pins the class name and assigns all six behaviours (49–54) to it, so splitting would require inventing a class name the plan does not contain. I compacted the `Act()` helper to expression-bodied form to get from 104 to 101. Declared rather than silently split. |

### Defect found in pass 1's code — declared, not changed

**`POST /api/engineers/{id}/publish` cannot accept the body contract the plan specifies.**

The plan (Scope item 2, and the API-surface table) says the body is `{ "increment": "Patch|Minor|Major" }`.
`api/E3A.Api/Program.cs:78-81` registers `JsonStringEnumConverter` via `ConfigureHttpJsonOptions`, which
configures `Microsoft.AspNetCore.Http.Json.JsonOptions` — that type is read by **minimal APIs only**.
MVC controllers read `Microsoft.AspNetCore.Mvc.JsonOptions`, and `Program.cs:35` calls a bare
`AddControllers()` with no `.AddJsonOptions(...)`. `VersionIncrement` carries no `[JsonConverter]`.

I verified this empirically rather than assuming it, deserialising the exact request record shape under
`new JsonSerializerOptions(JsonSerializerDefaults.Web)`:

```
{"increment":"Patch"}  -> JsonException
{"increment":0}        -> Patch
```

So the shipped endpoint accepts only the numeric form today. I did **not** fix it: `Program.cs` is not in
the plan's "Existing code touched" list, and the brief forbids revisiting pass 1's code. The one-line fix is
`AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));`.
Note it would also change the *response* shape of every enum-valued field across the whole API, so it is a
contract decision for the dev, not a silent patch — which is precisely why I left it.

Consequence for Postman: the "Publish Engineer" request ships the body that actually works today
(`{ "increment": 0 }`) plus a one-line `description` recording the enum mapping and the reason. Postman
mirrors the implemented endpoint, not the intended one. **When the converter is registered, flip that body
to `{ "increment": "Patch" }` and drop the description.**

### Build & test

Commands run from `D:\Personal\_e3a`:

```
dotnet build api/E3a.slnx
    9 Warning(s)
    0 Error(s)
```
The same 9 pre-existing `core-libraries` warnings as the verified baseline. Zero new warnings
(`TreatWarningsAsErrors` is on).

```
dotnet test api/E3A.Tests/E3A.Tests.csproj
Passed!  - Failed: 0, Passed: 347, Skipped: 0, Total: 347, Duration: 505 ms
```
Baseline was 324/324. **+23 = exactly the 23 planned pass-2 test methods** (none are `[Theory]`).

Postman JSON re-parsed after editing — parses clean; inventory verified as Engineers (11 requests),
Catalog (3), Publishing (1).

Not run: no database, no Functions host, no real queue or blob account were involved.

### Notes for review

1. **The pass-1 defect above is the most important thing on this page.** It is a real, reproducible
   contract break on an endpoint that is already on the branch, and it is the only reason the Postman
   body is numeric.
2. **`ISender` vs `IMediator` in the unlist/relist handlers.** The plan says `ISender`; I used `ISender`.
   Overload resolution binds `sender.Send(new RegenerateMarketplaceCommand(), cancellationToken)` to
   `Send<TRequest>(TRequest, CancellationToken) where TRequest : IRequest`, which is what the tests
   verify with `Received(1).Send(Arg.Any<RegenerateMarketplaceCommand>(), Arg.Any<CancellationToken>())`.
3. **Guard order is asserted, not assumed.** Both unlist and relist tests prove the conflict guard fires
   before the status guard: the conflict test leaves the engineer in its valid status and only stubs an
   in-progress version. Every throwing path asserts both `DidNotReceive().SaveChangesAsync` **and**
   `DidNotReceive().Send(...)`, so a failed guard can never leave the marketplace regenerated.
4. **`RelistEngineerHandlerTests` has no unauthenticated / not-found / not-owner tests** — the plan
   assigns tests 57–59 only (happy path, wrong status, conflict). Those three guards are identical code
   to `UnlistEngineerHandler` and are covered there. Flagging so the gap reads as deliberate.
5. **`GetPublishStatus` performs zero `SaveChangesAsync`** — it is a pure read, so tests 82–87 carry no
   "no save" assertion; the handler holds no write surface at all.
6. **`GetPublishStatusQueryHandler` authorises through the engineer, not the version.** `ItemVersion` has
   no `OwnerUserId` (it is polymorphic by `(ItemType, ItemId)`), so ownership is resolved by loading the
   engineer. A version whose engineer is gone therefore returns 404 `ENGINEER_NOT_FOUND`, which is what
   test 86 pins.
7. **Docs**: `docs/design-prompt.md` deliberately untouched. Unlist/relist have no UI in this slice
   (frontend publish UI is explicitly out of scope), so that is incompleteness, not divergence.

---

## Pass 3 — enum body binding fix

Scope executed: the single defect pass 2 declared. No new feature work, no refactoring, no new files.

### Files modified

| Path | Change |
|---|---|
| `api/E3A.Api/Program.cs` | Line 35: `AddControllers()` → `AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));`. The `ConfigureHttpJsonOptions` block at :78 was **left in place** — both registrations are live, they serve different pipelines. `using System.Text.Json.Serialization;` was already present (line 21), so no using change. |
| `postman/e3a.postman_collection.json` | "Publish Engineer" body `{"increment": 0}` → `{"increment": "Patch"}`; the `description` explaining the numeric workaround removed. No other request touched. |

No files created. No files deleted. No test changed.

### Blast radius — verified independently before implementing

I did not take the scoping on trust. Grepping every `.cs` under `api/` for `EngineerStatus`, `VersionIncrement`,
`ItemVersionStatus`, `ItemType` and `CatalogSort` confirms:

- `PublishEngineerRequest.Increment` is the only enum-typed property in any JSON request or response body.
- `CatalogSort` occurs exactly once outside tests — `CatalogController.GetCatalog(... [FromQuery] CatalogSort sort ...)`.
  Query-string binding is done by `ModelBinder`, not the JSON input formatter, so `?sort=Newest` is unaffected either way.
- No result record exposes an enum: `PublishStatusResult.Status` and `EngineerResult.Status` are `string`
  (`version.Status.ToString()` / `nameof(...)`), which the existing tests pin.

So the change alters exactly one property's wire contract, as described.

### Empirical verification — method and observations

**Method.** Starting the API and POSTing would have proved nothing: `POST /api/engineers/{id}/publish` sits behind
`[Authorize]`, so the 401 fires before model binding is ever reached. Instead I exercised the exact component that
performs controller body binding — `SystemTextJsonInputFormatter`, pulled from the real
`IOptions<MvcOptions>.Value.InputFormatters` of a built host — against the **real** `PublishEngineerRequest` type
(project-referenced from `E3A.Api`, not re-declared). I ran it twice in one process: a **control** with bare
`AddControllers()` (the pre-fix line) and the **fixed** registration, so the delta is attributable to the change
rather than to STJ web defaults. Harness lives outside the repo, in the session scratchpad; nothing was added to
the working tree.

```
=== CONTROL: bare AddControllers() (pre-fix Program.cs:35) ===
Mvc.JsonOptions contains JsonStringEnumConverter : False
  {"increment":"Patch"}    -> REJECTED (JSON value could not be converted ... Path: $.increment)
  {"increment":"Minor"}    -> REJECTED
  {"increment":"Major"}    -> REJECTED
  {"increment":"Nonsense"} -> REJECTED
  {"increment":0}          -> BOUND Increment=Patch (numeric 0)
  {}                       -> REJECTED (missing required properties including: 'increment')

=== FIXED: AddControllers().AddJsonOptions(+JsonStringEnumConverter) ===
Mvc.JsonOptions contains JsonStringEnumConverter : True
  {"increment":"Patch"}    -> BOUND Increment=Patch (numeric 0) [enum defined: True]
  {"increment":"Minor"}    -> BOUND Increment=Minor (numeric 1) [enum defined: True]
  {"increment":"Major"}    -> BOUND Increment=Major (numeric 2) [enum defined: True]
  {"increment":"Nonsense"} -> REJECTED (JSON value could not be converted ... Path: $.increment)
  {"increment":0}          -> BOUND Increment=Patch (numeric 0)
  {}                       -> REJECTED (missing required properties including: 'increment')
```

Against the three things I was asked to confirm:

1. `{"increment": "Patch"}` now binds. **Confirmed.**
2. `{"increment": "Major"}` binds to `Major` — numeric `2`, `Enum.IsDefined` true. It does **not** silently land on
   the first enum member. **Confirmed**, and checked for `Minor` (1) too, so this is not an accident of ordering.
3. `{"increment": "Nonsense"}` is **rejected**, not swallowed into a default `Patch`. **Confirmed.** The converter
   validates rather than falls back, which was the stated worse-than-the-bug outcome.

Two facts beyond the brief, both worth knowing:

- `{"increment": 0}` still binds. `JsonStringEnumConverter` accepts numeric input as well as names, so the change is
  **backward compatible** — any client already sending the numeric workaround keeps working.
- `{}` is still rejected for a missing `increment`. The `[property: JsonRequired]` under-posting guard from pass 1's
  deviation #6 survives the converter, so a missing field is still a 400 and not a silent `Patch`.

The minimal-API side was not re-verified empirically; the `ConfigureHttpJsonOptions` block is untouched and
`Mvc.JsonOptions` / `Http.Json.JsonOptions` are independent option types, so `/health` and the Core notification/OTP
endpoint groups keep the registration they already had.

### Build & test

Commands run from `D:\Personal\_e3a`:

```
dotnet build api/E3a.slnx --no-incremental
    9 Warning(s)
    0 Error(s)
Build succeeded.
```

A plain incremental `dotnet build api/E3a.slnx` reports `0 Warning(s)` only because `core-libraries` is not
recompiled; I forced `--no-incremental` to get a comparable number. All 9 are the pre-existing `core-libraries`
warnings and were enumerated to confirm it — `Core.Validation` ×2 (CS8602), `Core.OTP` ×2 (CS8618),
`Core.Notifications` ×5 (CS8618). **Zero new warnings**; `TreatWarningsAsErrors` is on.

```
dotnet test api/E3A.Tests/E3A.Tests.csproj
Passed!  - Failed: 0, Passed: 347, Skipped: 0, Total: 347, Duration: 1 s
```

**347/347 — matches the stated baseline exactly.** No test was added, removed or modified in this pass.

Postman collection re-parsed after editing: parses clean; the "Publish Engineer" request resolves to
`POST {{baseUrl}}/api/engineers/{{engineerId}}/publish`, body `{"increment": "Patch"}`, no description, still
inheriting collection-level bearer auth like every other authenticated request.

Not run: no database, no Functions host, no real queue or blob account.

### Deviations

`None.`

### Notes for review

1. **The pass-2 defect note is now stale in one respect.** Pass 2 wrote that the fix "would also change the response
   shape of every enum-valued field across the whole API." That framing was cautious but, on the evidence above,
   inaccurate — no result record exposes an enum, so no response shape changes at all. I left pass 2's text as
   written (it is a historical record) and am recording the correction here rather than editing it.
2. **Both JSON registrations are now live and they are not redundant.** `Mvc.JsonOptions` (new) serves controllers;
   `Http.Json.JsonOptions` (existing, :78) serves the minimal-API endpoints. Deleting either would silently break
   one half. Worth a glance from the reviewer so the apparent duplication does not read as an oversight.
3. **No `.editorconfig` or analyzer change was needed** — the one-line registration compiles clean under
   `TreatWarningsAsErrors`.
4. **`VersionIncrement` still carries no `[JsonConverter]` attribute.** The behaviour is entirely from the global
   converter. If anyone later removes the `AddJsonOptions` call, this endpoint regresses silently to numeric-only
   and no test catches it — controller/DI wiring is out of test scope per `conventions/dotnet-testing.md` §5. That
   is a known, accepted gap, not something I worked around.

---

## Pass 4 — N4 upload race + N2 determinism test

Scope: exactly the two review follow-ups the orchestrator selected (N4, N2). N1, N3, N5–N9 untouched.
No change to `Program.cs`, `/docs`, Postman or `api/core-libraries/`.

### Files created

| Path | Lines | Purpose |
|---|---|---|
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadEngineerDraftHandlerPublishGuardTests.cs` | 95 | The four N4 tests: conflict on `Queued`, conflict on `Building`, success with no versions, success with only terminal versions. |

### Files modified

| Path | Change |
|---|---|
| `api/E3A.Application/Engineers/UploadEngineerDraft/UploadEngineerDraftHandler.cs` | Takes `IItemVersionRepository` (second ctor parameter, mirroring `PublishEngineerHandler`); in-flight publish guard added after the owner check and before any blob or options work, throwing `ConflictCoreException(ErrorCodes.PublishAlreadyInProgress)`. Predicate copied verbatim from `PublishEngineerHandler.cs:41`. 71 lines. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadEngineerDraftHandlerTests.cs` | Constructor wiring only — new substitute field, sixth ctor argument. No assertion changed. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadEngineerDraftHandlerGuardTests.cs` | Constructor wiring only — same. No assertion changed. |
| `api/E3A.Tests/Publishing/Shared/DeterministicZipperTests.cs` | Test 23 strengthened: after the existing byte/hash equality assertions it now opens the archive and asserts every entry's `LastWriteTime` is the 1980 epoch. Test 24 untouched. 72 lines. |

No new error code, resx key, DI registration or endpoint. `IItemVersionRepository` was already registered
(`E3A.Infrastructure/DependencyInjection.cs:16`) and the handler is MediatR-resolved, so no wiring change was needed.

### Fix 1 — N4 detail

The guard sits between the `EngineerNotOwned` check and `var options = uploadsOptions.Value;`. A rejected upload
therefore never calls `OpenReadStream`, never calls `DeleteByPrefixAsync`/`UploadAsync`, never mutates the engineer
and never calls `SaveChangesAsync` — all four asserted by `AssertStorageAndDatabaseUntouched()` in the new tests.
`SaveChangesAsync` remains exactly once, in the handler, on the success path only.

The four tests drive one substitute stub that **compiles the predicate the handler actually passes** and applies it
to an in-memory `List<ItemVersion>` (same technique as `RegenerateMarketplaceHandlerTests` test 75). That makes the
terminal-versions test meaningful: `Published` and `Failed` versions for the same engineer are present in the list
and the real predicate rejects them, rather than the test merely relying on a substitute's default `null`.

### Fix 2 — N2 detail, and proof that it bites

The assertion compares wall-clock components (`Year`, `DayOfYear`, `TimeOfDay`) against the
`DeterministicTimestamp` constant rather than using `DateTimeOffset` equality. **The exact assertion N2 suggested
does not work**, and I confirmed that empirically before changing it: with
`archive.Entries.Should().OnlyContain(x => x.LastWriteTime == new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero))`
the test failed on this machine —

```
Expected archive.Entries to contain only items matching (x.LastWriteTime == DeterministicZipperTests.ExpectedTimestamp),
but {.claude-plugin/plugin.json, agents/reviewer.md, commands/ship.md, skills/house-rules/SKILL.md} do(es) not match.
```

Zip entries carry MS-DOS timestamps with no timezone, so `ZipArchiveEntry.LastWriteTime` reads back as
`1980-01-01T00:00:00` at the **machine's local offset**. A `DateTimeOffset` equality against `+00:00` therefore
passes only on a UTC machine and fails everywhere else — it would have been a CI-vs-laptop flake. The component
comparison is offset-independent and still binds to the constant.

Proof the strengthened test actually bites: I temporarily changed `DeterministicZipper.cs:12` to
`= DateTimeOffset.UtcNow;` and ran the class:

```
Failed E3A.Tests.Publishing.Shared.DeterministicZipperTests.Create_ShouldProduceIdenticalBytesAndHash_WhenCalledTwiceWithSameInput
Failed! - Failed: 1, Passed: 3, Skipped: 0, Total: 4
```

Two things worth recording from that run. The strengthened test failed, as intended. And the **other three zipper
tests all still passed** under a wall clock — including test 24 — which confirms N2's diagnosis exactly: before this
change nothing in the suite pinned the epoch. (The field is `static readonly`, so both `Create` calls in test 23 saw
the same value regardless of granularity; the 2-second-bucket argument was not even needed.)
`DeterministicZipper.cs:12` was restored to `new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)` and re-verified after the run.

### Build & test

```
dotnet build api/E3a.slnx --no-incremental
Build succeeded.
    9 Warning(s)
    0 Error(s)
```

All 9 warnings are the pre-existing `core-libraries` set (`Core.Validation` x2 CS8602, `Core.OTP` x2 CS8618,
`Core.Notifications` x5 CS8618). Zero new warnings.

```
dotnet test api/E3A.Tests/E3A.Tests.csproj
Passed!  - Failed: 0, Passed: 351, Skipped: 0, Total: 351, Duration: 1 s
```

**351/351** — the 347 baseline plus the four new N4 tests. No pre-existing test was disabled, renamed or weakened;
the two edits to existing test files are constructor wiring only.

Not run: no database, no Functions host, no real queue or blob account.

### Deviations

| Plan said | Reality | What I did |
|---|---|---|
| N2's suggested assertion: `archive.Entries[0].LastWriteTime.Should().Be(new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero))` | Zip stores an MS-DOS timestamp with no timezone, so `LastWriteTime` reads back at the machine's local offset; the equality fails on any non-UTC machine (observed failing here). | Asserted the offset-independent wall-clock components (`Year` / `DayOfYear` / `TimeOfDay`) against the same 1980 constant, and across **all** entries rather than only `Entries[0]`. Same guarantee, no timezone dependence. |
| "Add them to the existing guard-test file if one fits; otherwise a sibling file." | `UploadEngineerDraftHandlerGuardTests.cs` is already 84 lines; four tests plus the shared stub would have pushed it past 125. | New sibling `UploadEngineerDraftHandlerPublishGuardTests.cs` (95 lines), matching the folder's existing Tests/GuardTests split and the ~100-line rule. |

### Notes for review

1. **Two existing test files were modified beyond the stated scope, unavoidably** — adding a constructor parameter to
   `UploadEngineerDraftHandler` breaks every direct `new` of it. Both edits are one field plus one argument; no
   assertion, name or fixture changed.
2. **The guard is read-only with respect to the version table.** It is a `FirstOrDefaultAsync`, not a lock — two
   concurrent requests could still both pass it. That is the same window `PublishEngineerHandler`,
   `UnlistEngineerHandler` and `RelistEngineerHandler` already have, and closing it properly needs the unique
   filtered index plus a caught constraint violation. Out of scope; recorded because N4 is a race and this fix
   narrows it rather than eliminating it.
3. **No docs change.** Grepped `/docs` for the upload/publish conflict semantics — nothing there describes whether
   an upload is permitted during a publish, so there is no stale statement to diverge from. Per
   `.claude/rules/docs-sync.md` this is incompleteness, not divergence. Postman is likewise unchanged: no URL,
   method, auth mode or request body changed; the upload endpoint simply gains a `409` failure mode.
4. **`ErrorCodes.PublishAlreadyInProgress` is now raised from four handlers.** Its resx wording reads correctly for
   the upload case too, so nothing was touched in either resx file.
