# Plan — Publish Pipeline

## Goal

A creator who has uploaded a `.claude` folder can call `POST /api/engineers/{engineerId}/publish` with a
version increment, get `202` with a version id, and poll `GET /api/publish/{versionId}/status` until the
version reports `Published`. At that point an immutable zip exists at
`https://e3a.dev/z/e3a-{slug}/{semanticVersion}.zip`, a pinned single-plugin marketplace exists at
`/m/e3a-{slug}/{semanticVersion}/marketplace.json`, and the root `/marketplace.json` lists the engineer —
so `/plugin marketplace add https://e3a.dev/marketplace.json` followed by `/plugin install e3a-{slug}`
installs it into Claude Code. The creator can also `Unlist` a published engineer to remove it from
discovery without breaking existing installs, and `Relist` it.

## Scope

**In:**
1. `ItemVersion` aggregate (`E3A.Domain/Publishing/`), `IItemVersionRepository`, EF configuration, migration `versions002`.
2. `POST /api/engineers/{engineerId}/publish` → `202 Accepted` + `PublishStatusResult`; body `{ "increment": "Patch|Minor|Major" }`.
3. `PublishRequestedDomainEvent` → `PublishRequestedEventHandler` → `IStorageQueueClient.SendMessageAsync(..., visibilityTimeout)` on queue `publish-jobs` (mirrors Morabh `OrderWorkflowNotificationEventHandler`).
4. New project `api/E3A.Jobs` — isolated Functions v4 / .NET 10 worker with one `[QueueTrigger]` function (mirrors `Morabh.Jobs`), added to `api/E3A.slnx`.
5. Worker pipeline: load version → ignore unless `Queued`/`Building` → `Building` → freeze `drafts/{ownerUserId}/{engineerId}/**` → `snapshots/{versionId}/**` → assemble plugin tree from snapshot + `FrozenManifestJson` → structure validation → deterministic zip + sha256 → upload `public/z/{pluginName}/{semanticVersion}.zip` → `Published` + `engineer.MarkPublished(...)` → pinned marketplace → root marketplace regeneration.
6. `GET /api/publish/{versionId}/status`.
7. `EngineerStatus.Unlisted` + `POST /api/engineers/{id}/unlist` and `POST /api/engineers/{id}/relist`, blocked while a version is `Queued`/`Building`, each regenerating the root marketplace.
8. Additive `Core.Azure.IStorageBlobClient` members: `UploadAsync` overload (content type + cache control + overwrite flag), `ListByPrefixAsync`, `DownloadAsync`.
9. New `Publishing` options section + four new `Azure` keys.
10. Postman: 4 new requests. Docs sync: `architecture.md`, `implementation-plan.md`, `plugin-spec.md`.

**Out:** security scanner and the `Rejected` status path (next slice) · teams (`ItemType.Team` exists in the
enum but nothing constructs it) · frontend publish UI · Cloudflare Worker/CDN config · install-count
tracking · takedown/delete of published zips.

**Deferred:**
- Scanner step between structure validation and zip — `security-scan` slice; the worker gains exactly one call between step 7 and step 8 of `ProcessPublishJobHandler`.
- `ScanReportJson` column — lands with the scanner, which knows its own shape. Adding it blind now would be a guessed schema.
- Recovery of a version stranded in `Building` after `maxDequeueCount` retries (operational tooling, no product surface yet).

## Scale assessment (read this first)

