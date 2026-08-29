# Plan — Teams (pinned-member team plugin)

## Scope-split judgment (required by the brief)

The acceptance file's split is **sound**. `team-compile-merge` (hook concatenation with per-member
attribution, `.mcp.json`/`.lsp.json` merge-by-server-name, the "newer member versions" prompt) is
separable because it only adds *more* roots to the assembled tree; it changes no schema, no endpoint,
no plugin name and no pin semantics. This slice ships an installable `e3a-team-{slug}` plugin built
from `agents/`, `skills/`, `commands/` — exactly the three roots `PluginStructureValidator` already
treats as installable content. One consequence must be stated loudly and is carried into Decisions
and Docs: **in this slice a member's hooks, `.mcp.json`, `.lsp.json`, `output-styles/`, `monitors/`,
`bin/`, `themes/` are NOT carried into the team plugin.** `docs/plugin-spec.md` currently promises
they are merged, so that promise is edited to say "deferred", not silently broken.

## Goal

A signed-in creator can create a team, give it a creator-typed slug, put up to `MaxMembersPerTeam`
published engineers in it in an explicit order — each pinned to an exact `ItemVersion` — and publish
the team. The existing `publish-jobs` worker builds one installable Claude Code plugin named
`e3a-team-{slug}` from the pinned members' immutable `snapshots/{versionId}/**` content, namespaced
so two members' skills and agents can never overwrite each other, uploads it to the same public
container at `z/e3a-team-{slug}/{semanticVersion}.zip`, writes the pinned per-version marketplace,
and lists the team in the root `marketplace.json` alongside engineers. A member engineer publishing
a new version afterwards cannot change any already-published team: the roster is frozen into the
version row and the content is read from a per-version snapshot prefix that is never rewritten.

## Scope

**In**
- `Team` + `TeamMember` aggregate, EF configuration, migration `teams005`.
- Team CRUD: create, update, get, list-mine, slug availability, delete — owner-gated, mirroring the
  engineer slices.
- Full-replace member management (`PUT /api/teams/{teamId}/members`), pinning each member to an exact
  published `ItemVersion`.
- `TeamsOptions` caps: 10 teams per creator, 10 members per team, slug/name/tag limits.
- `POST /api/teams/{teamId}/publish` gives `202`, reusing `ItemVersion` with `ItemType.Team` and the
  existing `publish-jobs` queue.
- Team assembly in the existing worker: a shared pipeline with two builders (Decision 3).
- `e3a-team-{slug}` plugin naming; teams in `marketplace.json`.
- Postman collection; docs sync.

**Out**
- Hooks / `.mcp.json` / `.lsp.json` / `output-styles/` / `monitors/` / `bin/` / `themes/` merging.
- Team unlist/relist endpoints (`TeamStatus.Unlisted` is declared for parity, unreachable here).
- Security scan on team publish (parked on `feature/security-scan`, unmerged — acceptance decision 11).
- Team install counts, reports, catalog surfaces, frontend.
- Any new Azure resource. **None is needed:** teams read the existing `snapshots` container, write the
  existing `public` container, and ride the existing `publish-jobs` queue. Nothing to escalate.

**Deferred**
- `team-compile-merge`: the richer merge rules and the "a member has a newer version, republish to
  adopt it" prompt. Deferred by the acceptance file; safe because nothing is deployed and no team can
  be published to a live domain yet.
- Team catalog/detail endpoints (`GET /api/catalog` type filter, team detail): the catalog slice owns
  those shapes and this slice does not touch `CatalogEngineerResult`.

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Plugin name | `PluginName.ForTeam(slug)` produces `e3a-team-{slug}`; `PluginName.For` is **renamed** `ForEngineer` | Dev's locked answer (acceptance decision 1). Renaming the engineer method makes it impossible to hand a team slug to the engineer namer; 3 production call sites. |
| 2 | Versioning | Reuse `ItemVersion` with `ItemType.Team`; no new table | Acceptance decision 2. The enum value and the `(ItemType, ItemId, VersionNumber)` unique index already exist. |
| 3 | Worker shape | **One worker, one pipeline, two builders.** `ProcessPublishJobHandler` keeps the version guard, the Building transition, and the validate/zip/upload/pinned-marketplace tail, and switches on `ItemType` to call `EngineerPublishBuilder.BuildAsync` or `TeamPublishBuilder.BuildAsync` — both static units in `Publishing/Shared` taking their dependencies as parameters | Acceptance decision 3 authorises exactly this if an inline branch makes the handler unwieldy, and it does: an inline branch pushes the handler past 150 lines with 10 constructor dependencies and two entity types to mark published. The chosen shape mirrors the codebase's own idiom (`DraftSnapshotFreezer.FreezeAsync(storageBlobClient, ...)`) — static units with explicit dependencies. No new interface, no second worker, no DI abstraction; handler stays about 80 lines. |
| 4 | Member pinning | `TeamMember.PinnedVersionId` is an exact `ItemVersion.Id`, resolved and stored when members are set | Acceptance decision 4. |
| 5 | How the client names the version | `pinnedVersionId` is **optional** in the request. Null resolves to the member's **existing pin** when the engineer is already a member, otherwise to the engineer's `LatestVersionId`. Null with no `LatestVersionId` gives `TEAM_MEMBER_NOT_PUBLISHED` | No endpoint lists an engineer's versions, so a required id would make the API unusable. Falling back to the existing pin means a reorder or an unrelated add can never silently re-pin an existing member to a newer version — re-pinning stays an explicit act, which is precisely the deferred "adopt newer versions" flow. |
| 6 | Unpublished member | Rejected. The resolved version must exist, be `ItemType.Engineer`, belong to that engineer, and be `Published` | Acceptance decision 5. |
| 7 | Unlisted / deleted member engineer | Adding an **unlisted** engineer is allowed as long as a `Published` version exists to pin. A **deleted** engineer cannot be newly added (`GetByIdAsync` is soft-delete filtered, giving `ENGINEER_NOT_FOUND`), but an already-pinned member that is later deleted still builds, because the worker never loads the member `Engineer` | Acceptance decision 6: unlist is not takedown and published teams keep working. The check is about the *version*, not the engineer's listing state. |
| 8 | Cross-owner membership | Allowed; no owner check on member engineers | Acceptance decision 7. |
| 9 | Team slug policy | Identical to engineers: creator-typed, kebab-case `^[a-z0-9]+(-[a-z0-9]+)*$`, 3 to `SlugMaxLength`, reserved words rejected, auto-suffixed on collision, frozen once `LatestVersionId != null` | Acceptance decision 8. Achieved by **reuse, not duplication**: `EngineerSlugGenerator` moves to `E3A.Domain/SharedKernel/SlugGenerator.cs` and `EngineerSlugResolver` moves to `E3A.Application/Shared/SlugResolver.cs` with a `Func<string, CancellationToken, Task<bool>>` uniqueness probe instead of `IEngineerRepository`. A copied resolver would duplicate the suffix-length invariant, the exact defect class skill section 8.3 exists to prevent. |
| 10 | Team slug uniqueness scope | Unique within the **Teams** table only | Plugin names are namespaced (`e3a-` versus `e3a-team-`), so a team slug equal to an engineer slug is structurally harmless — the dev's stated reason for choosing option (b). |
| 11 | Reserved slugs for teams | `TeamsOptions.ReservedSlugs`, its own list, seeded with the same values as `Engineers.ReservedSlugs` | Caps and lists live in `[Area]Options` (skill section 8.1). A shared list would couple two areas' configuration for no benefit. |
| 12 | Members-per-team cap | `TeamsOptions.MaxMembersPerTeam = 10` | The hard ceiling is already `MaxPluginFileCount` (400) and `MaxPluginBytes` (100 MB) applied to the merged tree; the member cap is a usability guard, not a safety one. 10 mirrors `MaxTeamsPerCreator` and comfortably covers the P5 demo (3-engineer team) and the flagship seed squad (4 members). |
| 13 | Member management API | **One idempotent full-replace endpoint** `PUT /api/teams/{teamId}/members` carrying the whole ordered roster, not three endpoints | Delivers add, remove and reorder (acceptance in-scope item 3) in one slice; makes ordering a property of the payload rather than of a mutation sequence; removes the member-not-found and reorder-set-mismatch guards entirely; and matches the composer in `docs/design-prompt.md` item 9 ("draggable ordered member list"), which edits the whole list. |
| 14 | Sort order storage | `TeamMember.SortOrder` is the member's zero-based index in the submitted list; `Team.ReplaceMembers` clears and rebuilds the collection so indices are always `0..n-1` with no gaps. **No unique index on `(TeamId, SortOrder)`** | Delete-and-reinsert is trivially deterministic at 10 rows or fewer and avoids the transient duplicate-order violations a unique index would cause inside a single `SaveChangesAsync`. Nothing references a `TeamMember.Id`. |
| 15 | Membership uniqueness | `HasIndex(x => new { x.TeamId, x.EngineerId }).IsUnique().HasFilter("[IsDeleted] = 0")` plus a validator rule rejecting a duplicate `engineerId` in the payload | Mirrors the `ItemVersion` unique-business-key pattern. The index is the backstop; the validator gives the readable 422. |
| 16 | `TeamMember` key | Own `Guid Id` primary key (from `Core.DDD.Entity`) plus the unique index above — **not** the composite `(TeamId, EngineerId)` key in `docs/implementation-plan.md:42` | `Entity(Guid id)` mandates a `Guid` key and `ISoftDeletable`; a composite key fights the base class. The doc is corrected. |
| 17 | Foreign keys | `Team` to `Members`: `OnDelete(DeleteBehavior.Cascade)` (owned composition; removing from the collection deletes the orphan row). `TeamMember.EngineerId` and `TeamMember.PinnedVersionId` are **plain `Guid`s with no foreign key and no navigation** | Mirrors `ItemVersion.ItemId`. Decisions 6/7 require a team to survive its member engineer being deleted, which a foreign key would forbid. Cascade never actually fires because `Team.Delete()` soft-deletes. |
| 18 | Denormalised member fields | `TeamMember` stores `EngineerSlug` and `PinnedSemanticVersion` beside the ids | Provably immutable: a member must have a `Published` version, so `LatestVersionId != null`, so `IsSlugMutable == false`; and a published `ItemVersion`'s `SemanticVersion` never changes. This lets `GET /api/teams/{id}` answer with zero extra queries and lets the worker assemble a team whose member engineer row has since been deleted. |
| 19 | What a team version freezes | `ItemVersion.FrozenManifestJson` for a team holds a serialized `TeamRosterResult`: the ordered `(EngineerId, EngineerSlug, PinnedVersionId, PinnedSemanticVersion, SortOrder)` list | This is what makes the pinning invariant airtight rather than merely conventional: after publish, editing `TeamMember` rows cannot alter a published version's content, because the worker reads the roster from the version row, not from the table. Mirrors how an engineer version freezes its import manifest. |
| 20 | Member content source | Per member, every blob under `snapshots/{pinnedVersionId}/`, filtered by that version's own `FrozenManifestJson` allowed-target-path set (`Imported` union `Converted`) | Identical filter to the engineer publish, so a member contributes to a team exactly the files it ships in its own plugin. `DraftSnapshotFreezer` writes a fresh prefix per version and never deletes another version's prefix, so pinned snapshots are permanent. |
| 21 | Roots carried into a team | `agents/`, `skills/`, `commands/` only. Everything else in a member snapshot is dropped | The deferred `team-compile-merge` owns the rest. These three are exactly `PluginStructureValidator.InstallableRoots`. A team whose members contribute none of them fails validation with `PLUGIN_NO_INSTALLABLE_CONTENT`. |
| 22 | Skills namespacing | Always, unconditionally: `skills/{skill-slug}/...` becomes `skills/{member-slug}--{skill-slug}/...` | `docs/plugin-spec.md`. Unconditional namespacing is order-independent and makes the team's structure legible. |
| 23 | `agents/` and `commands/` collisions | On collision **every** colliding member's file is prefixed `{member-slug}--`, not just the later ones | The spec says "prefixed on collision" without saying whose. Prefixing all of them is symmetric, so the output does not depend on member order — one fewer source of non-determinism — and no member is arbitrarily privileged. Non-colliding names stay clean. |
| 24 | Residual duplicate paths | `PluginStructureValidator` gains a duplicate-path rule (`PLUGIN_DUPLICATE_PATH`, `OrdinalIgnoreCase`), applied on both publish paths | A member file literally named `{otherslug}--x.md` could collide with another member's prefixed name; without a guard `DeterministicZipper` would silently emit two entries for one path. It also catches the pre-existing engineer hazard of two manifest targets differing only by case. |
| 25 | Determinism | Members ordered by `SortOrder` then `EngineerId`; each member's files ordered `Ordinal` by path; the final tree ordered `Ordinal` by path; `DeterministicZipper` already fixes the entry timestamp | Same roster gives a byte-identical zip and therefore an identical sha256, verified by test. |
| 26 | Team author metadata | `authorName` is the team owner's Identity `UserName`, falling back to the team slug; `author.url` is `{PublicSiteUrl}/t/{slug}` | Mirrors the engineer rule exactly. `/t/:name` is the real route in `web/src/App.tsx:55`. Both path segments move into a new `PublicCatalogUrl` unit so `/e/` and `/t/` stop being inline literals (constitution section 0.3). |
| 27 | Team description and tags at build time | Read live from the `Team` row, not frozen | Mirrors engineers: `MarketplaceDocumentGenerator.GeneratePlugin` already reads `engineer.Description` and `Tags` live. Only the roster is pinned. |
| 28 | Empty team publish | Rejected in `PublishTeamHandler` (`TEAM_EMPTY`, 400) **and** re-checked in `TeamPublishBuilder`, which fails the version with `TEAM_EMPTY` | Acceptance decision 10, plus the roster could be emptied between enqueue and dequeue. |
| 29 | Members editable after publish | Yes. Only the **slug** freezes | The published zip and its frozen roster are immutable; edits take effect on the next team publish. This is the republish flow's foundation. |
| 30 | `PublishStatusResult.EngineerId` | Renamed `ItemId`, plus a new `ItemType` field | The field is now wrong for teams. Not a breaking change in practice: `web/` is still on static fixtures (`web/src/lib/catalog`) and never calls the publish-status endpoint. |
| 31 | `GetPublishStatusQueryHandler` ownership check | Branches on `version.ItemType`; teams check `Team.OwnerUserId` and throw `TEAM_NOT_FOUND` / `TEAM_NOT_OWNED` | Today it unconditionally loads an `Engineer`, so a team version would 404. |
| 32 | Marketplace regeneration | Two bounded page loops, one per item type, each extracted into a static collector; results concatenated and ordered `Ordinal` by plugin name | Keeps `RegenerateMarketplaceHandler` at about 20 lines. Ordering by `e3a-{slug}` / `e3a-team-{slug}` preserves today's engineer ordering exactly. |
| 33 | Marketplace bound | `MarketplaceEngineerLimitExceeded` keeps guarding the engineer loop unchanged; a new `MARKETPLACE_TEAM_LIMIT_EXCEEDED` guards the team loop. Both use the same `MarketplacePageSize` and `MarketplaceMaxPages`, so the document's bound doubles from `pageSize * maxPages` to `2 * pageSize * maxPages` entries | A shared counter would let a large engineer catalogue silently starve teams; a per-type guard names the offending type in the failure. |
| 34 | `TeamStatus.Unlisted` | Declared in the enum, unreachable in this slice (no `Unlist()`/`Relist()` method, no endpoint) | Status vocabulary parity with `EngineerStatus`, mirroring the accepted precedent of `ItemType.Team` shipping unused. |
| 35 | No `InstallCount` or `DraftManifestJson` on `Team` | Omitted | Teams have no upload draft, and install counting is explicitly out of the run. The "same shape" claim in `docs/implementation-plan.md` is corrected rather than satisfied with dead columns. |
| 36 | `ITeamMemberRepository` | Not created | Members are only ever loaded through the `Team` aggregate with an `Include`; `IRepository<Team>` change tracking persists adds and removes. |
| 37 | Security scan | Not wired | Acceptance decision 11. Recorded so its absence is not read as an oversight. |
| 38 | `appsettings.json` | Gitignored (`.gitignore:23`). The implementer adds the `Teams` section to the local file so the API runs; tests build `TeamsOptions` directly | Constitution section 2. **The new `Teams` section must be announced to the dev for his environments and Azure App Configuration.** |

