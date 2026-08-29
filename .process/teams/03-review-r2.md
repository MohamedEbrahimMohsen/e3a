VERDICT: APPROVED

# Review round 2 — Teams (pinned-member team plugin)

Scoped verification of the rework against round 1's single blocking finding. That finding is
resolved: the pinning invariant is now proven by a test that genuinely bites, at the level where the
invariant actually lives. Nothing else changed, and nothing round 1 verified has regressed.

## Ruling on round 1's finding 1 — resolved

`api/E3A.Tests/Publishing/Shared/TeamPublishBuilderTests.cs:80-93` is a real test.

- The newer `ItemVersion` genuinely reaches the system under test. `GivenNewerPublishedVersion`
  (`:113-124`) re-stubs `_itemVersionRepository.FindAsync` to return `[alpha, beta, newerVersion]`
  (`:121`) and stubs `ListByPrefixAsync` for the newer version's own snapshot prefix with two blobs
  (`:120`), one of them `agents/refactorer.md` — a path the newer manifest covers (`:115`), so the
  assembler would not silently drop it. The team row, the roster and the pinned ids are untouched.
  Contrast round 1's defect, where `newerVersionOfAlpha` was constructed and discarded.
- Assertions are ordered so the Definition-of-Done invariant reports first: `FailureReason` null
  (`:89`), sha256 equal to the pre-newer-version build (`:90`), `agents/refactorer.md` absent from
  `Files` (`:91`), and `ListByPrefixAsync` never called with the newer prefix (`:92`). The newer id
  is a fresh `Guid`, so `:92` cannot be satisfied by accident.

**Mutation run independently, not taken from the transcript.** I byte-copied
`api/E3A.Application/Publishing/Shared/TeamPublishBuilder.cs`, then mutated resolution from pin to
engineer at `:43` and `:57`:

    -  var memberVersion = memberVersions.Find(x => x.Id == member.PinnedVersionId);
    +  var memberVersion = memberVersions.Where(x => x.ItemId == member.EngineerId).OrderByDescending(x => x.VersionNumber).FirstOrDefault();
    -  TeamSnapshotReader.ReadAsync(storageBlobClient, azureOptions, member.PinnedVersionId, ...)
    +  TeamSnapshotReader.ReadAsync(storageBlobClient, azureOptions, memberVersion.Id, ...)

`dotnet test api/E3A.slnx` under the mutation:

    Failed E3A.Tests.Publishing.Shared.TeamPublishBuilderTests.BuildAsync_ShouldProduceIdenticalZipSha256_WhenTheMemberEngineerHasPublishedANewerVersion
     Expected DeterministicZipper.Create(afterNewerVersion.Files).Sha256 to be "77391ee75bd8a54bca8eecaf9a61a63c2416dcb6671e388e3c7670f4554f5af4", but "9149e0f8e31d6bbe542b5545b3fe998b9ef82ac787c3394bbffb87f76445a5e8" differs near "914" (index 0).
    Failed! - Failed: 1, Passed: 520, Skipped: 0, Total: 521

Exactly one failure, and it is the new test — no other test in 521 detects the defect, which is
precisely why this one had to exist. Restored from the byte copy: `cmp` reports byte-identical, md5
`909f759561c4863a94001ada07d391b8`, `git status` unchanged at 73 entries, and `dotnet test` returns
`Failed: 0, Passed: 521`. The shas match the implementer's transcript exactly, so the transcript was
honest as well as correct.

**The `FindAsync`-predicate caveat is sufficient.** The substitute ignores its predicate, so the test
cannot distinguish "queried by pin" from "queried broadly, then filtered by pin". That distinction is
not the invariant — both produce pinned content. What the test does constrain is resolution inside
the loop (`TeamPublishBuilder.cs:43`) and the blob prefix actually read (`:57`), which is where the
defect lives; the `DidNotReceive` at `:92` pins content resolution independently of the stub's
realism, and test 115 (`TeamPublishBuilderTests.cs:44-56`) still bounds the exact prefix set and the
total call count. The implementer disclosed this rather than glossing it; the disclosure is accurate
and the residual is not a way for a wrong implementation to ship.

**The rewritten assembler test is sound and non-tautological.**
`TeamTreeAssemblerDeterminismTests.cs:35-45` now actually feeds the larger snapshot —
`Assemble([largerAlpha, pinnedMembers[1]])` at `:42` — and asserts the sha256 **differs** (`:44`).
Its passing proves the assembler is content-sensitive, which is what makes "unchanged" at builder
level a claim about pinning rather than a property of a constant. A degenerate or
content-insensitive assembler fails it. The name
`Assemble_ShouldProduceADifferentZipSha256_WhenAMemberSnapshotGainsAFile` makes no pinning claim.

## Deviation 7 — accepted

Moving test 109's invariant to `TeamPublishBuilderTests` under the same method name (`BuildAsync_`
prefix) and renaming the assembler case was the right call, and it is properly declared at
`02-implementation.md:315`.

The plan's fixed-name contract and the plan's Definition of Done (`01-plan.md:721`) were in direct
conflict. `TeamTreeAssembler.Assemble` is pure over `List<TeamMemberSnapshot>` with no channel
through which live member state can leak, so at that level the DoD line is not merely hard to
assert — it is inexpressible, and any test bearing that name there is necessarily vacuous. Honouring
the name would have preserved the letter of the test plan at the cost of the invariant the test
exists to protect. The move is minimal (one method relocated, one renamed, no file created or
deleted) and was surfaced as a numbered deviation rather than slipped in. `01-plan.md:721` is
satisfied for the first time.

