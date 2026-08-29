# CodeRabbit rework — PR #7 (teams)

All 8 IMPLEMENT items are done. RC5 stays rejected (no options validation added). RC6 stays escalated
(no change to `api/core-libraries`). `.process/teams/03-review.md` and `03-review-r2.md` are untouched.

No Azure resource, no `az` command, no migration, no resx change, no `try`/`catch` added to any handler.

## Numbered findings

| # | Item | What changed | Where |
|---|---|---|---|
| 1 | **CRITICAL** namespace overlap (RC4, RC3) | Added `PluginName.IsTeamNamespaced(slug)` beside `TeamSegment`; rewrote the WHY comment that asserted the falsified invariant | `api/E3A.Application/Publishing/Shared/PluginName.cs:8-11,23-26` |
| 1 | | New rule `.Must(slug => !PluginName.IsTeamNamespaced(SlugGenerator.NormalizeTypedSlug(slug)))` with `ErrorCodes.EngineerSlugReserved` and the existing "is reserved." message, keeping each site's own `.When(...)` guard | `CreateEngineerValidator.cs:44-48`, `UpdateEngineerValidator.cs:47-51`, `CheckSlugAvailabilityQueryValidator.cs:44-48` |
| 1 | | Tests: one failing case per validator, plus a true/false pair for the new public member | `CreateEngineerSlugValidatorTests.cs:71-78`, `UpdateEngineerSlugValidatorTests.cs:73-80`, `CheckSlugAvailabilityQueryValidatorTests.cs:74-81`, `PluginNameTests.cs:17-31` |
| 1 | | Doc: naming contract now states engineer slugs may not begin `team-`, and why | `docs/implementation-plan.md:51` |
| 1 | | Falsified claim corrected in the live PR description | `.process/teams/pr-body.md:19` |
| 1 | | Dated correction appended (decisions left intact) | `.process/teams/00-acceptance.md` tail, `.process/teams/01-plan.md` tail |
| 2 | Team-version fixtures (RC9) | Added `ItemVersionFactory.PublishedTeam(Guid teamId, ...)` mirroring `Published`; used at both sites | `ItemVersionFactory.cs:39-46`, `SetTeamMembersHandlerGuardTests.cs:104`, `PublishTeamHandlerTests.cs:69` |
| 3 | `RegenerateMarketplaceTeamTests` (RC7) | `ItemVersionFactory.Published(team.Id, ...)` → `PublishedTeam(team.Id, ...)`, same `zipBlobPath` | `RegenerateMarketplaceTeamTests.cs:95` |
| 4 | Slug-suggestion assertion (RC8) | Two weak assertions replaced with `result.SuggestedSlug.Should().Be(SuffixedSlug);` | `CheckTeamSlugAvailabilityQueryHandlerTests.cs:53` |
| 5 | Republish adoption (RC11) | Both sites corrected to the explicit re-pin-then-republish workflow; the deferred `team-compile-merge` target kept named | `docs/plugin-spec.md:73-78`, `docs/implementation-plan.md:53` |
| 6 | Failure guarantee (RC10 first half) | "any failure path" → "any **build**-failure path", with the post-zip failure spelled out | `docs/architecture.md:55-59` |
| 7 | Fence language (RC2) | opening fence → `text` | `.process/teams/pr-body.md:39` |
| 8 | Pinning argument (RC1 PR-body half) | Dependency absence demoted to supporting evidence; `ITeamRepository` presence acknowledged; the frozen-roster / pinned-snapshot data flow named as the proof | `.process/teams/pr-body.md:12` |

## Mutation proofs

**RC9 site b — `Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsATeamVersion`.**
Deleted `version.ItemType != ItemType.Engineer || ` from `TeamMemberPinResolver.cs:27` and ran
`dotnet test --filter FullyQualifiedName~SetTeamMembersHandlerGuardTests`:

- with the **new** `PublishedTeam` fixture: `Failed: 1, Passed: 9` — the failing test is exactly
  `Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsATeamVersion`. It bites.
- with the **old** `QueuedTeam` fixture restored and the clause still deleted: `Failed: 0, Passed: 10`.
  That is the direct confirmation that the test was vacuous before this change.
- clause restored, new fixture in place: `Failed: 0, Passed: 10`. `git diff` on
  `TeamMemberPinResolver.cs` is empty, so the restore is byte-exact.

**RC9 site a — `Handle_ShouldIncrementFromTheLatestVersion_WhenTeamHasPublishedBefore`.**
Not requested, but checked because the fixture changed. Mutated `PublishTeamHandler.cs:60` to
`SemanticVersionCalculator.Next(null, request.Increment)`: `Failed: 1, Passed: 3`, the failure being
that test. Restored; `git diff` on the handler is empty. The increment assertions do bite, as the
triage said — this site was fixture realism only.

