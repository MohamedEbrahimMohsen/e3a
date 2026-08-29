VERDICT: CHANGES_REQUESTED

# Review — Teams (pinned-member team plugin)

One blocking finding. The shipped **code** implements the pinning invariant correctly and structurally;
the **test** that the plan's Definition of Done nominates as its proof asserts nothing about pinning.
Everything else in the slice — 50 files, the migration, the save-count matrix, the error-code table,
Postman, the 11 docs edits — verified clean.

## Blocking

### 1. Test 109, the pinning-invariant proof, is vacuous — it never feeds the newer snapshot to the system under test

**Where:** `api/E3A.Tests/Publishing/Shared/TeamTreeAssemblerDeterminismTests.cs:36-46`
(specifically `:41` constructs `newerVersionOfAlpha`, and `:42` ignores it)

**Rule:** plan Definition of Done — "A member engineer publishing a newer version does not change a
rebuilt team's sha256, asserted by test" (`01-plan.md:721`); plan test row 109 (`01-plan.md:633`);
testing convention §9.

**Problem:** the test builds `newerVersionOfAlpha` and then never passes it to `Assemble`. Both
`DeterministicZipper.Create(Assemble(pinnedMembers))` calls at `:40` and `:42` take the **identical**
`pinnedMembers` list. The test is therefore equivalent to test 107
(`Assemble_ShouldProduceIdenticalZipSha256_WhenCalledTwiceWithTheSameRoster`) plus one tautological
assertion that a locally-built 3-path snapshot has more assets than a locally-built 2-path snapshot.

It is also at the wrong level. `TeamTreeAssembler.Assemble` is a pure function over a
`List<TeamMemberSnapshot>` — it has no channel through which live member state could ever leak, so no
mutation of it can express the defect the test claims to exclude. The pinning invariant actually lives
in `TeamPublishBuilder.BuildAsync` (roster read from `version.FrozenManifestJson`,
`TeamPublishBuilder.cs:24`) and `TeamSnapshotReader.ReadAsync` (reads only
`snapshots/{pinnedVersionId}/`, `TeamSnapshotReader.cs:10-11`). No test anywhere in the suite places a
newer published `ItemVersion` for a member engineer in front of the builder — confirmed by grep:
`Newer` appears in exactly one test file, this one.

`02-implementation.md:169-174` asserts under "Verification of the risk areas you called out" that
"Test 109 builds a strictly larger newer member snapshot … and asserts the sha256 from the pinned
snapshots is unchanged". Both clauses are individually true and mutually unconnected: the larger
snapshot is never an input to the thing being hashed. The claim that this test verifies the invariant
is not supported by the code.

**Failure:** mutation run, then reverted (file md5 restored to `f4fee59c486027c77226a9c910bd8106`):
changing `:42` to `DeterministicZipper.Create(Assemble([newerVersionOfAlpha, pinnedMembers[1]]))`
makes the test fail on the sha256 comparison at `:45`. That input — the newer member content actually
reaching the build — is precisely what the shipped test never supplies. Conversely, no change to
production code can make the shipped test fail, because the only variable it varies is one it does not
use.

**Fix:** move the test to the builder level, where the invariant exists. In `TeamPublishBuilderTests`
(or a new `TeamPublishBuilderPinningTests`), take the existing `GivenTwoMemberTeam` scenario and
additionally stub a **newer published `ItemVersion`** for alpha's engineer id (higher `VersionNumber`,
its own snapshot prefix stubbed with strictly more files) into the `_itemVersionRepository.FindAsync`
result and the blob client. Build twice — once before that newer version exists, once after — and
assert `DeterministicZipper.Create(build.Files).Sha256` is unchanged and that the newer version's files
are absent from `build.Files`. That test fails the moment the builder resolves content by engineer
rather than by `PinnedVersionId`.

## Non-blocking

- `api/E3A.Tests/Teams/SetTeamMembers/SetTeamMembersHandlerGuardTests.cs` (151 lines) and
  `SetTeamMembersHandlerTests.cs` (123 lines) also exceed the ~100-line guideline but are absent from
  Deviation 6's list — seven files exceed it, not five. The ruling on Deviation 6 stands; only the
  count in the report is wrong.
- `02-implementation.md:153` — "This was the only existing assertion I changed in the entire slice" is
  inaccurate. Three existing assertions changed: `Received(1)`→`Received(2)`
  (`ProcessPublishJobHandlerFailureTests.cs:51`), `result.EngineerId`→`result.ItemId`
  (`GetPublishStatusQueryHandlerTests.cs:44`), and `EngineerId`→`ItemId` plus the new `ItemType`
  assertion (`PublishStatusResultGeneratorTests.cs:24`). All three are explicitly mandated by the plan
  (`01-plan.md:135,137,138`), so nothing is hidden — the sentence just overstates.