## Existing code touched

| File | Change |
|------|--------|
| `api/E3A.Domain/Engineers/EngineerSlugGenerator.cs` | **Delete** — moved to `E3A.Domain/SharedKernel/SlugGenerator.cs`, class renamed `SlugGenerator`, members and WHY comments unchanged. |
| `api/E3A.Application/Engineers/Shared/EngineerSlugResolver.cs` | **Delete** — moved to `E3A.Application/Shared/SlugResolver.cs` with a generalized signature. |
| `api/E3A.Application/Engineers/Shared/SlugAvailabilityResult.cs` | **Move** to `api/E3A.Application/Shared/SlugAvailabilityResult.cs`, namespace becomes `E3A.Application.Shared`. Record unchanged. |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs` | `EngineerSlugGenerator.NormalizeTypedSlug` becomes `SlugGenerator.NormalizeTypedSlug`; `EngineerSlugResolver.ResolveUniqueAsync(slug, engineerRepository, generator, options, ct)` becomes `SlugResolver.ResolveUniqueAsync(slug, engineerRepository.IsSlugExistsAsync, generator, options.SlugMaxLength, options.SlugSuffixSize, ct)`. Usings updated. |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs` | `EngineerSlugGenerator` becomes `SlugGenerator`; using updated. No rule changes. |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs` | Same two symbol changes as `CreateEngineerHandler`. |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerValidator.cs` | `EngineerSlugGenerator` becomes `SlugGenerator`. |
| `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQuery.cs` | Using changes to `E3A.Application.Shared` for `SlugAvailabilityResult`. |
| `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandler.cs` | Symbol changes as above; using changes for `SlugAvailabilityResult`. |
| `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryValidator.cs` | `EngineerSlugGenerator` becomes `SlugGenerator`. |
| `api/E3A.Application/Publishing/Shared/PluginName.cs` | `For` renamed `ForEngineer`; add `ForTeam(string slug)` returning `$"{Prefix}{TeamSegment}{slug}"` with `private const string TeamSegment = "team-";` and a WHY comment ("teams carry their own namespace segment so a team slug can never collide with an engineer slug"). |
| `api/E3A.Application/Publishing/Shared/PluginJsonGenerator.cs` | `PluginName.For` becomes `ForEngineer`; author URL now `PublicCatalogUrl.ForEngineer(...)`; add overload `Generate(Team team, string semanticVersion, string authorName, PublishingOptions options)` using `PluginName.ForTeam` and `PublicCatalogUrl.ForTeam`. |
| `api/E3A.Application/Publishing/Shared/MarketplaceDocumentGenerator.cs` | Same three changes: renamed call, `PublicCatalogUrl`, and a `GeneratePlugin(Team team, ItemVersion version, string authorName, PublishingOptions options)` overload. |
| `api/E3A.Application/Publishing/Shared/PluginStructureValidator.cs` | Add overload `Validate(List<PluginFile> files, PublishingOptions options)` containing today's checks 2–6 plus the new duplicate-path rule; the existing 3-argument `Validate(files, manifest, options)` keeps its signature and now returns the manifest-coverage error prepended to the 2-argument result. No existing call site or test changes. |
| `api/E3A.Application/Publishing/Shared/PublishStatusResult.cs` | `Guid EngineerId` becomes `Guid ItemId`; new `string ItemType` after it. |
| `api/E3A.Application/Publishing/Shared/PublishStatusResultGenerator.cs` | Pass `version.ItemId` and `version.ItemType.ToString()`. |
| `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs` | Rewritten per "Worker control flow" below: gains `ITeamRepository`, loses direct assembly code, switches on `ItemType`. |
| `api/E3A.Application/Publishing/RegenerateMarketplace/RegenerateMarketplaceHandler.cs` | Rewritten to call `PublishedEngineerCollector` and `PublishedTeamCollector`, concatenate, order `Ordinal` by `Name`, serialize, upload. Gains `ITeamRepository`. The private `PublishedEngineerVersion` record and `ResolveAuthorName` move into `PublishedEngineerCollector`. |
| `api/E3A.Application/Publishing/GetPublishStatus/GetPublishStatusQueryHandler.cs` | Gains `ITeamRepository`; branches on `version.ItemType` for the ownership check. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Add the `// Teams` group and two publishing codes (see Error codes). |
| `api/E3A.Application/DependencyInjection.cs` | `services.Configure<TeamsOptions>(configuration.GetSection(TeamsOptions.SectionName));` |
| `api/E3A.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ITeamRepository, TeamRepository>();` |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | Add `IOptions<TeamsOptions> teamsOptions` to the primary constructor; `DbSet<Team> Teams`, `DbSet<TeamMember> TeamMembers`; call `ConfigureTeams(modelBuilder)` from `OnModelCreating`; add `Team` and `TeamMember` to `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. |
| `api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add teams005`. |
| `api/E3A.Api/Resources/Messages.en.resx` | 28 new keys (see Error codes). |
| `api/E3A.Api/Resources/Messages.ar.resx` | The same 28 keys, Arabic, no tashkeel, placeholders preserved. |
| `api/E3A.Api/appsettings.json` (gitignored, local only) | New `Teams` section with the values in `TeamsOptions`. |
| `postman/e3a.postman_collection.json` | New `Teams` folder with 8 requests (see API surface), mirroring the `Engineers` folder shape: bearer auth inherited, `Content-Type: application/json` header on bodied requests, `{{baseUrl}}` / `{{teamId}}` variables. |
| `api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests.cs` | Constructor gains `Substitute.For<ITeamRepository>()` in the `ProcessPublishJobHandler` argument list, positioned after `_engineerRepository`. No assertion changes. |
| `api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerGuardTests.cs` | Same constructor change. No assertion changes. |
| `api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerRetryTests.cs` | Same constructor change. No assertion changes. |
| `api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerFailureTests.cs` | Same constructor change, **plus** `Handle_ShouldFailVersion_WhenEngineerIsMissing` changes `Received(1)` to `Received(2)` on `SaveChangesAsync`, because the item lookup now happens after the Building transition (see the save-count table). Nothing else changes. |
| `api/E3A.Tests/Publishing/RegenerateMarketplace/RegenerateMarketplaceHandlerTests.cs` | Constructor gains `ITeamRepository`; add a constructor-level stub `_teamRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<Func<IQueryable<Team>, IQueryable<Team>>>(), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), Arg.Any<bool>())` returning `new PageData<Team> { Items = [], TotalPages = 0 }`. No assertion changes. |
| `api/E3A.Tests/Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests.cs` | Constructor gains `ITeamRepository`; `result.EngineerId` assertions become `result.ItemId`. |
| `api/E3A.Tests/Publishing/Shared/PublishStatusResultGeneratorTests.cs` | `EngineerId` becomes `ItemId`; add an assertion that `ItemType` is `nameof(ItemType.Engineer)`. |
| `api/E3A.Tests/Publishing/Shared/ItemVersionFactory.cs` | Add `public static ItemVersion QueuedTeam(Guid teamId, int versionNumber = 1, string semanticVersion = DefaultSemanticVersion, string frozenManifestJson = DefaultFrozenManifestJson)` calling `ItemVersion.Create(ItemType.Team, ...)`. |
| `api/E3A.Tests/Engineers/EngineerSlugGeneratorTests.cs` | Rename file/class to `api/E3A.Tests/SharedKernel/SlugGeneratorTests.cs`, namespace `E3A.Tests.SharedKernel`, symbol `SlugGenerator`. Test bodies unchanged. |
| `api/E3A.Tests/Engineers/EngineerSlugGeneratorTypedInputTests.cs` | Rename to `api/E3A.Tests/SharedKernel/SlugGeneratorTypedInputTests.cs` the same way. |
| `api/E3A.Tests/Engineers/Shared/EngineerSlugResolverTests.cs` | Rename to `api/E3A.Tests/Shared/SlugResolverTests.cs`, class `SlugResolverTests`, namespace `E3A.Tests.Shared`; calls pass `engineerRepository.IsSlugExistsAsync` and the two ints instead of the repository and options object. Assertions unchanged. |
| `docs/implementation-plan.md` | Docs sync (see Docs sync). |
| `docs/plugin-spec.md` | Docs sync. |
| `docs/architecture.md` | Docs sync. |

## Files to create

### Domain

| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/E3A.Domain/SharedKernel/SlugGenerator.cs` | `namespace E3A.Domain.SharedKernel;` `public static class SlugGenerator` | Byte-for-byte the body of today's `EngineerSlugGenerator`: `Normalize(string displayName, int maxLength)`, `NormalizeTypedSlug(string? slug)`, `IsValidFormat(string slug)`, the `SlugFormatMatchTimeout` / `SlugFormatRegex` statics and both WHY comments. |
| 2 | `api/E3A.Domain/Teams/TeamStatus.cs` | `namespace E3A.Domain.Teams;` `public enum TeamStatus` | `Draft, Published, Unlisted, Deleted` — value order mirrors `EngineerStatus`. No extensions class (mirrors `EngineerStatus.cs`). |
| 3 | `api/E3A.Domain/Teams/TeamMemberPin.cs` | `public sealed record TeamMemberPin(Guid EngineerId, string EngineerSlug, Guid PinnedVersionId, string PinnedSemanticVersion);` | Resolved, validated pin handed to `Team.ReplaceMembers`. |
| 4 | `api/E3A.Domain/Teams/TeamMember.cs` | `public class TeamMember : AuditEntity` | Properties, all `{ get; private set; }`: `Guid TeamId`, `Guid EngineerId`, `string EngineerSlug = default!`, `Guid PinnedVersionId`, `string PinnedSemanticVersion = default!`, `int SortOrder`. `private TeamMember(Guid id, Guid? createdBy) : base(id, createdBy) { }`. `public static TeamMember Create(Guid teamId, TeamMemberPin pin, int sortOrder, Guid createdBy)` returns `new TeamMember(Guid.NewGuid(), createdBy) { TeamId = teamId, EngineerId = pin.EngineerId, EngineerSlug = pin.EngineerSlug, PinnedVersionId = pin.PinnedVersionId, PinnedSemanticVersion = pin.PinnedSemanticVersion, SortOrder = sortOrder, CreationDate = DateTimeOffset.UtcNow, UpdationDate = DateTimeOffset.UtcNow, }`. No other methods. |
| 5 | `api/E3A.Domain/Teams/Team.cs` | `public class Team : AuditEntity` | See "Domain behaviour" for the exact bodies. |
| 6 | `api/E3A.Domain/Teams/ITeamRepository.cs` | `public interface ITeamRepository : IRepository<Team> { Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken); }` | Mirrors `IEngineerRepository`. |

### Application — shared

| # | Path | Type | Contract |
|---|------|------|----------|
| 7 | `api/E3A.Application/Shared/SlugResolver.cs` | `namespace E3A.Application.Shared;` `public static class SlugResolver` | `public static async Task<string> ResolveUniqueAsync(string baseSlug, Func<string, CancellationToken, Task<bool>> isSlugExistsAsync, IGenerator generator, int slugMaxLength, int slugSuffixSize, CancellationToken cancellationToken)`. Body is today's `EngineerSlugResolver` with `engineerRepository.IsSlugExistsAsync(...)` replaced by `isSlugExistsAsync(...)`, `options.SlugMaxLength` by `slugMaxLength`, `options.SlugSuffixSize` by `slugSuffixSize`, and `EngineerSlugGenerator.Normalize` by `SlugGenerator.Normalize`. Both WHY comments preserved. |
| 8 | `api/E3A.Application/Shared/SlugAvailabilityResult.cs` | `namespace E3A.Application.Shared;` `public sealed record SlugAvailabilityResult(string Slug, bool IsAvailable, string? SuggestedSlug);` | Moved verbatim. |
| 9 | `api/E3A.Application/Options/TeamsOptions.cs` | `public sealed class TeamsOptions` | `public const string SectionName = "Teams";` then `int MaxTeamsPerCreator`, `int MaxMembersPerTeam`, `int DisplayNameMaxLength`, `int DescriptionMaxLength`, `int SlugMaxLength`, `int SlugSuffixSize`, `int SlugMinLength`, `int MaxTags`, `int TagMaxLength`, `int TagsColumnMaxLength`, `List<string> ReservedSlugs { get; set; } = [];` — all `{ get; set; }`. Local `appsettings.json` values: `10, 10, 100, 500, 100, 4, 3, 10, 30, 400,` and the same reserved list as `Engineers`. |

