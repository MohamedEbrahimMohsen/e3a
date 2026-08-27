VERDICT: APPROVED

# Review — .claude Folder Upload + Import Manifest

## Blocking

None.

## Non-blocking

- api/E3A.Application/Engineers/UploadEngineerDraft/DraftNormalizer.cs (135 lines), SettingsJsonImporter.cs (103), api/E3A.Tests/Engineers/EngineerTests.cs (130) — over the skill's ~100-line guidance. The overrun was declared (deviation #3 / note #2), and splitting would have created production files the plan's fixed file list does not allow, so the implementer chose the contract over the guidance — the defensible call. Recommend the dev authorize a follow-up split of DraftNormalizer (mapping-table classification could move to its own file) rather than letting it grow in slice 3.
- api/E3A.Application/Engineers/UploadEngineerDraft/UploadEngineerDraftHandler.cs:41 — "await using var zipStream = ..." performs an awaited DisposeAsync without .ConfigureAwait(false). The plan mandated this exact line (Handlers step 6), so it is contract-faithful, and the wrapped stream disposes synchronously in practice; noting it only because the skill's ConfigureAwait rule is written as "every await".
- api/E3A.Api/appsettings.json is git-ignored (.gitignore:23, pre-existing dev choice) — the Azure/Uploads sections exist only on this machine. On CI or a fresh clone every UploadsOptions cap binds to 0/empty, which makes ValidateMaxFileSize(0) reject every upload. Not an implementer defect (it followed slice 1's established practice and flagged it), but a repo-level decision the dev should make: either track a defaults file or accept that config is deploy-time-only.
- Hooks uploaded natively as hooks/hooks.json are imported without per-hook HookWarnings entries — warnings are parsed only from settings.json#hooks (plan Decision 14, test 34). Not a docs divergence: plugin-spec's ingestion table defines settings.json as the hooks source, and real .claude folders keep hooks there. Worth extending warning extraction to a native hooks.json when slice 3 builds the detail-page hook warning.

## Verified

Claims from 02-implementation.md, independently confirmed:

- Build: ran dotnet build E3A.slnx — Build succeeded, 9 warnings, 0 errors; all 9 are the pre-existing vendored-library warnings (Core.Validation CS8602 x2, Core.OTP CS8618 x2, Core.Notifications CS8618 x5). Zero warnings in E3A.* or the new Core.Azure method.
- Tests: ran dotnet test E3A.Tests — Passed 131, Failed 0, Skipped 0. Count reconciles: 63 pre-existing + 68 new cases (61 new methods; the path-normalizer Theory expands x2 and the draft-normalizer Theory x7).
- File inventory: exactly the plan's 17 production files + 14 test files exist (git untracked list matched one-to-one); nothing extra; the tracked diff touches exactly the 8 files in "Existing code touched". Program.cs, E3A.Infrastructure/**, migrations, all .csproj, Directory.Packages.props, /docs are untouched (git diff main --stat confirms).
- Deviation 1 (Core.Azure GetBlobsAsync): StorageBlobClient.cs:29 calls GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken) — positional None/None are the SDK's documented defaults, so semantics equal the plan's named-argument body; CreateIfNotExistsAsync, the delete loop, and .ConfigureAwait(false) are verbatim. Exactly one method added to IStorageBlobClient; UploadAsync untouched; Azure SDK types appear nowhere in E3A.* (grep confirmed).
- Deviation 2 (ReadBufferSizeBytes = 81920): named constant with a WHY comment (ClaudeFolderZipReader.cs:15-16); correctly judged not a product tunable — inventing an options property the plan's fixed UploadsOptions shape does not list would itself have been a contract break.
- Deviation 4 (manifest ordering): settings-derived entries sort after loop entries; no plan assertion or test depends on entry order. Harmless.
- Contracts: every signature matches the plan tables — options (files 1-2), the five manifest records (file 3), 11 categories (file 4), command/validator/reader/sanitizer/normalizer/generators/importer/handlers (files 5-17). Reason and snippet constants match the plan strings character-for-character.
- Handler flow: UploadEngineerDraftHandler follows the 14 steps in order — guard, tracking GetByIdAsync, 404, 403, read, sanitize, path-normalize, map (UtcNow passed in once, Decision 24), delete prefix {OwnerUserId}/{EngineerId}/, per-asset upload, ReplaceDraftManifest, single SaveChangesAsync last, return manifest. No try/catch in either handler; the only catches live inside the pure engines exactly as Decision 21 and note 3 describe.
- Domain: Engineer.ReplaceDraftManifest sits between MarkPublished and Delete, sets UpdationDate, no guard (Decision 10); no handler assigns entity properties directly.
- Error codes + resx: all 12 constants with the exact SCREAMING_SNAKE values; all 12 keys present in both resx files, {limit}/{path} placeholders intact in both languages, Arabic without tashkeel.
- Controller: two thin actions at POST {engineerId:guid}/upload and GET {engineerId:guid}/import-manifest, [FromForm] IFormFile, token passed to Send; mirrors the neighboring actions (plain [Authorize], no policy — consistent with slice 1; the repo has no DefaultCodes).
- DI: exactly the two Configure<> lines added after EngineersOptions; AddCoreAzure() at Program.cs:65 already registers IStorageBlobClient.
- Config: on-disk appsettings.json Azure/Uploads sections match the plan JSON verbatim (empty StorageAccountUrl, nothing secret); UploadsOptionsFactory.Default() mirrors those values.
- Core.Validation fit: ValidateRequired / ValidateAllowedExtensions / ValidateMaxFileSize(int megabytes) verified against the vendored extension signatures — including that ValidateRequired(IFormFile) fails on Length == 0 (test 45's premise).
- Round-trip: serialize-on-upload / deserialize-on-GET share the one record contract; handler test 54 round-trips through the real serializer.
- Skill §8 walk: 8.1 caps in UploadsOptions/AzureOptions, invariants as named constants with WHY comments — clean; 8.2 no hand-rolled identifiers (none needed); 8.3 no slug logic in this slice; 8.4 no lifecycle naming changes; 8.5 no ad-hoc IsDeleted checks — deleted rows stay unreachable via the global filter (Decision 10). No DON'T pattern present in the diff.
- Skill §9 / style absolutes: file-scoped namespaces, sealed on every new class/record, DateTimeOffset only (grep confirmed no DateTime), [] collections, one-line type declarations, .ConfigureAwait(false) on every await in production code, comments limited to plan-mandated WHY invariants, FluentAssertions stays 6.12.2 (Directory.Packages.props untouched).
- Docs sync (#7): checked docs/plugin-spec.md (ingestion mapping, converted house-rules skill + snippet, skipped settings keys, sanitize list, upload constraints 20 MB / 400 files / extensions / traversal / symlink), docs/implementation-plan.md (drafts/{userId}/{itemId}/... layout, upload normalization, SKILL.md-at-root), docs/security-scan.md (sanitize strips settings.local.json / .env* / memory-session before storage), docs/architecture.md (drafts private blob). The implementation answers every question the docs answer the same way the docs do. Everything the docs describe that is absent (script-tier scan, publish pipeline, kebab-case validation, plugin.json generation) is slice-3+ incompleteness — correctly not flagged.

## Test quality

- ClaudeFolderZipReaderTests — strong: real zips through the real ZipArchive, including a genuine symlink ExternalAttributes fixture and a real cap breach on actual decompressed bytes. Every reader throw (5 codes) covered.
- ClaudeFolderSanitizerTests / UploadPathNormalizerTests / DraftNormalizerTests / DraftNormalizerConversionTests / SettingsJsonImporterTests / HouseRulesSkillGeneratorTests — pure-engine tests with real inputs and structural assertions (exact path lists, record equality against the emitting-class constants, JSON re-parsed to check the hooks wrapper). These genuinely constrain the mapping table; renaming a category, reordering the unwrap, or dropping a skip reason breaks them. Every engine throw (duplicate, extension, empty) covered.
- UploadEngineerDraftValidatorTests — passing case plus one failing case per rule, bound to ErrorCodes.*; the IFormFile substitute only fakes the two members the validator reads, which is the correct use of a substitute.
- UploadEngineerDraftHandlerGuardTests / UploadEngineerDraftHandlerTests — every throwing path asserts DidNotReceive on save (and on blob calls for the invalid-zip path); the happy path asserts the exact delete prefix, the exact two blob names (proving house-rules generation runs through the handler), Received(1) on Update + SaveChangesAsync, and a real-serializer round-trip of the persisted manifest. Not vacuous — substitutes are verified against derived values, not echoes.
- GetImportManifestQueryHandlerTests — all four throw branches plus a round-trip through ReplaceDraftManifest with real JSON.
- No test asserts a substitute's echo; no wall-clock equality (before / BeOnOrAfter used); no reflection into entities.

All 61 plan test rows exist with the exact names, plus the two EngineerTests appends.