- `api/E3A.Tests/Publishing/Shared/PublicCatalogUrlTests.cs:11`
  `ForEngineer_ShouldBuildEnginerPageUrl…` and `RegenerateMarketplaceTeamTests.cs:47`
  `Handle_ShouldIncludeEnginersAndTeams…` both misspell "Engineer". Both typos originate in the plan
  (`01-plan.md:661,671`) and the implementer correctly honoured the name contract.
- `01-plan.md:448` save-count row "`Building` (queue retry), build fails → 1" has no test. It is
  structurally guaranteed by the `if (version.Status == ItemVersionStatus.Queued)` gate
  (`ProcessPublishJobHandler.cs:32`) and the plan's test plan never required it. Cheap to add beside
  test 132.

## Verified

**Build and test claims — independently reproduced.**

- `dotnet build api/E3A.slnx --no-incremental` → **Build succeeded, 0 Errors, 9 Warnings**, all nine in
  `api/core-libraries` (Core.Validation x2, Core.OTP x2, Core.Notifications x5). Zero in any E3A
  project. Matches the report exactly.
- `dotnet test api/E3A.slnx` → **Failed: 0, Passed: 520, Skipped: 0, Total: 520**. Matches.

**Migration `teams005`** (`20260829124339_teams005.cs`) creates only `Teams` and `TeamMembers` — two
`CreateTable` calls, nothing else. `IX_Teams_Slug` unique with the `IsDeleted = 0` filter (`:84-89`),
`IX_TeamMembers_TeamId_EngineerId` unique with the same filter (`:72-77`), `IX_Teams_OwnerUserId`
(`:79-82`), `IX_TeamMembers_TeamId` (`:67-70`), and `onDelete: ReferentialAction.Cascade` on the FK
(`:59-64`). Column widths 100/100/500/400/50/20 match `TeamsOptions`. Exactly the plan's contract.

**Pinning invariant — the code half is sound (the test half is finding 1).**
`TeamPublishBuilder.BuildAsync` (`TeamPublishBuilder.cs:15`) takes `ITeamRepository`,
`IItemVersionRepository`, `IUserRepository`, `IStorageBlobClient` and **no `IEngineerRepository`** —
structurally incapable of reading live member state. The roster is deserialized from
`version.FrozenManifestJson` (`:24`), never from `TeamMember` rows. This half *is* genuinely
constrained by test: `TeamPublishBuilderTests.GivenTwoMemberTeam` (`:83`) stubs the repository with
`TeamFactory.Draft(...)`, whose `Members` collection is **empty** — so if the builder read the table
instead of the frozen roster it would fail `TeamEmpty` and tests 114/115/116/117 would all break. Test
115 additionally pins `ListByPrefixAsync` to exactly the two pinned prefixes and no others (`:50-53`).
`TeamSnapshotReader` performs no write, asserted by test 113.

**Determinism.** Tests 107 and 108 bite. 108 supplies `[members[1], members[0]]` and asserts an equal
sha256; the symmetric collision rule at `TeamTreeAssembler.cs:26-33` prefixes *every* colliding member
(the fixture has alpha and beta both shipping `agents/reviewer.md`), so a prefix-only-the-later-one
implementation would produce order-dependent output and fail.

**Save-count matrix — verified per path, not in aggregate.** Three `SaveChangesAsync` sites
(`ProcessPublishJobHandler.cs:36` MarkBuilding, `:98` FailAsync, `:72` terminal). Queued+success → 2
(team test 127 `:61`); Queued+build-fails → 2 (team test 130 `:95`; engineer
`ProcessPublishJobHandlerFailureTests.cs:51,63`); Building+success → 1 (team test 132 `:117`);
Building+fail → 1 by construction; not-found and terminal → 0.

**Nothing reaches the public container on failure, both types.** Team:
`ProcessPublishJobHandlerTeamTests.cs:94` asserts `DidNotReceive()` on `UploadAsync` bound to
`PublicBlobContainerName`. Engineer: `ProcessPublishJobHandlerFailureTests.cs:62,76` assert
`DidNotReceive()` on *any* `UploadAsync`, which is strictly stronger. Builder level:
`TeamPublishBuilderFailureTests.cs:118-133` is a `[Theory]` over two failure setups asserting no upload
on either overload. The engineer path's pre-gate `DraftSnapshotFreezer` write targets the private
snapshots container only; the team path writes nothing pre-gate.