### Application — Teams area

| # | Path | Type | Contract |
|---|------|------|----------|
| 10 | `api/E3A.Application/Teams/Shared/TeamResult.cs` | `namespace E3A.Application.Teams.Shared;` three records | `public sealed record TeamResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, string Status, Guid? LatestVersionId, int MemberCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);` · `public sealed record TeamMemberResult(Guid EngineerId, string EngineerSlug, Guid PinnedVersionId, string PinnedSemanticVersion, int SortOrder);` · `public sealed record TeamDetailResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, string Status, Guid? LatestVersionId, List<TeamMemberResult> Members, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);`. All client-facing; no `LocalizedText` anywhere in e3a, so no `.Localized()` calls. |
| 11 | `api/E3A.Application/Teams/Shared/TeamResultGenerator.cs` | `public static class TeamResultGenerator` | `Generate(Team team)` gives `TeamResult` with `MemberCount = team.Members.Count` and `Status = team.Status.ToString()`. `GenerateDetail(Team team)` gives `TeamDetailResult` whose `Members` is `team.Members.OrderBy(x => x.SortOrder).ThenBy(x => x.EngineerId).Select(x => new TeamMemberResult(x.EngineerId, x.EngineerSlug, x.PinnedVersionId, x.PinnedSemanticVersion, x.SortOrder)).ToList()`. |
| 12 | `api/E3A.Application/Teams/Shared/TeamRosterResult.cs` | two records | `public sealed record TeamRosterResult(List<TeamRosterMemberResult> Members);` · `public sealed record TeamRosterMemberResult(Guid EngineerId, string EngineerSlug, Guid PinnedVersionId, string PinnedSemanticVersion, int SortOrder);` |
| 13 | `api/E3A.Application/Teams/Shared/TeamRosterGenerator.cs` | `public static class TeamRosterGenerator` | `public static TeamRosterResult Generate(Team team)` returns the members ordered by `SortOrder` then `EngineerId`, projected one-to-one. |
| 14 | `api/E3A.Application/Teams/Shared/TeamMemberPinResolver.cs` | `public static class TeamMemberPinResolver` | `public static List<Guid> ResolveVersionIds(List<TeamMemberSelection> selections, List<Engineer> engineers, List<TeamMember> existingMembers)` — for each selection in order: find the engineer by id, throw `NotFoundCoreException(ErrorCodes.EngineerNotFound)` when absent; take `selection.PinnedVersionId`, else the existing member's `PinnedVersionId`, else `engineer.LatestVersionId`; throw `BusinessRuleViolationCoreException(ErrorCodes.TeamMemberNotPublished, context: new Dictionary<string, object> { ["engineerId"] = selection.EngineerId })` when all three are null. `public static List<TeamMemberPin> ResolvePins(List<TeamMemberSelection> selections, List<Engineer> engineers, List<ItemVersion> versions, List<TeamMember> existingMembers)` — resolves the same version id, then finds the version and throws `BusinessRuleViolationCoreException(ErrorCodes.TeamMemberVersionNotPublished, context: new Dictionary<string, object> { ["engineerId"] = selection.EngineerId })` unless `version != null && version.ItemType == ItemType.Engineer && version.ItemId == engineer.Id && version.Status == ItemVersionStatus.Published`; returns `new TeamMemberPin(engineer.Id, engineer.Slug, version.Id, version.SemanticVersion)` in submitted order. Pure, no I/O. |
| 15 | `api/E3A.Application/Teams/CreateTeam/CreateTeamCommand.cs` | `namespace E3A.Application.Teams.CreateTeam;` | `public sealed record CreateTeamCommand(string Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<TeamResult>;` |
| 16 | `api/E3A.Application/Teams/CreateTeam/CreateTeamValidator.cs` | `public sealed class CreateTeamValidator : AbstractValidator<CreateTeamCommand>` | Constructor takes `IOptions<TeamsOptions> teamsOptions`. Rules are `CreateEngineerValidator`'s, one for one, with `SlugGenerator` and `TEAM_*` codes: `Slug` `ValidateRequired(TeamSlugRequired)`; `Must(len >= SlugMinLength)` `WithErrorCode(TeamSlugTooShort)`; `Must(len <= SlugMaxLength)` `WithErrorCode(TeamSlugTooLong)`; `Must(SlugGenerator.IsValidFormat)` `WithErrorCode(TeamSlugInvalid)`; `Must(not in ReservedSlugs, OrdinalIgnoreCase)` `WithErrorCode(TeamSlugReserved)` — each `.When(x => !string.IsNullOrWhiteSpace(x.Slug))`. `DisplayName` `ValidateRequired(TeamDisplayNameRequired).ValidateMaxLength(options.DisplayNameMaxLength, TeamDisplayNameTooLong)` and `Must(x => x.Any(char.IsAsciiLetterOrDigit))` `WithErrorCode(TeamDisplayNameInvalid)` when not blank. `Description` `ValidateMaxLength(options.DescriptionMaxLength, TeamDescriptionTooLong)`. `Tags` `ValidateListMaxItems(options.MaxTags, TeamTooManyTags)`; `RuleForEach(x => x.Tags).ValidateRequired(TeamTagRequired).ValidateMaxLength(options.TagMaxLength, TeamTagTooLong)`. Same `WithMessage` texts as the engineer validator with "engineer" replaced by "team" where the text names it. |
| 17 | `api/E3A.Application/Teams/CreateTeam/CreateTeamHandler.cs` | `public sealed class CreateTeamHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<TeamsOptions> teamsOptions) : IRequestHandler<CreateTeamCommand, TeamResult>` | 1. `currentUserService.UserId` null or `Guid.Empty` gives `UnauthorizedCoreException(UserNotAuthenticated)`. 2. `teamRepository.CountAsync(cancellationToken, x => x.OwnerUserId == ownerUserId)`; at or above `options.MaxTeamsPerCreator` gives `BusinessRuleViolationCoreException(TeamLimitReached, context: { ["limit"] = options.MaxTeamsPerCreator })`. 3. `slug = await SlugResolver.ResolveUniqueAsync(SlugGenerator.NormalizeTypedSlug(request.Slug), teamRepository.IsSlugExistsAsync, generator, options.SlugMaxLength, options.SlugSuffixSize, cancellationToken)`. 4. `Team.Create(ownerUserId, slug, request.DisplayName, request.Description, request.Tags)`. 5. `AddAsync`, one `SaveChangesAsync`. 6. return `TeamResultGenerator.Generate(team)`. |
| 18 | `api/E3A.Application/Teams/UpdateTeam/UpdateTeamCommand.cs` | | `public sealed record UpdateTeamCommand(Guid TeamId, string? Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<TeamResult>;` |
| 19 | `api/E3A.Application/Teams/UpdateTeam/UpdateTeamValidator.cs` | | Mirrors `UpdateEngineerValidator`: `TeamId` `ValidateRequired(TeamIdRequired)`, then every `CreateTeamValidator` slug rule guarded with `.When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug))` plus `ValidateRequired(TeamSlugRequired).When(x => x.Slug != null)`, and the same name/description/tag rules. |
| 20 | `api/E3A.Application/Teams/UpdateTeam/UpdateTeamHandler.cs` | `public sealed class UpdateTeamHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<TeamsOptions> teamsOptions) : IRequestHandler<UpdateTeamCommand, TeamResult>` | Mirrors `UpdateEngineerHandler` exactly: auth guard; `GetByIdAsync(request.TeamId, cancellationToken, include: query => query.Include(x => x.Members))` null gives `NotFoundCoreException(TeamNotFound)`; owner mismatch gives `ForbiddenCoreException(TeamNotOwned)`; private `ResolveSlugChangeAsync` returns null when `request.Slug` is null or already equal, throws `BusinessRuleViolationCoreException(TeamSlugFrozen)` when `!team.IsSlugMutable`, else resolves uniquely; `team.UpdateMetadata(...)`; `team.ChangeSlug(resolved)` when non-null; `Update`; one `SaveChangesAsync`; return `TeamResultGenerator.Generate(team)`. |
| 21 | `api/E3A.Application/Teams/DeleteTeam/DeleteTeamCommand.cs` | | `public sealed record DeleteTeamCommand(Guid TeamId) : IRequest;` |
| 22 | `api/E3A.Application/Teams/DeleteTeam/DeleteTeamValidator.cs` | | `RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);` |
| 23 | `api/E3A.Application/Teams/DeleteTeam/DeleteTeamHandler.cs` | `public sealed class DeleteTeamHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteTeamCommand>` | Mirrors `DeleteEngineerHandler`: auth, `TeamNotFound`, `TeamNotOwned`, `team.Delete()`, `Update`, one `SaveChangesAsync`. |
| 24 | `api/E3A.Application/Teams/GetTeam/GetTeamQuery.cs` | | `public sealed record GetTeamQuery(Guid TeamId) : IRequest<TeamDetailResult>;` |
| 25 | `api/E3A.Application/Teams/GetTeam/GetTeamQueryValidator.cs` | | `RuleFor(x => x.TeamId).ValidateRequired(ErrorCodes.TeamIdRequired);` |
| 26 | `api/E3A.Application/Teams/GetTeam/GetTeamQueryHandler.cs` | `public sealed class GetTeamQueryHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService) : IRequestHandler<GetTeamQuery, TeamDetailResult>` | Mirrors `GetEngineerQueryHandler`: load with `include: query => query.Include(x => x.Members)`, `asNoTracking: true`; null gives `NotFoundCoreException(TeamNotFound)`; when `Status == TeamStatus.Published` return immediately; otherwise auth guard then owner guard (`TeamNotOwned`); return `TeamResultGenerator.GenerateDetail(team)`. |
| 27 | `api/E3A.Application/Teams/ListMyTeams/ListMyTeamsQuery.cs` | | `public sealed record ListMyTeamsQuery : IRequest<List<TeamResult>>;` |
| 28 | `api/E3A.Application/Teams/ListMyTeams/ListMyTeamsQueryHandler.cs` | `public sealed class ListMyTeamsQueryHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService) : IRequestHandler<ListMyTeamsQuery, List<TeamResult>>` | Mirrors `ListMyEngineersQueryHandler`: auth guard; `FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, include: query => query.Include(x => x.Members), asNoTracking: true)`; `OrderByDescending(x => x.CreationDate).Select(TeamResultGenerator.Generate).ToList()`. No validator (mirrors `ListMyEngineers`). |
| 29 | `api/E3A.Application/Teams/CheckTeamSlugAvailability/CheckTeamSlugAvailabilityQuery.cs` | | `public sealed record CheckTeamSlugAvailabilityQuery(string Slug) : IRequest<SlugAvailabilityResult>;` |
| 30 | `api/E3A.Application/Teams/CheckTeamSlugAvailability/CheckTeamSlugAvailabilityQueryValidator.cs` | | The five slug rules from `CreateTeamValidator`, exactly as `CheckSlugAvailabilityQueryValidator` mirrors `CreateEngineerValidator`. |
| 31 | `api/E3A.Application/Teams/CheckTeamSlugAvailability/CheckTeamSlugAvailabilityQueryHandler.cs` | `public sealed class CheckTeamSlugAvailabilityQueryHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<TeamsOptions> teamsOptions) : IRequestHandler<CheckTeamSlugAvailabilityQuery, SlugAvailabilityResult>` | Mirrors `CheckSlugAvailabilityQueryHandler` against `teamRepository`. |
| 32 | `api/E3A.Application/Teams/SetTeamMembers/SetTeamMembersCommand.cs` | | `public sealed record SetTeamMembersCommand(Guid TeamId, List<TeamMemberSelection> Members) : IRequest<TeamDetailResult>;` and `public sealed record TeamMemberSelection(Guid EngineerId, Guid? PinnedVersionId);` in the same file. |
| 33 | `api/E3A.Application/Teams/SetTeamMembers/SetTeamMembersValidator.cs` | `public sealed class SetTeamMembersValidator : AbstractValidator<SetTeamMembersCommand>` | Constructor takes `IOptions<TeamsOptions> teamsOptions`. `RuleFor(x => x.TeamId).ValidateRequired(TeamIdRequired);` · `RuleFor(x => x.Members).ValidateListMaxItems(options.MaxMembersPerTeam, TeamMemberLimitReached);` · `RuleFor(x => x.Members).Must(members => members.Select(x => x.EngineerId).Distinct().Count() == members.Count).WithMessage("{PropertyName} must not repeat an engineer.").WithErrorCode(TeamMemberDuplicate);` · `RuleForEach(x => x.Members).ChildRules(member => member.RuleFor(x => x.EngineerId).ValidateRequired(TeamMemberEngineerIdRequired));`. An empty list is valid. |
| 34 | `api/E3A.Application/Teams/SetTeamMembers/SetTeamMembersHandler.cs` | `public sealed class SetTeamMembersHandler(ITeamRepository teamRepository, IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService) : IRequestHandler<SetTeamMembersCommand, TeamDetailResult>` | 1. auth guard. 2. `team = await teamRepository.GetByIdAsync(request.TeamId, cancellationToken, include: query => query.Include(x => x.Members))`; null gives `NotFoundCoreException(TeamNotFound)`. 3. owner mismatch gives `ForbiddenCoreException(TeamNotOwned)`. 4. when `request.Members.Count == 0`: `team.ReplaceMembers([], userId.Value)`, `Update`, one `SaveChangesAsync`, return detail. 5. `engineerIds = request.Members.Select(x => x.EngineerId).ToList();` `engineers = await engineerRepository.FindAsync(x => engineerIds.Contains(x.Id), cancellationToken, asNoTracking: true)`. 6. `versionIds = TeamMemberPinResolver.ResolveVersionIds(request.Members, engineers, team.Members)`. 7. `versions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id), cancellationToken, asNoTracking: true)`. 8. `pins = TeamMemberPinResolver.ResolvePins(request.Members, engineers, versions, team.Members)`. 9. `team.ReplaceMembers(pins, userId.Value)`. 10. `teamRepository.Update(team)`, one `SaveChangesAsync`. 11. return `TeamResultGenerator.GenerateDetail(team)`. |
| 35 | `api/E3A.Application/Teams/PublishTeam/PublishTeamCommand.cs` | | `public sealed record PublishTeamCommand(Guid TeamId, VersionIncrement Increment) : IRequest<PublishStatusResult>;` |
| 36 | `api/E3A.Application/Teams/PublishTeam/PublishTeamValidator.cs` | | `RuleFor(x => x.TeamId).ValidateRequired(TeamIdRequired);` · `RuleFor(x => x.Increment).IsInEnum().WithErrorCode(PublishIncrementInvalid);` |
| 37 | `api/E3A.Application/Teams/PublishTeam/PublishTeamHandler.cs` | `public sealed class PublishTeamHandler(ITeamRepository teamRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, IOptions<PublishingOptions> publishingOptions) : IRequestHandler<PublishTeamCommand, PublishStatusResult>` | Mirrors `PublishEngineerHandler` with `ItemType.Team`: 1. auth. 2. `GetByIdAsync(teamId, ct, include Members)` null gives `NotFoundCoreException(TeamNotFound)`. 3. owner mismatch gives `ForbiddenCoreException(TeamNotOwned)`. 4. `team.Members.Count == 0` gives `BadRequestCoreException(TeamEmpty)`. 5. in-progress `Queued`/`Building` version for `(ItemType.Team, team.Id)` gives `ConflictCoreException(PublishAlreadyInProgress)`. 6. version count at or above `MaxVersionsPerItem` gives `BusinessRuleViolationCoreException(PublishVersionLimitReached, context: { ["limit"] = ... })`. 7. `latest` by `OrderByDescending(VersionNumber)`; `semanticVersion = SemanticVersionCalculator.Next(latest?.SemanticVersion, request.Increment)`. 8. `frozenRosterJson = JsonSerializer.Serialize(TeamRosterGenerator.Generate(team))`. 9. `ItemVersion.Create(ItemType.Team, team.Id, (latest?.VersionNumber ?? 0) + 1, semanticVersion, frozenRosterJson, userId.Value)`. 10. `AddAsync`, one `SaveChangesAsync`. 11. return `PublishStatusResultGenerator.Generate(version, options)`. |

