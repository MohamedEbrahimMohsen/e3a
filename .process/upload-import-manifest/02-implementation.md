# Implementation — .claude Folder Upload + Import Manifest

## Files created

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Application/Options/UploadsOptions.cs` | 15 | All upload caps/lists (`SectionName = "Uploads"`). |
| `api/E3A.Application/Options/AzureOptions.cs` | 10 | Managed identity, storage account URL, drafts container (`SectionName = "Azure"`). |
| `api/E3A.Application/Engineers/Shared/ImportManifestResult.cs` | 11 | Manifest contract: 5 sealed records (result + persisted JSON shape). |
| `api/E3A.Application/Engineers/Shared/ImportCategories.cs` | 16 | 11 category constants. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/UploadEngineerDraftCommand.cs` | 7 | `sealed record (Guid EngineerId, IFormFile File)`. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/UploadEngineerDraftValidator.cs` | 25 | Id required · file required/zip/max size from options. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/UploadedFile.cs` | 3 | `sealed record UploadedFile(string Path, byte[] Content)`. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/ClaudeFolderZipReader.cs` | 91 | Zip read: caps, traversal, rooted paths, symlink mask, actual-bytes zip-bomb guard. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/ClaudeFolderSanitizer.cs` | 37 | Strips machine-local/secret files; records every stripped path. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/UploadPathNormalizer.cs` | 87 | Root unwrap loop, `.claude/` strip, duplicate + extension enforcement. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/DraftNormalizer.cs` | 135 | The mapping table → assets + manifest. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/HouseRulesSkillGenerator.cs` | 43 | Generates `skills/{folder}/SKILL.md` with front matter + `## Source:` sections. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/SettingsJsonImporter.cs` | 103 | `settings.json` → `hooks/hooks.json` + hook warnings + per-key skips. |
| `api/E3A.Application/Engineers/UploadEngineerDraft/UploadEngineerDraftHandler.cs` | 63 | Guard → ownership → read/sanitize/normalize/map → delete prefix → upload → save. |
| `api/E3A.Application/Engineers/GetImportManifest/GetImportManifestQuery.cs` | 6 | Query record. |
| `api/E3A.Application/Engineers/GetImportManifest/GetImportManifestQueryValidator.cs` | 13 | Engineer id required. |
| `api/E3A.Application/Engineers/GetImportManifest/GetImportManifestQueryHandler.cs` | 42 | Owner-only read; 404 when nothing uploaded. |
| `api/E3A.Tests/Engineers/Shared/ZipFixtureFactory.cs` | 47 | `Build`, `BuildWithExternalAttributes`, `AsStream`. |
| `api/E3A.Tests/Engineers/Shared/UploadsOptionsFactory.cs` | 21 | Options mirroring the committed defaults, with cap overrides. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/ClaudeFolderZipReaderTests.cs` | 93 | Tests 3–10. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/ClaudeFolderSanitizerTests.cs` | 69 | Tests 11–16. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadPathNormalizerTests.cs` | 67 | Tests 17–22. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/DraftNormalizerTests.cs` | 99 | Tests 23–30. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/DraftNormalizerConversionTests.cs` | 66 | Tests 31–35. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/HouseRulesSkillGeneratorTests.cs` | 38 | Tests 36–37. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/SettingsJsonImporterTests.cs` | 77 | Tests 38–42. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadEngineerDraftValidatorTests.cs` | 68 | Tests 43–47. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadEngineerDraftHandlerGuardTests.cs` | 82 | Tests 48–51. |
| `api/E3A.Tests/Engineers/UploadEngineerDraft/UploadEngineerDraftHandlerTests.cs` | 67 | Tests 52–54. |
| `api/E3A.Tests/Engineers/GetImportManifest/GetImportManifestQueryValidatorTests.cs` | 26 | Tests 55–56. |
| `api/E3A.Tests/Engineers/GetImportManifest/GetImportManifestQueryHandlerTests.cs` | 82 | Tests 57–61. |

17 production files + 14 test files — exactly the plan's list, nothing more, nothing fewer.

## Files modified

| Path | Change |
|------|--------|
| `api/core-libraries/Core.Azure/Clients/StorageBlobClient.cs` | `DeleteByPrefixAsync` added to `IStorageBlobClient` and `StorageBlobClient` (+ `using Azure.Storage.Blobs.Models;` — see Deviations). `UploadAsync` untouched. |
| `api/E3A.Domain/Engineers/Engineer.cs` | `ReplaceDraftManifest(string draftManifestJson)` added between `MarkPublished` and `Delete`; sets `UpdationDate`. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | `EngineerDraftNotUploaded` appended to `// Engineers`; new `// Uploads` group with the 11 constants. |
| `api/E3A.Application/DependencyInjection.cs` | `Configure<UploadsOptions>` + `Configure<AzureOptions>` after the `EngineersOptions` line. |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | Two thin actions (`POST {id}/upload`, `GET {id}/import-manifest`) + two usings. 77 lines. |
| `api/E3A.Api/appsettings.json` | `Azure` gains `StorageAccountUrl` (empty) + `DraftsBlobContainerName` (`drafts`); new `Uploads` section verbatim from the plan. **This file is git-ignored** (`.gitignore:23`) — see Notes. |
| `api/E3A.Api/Resources/Messages.en.resx` / `Messages.ar.resx` | The 12 new keys appended to both, placeholders `{limit}`/`{path}` intact, Arabic without tashkeel. |
| `api/E3A.Tests/Engineers/EngineerTests.cs` | Two `ReplaceDraftManifest` tests appended. |

