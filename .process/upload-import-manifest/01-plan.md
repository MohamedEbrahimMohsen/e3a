# Plan — .claude Folder Upload + Import Manifest

## Goal
An authenticated creator can upload their whole `.claude` folder as a zip against an engineer draft they own. The API validates the caps (20 MB zip, 400 files, allowed types, path-traversal-safe, no symlinks), sanitizes machine-local/secret files (recording what was stripped), normalizes the tree per the `docs/plugin-spec.md` mapping into a three-column import manifest (imported / converted / skipped, plus hook warnings and a CLAUDE.md snippet), persists the normalized draft assets to the private drafts Blob container at `{userId}/{engineerId}/…`, stores the manifest JSON in `Engineer.DraftManifestJson`, and returns the manifest. The creator can GET the manifest for an owned draft, and re-uploading replaces the prior draft assets and manifest. Today nothing writes `DraftManifestJson` and no blob storage is wired.

## Scope
**In:** two use cases (`UploadEngineerDraft`, `GetImportManifest`); the pure normalization engine (`ClaudeFolderZipReader`, `ClaudeFolderSanitizer`, `UploadPathNormalizer`, `DraftNormalizer`, `HouseRulesSkillGenerator`, `SettingsJsonImporter`) in the `UploadEngineerDraft` use-case folder; `ImportManifestResult` (+ sub-records, categories) in `Engineers/Shared`; `UploadsOptions` + `AzureOptions` bound from configuration; one new method `DeleteByPrefixAsync` on the vendored `Core.Azure` `IStorageBlobClient`/`StorageBlobClient`; `Engineer.ReplaceDraftManifest`; two controller actions on the existing `EngineersController`; 12 new error codes + ar/en resource strings; `Azure`/`Uploads` appsettings sections; full unit tests per `conventions/dotnet-testing.md`.

**Out:** the security scan (slice ③); publish/versions/zips/marketplace.json (③); teams (④); catalog (⑤); GitHub OAuth (auth stays `[Authorize]` + `ICurrentUserService`); frontend; queues; any EF migration (`DraftManifestJson` is already a mapped `nvarchar(max)` column in `20260827082800_initial` — verified); any change to `Program.cs`, `E3A.Infrastructure/DependencyInjection.cs`, any `.csproj`, `Directory.Packages.props`, or middleware order.

**Deferred:**