### Application — Publishing shared

| # | Path | Type | Contract |
|---|------|------|----------|
| 38 | `api/E3A.Application/Publishing/Shared/PublicCatalogUrl.cs` | `public static class PublicCatalogUrl` | `// The public catalog page a plugin's author field points at; the segments match the SPA routes.` `private const string EngineerSegment = "e";` `private const string TeamSegment = "t";` · `public static string ForEngineer(string publicSiteUrl, string slug)` returns `$"{publicSiteUrl.TrimEnd('/')}/{EngineerSegment}/{slug}"` · `public static string ForTeam(string publicSiteUrl, string slug)` the same with `TeamSegment`. |
| 39 | `api/E3A.Application/Publishing/Shared/PublishBuild.cs` | `public sealed record PublishBuild(Engineer? Engineer, Team? Team, string PluginName, string AuthorName, List<PluginFile> Files, string? FailureReason);` | Exactly one of `Engineer` / `Team` is non-null on a successful build. `FailureReason` non-null means the item-specific stage failed and `Files` is `[]`. Two static factories on the record are **not** used; the builders construct it inline. |
| 40 | `api/E3A.Application/Publishing/Shared/EngineerPublishBuilder.cs` | `public static class EngineerPublishBuilder` | `public static async Task<PublishBuild> BuildAsync(IEngineerRepository engineerRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, AzureOptions azureOptions, PublishingOptions publishingOptions, ItemVersion version, CancellationToken cancellationToken)`. Steps, in order — each failure returns `new PublishBuild(null, null, string.Empty, string.Empty, [], ErrorCodes.X)`: (1) `engineer = GetByIdAsync(version.ItemId)`, null gives `EngineerNotFound`; (2) `snapshotAssets = await DraftSnapshotFreezer.FreezeAsync(storageBlobClient, azureOptions, engineer.OwnerUserId, engineer.Id, version.Id, cancellationToken)`, empty gives `EngineerSnapshotEmpty`; (3) `manifest = JsonSerializer.Deserialize<ImportManifestResult>(version.FrozenManifestJson)`, null gives `EngineerDraftNotUploaded`; (4) `user = await userRepository.GetByIdAsync(engineer.OwnerUserId, cancellationToken, asNoTracking: true)`, `authorName = string.IsNullOrWhiteSpace(user?.UserName) ? engineer.Slug : user.UserName`; (5) `files = PluginTreeAssembler.Assemble(snapshotAssets, manifest, engineer, version.SemanticVersion, authorName, publishingOptions)`; (6) `errors = PluginStructureValidator.Validate(files, manifest, publishingOptions)`, non-empty gives `string.Join(", ", errors)`; (7) return `new PublishBuild(engineer, null, PluginName.ForEngineer(engineer.Slug), authorName, files, null)`. Behaviour and failure codes are identical to today's handler. |
| 41 | `api/E3A.Application/Publishing/Shared/TeamSnapshotReader.cs` | `public static class TeamSnapshotReader` | `public static async Task<List<PluginFile>> ReadAsync(IStorageBlobClient storageBlobClient, AzureOptions azureOptions, Guid versionId, CancellationToken cancellationToken)`. Builds `prefix = PublishBlobPaths.SnapshotPrefix(versionId)`; `ListByPrefixAsync(..., azureOptions.SnapshotsBlobContainerName, prefix, ...)`; for each name `DownloadAsync`, skipping null content; produces `new PluginFile(blobName[prefix.Length..], content)`; returns `[.. files.OrderBy(x => x.Path, StringComparer.Ordinal)]`. Read-only: it never writes a blob. |
| 42 | `api/E3A.Application/Publishing/Shared/TeamMemberSnapshot.cs` | `public sealed record TeamMemberSnapshot(string MemberSlug, ImportManifestResult Manifest, List<PluginFile> SnapshotAssets);` | Input row for the pure assembler. |
| 43 | `api/E3A.Application/Publishing/Shared/TeamTreeAssembler.cs` | `public static class TeamTreeAssembler` | `// Claude Code addresses a skill by its folder name, so two members' identically named skills must be given distinct folders.` `private const string NamespaceSeparator = "--";` `private const string SkillsRoot = "skills/";` `private static readonly string[] PrefixableRoots = ["agents/", "commands/"];` · `public static List<PluginFile> Assemble(List<TeamMemberSnapshot> members, Team team, string semanticVersion, string authorName, PublishingOptions options)`. Algorithm: (a) for each member in the given order, keep only snapshot assets whose `Path` is in that member's allowed set (`Manifest.Imported.Select(TargetPath)` union `Manifest.Converted.Select(TargetPath)`, `OrdinalIgnoreCase`) **and** starts with `skills/`, `agents/` or `commands/` (`OrdinalIgnoreCase`); (b) map every `skills/{rest}` path to `skills/{memberSlug}--{rest}`; (c) leave `agents/` and `commands/` paths as the candidate path; (d) group the candidate `agents/`/`commands/` paths across members with `OrdinalIgnoreCase`, and for every group produced by more than one distinct member slug, rewrite each of those members' entries as `{root}{memberSlug}--{fileName}` where `fileName` is the path after the root; (e) add `PluginJsonGenerator.Generate(team, semanticVersion, authorName, options)`; (f) return `[.. files.OrderBy(x => x.Path, StringComparer.Ordinal)]`. Pure; no I/O; no `PluginFile.Content` mutation. |
| 44 | `api/E3A.Application/Publishing/Shared/TeamPublishBuilder.cs` | `public static class TeamPublishBuilder` | `public static async Task<PublishBuild> BuildAsync(ITeamRepository teamRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, IStorageBlobClient storageBlobClient, AzureOptions azureOptions, PublishingOptions publishingOptions, ItemVersion version, CancellationToken cancellationToken)`. Steps, each failure returning a failed `PublishBuild`: (1) `team = GetByIdAsync(version.ItemId)`, null gives `TeamNotFound`; (2) `roster = JsonSerializer.Deserialize<TeamRosterResult>(version.FrozenManifestJson)`, null gives `TeamRosterInvalid`; (3) `roster.Members.Count == 0` gives `TeamEmpty`; (4) `orderedMembers = roster.Members.OrderBy(x => x.SortOrder).ThenBy(x => x.EngineerId).ToList()`; (5) `versionIds = orderedMembers.Select(x => x.PinnedVersionId).ToList()`, `memberVersions = await itemVersionRepository.FindAsync(x => versionIds.Contains(x.Id), cancellationToken, asNoTracking: true)`; (6) for each ordered member: find its version — missing, or not `ItemType.Engineer`, or `ItemId != member.EngineerId`, or not `Published` gives `TeamMemberVersionNotPublished`; `JsonSerializer.Deserialize<ImportManifestResult>(memberVersion.FrozenManifestJson)` null gives `TeamMemberManifestInvalid`; `assets = await TeamSnapshotReader.ReadAsync(storageBlobClient, azureOptions, member.PinnedVersionId, cancellationToken)`, empty gives `TeamMemberSnapshotEmpty`; append `new TeamMemberSnapshot(member.EngineerSlug, manifest, assets)`; (7) `user = await userRepository.GetByIdAsync(team.OwnerUserId, cancellationToken, asNoTracking: true)`, `authorName = string.IsNullOrWhiteSpace(user?.UserName) ? team.Slug : user.UserName`; (8) `files = TeamTreeAssembler.Assemble(snapshots, team, version.SemanticVersion, authorName, publishingOptions)`; (9) `errors = PluginStructureValidator.Validate(files, publishingOptions)`, non-empty gives `string.Join(", ", errors)`; (10) return `new PublishBuild(null, team, PluginName.ForTeam(team.Slug), authorName, files, null)`. Never loads a member `Engineer`. Never writes a blob. |
| 45 | `api/E3A.Application/Publishing/Shared/PublishedEngineerCollector.cs` | `public static class PublishedEngineerCollector` | `public static async Task<List<MarketplacePlugin>> CollectAsync(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, IUserRepository userRepository, PublishingOptions options, CancellationToken cancellationToken)` — today's `RegenerateMarketplaceHandler` body verbatim, including the `MarketplaceEngineerLimitExceeded` page guard, the `PublishedEngineerVersion` private record and `ResolveAuthorName`, returning the plugin list instead of writing the blob. |
| 46 | `api/E3A.Application/Publishing/Shared/PublishedTeamCollector.cs` | `public static class PublishedTeamCollector` | Same shape over `ITeamRepository`, filtering `x.Status == TeamStatus.Published && x.LatestVersionId != null`, ordering pages by `x.Slug`, throwing `InternalServerErrorCoreException(ErrorCodes.MarketplaceTeamLimitExceeded)` past `MarketplaceMaxPages`, resolving `authorName` from the owner's `UserName` with the team slug as fallback, and calling `MarketplaceDocumentGenerator.GeneratePlugin(team, version, authorName, options)`. |

### Infrastructure

| # | Path | Type | Contract |
|---|------|------|----------|
| 47 | `api/E3A.Infrastructure/Teams/TeamRepository.cs` | `public class TeamRepository(AppDbContext context) : Repository<Team>(context), ITeamRepository` | `IsSlugExistsAsync` body identical to `EngineerRepository`'s: `await CountAsync(cancellationToken, x => x.Slug == slug).ConfigureAwait(false) > 0`. |
| 48 | `api/E3A.Infrastructure/Data/Migrations/<timestamp>_teams005.cs` + `.Designer.cs` | EF migration | Generated by `dotnet ef migrations add teams005 --project api/E3A.Infrastructure --startup-project api/E3A.Api`. Must create `Teams` and `TeamMembers` with the columns and indexes below and nothing else. |

`ConfigureTeams(ModelBuilder modelBuilder)` in `AppDbContext` (named private method, reading `teamsOptions.Value`):

```
Team:   Slug required, HasMaxLength(SlugMaxLength), HasIndex(Slug).IsUnique().HasFilter("[IsDeleted] = 0")
        HasIndex(OwnerUserId)
        DisplayName required HasMaxLength(DisplayNameMaxLength)
        Description HasMaxLength(DescriptionMaxLength)
        Status HasConversion<string>().HasMaxLength(EnumColumnMaxLength)
        Tags JSON value conversion identical to Engineer.Tags, HasMaxLength(TagsColumnMaxLength)
        HasMany(x => x.Members).WithOne().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade)
TeamMember: EngineerSlug required HasMaxLength(teamsSchema.SlugMaxLength)
        PinnedSemanticVersion required HasMaxLength(publishingSchema.SemanticVersionMaxLength)
        HasIndex(new { TeamId, EngineerId }).IsUnique().HasFilter("[IsDeleted] = 0")
        HasIndex(x => x.TeamId)
```

### API