No other file touched: `Program.cs`, `E3A.Infrastructure/**`, migrations, every `.csproj`, `Directory.Packages.props`, `/docs` are byte-identical.

## Deviations

| Plan said | Reality | What I did |
|-----------|---------|------------|
| `Core.Azure` body: `blobContainerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken)` | Does not compile against the vendored Azure.Storage.Blobs: `error CS7036: no argument given that corresponds to the required parameter 'traits'` — this overload has no optional parameters. | Called it positionally: `GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)` and added `using Azure.Storage.Blobs.Models;`. Same semantics (`None`/`None` are the documented defaults); everything else in the plan's body is verbatim, including `CreateIfNotExistsAsync` and `.ConfigureAwait(false)`. |
| Nothing about a read-buffer size for the reader's cumulative byte cap. | The cap must be checked per read, so a buffer size is needed and `UploadsOptions` (whose shape the plan fixes) has no such property. | Used a named constant `ReadBufferSizeBytes = 81920` with a WHY comment in `ClaudeFolderZipReader` rather than inventing an options property the plan does not list. Not a product tunable, so §8.1 does not apply. |
| "Every file ≤ ~100 lines." | `DraftNormalizer.cs` is 135 and `SettingsJsonImporter.cs` is 103. | Kept them as single files: the plan fixes the file list, so splitting the mapping table or the settings importer would mean creating a production file the plan does not list. I compacted both (`DraftNormalizer` went 151 → 135 by pulling the settings block out of the loop and making the leaf helpers expression-bodied). Flagging rather than silently breaking the file contract — say the word and I will split `DraftNormalizer` into a mapping-table helper file. |
| Manifest entry order implied by processing files in upload order (settings entries at their position in the loop). | The compaction above processes `settings.json` after the main loop. | `Imported`/`Skipped` entries coming from `settings.json` now sort last, and the generated `hooks/hooks.json` asset is appended after the imported assets. No test or plan assertion depends on that ordering; behaviour is otherwise identical. |

Everything else — signatures, file paths, type names, error codes, reason constants, the handler's 14 steps, the 61 test names — is implemented verbatim.

## Build & test

Run from `D:\Personal\_e3a\api`:

```
dotnet build E3A.slnx
  Build succeeded.
      9 Warning(s)
      0 Error(s)

dotnet test E3A.Tests/E3A.Tests.csproj
  Passed!  - Failed: 0, Passed: 131, Skipped: 0, Total: 131, Duration: 914 ms - E3A.Tests.dll (net10.0)
```

The 9 warnings are all pre-existing in the vendored core libraries (`Core.Validation` CS8602 ×2, `Core.OTP` CS8618 ×2, `Core.Notifications` CS8618 ×5). Zero warnings in `E3A.*` or in the new `Core.Azure` method. `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` + SonarAnalyzer are on, so the build passing means analyzer-clean.

Test count: 131 total = 63 pre-existing + 68 new cases (61 new test methods; two `[Theory]`s expand to 7 and 2 cases). Verified 59 new methods across the 12 new test classes + 2 appended to `EngineerTests`.

## Notes for review

1. **`appsettings.json` is git-ignored** (`.gitignore:23`, alongside the E3a.Functions one). My `Azure`/`Uploads` edits are on disk and drive the local run, but `git status` will not show them and they will not reach another machine or CI. This is the same situation as slice ①'s `Engineers` section, so I followed the existing repo practice rather than inventing a tracked defaults file — but if the intent is for committed defaults to exist, that is a repo-level gap worth deciding on.
2. **`EngineerTests.cs` is now 130 lines** after the two-test append the plan mandates. Pre-existing file, plan-directed growth, but it is over the ~100 guidance.
3. `ClaudeFolderZipReader` catches `InvalidDataException` around the whole entry walk, so a corrupt entry discovered mid-read also surfaces as `UPLOAD_ZIP_INVALID`; `SettingsJsonImporter` likewise maps a lazily-thrown `JsonException` to the unparseable skip. Both are inside the pure engine — the handler stays try/catch-free.
4. `SettingsJsonImporter` matches the `hooks` key case-insensitively and lower-cases before the `permissions`/`env`/`model`/`statusLine` switch, but the skipped-entry source string preserves the creator's original casing (`settings.json#statusLine`).
5. Duplicate detection runs before extension enforcement in `UploadPathNormalizer`, so a zip containing both `.claude/skills/x/SKILL.md` and `skills/x/skill.md` reports `UPLOAD_DUPLICATE_PATH` rather than a type error. Plan step order (d) then (e) — matches.
6. `IFormFile` binds to the Core.Validation extensions declared on `IRuleBuilder<T, IFormFile?>` even though the command's `File` is non-nullable; it compiles clean with no nullability warning.
7. No `Core.Azure` unit tests, per Decision 25 — `DeleteByPrefixAsync` is exercised only through the handler substitute.
8. `/docs` unchanged: this slice implements plugin-spec / security-scan / implementation-plan as written (incompleteness closing, no divergence).