| Item | Why |
|------|-----|
| Script-tier security scan of hook scripts | `docs/security-scan.md` puts the scan in the publish pipeline (slice ③). This slice only imports hooks and records warnings in the manifest. |
| Kebab-case normalization of skill folder names | Renaming paths at draft time would desynchronize what the creator uploaded from what they see; structure validation belongs to slice ③'s `StructureValidator`. This slice only enforces SKILL.md-at-root (skip with reason). |
| Auditing via `IAuditableCommand` | No existing command opts in (verified: zero implementors in `E3A.*`); opting in only here would be inconsistent. Cross-cutting decision for a later slice. |
| Folder (non-zip) upload | Browser folder pickers zip client-side; the API contract is one multipart zip. Not divergence from plugin-spec's "zip or folder" — the folder path is a frontend concern. |
| Blob download / draft-content browsing | Nothing in this slice reads blobs back; slice ③ will add download to `Core.Azure` when the publish pipeline needs it. |
| Concurrency guard on simultaneous uploads to one engineer | Single-creator drafts; last-writer-wins is acceptable for v0.1 (Decision 15). |

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | One slice or three (upload / GET manifest / replace)? | **One vertical slice.** Replace is the same endpoint (delete-prefix-then-upload); GET reads what upload wrote. | They share the manifest contract, error codes, controller, and options; splitting produces plans that re-edit the same files. |
| 2 | Blob mechanics. | Use the vendored `Core.Azure` `IStorageBlobClient` exactly as Morabh does (`UploadAsync(stream, managedIdentityClientId, storageAccountUrl, containerName, blobName, ct)`), **and add one method `DeleteByPrefixAsync`** to the same interface/class. No new Azure SDK usage anywhere in `E3A.*`. | Core-first mandate. Verified the vendored client has only `UploadAsync`; replace-upload needs prefix deletion. Adding the missing capability to `Core.Azure` is the Core-first move; a hand-rolled client in Infrastructure is prohibited. |
| 3 | Why delete-before-upload instead of overwrite? | The handler always calls `DeleteByPrefixAsync` first, then uploads. | Verified: `StorageBlobClient.UploadAsync` binds `blobClient.UploadAsync(content, cancellationToken:)`, which does **not** overwrite and throws `BlobAlreadyExists` on re-upload. Deleting the prefix first gives replace semantics and makes overwrite impossible. Do not "optimize" the delete away. |
| 4 | Blob layout. | Container from `AzureOptions.DraftsBlobContainerName` (default `drafts`); blob name `{OwnerUserId}/{EngineerId}/{targetPath}`; delete prefix `{OwnerUserId}/{EngineerId}/`. | Mirrors Morabh's per-purpose container-name keys (`VerificationDocumentsBlobContainerName` pattern, verified) and satisfies implementation-plan's `drafts/{userId}/{itemId}/...` (container + path). |
| 5 | Individual asset blobs or one normalized zip? | **Individual files**, one blob per normalized asset, sequential loop. | Literal reading of "draft assets in blob"; slice ③ and future draft browsing consume files. ≤400 files / ≤20 MB makes the loop acceptable. |
| 6 | Where does the normalization engine live? | Static classes in `E3A.Application/Engineers/UploadEngineerDraft/` — no interfaces, no DI registrations. Result records co-located with their producer (mirrors `UploadResult` living in `StorageBlobClient.cs`). | Pure in-memory logic with no I/O; the `EngineerSlugGenerator` static-class precedent. Avoids new abstractions. Options are resolved in the handler/validator and passed as parameters — testable without substitutes. |
| 7 | Caps and lists — options or constants? | All tunables in **`UploadsOptions`** (`SectionName = "Uploads"`): zip size (MB, matching `ValidateMaxFileSize(int megabytes)` — verified signature), uncompressed cap, file count, allowed extensions, hook-script extensions, sanitize name/prefix/folder lists. The **recognized plugin root names** (`skills`, `agents`, …) and generated-file names are named constants with a WHY comment. | Skill §8.1: caps/tunables → options. The root names are the plugin-format contract from plugin-spec — true invariants, changing them breaks the product. |
| 8 | Container/URL config. | New `AzureOptions` in `E3A.Application/Options` (`SectionName = "Azure"` const, e3a style): `ManagedIdentityClientId`, `StorageAccountUrl`, `DraftsBlobContainerName`. Extend the existing `"Azure"` appsettings section with `"StorageAccountUrl": ""` and `"DraftsBlobContainerName": "drafts"`. | Mirrors Morabh's `AzureOptions` (verified) minus the queue/AAC keys this slice doesn't consume. Empty URL placeholder = env-specific, nothing secret committed; Mohamed creates the storage account later and fills `appsettings.Development.json` / Azure app settings. |
| 9 | Command file shape. | `UploadEngineerDraftCommand(Guid EngineerId, IFormFile File)` — non-nullable `IFormFile`, controller action takes `[FromForm] IFormFile file`. | Mirrors Morabh's `UploadFileCommand`/`FilesController` exactly (verified). `IFormFile` + `ZipArchive` compile in `E3A.Application` (compile-probed — the `Microsoft.AspNetCore.App` FrameworkReference flows transitively via `Core.Validation`). A request with no file part gets the framework's 400, same accepted behaviour as slice ① Decision 23. |
| 10 | Who may upload / read the manifest, and in which status? | Owner only, both endpoints (manifest is creator-facing even when the engineer is Published — a published engineer keeps a draft workspace for its next version). No status guard: `Deleted` rows are unreachable through the global soft-delete filter. 401 no user → 404 not found → 403 not owner, in that order — same as `UpdateEngineerHandler`. | Mirrors the existing owner-check shape verbatim. |
| 11 | Constraint violations vs. mapping outcomes. | Upload **constraints** (invalid zip, >400 files, uncompressed cap, traversal, symlink, disallowed extension, duplicate path, empty result) → `BadRequestCoreException` (400) with `context` naming the offending `path`/`limit`. **Mapping** outcomes (no plugin equivalent, settings keys, non-text rule files, skill folder without SKILL.md) → `skipped` manifest entries with reasons — "nothing silently dropped". | plugin-spec separates "Upload constraints" from the three-column manifest. Rejection uses the existing exception table; no new exception types. |
| 12 | Extension policy details. | Allowlist match on `Path.GetExtension` (case-insensitive), enforced in `UploadPathNormalizer` **after** sanitize (so `.DS_Store`/`.env` are stripped, not rejected). Extension-less files (e.g. `bin/mytool`) are rejected — spec allows only text + png/jpg/svg + hook scripts, and the security-scan hygiene tier blocks binaries anyway. | Ordering matters: sanitize-then-enforce keeps junk from failing good uploads. |
| 13 | Zip-of-what? (root shapes) | `UploadPathNormalizer` unwraps a single non-recognized root folder repeatedly (handles `myrepo/.claude/…` and `.claude/…`), then strips any remaining leading `.claude/` segment (handles repo-root zips with `CLAUDE.md` beside `.claude/`). Unwrap never fires when the single root is a recognized name (`skills`, …, `rules`, `conventions`, `docs`) or a root-level file. | Deterministic, covers the three realistic zip shapes without heuristics. A full repo zip containing source code will fail the extension allowlist — acceptable: the documented upload is the `.claude` folder (optionally + root `CLAUDE.md`). |
| 14 | settings.json handling. | Parse with `JsonDocument`. `hooks` object → generate `hooks/hooks.json` with content `{"hooks": <original object>}` (imported entry, source `settings.json#hooks`) + one `HookWarningResult(Event, Matcher, Command)` per hook command (event-only when the shape is unrecognized). Keys `permissions`, `env`, `model`, `statusLine`, and any other key → skipped entries `settings.json#<key>` with reasons. Unparseable file → single skipped entry. If the upload already contains `hooks/hooks.json`, the uploaded file wins and `settings.json#hooks` is skipped with a reason. | plugin-spec's imported/skipped table + hooks policy ("manifest lists every hook with its trigger event"). The `{"hooks": …}` wrapper is the plugin `hooks/hooks.json` format. |
| 15 | Replace semantics + failure window. | Replace = delete prefix, upload assets, then `ReplaceDraftManifest` + one `SaveChangesAsync`. If an upload fails midway, the DB manifest is untouched (stale) while blobs are partial; the next successful upload heals both. Accepted for v0.1. | True atomicity needs staged prefixes + swap — not worth it pre-publish; drafts are only consumed by their owner and slice ③ re-reads at publish time. |
| 16 | House-rules conversion. | Sources: root `CLAUDE.md` first, then `.md`/`.markdown`/`.txt` under `rules/`, `conventions/`, `docs/` (ordinal path order). Output: one generated `skills/house-rules/SKILL.md` (front matter + `## Source: {path}` sections); if the upload already has any file under `skills/house-rules/`, the generated skill moves to `skills/e3a-house-rules/` (name in front matter follows the folder). Manifest gets one converted entry per source and a fixed `ClaudeMdSnippet` string. No sources → no skill, `ClaudeMdSnippet` null, `Converted` empty. | plugin-spec's Converted section; the `e3a-` prefix resolves the only possible collision deterministically and attributably. |
| 17 | Skills structure check. | A file under `skills/` is imported only when it lives under `skills/{folder}/` and that folder contains a `SKILL.md` (case-insensitive) at its root; otherwise skipped with reason. | implementation-plan: "skills keep SKILL.md-at-root validation". Skip (not reject) keeps the manifest transparent. |
| 18 | Manifest persistence & round-trip. | `ImportManifestResult` (+ sub-records) is both the persisted JSON shape and the API result. Serialize with `JsonSerializer.Serialize(manifest)` (default options); GET deserializes with `JsonSerializer.Deserialize<ImportManifestResult>(json)!` (null-forgiving — a stored manifest is never the literal `null`). One contract, zero duplicate types. | Records with positional constructors round-trip under System.Text.Json; ASP.NET camel-cases the HTTP response like every other endpoint. |
| 19 | GET with no upload yet. | `NotFoundCoreException(ErrorCodes.EngineerDraftNotUploaded)` → 404. | The manifest resource genuinely does not exist yet; 404 is the table's mapping. |
| 20 | Manifest reason/category strings. | Reasons are `public const string` on the class that emits them (`DraftNormalizer`, `SettingsJsonImporter`); categories are consts in `ImportCategories`. Plain English (e3a is EN-only); these are data in a creator-facing document, not error messages, so no resx. | No magic strings, testable by referencing the consts, and no fake localization of persisted JSON. |
| 21 | Zip reading details. | Reader copies the request stream into a `MemoryStream` first (guarantees seekability for `ZipArchive`), skips directory entries (name ends with `/`), normalizes `\`→`/`, rejects rooted paths and `..` segments (`UPLOAD_UNSAFE_PATH`), detects symlinks via `((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000` (`UPLOAD_SYMLINK_NOT_ALLOWED`), enforces the file-count cap and a cumulative **actual-bytes-read** cap (zip-bomb guard — never trust the declared length alone). `InvalidDataException` from `ZipArchive` is caught **inside the reader** and rethrown as `BadRequestCoreException(UPLOAD_ZIP_INVALID)` — the handler itself stays try/catch-free. | Compile-probed `ZipArchive`/`ExternalAttributes` availability in `E3A.Application`. |
| 22 | Sanitize matching. | Case-insensitive. Strip when: file name ∈ `StrippedFileNames`; file name starts with any `StrippedFileNamePrefixes`; **any** path segment ∈ `StrippedFolderNames`. Every stripped path is recorded in `ImportManifestResult.StrippedPaths`. | security-scan.md sanitize contract + "record what was stripped". Any-segment matching catches nested `memory/` etc. |
| 23 | Duplicate target paths (e.g. `.claude/skills/x` + `skills/x`). | `BadRequestCoreException(UPLOAD_DUPLICATE_PATH, context: path)` — case-insensitive comparison. | Deterministic; silent last-wins would contradict "nothing silently dropped". |
| 24 | Timestamp determinism. | The handler computes `DateTimeOffset.UtcNow` once and passes it into `DraftNormalizer.Normalize(...)` as `uploadedAt`; the manifest stores it as `UploadedAt`. | Pure normalizer stays clock-free and its tests deterministic (conventions §8). |
| 25 | No `Core.Azure` unit tests. | `StorageBlobClient` (including the new `DeleteByPrefixAsync`) is an Azure SDK client — same category as `Repository<T>`: out of unit-test scope. Handler tests substitute `IStorageBlobClient`. | conventions §5 excludes infrastructure clients; the method is a thin SDK walk. |

## Existing code touched

| File | Change |
|------|--------|
| `api/core-libraries/Core.Azure/Clients/StorageBlobClient.cs` | Add `Task DeleteByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken);` to `IStorageBlobClient` and the implementation to `StorageBlobClient` (exact body below). Nothing else in `core-libraries/` changes. |
| `api/E3A.Domain/Engineers/Engineer.cs` | Add the `ReplaceDraftManifest` method (body below). No other member changes. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Append `EngineerDraftNotUploaded` to the `// Engineers` group and a new `// Uploads` group with the 11 constants from **Error codes**. |
| `api/E3A.Application/DependencyInjection.cs` | Add, after the `EngineersOptions` line: `services.Configure<UploadsOptions>(configuration.GetSection(UploadsOptions.SectionName));` and `services.Configure<AzureOptions>(configuration.GetSection(AzureOptions.SectionName));`. No using changes needed (`E3A.Application.Options` already imported). |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | Add two actions (**API surface**) + usings `E3A.Application.Engineers.UploadEngineerDraft;`, `E3A.Application.Engineers.GetImportManifest;`. File stays ≈ 78 lines. |
| `api/E3A.Api/appsettings.json` | In the existing `"Azure"` section add `"StorageAccountUrl": ""` and `"DraftsBlobContainerName": "drafts"`. Add the top-level `"Uploads"` section (**Configuration**). |
| `api/E3A.Api/Resources/Messages.en.resx` / `Messages.ar.resx` | Append the 12 keys from **Error codes**, existing `<data name="…" xml:space="preserve"><value>…</value></data>` shape. |
| `api/E3A.Tests/Engineers/EngineerTests.cs` | Append the two `ReplaceDraftManifest` tests. |

Untouched: `Program.cs`, `E3A.Infrastructure/**` (no new repository; `AddCoreAzure()` already registers the blob client — verified in `Program.cs` line 65), all migrations, every `.csproj`, `EngineersOptions`, all existing use cases and their tests, `/docs` (verified: this slice implements plugin-spec/security-scan/implementation-plan as written — no divergence, only incompleteness closing).

## Files to create

All paths relative to `D:/Personal/_e3a/`. Every file: file-scoped namespace matching folder, one-line type declarations, block bodies with braces, `DateTimeOffset` only, `[]` collections, `.ConfigureAwait(false)` on every await outside controllers/test bodies, no comments except the WHY-invariants noted.

### Application — Options

| # | Path | Contract |
|---|------|----------|
| 1 | `api/E3A.Application/Options/UploadsOptions.cs` | `public sealed class UploadsOptions` · `public const string SectionName = "Uploads";` · properties (all `{ get; set; }`): `int MaxZipSizeMegabytes` · `long MaxUncompressedSizeBytes` · `int MaxFileCount` · `List<string> AllowedExtensions = []` · `List<string> HookScriptExtensions = []` · `List<string> StrippedFileNames = []` · `List<string> StrippedFileNamePrefixes = []` · `List<string> StrippedFolderNames = []` |
| 2 | `api/E3A.Application/Options/AzureOptions.cs` | `public sealed class AzureOptions` · `public const string SectionName = "Azure";` · `string ManagedIdentityClientId { get; set; } = string.Empty;` · `string StorageAccountUrl { get; set; } = string.Empty;` · `string DraftsBlobContainerName { get; set; } = string.Empty;` |

### Application — Shared manifest contract

| # | Path | Contract |
|---|------|----------|
| 3 | `api/E3A.Application/Engineers/Shared/ImportManifestResult.cs` | Five sealed records (one contract, one file):<br>`public sealed record ImportManifestResult(List<ImportedItemResult> Imported, List<ConvertedItemResult> Converted, List<SkippedItemResult> Skipped, List<string> StrippedPaths, List<HookWarningResult> HookWarnings, string? ClaudeMdSnippet, DateTimeOffset UploadedAt);`<br>`public sealed record ImportedItemResult(string SourcePath, string TargetPath, string Category);`<br>`public sealed record ConvertedItemResult(string SourcePath, string TargetPath, string Reason);`<br>`public sealed record SkippedItemResult(string SourcePath, string Reason);`<br>`public sealed record HookWarningResult(string Event, string? Matcher, string? Command);` All fields client-facing; no `LocalizedText`. |
| 4 | `api/E3A.Application/Engineers/Shared/ImportCategories.cs` | `public static class ImportCategories` — `public const string` for: `Skills = "Skills"`, `Agents = "Agents"`, `Commands = "Commands"`, `Hooks = "Hooks"`, `HookScripts = "HookScripts"`, `McpServers = "McpServers"`, `LspServers = "LspServers"`, `OutputStyles = "OutputStyles"`, `Monitors = "Monitors"`, `Bin = "Bin"`, `Themes = "Themes"`. |

### Application — UploadEngineerDraft use case

Namespace for all: `E3A.Application.Engineers.UploadEngineerDraft;`

| # | Path | Contract |
|---|------|----------|
| 5 | `.../UploadEngineerDraft/UploadEngineerDraftCommand.cs` | `public sealed record UploadEngineerDraftCommand(Guid EngineerId, IFormFile File) : IRequest<ImportManifestResult>;` (`using Microsoft.AspNetCore.Http;`) |
| 6 | `.../UploadEngineerDraft/UploadEngineerDraftValidator.cs` | `public sealed class UploadEngineerDraftValidator : AbstractValidator<UploadEngineerDraftCommand>` — ctor `(IOptions<UploadsOptions> uploadsOptions)`; read `.Value` once. Rules, in order:<br>`RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);`<br>`RuleFor(x => x.File).ValidateRequired(ErrorCodes.UploadFileRequired).ValidateAllowedExtensions(ZipExtensions, ErrorCodes.UploadFileMustBeZip).ValidateMaxFileSize(options.MaxZipSizeMegabytes, ErrorCodes.UploadFileTooLarge);`<br>plus `// The endpoint contract accepts exactly one archive format.` `private static readonly List<string> ZipExtensions = [".zip"];` |
| 7 | `.../UploadEngineerDraft/UploadedFile.cs` | `public sealed record UploadedFile(string Path, byte[] Content);` — `Path` always `/`-separated, relative. |
| 8 | `.../UploadEngineerDraft/ClaudeFolderZipReader.cs` | `public static class ClaudeFolderZipReader` — `public static List<UploadedFile> Read(Stream zipStream, UploadsOptions options)`. Behaviour per Decision 21: copy to `MemoryStream`; open `ZipArchive` inside `try { } catch (InvalidDataException exception) { throw new BadRequestCoreException(ErrorCodes.UploadZipInvalid, innerException: exception); }`; per entry — skip if `FullName` ends with `/` or is empty; normalize separators; throw `UploadUnsafePath` (context `["path"]`) when `Path.IsPathRooted(normalized)` or any segment is `..`; throw `UploadSymlinkNotAllowed` (context `["path"]`) on the symlink attribute mask; increment count, throw `UploadTooManyFiles` (context `["limit"] = options.MaxFileCount`) when exceeded; read content via `entry.Open()` → `MemoryStream`, accumulate actual bytes, throw `UploadUncompressedTooLarge` (context `["limit"] = options.MaxUncompressedSizeBytes`) when exceeded; add `new UploadedFile(normalized, bytes)`. |
| 9 | `.../UploadEngineerDraft/ClaudeFolderSanitizer.cs` | `public sealed record SanitizeOutcome(List<UploadedFile> Files, List<string> StrippedPaths);`<br>`public static class ClaudeFolderSanitizer` — `public static SanitizeOutcome Sanitize(List<UploadedFile> files, UploadsOptions options)`. Matching per Decision 22, all `StringComparison.OrdinalIgnoreCase`. Non-matching files pass through in order. |
| 10 | `.../UploadEngineerDraft/UploadPathNormalizer.cs` | `public static class UploadPathNormalizer` — `public static List<UploadedFile> Normalize(List<UploadedFile> files, UploadsOptions options)`. Steps: (a) trim leading `./`; (b) unwrap loop per Decision 13 using `// Plugin-format roots (docs/plugin-spec.md): a zip whose single root is one of these is already unwrapped.` `private static readonly HashSet<string> RecognizedRootNames = new(StringComparer.OrdinalIgnoreCase) { "skills", "agents", "commands", "hooks", "output-styles", "monitors", "bin", "themes", "rules", "conventions", "docs" };` — unwrap only when **every** path starts with `<root>/` and `<root>` ∉ set; (c) strip one leading `.claude/` where present; (d) throw `UploadDuplicatePath` (context `["path"]`) on case-insensitive path collision; (e) throw `UploadFileTypeNotAllowed` (context `["path"]`) when `Path.GetExtension` (lowered) ∉ `options.AllowedExtensions`. |
| 11 | `.../UploadEngineerDraft/DraftNormalizer.cs` | `public sealed record NormalizedDraft(List<UploadedFile> Assets, ImportManifestResult Manifest);`<br>`public static class DraftNormalizer` — `public static NormalizedDraft Normalize(List<UploadedFile> files, List<string> strippedPaths, UploadsOptions options, DateTimeOffset uploadedAt)`. Applies the **Mapping table** below; delegates to `SettingsJsonImporter` and `HouseRulesSkillGenerator`; throws `BadRequestCoreException(ErrorCodes.UploadEmpty)` when `Assets` ends empty. Public reason consts: `NoPluginEquivalentReason = "No plugin equivalent."` · `SkillMissingSkillFileReason = "Skill folders must contain SKILL.md at their root."` · `NotConvertibleReason = "Only markdown and text content is merged into the house-rules skill."` |
| 12 | `.../UploadEngineerDraft/HouseRulesSkillGenerator.cs` | `public sealed record HouseRulesGeneration(UploadedFile SkillFile, List<ConvertedItemResult> Converted, string ClaudeMdSnippet);`<br>`public static class HouseRulesSkillGenerator` — `public static HouseRulesGeneration Generate(List<UploadedFile> sources, string skillFolderName)`. `sources` are already ordered (CLAUDE.md first, then ordinal by path — the normalizer orders them). SKILL.md content: front matter `---\nname: {skillFolderName}\ndescription: House rules imported from this creator's CLAUDE.md and rules. Use at the start of every task and whenever writing, reviewing, or planning work in this project so it follows these standards.\n---\n` then `# House Rules` then per source `## Source: {path}` + UTF-8 decoded content. Target path `skills/{skillFolderName}/SKILL.md`. Converted reason const: `MergedIntoHouseRulesReason = "Merged into the generated house-rules skill; always-on and path-scoped behaviour becomes trigger-based."` Snippet const: `ClaudeMdSnippet = "Always read and follow the house-rules skill before doing any work in this project."` |
| 13 | `.../UploadEngineerDraft/SettingsJsonImporter.cs` | `public sealed record SettingsImport(UploadedFile? HooksFile, List<HookWarningResult> HookWarnings, List<SkippedItemResult> Skipped);`<br>`public static class SettingsJsonImporter` — `public static SettingsImport Import(UploadedFile settingsFile, bool hooksFileAlreadyUploaded)`. Behaviour per Decision 14; parse failure → `SettingsImport(null, [], [skipped("settings.json", SettingsUnparseableReason)])`. Public reason consts: `SettingsUnparseableReason = "settings.json could not be parsed."` · `PermissionsSkippedReason = "Plugins cannot carry permissions; they will be shown on the detail page as recommended settings."` · `EnvironmentSkippedReason = "Environment variables have no plugin equivalent."` · `ModelSkippedReason = "Model selection has no plugin equivalent."` · `StatuslineSkippedReason = "Statusline has no plugin equivalent."` · `NoPluginEquivalentReason = "No plugin equivalent."` · `HooksAlreadyUploadedReason = "The upload already contains hooks/hooks.json; the settings.json hooks section was not converted."` The `{"hooks": …}` wrapper carries a comment: `// Plugin hooks.json format: the hooks object is wrapped in a top-level "hooks" property.` |
| 14 | `.../UploadEngineerDraft/UploadEngineerDraftHandler.cs` | `public sealed class UploadEngineerDraftHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IStorageBlobClient storageBlobClient, IOptions<UploadsOptions> uploadsOptions, IOptions<AzureOptions> azureOptions) : IRequestHandler<UploadEngineerDraftCommand, ImportManifestResult>` — steps in **Handlers**. |

### Application — GetImportManifest use case

Namespace: `E3A.Application.Engineers.GetImportManifest;`

| # | Path | Contract |
|---|------|----------|
| 15 | `.../GetImportManifest/GetImportManifestQuery.cs` | `public sealed record GetImportManifestQuery(Guid EngineerId) : IRequest<ImportManifestResult>;` |
| 16 | `.../GetImportManifest/GetImportManifestQueryValidator.cs` | `public sealed class GetImportManifestQueryValidator : AbstractValidator<GetImportManifestQuery>` — `RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);` |
| 17 | `.../GetImportManifest/GetImportManifestQueryHandler.cs` | `public sealed class GetImportManifestQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<GetImportManifestQuery, ImportManifestResult>` — steps in **Handlers**. |

### Tests (`api/E3A.Tests/Engineers/…`, namespaces mirror folders)

| # | Path |
|---|------|
| 18 | `Shared/ZipFixtureFactory.cs` |
| 19 | `Shared/UploadsOptionsFactory.cs` |
| 20 | `UploadEngineerDraft/ClaudeFolderZipReaderTests.cs` |
| 21 | `UploadEngineerDraft/ClaudeFolderSanitizerTests.cs` |
| 22 | `UploadEngineerDraft/UploadPathNormalizerTests.cs` |
| 23 | `UploadEngineerDraft/DraftNormalizerTests.cs` |
| 24 | `UploadEngineerDraft/DraftNormalizerConversionTests.cs` |
| 25 | `UploadEngineerDraft/HouseRulesSkillGeneratorTests.cs` |
| 26 | `UploadEngineerDraft/SettingsJsonImporterTests.cs` |
| 27 | `UploadEngineerDraft/UploadEngineerDraftValidatorTests.cs` |
| 28 | `UploadEngineerDraft/UploadEngineerDraftHandlerGuardTests.cs` |
| 29 | `UploadEngineerDraft/UploadEngineerDraftHandlerTests.cs` |
| 30 | `GetImportManifest/GetImportManifestQueryValidatorTests.cs` |
| 31 | `GetImportManifest/GetImportManifestQueryHandlerTests.cs` |

`ZipFixtureFactory` — `public static class`, `namespace E3A.Tests.Engineers.Shared;`:
`public static byte[] Build(params (string Path, string Content)[] entries)` — `ZipArchive` in `Create` mode over a `MemoryStream`, one entry per tuple, UTF-8 content; returns the array. Plus `public static byte[] BuildWithExternalAttributes(string path, string content, int externalAttributes)` for the symlink test (`externalAttributes = unchecked((int)0xA1FF0000)`), and `public static Stream AsStream(byte[] zipBytes)` returning a `MemoryStream`.

`UploadsOptionsFactory` — `public static UploadsOptions Default()` returning values mirroring the committed appsettings defaults (constitution §2), with optional parameters `int maxFileCount = 400`, `long maxUncompressedSizeBytes = 104857600` for cap tests.

## Configuration (appsettings.json additions)

```json
"Azure": {
  "AACAppSettingsEndpoint": "",
  "ManagedIdentityClientId": "",
  "StorageAccountUrl": "",
  "DraftsBlobContainerName": "drafts"
},
"Uploads": {
  "MaxZipSizeMegabytes": 20,
  "MaxUncompressedSizeBytes": 104857600,
  "MaxFileCount": 400,
  "AllowedExtensions": [ ".md", ".markdown", ".txt", ".json", ".yaml", ".yml", ".toml", ".xml", ".csv", ".html", ".css", ".png", ".jpg", ".jpeg", ".svg", ".sh", ".ps1", ".js", ".py" ],
  "HookScriptExtensions": [ ".sh", ".ps1", ".js", ".py" ],
  "StrippedFileNames": [ "settings.local.json", ".credentials.json", "history.jsonl", ".ds_store", "thumbs.db", "desktop.ini" ],
  "StrippedFileNamePrefixes": [ ".env" ],
  "StrippedFolderNames": [ "memory", "sessions", "session-env", "shell-snapshots", "todos", "logs", "cache", "statsig", "file-history", "projects" ]
}
```

(The `"Azure"` block above shows the final section — only the last two keys are new. Nothing secret: the URL is empty until Mohamed creates the storage account.)

## Error codes

`ErrorCodes.cs` — append to `// Engineers`: `EngineerDraftNotUploaded = "ENGINEER_DRAFT_NOT_UPLOADED"`. New `// Uploads` group:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `UploadFileRequired` | `UPLOAD_FILE_REQUIRED` | `UploadEngineerDraftValidator` | `ValidationBehaviourException` | 422 |
| `UploadFileMustBeZip` | `UPLOAD_FILE_MUST_BE_ZIP` | validator | `ValidationBehaviourException` | 422 |
| `UploadFileTooLarge` | `UPLOAD_FILE_TOO_LARGE` | validator | `ValidationBehaviourException` | 422 |
| `UploadZipInvalid` | `UPLOAD_ZIP_INVALID` | `ClaudeFolderZipReader` | `BadRequestCoreException` | 400 |
| `UploadTooManyFiles` | `UPLOAD_TOO_MANY_FILES` | reader (context `limit`) | `BadRequestCoreException` | 400 |
| `UploadUncompressedTooLarge` | `UPLOAD_UNCOMPRESSED_TOO_LARGE` | reader (context `limit`) | `BadRequestCoreException` | 400 |
| `UploadUnsafePath` | `UPLOAD_UNSAFE_PATH` | reader (context `path`) | `BadRequestCoreException` | 400 |
| `UploadSymlinkNotAllowed` | `UPLOAD_SYMLINK_NOT_ALLOWED` | reader (context `path`) | `BadRequestCoreException` | 400 |
| `UploadFileTypeNotAllowed` | `UPLOAD_FILE_TYPE_NOT_ALLOWED` | `UploadPathNormalizer` (context `path`) | `BadRequestCoreException` | 400 |
| `UploadDuplicatePath` | `UPLOAD_DUPLICATE_PATH` | `UploadPathNormalizer` (context `path`) | `BadRequestCoreException` | 400 |
| `UploadEmpty` | `UPLOAD_EMPTY` | `DraftNormalizer` | `BadRequestCoreException` | 400 |
| `EngineerDraftNotUploaded` | `ENGINEER_DRAFT_NOT_UPLOADED` | `GetImportManifestQueryHandler` | `NotFoundCoreException` | 404 |

Reused (already in `ErrorCodes` + both resx — do not duplicate): `UserNotAuthenticated` (401), `EngineerNotFound` (404), `EngineerNotOwned` (403), `EngineerIdRequired` (422).

Context exceptions use the named argument (`context:`) — the second constructor parameter is `message`. Resource strings (key = code value; keep `{limit}`/`{path}` placeholders intact in both languages; Arabic without tashkeel):

| Key | en | ar |
|-----|----|----|
| `UPLOAD_FILE_REQUIRED` | `An upload file is required.` | `ملف الرفع مطلوب.` |
| `UPLOAD_FILE_MUST_BE_ZIP` | `The upload must be a zip file.` | `يجب ان يكون الملف المرفوع بصيغة zip.` |
| `UPLOAD_FILE_TOO_LARGE` | `The uploaded file exceeds the maximum allowed size.` | `يتجاوز الملف المرفوع الحد الاقصى المسموح به.` |
| `UPLOAD_ZIP_INVALID` | `The uploaded file is not a valid zip archive.` | `الملف المرفوع ليس ملف zip صالحا.` |
| `UPLOAD_TOO_MANY_FILES` | `The upload contains more than {limit} files.` | `يحتوي الملف المرفوع على اكثر من {limit} ملفا.` |
| `UPLOAD_UNCOMPRESSED_TOO_LARGE` | `The upload expands beyond the allowed size of {limit} bytes.` | `يتجاوز حجم المحتوى بعد فك الضغط الحد المسموح {limit} بايت.` |
| `UPLOAD_UNSAFE_PATH` | `The upload contains an unsafe file path: {path}.` | `يحتوي الملف المرفوع على مسار غير امن: {path}.` |
| `UPLOAD_SYMLINK_NOT_ALLOWED` | `Symbolic links are not allowed in uploads: {path}.` | `الروابط الرمزية غير مسموح بها: {path}.` |
| `UPLOAD_FILE_TYPE_NOT_ALLOWED` | `The upload contains a file type that is not allowed: {path}.` | `يحتوي الملف المرفوع على نوع ملف غير مسموح به: {path}.` |
| `UPLOAD_DUPLICATE_PATH` | `The upload contains duplicate entries for the same path: {path}.` | `يحتوي الملف المرفوع على مدخلات مكررة لنفس المسار: {path}.` |
| `UPLOAD_EMPTY` | `The upload contains no usable files.` | `لا يحتوي الملف المرفوع على ملفات قابلة للاستخدام.` |
| `ENGINEER_DRAFT_NOT_UPLOADED` | `No draft has been uploaded for this engineer yet.` | `لم يتم رفع مسودة لهذا المهندس بعد.` |

## Domain behaviour

Append to `Engineer` (after `MarkPublished`, before `Delete`):

```csharp
public void ReplaceDraftManifest(string draftManifestJson)
{
    DraftManifestJson = draftManifestJson;
    UpdationDate = DateTimeOffset.UtcNow;
}
```

No guard: any non-deleted owned engineer may receive a draft (Decision 10), and deleted rows are unreachable through the global filter. Handlers never assign `DraftManifestJson` directly.

## Core.Azure change (exact)

In `Clients/StorageBlobClient.cs`, add to `IStorageBlobClient`:

```csharp
Task DeleteByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken);
```

and to `StorageBlobClient`:

```csharp
public async Task DeleteByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken)
{
    var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUrl), miClient.GetCredential(managedIdentityClientId));
    var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
    await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    await foreach (var blobItem in blobContainerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken).ConfigureAwait(false))
    {
        await blobContainerClient.DeleteBlobIfExistsAsync(blobItem.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
```

`UploadAsync` and everything else in the file are untouched.

## Mapping table (the DraftNormalizer contract)

Applied to sanitized, path-normalized files, in this order; matching case-insensitive:

| Source (after normalization) | Disposition | Target | Category / Reason |
|---|---|---|---|
| `skills/{folder}/**` where `skills/{folder}/SKILL.md` exists | imported 1:1 | same path | `ImportCategories.Skills` |
| `skills/**` otherwise (loose file or folder without SKILL.md) | skipped | — | `SkillMissingSkillFileReason` |
| `agents/**` | imported 1:1 | same path | `Agents` |
| `commands/**` | imported 1:1 | same path | `Commands` |
| `hooks/**` | imported 1:1 | same path | `Hooks` |
| `output-styles/**` | imported 1:1 | same path | `OutputStyles` |
| `monitors/**` | imported 1:1 | same path | `Monitors` |
| `bin/**` | imported 1:1 | same path | `Bin` |
| `themes/**` | imported 1:1 | same path | `Themes` |
| `.mcp.json` (root) | imported 1:1 | `.mcp.json` | `McpServers` |
| `.lsp.json` (root) | imported 1:1 | `.lsp.json` | `LspServers` |
| `CLAUDE.md` (root) + `rules/** conventions/** docs/**` with `.md`/`.markdown`/`.txt` | converted | generated `skills/house-rules/SKILL.md` (or `skills/e3a-house-rules/…` on collision, Decision 16) | `MergedIntoHouseRulesReason`; sets `ClaudeMdSnippet` |
| `rules/** conventions/** docs/**` with any other extension | skipped | — | `NotConvertibleReason` |
| `settings.json` (root) | `SettingsJsonImporter` | `hooks/hooks.json` (imported, source `settings.json#hooks`, category `Hooks`) unless the upload already provided it; other keys → skipped `settings.json#<key>` with the per-key reasons; hook warnings emitted | Decision 14 |
| any other file whose extension ∈ `HookScriptExtensions` | imported 1:1 | same path | `HookScripts` |
| anything else | skipped | — | `NoPluginEquivalentReason` |

After mapping: `Assets` = imported files + generated house-rules SKILL.md + generated hooks.json; empty `Assets` → `BadRequestCoreException(UploadEmpty)`.

## Handlers

Current-user guard is always step 1, verbatim from `UpdateEngineerHandler`:
`var userId = currentUserService.UserId;` → `if (userId == null || userId == Guid.Empty) { throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated); }` → `var ownerUserId = userId.Value;`

**`UploadEngineerDraftHandler.Handle`** (returns `Task<ImportManifestResult>`)

1. Current-user guard.
2. `var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken).ConfigureAwait(false);` (tracking)
3. `null` → `throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);`
4. `engineer.OwnerUserId != ownerUserId` → `throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);`
5. `var options = uploadsOptions.Value;` · `var azure = azureOptions.Value;`
6. `await using var zipStream = request.File.OpenReadStream();` then `var files = ClaudeFolderZipReader.Read(zipStream, options);`
7. `var sanitized = ClaudeFolderSanitizer.Sanitize(files, options);`
8. `var normalizedPaths = UploadPathNormalizer.Normalize(sanitized.Files, options);`
9. `var draft = DraftNormalizer.Normalize(normalizedPaths, sanitized.StrippedPaths, options, DateTimeOffset.UtcNow);`
10. `var blobPrefix = $"{engineer.OwnerUserId}/{engineer.Id}/";`
11. `await storageBlobClient.DeleteByPrefixAsync(azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.DraftsBlobContainerName, blobPrefix, cancellationToken).ConfigureAwait(false);`
12. `foreach` asset in `draft.Assets`: `using var contentStream = new MemoryStream(asset.Content);` → `await storageBlobClient.UploadAsync(contentStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.DraftsBlobContainerName, blobPrefix + asset.Path, cancellationToken).ConfigureAwait(false);`
13. `engineer.ReplaceDraftManifest(JsonSerializer.Serialize(draft.Manifest));` → `engineerRepository.Update(engineer);` → `await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);` (the only save, after all blob work)
14. `return draft.Manifest;`

No try/catch anywhere in the handler.

**`GetImportManifestQueryHandler.Handle`**

1. Current-user guard.
2. `var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
3. `null` → `NotFoundCoreException(ErrorCodes.EngineerNotFound)`.
4. not owner → `ForbiddenCoreException(ErrorCodes.EngineerNotOwned)`.
5. `engineer.DraftManifestJson == null` → `NotFoundCoreException(ErrorCodes.EngineerDraftNotUploaded)`.
6. `return JsonSerializer.Deserialize<ImportManifestResult>(engineer.DraftManifestJson)!;`

## API surface

Added to the existing `EngineersController` (`[ApiController] [Route("api/engineers")] [Authorize]`, no policy — slice ① Decision 12 stands; there is no `DefaultCodes` in this repo, verified):

| Method | Route | Auth | Input | Output |
|---|---|---|---|---|
| POST | `api/engineers/{engineerId:guid}/upload` | `[Authorize]` (owner enforced in handler) | `[FromForm] IFormFile file`, `[FromRoute] Guid engineerId` | `Ok(ImportManifestResult)` |
| GET | `api/engineers/{engineerId:guid}/import-manifest` | `[Authorize]` (owner enforced in handler) | route id | `Ok(ImportManifestResult)` |

```csharp
[HttpPost("{engineerId:guid}/upload")]
public async Task<ActionResult> UploadEngineerDraft([FromRoute] Guid engineerId, [FromForm] IFormFile file, CancellationToken cancellationToken)
{
    var result = await mediator.Send(new UploadEngineerDraftCommand(engineerId, file), cancellationToken);
    return Ok(result);
}

[HttpGet("{engineerId:guid}/import-manifest")]
public async Task<ActionResult> GetImportManifest([FromRoute] Guid engineerId, CancellationToken cancellationToken)
{
    var result = await mediator.Send(new GetImportManifestQuery(engineerId), cancellationToken);
    return Ok(result);
}
```

No `Requests.cs` change (no request record needed — the HTTP shape is route id + file, mirroring Morabh's `FilesController`). 20 MB is under Kestrel's default body limit; no `Program.cs` change.

## Test plan

xUnit + NSubstitute + FluentAssertions **6.12.2** (repo-pinned — do not upgrade). Entities via `EngineerFactory.Draft(ownerUserId)`; manifests set via `engineer.ReplaceDraftManifest(...)`; zips via `ZipFixtureFactory`; options via `UploadsOptionsFactory.Default()` / `Options.Create(...)`. Exception asserts bind to `ErrorCodes.*` via `.Where(x => x.ErrorCode == ErrorCodes.X)`. `IFormFile` is substituted (`OpenReadStream()` returns `ZipFixtureFactory.AsStream(zipBytes)`, `FileName` `"claude.zip"`, `Length` = byte count).

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `EngineerTests` (append) | `ReplaceDraftManifest_ShouldStoreManifestJson_WhenCalled` | `DraftManifestJson` equals input |
| 2 | | `ReplaceDraftManifest_ShouldAdvanceUpdationDate_WhenCalled` | `UpdationDate.Should().BeOnOrAfter(before)` |
| 3 | `ClaudeFolderZipReaderTests` | `Read_ShouldReturnFilesWithNormalizedPaths_WhenZipIsValid` | paths `/`-separated, contents match |
| 4 | | `Read_ShouldSkipDirectoryEntries_WhenZipContainsFolderEntries` | only file entries returned |
| 5 | | `Read_ShouldThrowZipInvalid_WhenStreamIsNotAZip` | `BadRequestCoreException` · `UploadZipInvalid` |
| 6 | | `Read_ShouldThrowTooManyFiles_WhenFileCountExceedsCap` | `UploadTooManyFiles` (factory `maxFileCount: 2`, 3-entry zip) |
| 7 | | `Read_ShouldThrowUncompressedTooLarge_WhenContentExceedsCap` | `UploadUncompressedTooLarge` (tiny cap) |
| 8 | | `Read_ShouldThrowUnsafePath_WhenEntryContainsParentSegment` | `UploadUnsafePath` for `../evil.md` |
| 9 | | `Read_ShouldThrowUnsafePath_WhenEntryPathIsRooted` | `UploadUnsafePath` for `/abs/evil.md` |
| 10 | | `Read_ShouldThrowSymlinkNotAllowed_WhenEntryIsSymlink` | `UploadSymlinkNotAllowed` (ExternalAttributes fixture) |
| 11 | `ClaudeFolderSanitizerTests` | `Sanitize_ShouldStripSettingsLocalJson_WhenPresent` | file removed, path in `StrippedPaths` |
| 12 | | `Sanitize_ShouldStripEnvFiles_WhenNameStartsWithEnvPrefix` | `.env`, `.env.local` stripped |
| 13 | | `Sanitize_ShouldStripFilesInsideStrippedFolders_WhenAnySegmentMatches` | `memory/x.md`, `skills/a/sessions/y.md` stripped |
| 14 | | `Sanitize_ShouldStripOsJunk_WhenPresent` | `.DS_Store`, `Thumbs.db` stripped |
| 15 | | `Sanitize_ShouldMatchCaseInsensitively_WhenNamesDifferByCase` | `SETTINGS.LOCAL.JSON` stripped |
| 16 | | `Sanitize_ShouldKeepAllFilesAndRecordNothing_WhenNothingMatches` | files intact, `StrippedPaths` empty |
| 17 | `UploadPathNormalizerTests` | `Normalize_ShouldUnwrapSingleRootFolder_WhenRootIsNotRecognized` | `upload/skills/a/SKILL.md` → `skills/a/SKILL.md` |
| 18 | | `Normalize_ShouldUnwrapNestedRoots_WhenZipWrapsRepoAndClaudeFolder` | `myrepo/.claude/agents/x.md` → `agents/x.md` |
| 19 | | `Normalize_ShouldNotUnwrap_WhenRootIsRecognizedFolder` | `skills/...` unchanged |
| 20 | | `Normalize_ShouldStripClaudePrefix_WhenClaudeFolderSitsBesideClaudeMd` | `CLAUDE.md` + `.claude/skills/...` → `CLAUDE.md` + `skills/...` |
| 21 | | `Normalize_ShouldThrowDuplicatePath_WhenTwoFilesCollide` | `UploadDuplicatePath` |
| 22 | | `Normalize_ShouldThrowFileTypeNotAllowed_WhenExtensionIsNotAllowed` | `UploadFileTypeNotAllowed` for `tool.exe`; also covers extension-less `bin/mytool` via second `[Theory]` case |
| 23 | `DraftNormalizerTests` | `Normalize_ShouldImportRecognizedFolders_WhenPresent` | `[Theory]`: (`agents/a.md`, Agents), (`commands/c.md`, Commands), (`hooks/h.sh`, Hooks), (`output-styles/o.md`, OutputStyles), (`monitors/m.sh`, Monitors), (`bin/b.sh`, Bin), (`themes/t.json`, Themes) — imported entry source==target, category |
| 24 | | `Normalize_ShouldImportSkillFolder_WhenSkillFileAtRoot` | `skills/a/SKILL.md` + `skills/a/ref.md` imported, category Skills |
| 25 | | `Normalize_ShouldSkipSkillFiles_WhenSkillFileMissing` | `skills/a/notes.md` skipped with `SkillMissingSkillFileReason` |
| 26 | | `Normalize_ShouldImportRootConfigurationFiles_WhenMcpAndLspPresent` | `.mcp.json` → McpServers, `.lsp.json` → LspServers |
| 27 | | `Normalize_ShouldImportLooseHookScripts_WhenScriptExtensionOutsideRecognizedRoots` | `scripts/check.sh` imported, category HookScripts |
| 28 | | `Normalize_ShouldSkipUnknownFiles_WithNoPluginEquivalentReason` | `README.md` at root skipped |
| 29 | | `Normalize_ShouldThrowUploadEmpty_WhenNoAssetsRemain` | only-skipped input → `UploadEmpty` |
| 30 | | `Normalize_ShouldSetUploadedAtAndStrippedPaths_WhenManifestGenerated` | `UploadedAt` == passed value; `StrippedPaths` passed through |
| 31 | `DraftNormalizerConversionTests` | `Normalize_ShouldGenerateHouseRulesSkill_WhenClaudeMdAndRuleFoldersPresent` | asset `skills/house-rules/SKILL.md` exists; converted entries for `CLAUDE.md` + `rules/x.md`; `ClaudeMdSnippet` == generator const |
| 32 | | `Normalize_ShouldPrefixGeneratedSkill_WhenUploadAlreadyContainsHouseRules` | target `skills/e3a-house-rules/SKILL.md` |
| 33 | | `Normalize_ShouldSkipNonTextFilesUnderRuleFolders_WhenPresent` | `docs/diagram.png` skipped with `NotConvertibleReason` |
| 34 | | `Normalize_ShouldPreferUploadedHooksJson_WhenBothSourcesPresent` | uploaded `hooks/hooks.json` imported; skipped entry `settings.json#hooks` with `HooksAlreadyUploadedReason` |
| 35 | | `Normalize_ShouldReturnNullSnippetAndNoConversion_WhenNoHouseRuleSources` | `ClaudeMdSnippet` null, `Converted` empty |
| 36 | `HouseRulesSkillGeneratorTests` | `Generate_ShouldEmitFrontMatterAndAllSources_WhenSourcesProvided` | content contains `name: house-rules`, `description:`, each `## Source: {path}` + body |
| 37 | | `Generate_ShouldTargetGivenFolder_WhenFolderNameProvided` | `skills/e3a-house-rules/SKILL.md` path + matching front-matter name |
| 38 | `SettingsJsonImporterTests` | `Import_ShouldProduceHooksFileAndWarnings_WhenHooksSectionIsWellFormed` | `HooksFile.Path == "hooks/hooks.json"`; content JSON has top-level `hooks`; one `HookWarningResult("PreToolUse", "Bash", command)` |
| 39 | | `Import_ShouldWarnPerEventWithoutCommand_WhenHookShapeIsUnrecognized` | warning with `Event` set, `Matcher`/`Command` null |
| 40 | | `Import_ShouldSkipKnownSettingsKeys_WithReasons` | `settings.json#permissions/env/model/statusLine` skipped with their consts |
| 41 | | `Import_ShouldReturnSkippedOnly_WhenJsonIsInvalid` | `HooksFile` null; one skipped `settings.json` with `SettingsUnparseableReason` |
| 42 | | `Import_ShouldSkipHooksSection_WhenHooksFileAlreadyUploaded` | `HooksFile` null; skipped `settings.json#hooks` |
| 43 | `UploadEngineerDraftValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true (substituted `.zip` file, small length) |
| 44 | | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | `EngineerIdRequired` |
| 45 | | `Validate_ShouldFail_WhenFileIsEmpty` | `Length` 0 → `UploadFileRequired` |
| 46 | | `Validate_ShouldFail_WhenFileIsNotZip` | `FileName "x.rar"` → `UploadFileMustBeZip` |
| 47 | | `Validate_ShouldFail_WhenFileExceedsMaxSize` | `Length` 21 MB → `UploadFileTooLarge` |
| 48 | `UploadEngineerDraftHandlerGuardTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsMissing` | `UnauthorizedCoreException` · `UserNotAuthenticated`; `DidNotReceive` Save |
| 49 | | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `EngineerNotFound`; no Save |
| 50 | | `Handle_ShouldThrowForbidden_WhenEngineerIsNotOwned` | `EngineerNotOwned`; no Save |
| 51 | | `Handle_ShouldNotTouchBlobOrSave_WhenZipIsInvalid` | `UploadZipInvalid`; `DidNotReceive` `DeleteByPrefixAsync`/`UploadAsync`/`SaveChangesAsync` |
| 52 | `UploadEngineerDraftHandlerTests` | `Handle_ShouldDeletePriorAssets_WhenUploadIsValid` | `Received(1).DeleteByPrefixAsync(...)` with prefix `$"{ownerUserId}/{engineer.Id}/"` and options container/url values |
| 53 | | `Handle_ShouldUploadNormalizedAssets_WhenUploadIsValid` | `Received().UploadAsync(...)` per expected asset blob name (fixture: `skills/a/SKILL.md` + `CLAUDE.md` → skill + generated house-rules) |
| 54 | | `Handle_ShouldPersistManifestAndReturnIt_WhenUploadIsValid` | `engineer.DraftManifestJson` deserializes back to the returned manifest; `Received(1).SaveChangesAsync`; `UploadedAt` on/after captured `before` |
| 55 | `GetImportManifestQueryValidatorTests` | `Validate_ShouldPass_WhenQueryIsValid` | true |
| 56 | | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | `EngineerIdRequired` |
| 57 | `GetImportManifestQueryHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsMissing` | `UserNotAuthenticated` |
| 58 | | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `EngineerNotFound` |
| 59 | | `Handle_ShouldThrowForbidden_WhenEngineerIsNotOwned` | `EngineerNotOwned` |
| 60 | | `Handle_ShouldThrowNotFound_WhenDraftNotUploaded` | `EngineerDraftNotUploaded` |
| 61 | | `Handle_ShouldReturnManifest_WhenDraftUploaded` | round-trips a manifest set via `ReplaceDraftManifest(JsonSerializer.Serialize(...))` |

No repository, controller, EF-configuration, or `Core.Azure` tests (conventions §5, Decision 25). No existing test is modified except the `EngineerTests` append.

## Definition of done

- [ ] `IStorageBlobClient`/`StorageBlobClient` gained exactly one method, `DeleteByPrefixAsync`, with the body specified above; no other `core-libraries` change; no Azure SDK type referenced anywhere in `E3A.*`.
- [ ] `Engineer.ReplaceDraftManifest` exists, sets `UpdationDate`; handlers never assign entity properties directly.
- [ ] 17 new production files exist at the exact paths above with the exact type names/signatures; no additional production files.
- [ ] No new interfaces, exception types, services, or repository members; engine classes are static; no DI registrations beyond the two `Configure<>` lines.
- [ ] All caps/lists come from `UploadsOptions`/`AzureOptions` bound in `E3A.Application/DependencyInjection.cs`; recognized root names and generated-file names are named constants with WHY comments; zero inline magic values.
- [ ] Upload flow order: guard → ownership → read (caps, traversal, symlink) → sanitize → path-normalize (duplicates, extensions) → map → delete blob prefix → upload assets → `ReplaceDraftManifest` → single `SaveChangesAsync` → return manifest.
- [ ] Replace-upload works because delete-prefix precedes upload (`UploadAsync` does not overwrite).
- [ ] Manifest JSON round-trips: what upload stores, GET returns; `StrippedPaths`, `HookWarnings`, `ClaudeMdSnippet`, `UploadedAt` populated per this plan.
- [ ] The 12 new `ErrorCodes` constants exist with the exact values, and all 12 keys are present in **both** resx files with placeholders intact.
- [ ] Controller: exactly two new thin actions at `POST …/upload` and `GET …/import-manifest`, `[FromForm] IFormFile file`, `CancellationToken` passed to `Send`; `Program.cs` and middleware order untouched.
- [ ] `appsettings.json`: `Azure` section extended (empty URL — nothing secret), `Uploads` section matches the JSON above verbatim.
- [ ] No EF migration added; `E3A.Infrastructure` byte-identical.
- [ ] All 61 tests above exist with the exact names, pass, and no existing test breaks; FluentAssertions stays 6.12.2.
- [ ] `dotnet build` zero new warnings; every file ≤ ~100 lines (split test classes are already sized for this); file-scoped namespaces; `.ConfigureAwait(false)` everywhere required.
- [ ] No `/docs` edit needed — verified this slice matches plugin-spec, security-scan, implementation-plan, and architecture as written (incompleteness closes; nothing diverges).