**Pass 1 was behaviour-neutral.** Full `git diff -M` of `api/E3A.Tests/` reviewed line by line: the
only assertion edits in the entire slice are the three plan-mandated ones listed under Non-blocking.
Every other test change is a constructor argument (`Substitute.For<ITeamRepository>()`), a `using`, a
namespace, a class rename, or the `RegenerateMarketplaceHandlerTests` empty-page stub — all itemised in
`01-plan.md:132-142`. `SlugGeneratorTests`, `SlugGeneratorTypedInputTests` and `SlugResolverTests`
carry the renamed class names in the renamed namespaces with bodies otherwise intact.

**Contract fidelity.** All 50 planned files exist; no extra production file was created. `Team.cs` and
`TeamMember.cs` match the plan's prescribed bodies member for member, every mutator stamps
`UpdationDate`, zero domain throws, no `InstallCount` / `DraftManifestJson` / `Unlist`. `TeamsOptions`
holds all ten caps plus `ReservedSlugs`; no cap is a constant anywhere else. `EngineerSlugGenerator`,
`EngineerSlugResolver` and the old `PluginName.For` no longer exist anywhere in `api/` or `web/`. The
`/e/` and `/t/` segments appear only inside `PublicCatalogUrl.cs`. Eight controller actions match the
plan's API surface (verbs, routes, `[AllowAnonymous]` on `GET {teamId}` only, `201 CreatedAtAction`,
`202 Accepted`, `204 NoContent`); the controller holds no business logic. Both `Team` and `TeamMember`
are registered in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` (`AppDbContext.cs:117-118`); no
ad-hoc `IsDeleted` check exists in any query or handler.

**Skill §8 DO/DON'T walked entry by entry — no DON'T present.** 8.1 caps in `TeamsOptions`, consumed by
validators *and* `AppDbContext.ConfigureTeams`. 8.2 `IGenerator` injected, no hand-rolled randomness.
8.3 `ITeamRepository.IsSlugExistsAsync` plus the `SlugResolver` suffix loop, no `ConflictCoreException`
for an auto-resolvable slug. 8.4 `TeamStatus.Deleted` with `Delete()` calling `SoftDelete()`. 8.5 the
query filter lives only in the global method; the partial-index SQL filter stays with the index.

**Style absolutes.** Swept the new code: zero `try`/`catch`, zero `DateTime` (all `DateTimeOffset`),
zero block-scoped namespaces, every handler and validator `sealed`, zero comments in the Teams area,
`SaveChangesAsync` once per handler. Every `await` outside controllers and test bodies carries
`.ConfigureAwait(false)` — the single miss in the repo (`UploadEngineerDraftHandler.cs:49`,
`await using`) is pre-existing and untouched by this slice.

**Error codes.** 28 new constants (26 in a new `// Teams` group, plus `PluginDuplicatePath` and
`MarketplaceTeamLimitExceeded` in `// Publishing`), values matching the plan's table one for one. All
28 present exactly once in **both** `Messages.en.resx` and `Messages.ar.resx` (scripted check, zero
mismatches). The `{limit}` and `{engineerId}` placeholders are intact in both languages; the Arabic
carries no tashkeel. Every one of the 12 distinct codes thrown from new Teams and collector code is
asserted against an `ErrorCodes.*` constant in the suite; every `Failed(...)` branch in
`TeamPublishBuilder` has a dedicated test (rows 118-125).

**Postman.** New `Teams` folder with all eight endpoints in plan order, correct methods and URLs,
`Content-Type: application/json` on the four bodied requests, bearer auth inherited from the
collection, bodies matching the plan's samples. Diff is **+212 / -0** — purely additive, no request
removed or mutated. The `PublishStatusResult` shape change touches only response bodies, so no team or
engineer request is stale.

**Docs sync.** All 11 planned edits made, none more than required. `docs/plugin-spec.md` now marks
hooks, `.mcp.json`, `.lsp.json`, `output-styles/`, `monitors/`, `bin/` and `themes/` as **deferred to
the `team-compile-merge` slice, not merged today**, while keeping the target rules — the promise is
narrowed honestly rather than silently broken, and the not-yet-built parts are not deleted.
`docs/architecture.md` describes the `ItemType` branch, the read-only pinned-snapshot read, the
two-save bound and the no-public-write-on-failure guarantee. `docs/implementation-plan.md` carries the
corrected `team_members` shape, the `e3a-team-{slug}` namespace and the split P5.
`docs/security-scan.md` and `docs/design-prompt.md` are untouched, correctly. No divergence found.
**The unplanned 12th edit (10 members per team, in the `implementation-plan.md` limits line) is correct
and should stay** — a new cap is a policy change, which `.claude/rules/docs-sync.md` lists as a
divergence trigger, so it was obligatory rather than optional. The implementer undersold it as
incompleteness.