The two "Enginer" to "Engineer" corrections (`PublicCatalogUrlTests.cs:10`,
`RegenerateMarketplaceTeamTests.cs:47`) likewise depart from plan names at `01-plan.md:661,671` — but
those plan names are typos that round 1 identified as such, so fixing them is an improvement, not a
hidden deviation. A repo-wide grep for "Enginer" across `api/`, `docs/` and `postman/` now returns
nothing.

## Non-blocking

- `api/E3A.Tests/Publishing/Shared/TeamPublishBuilderTests.cs:115` — the newer version's manifest is
  strictly **wider** than the pinned one. An implementation that read content from the pinned prefix
  but took the *manifest* from the engineer's latest version would still pass, because a wider
  manifest filters nothing out; a **narrower** newer manifest would expose it. A second case would
  close that corner. Does not gate — the shipped code resolves both from the pin
  (`TeamPublishBuilder.cs:43,57`) and the primary mutation is caught.
- `api/E3A.Tests/Publishing/Shared/TeamPublishBuilderTests.cs` is now 141 lines. Same standing ruling
  as round 1's Deviation 6: recorded, not gating.

## Verified

**Build and test claims — independently reproduced, not read from the report.**

- `dotnet build api/E3A.slnx --no-incremental` gives **Build succeeded, 0 Errors, 9 Warnings.** All
  nine are in `api/core-libraries` (Core.Validation x2, Core.OTP x2, Core.Notifications x5); zero in
  any E3A project. Matches the claim.
- `dotnet test api/E3A.slnx` gives **Failed: 0, Passed: 521, Skipped: 0, Total: 521.** Matches the
  claimed 521, up one from round 1's independently reproduced 520 — consistent with one test added
  and one rewritten in place.

**Scope containment — verified from the tree, not the claim.** Every file under `api/`, `docs/`,
`postman/` and `.claude/` modified after round 1's review (`.process/teams/03-review.md`, mtime
16:30:14) is exactly:

- `api/E3A.Tests/Publishing/Shared/PublicCatalogUrlTests.cs` — typo only
- `api/E3A.Tests/Publishing/RegenerateMarketplace/RegenerateMarketplaceTeamTests.cs` — typo only
- `api/E3A.Tests/Publishing/Shared/TeamTreeAssemblerDeterminismTests.cs` — rewritten case
- `api/E3A.Tests/Publishing/Shared/TeamPublishBuilderTests.cs` — new test plus scenario plumbing
- `api/E3A.Application/Publishing/Shared/TeamPublishBuilder.cs` — touched only by the implementer's
  own mutation experiment and restored; its md5 was already `909f759561c4863a94001ada07d391b8`
  before my run, and reading it end to end confirms pin-based resolution at `:43` and `:57` and no
  `IEngineerRepository` in the signature at `:15`.

So "no production code changed" holds. The two report corrections (`02-implementation.md:296-309`)
are accurate: three existing assertions changed, all plan-mandated, and seven test files exceed
about 100 lines rather than five.

**Nothing round 1 verified has regressed.** The mtime sweep shows `docs/`, `postman/`, both resx
files, the `teams005` migration and all Teams production code untouched this round. Spot-confirmed
anyway: `postman/e3a.postman_collection.json` diff is **+212 / -0** — additive only, so no stale,
missing or orphaned request; `Messages.en.resx` and `Messages.ar.resx` each hold 81 data entries and
a diff over the sorted key sets reports them identical; `docs/architecture.md` +19/-5,
`docs/implementation-plan.md` +6/-6, `docs/plugin-spec.md` +23/-8, with `docs/security-scan.md` and
`docs/design-prompt.md` untouched. `git diff` on any `AzureOptions` file is empty. Round 1's other
non-blocking items were correctly left alone; their presence is not a finding.

**No Azure resource, no `az` command.** No `AzureOptions` member, no new container or queue. The only
commands run this round were `dotnet build`, `dotnet test`, `git`, `find`, `grep` and file reads.

**Docs sync.** Nothing this round alters behaviour, scope, architecture, policy or a contract — two
test method renames and one test added. No doc references either renamed method. No divergence, and
per `.claude/rules/docs-sync.md` incompleteness is not reportable.

## Test quality

Only the delta is re-judged; round 1's per-class assessment stands for the rest.

- **`TeamPublishBuilderTests`** (5) — now constrains the invariant it names. 114-117 already failed if
  the roster were read from the `TeamMember` table (the stubbed team's `Members` is empty) and 115
  bounds the exact prefix set; the new test is the only one of 521 that catches pin-to-engineer
  resolution, proven by the mutation above. The strongest class in the suite.
- **`TeamTreeAssemblerDeterminismTests`** (3) — all three now constrain. 107 and 108 bite the
  symmetric collision rule; the rewritten third case varies its input and asserts the converse
  property that gives the builder-level "unchanged" its meaning. Round 1's "one test in the suite
  fails the bar" no longer applies.
- `PublicCatalogUrlTests`, `RegenerateMarketplaceTeamTests` — bodies unchanged, names corrected only;
  assertions verified untouched.

The slice is ready for its PR.
