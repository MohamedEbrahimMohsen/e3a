VERDICT: APPROVED

# Review — Publish Pipeline

Reviewed the working tree on `feature/publish-pipeline` against `main` @ `ba2c824`. Note: the branch
has no commits yet — `HEAD == main` — so every diff below was taken with `git diff main -- <path>`
against the uncommitted tree. Read `01-plan.md`, all three passes of `02-implementation.md`,
`00-acceptance.md`, the vendored `.claude/skills/dotnet-feature/SKILL.md`, `conventions/dotnet-testing.md`
and `.claude/rules/docs-sync.md`, then every changed and added file end to end.

**No blocking findings.** The non-blocking follow-ups below are worth writing down while the dev is
away, but none of them gates the slice.

## Blocking

None.

## Non-blocking

### N1. `RelistEngineerHandler` has three untested `throw` branches
`api/E3A.Application/Engineers/RelistEngineer/RelistEngineerHandler.cs:20` (unauthenticated), `:27`
(not found), `:32` (not owner). `conventions/dotnet-testing.md` §9 asks for a test per throw branch;
the plan's test table assigns only rows 57–59 to `RelistEngineerHandlerTests`, and pass 2 flagged the
gap deliberately (note #4). The three guards are byte-identical to `UnlistEngineerHandler.cs:18/25/30`,
which are fully covered by tests 50–52, so nothing is wrong today — but a future copy-paste divergence
in Relist would go unnoticed. Three tests mirroring `UnlistEngineerHandlerTests` close it.

### N2. No test pins `DeterministicZipper`'s fixed 1980 stamp
`api/E3A.Application/Publishing/Shared/DeterministicZipper.cs:12,23`. Test 24
(`api/E3A.Tests/Publishing/Shared/DeterministicZipperTests.cs:25`) genuinely proves order-independence:
reversing the input and asserting byte equality would fail immediately if the `OrderBy(..., Ordinal)`
were dropped. But test 23 (`:15`) would **still pass** if `DeterministicTimestamp` were replaced by
`DateTimeOffset.UtcNow` — zip entries carry MS-DOS timestamps at 2-second granularity, so two calls in
one test almost always land in the same bucket. Cross-run determinism is what D4 actually depends on: a
retry minutes later producing a different sha256 would record a hash that does not match the
already-uploaded blob, and `/plugin install` would fail integrity verification. One assertion inside the
existing round-trip test — `archive.Entries[0].LastWriteTime.Should().Be(new DateTimeOffset(1980,1,1,0,0,0,TimeSpan.Zero))`
— closes it.

### N3. `ProcessPublishJobHandler`'s `manifest == null` branch is untested
`api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:60-64` marks the version
`Failed` with `ErrorCodes.EngineerDraftNotUploaded` and returns. It is reachable (a `FrozenManifestJson`
of literal `"null"`), and the plan's rows 71–73 cover only engineer-missing / snapshot-empty /
structure-invalid. Same shape as the covered branches, so a gap rather than a defect.

### N4. Draft blobs are not frozen against concurrent re-upload while a version is `Building`
`UploadEngineerDraftHandler.cs:48-56` deletes and rewrites `drafts/{owner}/{engineer}/**` with no
in-progress-publish guard, while `DraftSnapshotFreezer.cs:15-20` reads those blobs **live** at build
time against a manifest frozen at request time. Concrete sequence: creator publishes (manifest frozen),
worker enters `Building`, creator re-uploads a different `.claude` folder, worker freezes the new blobs.
Best case validation fails with `PLUGIN_MANIFEST_ASSET_MISSING`; worse case the paths still match and
e3a publishes content the creator never saw in the manifest shown to them. A narrower variant also
weakens D4: if attempt 1 uploads the zip and then fails, and the draft changes before the retry,
`ListByPrefixAsync` finds the old blob, the upload is skipped, and `MarkPublished` records the sha256 of
the *new* bytes against the *old* blob. Cheapest fix is the guard the publish/unlist/relist handlers
already carry — reject `POST {id}/upload` while a version is `Queued`/`Building`.

### N5. Publishing a new version silently re-lists an unlisted engineer
`ProcessPublishJobHandler.cs:89` calls `engineer.MarkPublished(version.Id)`, and `Engineer.cs:52-57`
sets `Status = Published` unconditionally. So `Unlist()` → publish v2 → the engineer is back in the root
marketplace without an explicit relist. Neither the plan nor `00-acceptance.md` decision #3 says which
way this should go; it may well be what you want. Worth a deliberate decision before P4.

### N6. The publish file cap and the upload file cap are both 400, but publish adds a file
`PluginStructureValidator.cs:43` compares `files.Count` — which includes the generated
`.claude-plugin/plugin.json` added at `PluginTreeAssembler.cs:14` — against `MaxPluginFileCount` (400,
the same value as `Uploads:MaxFileCount`). A draft whose manifest imports exactly 400 assets uploads
fine and then fails at publish with `PLUGIN_TOO_MANY_FILES`. Both are config values, so this is a
tuning note rather than a code change.

### N7. Two config keys must agree with nothing in code linking them
`AzureOptions.StorageAccountQueueUrl` (used by `PublishRequestedEventHandler.cs:16` and, per
`Core.Azure/Clients/StorageQueueClient.cs:20`, a *per-queue* URI) and `AzureOptions.PublishQueueName`
(used only by the trigger binding at `ProcessPublishJobFunction.cs:15`). They agree on disk
(`appsettings.json:17,21` — both `publish-jobs`), but in another environment a mismatch means the
producer writes to a queue nobody reads, with no error anywhere. Composing the queue URL from
`StorageAccountQueueUrl` + `PublishQueueName` would make the pair impossible to desynchronise. Related:
with the `Publishing` section absent entirely, `MaxPluginFileCount`/`MaxPluginBytes` bind to `0` and the
worker fails every publish closed (safe), while `MarketplacePageSize` binds to `0` and
`RegenerateMarketplaceHandler.cs:27` would page with size `0`. This is the "options failing open when
config keys are absent" debt that `00-acceptance.md` explicitly excludes from this slice, so it is
recorded, not charged.

### N8. Small duplication
`$"{options.PublicSiteUrl.TrimEnd('/')}/e/{engineer.Slug}"` is written twice —
`PluginJsonGenerator.cs:14` and `MarketplaceDocumentGenerator.cs:14`. Every other path in this slice
lives in `PublishBlobPaths`; a `CatalogUrl(publicSiteUrl, slug)` there would keep the pattern.

### N9. Files over the ~100-line guidance
`ProcessPublishJobHandler.cs` (106), `RegenerateMarketplaceHandlerTests.cs` (137),
`ProcessPublishJobHandlerTests.cs` (102), `UnlistEngineerHandlerTests.cs` (101). Three were declared
(pass 1 deviation #9, pass 2 deviation #3); `ProcessPublishJobHandlerTests.cs` was not. All sit within
or near the skill's "~80–100", and splitting would require inventing class names the plan does not
contain. Recorded, not asked for.

## Verified

### Claims from `02-implementation.md` I independently confirmed

- **`dotnet build api/E3a.slnx --no-incremental` → 0 errors, 9 warnings.** Re-run. All 9 are the
  pre-existing `core-libraries` set (`Core.Validation` ×2 CS8602, `Core.OTP` ×2 CS8618,
  `Core.Notifications` ×5 CS8618). Zero new warnings; `E3A.Jobs` builds inside the solution.
- **`dotnet test api/E3A.Tests/E3A.Tests.csproj` → Failed: 0, Passed: 347, Skipped: 0.** Re-run, matches
  the claimed baseline exactly.
- **All 87 test-plan rows exist with the exact class and method names.** Machine-checked: extracted every
  `| n | Class | Method |` row from `01-plan.md` and matched it against every test method declared under
  `api/E3A.Tests/`. Zero misses.
- **All 49 "Files to create" exist and nothing extra was created.** `api/E3A.Jobs/` contains exactly
  `E3A.Jobs.csproj`, `Program.cs`, `host.json`, `Functions/ProcessPublishJobFunction.cs`, as the DoD
  requires. The only additions beyond the plan are the three declared ones — the `PublishingOptionsFactory`
  test factory, `PluginFileFactory.ConvertingManifest`, `EngineerFactory.Unlisted` — all in test code.
- **`api/core-libraries/` is exactly three additive members.** `git diff main -- api/core-libraries/`
  touches only `StorageBlobClient.cs`: `+using Azure;` (required for `ETag.All`), three interface members
  and three implementations. The two pre-existing signatures and bodies are byte-identical to `main` —
  the diff contains no `-` line inside either.
- **Pass 3's two JSON registrations are both required and neither is redundant.** `Program.cs:35`
  `AddControllers().AddJsonOptions(...)` configures `Microsoft.AspNetCore.Mvc.JsonOptions`, read by the
  MVC input/output formatters — the only path that binds `PublishEngineerRequest`. `Program.cs:78`
  `ConfigureHttpJsonOptions` configures `Microsoft.AspNetCore.Http.Json.JsonOptions`, read by minimal
  APIs; this app has six live minimal-API surfaces (`Program.cs:111-118`:
  `MapCoreDevicesNotificationEndpoints`, `MapCoreFirebaseNotificationEndpoints`,
  `MapCoreUserNotificationEndpoints`, `MapCoreNotificationTemplateEndpoints`, `MapCoreOTPEndpoints`,
  `/health`). Deleting either would silently break one half. The pass-3 evidence supports the claim:
  driving the real `SystemTextJsonInputFormatter` pulled from `IOptions<MvcOptions>.Value.InputFormatters`
  of a built host, against the project-referenced `PublishEngineerRequest`, with a pre-fix control in the
  same process, isolates the delta to the registration rather than to STJ web defaults — which is exactly
  what the claim needs. The one case the harness did not exercise, an out-of-range *numeric*
  (`{"increment": 7}` binds without error under `JsonStringEnumConverter`), is caught downstream by
  `PublishEngineerValidator.cs:12` `IsInEnum()` and pinned by test 37.
- **Pass 1's open question about the queue payload round-trip resolves cleanly.**
  `Core.DDD.Entities.DomainEvent` is `public record DomainEvent() : INotification;` — no members — so
  `StorageQueueClient.cs:21` emits exactly `{"VersionId":"<guid>"}`, base64-encoded
  (`QueueMessageEncoding.Base64`, `:19`), which matches the Functions queue extension's default encoding,
  and the isolated worker's case-insensitive default binding maps it onto the positional record. Still
  worth one manual smoke test before the first real publish, but no defect is visible by inspection.
- **`docs/security-scan.md` is untouched.** `git status` lists only `architecture.md`,
  `implementation-plan.md` and `plugin-spec.md` under `docs/`. Correct — the unbuilt scanner is
  incompleteness, which `.claude/rules/docs-sync.md` says is never a finding. `docs/design-prompt.md` is
  likewise correctly left alone (no publish UI in scope).
- **`api/.editorconfig` (pass 1 deviation #1) is the right call.** The line lands inside the `[*.cs]`
  section (`.editorconfig:255`; the section opens at `:5`), so it actually applies — confirmed by the
  green build with the class still named `PublishRequestedEventHandler`. Of the three options — mute
  CA1711 entirely, rename the type, or allow the suffix — allowing the suffix is the narrowest: it keeps
  the rule live for genuine delegate-suffix mistakes, preserves the type name the plan and test rows
  60–61 pin, and matches the file's own precedent for analyzer/convention conflicts (the CA1716 block
  immediately above at `:249-252`). Approved as written.
- **Pass 2's three deviations and pass 3's `Deviations: None.`** All check out. Pass 2 #1
  (`EngineerFactory.Unlisted`, `EngineerFactory.cs:38-44`) is built purely from `Published()` + `Unlist()`
  — no reflection, per testing §4. Pass 2 #2 (correcting `plugin-spec.md:60`'s stale
  `author { name: "@login", url }`) was necessary; leaving it would have been exactly the divergence the
  docs rule calls blocking. Pass 2 #3 is the line-count note in N9. Pass 3 genuinely changed only
  `Program.cs:35` and one Postman body — `git diff main -- api/E3A.Api/Program.cs` is a single line, and
  no test file differs from pass 2.

### Plan items confirmed present and correct

- **Intent (review order #1).** The Goal is met end to end in code:
  `POST /api/engineers/{id}/publish` → `202` with a `PublishStatusResult`
  (`EngineersController.cs:73-78`) → `PublishRequestedDomainEvent` raised inside `ItemVersion.Create`
  (`ItemVersion.cs:36`, D20) → queue with the configured visibility timeout
  (`PublishRequestedEventHandler.cs:16`) → `[QueueTrigger("%Azure:PublishQueueName%")]`
  (`ProcessPublishJobFunction.cs:15`, D15) → worker → immutable zip at
  `z/{pluginName}/{semanticVersion}.zip` → pinned `m/{pluginName}/{semanticVersion}/marketplace.json`
  → root `marketplace.json`. Unlist and relist both regenerate. No in-scope use case is silently missing.
- **D16 and DoD — `ProcessPublishJobHandler` `SaveChangesAsync` count.** Read by ordering: success is
  `:45` (Building checkpoint) + `:92` (terminal) = 2; engineer-missing is `:37` only = 1, because it sits
  *before* the checkpoint and `FailAsync` (`:104`) is the sole save; snapshot-empty, manifest-null and
  validation-failure are checkpoint + `FailAsync` = 2; resume-from-`Building` skips `:41-46` entirely = 1.
  No path exceeds two. Asserted, not assumed, by tests 64 (`Received(2)`), 68 (`Received(1)`), 71
  (`Received(1)`) and 72 (`Received(2)`).
- **D3 and DoD — early return.** `ProcessPublishJobHandler.cs:28-31` returns before any repository or
  blob call for any status that is not `Queued`/`Building`; test 70 proves it with `DidNotReceive()` on
  both `SaveChangesAsync` and `ListByPrefixAsync`.
- **No `try`/`catch`.** Zero occurrences across `E3A.Application/Publishing/**`, the three new Engineers
  slices and `E3A.Jobs/**`. Blob and database failures bubble to the Function and drive queue retry, as
  the plan requires.
- **D4 — zip re-upload on retry.** `ProcessPublishJobHandler.cs:80-86`: `ListByPrefixAsync` on the exact
  blob name, upload only when the result is empty, `overwrite: false`. Tests 65 and 67 pin both sides,
  including the `DidNotReceive` on the skip path.
- **D5 / D6 / D7.** `host.json:12-13` sets `batchSize: 1` and `newBatchThreshold: 0`. Both marketplace
  writes are single in-memory-then-`overwrite: true` PUTs (`ProcessPublishJobHandler.cs:96-97`,
  `RegenerateMarketplaceHandler.cs:55-56`) — no staged prefix, exactly as D6 records. Pagination is
  bounded and throws rather than truncating (`RegenerateMarketplaceHandler.cs:25-40`); the cap arithmetic
  is correct at the boundary (50 pages read with `MarketplaceMaxPages = 50`, throw only on the attempt to
  read page 51), and test 78 pins the throw plus `DidNotReceive().UploadAsync`.
- **D2 — no branch in the Function.** `ProcessPublishJobFunction.cs:19-20` is two unconditional `Send`
  calls; the single log line is a `LoggerMessage.Define` static field (declared deviation #5), which
  preserves the behaviour while satisfying CA1848/CA1873.
- **D8 — guards in handlers, unguarded domain mutators.** `ItemVersion` and `Engineer.Unlist/Relist`
  throw nothing; every guard is a `Core.Errors` type from skill §5.9. No new exception class anywhere in
  the diff.
- **D19 and DoD — every new tunable in Options.** `PublishingOptions.cs` holds all 14. The only literals
  in production code are the ones the DoD permits (`e3a-`, `.claude-plugin/plugin.json`, `archive`, the
  1980 stamp, `application/zip`, `application/json`, `marketplace.json`, `Sha256HexLength = 64`) plus the
  four Claude Code protocol roots in `PluginStructureValidator.cs:10-15` — all named constants with WHY
  comments (declared deviation #8; I agree these are protocol facts, not product tunables).
- **Skill §8 DO/DON'T catalog, entry by entry.** §8.1 caps in `[Area]Options`, not entity constants ✓
  (`PublishingOptions`; `AppDbContext.cs:60-64` consumes them for column widths, and `Sha256HexLength` is
  a true invariant with a WHY comment at `:21`). §8.2 `IGenerator` for identifiers ✓ — no hand-rolled
  randomness or suffix generation in this slice. §8.3 `Is…ExistsAsync` + suffix loop ✓ — slug logic
  untouched. §8.4 `Deleted` not `Removed` ✓ — `Unlisted` inserted between `Published` and `Deleted`,
  `Deleted` untouched; safe because `Status` is `HasConversion<string>()` and no enum-valued field crosses
  the wire (`EngineerResult.Status` and `PublishStatusResult.Status` are both `string`). §8.5 soft-delete
  has exactly one home ✓ — `ItemVersion` added to `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`
  (`AppDbContext.cs:83`), the partial-index SQL filter stays with the index (`:65`), and there is no
  ad-hoc `IsDeleted` check anywhere in the new code. **No DON'T pattern is present in the diff.**
- **Skill §9 checklist.** File-scoped namespaces everywhere ✓. `sealed` on every command, query,
  validator, handler, result and test class ✓ (`PublishController` and `ProcessPublishJobFunction` are
  plain `class`, matching `EngineersController` and the plan's own contract for file #49).
  `DateTimeOffset` only — zero `DateTime` in the new code ✓. `.ConfigureAwait(false)` on every
  non-controller, non-test await ✓; the only bare awaits are `E3A.Jobs/Program.cs:32` top-level, which
  mirrors `E3A.Api/Program.cs`, and awaits inside test bodies, which testing §3 explicitly exempts.
  `[]` collection expressions ✓. `Result` suffix on both new CQRS outputs ✓. No `DefaultCodes`
  introduced; class-level `[Authorize]` plus an owner check in the handler on all four routes ✓.
  Migration `versions002` creates `ItemVersions` with `IX_ItemVersions_ItemId` and the unique
  `IX_ItemVersions_ItemType_ItemId_VersionNumber` filtered on `[IsDeleted] = 0`
  (`20260828115939_versions002.cs:41-51`) ✓. All 15 new `ErrorCodes` constants have keys in **both**
  resx files, with an identical `{limit}` placeholder and Arabic without tashkeel ✓. No Cloudflare purge
  code anywhere in the diff ✓.
- **Correctness spot checks (review order #2).** `SemanticVersionCalculator` rejects negatives and signs
  via `NumberStyles.None` and returns `1.0.0` on any malformed input rather than throwing.
  `PluginStructureValidator.SkillFolders` correctly requires `> 2` path segments, so `skills/x.md` is not
  treated as a skill folder while `skills/a/b/c.md` is attributed to `skills/a/`.
  `PluginTreeAssembler`'s `allowed` set and the validator's `paths` set both use `OrdinalIgnoreCase`, so
  D11's "manifest target with no snapshot asset" reliably produces `PLUGIN_MANIFEST_ASSET_MISSING`.
  `DraftSnapshotFreezer` deletes the snapshot prefix before writing, so a retry cannot collide with the
  no-overwrite default of the existing 6-arg `UploadAsync`. `ImportManifestResult` is serialized and
  deserialized with default (PascalCase, case-sensitive) options on both sides
  (`UploadEngineerDraftHandler.cs:56` vs `ProcessPublishJobHandler.cs:58`), so the frozen manifest
  round-trips. Every new guard ordering matches the plan's step list exactly.
- **Ripple of `EngineerStatus.Unlisted`.** Confirmed by grep: `GetCatalogQueryHandler`,
  `GetCatalogEngineerQueryHandler`, `GetCatalogTagsQueryHandler` and `RegenerateMarketplaceHandler` all
  filter `== EngineerStatus.Published`, so unlisted engineers vanish from browse, public detail, tags and
  the root marketplace automatically; `GetEngineerQueryHandler.cs:21` falls through to the owner-only
  branch. The plan's "no change needed" claim holds.

### Postman sync (review order #7)

`postman/e3a.postman_collection.json` parses clean and mirrors the API surface as changed:

- `Engineers / Publish Engineer` — `POST {{baseUrl}}/api/engineers/{{engineerId}}/publish`,
  `Content-Type: application/json`, body `{ "increment": "Patch" }`. The string form, correct after
  pass 3's fix, with the numeric-workaround `description` removed.
- `Engineers / Unlist Engineer` — `POST {{baseUrl}}/api/engineers/{{engineerId}}/unlist`, no body.
- `Engineers / Relist Engineer` — `POST {{baseUrl}}/api/engineers/{{engineerId}}/relist`, no body.
- `Publishing / Get Publish Status` — `GET {{baseUrl}}/api/publish/{{versionId}}/status`, new folder.

All four carry no per-request `auth`, so they inherit the collection-level bearer, matching every other
authenticated request. No stale or orphaned entry: the 15 requests map 1:1 onto the 15 routes across
`EngineersController`, `CatalogController` and `PublishController`. (`{{versionId}}` is not declared in
`collection.variable` — but neither are `baseUrl` or `engineerId`; this collection has always relied on
an environment, so it is not a new gap.)

### Docs sync (review order #8)

This change alters architecture (a new Functions worker project), scope (unlist/relist added, scanner
split out), policy (cache headers replace Cloudflare purge) and a format contract (`marketplace.json`
shape), so three owned docs had to move — and all three did.

- **`docs/architecture.md`** — the `BackgroundService` box is replaced by
  `E3A.Jobs (Azure Functions v4 isolated, .NET 10) ◄── Storage Queue publish-jobs`; the cache-header
  policy in "Reads never hit the API" matches `appsettings.json:29-30` exactly; the pipeline step list
  matches `ProcessPublishJobHandler.Handle` step for step, with the scanner marked as the next slice;
  "poison after `maxDequeueCount` (5)" matches `host.json:14`; "purge Cloudflare cache" is gone.
- **`docs/implementation-plan.md`** — locked-stack line, key decision 3, the `versions` row
  (`SemanticVersion`, `FailureReason`, `ScanReportJson` deferred), `Status(Draft|Published|Unlisted|Deleted)`,
  the API-surface line (`unlist`, `relist`, owner-only status poll) and the P3 split all agree with the
  code as changed.
- **`docs/plugin-spec.md`** — the wrapper shape matches `MarketplaceDocument.cs:3-5` field for field
  (`name` / `owner{name,url}` / `plugins[]`, camelCase via `PluginJsonSerializer.cs:10`); the unlist
  semantics match `RegenerateMarketplaceHandler.cs:27`; the attribution paragraph matches
  `ProcessPublishJobHandler.cs:67` and `RegenerateMarketplaceHandler.cs:59-63`; the stale
  `author { name: "@login", url }` comment at line 60 was corrected.

**No divergence found.** No incompleteness is reported, per the rule.

## Test quality

Per class — does it actually constrain the implementation?

| Class | Verdict |
|---|---|
| `Publishing/ItemVersionTests` | Constrains. Real state assertions, `BeOnOrAfter(before)` per testing §8, domain event asserted by type *and* `VersionId`. |
| `Publishing/Shared/SemanticVersionCalculatorTests` | Constrains. 15 malformed-input rows across all three increments, plus the `0.0.9 → 0.0.10` case that would catch a string-concat bug. |
| `Publishing/Shared/PluginJsonGeneratorTests` | Constrains, mildly. Substring assertions rather than a parse, but they pin the `e3a-` prefix, the version and the catalog URL — all three fail if wrong. |
| `Publishing/Shared/PluginTreeAssemblerTests` | Constrains. The extra-file test proves filtering; the ordering test compares against a recomputed ordinal sort, so dropping `OrderBy` fails it. |
| `Publishing/Shared/PluginStructureValidatorTests` | Constrains. All six rules have a failing case, plus a clean-tree case that catches a rule firing spuriously; the unsafe-path theory covers all five shapes the plan named. |
| `Publishing/Shared/DeterministicZipperTests` | **Partly.** Test 24 is the strong one — reversing the input and asserting byte equality genuinely fails without the ordinal sort. Test 23 is near-vacuous on its own (see N2): it would pass with a wall-clock stamp. Tests 25 and 26 do real round-trip and hash work. |
| `Publishing/Shared/MarketplaceDocumentGeneratorTests` | Constrains, mildly. Substring rather than structural assertions on the wrapper, but the archive source type, absolute zip URL, sha256 and keywords are asserted on the typed object, which is the load-bearing part. |
| `Publishing/Shared/DraftSnapshotFreezerTests` | Constrains. Verifies the snapshot-prefix delete, per-blob upload under the exact versioned name, ordinal ordering of the result, and the null-download skip with a matching `DidNotReceive`. |
| `Publishing/Shared/PublishStatusResultGeneratorTests` | Constrains. Both the URL-built and URL-null branches. |
| `Engineers/PublishEngineer/PublishEngineerValidatorTests` | Constrains. Pass case plus one failing case per rule, including an undefined enum value. |
| `Engineers/PublishEngineer/PublishEngineerHandlerTests` | Constrains. The theory pins all three increments *and* `VersionNumber == previous + 1`; `AddAsync` is matched with `Arg.Is<ItemVersion>` on `FrozenManifestJson`, not `Arg.Any`. |
| `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | Constrains. All six throws, each with `DidNotReceive().SaveChangesAsync`; the cap test asserts the `limit` context value. |
| `Engineers/UnlistEngineer/*` | Constrains. All five throws plus the happy path; every throwing test asserts **both** no-save and no-`Send`, so a mis-ordered guard cannot leave the marketplace regenerated. Guard order is genuinely pinned — the conflict test leaves the engineer in a valid status and stubs only an in-progress version. |
| `Engineers/RelistEngineer/*` | Constrains what it covers; three guard branches uncovered — see N1. |
| `Engineers/EngineerListingTests` | Constrains. `Unlist` asserts `LatestVersionId` is unchanged, which is the acceptance-#3 property. |
| `Publishing/PublishRequested/PublishRequestedEventHandlerTests` | Constrains. Test 61 passes the exact `TimeSpan` as a matcher, so a hardcoded timeout fails it. |
| `Publishing/ProcessPublishJob/ProcessPublishJobValidatorTests` | Constrains. |
| `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests` | Constrains, strongly. Not substitute-echo: the zip path, sha256, size, `LatestVersionId`, engineer status and save count are all derived by the handler from real inputs through the real zipper. Two nits — the skip-upload test asserts only a 64-character sha256 rather than equality with a recomputed hash, and the pinned-marketplace test asserts the blob name and headers but not the document body. |
| `Publishing/ProcessPublishJob/ProcessPublishJobHandlerGuardTests` | Constrains. `DidNotReceive` on both the save and the first blob call, so an early return that leaked a blob call would fail. |
| `Publishing/ProcessPublishJob/ProcessPublishJobHandlerFailureTests` | Constrains. Each failure asserts the exact `FailureReason` code, the absence of any zip upload and the save count; the structure-validation test also asserts the engineer is still `Draft`. |
| `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | Constrains, strongly — the best test class in the slice. Test 75 compiles the predicate actually handed to `FindPaginatedAsync` and proves it accepts a published engineer and rejects an unlisted one, which no amount of stubbed page contents could show. The uploaded JSON is captured off the real `Stream` rather than read back off a substitute. |
| `Publishing/GetPublishStatus/*` | Constrains. All four guards plus both mapping shapes; a pure read, so the absence of save assertions is correct. |

Nothing in the suite is a substitute asserting back what it was told to return.