**`PublishStatusResult.EngineerId` → `ItemId` plus `ItemType`.** Every consumer moved; grep confirms no
reader of the old member survives in `api/` or `web/`. `PublishStatusResultGenerator` passes
`version.ItemId` and `version.ItemType.ToString()`. `GetPublishStatusQueryHandler` branches on
`version.ItemType` and throws `TeamNotFound` / `TeamNotOwned` for team versions (tests 145-147).

**No Azure resource, no `az` command, no new `AzureOptions` member.** Teams reuse the existing
snapshots and public containers and the existing `publish-jobs` queue.

### Rulings requested by the implementer

**1. The `Teams` config section existing only on this machine — an accepted property of the repo's
config policy, not a blocking defect; the documentation obligation is discharged.**
`docs/constitution.md:99` settles it: `appsettings.json` is git-ignored, no configuration file is
committed, every environment supplies the full configuration externally, and **new options sections
must be announced to the dev so he can mirror them into his environments**. `.gitignore:23` confirms
`/api/E3A.Api/appsettings.json` is ignored and `git ls-files` confirms no `appsettings.json` is
tracked. `Engineers`, `Uploads`, `Azure`, `Catalog` and `Publishing` are all in exactly the same
position, and `Engineers` already drives EF column widths identically. The announcement was made in
plan Decision 38 and `02-implementation.md:194-207` with the full JSON block. Nothing to fix here. The
schema-drift exposure is real but pre-existing and repo-wide; closing it is a config-governance slice
of its own, not this slice's debt.

**2. `PLUGIN_DUPLICATE_PATH` on the engineer path — not a regression. It cannot fire on any engineer
content that publishes successfully today.** I traced every path that can reach `files` on the engineer
build. The rule triggers only when two entries differ solely by case
(`PluginStructureValidator.cs:33,36-39`, `OrdinalIgnoreCase` set versus list count). Those entries come
from `PluginTreeAssembler.Assemble` (`:13`) — draft snapshot blobs whose paths are in the frozen
manifest — plus the generated `plugin.json`. Upstream, `UploadPathNormalizer.Normalize`
(`UploadPathNormalizer.cs:27-35`) already rejects the whole upload with `UPLOAD_DUPLICATE_PATH` (400)
using an `OrdinalIgnoreCase` `seenPaths` set, so a draft can never hold two paths differing only by
case. The one synthetic Converted target, the generated house-rules skill, is itself collision-guarded
case-insensitively at `DraftNormalizer.cs:103-105`. And `.claude-plugin/plugin.json` cannot arrive from
a user zip in a way that duplicates the generated one: `DraftNormalizer.Classify` (`:108-122`) has no
`ImportedRootCategories` entry for that root, so it is skipped and never enters the allowed set. The
rule is a pure backstop against a hazard the upload gate already closes. It is also a strict
improvement: before it, such a tree would have passed validation and `DeterministicZipper` (`:22-27`)
would have silently emitted two entries for one effective path. The implementer's note 4 ("it cannot
fire on any current fixture") understates the guarantee — it cannot fire on any reachable engineer
draft at all.

**Deviation 2 (`TeamFactory.PublishedMember` plus `TeamMemberFixture`) — sound, not papering over a
plan error.** The plan's T1/T2 factories genuinely cannot produce an `Engineer` whose
`LatestVersionId` equals a real `ItemVersion.Id`, because that field is only settable through
`MarkPublished`. The implementation builds it purely through domain methods —
`EngineerFactory.Draft(...)`, then `ItemVersionFactory.Published(...)`, then
`engineer.MarkPublished(version.Id)` (`TeamFactory.cs:50-57`) — which is exactly what
`conventions/dotnet-testing.md` §4 prescribes and avoids the prohibited reflection. Both additions live
inside the planned T1 file, so no unplanned file appeared. The right call; the plan's factory list was
simply incomplete.