47 production files + 22 test files. This is the largest slice in the pipeline so far and every acceptance
item is genuinely load-bearing for "installable", **except** items 6 (`GetPublishStatus`) and 7
(unlist/relist), which together are ~12 production files. Both are cheap and both are pinned by the
acceptance doc, so they stay in scope — but they are the designated cut line. **Build in the order given
in "Build order" below**: after step 6 the pipeline is end-to-end functional, and steps 7–8 are additive.
If a pass runs out of room, stop at a step boundary with a green build rather than leaving a half-built
worker.

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Where does the worker's logic live? | All of it in `E3A.Application/Publishing/*` as MediatR handlers. `E3A.Jobs` holds only `Program.cs`, `host.json`, `E3A.Jobs.csproj` and a `ProcessPublishJobFunction` that sends two commands. | `E3A.Tests` references only `E3A.Application` + `E3A.Domain`. Logic in `E3A.Jobs` would be untestable. Mirrors `Morabh.Jobs`, whose functions are thin `mediator.Send` shells. |
| D2 | How is root-marketplace regeneration sequenced after a publish? | The Function sends `ProcessPublishJobCommand` then, unconditionally, `RegenerateMarketplaceCommand`. Two `Send` calls, **no branch in the Function**. | A branch in a Function is untested code (functions are out of test scope, same as controllers). Regeneration is a pure projection of committed DB state, so running it after an ignored or already-published job is a harmless idempotent rewrite. If the job throws, the second `Send` never runs and the whole message retries. |
| D3 | Worker guard — acceptance says "ignore unless `Queued`" | Ignore unless status is `Queued` **or** `Building`. | Literal "`Queued` only" makes queue retries useless: a transient blob failure after the `Building` checkpoint would make every retry a no-op and strand the version forever. The intent of the rule is "never reprocess a terminal version"; `Queued or Building` expresses that correctly. |
| D4 | Zip re-upload on retry vs. immutability (decision #10) | Before uploading, call `ListByPrefixAsync` on the exact zip blob name. If it already exists, skip the upload and use the locally computed sha256/size. Otherwise upload with `overwrite: false`. | The blob path contains `{pluginName}/{semanticVersion}`, and `(ItemType, ItemId, VersionNumber)` is uniquely indexed, so the only possible prior writer of that exact path is a previous attempt of **this same version**. Deterministic zipping makes those bytes identical. Immutability is preserved and retries work, without `try`/`catch`. |
| D5 | Concurrent publishes racing on `marketplace.json` | `host.json` sets `extensions.queues.batchSize = 1` and `newBatchThreshold = 0` — publish jobs process serially. | Eliminates the read-modify-write race between two regenerations. At v0.1 volume serialisation costs nothing. Cheaper and more certain than any lease/ETag scheme. |
| D6 | RC4 "staged-prefix atomic replace" | Not implemented as a staged prefix. Instead: build the entire document in memory, then one `UploadAsync(overwrite: true)`. | A single-shot PUT Blob is atomic in Azure Storage — readers see the old or the new blob, never a partial one. `marketplace.json` will never approach the 256 MiB multi-block threshold. Combined with D5, RC4's actual failure mode (a torn or truncated marketplace) cannot occur. Recorded here so the reviewer does not read the missing staged prefix as an unaddressed carry-over. |
| D7 | RC17 bounded pagination | `RegenerateMarketplaceHandler` loops `FindPaginatedAsync` with `PublishingOptions.MarketplacePageSize`, hard-stopping at `MarketplaceMaxPages`; hitting the cap throws `InternalServerErrorCoreException(ErrorCodes.MarketplaceEngineerLimitExceeded)`. | Silent truncation would silently delist real engineers. Failing loudly turns a capacity problem into an alert instead of a data-loss bug. |
| D8 | Domain-method guards + `ErrorCodes` | State-transition guards live in the **handlers**, throwing `BusinessRuleViolationCoreException(ErrorCodes.X)` / `ConflictCoreException`. Domain methods are unguarded mutators that set `UpdationDate`. | `E3A.Domain` does not (and must not) reference `E3A.Application`, so `ErrorCodes` is unreachable from an entity, and `Core.Errors` has no `BusinessRuleViolationException` — only `BusinessRuleViolationCoreException`. The repo's own precedent is `UpdateEngineerHandler.cs:68`, which throws `EngineerSlugFrozen` from the handler. Mirror it; do not invent a domain error-code registry. |
| D9 | `IRepository<User>` for author attribution | Add `IUserRepository : IRepository<User>` + `UserRepository(AppDbContext) : Repository<User>(context), IUserRepository`. | The open generic `IRepository<>` **is** registered by `AddCoreEntityFrameworkCore`, but `Repository<T>` takes a `DbContext` and only `AppDbContext` is registered as a service — resolving `IRepository<User>` would fail at runtime. Two files mirroring `EngineerRepository` exactly. |
| D10 | `author.name` before OAuth (acceptance #6 says "creator's DisplayName") | `user.UserName`, falling back to `engineer.Slug` when null/empty. `author.url` = `{PublicSiteUrl}/e/{slug}`. **DEV-DECISION** — flagged, not blocking. | `E3A.Domain/Identity/User.cs` is a bare `IdentityUser<Guid>` with **no `DisplayName` property**. Adding one is an Identity migration and scope creep. `UserName` is Identity's existing unique handle and is exactly what the GitHub login will be written into when the OAuth slice lands, so nothing has to move later. |
| D11 | Manifest's role in assembly | `PluginTreeAssembler` builds the allowed target-path set from `manifest.Imported[].TargetPath ∪ manifest.Converted[].TargetPath` and emits only snapshot assets in that set. A manifest target path with no matching snapshot asset fails validation with `PluginManifestAssetMissing`. | Gives `FrozenManifestJson` a real, testable job (the creator publishes exactly what the manifest showed them) and catches drafts/snapshot drift instead of silently shipping a different tree. |
| D12 | Snapshot round-trip | Download each draft blob once, upload the bytes to `snapshots/{versionId}/...`, and assemble from those **in-memory** bytes rather than re-downloading from the snapshot container. | Byte-identical by construction; halves blob traffic. The upload handler already holds a whole draft in memory under the same 100 MB / 400 file caps. |
| D13 | New `PluginFile` record vs. reusing `UploadedFile` | New `sealed record PluginFile(string Path, byte[] Content)` in `Publishing/Shared/`. | Reusing `E3A.Application.Engineers.UploadEngineerDraft.UploadedFile` would couple the publish pipeline to the upload slice's internals across areas. One five-line record is cheaper than that coupling. |
| D14 | Failed-publish diagnostics | `ItemVersion.FailureReason` (nullable string) holds `ErrorCodes` constants joined by `", "`. Surfaced raw by `GetPublishStatus`. | A joined list cannot pass through `ILocalizer.GetMessage`. Returning the codes keeps the contract machine-readable; the web app maps codes to copy. New column, so `implementation-plan.md`'s `versions` row must be updated (docs sync). |
| D15 | Queue name in the trigger attribute | `[QueueTrigger("%Azure:PublishQueueName%", Connection = "StorageAccountConnection")]`. | Morabh hardcodes `"orderworkflownotifications"`, but `docs/constitution.md` §0.3 names "container/queue names" as tunables that must be configuration, and the constitution wins on conflict. `%Section:Key%` is the standard Functions binding-expression form, so this is still idiomatic. |
| D16 | `SaveChangesAsync` count in `ProcessPublishJobHandler` | **At most two**, and never more than two on any path: the `Building` checkpoint and the terminal (`Published` or `Failed`) write. | The single-save rule exists so handlers don't dribble writes. Here the `Building` checkpoint must be visible to `GetPublishStatus` before minutes of blob work. Documented so the reviewer does not read it as a violation. |
| D17 | Version-increment enum placement | `VersionIncrement`, `ItemType`, `ItemVersionStatus` all in `E3A.Domain/Publishing/`, one file each. | Mirrors `E3A.Domain/Engineers/EngineerStatus.cs`. `VersionIncrement` is bound directly by the API request, which is fine — `EngineerStatus` is already exposed the same way. |
| D18 | `marketplace.json` wrapper shape | `{ "name": <MarketplaceName>, "owner": { "name": <MarketplaceOwnerName>, "url": <PublicSiteUrl> }, "plugins": [ … ] }`. Pinned per-version file uses the identical wrapper with a single-element `plugins` array. | `docs/plugin-spec.md` documents only the entry object, not the wrapper Claude Code requires. Decided here and added to `plugin-spec.md` as part of docs sync. |
| D19 | Extra `Core.Azure` methods beyond the announced overload | `ListByPrefixAsync` and `DownloadAsync` are added alongside the announced `UploadAsync` overload. Announced here as a second core change. | The freeze step is impossible without list + download; `IStorageBlobClient` currently has only upload and delete-by-prefix. Both additions are purely additive, on the interface that already got `DeleteByPrefixAsync` approved, and no existing signature changes. |
| D20 | Domain event raised by which aggregate | `ItemVersion.Create(...)` raises `PublishRequestedDomainEvent(id)` on the new instance. | The `ItemVersion` is `Added`, so it is guaranteed present in `ChangeTracker.Entries<Entity>()`. Raising from `Engineer` would rely on an otherwise-`Unchanged` entry staying tracked. |
| D21 | Unlist/relist and the marketplace | `UnlistEngineerHandler` / `RelistEngineerHandler` inject `ISender` and `Send(new RegenerateMarketplaceCommand(), cancellationToken)` inline after saving. | Unlist must actually stop discovery (acceptance decision #3), and the regeneration is one paginated read plus one small blob PUT. Routing it through the queue would need a second message shape and a branch in the Function (see D2). Testable via a substituted `ISender`. |

## Existing code touched

| File | Change |
|------|--------|
| `api/core-libraries/Core.Azure/Clients/StorageBlobClient.cs` | Add three members to `IStorageBlobClient` + `StorageBlobClient` (D19). Existing two signatures untouched. |
| `api/E3A.Domain/Engineers/EngineerStatus.cs` | Add `Unlisted` between `Published` and `Deleted`. |
| `api/E3A.Domain/Engineers/Engineer.cs` | Add `Unlist()` and `Relist()`. |
| `api/E3A.Application/Options/AzureOptions.cs` | Add `StorageAccountQueueUrl`, `SnapshotsBlobContainerName`, `PublicBlobContainerName`, `PublishQueueName`. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Add the `// Publishing` group (see Error codes). |
| `api/E3A.Application/DependencyInjection.cs` | `services.Configure<PublishingOptions>(configuration.GetSection(PublishingOptions.SectionName));` |
| `api/E3A.Infrastructure/DependencyInjection.cs` | Register `IItemVersionRepository → ItemVersionRepository` and `IUserRepository → UserRepository`. |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | `DbSet<ItemVersion> ItemVersions { get; set; }`; new `ConfigureItemVersions(modelBuilder)` private method called from `OnModelCreating`; `modelBuilder.Entity<ItemVersion>().HasQueryFilter(x => !x.IsDeleted);` in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. Constructor gains `IOptions<PublishingOptions> publishingOptions`. |
| `api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add versions002`. |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | Add `PublishEngineer`, `UnlistEngineer`, `RelistEngineer` actions. |
| `api/E3A.Api/Controllers/Engineers/Requests.cs` | Add `public sealed record PublishEngineerRequest(VersionIncrement Increment);` |
| `api/E3A.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | One `<data>` entry per new error code. |
| `api/E3A.Api/appsettings.json` | Add the `Publishing` section and `Azure:PublishQueueName`. (`SnapshotsBlobContainerName`, `PublicBlobContainerName`, `StorageAccountQueueUrl` are already present.) |
| `api/E3A.slnx` | `<Project Path="E3A.Jobs/E3A.Jobs.csproj" />` after `E3A.Infrastructure`. **Required** — `tools/E3A.Seeder` is absent from the solution and that has already produced bugs a solution build cannot catch. |
| `postman/e3a.postman_collection.json` | Add "Publish Engineer", "Unlist Engineer", "Relist Engineer" to the `Engineers` folder; add a new `Publishing` folder with "Get Publish Status". |
| `docs/architecture.md` | See Docs sync. |
| `docs/implementation-plan.md` | See Docs sync. |
| `docs/plugin-spec.md` | See Docs sync. |

## Files to create

### E3A.Domain — namespace `E3A.Domain.Publishing`

| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/E3A.Domain/Publishing/ItemType.cs` | enum | `public enum ItemType { Engineer, Team }` |
| 2 | `api/E3A.Domain/Publishing/ItemVersionStatus.cs` | enum | `public enum ItemVersionStatus { Queued, Building, Published, Rejected, Failed }` — `Rejected` is declared but unreachable until the scanner slice. |
| 3 | `api/E3A.Domain/Publishing/VersionIncrement.cs` | enum | `public enum VersionIncrement { Patch, Minor, Major }` |
| 4 | `api/E3A.Domain/Publishing/PublishRequestedDomainEvent.cs` | sealed record | `public sealed record PublishRequestedDomainEvent(Guid VersionId) : DomainEvent();` — this record **is** the queue message payload. |
| 5 | `api/E3A.Domain/Publishing/ItemVersion.cs` | class : `AuditEntity` | See Domain behaviour. |
| 6 | `api/E3A.Domain/Publishing/IItemVersionRepository.cs` | interface | `public interface IItemVersionRepository : IRepository<ItemVersion> { }` — empty; `IRepository<T>` covers every query needed (`GetByIdAsync`, `FirstOrDefaultAsync` with `orderBy`, `FindAsync`, `CountAsync`). |
| 7 | `api/E3A.Domain/Identity/IUserRepository.cs` | interface | `public interface IUserRepository : IRepository<User> { }` |

### E3A.Application — options

| # | Path | Type | Contract |
|---|------|------|----------|
| 8 | `api/E3A.Application/Options/PublishingOptions.cs` | `sealed class` | `public const string SectionName = "Publishing";` then `int MaxVersionsPerItem`, `int QueueVisibilityTimeoutSeconds`, `string PublicSiteUrl`, `string MarketplaceName`, `string MarketplaceOwnerName`, `string MarketplaceCacheControl`, `string ZipCacheControl`, `int MarketplacePageSize`, `int MarketplaceMaxPages`, `int MaxPluginFileCount`, `long MaxPluginBytes`, `int SemanticVersionMaxLength`, `int BlobPathMaxLength`, `int FailureReasonMaxLength`. All `{ get; set; }`; strings default `string.Empty`. |

### E3A.Application/Publishing/Shared — namespace `E3A.Application.Publishing.Shared`

| # | Path | Type | Contract |
|---|------|------|----------|
| 9 | `PluginFile.cs` | sealed record | `public sealed record PluginFile(string Path, byte[] Content);` |
| 10 | `PluginName.cs` | static class | `private const string Prefix = "e3a-";` with a WHY comment (the installed plugin identity — changing it breaks every existing install). `public static string For(string slug)` → `$"{Prefix}{slug}"`. |
| 11 | `PublishBlobPaths.cs` | static class | Consts: `ZipContentType = "application/zip"`, `MarketplaceContentType = "application/json"`, `RootMarketplaceBlobName = "marketplace.json"`. Methods (all block-bodied): `static string DraftPrefix(Guid ownerUserId, Guid engineerId)` → `$"{ownerUserId}/{engineerId}/"` · `static string SnapshotPrefix(Guid versionId)` → `$"{versionId}/"` · `static string Zip(string pluginName, string semanticVersion)` → `$"z/{pluginName}/{semanticVersion}.zip"` · `static string PinnedMarketplace(string pluginName, string semanticVersion)` → `$"m/{pluginName}/{semanticVersion}/marketplace.json"` · `static string ZipUrl(string publicSiteUrl, string zipBlobPath)` → `$"{publicSiteUrl.TrimEnd('/')}/{zipBlobPath}"`. |
| 12 | `SemanticVersionCalculator.cs` | static class | `public static string Next(string? previousSemanticVersion, VersionIncrement increment)`. Returns `"1.0.0"` when `previousSemanticVersion` is null, whitespace, or not three dot-separated non-negative integers. Otherwise switch expression: `Patch` → `major.minor.(patch+1)`, `Minor` → `major.(minor+1).0`, `Major` → `(major+1).0.0`. `CultureInfo.InvariantCulture` on every parse/format. No throw. |
| 13 | `PluginJsonSerializer.cs` | static class | `private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };` and `public static string Serialize<T>(T value)`. Single serialization policy for every artefact the pipeline writes. |
| 14 | `PluginManifest.cs` | sealed records | `public sealed record PluginManifest(string Name, string Version, string? Description, PluginAuthor Author);` and `public sealed record PluginAuthor(string Name, string Url);` |
| 15 | `PluginJsonGenerator.cs` | static class | `public const string PluginJsonPath = ".claude-plugin/plugin.json";` (WHY: the exact path Claude Code's loader resolves). `public static PluginFile Generate(Engineer engineer, string semanticVersion, string authorName, PublishingOptions options)` → builds `PluginManifest(PluginName.For(engineer.Slug), semanticVersion, engineer.Description, new PluginAuthor(authorName, $"{options.PublicSiteUrl.TrimEnd('/')}/e/{engineer.Slug}"))`, serializes via `PluginJsonSerializer`, returns `new PluginFile(PluginJsonPath, Encoding.UTF8.GetBytes(json))`. |
| 16 | `PluginTreeAssembler.cs` | static class | `public static List<PluginFile> Assemble(List<PluginFile> snapshotAssets, ImportManifestResult manifest, Engineer engineer, string semanticVersion, string authorName, PublishingOptions options)`. Steps: (1) `allowed` = `HashSet<string>(manifest.Imported.Select(x => x.TargetPath).Concat(manifest.Converted.Select(x => x.TargetPath)), StringComparer.OrdinalIgnoreCase)`; (2) keep only snapshot assets whose `Path` is in `allowed`; (3) append `PluginJsonGenerator.Generate(...)`; (4) return ordered by `Path` with `StringComparer.Ordinal`. Does **not** throw — missing assets are caught by the validator (D11). |
| 17 | `PluginStructureValidator.cs` | static class | `public static List<string> Validate(List<PluginFile> files, ImportManifestResult manifest, PublishingOptions options)` returns `ErrorCodes` constants (empty list = valid). Rules in order: `PluginManifestAssetMissing` when any allowed manifest target path is absent from `files` · `PluginNoInstallableContent` when no file path starts with `agents/`, `skills/` or `commands/` · `PluginUnsafePath` when any path is empty, rooted (`/`), contains `\`, or contains a `..` segment · `PluginSkillMissingSkillFile` when a `skills/{folder}/` group has no `skills/{folder}/SKILL.md` · `PluginTooManyFiles` when `files.Count > options.MaxPluginFileCount` · `PluginTooLarge` when `files.Sum(x => x.Content.LongLength) > options.MaxPluginBytes`. Each code appears at most once. |
| 18 | `DeterministicZipper.cs` | static class + record | `public sealed record ZippedPlugin(byte[] Content, string Sha256, long SizeBytes);` and `public static ZippedPlugin Create(List<PluginFile> files)`. Invariant const with WHY comment: `private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);` (MS-DOS zip epoch — the earliest representable stamp; a wall-clock stamp would change the sha256 on every run). Implementation: `MemoryStream` → `ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8)`; iterate `files.OrderBy(x => x.Path, StringComparer.Ordinal)`; `archive.CreateEntry(file.Path, CompressionLevel.Optimal)`; set `entry.LastWriteTime = DeterministicTimestamp`; write bytes; no directory entries; `'/'` separators only. Sha256 = lowercase hex of `SHA256.HashData(bytes)`. `SizeBytes` = zip length. |
| 19 | `DraftSnapshotFreezer.cs` | static class | `public static async Task<List<PluginFile>> FreezeAsync(IStorageBlobClient storageBlobClient, AzureOptions azureOptions, Guid ownerUserId, Guid engineerId, Guid versionId, CancellationToken cancellationToken)`. Steps: list `DraftPrefix(...)` in `DraftsBlobContainerName`; for each blob name, `DownloadAsync`, skip nulls, relative path = name minus prefix; `UploadAsync(stream, …, SnapshotsBlobContainerName, SnapshotPrefix(versionId) + relativePath, contentType: MarketplaceContentType? no — use the 5-arg existing overload, overwrite not required)`; collect `new PluginFile(relativePath, bytes)`; return ordered by `Path`, `StringComparer.Ordinal`. Uses the **existing** 6-arg `UploadAsync` (snapshots are private, no cache headers, and a re-freeze of the same version writes the same paths — call `DeleteByPrefixAsync(SnapshotPrefix(versionId))` first so a retry cannot hit the no-overwrite default). `.ConfigureAwait(false)` on every await. |
| 20 | `MarketplaceDocument.cs` | sealed records | `public sealed record MarketplaceDocument(string Name, MarketplaceOwner Owner, List<MarketplacePlugin> Plugins);` · `public sealed record MarketplaceOwner(string Name, string Url);` · `public sealed record MarketplacePlugin(string Name, string? Description, string Version, PluginAuthor Author, List<string> Keywords, MarketplaceSource Source);` · `public sealed record MarketplaceSource(string Source, string Url, string Sha256);` — `Source` is always `"archive"` (WHY const in the generator: relative paths do not resolve for URL-added marketplaces). |
| 21 | `MarketplaceDocumentGenerator.cs` | static class | `private const string ArchiveSourceType = "archive";` (WHY comment as above). `public static MarketplacePlugin GeneratePlugin(Engineer engineer, ItemVersion version, string authorName, PublishingOptions options)` → name `PluginName.For(engineer.Slug)`, `engineer.Description`, `version.SemanticVersion`, `new PluginAuthor(authorName, $"{PublicSiteUrl}/e/{slug}")`, `[.. engineer.Tags]`, `new MarketplaceSource(ArchiveSourceType, PublishBlobPaths.ZipUrl(options.PublicSiteUrl, version.ZipBlobPath!), version.ZipSha256!)`. `public static string Generate(List<MarketplacePlugin> plugins, PublishingOptions options)` → serializes `new MarketplaceDocument(options.MarketplaceName, new MarketplaceOwner(options.MarketplaceOwnerName, options.PublicSiteUrl), plugins)` via `PluginJsonSerializer`. |
| 22 | `PublishStatusResult.cs` | sealed record | `public sealed record PublishStatusResult(Guid VersionId, Guid EngineerId, int VersionNumber, string SemanticVersion, string Status, string? ZipUrl, string? ZipSha256, long SizeBytes, string? FailureReason, DateTimeOffset UpdatedAt);` Client-facing; no `LocalizedText` anywhere in this slice, so no `.Localized()` calls. |
| 23 | `PublishStatusResultGenerator.cs` | static class | `public static PublishStatusResult Generate(ItemVersion version, PublishingOptions options)` — `Status = version.Status.ToString()`, `ZipUrl = version.ZipBlobPath == null ? null : PublishBlobPaths.ZipUrl(options.PublicSiteUrl, version.ZipBlobPath)`, `UpdatedAt = version.UpdationDate`. |

### E3A.Application — publish command (namespace `E3A.Application.Engineers.PublishEngineer`)

| # | Path | Type | Contract |
|---|------|------|----------|
| 24 | `api/E3A.Application/Engineers/PublishEngineer/PublishEngineerCommand.cs` | sealed record | `public sealed record PublishEngineerCommand(Guid EngineerId, VersionIncrement Increment) : IRequest<PublishStatusResult>;` |
| 25 | `.../PublishEngineerValidator.cs` | sealed class | `RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);` · `RuleFor(x => x.Increment).IsInEnum().WithErrorCode(ErrorCodes.PublishIncrementInvalid);` |
| 26 | `.../PublishEngineerHandler.cs` | sealed class | `public sealed class PublishEngineerHandler(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<PublishEngineerCommand, PublishStatusResult>`. `Handle` steps: (1) `currentUserService.UserId` null/empty → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `GetByIdAsync(request.EngineerId)` null → `NotFoundCoreException(ErrorCodes.EngineerNotFound)`; (3) `engineer.OwnerUserId != userId` → `ForbiddenCoreException(ErrorCodes.EngineerNotOwned)`; (4) `string.IsNullOrWhiteSpace(engineer.DraftManifestJson)` → `BadRequestCoreException(ErrorCodes.EngineerDraftNotUploaded)`; (5) `FirstOrDefaultAsync(x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id && (x.Status == ItemVersionStatus.Queued \|\| x.Status == ItemVersionStatus.Building))` non-null → `ConflictCoreException(ErrorCodes.PublishAlreadyInProgress)`; (6) `CountAsync(ct, x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id) >= options.MaxVersionsPerItem` → `BusinessRuleViolationCoreException(ErrorCodes.PublishVersionLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxVersionsPerItem })`; (7) `latest = FirstOrDefaultAsync(x => x.ItemType == … && x.ItemId == …, ct, orderBy: query => query.OrderByDescending(x => x.VersionNumber))`; (8) `semanticVersion = SemanticVersionCalculator.Next(latest?.SemanticVersion, request.Increment)`, `versionNumber = (latest?.VersionNumber ?? 0) + 1`; (9) `ItemVersion.Create(ItemType.Engineer, engineer.Id, versionNumber, semanticVersion, engineer.DraftManifestJson!, userId.Value)`; (10) `AddAsync` then **one** `SaveChangesAsync`; (11) `return PublishStatusResultGenerator.Generate(version, options);` |

### E3A.Application — unlist / relist

| # | Path | Type | Contract |
|---|------|------|----------|
| 27 | `api/E3A.Application/Engineers/UnlistEngineer/UnlistEngineerCommand.cs` | sealed record | `public sealed record UnlistEngineerCommand(Guid EngineerId) : IRequest<EngineerResult>;` |
| 28 | `.../UnlistEngineerValidator.cs` | sealed class | `RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);` |
| 29 | `.../UnlistEngineerHandler.cs` | sealed class | `(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, ISender sender) : IRequestHandler<UnlistEngineerCommand, EngineerResult>`. Steps: user guard → `EngineerNotFound` → `EngineerNotOwned` → in-progress version → `ConflictCoreException(ErrorCodes.PublishAlreadyInProgress)` → `engineer.Status != EngineerStatus.Published` → `BusinessRuleViolationCoreException(ErrorCodes.EngineerNotPublished)` → `engineer.Unlist()` → `Update` → `SaveChangesAsync` (once) → `await sender.Send(new RegenerateMarketplaceCommand(), cancellationToken).ConfigureAwait(false)` → `return EngineerResultGenerator.Generate(engineer);` |
| 30 | `api/E3A.Application/Engineers/RelistEngineer/RelistEngineerCommand.cs` | sealed record | `public sealed record RelistEngineerCommand(Guid EngineerId) : IRequest<EngineerResult>;` |
| 31 | `.../RelistEngineerValidator.cs` | sealed class | `RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);` |
| 32 | `.../RelistEngineerHandler.cs` | sealed class | Identical shape to #29 with `engineer.Status != EngineerStatus.Unlisted` → `BusinessRuleViolationCoreException(ErrorCodes.EngineerNotUnlisted)` and `engineer.Relist()`. |

### E3A.Application — worker slices (namespace `E3A.Application.Publishing.*`)

| # | Path | Type | Contract |
|---|------|------|----------|
| 33 | `api/E3A.Application/Publishing/PublishRequested/PublishRequestedEventHandler.cs` | sealed class | `public sealed class PublishRequestedEventHandler(IStorageQueueClient storageQueueClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions) : INotificationHandler<PublishRequestedDomainEvent>`. `Handle` → `await storageQueueClient.SendMessageAsync(notification, azure.ManagedIdentityClientId, azure.StorageAccountQueueUrl, cancellationToken, visibilityTimeout: TimeSpan.FromSeconds(publishing.QueueVisibilityTimeoutSeconds)).ConfigureAwait(false);` — the visibility timeout is the enqueue-race guard (`CoreDbContext.SaveChangesAsync` publishes events *before* `base.SaveChangesAsync`). |
| 34 | `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobCommand.cs` | sealed record | `public sealed record ProcessPublishJobCommand(Guid VersionId) : IRequest;` |
| 35 | `.../ProcessPublishJobValidator.cs` | sealed class | `RuleFor(x => x.VersionId).ValidateRequired(ErrorCodes.PublishVersionIdRequired);` |
| 36 | `.../ProcessPublishJobHandler.cs` | sealed class | `public sealed class ProcessPublishJobHandler(IItemVersionRepository itemVersionRepository, IEngineerRepository engineerRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<ProcessPublishJobCommand>`. Ordered steps below. |
| 37 | `api/E3A.Application/Publishing/RegenerateMarketplace/RegenerateMarketplaceCommand.cs` | sealed record | `public sealed record RegenerateMarketplaceCommand : IRequest;` (no properties, no validator) |
| 38 | `.../RegenerateMarketplaceHandler.cs` | sealed class | `(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, IOptions<AzureOptions> azureOptions, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<RegenerateMarketplaceCommand>`. Ordered steps below. |
| 39 | `api/E3A.Application/Publishing/GetPublishStatus/GetPublishStatusQuery.cs` | sealed record | `public sealed record GetPublishStatusQuery(Guid VersionId) : IRequest<PublishStatusResult>;` |
| 40 | `.../GetPublishStatusQueryValidator.cs` | sealed class | `RuleFor(x => x.VersionId).ValidateRequired(ErrorCodes.PublishVersionIdRequired);` |
| 41 | `.../GetPublishStatusQueryHandler.cs` | sealed class | `(IItemVersionRepository itemVersionRepository, IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<GetPublishStatusQuery, PublishStatusResult>`. Steps: user guard → `GetByIdAsync(request.VersionId, asNoTracking: true)` null → `NotFoundCoreException(ErrorCodes.PublishVersionNotFound)` → `engineerRepository.GetByIdAsync(version.ItemId, asNoTracking: true)` null → `NotFoundCoreException(ErrorCodes.EngineerNotFound)` → `engineer.OwnerUserId != userId` → `ForbiddenCoreException(ErrorCodes.EngineerNotOwned)` → `PublishStatusResultGenerator.Generate(version, options)`. |

**`ProcessPublishJobHandler.Handle` — ordered steps (D3, D4, D12, D16):**

1. `version = await itemVersionRepository.GetByIdAsync(request.VersionId, cancellationToken)`. Null → `throw new NotFoundCoreException(ErrorCodes.PublishVersionNotFound)`. This is the retryable "version not found" path (acceptance decision #9): the queue trigger redelivers after the visibility timeout; `maxDequeueCount` retries then poison.
2. `if (version.Status is not (ItemVersionStatus.Queued or ItemVersionStatus.Building)) { return; }`
3. `engineer = await engineerRepository.GetByIdAsync(version.ItemId, cancellationToken)`. Null → `version.MarkFailed(ErrorCodes.EngineerNotFound); itemVersionRepository.Update(version); await itemVersionRepository.SaveChangesAsync(...); return;`
4. `if (version.Status == ItemVersionStatus.Queued) { version.MarkBuilding(); itemVersionRepository.Update(version); await itemVersionRepository.SaveChangesAsync(...); }` ← **save #1**
5. `snapshotAssets = await DraftSnapshotFreezer.FreezeAsync(storageBlobClient, azure, engineer.OwnerUserId, engineer.Id, version.Id, cancellationToken)`. Empty → `MarkFailed(ErrorCodes.EngineerSnapshotEmpty)` + update + save + `return`.
6. `manifest = JsonSerializer.Deserialize<ImportManifestResult>(version.FrozenManifestJson)`. Null → `MarkFailed(ErrorCodes.EngineerDraftNotUploaded)` + update + save + `return`.
7. `user = await userRepository.GetByIdAsync(engineer.OwnerUserId, cancellationToken, asNoTracking: true)`; `authorName = string.IsNullOrWhiteSpace(user?.UserName) ? engineer.Slug : user.UserName;`
8. `pluginFiles = PluginTreeAssembler.Assemble(snapshotAssets, manifest, engineer, version.SemanticVersion, authorName, publishing)`
9. *(scanner slice inserts its single step here)*
10. `errors = PluginStructureValidator.Validate(pluginFiles, manifest, publishing)`. Non-empty → `MarkFailed(string.Join(", ", errors))` + update + save + `return`. ← **save #2 (failure path)**
11. `zipped = DeterministicZipper.Create(pluginFiles)`; `pluginName = PluginName.For(engineer.Slug)`; `zipBlobPath = PublishBlobPaths.Zip(pluginName, version.SemanticVersion)`.
12. `existing = await storageBlobClient.ListByPrefixAsync(azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, zipBlobPath, cancellationToken)`. `if (existing.Count == 0) { await storageBlobClient.UploadAsync(new MemoryStream(zipped.Content), …, azure.PublicBlobContainerName, zipBlobPath, PublishBlobPaths.ZipContentType, publishing.ZipCacheControl, overwrite: false, cancellationToken); }` (D4)
13. `version.MarkPublished(zipBlobPath, zipped.Sha256, zipped.SizeBytes); engineer.MarkPublished(version.Id); itemVersionRepository.Update(version); engineerRepository.Update(engineer); await itemVersionRepository.SaveChangesAsync(cancellationToken);` ← **save #2 (success path)**. One `SaveChangesAsync` call covers both entities — the repositories share `AppDbContext`.
14. `pinnedJson = MarketplaceDocumentGenerator.Generate([MarketplaceDocumentGenerator.GeneratePlugin(engineer, version, authorName, publishing)], publishing)`; upload to `PublishBlobPaths.PinnedMarketplace(pluginName, version.SemanticVersion)` with `MarketplaceContentType`, `publishing.MarketplaceCacheControl`, `overwrite: true`.

No `try`/`catch` anywhere. Blob and database exceptions bubble to the Function and drive queue retry.

**`RegenerateMarketplaceHandler.Handle` — ordered steps (D6, D7):**

1. `List<Engineer> published = []; var pageNumber = 1;`
2. Loop: `page = await engineerRepository.FindPaginatedAsync(pageNumber, publishing.MarketplacePageSize, cancellationToken, x => x.Status == EngineerStatus.Published && x.LatestVersionId != null, orderBy: query => query.OrderBy(x => x.Slug), asNoTracking: true)`; `published.AddRange(page.Items)`; break when `pageNumber >= page.TotalPages`; `pageNumber++`; `if (pageNumber > publishing.MarketplaceMaxPages) { throw new InternalServerErrorCoreException(ErrorCodes.MarketplaceEngineerLimitExceeded); }`
3. `versionIds = published.Select(x => x.LatestVersionId!.Value).ToList();` `versions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id) && x.Status == ItemVersionStatus.Published, cancellationToken, asNoTracking: true)`
4. `ownerIds = published.Select(x => x.OwnerUserId).Distinct().ToList();` `users = await userRepository.FindAsync(x => ownerIds.Contains(x.Id), cancellationToken, asNoTracking: true)`
5. `plugins = published.Select(...).Where(version is not null).Select(...GeneratePlugin(engineer, version, authorName, publishing)).ToList()` — engineers whose latest version is not `Published` are skipped; `authorName` resolved from `users` with the `engineer.Slug` fallback (D10).
6. `json = MarketplaceDocumentGenerator.Generate(plugins, publishing)`
7. Single `UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), …, azure.PublicBlobContainerName, PublishBlobPaths.RootMarketplaceBlobName, PublishBlobPaths.MarketplaceContentType, publishing.MarketplaceCacheControl, overwrite: true, cancellationToken)`.

No `SaveChangesAsync` — read-only against the database.

### E3A.Infrastructure

| # | Path | Type | Contract |
|---|------|------|----------|
| 42 | `api/E3A.Infrastructure/Publishing/ItemVersionRepository.cs` | class | `public class ItemVersionRepository(AppDbContext context) : Repository<ItemVersion>(context), IItemVersionRepository { }` |
| 43 | `api/E3A.Infrastructure/Identity/UserRepository.cs` | class | `public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository { }` |
| 44 | `api/E3A.Infrastructure/Data/Migrations/<timestamp>_versions002.cs` (+ `.Designer.cs`) | migration | Generated: `dotnet ef migrations add versions002 --project api/E3A.Infrastructure --startup-project api/E3A.Api`. Creates `ItemVersions` only — `EngineerStatus.Unlisted` needs no schema change because `Status` is `HasConversion<string>()`. |

**`AppDbContext.ConfigureItemVersions(ModelBuilder)`** (new private method; constructor gains `IOptions<PublishingOptions> publishingOptions`):

```
builder.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
builder.Property(x => x.SemanticVersion).IsRequired().HasMaxLength(publishingSchema.SemanticVersionMaxLength);
builder.Property(x => x.FrozenManifestJson).IsRequired();
builder.Property(x => x.ZipBlobPath).HasMaxLength(publishingSchema.BlobPathMaxLength);
builder.Property(x => x.ZipSha256).HasMaxLength(Sha256HexLength);   // new private const = 64, WHY: SHA-256 hex is fixed width
builder.Property(x => x.FailureReason).HasMaxLength(publishingSchema.FailureReasonMaxLength);
builder.HasIndex(x => new { x.ItemType, x.ItemId, x.VersionNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
builder.HasIndex(x => x.ItemId);
```
No foreign key and no navigation — `(ItemType, ItemId)` is polymorphic so teams slot in without a schema change.

### E3A.Api

| # | Path | Type | Contract |
|---|------|------|----------|
| 45 | `api/E3A.Api/Controllers/Publishing/PublishController.cs` | class | `namespace E3A.Api.Controllers.Publishing;` `[ApiController] [Route("api/publish")] [Authorize] public class PublishController(IMediator mediator) : ControllerBase` with `[HttpGet("{versionId:guid}/status")] public async Task<ActionResult> GetPublishStatus([FromRoute] Guid versionId, CancellationToken cancellationToken)` → `Ok(await mediator.Send(new GetPublishStatusQuery(versionId), cancellationToken))`. |

### E3A.Jobs (new project)

| # | Path | Type | Contract |
|---|------|------|----------|
| 46 | `api/E3A.Jobs/E3A.Jobs.csproj` | csproj | `<AzureFunctionsVersion>v4</AzureFunctionsVersion>`, `<OutputType>Exe</OutputType>`. `TargetFramework`/`Nullable`/`ImplicitUsings` come from `Directory.Build.props` — do not restate them. `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. `PackageReference` (versionless — central package management): `Microsoft.Azure.Functions.Worker`, `.Worker.Sdk`, `.Worker.Extensions.Abstractions`, `.Worker.Extensions.Storage.Queues`, `.Worker.Extensions.Http.AspNetCore`, `Microsoft.Extensions.Configuration.AzureAppConfiguration`, `Azure.Storage.Blobs`, `Microsoft.EntityFrameworkCore.SqlServer`. All already present in `api/Directory.Packages.props` under the "Jobs host" group — add `Microsoft.EntityFrameworkCore.SqlServer` there only if the group lacks it (it is in the "Framework and third party" group already, so no edit needed). `ProjectReference`: `E3A.Application`, `E3A.Domain`, `E3A.Infrastructure`. |
| 47 | `api/E3A.Jobs/Program.cs` | top-level | Mirror `Morabh.Jobs/Program.cs`: `var builder = FunctionsApplication.CreateBuilder(args); builder.ConfigureFunctionsWebApplication();` then `builder.Services.AddHttpContextAccessor(); builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DbConnectionString"))); builder.Services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>(); builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddCoreLocalization(); builder.Services.AddCoreAzure(); builder.Services.AddCoreCQRS(); builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>(); builder.Services.AddCoreUtilities(); builder.Services.AddApplication(builder.Configuration); builder.Services.AddInfrastructure(); builder.Build().Run();` No controllers, no auth, no Scalar. |
| 48 | `api/E3A.Jobs/host.json` | json | `{ "version": "2.0", "logging": { "applicationInsights": { "samplingSettings": { "isEnabled": true } } }, "extensions": { "queues": { "batchSize": 1, "newBatchThreshold": 0, "maxDequeueCount": 5, "visibilityTimeout": "00:00:30" } } }` — `batchSize: 1` is decision D5. |
| 49 | `api/E3A.Jobs/Functions/ProcessPublishJobFunction.cs` | class | `public class ProcessPublishJobFunction(ISender mediator, ILogger<ProcessPublishJobFunction> logger)` with `[Function("ProcessPublishJob")] public async Task ProcessPublishJob([QueueTrigger("%Azure:PublishQueueName%", Connection = "StorageAccountConnection")] PublishRequestedDomainEvent publishRequested, CancellationToken cancellationToken)`. Body: one `logger.LogInformation` naming `publishRequested.VersionId`, then `await mediator.Send(new ProcessPublishJobCommand(publishRequested.VersionId), cancellationToken).ConfigureAwait(false); await mediator.Send(new RegenerateMarketplaceCommand(), cancellationToken).ConfigureAwait(false);` No branches, no `try`/`catch`. |

`api/E3A.Jobs/local.settings.json` is **not** created (gitignored, machine-local). Required local keys are listed under Configuration.

## Core.Azure change (announced)

`api/core-libraries/Core.Azure/Clients/StorageBlobClient.cs` — three additive members on `IStorageBlobClient` and `StorageBlobClient`. **No existing signature changes** (`core-libraries` is vendored and shared).

```csharp
Task<UploadResult> UploadAsync(Stream content, string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, string contentType, string cacheControl, bool overwrite, CancellationToken cancellationToken);
Task<List<string>> ListByPrefixAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string prefix, CancellationToken cancellationToken);
Task<byte[]?> DownloadAsync(string managedIdentityClientId, string storageAccountUrl, string blobContainerName, string blobName, CancellationToken cancellationToken);
```

Overload implementation: same `BlobServiceClient` / `CreateIfNotExistsAsync` / `GetBlobClient` preamble as the existing method, then
`var uploadOptions = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = cacheControl } };`
`if (!overwrite) { uploadOptions.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }; }`
`await blobClient.UploadAsync(content, uploadOptions, cancellationToken).ConfigureAwait(false);`
`ListByPrefixAsync` returns `blobItem.Name` values from `GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)`.
`DownloadAsync` returns `null` when `ExistsAsync` is false, else the bytes from `DownloadContentAsync`.

`Core.Azure` is out of test scope (infrastructure client, same as `Repository<T>`).

## Error codes

Added to `api/E3A.Application/Exceptions/ErrorCodes.cs` under a new `// Publishing` comment group (plus two under `// Engineers`).

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `PublishVersionIdRequired` | `PUBLISH_VERSION_ID_REQUIRED` | `ProcessPublishJobValidator`, `GetPublishStatusQueryValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `PublishIncrementInvalid` | `PUBLISH_INCREMENT_INVALID` | `PublishEngineerValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `PublishVersionNotFound` | `PUBLISH_VERSION_NOT_FOUND` | `ProcessPublishJobHandler` step 1, `GetPublishStatusQueryHandler` | `NotFoundCoreException` | 404 |
| `PublishAlreadyInProgress` | `PUBLISH_ALREADY_IN_PROGRESS` | `PublishEngineerHandler`, `UnlistEngineerHandler`, `RelistEngineerHandler` | `ConflictCoreException` | 409 |
| `PublishVersionLimitReached` | `PUBLISH_VERSION_LIMIT_REACHED` | `PublishEngineerHandler` | `BusinessRuleViolationCoreException` (context `limit`) | 400 |
| `EngineerNotPublished` | `ENGINEER_NOT_PUBLISHED` | `UnlistEngineerHandler` | `BusinessRuleViolationCoreException` | 400 |
| `EngineerNotUnlisted` | `ENGINEER_NOT_UNLISTED` | `RelistEngineerHandler` | `BusinessRuleViolationCoreException` | 400 |
| `EngineerSnapshotEmpty` | `ENGINEER_SNAPSHOT_EMPTY` | `ProcessPublishJobHandler` step 5 | none — stored in `FailureReason` | n/a |
| `PluginNoInstallableContent` | `PLUGIN_NO_INSTALLABLE_CONTENT` | `PluginStructureValidator` | none — stored in `FailureReason` | n/a |
| `PluginManifestAssetMissing` | `PLUGIN_MANIFEST_ASSET_MISSING` | `PluginStructureValidator` | none — stored in `FailureReason` | n/a |
| `PluginUnsafePath` | `PLUGIN_UNSAFE_PATH` | `PluginStructureValidator` | none — stored in `FailureReason` | n/a |
| `PluginSkillMissingSkillFile` | `PLUGIN_SKILL_MISSING_SKILL_FILE` | `PluginStructureValidator` | none — stored in `FailureReason` | n/a |
| `PluginTooManyFiles` | `PLUGIN_TOO_MANY_FILES` | `PluginStructureValidator` | none — stored in `FailureReason` | n/a |
| `PluginTooLarge` | `PLUGIN_TOO_LARGE` | `PluginStructureValidator` | none — stored in `FailureReason` | n/a |
| `MarketplaceEngineerLimitExceeded` | `MARKETPLACE_ENGINEER_LIMIT_EXCEEDED` | `RegenerateMarketplaceHandler` | `InternalServerErrorCoreException` | 500 |

Every constant gets a key in **both** resx files (keys are the error-code values). Placeholders kept identical in both languages; Arabic without tashkeel.

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|-----|--------------------|--------------------|
| `PUBLISH_VERSION_ID_REQUIRED` | `A version identifier is required.` | `معرف الاصدار مطلوب.` |
| `PUBLISH_INCREMENT_INVALID` | `The version increment is not recognized.` | `نوع زيادة الاصدار غير معروف.` |
| `PUBLISH_VERSION_NOT_FOUND` | `We couldn't find that version.` | `تعذر العثور على هذا الاصدار.` |
| `PUBLISH_ALREADY_IN_PROGRESS` | `A publish is already running for this engineer.` | `هناك عملية نشر جارية بالفعل لهذا المهندس.` |
| `PUBLISH_VERSION_LIMIT_REACHED` | `You have reached the limit of {limit} versions for this engineer.` | `لقد وصلت الى الحد الاقصى وهو {limit} اصدار لهذا المهندس.` |
| `ENGINEER_NOT_PUBLISHED` | `Only a published engineer can be unlisted.` | `يمكن اخفاء المهندس المنشور فقط.` |
| `ENGINEER_NOT_UNLISTED` | `Only an unlisted engineer can be relisted.` | `يمكن اعادة اظهار المهندس المخفي فقط.` |
| `ENGINEER_SNAPSHOT_EMPTY` | `The uploaded draft has no files to publish.` | `لا توجد ملفات قابلة للنشر في المسودة المرفوعة.` |
| `PLUGIN_NO_INSTALLABLE_CONTENT` | `The plugin has no agents, skills or commands to install.` | `لا يحتوي الملحق على وكلاء او مهارات او اوامر للتثبيت.` |
| `PLUGIN_MANIFEST_ASSET_MISSING` | `A file listed in the import manifest is missing from the upload.` | `احد الملفات المذكورة في قائمة الاستيراد غير موجود في الرفع.` |
| `PLUGIN_UNSAFE_PATH` | `The plugin contains an unsafe file path.` | `يحتوي الملحق على مسار ملف غير امن.` |
| `PLUGIN_SKILL_MISSING_SKILL_FILE` | `A skill folder is missing its SKILL.md file.` | `احد مجلدات المهارات ينقصه ملف SKILL.md.` |
| `PLUGIN_TOO_MANY_FILES` | `The plugin contains too many files.` | `يحتوي الملحق على عدد ملفات اكبر من المسموح.` |
| `PLUGIN_TOO_LARGE` | `The plugin is larger than the allowed size.` | `حجم الملحق اكبر من الحجم المسموح.` |
| `MARKETPLACE_ENGINEER_LIMIT_EXCEEDED` | `The marketplace has more engineers than the generator can process.` | `عدد المهندسين في السوق اكبر مما يستطيع المولد معالجته.` |

## Domain behaviour

### `api/E3A.Domain/Publishing/ItemVersion.cs`

```csharp
public class ItemVersion : AuditEntity
{
    public ItemType ItemType { get; private set; }
    public Guid ItemId { get; private set; }
    public int VersionNumber { get; private set; }
    public string SemanticVersion { get; private set; } = default!;
    public string FrozenManifestJson { get; private set; } = default!;
    public ItemVersionStatus Status { get; private set; }
    public string? ZipBlobPath { get; private set; }
    public string? ZipSha256 { get; private set; }
    public long SizeBytes { get; private set; }
    public string? FailureReason { get; private set; }
    public bool IsTerminal => Status is ItemVersionStatus.Published or ItemVersionStatus.Rejected or ItemVersionStatus.Failed;

    private ItemVersion(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static ItemVersion Create(ItemType itemType, Guid itemId, int versionNumber, string semanticVersion, string frozenManifestJson, Guid createdBy)
    {
        var version = new ItemVersion(Guid.NewGuid(), createdBy)
        {
            ItemType = itemType,
            ItemId = itemId,
            VersionNumber = versionNumber,
            SemanticVersion = semanticVersion,
            FrozenManifestJson = frozenManifestJson,
            Status = ItemVersionStatus.Queued,
            SizeBytes = 0,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };

        version.RaiseDomainEvent(new PublishRequestedDomainEvent(version.Id));

        return version;
    }

    public void MarkBuilding()
    {
        Status = ItemVersionStatus.Building;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkPublished(string zipBlobPath, string zipSha256, long sizeBytes)
    {
        Status = ItemVersionStatus.Published;
        ZipBlobPath = zipBlobPath;
        ZipSha256 = zipSha256;
        SizeBytes = sizeBytes;
        FailureReason = null;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string failureReason)
    {
        Status = ItemVersionStatus.Failed;
        FailureReason = failureReason;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
```

No `BusinessRuleViolationException` guards inside the entity — see D8. `Create` burns the version number
even if the build later fails (acceptance decision #7): nothing ever resets `VersionNumber`, and
`PublishEngineerHandler` always derives the next number from `max(VersionNumber) + 1` across **all**
statuses.

### `api/E3A.Domain/Engineers/Engineer.cs` — two new methods

```csharp
public void Unlist()
{
    Status = EngineerStatus.Unlisted;
    UpdationDate = DateTimeOffset.UtcNow;
}

public void Relist()
{
    Status = EngineerStatus.Published;
    UpdationDate = DateTimeOffset.UtcNow;
}
```

Unlist deliberately leaves `LatestVersionId`, the zip blob and the pinned marketplace untouched — unlist is
not a takedown (acceptance decision #3). Existing installs keep resolving; only the root `marketplace.json`
drops the entry, because `RegenerateMarketplaceHandler` filters on `Status == EngineerStatus.Published`.

### Ripple of `EngineerStatus.Unlisted` on existing handlers — verified, no change needed

- `GetCatalogQueryHandler` and `GetCatalogEngineerQueryHandler` filter `Status == EngineerStatus.Published` → unlisted engineers vanish from browse and public detail automatically.
- `GetEngineerQueryHandler` returns anonymously only when `Status == EngineerStatus.Published`; `Unlisted` falls through to the owner-only branch, which is correct.

## API surface

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| POST | `api/engineers/{engineerId:guid}/publish` | `[Authorize]` (class-level; owner enforced in handler) | `[FromBody] PublishEngineerRequest(VersionIncrement Increment)` | `202 Accepted` — `Accepted(result)` with `PublishStatusResult` |
| POST | `api/engineers/{engineerId:guid}/unlist` | `[Authorize]` | none | `200 Ok(EngineerResult)` |
| POST | `api/engineers/{engineerId:guid}/relist` | `[Authorize]` | none | `200 Ok(EngineerResult)` |
| GET | `api/publish/{versionId:guid}/status` | `[Authorize]` | none | `200 Ok(PublishStatusResult)` |

No policy constants: this repo has **no `DefaultCodes` class**; every existing action relies on the
class-level `[Authorize]` plus an owner check inside the handler (`EngineersController`, `CatalogController`).
Mirror that — do **not** introduce `DefaultCodes` in this slice.

## Configuration (announce to the dev — `appsettings.json` is deploy-time only)

New `Publishing` section in `api/E3A.Api/appsettings.json`:

```json
"Publishing": {
  "MaxVersionsPerItem": 50,
  "QueueVisibilityTimeoutSeconds": 10,
  "PublicSiteUrl": "https://e3a.dev",
  "MarketplaceName": "e3a",
  "MarketplaceOwnerName": "e3a",
  "MarketplaceCacheControl": "public, max-age=60",
  "ZipCacheControl": "public, max-age=31536000, immutable",
  "MarketplacePageSize": 100,
  "MarketplaceMaxPages": 50,
  "MaxPluginFileCount": 400,
  "MaxPluginBytes": 104857600,
  "SemanticVersionMaxLength": 20,
  "BlobPathMaxLength": 400,
  "FailureReasonMaxLength": 500
}
```

New `Azure` key: `"PublishQueueName": "publish-jobs"`.

`api/E3A.Jobs/local.settings.json` (gitignored, created by the dev) needs: `AzureWebJobsStorage`,
`StorageAccountConnection` (or `StorageAccountConnection__queueServiceUri` +
`StorageAccountConnection__credential` for identity-based binding),
`FUNCTIONS_WORKER_RUNTIME: "dotnet-isolated"`, `ConnectionStrings:DbConnectionString`, and the same
`Azure` + `Publishing` sections as the API.

## Test plan

Per `conventions/dotnet-testing.md` §5. `E3A.Jobs` functions, `Core.Azure`, repositories, EF configuration
and controllers are out of scope. New shared factory: `api/E3A.Tests/Publishing/Shared/ItemVersionFactory.cs`
(`Queued()`, `Building()`, `Published()`, `Failed()` — each built through `ItemVersion.Create` + domain
methods only, no reflection) and `api/E3A.Tests/Publishing/Shared/PluginFileFactory.cs`
(`Manifest(params string[] targetPaths)` returning an `ImportManifestResult`, and `Files(...)` returning
`List<PluginFile>`).

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Publishing/ItemVersionTests` | `Create_ShouldReturnQueuedVersion_WhenCalled` | `Status == Queued`, `VersionNumber`, `SemanticVersion`, `FrozenManifestJson`, `SizeBytes == 0`, `Id != Guid.Empty`, `ZipBlobPath` null |
| 2 | `Publishing/ItemVersionTests` | `Create_ShouldRaisePublishRequestedDomainEvent_WhenCalled` | `GetDomainEvents()` has one `PublishRequestedDomainEvent` whose `VersionId == version.Id` |
| 3 | `Publishing/ItemVersionTests` | `MarkBuilding_ShouldSetBuildingAndAdvanceUpdationDate_WhenCalled` | `Status == Building`; `UpdationDate` on or after a captured `before` |
| 4 | `Publishing/ItemVersionTests` | `MarkPublished_ShouldRecordZipMetadataAndClearFailureReason_WhenCalled` | `Status == Published`, `ZipBlobPath`, `ZipSha256`, `SizeBytes`, `FailureReason` null, `UpdationDate` advanced |
| 5 | `Publishing/ItemVersionTests` | `MarkFailed_ShouldSetFailedWithReason_WhenCalled` | `Status == Failed`, `FailureReason`, `UpdationDate` advanced |
| 6 | `Publishing/ItemVersionTests` | `IsTerminal_ShouldBeTrue_WhenStatusIsPublishedOrFailed` | `Queued`/`Building` false; `Published`/`Failed` true |
| 7 | `Engineers/EngineerListingTests` | `Unlist_ShouldSetUnlistedAndAdvanceUpdationDate_WhenCalled` | `Status == Unlisted`, `LatestVersionId` unchanged, `UpdationDate` advanced |
| 8 | `Engineers/EngineerListingTests` | `Relist_ShouldSetPublishedAndAdvanceUpdationDate_WhenCalled` | `Status == Published`, `UpdationDate` advanced |
| 9 | `Publishing/Shared/SemanticVersionCalculatorTests` | `Next_ShouldReturnInitialVersion_WhenPreviousIsMissing` `[Theory]` over `null`, `""`, `"   "`, `"not-a-version"`, `"1.2"` × each increment | `"1.0.0"` |
| 10 | `Publishing/Shared/SemanticVersionCalculatorTests` | `Next_ShouldBumpCorrectComponent_WhenPreviousExists` `[Theory]` `("1.2.3", Patch, "1.2.4")`, `("1.2.3", Minor, "1.3.0")`, `("1.2.3", Major, "2.0.0")`, `("0.0.9", Patch, "0.0.10")` | exact string |
| 11 | `Publishing/Shared/PluginJsonGeneratorTests` | `Generate_ShouldEmitPrefixedNameAndAuthor_WhenCalled` | path `.claude-plugin/plugin.json`; JSON contains `"name": "e3a-{slug}"`, the semantic version, `"url": "https://e3a.dev/e/{slug}"`, camelCase keys |
| 12 | `Publishing/Shared/PluginJsonGeneratorTests` | `Generate_ShouldOmitDescription_WhenEngineerHasNone` | serialized JSON contains no `description` key |
| 13 | `Publishing/Shared/PluginTreeAssemblerTests` | `Assemble_ShouldKeepOnlyManifestTargets_WhenSnapshotHasExtraFiles` | an asset absent from the manifest is dropped; manifest-listed assets survive |
| 14 | `Publishing/Shared/PluginTreeAssemblerTests` | `Assemble_ShouldIncludeConvertedHouseRulesSkill_WhenManifestConvertsIt` | `skills/house-rules/SKILL.md` present |
| 15 | `Publishing/Shared/PluginTreeAssemblerTests` | `Assemble_ShouldAppendPluginJsonAndOrderOrdinally_WhenCalled` | result contains `.claude-plugin/plugin.json`; paths equal their ordinal-sorted sequence |
| 16 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReturnEmpty_WhenTreeIsWellFormed` | empty list |
| 17 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReportManifestAssetMissing_WhenManifestTargetIsAbsent` | contains `ErrorCodes.PluginManifestAssetMissing` |
| 18 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReportNoInstallableContent_WhenOnlyPluginJsonExists` | contains `ErrorCodes.PluginNoInstallableContent` |
| 19 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReportUnsafePath_WhenPathEscapes` `[Theory]` `"../x.md"`, `"skills/../../x.md"`, `"/agents/x.md"`, `"agents\\x.md"`, `""` | contains `ErrorCodes.PluginUnsafePath` |
| 20 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReportSkillMissingSkillFile_WhenSkillFolderLacksIt` | contains `ErrorCodes.PluginSkillMissingSkillFile` |
| 21 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReportTooManyFiles_WhenCountExceedsOption` | contains `ErrorCodes.PluginTooManyFiles` |
| 22 | `Publishing/Shared/PluginStructureValidatorTests` | `Validate_ShouldReportTooLarge_WhenTotalBytesExceedOption` | contains `ErrorCodes.PluginTooLarge` |
| 23 | `Publishing/Shared/DeterministicZipperTests` | `Create_ShouldProduceIdenticalBytesAndHash_WhenCalledTwiceWithSameInput` | `zipA.Content` sequence-equal `zipB.Content`; `Sha256` equal |
| 24 | `Publishing/Shared/DeterministicZipperTests` | `Create_ShouldProduceIdenticalBytes_WhenInputOrderDiffers` | shuffled input yields identical bytes and hash |
| 25 | `Publishing/Shared/DeterministicZipperTests` | `Create_ShouldRoundTripEveryEntry_WhenOpened` | reopening the archive yields the same paths (ordinal-ordered) and contents |
| 26 | `Publishing/Shared/DeterministicZipperTests` | `Create_ShouldReturnLowercaseHexSha256OfContent_WhenCalled` | `Sha256` length 64, matches `SHA256.HashData(zip.Content)`, all lowercase; `SizeBytes == Content.LongLength` |
| 27 | `Publishing/Shared/MarketplaceDocumentGeneratorTests` | `GeneratePlugin_ShouldBuildArchiveSource_WhenVersionIsPublished` | `Source.Source == "archive"`, url `https://e3a.dev/z/e3a-{slug}/{semanticVersion}.zip`, `Sha256` matches version, keywords equal tags |
| 28 | `Publishing/Shared/MarketplaceDocumentGeneratorTests` | `Generate_ShouldWrapPluginsWithNameAndOwner_WhenCalled` | JSON has `name`, `owner.name`, `owner.url`, `plugins` array of expected length; camelCase keys |
| 29 | `Publishing/Shared/MarketplaceDocumentGeneratorTests` | `Generate_ShouldEmitEmptyPluginsArray_WhenNoneArePublished` | `plugins` present and empty |
| 30 | `Publishing/Shared/DraftSnapshotFreezerTests` | `FreezeAsync_ShouldCopyEveryDraftBlobToSnapshotPrefix_WhenDraftExists` | `DeleteByPrefixAsync` received once for `{versionId}/`; `UploadAsync` received once per blob with blob name `{versionId}/{relativePath}`; returned `PluginFile` paths are the relative paths, ordinal-ordered |
| 31 | `Publishing/Shared/DraftSnapshotFreezerTests` | `FreezeAsync_ShouldReturnEmpty_WhenDraftPrefixHasNoBlobs` | empty list; `UploadAsync` never received |
| 32 | `Publishing/Shared/DraftSnapshotFreezerTests` | `FreezeAsync_ShouldSkipBlob_WhenDownloadReturnsNull` | that path absent from the result and never uploaded |
| 33 | `Publishing/Shared/PublishStatusResultGeneratorTests` | `Generate_ShouldBuildAbsoluteZipUrl_WhenVersionIsPublished` | `ZipUrl == "https://e3a.dev/z/…"`, `Status == "Published"`, `UpdatedAt == version.UpdationDate` |
| 34 | `Publishing/Shared/PublishStatusResultGeneratorTests` | `Generate_ShouldReturnNullZipUrl_WhenVersionHasNoZip` | `ZipUrl` null, `Status == "Queued"` |
| 35 | `Engineers/PublishEngineer/PublishEngineerValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 36 | `Engineers/PublishEngineer/PublishEngineerValidatorTests` | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | error code `ErrorCodes.EngineerIdRequired` |
| 37 | `Engineers/PublishEngineer/PublishEngineerValidatorTests` | `Validate_ShouldFail_WhenIncrementIsNotDefined` | error code `ErrorCodes.PublishIncrementInvalid` |
| 38 | `Engineers/PublishEngineer/PublishEngineerHandlerTests` | `Handle_ShouldCreateQueuedVersion_WhenFirstPublish` | result `Status == "Queued"`, `SemanticVersion == "1.0.0"`, `VersionNumber == 1`; `AddAsync` received once; `SaveChangesAsync` received exactly once |
| 39 | `Engineers/PublishEngineer/PublishEngineerHandlerTests` | `Handle_ShouldIncrementFromLatestVersion_WhenPreviousExists` `[Theory]` over the three increments | expected `SemanticVersion` and `VersionNumber == previous + 1` |
| 40 | `Engineers/PublishEngineer/PublishEngineerHandlerTests` | `Handle_ShouldFreezeDraftManifest_WhenCreatingVersion` | the added `ItemVersion.FrozenManifestJson` equals `engineer.DraftManifestJson` |
| 41 | `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` / `ErrorCodes.UserNotAuthenticated`; `SaveChangesAsync` never received |
| 42 | `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `NotFoundCoreException` / `EngineerNotFound`; no save |
| 43 | `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | `Handle_ShouldThrowForbidden_WhenCallerIsNotOwner` | `ForbiddenCoreException` / `EngineerNotOwned`; no save |
| 44 | `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | `Handle_ShouldThrowBadRequest_WhenDraftWasNeverUploaded` | `BadRequestCoreException` / `EngineerDraftNotUploaded`; no save |
| 45 | `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | `Handle_ShouldThrowConflict_WhenAVersionIsAlreadyQueuedOrBuilding` | `ConflictCoreException` / `PublishAlreadyInProgress`; no save |
| 46 | `Engineers/PublishEngineer/PublishEngineerHandlerGuardTests` | `Handle_ShouldThrowBusinessRuleViolation_WhenVersionCapIsReached` | `BusinessRuleViolationCoreException` / `PublishVersionLimitReached`; `Context["limit"]` equals the option; no save |
| 47 | `Engineers/UnlistEngineer/UnlistEngineerValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 48 | `Engineers/UnlistEngineer/UnlistEngineerValidatorTests` | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | `ErrorCodes.EngineerIdRequired` |
| 49 | `Engineers/UnlistEngineer/UnlistEngineerHandlerTests` | `Handle_ShouldUnlistAndRegenerateMarketplace_WhenEngineerIsPublished` | `engineer.Status == Unlisted`; `SaveChangesAsync` received once; `sender.Send(Arg.Any<RegenerateMarketplaceCommand>(), …)` received once |
| 50 | `Engineers/UnlistEngineer/UnlistEngineerHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException`; no save; no `Send` |
| 51 | `Engineers/UnlistEngineer/UnlistEngineerHandlerTests` | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `NotFoundCoreException` / `EngineerNotFound`; no save; no `Send` |
| 52 | `Engineers/UnlistEngineer/UnlistEngineerHandlerTests` | `Handle_ShouldThrowForbidden_WhenCallerIsNotOwner` | `ForbiddenCoreException` / `EngineerNotOwned`; no save; no `Send` |
| 53 | `Engineers/UnlistEngineer/UnlistEngineerHandlerTests` | `Handle_ShouldThrowConflict_WhenAPublishIsRunning` | `ConflictCoreException` / `PublishAlreadyInProgress`; no save; no `Send` |
| 54 | `Engineers/UnlistEngineer/UnlistEngineerHandlerTests` | `Handle_ShouldThrowBusinessRuleViolation_WhenEngineerIsNotPublished` | `BusinessRuleViolationCoreException` / `EngineerNotPublished`; no save; no `Send` |
| 55 | `Engineers/RelistEngineer/RelistEngineerValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 56 | `Engineers/RelistEngineer/RelistEngineerValidatorTests` | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | `ErrorCodes.EngineerIdRequired` |
| 57 | `Engineers/RelistEngineer/RelistEngineerHandlerTests` | `Handle_ShouldRelistAndRegenerateMarketplace_WhenEngineerIsUnlisted` | `Status == Published`; one save; one `Send` |
| 58 | `Engineers/RelistEngineer/RelistEngineerHandlerTests` | `Handle_ShouldThrowBusinessRuleViolation_WhenEngineerIsNotUnlisted` | `BusinessRuleViolationCoreException` / `EngineerNotUnlisted`; no save; no `Send` |
| 59 | `Engineers/RelistEngineer/RelistEngineerHandlerTests` | `Handle_ShouldThrowConflict_WhenAPublishIsRunning` | `ConflictCoreException` / `PublishAlreadyInProgress`; no save; no `Send` |
| 60 | `Publishing/PublishRequested/PublishRequestedEventHandlerTests` | `Handle_ShouldSendEventToPublishQueue_WhenRaised` | `SendMessageAsync` received once with the notification, the configured managed identity id and queue url |
| 61 | `Publishing/PublishRequested/PublishRequestedEventHandlerTests` | `Handle_ShouldApplyConfiguredVisibilityTimeout_WhenSending` | `visibilityTimeout == TimeSpan.FromSeconds(options.QueueVisibilityTimeoutSeconds)` |
| 62 | `Publishing/ProcessPublishJob/ProcessPublishJobValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 63 | `Publishing/ProcessPublishJob/ProcessPublishJobValidatorTests` | `Validate_ShouldFail_WhenVersionIdIsEmpty` | `ErrorCodes.PublishVersionIdRequired` |
| 64 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests` | `Handle_ShouldPublishVersionAndEngineer_WhenDraftIsValid` | `version.Status == Published`, `ZipBlobPath == "z/e3a-{slug}/1.0.0.zip"`, `ZipSha256` non-empty, `SizeBytes > 0`; `engineer.LatestVersionId == version.Id`, `engineer.Status == Published`; `SaveChangesAsync` received exactly twice |
| 65 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests` | `Handle_ShouldUploadZipWithImmutableCacheHeaders_WhenPublishing` | `UploadAsync` received with the public container, the zip blob name, `"application/zip"`, `options.ZipCacheControl`, `overwrite: false` |
| 66 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests` | `Handle_ShouldWritePinnedMarketplace_WhenPublishing` | `UploadAsync` received with `m/e3a-{slug}/1.0.0/marketplace.json`, `"application/json"`, `options.MarketplaceCacheControl`, `overwrite: true` |
| 67 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests` | `Handle_ShouldSkipZipUpload_WhenBlobAlreadyExists` | `ListByPrefixAsync` returns the zip name → no `UploadAsync` for the zip path; version still reaches `Published` with the locally computed sha256 |
| 68 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests` | `Handle_ShouldResumeFromBuilding_WhenVersionIsAlreadyBuilding` | reaches `Published`; `SaveChangesAsync` received exactly once (no second `Building` checkpoint) |
| 69 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerGuardTests` | `Handle_ShouldThrowNotFound_WhenVersionDoesNotExist` | `NotFoundCoreException` / `PublishVersionNotFound`; no save; no blob call |
| 70 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerGuardTests` | `Handle_ShouldDoNothing_WhenVersionIsTerminal` `[Theory]` `Published`, `Failed` | no save; no blob call |
| 71 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerFailureTests` | `Handle_ShouldFailVersion_WhenEngineerIsMissing` | `Status == Failed`, `FailureReason == ErrorCodes.EngineerNotFound`; `SaveChangesAsync` received once |
| 72 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerFailureTests` | `Handle_ShouldFailVersion_WhenSnapshotIsEmpty` | `FailureReason == ErrorCodes.EngineerSnapshotEmpty`; no zip upload; two saves |
| 73 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerFailureTests` | `Handle_ShouldFailVersion_WhenStructureValidationFails` | `Status == Failed`, `FailureReason` contains `ErrorCodes.PluginNoInstallableContent`; no zip upload; `engineer.Status` unchanged |
| 74 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | `Handle_ShouldWriteMarketplaceWithEveryPublishedEngineer_WhenCalled` | one `UploadAsync` to `marketplace.json` with `"application/json"`, `options.MarketplaceCacheControl`, `overwrite: true`; uploaded JSON contains both plugin names |
| 75 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | `Handle_ShouldExcludeUnlistedEngineers_WhenGenerating` | the unlisted engineer's plugin name absent from the uploaded JSON |
| 76 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | `Handle_ShouldSkipEngineer_WhenLatestVersionIsNotPublished` | that engineer absent; the others present |
| 77 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | `Handle_ShouldPageThroughAllEngineers_WhenResultsExceedOnePage` | `FindPaginatedAsync` received for pages 1 and 2; both pages' plugins in the JSON |
| 78 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | `Handle_ShouldThrowInternalServerError_WhenPageCapIsExceeded` | `InternalServerErrorCoreException` / `MarketplaceEngineerLimitExceeded`; no `UploadAsync` |
| 79 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests` | `Handle_ShouldFallBackToSlug_WhenOwnerHasNoUserName` | uploaded JSON `author.name` equals the engineer slug |
| 80 | `Publishing/GetPublishStatus/GetPublishStatusQueryValidatorTests` | `Validate_ShouldPass_WhenQueryIsValid` | `IsValid` true |
| 81 | `Publishing/GetPublishStatus/GetPublishStatusQueryValidatorTests` | `Validate_ShouldFail_WhenVersionIdIsEmpty` | `ErrorCodes.PublishVersionIdRequired` |
| 82 | `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests` | `Handle_ShouldReturnStatus_WhenCallerOwnsTheEngineer` | `VersionId`, `EngineerId`, `Status`, `SemanticVersion`, `ZipUrl` all mapped |
| 83 | `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests` | `Handle_ShouldReturnFailureReason_WhenVersionFailed` | `Status == "Failed"`, `FailureReason` mapped, `ZipUrl` null |
| 84 | `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` / `UserNotAuthenticated` |
| 85 | `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests` | `Handle_ShouldThrowNotFound_WhenVersionDoesNotExist` | `NotFoundCoreException` / `PublishVersionNotFound` |
| 86 | `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests` | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `NotFoundCoreException` / `EngineerNotFound` |
| 87 | `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests` | `Handle_ShouldThrowForbidden_WhenCallerIsNotOwner` | `ForbiddenCoreException` / `EngineerNotOwned` |

Test file split so no file exceeds ~100 lines: `ProcessPublishJobHandlerTests` / `…GuardTests` / `…FailureTests`,
and `PublishEngineerHandlerTests` / `…GuardTests`, exactly as `UploadEngineerDraftHandlerTests` /
`…GuardTests` already do.

## Docs sync (`.claude/rules/docs-sync.md` — divergence, blocking)

**`docs/architecture.md`**
- Diagram: replace `BackgroundService ◄── Storage Queue (publish pipeline)` under `E3A.Api` with a sibling box `E3A.Jobs (Azure Functions v4 isolated, .NET 10) ◄── Storage Queue publish-jobs`.
- "Reads never hit the API" bullet: replace "served from Blob via Cloudflare cache" wording about purging with the cache-header policy — `marketplace.json` `max-age=60`, zips `max-age=31536000, immutable`, set at blob write time.
- "Publish pipeline (queue worker)" section: rewrite the step list to the implemented order — dequeue → ignore unless `Queued`/`Building` → `Building` → freeze drafts into `snapshots/{versionId}` → assemble from snapshot + frozen manifest → validate structure → *(security scan — next slice)* → deterministic zip + sha256 → upload `public/z/…` → `Published` + `LatestVersionId` → pinned `public/m/…/marketplace.json` → regenerate root `marketplace.json`. **Delete "purge Cloudflare cache".** Poison after `maxDequeueCount` (5).
- "Backend style" paragraph: add `E3A.Jobs` to the project list; remove `Cloudflare purger` from the `E3A.Infrastructure` list and note that the plugin builder / marketplace generator live in `E3A.Application/Publishing/Shared`.

**`docs/implementation-plan.md`**
- "Locked stack" line: `publish pipeline as a BackgroundService reading Storage Queue` → `publish pipeline as an isolated Azure Functions worker (E3A.Jobs, .NET 10 v4) reading Storage Queue publish-jobs`. Drop `Cloudflare … purge` from the same sentence's implications; keep Cloudflare as CDN/rate-limit only.
- Key architecture decision 3: replace "marketplace.json cached + purged via Cloudflare API on every publish" with the cache-header policy.
- Data model `versions` row: add `FailureReason`; note `ScanReportJson` arrives with the scanner slice.
- `engineers`/`teams` row: `Status(Draft|Published|Unlisted|Deleted)`.
- "API surface" line: add `POST {id}/unlist`, `POST {id}/relist`, and confirm `GET /publish/{versionId}/status`.
- "Publish pipeline" line: same rewrite as architecture.md; remove "purge Cloudflare".
- **P3 phase bullet**: record the scanner split — P3 is delivered as `publish-pipeline` (this slice) then `security-scan` (scanner engine, rule tiers, corpus fixtures, `Rejected` path), and state that the scanner MUST land before the first real publish.

**`docs/plugin-spec.md`**
- `marketplace.json` section: add the wrapper shape decided in D18 (`name` / `owner` / `plugins`); the existing snippet only shows one entry.
- Naming/attribution: note that before GitHub OAuth, `author.name` is the creator's Identity `UserName` (falling back to the engineer slug) and `author.url` is the e3a catalog page `https://<domain>/e/{slug}`; the GitHub URL arrives with the OAuth slice.

`docs/security-scan.md` is **not** touched — the scanner being unbuilt is incompleteness, not divergence.

## Build order

1. `Core.Azure` additions + `AzureOptions` + `PublishingOptions` + `appsettings.json` + `ErrorCodes` + both resx.
2. `E3A.Domain/Publishing/*` + `IUserRepository` + `Engineer.Unlist/Relist` + `EngineerStatus.Unlisted`.
3. `AppDbContext` config + repositories + DI + migration `versions002`. Build green here.
4. `E3A.Application/Publishing/Shared/*` (pure units) + their tests. Tests green here.
5. `PublishEngineer` slice + `PublishRequestedEventHandler` + controller action + tests.
6. `ProcessPublishJob` + `RegenerateMarketplace` + `E3A.Jobs` project + `E3A.slnx` + tests. **Pipeline is end-to-end functional at the end of this step.**
7. `GetPublishStatus` slice + `PublishController` + tests.
8. `UnlistEngineer` / `RelistEngineer` slices + controller actions + tests.
9. Postman + docs sync.

## Definition of done

- [ ] `POST /api/engineers/{id}/publish` returns `202` with a `PublishStatusResult` whose `Status` is `Queued` and `SemanticVersion` is `1.0.0` on first publish.
- [ ] Every guard in `PublishEngineerHandler` (unauthenticated, not found, not owner, no draft, publish in progress, version cap) throws the exception in the Error-codes table and calls `SaveChangesAsync` zero times.
- [ ] `PublishRequestedDomainEvent` is raised inside `ItemVersion.Create` and reaches `IStorageQueueClient.SendMessageAsync` with `visibilityTimeout = PublishingOptions.QueueVisibilityTimeoutSeconds`.
- [ ] `api/E3A.Jobs` exists, targets Functions v4 isolated on `net10.0`, contains **only** `Program.cs`, `host.json`, `Functions/ProcessPublishJobFunction.cs`, `E3A.Jobs.csproj`, and is listed in `api/E3A.slnx`.
- [ ] `host.json` sets `extensions.queues.batchSize = 1` and `newBatchThreshold = 0`.
- [ ] `ProcessPublishJobFunction` has no branches, no `try`/`catch`, and sends exactly `ProcessPublishJobCommand` then `RegenerateMarketplaceCommand`.
- [ ] `ProcessPublishJobHandler` returns immediately for any status other than `Queued`/`Building`, and calls `SaveChangesAsync` at most twice on every path.
- [ ] `DeterministicZipper.Create` produces byte-identical output and an identical sha256 for the same file set regardless of input order (tests 23–24 prove it).
- [ ] The zip is uploaded with `application/zip`, `PublishingOptions.ZipCacheControl`, `overwrite: false`, and is skipped entirely when the blob already exists.
- [ ] Both marketplace writes use `application/json`, `PublishingOptions.MarketplaceCacheControl`, `overwrite: true`, and each is a single in-memory-then-upload operation.
- [ ] `RegenerateMarketplaceHandler` pages with `FindPaginatedAsync` and throws `MarketplaceEngineerLimitExceeded` rather than truncating.
- [ ] No Cloudflare purge code exists anywhere in the diff.
- [ ] `Core.Azure` gains three members; the two pre-existing signatures are byte-identical to `main`.
- [ ] Every new tunable is in `PublishingOptions`/`AzureOptions` bound from `appsettings.json`; the only new literals are `e3a-` (PluginName), `.claude-plugin/plugin.json`, `"archive"`, the 1980-01-01 zip timestamp, `application/zip`, `application/json`, `marketplace.json`, and the sha256 hex width — each a named constant with a WHY comment.
- [ ] No `DefaultCodes` class is introduced; every new action carries only the class-level `[Authorize]` with the owner check in the handler.
- [ ] No new exception type; every throw is a `Core.Errors` type from the skill's §5.9 table.
- [ ] Every new `ErrorCodes` constant has a key in **both** `Messages.en.resx` and `Messages.ar.resx`.
- [ ] `ItemVersion` is registered in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`; no ad-hoc `IsDeleted` checks anywhere.
- [ ] Migration `versions002` creates `ItemVersions` with the unique filtered index on `(ItemType, ItemId, VersionNumber)`.
- [ ] All 87 tests in the test plan exist with the exact names given, and `dotnet test` is green.
- [ ] `dotnet build api/E3A.slnx` is clean with zero new warnings (`TreatWarningsAsErrors` is on).
- [ ] No file exceeds ~100 lines; file-scoped namespaces; `sealed`; `DateTimeOffset` only; `.ConfigureAwait(false)` on every non-controller, non-test await; braces on every `if`.
- [ ] `postman/e3a.postman_collection.json` has "Publish Engineer", "Unlist Engineer", "Relist Engineer" and a `Publishing` folder with "Get Publish Status".
- [ ] `docs/architecture.md`, `docs/implementation-plan.md` and `docs/plugin-spec.md` are updated exactly as listed in Docs sync; `docs/security-scan.md` is untouched.