| # | Path | Type | Contract |
|---|------|------|----------|
| 49 | `api/E3A.Api/Controllers/Teams/Requests.cs` | `namespace E3A.Api.Controllers.Teams;` | `public sealed record CreateTeamRequest(string Slug, string DisplayName, string? Description, List<string>? Tags);` · `public sealed record UpdateTeamRequest(string? Slug, string DisplayName, string? Description, List<string>? Tags);` · `public sealed record SetTeamMembersRequest(List<TeamMemberRequest>? Members);` · `public sealed record TeamMemberRequest(Guid EngineerId, Guid? PinnedVersionId);` · `public sealed record PublishTeamRequest([property: JsonRequired] VersionIncrement Increment);` |
| 50 | `api/E3A.Api/Controllers/Teams/TeamsController.cs` | `[ApiController] [Route("api/teams")] [Authorize] public class TeamsController(IMediator mediator) : ControllerBase` | Actions listed in API surface. Thin: map, send, return. `CancellationToken cancellationToken` on every action. |

## Error codes

`DefaultCodes` does not exist in this repo and no policy constants are used anywhere — controllers
carry a bare `[Authorize]` with `[AllowAnonymous]` on public reads. `TeamsController` mirrors that and
introduces no policy.

New constants in `api/E3A.Application/Exceptions/ErrorCodes.cs`. The first 26 go in a new
`// Teams` group placed after the `// Engineers` group; the last two go at the end of the existing
`// Publishing` group.

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `TeamNotFound` | `TEAM_NOT_FOUND` | Update/Delete/Get/SetMembers/PublishTeam handlers, `GetPublishStatusQueryHandler`; `TeamPublishBuilder` (as a failure reason) | `NotFoundCoreException` | 404 |
| `TeamNotOwned` | `TEAM_NOT_OWNED` | Update/Delete/Get/SetMembers/PublishTeam handlers, `GetPublishStatusQueryHandler` | `ForbiddenCoreException` | 403 |
| `TeamLimitReached` | `TEAM_LIMIT_REACHED` | `CreateTeamHandler` | `BusinessRuleViolationCoreException` (context `limit`) | 400 |
| `TeamIdRequired` | `TEAM_ID_REQUIRED` | Update/Delete/Get/SetMembers/PublishTeam validators | validation pipeline | 422 |
| `TeamDisplayNameRequired` | `TEAM_DISPLAY_NAME_REQUIRED` | Create/Update validators | validation pipeline | 422 |
| `TeamDisplayNameTooLong` | `TEAM_DISPLAY_NAME_TOO_LONG` | Create/Update validators | validation pipeline | 422 |
| `TeamDisplayNameInvalid` | `TEAM_DISPLAY_NAME_INVALID` | Create/Update validators | validation pipeline | 422 |
| `TeamDescriptionTooLong` | `TEAM_DESCRIPTION_TOO_LONG` | Create/Update validators | validation pipeline | 422 |
| `TeamTooManyTags` | `TEAM_TOO_MANY_TAGS` | Create/Update validators | validation pipeline | 422 |
| `TeamTagRequired` | `TEAM_TAG_REQUIRED` | Create/Update validators | validation pipeline | 422 |
| `TeamTagTooLong` | `TEAM_TAG_TOO_LONG` | Create/Update validators | validation pipeline | 422 |
| `TeamSlugRequired` | `TEAM_SLUG_REQUIRED` | Create/Update/SlugAvailability validators | validation pipeline | 422 |
| `TeamSlugTooShort` | `TEAM_SLUG_TOO_SHORT` | Create/Update/SlugAvailability validators | validation pipeline | 422 |
| `TeamSlugTooLong` | `TEAM_SLUG_TOO_LONG` | Create/Update/SlugAvailability validators | validation pipeline | 422 |
| `TeamSlugInvalid` | `TEAM_SLUG_INVALID` | Create/Update/SlugAvailability validators | validation pipeline | 422 |
| `TeamSlugReserved` | `TEAM_SLUG_RESERVED` | Create/Update/SlugAvailability validators | validation pipeline | 422 |
| `TeamSlugFrozen` | `TEAM_SLUG_FROZEN` | `UpdateTeamHandler` | `BusinessRuleViolationCoreException` | 400 |
| `TeamEmpty` | `TEAM_EMPTY` | `PublishTeamHandler`; `TeamPublishBuilder` (failure reason) | `BadRequestCoreException` | 400 |
| `TeamMemberLimitReached` | `TEAM_MEMBER_LIMIT_REACHED` | `SetTeamMembersValidator` | validation pipeline | 422 |
| `TeamMemberDuplicate` | `TEAM_MEMBER_DUPLICATE` | `SetTeamMembersValidator` | validation pipeline | 422 |
| `TeamMemberEngineerIdRequired` | `TEAM_MEMBER_ENGINEER_ID_REQUIRED` | `SetTeamMembersValidator` | validation pipeline | 422 |
| `TeamMemberNotPublished` | `TEAM_MEMBER_NOT_PUBLISHED` | `TeamMemberPinResolver.ResolveVersionIds` | `BusinessRuleViolationCoreException` (context `engineerId`) | 400 |
| `TeamMemberVersionNotPublished` | `TEAM_MEMBER_VERSION_NOT_PUBLISHED` | `TeamMemberPinResolver.ResolvePins`; `TeamPublishBuilder` (failure reason) | `BusinessRuleViolationCoreException` (context `engineerId`) | 400 |
| `TeamMemberSnapshotEmpty` | `TEAM_MEMBER_SNAPSHOT_EMPTY` | `TeamPublishBuilder` (failure reason only) | none — written to `ItemVersion.FailureReason` | n/a |
| `TeamMemberManifestInvalid` | `TEAM_MEMBER_MANIFEST_INVALID` | `TeamPublishBuilder` (failure reason only) | none | n/a |
| `TeamRosterInvalid` | `TEAM_ROSTER_INVALID` | `TeamPublishBuilder` (failure reason only) | none | n/a |
| `PluginDuplicatePath` | `PLUGIN_DUPLICATE_PATH` | `PluginStructureValidator` | none — becomes an `ItemVersion.FailureReason` | n/a |
| `MarketplaceTeamLimitExceeded` | `MARKETPLACE_TEAM_LIMIT_EXCEEDED` | `PublishedTeamCollector` | `InternalServerErrorCoreException` | 500 |

Resource strings — add all 28 keys to **both** `Messages.en.resx` and `Messages.ar.resx`, keeping
`{limit}` and `{engineerId}` placeholders intact in both languages, Arabic without tashkeel.

| Key | English | Arabic |
|-----|---------|--------|
| `TEAM_NOT_FOUND` | We couldn't find that team. | لم نتمكن من العثور على هذا الفريق. |
| `TEAM_NOT_OWNED` | This team belongs to another creator. | هذا الفريق يخص منشئا اخر. |
| `TEAM_LIMIT_REACHED` | You have reached the limit of {limit} teams. | لقد وصلت الى الحد الاقصى وهو {limit} فريق. |
| `TEAM_ID_REQUIRED` | The team is required. | الفريق مطلوب. |
| `TEAM_DISPLAY_NAME_REQUIRED` | The team name is required. | اسم الفريق مطلوب. |
| `TEAM_DISPLAY_NAME_TOO_LONG` | The team name is too long. | اسم الفريق طويل جدا. |
| `TEAM_DISPLAY_NAME_INVALID` | The team name must contain at least one English letter or digit. | يجب ان يحتوي اسم الفريق على حرف انجليزي او رقم على الاقل. |
| `TEAM_DESCRIPTION_TOO_LONG` | The team description is too long. | وصف الفريق طويل جدا. |
| `TEAM_TOO_MANY_TAGS` | The team has too many tags. | عدد وسوم الفريق اكثر من المسموح. |
| `TEAM_TAG_REQUIRED` | A tag cannot be empty. | لا يمكن ان يكون الوسم فارغا. |
| `TEAM_TAG_TOO_LONG` | A tag is too long. | الوسم طويل جدا. |
| `TEAM_SLUG_REQUIRED` | The team slug is required. | الاسم المختصر للفريق مطلوب. |
| `TEAM_SLUG_TOO_SHORT` | The team slug is too short. | الاسم المختصر للفريق قصير جدا. |
| `TEAM_SLUG_TOO_LONG` | The team slug is too long. | الاسم المختصر للفريق طويل جدا. |
| `TEAM_SLUG_INVALID` | The team slug must be lowercase letters, digits and single hyphens. | يجب ان يتكون الاسم المختصر للفريق من حروف صغيرة وارقام وشرطات مفردة. |
| `TEAM_SLUG_RESERVED` | That team slug is reserved. | هذا الاسم المختصر محجوز. |
| `TEAM_SLUG_FROZEN` | A slug cannot be changed after the team has been published. | لا يمكن تغيير الاسم المختصر بعد نشر الفريق. |
| `TEAM_EMPTY` | A team needs at least one member before it can be published. | يحتاج الفريق الى عضو واحد على الاقل قبل النشر. |
| `TEAM_MEMBER_LIMIT_REACHED` | A team cannot have more members than the allowed limit. | لا يمكن ان يتجاوز عدد اعضاء الفريق الحد المسموح. |
| `TEAM_MEMBER_DUPLICATE` | The same engineer cannot be added to a team twice. | لا يمكن اضافة نفس المهندس الى الفريق مرتين. |
| `TEAM_MEMBER_ENGINEER_ID_REQUIRED` | Each team member needs an engineer. | كل عضو في الفريق يحتاج الى مهندس. |
| `TEAM_MEMBER_NOT_PUBLISHED` | Engineer {engineerId} has no published version to pin. | المهندس {engineerId} ليس له اصدار منشور لتثبيته. |
| `TEAM_MEMBER_VERSION_NOT_PUBLISHED` | The pinned version for engineer {engineerId} is not a published version of that engineer. | الاصدار المثبت للمهندس {engineerId} ليس اصدارا منشورا لهذا المهندس. |
| `TEAM_MEMBER_SNAPSHOT_EMPTY` | A pinned member version has no stored content. | احد اصدارات الاعضاء المثبتة لا يحتوي على اي محتوى مخزن. |
| `TEAM_MEMBER_MANIFEST_INVALID` | A pinned member version has an unreadable import manifest. | احد اصدارات الاعضاء المثبتة يحتوي على بيان استيراد غير قابل للقراءة. |
| `TEAM_ROSTER_INVALID` | The frozen team roster could not be read. | تعذرت قراءة قائمة اعضاء الفريق المجمدة. |
| `PLUGIN_DUPLICATE_PATH` | The plugin contains two files with the same path. | تحتوي الاضافة على ملفين بنفس المسار. |
| `MARKETPLACE_TEAM_LIMIT_EXCEEDED` | The marketplace has more teams than the generator can process. | يحتوي السوق على فرق اكثر مما يستطيع المولد معالجته. |

## Domain behaviour

`api/E3A.Domain/Teams/Team.cs` — exact expected members:

```csharp
using Core.DDD.Entities;

namespace E3A.Domain.Teams;

public class Team : AuditEntity
{
    public Guid OwnerUserId { get; private set; }
    public string Slug { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public TeamStatus Status { get; private set; }
    public Guid? LatestVersionId { get; private set; }
    public List<TeamMember> Members { get; private set; } = [];
    public bool IsSlugMutable => LatestVersionId == null;

    private Team(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Team Create(Guid ownerUserId, string slug, string displayName, string? description, List<string> tags)
    {
        return new Team(Guid.NewGuid(), ownerUserId)
        {
            OwnerUserId = ownerUserId,
            Slug = slug,
            DisplayName = displayName,
            Description = description,
            Tags = [.. tags],
            Status = TeamStatus.Draft,
            LatestVersionId = null,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateMetadata(string displayName, string? description, List<string> tags)
    {
        DisplayName = displayName;
        Description = description;
        Tags = [.. tags];
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void ChangeSlug(string slug)
    {
        Slug = slug;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void ReplaceMembers(List<TeamMemberPin> pins, Guid updatedBy)
    {
        Members.Clear();

        for (var index = 0; index < pins.Count; index++)
        {
            Members.Add(TeamMember.Create(Id, pins[index], index, updatedBy));
        }

        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkPublished(Guid latestVersionId)
    {
        Status = TeamStatus.Published;
        LatestVersionId = latestVersionId;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Delete()
    {
        Status = TeamStatus.Deleted;
        SoftDelete();
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
```

Notes the implementer must honour:

- Every mutator sets `UpdationDate = DateTimeOffset.UtcNow`. `Create` sets both stamps, mirroring `Engineer.Create`.
- **No `BusinessRuleViolationException` guard belongs on `Team`.** Every team rule (ownership, limits,
  frozen slug, empty publish, member publishedness) needs data the entity does not hold, so it is
  enforced in the handler or the pin resolver with a `Core.Errors` exception — exactly as
  `Engineer` does today (`Engineer` has zero domain throws). Do not invent one to satisfy a checklist.
- `ReplaceMembers` is the **only** way membership changes. There is no `AddMember` or `RemoveMember`.
- `Members.Clear()` plus re-add relies on the cascade relationship so EF deletes orphan rows.
- `IsSlugMutable` is the same rule as `Engineer.IsSlugMutable`.

## Worker control flow

`ProcessPublishJobHandler.Handle` after the change, in order:

```
1  version = itemVersionRepository.GetByIdAsync(request.VersionId)
2  version == null                      -> throw NotFoundCoreException(PublishVersionNotFound)     [0 saves]
3  status not Queued|Building           -> return                                                   [0 saves]
4  status == Queued                     -> version.MarkBuilding(); Update; SaveChangesAsync()       [save #1]
5  build = version.ItemType switch {
        ItemType.Team => await TeamPublishBuilder.BuildAsync(teamRepository, itemVersionRepository, userRepository, storageBlobClient, azure, publishing, version, cancellationToken),
        _             => await EngineerPublishBuilder.BuildAsync(engineerRepository, userRepository, storageBlobClient, azure, publishing, version, cancellationToken),
     }
6  build.FailureReason != null          -> await FailAsync(version, build.FailureReason)            [save #2] ; return
7  zipped = DeterministicZipper.Create(build.Files)
8  zipBlobPath = PublishBlobPaths.Zip(build.PluginName, version.SemanticVersion)
9  existing = storageBlobClient.ListByPrefixAsync(public container, zipBlobPath)
10 not present                          -> UploadAsync(zip, public, overwrite: false, ZipCacheControl)
11 version.MarkPublished(zipBlobPath, zipped.Sha256, zipped.SizeBytes)
12 MarkItemPublished(build, version.Id)     // team.MarkPublished + teamRepository.Update, else engineer.MarkPublished + engineerRepository.Update
13 pinnedJson = MarketplaceDocumentGenerator.Generate([GeneratePinnedPlugin(build, version, publishing)], publishing)
14 UploadAsync(pinnedJson, public, PublishBlobPaths.PinnedMarketplace(build.PluginName, version.SemanticVersion), overwrite: true, MarketplaceCacheControl)
15 itemVersionRepository.Update(version); await itemVersionRepository.SaveChangesAsync()            [save #2]
```