**RC7 — `RegenerateMarketplaceTeamTests`. The assertion is NOT load-bearing, and I could not make it
bite.** There is no `ItemType` clause in `PublishedTeamCollector` to delete, so instead I added a
deliberately *wrong* one — `&& x.ItemType == ItemType.Engineer` on the version predicate at
`PublishedTeamCollector.cs:36` — which under a correct fixture should exclude every team version and
break the class. Result: `Failed: 0, Passed: 3`. The predicate never runs, because
`_itemVersionRepository` is an NSubstitute double that ignores its `Expression` argument and returns
the canned list. So neither fixture can detect an ItemType filter regression at this level; the change
is fixture realism, exactly as the triage predicted, and no stronger claim should be made for it.
Mutation reverted; `git diff` on `PublishedTeamCollector.cs` is empty.

## Deviations from the triage

| Triage said | What I did | Why |
|---|---|---|
| Item 1 test for `CreateEngineerSlugValidatorTests` alongside the existing reserved theory | Wrote it as a `[Fact]` with the literal `"team-alpha"`, not a `[Theory]` | A `[Theory](string slug)` version produced a body byte-identical to the existing `Validate_ShouldFail_WhenSlugIsReserved(string slug)`, which fails Sonar **S4144** and therefore the build (`TreatWarningsAsErrors`). A `[Fact]` with the literal differs. Case-insensitivity is covered where the rule lives, in `PluginNameTests`. |
| Test count 607–609 | **614** | The three validator tests land as expected. `PluginNameTests` gained 7 cases rather than 2: `IsTeamNamespaced` is the whole security guard, so I covered `team-alpha`/`Team-Alpha`/`team-` true and `alpha`/`teams`/`team`/`steam-alpha` false. `teams` and `team` matter — both are near misses that must **not** be rejected, and `teams` is already in `ReservedSlugs` for an unrelated reason. |

## Carried consequences and things worth a second look

- **Existing engineers with a `team-` slug are not migrated.** Not acted on, per instruction. The
  triage's basis for that being harmless (`00-acceptance.md:61`, nothing deployed) is unverified by me
  beyond reading it — I confirmed only that the repository contains no seed data, no fixture and no
  Postman example using a `team-`-prefixed **engineer** slug, so nothing in-tree breaks. If a database
  already holds one, `UpdateEngineer` on that row will now 422 on a slug it previously accepted, and
  the collision itself remains live for that row until it is renamed.
- **The guard is validator-level only.** It fires on `CreateEngineer`, `UpdateEngineer` and
  `CheckSlugAvailability`. Slugs written by any other route — a migration, a seed, direct SQL, or a
  future admin path that bypasses these three validators — are not covered. `PluginName.ForEngineer`
  itself still happily produces `e3a-team-x` if handed a `team-x` slug; making it throw would move a
  publish-time failure into a namer with three call sites, which is beyond this triage's scope.
- **`IsTeamNamespaced` is `OrdinalIgnoreCase` while the callers pass an already-lowercased slug.**
  `SlugGenerator.NormalizeTypedSlug` lowercases, so the comparison's case-insensitivity is redundant
  at those three call sites. I kept it because the method is public and the invariant should not
  depend on the caller having normalized first.
- **`docs/architecture.md` now states the post-zip failure case twice** — once at `:50-52` and once in
  the sentence I narrowed. That is deliberate (the narrowed claim needed its own counterweight so it
  is not read as "no public write ever"), but a reviewer may prefer a cross-reference to a restatement.
- **RC10's rejected half was genuinely already correct**; I re-read `docs/architecture.md:33-38` and it
  does list root-`marketplace.json` regeneration last, after "persist Published". No edit needed there.
- **Postman was not touched.** No endpoint was added, removed or changed shape; item 1 changes only
  which slug values a validator accepts. The collection's only slug examples are
  `dive-backend-engineer` and `dotnet-product-squad`, neither affected.

## Build and test

Run in the worktree `C:/Users/HP/AppData/Local/Temp/claude/wt-teams`.

| Command | Baseline (before any edit) | After |
|---|---|---|
| `dotnet build api/E3A.slnx --no-incremental` | `Build succeeded. 9 Warning(s) 0 Error(s)` | `Build succeeded. 9 Warning(s) 0 Error(s)` |
| `dotnet test api/E3A.slnx` | `Failed: 0, Passed: 604, Skipped: 0, Total: 604` | `Failed: 0, Passed: 614, Skipped: 0, Total: 614` |

All 9 warnings are in `api/core-libraries` (Core.Validation 2 × CS8602, Core.OTP 2 × CS8618,
Core.Notifications 5 × CS8618) — unchanged from baseline, and none in `api/E3A.*`.