**Deviations 5 and 6 (files over ~100 lines) — accepted, and correctly surfaced rather than silently
restructured.** `AppDbContext.cs` at 121: the plan prescribes `ConfigureTeams(ModelBuilder)` as a named
private method *in `AppDbContext`* (`01-plan.md:126,222`) and skill §6.3 prohibits
`ApplyConfigurationsFromAssembly`, so within this plan's contract the growth is forced; the file is a
registry and splitting it would itself have been an undeclared deviation from a written instruction.
`ErrorCodes.cs` at 101 is by skill §5.1 design a single flat comment-grouped registry — the length
guideline cannot bind it without contradicting §5.1. The test files are the weaker case: the guideline
is real and `TeamPublishBuilderFailureTests` at 168 could split cleanly into version failures and
content failures. But the plan fixes which numbered test lives in which named class, splitting would
break that contract mid-flight, and repo precedent exists (`RegenerateMarketplaceHandlerTests` at 140
on `main`). Raising it instead of choosing unilaterally was correct. **None of this gates.** If the dev
wants the test files split, that is a follow-up with a plan amendment, not rework here.

## Test quality

Per class — does it actually constrain the implementation?

- **`TeamTreeAssemblerDeterminismTests`** — 107 and 108 constrain (shuffled input breaks an asymmetric
  collision rule). **109 constrains nothing** — finding 1. This is the only test in the suite that
  fails the bar.
- `TeamTreeAssemblerTests` (9) — constrains. 98/99 fail without unconditional skill namespacing;
  101/102 assert both prefixed paths *and* `NotContain` the unprefixed one, so prefix-only-the-later
  fails; 103 drops non-installable roots; 104 adds a rogue asset absent from the manifest and asserts
  exclusion; 105 deserializes the real `plugin.json` and checks name, version and author URL.
- `TeamPublishBuilderTests` (4) and `TeamPublishBuilderFailureTests` (9) — strongly constrain. The
  member-less `TeamFactory.Draft` stub makes 114-117 fail if the roster is read from the table; 115
  pins the exact snapshot prefixes and the total call count; every `Failed(...)` branch has a test; 126
  is a `[Theory]` over two failure shapes asserting both `UploadAsync` overloads never fire.
- `ProcessPublishJobHandlerTeamTests` (6) — constrains. Real zip path, 64-character sha, both
  cache-header uploads with exact `overwrite` flags, `_engineerRepository.DidNotReceive().Update`, and
  the three distinct save counts.
- `SetTeamMembersHandlerTests` (6) and `SetTeamMembersHandlerGuardTests` (10) — constrain. Test 64 pins
  to an older version id genuinely distinct from `LatestVersionId`, so the null-to-existing-pin
  fallback cannot be faked; 65 asserts both repositories received no `FindAsync`; every guard asserts
  `DidNotReceive` on save.
- `PublishedTeamCollectorTests` (4) — constrains. 139 uses a team whose `LatestVersionId` has no
  matching published version and asserts exclusion; 141 stubs two pages against `marketplaceMaxPages: 1`.
- `RegenerateMarketplaceTeamTests` (3) — constrains. The ordering test mixes zeta/alpha engineers with
  alpha-squad/beta-squad teams; ordinal order interleaves the two types, so dropping the final
  `OrderBy` fails it.
- `TeamRosterGeneratorTests` (1) — constrains: `team.Members.Reverse()` before generating.
- `TeamTests`, `TeamSlugTests`, `TeamMembershipTests`, `TeamMemberTests` (14) — constrain.
  Resequencing, drop-previous, empty, pin-field copy, `BeOnOrAfter(before)` stamps, no wall-clock
  equality.
- `PluginStructureValidatorDuplicatePathTests` (3) — constrains, including the negative case.
- `PluginNameTests` and `PublicCatalogUrlTests` — thin but exact-value; the trailing-slash case is a
  real branch.
- Validator classes (CreateTeam, CreateTeamSlug, UpdateTeam, DeleteTeam, GetTeam, SetTeamMembers,
  CheckTeamSlugAvailability, PublishTeam) — each has a passing `[Fact]` plus one failing case per rule,
  asserting on `ErrorCodes.*` constants rather than message strings, per convention §7.

Every plan-named test method (133 distinct names extracted from `01-plan.md`) exists verbatim in the
suite. No wall-clock equality, no `DateTime`, no reflection, no inter-test ordering anywhere.

## Reviewer note

The one mutation experiment run against the working tree (`TeamTreeAssemblerDeterminismTests.cs:42`)
was reverted immediately; the file's md5 is back to `f4fee59c486027c77226a9c910bd8106` and
`git status` shows the same 72 entries as at review start. No other file was touched.