Two private helpers keep it under 100 lines: `MarkItemPublished(PublishBuild build, Guid versionId)`
and `static MarketplacePlugin GeneratePinnedPlugin(PublishBuild build, ItemVersion version, PublishingOptions options)`,
each a two-branch `if`/`else` on `build.Team != null`. `FailAsync` is unchanged.

**Save-count discipline — at most two `SaveChangesAsync` on every path, both types:**

| Path | Saves |
|------|-------|
| version not found (throws) | 0 |
| terminal version (returns) | 0 |
| `Queued`, build fails | 2 — MarkBuilding, MarkFailed |
| `Building` (queue retry), build fails | 1 — MarkFailed |
| `Queued`, success | 2 — MarkBuilding, then MarkPublished + item in one save |
| `Building` (queue retry), success | 1 |

The item lookup now happens **after** the Building transition on both paths (it used to happen before
it on the engineer path). This is the only behavioural change to the engineer path and it costs one
extra save on the "engineer row vanished" path; it is what makes both branches symmetric and keeps
the save-count table above true for teams. It changes exactly one existing assertion — see
`ProcessPublishJobHandlerFailureTests` in "Existing code touched".

**Nothing reaches the public container on a failure path.** Every failure returns at step 6, which is
before `DeterministicZipper.Create` and before any `UploadAsync` targeting
`azureOptions.PublicBlobContainerName`. The only blob write that can happen before the gate is
`DraftSnapshotFreezer` writing the **private** `snapshots` container on the engineer path; the team
path writes nothing at all before the gate because `TeamSnapshotReader` is read-only. This is asserted
by test for both paths.

## API surface

All actions are on `TeamsController` (`[Authorize]` at class level, no policy constants — the repo has
none), each taking `CancellationToken cancellationToken` and passing it to `mediator.Send`.

| Method | Route | Authorization | Request record | Command / query | Response |
|--------|-------|---------------|----------------|-----------------|----------|
| GET | `api/teams/mine` | `[Authorize]` (class) | — | `ListMyTeamsQuery()` | `200` `List<TeamResult>` |
| GET | `api/teams/slug-availability?slug=` | `[Authorize]` (class) | `[FromQuery] string slug` | `CheckTeamSlugAvailabilityQuery(slug)` | `200` `SlugAvailabilityResult` |
| GET | `api/teams/{teamId:guid}` | `[AllowAnonymous]` | — | `GetTeamQuery(teamId)` | `200` `TeamDetailResult` |
| POST | `api/teams` | `[Authorize]` (class) | `CreateTeamRequest` | `CreateTeamCommand(request.Slug, request.DisplayName, request.Description, request.Tags ?? [])` | `201 CreatedAtAction(nameof(GetTeam), new { teamId = result.Id }, result)` |
| PUT | `api/teams/{teamId:guid}` | `[Authorize]` (class) | `UpdateTeamRequest` | `UpdateTeamCommand(teamId, request.Slug, request.DisplayName, request.Description, request.Tags ?? [])` | `200` `TeamResult` |
| PUT | `api/teams/{teamId:guid}/members` | `[Authorize]` (class) | `SetTeamMembersRequest` | `SetTeamMembersCommand(teamId, [.. (request.Members ?? []).Select(x => new TeamMemberSelection(x.EngineerId, x.PinnedVersionId))])` | `200` `TeamDetailResult` |
| POST | `api/teams/{teamId:guid}/publish` | `[Authorize]` (class) | `PublishTeamRequest` | `PublishTeamCommand(teamId, request.Increment)` | `202 Accepted(result)` `PublishStatusResult` |
| DELETE | `api/teams/{teamId:guid}` | `[Authorize]` (class) | — | `DeleteTeamCommand(teamId)` | `204 NoContent()` |

Postman `Teams` folder mirrors these eight in the same order, using `{{baseUrl}}` and `{{teamId}}`,
with `Content-Type: application/json` on the four bodied requests. Sample bodies:
create `{"slug":"dotnet-product-squad","displayName":"DotNet Product Squad","description":"Backend, frontend, infra and review in one install.","tags":["dotnet","team"]}`;
set-members `{"members":[{"engineerId":"{{engineerId}}","pinnedVersionId":null}]}`;
publish `{"increment":"Patch"}`.

## Test plan

New and changed test files only. Every file follows `conventions/dotnet-testing.md` section 5:
`sealed class`, `_sut`, `Method_Should[Outcome]_When[Condition]`, factories not `new`,
`Received(1)` on success and `DidNotReceive()` on every throwing path, error-code constants not
messages, no wall-clock equality, no file over ~100 lines.

### Shared factories

| # | File | Contents |
|---|------|----------|
| T1 | `api/E3A.Tests/Teams/Shared/TeamFactory.cs` | `DefaultSlug = "dotnet-product-squad"`, `DefaultDisplayName = "DotNet Product Squad"`. `Draft(Guid ownerUserId, string slug = DefaultSlug, ...)`; `WithMembers(Guid ownerUserId, params TeamMemberPin[] pins)` (calls `Draft` then `ReplaceMembers`); `Published(Guid ownerUserId, string slug = DefaultSlug)` (`Draft` then `MarkPublished(Guid.NewGuid())`); `Pin(string engineerSlug = "dive-backend-engineer", Guid? engineerId = null, Guid? versionId = null, string semanticVersion = "1.0.0")` returning `TeamMemberPin`; `CreateTeamsOptions(int maxTeamsPerCreator = 10, int maxMembersPerTeam = 10)` returning a fully populated `TeamsOptions` mirroring the committed defaults. |
| T2 | `api/E3A.Tests/Publishing/Shared/TeamSnapshotFactory.cs` | `Roster(params TeamRosterMemberResult[] members)`; `RosterJson(...)` (`JsonSerializer.Serialize`); `MemberSnapshot(string memberSlug, params string[] paths)` building a `TeamMemberSnapshot` whose `Manifest` is `PluginFileFactory.Manifest(paths)` and whose assets are `PluginFileFactory.Files(paths)`. |

### Domain

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Teams/TeamTests` | `Create_ShouldReturnDraftTeam_WhenCalled` | `Status` is `Draft`, `LatestVersionId` null, `Members` empty, `Slug`/`DisplayName`/`Description`/`Tags` set, `Id` not `Guid.Empty`, `CreationDate` on or after a captured `before` |
| 2 | `Teams/TeamTests` | `Create_ShouldCopyTags_WhenSourceListIsMutatedAfterwards` | mutating the source list does not change `team.Tags` |
| 3 | `Teams/TeamTests` | `UpdateMetadata_ShouldReplaceFieldsAndAdvanceUpdationDate_WhenCalled` | new name/description/tags; `UpdationDate` on or after `before` |
| 4 | `Teams/TeamTests` | `ChangeSlug_ShouldReplaceSlugAndAdvanceUpdationDate_WhenCalled` | slug replaced; `UpdationDate` advanced |
| 5 | `Teams/TeamTests` | `MarkPublished_ShouldSetPublishedStatusAndLatestVersion_WhenCalled` | `Status` `Published`, `LatestVersionId` equals the captured id, `UpdationDate` advanced |
| 6 | `Teams/TeamTests` | `Delete_ShouldSetDeletedStatusAndSoftDelete_WhenCalled` | `Status` `Deleted`, `IsDeleted` true, `UpdationDate` advanced |
| 7 | `Teams/TeamSlugTests` | `IsSlugMutable_ShouldBeTrue_WhenTeamHasNeverPublished` | true on a draft |
| 8 | `Teams/TeamSlugTests` | `IsSlugMutable_ShouldBeFalse_WhenTeamHasPublished` | false after `MarkPublished` |
| 9 | `Teams/TeamMembershipTests` | `ReplaceMembers_ShouldAssignSequentialSortOrder_WhenPinsAreGiven` | three pins produce `SortOrder` 0,1,2 in submitted order |
| 10 | `Teams/TeamMembershipTests` | `ReplaceMembers_ShouldDropPreviousMembers_WhenCalledAgain` | `Members` contains only the second call's engineer ids; count matches |
| 11 | `Teams/TeamMembershipTests` | `ReplaceMembers_ShouldResequenceFromZero_WhenAMemberIsRemoved` | after replacing 3 with 2, orders are 0,1 with no gap |
| 12 | `Teams/TeamMembershipTests` | `ReplaceMembers_ShouldEmptyMembers_WhenPinsAreEmpty` | `Members` empty; `UpdationDate` advanced |
| 13 | `Teams/TeamMembershipTests` | `ReplaceMembers_ShouldCopyPinFields_WhenCalled` | each `TeamMember` carries the pin's `EngineerId`, `EngineerSlug`, `PinnedVersionId`, `PinnedSemanticVersion`, and `TeamId` equals the team id |
| 14 | `Teams/TeamMemberTests` | `Create_ShouldCopyPinAndSortOrder_WhenCalled` | all six fields plus `CreatedBy` and stamps |

### Application — CRUD handlers

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 15 | `Teams/CreateTeam/CreateTeamHandlerTests` | `Handle_ShouldCreateTeam_WhenRequestIsValid` | returns `TeamResult` with the normalized slug, `Status` `nameof(TeamStatus.Draft)`, `MemberCount` 0; `AddAsync` and `SaveChangesAsync` each `Received(1)` |
| 16 | same | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` with `UserNotAuthenticated`; `SaveChangesAsync` `DidNotReceive` |
| 17 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenTeamLimitIsReached` | `CountAsync` returns `MaxTeamsPerCreator`; `BusinessRuleViolationCoreException` with `TeamLimitReached`; no save |
| 18 | same | `Handle_ShouldUseSuffixedSlug_WhenSlugIsTaken` | `IsSlugExistsAsync` true then false; result slug differs from the requested slug and is non-empty |
| 19 | `Teams/CreateTeam/CreateTeamValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 20 | same | `Validate_ShouldFail_WhenDisplayNameIsEmpty` | `TeamDisplayNameRequired` |
| 21 | same | `Validate_ShouldFail_WhenDisplayNameIsTooLong` | `TeamDisplayNameTooLong` |
| 22 | same | `Validate_ShouldFail_WhenDisplayNameHasNoAsciiLetterOrDigit` | `TeamDisplayNameInvalid` |
| 23 | same | `Validate_ShouldFail_WhenDescriptionIsTooLong` | `TeamDescriptionTooLong` |
| 24 | same | `Validate_ShouldFail_WhenTagsExceedTheLimit` | `TeamTooManyTags` |
| 25 | same | `Validate_ShouldFail_WhenATagIsEmpty` | `TeamTagRequired` |
| 26 | same | `Validate_ShouldFail_WhenATagIsTooLong` | `TeamTagTooLong` |
| 27 | `Teams/CreateTeam/CreateTeamSlugValidatorTests` | `Validate_ShouldFail_WhenSlugIsEmpty` | `TeamSlugRequired` |
| 28 | same | `Validate_ShouldFail_WhenSlugIsTooShort` | `TeamSlugTooShort` |
| 29 | same | `Validate_ShouldFail_WhenSlugIsTooLong` | `TeamSlugTooLong` |
| 30 | same | `Validate_ShouldFail_WhenSlugHasInvalidCharacters` (`[Theory]`: `"Bad Slug"`, `"bad--slug"`, `"-bad"`, `"bad-"`) | `TeamSlugInvalid` |
| 31 | same | `Validate_ShouldFail_WhenSlugIsReserved` | `TeamSlugReserved` |
| 32 | `Teams/UpdateTeam/UpdateTeamHandlerTests` | `Handle_ShouldUpdateMetadata_WhenRequestIsValid` | fields updated; `Update` and `SaveChangesAsync` `Received(1)` |
| 33 | same | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserNotAuthenticated`; no save |
| 34 | same | `Handle_ShouldThrowNotFound_WhenTeamDoesNotExist` | `TeamNotFound`; no save |
| 35 | same | `Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner` | `TeamNotOwned`; no save |
| 36 | `Teams/UpdateTeam/UpdateTeamSlugHandlerTests` | `Handle_ShouldChangeSlug_WhenTeamHasNeverPublished` | slug changed; one save |
| 37 | same | `Handle_ShouldNotChangeSlug_WhenSlugIsUnchanged` | `IsSlugExistsAsync` `DidNotReceive`; slug unchanged |
| 38 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenSlugIsFrozen` | published team, different slug; `TeamSlugFrozen`; no save |
| 39 | `Teams/UpdateTeam/UpdateTeamValidatorTests` | `Validate_ShouldPass_WhenSlugIsNull` | `IsValid` true with a null slug |
| 40 | same | `Validate_ShouldFail_WhenTeamIdIsEmpty` | `TeamIdRequired` |
| 41 | same | `Validate_ShouldFail_WhenSlugIsProvidedAndInvalid` | `TeamSlugInvalid` |
| 42 | `Teams/DeleteTeam/DeleteTeamHandlerTests` | `Handle_ShouldSoftDeleteTeam_WhenCallerIsTheOwner` | `Status` `Deleted`, `IsDeleted` true; one save |
| 43 | same | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserNotAuthenticated`; no save |
| 44 | same | `Handle_ShouldThrowNotFound_WhenTeamDoesNotExist` | `TeamNotFound`; no save |
| 45 | same | `Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner` | `TeamNotOwned`; no save |
| 46 | `Teams/DeleteTeam/DeleteTeamValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` / `Validate_ShouldFail_WhenTeamIdIsEmpty` | `TeamIdRequired` |
| 47 | `Teams/GetTeam/GetTeamQueryHandlerTests` | `Handle_ShouldReturnTeam_WhenTeamIsPublishedAndCallerIsAnonymous` | detail returned; `currentUserService.UserId` never consulted for the guard |
| 48 | same | `Handle_ShouldReturnTeam_WhenCallerIsTheOwnerOfADraft` | detail returned |
| 49 | same | `Handle_ShouldThrowNotFound_WhenTeamDoesNotExist` | `TeamNotFound` |
| 50 | same | `Handle_ShouldThrowUnauthorized_WhenTeamIsDraftAndCallerIsAnonymous` | `UserNotAuthenticated` |
| 51 | same | `Handle_ShouldThrowForbidden_WhenTeamIsDraftAndCallerIsNotTheOwner` | `TeamNotOwned` |
| 52 | same | `Handle_ShouldOrderMembersBySortOrder_WhenTeamHasMembers` | `Members` come back ordered 0,1,2 regardless of the stored list order |
| 53 | `Teams/GetTeam/GetTeamQueryValidatorTests` | pass + `Validate_ShouldFail_WhenTeamIdIsEmpty` | `TeamIdRequired` |
| 54 | `Teams/ListMyTeams/ListMyTeamsQueryHandlerTests` | `Handle_ShouldReturnOwnedTeamsNewestFirst_WhenCallerIsAuthenticated` | ordering by `CreationDate` descending; `MemberCount` correct |
| 55 | same | `Handle_ShouldReturnEmptyList_WhenCallerOwnsNoTeams` | empty list, not null |
| 56 | same | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserNotAuthenticated` |
| 57 | `Teams/CheckTeamSlugAvailability/CheckTeamSlugAvailabilityQueryHandlerTests` | `Handle_ShouldReturnAvailable_WhenSlugIsFree` | `IsAvailable` true, `SuggestedSlug` null |
| 58 | same | `Handle_ShouldReturnSuggestion_WhenSlugIsTaken` | `IsAvailable` false, `SuggestedSlug` non-empty and different |
| 59 | same | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserNotAuthenticated` |
| 60 | `Teams/CheckTeamSlugAvailability/CheckTeamSlugAvailabilityQueryValidatorTests` | one pass plus one failing `[Fact]`/`[Theory]` per slug rule (5 rules) | `TeamSlugRequired`, `TeamSlugTooShort`, `TeamSlugTooLong`, `TeamSlugInvalid`, `TeamSlugReserved` |

### Application — membership

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 61 | `Teams/SetTeamMembers/SetTeamMembersHandlerTests` | `Handle_ShouldReplaceMembersInSubmittedOrder_WhenRequestIsValid` | three members, `SortOrder` 0,1,2 matching submitted order; one `SaveChangesAsync` |
| 62 | same | `Handle_ShouldPinToLatestVersion_WhenPinnedVersionIsNull` | member pinned to the engineer's `LatestVersionId`; `PinnedSemanticVersion` copied from that version |
| 63 | same | `Handle_ShouldPinToExplicitVersion_WhenPinnedVersionIsGiven` | the explicit older version id is stored, not `LatestVersionId` |
| 64 | same | `Handle_ShouldKeepExistingPin_WhenMemberIsAlreadyInTheTeamAndPinIsNull` | the pre-existing `PinnedVersionId` survives even though the engineer has a newer `LatestVersionId` |
| 65 | same | `Handle_ShouldRemoveMembers_WhenSubmittedListIsEmpty` | `Members` empty; one save; engineer and version repositories `DidNotReceive` any `FindAsync` |
| 66 | same | `Handle_ShouldDenormaliseSlugAndSemanticVersion_WhenReplacing` | `EngineerSlug` and `PinnedSemanticVersion` equal the engineer's slug and the version's semantic version |
| 67 | `Teams/SetTeamMembers/SetTeamMembersHandlerGuardTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserNotAuthenticated`; no save |
| 68 | same | `Handle_ShouldThrowNotFound_WhenTeamDoesNotExist` | `TeamNotFound`; no save |
| 69 | same | `Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner` | `TeamNotOwned`; no save |
| 70 | same | `Handle_ShouldThrowNotFound_WhenMemberEngineerDoesNotExist` | `EngineerNotFound`; no save |
| 71 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenMemberHasNeverPublished` | engineer with null `LatestVersionId`; `TeamMemberNotPublished`; no save |
| 72 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsNotPublished` | version in `Queued`; `TeamMemberVersionNotPublished`; no save |
| 73 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionBelongsToAnotherEngineer` | `TeamMemberVersionNotPublished`; no save |
| 74 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsATeamVersion` | `ItemType.Team` version id; `TeamMemberVersionNotPublished`; no save |
| 75 | same | `Handle_ShouldAddMember_WhenMemberEngineerIsUnlistedButHasAPublishedVersion` | succeeds; one save (Decision 7) |
| 76 | same | `Handle_ShouldAddMember_WhenMemberEngineerBelongsToAnotherCreator` | succeeds; one save (Decision 8) |
| 77 | `Teams/SetTeamMembers/SetTeamMembersValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | true |
| 78 | same | `Validate_ShouldPass_WhenMembersIsEmpty` | true |
| 79 | same | `Validate_ShouldFail_WhenTeamIdIsEmpty` | `TeamIdRequired` |
| 80 | same | `Validate_ShouldFail_WhenMembersExceedTheLimit` | `TeamMemberLimitReached` at `MaxMembersPerTeam + 1` |
| 81 | same | `Validate_ShouldFail_WhenTheSameEngineerAppearsTwice` | `TeamMemberDuplicate` |
| 82 | same | `Validate_ShouldFail_WhenAMemberEngineerIdIsEmpty` | `TeamMemberEngineerIdRequired` |
| 83 | `Teams/Shared/TeamMemberPinResolverTests` | `ResolvePins_ShouldPreserveSubmittedOrder_WhenMembersAreResolved` | pin order equals selection order |
| 84 | `Teams/Shared/TeamResultGeneratorTests` | `Generate_ShouldMapTeamFields_WhenCalled` | every `TeamResult` field including `MemberCount` and `Status` string |
| 85 | same | `GenerateDetail_ShouldOrderMembersBySortOrderThenEngineerId_WhenCalled` | deterministic order from a shuffled input |
| 86 | `Teams/Shared/TeamRosterGeneratorTests` | `Generate_ShouldOrderRosterBySortOrderThenEngineerId_WhenCalled` | ordering and one-to-one field copy |

### Application — team publish

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 87 | `Teams/PublishTeam/PublishTeamHandlerTests` | `Handle_ShouldCreateQueuedTeamVersion_WhenTeamHasMembers` | `ItemType.Team`, `ItemId` is the team id, `VersionNumber` 1, `SemanticVersion` `"1.0.0"`, `Status` `Queued`; `AddAsync` and `SaveChangesAsync` `Received(1)` |
| 88 | same | `Handle_ShouldFreezeTheRosterIntoTheVersion_WhenPublishing` | `JsonSerializer.Deserialize<TeamRosterResult>(version.FrozenManifestJson)` has the same member count, engineer ids, slugs, pinned version ids and semantic versions as the team, ordered by `SortOrder` |
| 89 | same | `Handle_ShouldIncrementFromTheLatestVersion_WhenTeamHasPublishedBefore` | previous `1.0.0` plus `Minor` gives `1.1.0`, `VersionNumber` 2 |
| 90 | same | `Handle_ShouldRaisePublishRequestedEvent_WhenVersionIsCreated` | `version.GetDomainEvents()` contains a `PublishRequestedDomainEvent` carrying the version id |
| 91 | `Teams/PublishTeam/PublishTeamHandlerGuardTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserNotAuthenticated`; no save |
| 92 | same | `Handle_ShouldThrowNotFound_WhenTeamDoesNotExist` | `TeamNotFound`; no save |
| 93 | same | `Handle_ShouldThrowForbidden_WhenCallerIsNotTheOwner` | `TeamNotOwned`; no save |
| 94 | same | `Handle_ShouldThrowBadRequest_WhenTeamHasNoMembers` | `TeamEmpty`; no save |
| 95 | same | `Handle_ShouldThrowConflict_WhenAPublishIsAlreadyInProgress` | `PublishAlreadyInProgress`; no save |
| 96 | same | `Handle_ShouldThrowBusinessRuleViolation_WhenVersionLimitIsReached` | `PublishVersionLimitReached`; no save |
| 97 | `Teams/PublishTeam/PublishTeamValidatorTests` | pass, `Validate_ShouldFail_WhenTeamIdIsEmpty`, `Validate_ShouldFail_WhenIncrementIsNotAnEnumValue` | `TeamIdRequired`, `PublishIncrementInvalid` |

### Publishing — assembly, determinism, worker

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 98 | `Publishing/Shared/TeamTreeAssemblerTests` | `Assemble_ShouldNamespaceSkillFolders_WhenMembersContributeSkills` | `skills/house-rules/SKILL.md` from member `alpha` becomes `skills/alpha--house-rules/SKILL.md` |
| 99 | same | `Assemble_ShouldNamespaceSkillsEvenWithoutCollision_WhenOnlyOneMemberHasThem` | single member still produces `skills/alpha--x/SKILL.md` |
| 100 | same | `Assemble_ShouldKeepAgentNames_WhenNoCollisionExists` | `agents/reviewer.md` and `agents/builder.md` from two members keep their names |
| 101 | same | `Assemble_ShouldPrefixEveryCollidingAgent_WhenTwoMembersShareAFileName` | both become `agents/alpha--reviewer.md` and `agents/beta--reviewer.md`; neither unprefixed path survives |
| 102 | same | `Assemble_ShouldPrefixEveryCollidingCommand_WhenTwoMembersShareAFileName` | same for `commands/` |
| 103 | same | `Assemble_ShouldDropNonInstallableRoots_WhenMembersShipHooksOrMcp` | `hooks/hooks.json`, `.mcp.json`, `output-styles/x.md` absent from the result |
| 104 | same | `Assemble_ShouldDropFilesMissingFromTheMemberManifest_WhenSnapshotHasExtraFiles` | an asset absent from that member's manifest targets is excluded |
| 105 | same | `Assemble_ShouldIncludeTeamPluginJson_WhenCalled` | `.claude-plugin/plugin.json` present; its deserialized `name` is `e3a-team-{slug}`, `version` is the semantic version, `author.url` ends `/t/{slug}` |
| 106 | same | `Assemble_ShouldOrderFilesOrdinallyByPath_WhenCalled` | result paths are `Ordinal`-sorted |
| 107 | `Publishing/Shared/TeamTreeAssemblerDeterminismTests` | `Assemble_ShouldProduceIdenticalZipSha256_WhenCalledTwiceWithTheSameRoster` | `DeterministicZipper.Create` over both results gives equal `Sha256` |
| 108 | same | `Assemble_ShouldProduceIdenticalZipSha256_WhenMemberInputOrderIsShuffled` | the same members supplied in a different list order give the same sha256 (proves the symmetric collision rule is order-independent) |
| 109 | same | `Assemble_ShouldProduceIdenticalZipSha256_WhenTheMemberEngineerHasPublishedANewerVersion` | the pinning invariant: rebuilding from the same pinned `TeamMemberSnapshot` set after a newer member version exists gives the same sha256 |
| 110 | `Publishing/Shared/TeamSnapshotReaderTests` | `ReadAsync_ShouldReturnRelativePaths_WhenSnapshotBlobsExist` | `snapshots/{versionId}/agents/x.md` becomes `agents/x.md` |
| 111 | same | `ReadAsync_ShouldReturnEmptyList_WhenNoSnapshotBlobsExist` | empty list |
| 112 | same | `ReadAsync_ShouldSkipBlobs_WhenDownloadReturnsNull` | null-content blob excluded |
| 113 | same | `ReadAsync_ShouldNotWriteAnyBlob_WhenCalled` | `UploadAsync` and `DeleteByPrefixAsync` both `DidNotReceive` |
| 114 | `Publishing/Shared/TeamPublishBuilderTests` | `BuildAsync_ShouldReturnTeamBuild_WhenRosterIsValid` | `FailureReason` null, `PluginName` is `e3a-team-{slug}`, `Team` non-null, `Engineer` null, files contain both members' namespaced skills |
| 115 | same | `BuildAsync_ShouldReadOnlyThePinnedSnapshotPrefixes_WhenRosterHasTwoMembers` | `ListByPrefixAsync` called with `{pinnedVersionId}/` for each pinned id and with no other prefix |
| 116 | same | `BuildAsync_ShouldNeverLoadTheMemberEngineer_WhenBuilding` | the builder signature has no `IEngineerRepository` — asserted structurally by the test compiling with only the four repositories, plus `authorName` resolved from the team owner |
| 117 | same | `BuildAsync_ShouldFallBackToTeamSlugForAuthorName_WhenOwnerUserNameIsBlank` | `AuthorName` equals the team slug |
| 118 | `Publishing/Shared/TeamPublishBuilderFailureTests` | `BuildAsync_ShouldFail_WhenTeamDoesNotExist` | `FailureReason` `TeamNotFound`, `Files` empty |
| 119 | same | `BuildAsync_ShouldFail_WhenRosterJsonIsUnreadable` | `TeamRosterInvalid` |
| 120 | same | `BuildAsync_ShouldFail_WhenRosterIsEmpty` | `TeamEmpty` |
| 121 | same | `BuildAsync_ShouldFail_WhenAPinnedVersionIsMissing` | `TeamMemberVersionNotPublished` |
| 122 | same | `BuildAsync_ShouldFail_WhenAPinnedVersionIsNotPublished` | `TeamMemberVersionNotPublished` |
| 123 | same | `BuildAsync_ShouldFail_WhenAMemberManifestIsUnreadable` | `TeamMemberManifestInvalid` |
| 124 | same | `BuildAsync_ShouldFail_WhenAMemberSnapshotIsEmpty` | `TeamMemberSnapshotEmpty` |
| 125 | same | `BuildAsync_ShouldFail_WhenNoMemberContributesInstallableContent` | `FailureReason` contains `PluginNoInstallableContent` |
| 126 | same | `BuildAsync_ShouldNotWriteAnyBlob_WhenBuildFails` (`[Theory]` over two failure setups) | `UploadAsync` `DidNotReceive` |
| 127 | `Publishing/ProcessPublishJob/ProcessPublishJobHandlerTeamTests` | `Handle_ShouldPublishTeamVersionAndTeam_WhenRosterIsValid` | `version.Status` `Published`, `version.ZipBlobPath` is `z/e3a-team-{slug}/1.0.0.zip`, `ZipSha256` 64 chars, `team.LatestVersionId` set, `team.Status` `Published`; `SaveChangesAsync` `Received(2)` |
| 128 | same | `Handle_ShouldUploadTeamZipWithImmutableCacheHeaders_WhenPublishing` | `UploadAsync` to the public container with the team zip path, `ZipContentType`, `ZipCacheControl`, `overwrite: false` |
| 129 | same | `Handle_ShouldWriteTeamPinnedMarketplace_WhenPublishing` | `UploadAsync` to `m/e3a-team-{slug}/1.0.0/marketplace.json`, `MarketplaceContentType`, `MarketplaceCacheControl`, `overwrite: true` |
| 130 | same | `Handle_ShouldFailVersionAndTouchNoPublicBlob_WhenTeamBuildFails` | roster empty; `version.FailureReason` `TeamEmpty`; `team.Status` still `Draft`; no `UploadAsync` to the public container; `SaveChangesAsync` `Received(2)` |
| 131 | same | `Handle_ShouldNotMarkTheEngineerPublished_WhenVersionIsATeamVersion` | `engineerRepository.Update` `DidNotReceive` |
| 132 | same | `Handle_ShouldResumeFromBuilding_WhenTeamVersionIsAlreadyBuilding` | published with `SaveChangesAsync` `Received(1)` |
| 133 | `Publishing/Shared/PluginStructureValidatorDuplicatePathTests` | `Validate_ShouldReturnDuplicatePathError_WhenTwoFilesShareAPath` | `PluginDuplicatePath` present |
| 134 | same | `Validate_ShouldReturnDuplicatePathError_WhenTwoFilePathsDifferOnlyByCase` | `PluginDuplicatePath` present |
| 135 | same | `Validate_ShouldReturnNoDuplicatePathError_WhenAllPathsAreDistinct` | `PluginDuplicatePath` absent |
| 136 | `Publishing/Shared/PluginNameTests` | `ForEngineer_ShouldPrefixWithE3a_WhenCalled` / `ForTeam_ShouldPrefixWithE3aTeam_WhenCalled` | `e3a-x` and `e3a-team-x` |
| 137 | `Publishing/Shared/PublicCatalogUrlTests` | `ForEngineer_ShouldBuildEnginerPageUrl_WhenCalled` / `ForTeam_ShouldBuildTeamPageUrl_WhenCalled` / `ForTeam_ShouldNotDoubleSlash_WhenSiteUrlHasATrailingSlash` | `https://e3a.dev/e/x`, `https://e3a.dev/t/x` |

### Publishing — marketplace

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 138 | `Publishing/Shared/PublishedTeamCollectorTests` | `CollectAsync_ShouldReturnPluginEntriesForPublishedTeams_WhenTeamsExist` | one entry per published team with `e3a-team-{slug}` name, version, sha256 and `/t/{slug}` author url |
| 139 | same | `CollectAsync_ShouldSkipTeams_WhenTheirLatestVersionIsNotPublished` | excluded |
| 140 | same | `CollectAsync_ShouldFallBackToTeamSlugForAuthorName_WhenOwnerUserNameIsBlank` | author name equals the slug |
| 141 | same | `CollectAsync_ShouldThrowInternalServerError_WhenTeamPagesExceedTheMaximum` | `InternalServerErrorCoreException` with `MarketplaceTeamLimitExceeded` |
| 142 | `Publishing/RegenerateMarketplace/RegenerateMarketplaceTeamTests` | `Handle_ShouldIncludeEnginersAndTeams_WhenBothArePublished` | the uploaded document's `plugins` array contains both `e3a-{slug}` and `e3a-team-{slug}` |
| 143 | same | `Handle_ShouldOrderPluginsOrdinallyByName_WhenBothTypesArePresent` | `plugins` names are `Ordinal`-sorted |
| 144 | same | `Handle_ShouldUploadOnce_WhenBothTypesArePresent` | one `UploadAsync` to `marketplace.json` with `overwrite: true` |
| 145 | `Publishing/GetPublishStatus/GetPublishStatusQueryTeamTests` | `Handle_ShouldReturnStatus_WhenVersionIsATeamVersionOwnedByCaller` | `ItemId` is the team id, `ItemType` is `nameof(ItemType.Team)` |
| 146 | same | `Handle_ShouldThrowNotFound_WhenTeamVersionHasNoTeam` | `TeamNotFound` |
| 147 | same | `Handle_ShouldThrowForbidden_WhenTeamVersionBelongsToAnotherCreator` | `TeamNotOwned` |

`EngineerPublishBuilder` gets no dedicated test file: it is a lift-and-shift of the engineer branch and
stays fully covered end to end by the four existing `ProcessPublishJobHandler*Tests` classes, which
must remain green with only the constructor and single-assertion edits listed above.

## Docs sync

Divergence only — never delete a not-yet-built feature from a doc.

| Doc + section | Current text | Required edit |
|---|---|---|
| `docs/implementation-plan.md` "Data model", `engineers / teams` bullet (line 41) | "(separate tables, same shape)" and "the slug is the entire plugin name `e3a-{slug}`" | Say engineer plugin names are `e3a-{slug}` and team plugin names are `e3a-team-{slug}`, so the two slug namespaces cannot collide; and note teams carry no `DraftManifestJson` and no `InstallCount` (no upload draft; install counting not built), so the shapes are near-identical rather than identical. |
| `docs/implementation-plan.md` "Data model", `team_members` bullet (line 42) | "`team_members`: (TeamId, EngineerId) PK, PinnedVersionId, SortOrder" | Replace with the shipped shape: `Id` primary key, `TeamId`, `EngineerId`, `EngineerSlug`, `PinnedVersionId`, `PinnedSemanticVersion`, `SortOrder`; unique index `(TeamId, EngineerId)` filtered on `IsDeleted = 0`; `EngineerId` and `PinnedVersionId` carry no foreign key so a member engineer can be deleted without breaking a published team. |
| `docs/implementation-plan.md` "Plugin build spec", Naming bullet (line 51) | "`e3a-{slug}` — the creator-typed slug is the plugin name" | Add the team form `e3a-team-{slug}`. |
| `docs/implementation-plan.md` "Plugin build spec", Team zip bullet (line 53) | "each member materialized as `agents/{member-slug}.md` + `skills/{member-slug}--{skill-slug}/`" | Replace with the shipped rule: skills always namespaced `skills/{member-slug}--{skill-slug}/`; `agents/` and `commands/` merged with every colliding file prefixed `{member-slug}--`; only those three roots are merged in this slice, with hooks, `.mcp.json`, `.lsp.json` and the other roots deferred to `team-compile-merge`. |
| `docs/implementation-plan.md` "Build phases", P5 (line 74) | "P5 Teams: team CRUD, member picker w/ pinned versions, namespaced team compile from snapshots, 'newer member versions' republish flow" | Split into the shipped `teams` slice (CRUD, pinned membership, limits, publish through the existing worker, `e3a-team-{slug}`, marketplace) and the deferred `team-compile-merge` slice (hook concatenation with attribution, `.mcp.json`/`.lsp.json` merge, the newer-versions republish prompt). |
| `docs/plugin-spec.md` "Naming" | "Plugin name: `e3a-{slug}`" | Add: teams are `e3a-team-{slug}`; team and engineer slugs are separate namespaces and may repeat. |
| `docs/plugin-spec.md` "Team plugin layout" | promises hooks concatenation and `.mcp.json`/`.lsp.json` merge-by-server-name as current behaviour | Keep the rules as the target, but mark hooks, `.mcp.json`, `.lsp.json` and the remaining roots **deferred to the `team-compile-merge` slice — not merged today**; state that today only `agents/`, `skills/` and `commands/` are merged; state the collision rule precisely (every colliding file is prefixed, not just the later one); state that the roster is frozen into the team version so a member republish cannot alter a published team. |
| `docs/plugin-spec.md` "marketplace.json" | describes engineers only ("unlisted engineers drop out", author url `/e/{slug}`) | Say teams are listed alongside engineers with `e3a-team-{slug}` names and `author.url` `https://<domain>/t/{slug}`. |
| `docs/architecture.md` "Publish pipeline (queue worker)" | one engineer-shaped sequence ("freeze drafts into `snapshots/{versionId}` → assemble from the snapshot + the frozen import manifest") | Describe the branch: the worker marks Building, then builds by item type — engineers freeze their drafts into `snapshots/{versionId}` and assemble from the frozen import manifest; teams read the frozen roster from the version row and read each pinned member's existing `snapshots/{pinnedVersionId}` prefix read-only, namespacing as they merge — after which validate, zip, upload, pinned marketplace and root regeneration are shared. Add: at most two `SaveChangesAsync` per job, and no public-container write happens on any failure path. |
| `docs/architecture.md` "Principles", Limits bullet | "50 engineers + 10 teams per creator; 50 versions per item" | Add the members-per-team cap (10). |
| `docs/security-scan.md` | no team text | **No edit.** The scanner is not wired here and the doc makes no claim this change contradicts. Absence of team scanning is incompleteness, not divergence (acceptance decision 11). |
| `docs/design-prompt.md` | describes the team detail and composer pages | **No edit.** No UI content, flow or component changes in this slice. |

## Definition of done

- [ ] `dotnet build` clean with zero new warnings; `dotnet test` green.
- [ ] `Team`, `TeamMember`, `TeamMemberPin`, `TeamStatus`, `ITeamRepository` exist in `api/E3A.Domain/Teams/` with private constructors, `Create` factories, and every mutator setting `UpdationDate`.
- [ ] `Team` has no `InstallCount`, no `DraftManifestJson`, no `Unlist`/`Relist` method, and no `BusinessRuleViolationException` throw.
- [ ] `Migrations/<timestamp>_teams005` creates only `Teams` and `TeamMembers`, with the unique filtered `Slug` index, the unique filtered `(TeamId, EngineerId)` index, the `OwnerUserId` and `TeamId` indexes, and cascade from `Teams` to `TeamMembers`.
- [ ] `Team` and `TeamMember` are both registered in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`; no `IsDeleted` check appears in any query or handler.
- [ ] Every cap (`MaxTeamsPerCreator`, `MaxMembersPerTeam`, all lengths, `ReservedSlugs`) lives in `TeamsOptions`; no numeric or string cap is a constant on an entity, validator or handler.
- [ ] `EngineerSlugGenerator` and `EngineerSlugResolver` no longer exist; `SlugGenerator` and `SlugResolver` are the single implementations and are used by both areas; no slug logic is duplicated.
- [ ] `PluginName.ForEngineer` and `PluginName.ForTeam` exist; `PluginName.For` is gone; `e3a-team-` is a named constant with a WHY comment.
- [ ] `/e/` and `/t/` appear only inside `PublicCatalogUrl`.
- [ ] The eight endpoints in "API surface" exist on `TeamsController` with the listed verbs, routes, status codes and request records; the controller contains no business logic.
- [ ] `PUT /api/teams/{teamId}/members` is idempotent: submitting the same ordered list twice leaves identical `SortOrder`, `PinnedVersionId` and `PinnedSemanticVersion` values.
- [ ] A member with no published version, a pinned version that is not `Published`, a pinned version belonging to another engineer, and a pinned team version are each rejected with the codes in "Error codes".
- [ ] Publishing an empty team is rejected in the handler and re-checked in `TeamPublishBuilder`.
- [ ] A team version's `FrozenManifestJson` deserializes to the ordered roster, and `TeamPublishBuilder` reads the roster from the version rather than from `TeamMember` rows.
- [ ] `TeamPublishBuilder` takes no `IEngineerRepository` and performs no blob write.
- [ ] `ProcessPublishJobHandler` is under 100 lines, has exactly one `ItemType` switch, and performs at most two `SaveChangesAsync` on every path per the save-count table.
- [ ] No `UploadAsync` targets the public container on any failure path, asserted by test for both item types.
- [ ] The same roster produces the same sha256 across repeated builds and across shuffled member input order, asserted by test.
- [ ] A member engineer publishing a newer version does not change a rebuilt team's sha256, asserted by test.
- [ ] Skills are always namespaced; colliding `agents/`/`commands/` files are all prefixed; hooks, `.mcp.json`, `.lsp.json` and other roots are dropped, asserted by test.
- [ ] `marketplace.json` contains engineers and teams, ordinal-ordered by plugin name, written once per regeneration; the team page loop is guarded by `MARKETPLACE_TEAM_LIMIT_EXCEEDED` and the engineer guard is unchanged.
- [ ] `GET /api/publish/{versionId}/status` returns `ItemId` and `ItemType` and enforces team ownership for team versions.
- [ ] All 28 new error codes exist in `ErrorCodes.cs` and as keys in **both** resx files, with `{limit}` and `{engineerId}` placeholders intact in both languages.
- [ ] `postman/e3a.postman_collection.json` has a `Teams` folder covering all eight endpoints.
- [ ] Every test row in the Test plan exists with the given class and method name and passes; the six listed existing test files are updated, not deleted.
- [ ] The eleven docs edits in "Docs sync" are made; `docs/security-scan.md` and `docs/design-prompt.md` are untouched.
- [ ] No Azure resource is created, referenced or required; `AzureOptions` gains no new member.
- [ ] The new `Teams` configuration section is called out in the implementation notes so the dev can mirror it into his environments and Azure App Configuration.
