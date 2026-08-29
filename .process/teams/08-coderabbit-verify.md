VERDICT: APPROVED

# CodeRabbit verification — PR #7 (teams), stage 4

Independent verification of `07-coderabbit-rework.md` against the working tree at
`C:/Users/HP/AppData/Local/Temp/claude/wt-teams`. Every claim below was reproduced by me, not read
from the rework report. No blocking findings.

## The Critical item — the collision is closed

**1. Every engineer-slug write path is covered. There is no fourth path.**

`Engineer.Slug` is written at exactly two sites in non-test code:

- `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs:36` (`Engineer.Create`)
- `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs:45` (`engineer.ChangeSlug`)

Both sit behind MediatR commands, and validators run in the pipeline via
`Core.CQRS/Behaviours/ValidationBehaviour.cs:7` registered at `Core.CQRS/DependencyInjection.cs:15`,
with `services.AddValidatorsFromAssembly(...)` at `api/E3A.Application/DependencyInjection.cs:14`. The
guard is present at `CreateEngineerValidator.cs:44-48`, `UpdateEngineerValidator.cs:47-51` and
`CheckSlugAvailabilityQueryValidator.cs:44-48`, each keeping its own `.When(...)` guard and each
normalizing with `SlugGenerator.NormalizeTypedSlug` before the check.

`CheckSlugAvailability` is a query and writes nothing; it is guarded anyway so the UI cannot advertise
a slug the create call will reject. `SlugResolver.ResolveUniqueAsync`
(`api/E3A.Application/Shared/SlugResolver.cs:8`) only ever appends a suffix to an already-validated
base, so it cannot manufacture a `team-` prefix. There is no seed data and no `HasData`/`InsertData`
anywhere in `api/E3A.Infrastructure`. Confirmed: no fourth path.

The reverse direction needs no guard, and I confirmed the reasoning holds. `PluginName.ForTeam`
(`PluginName.cs:18`) can only ever emit `e3a-team-{x}`; the only way `ForEngineer` (`:13`) reaches that
string is a slug starting `team-`. Closing the engineer side makes the two namespaces disjoint in both
directions without a cross-repository lookup. `TeamSegment` correctly stayed `private`
(`PluginName.cs:11`), so `IsTeamNamespaced` and `ForTeam` share one source of truth and cannot drift.

**2. The near-misses are still allowed, and the seven cases assert what they claim.**

`api/E3A.Tests/Publishing/Shared/PluginNameTests.cs:17-30` — true for `team-alpha`, `Team-Alpha`,
`team-`; **false** for `alpha`, `teams`, `team`, `steam-alpha`. All seven present and asserting the
claimed direction. `team` and `teams` are not rejected by the new rule.

One accurate caveat: the engineer slug `teams` is still rejected at validator level, but by the
pre-existing exact-match reserved list (`EngineerFactory.cs:58` lists `teams` among the 15 reserved
words), not by this change. That is prior behaviour and out of scope here. `team` is accepted by both
rules.

**3. Mutation proof that the three validator tests are not vacuous — run by me.**

`team-alpha` is valid kebab-case, is length 10 (within 3..100), and is **not** in `ReservedSlugs`, so
the only rule that can fire is the new one. Proved rather than argued: I replaced the predicate with
a constant-true lambda in all three validators and ran the validator tests.

    Failed: 3, Passed: 45, Total: 48

The three failures are exactly the three new `Validate_ShouldFail_WhenSlugUsesTheTeamNamespacePrefix`
tests. Restored; tree byte-identical.

## Ruling on the two disclosed limits

**Limit A — the guard is validator-level only. NOT blocking. Validator-level is sufficient here.**

I cannot write the "Failure:" line for this, which is the test for blocking. Both write paths are
behind validated commands (verified above), there is no seed, no `HasData`, no admin path, and no
migration writes an engineer row. So there is no reachable input today that produces a colliding name.

Making `PluginName.ForEngineer` throw would move a 422 validation error into a runtime exception inside
a namer with three call sites, one of which is the publish worker — that converts a clean rejection at
the API boundary into a failed publish job. That is a worse trade, and it is a design change, not a
fix. The mitigation that actually matters is already in place: the WHY comment at `PluginName.cs:8-11`
names `IsTeamNamespaced` and states that the validators are what enforce it, so a future implementer
reading `ForEngineer` sees the invariant and where it lives. Correctly ruled and correctly disclosed.

**Limit B — no migration for existing `team-`-prefixed engineer slugs. NOT blocking; a correctly
documented carried consequence.**

`00-acceptance.md:60` states nothing is deployed, and the migration history starts at
`20260827082800_initial` on a fresh schema, so no such row can exist. I independently confirmed no seed
data, no fixture and no Postman example uses a `team-`-prefixed engineer slug (`postman/` contains only
`dive-backend-engineer` and `dotnet-product-squad`).

Worth recording that the hypothetical blast radius is narrower than the rework report suggests: each
validator guards the rule with a null-slug `.When(...)`, so an `UpdateEngineer` that changes only
display name or tags passes a null slug and still succeeds. Only re-submitting the `team-` slug itself
would 422. The disclosure was honest and slightly conservative, which is the right direction to be
wrong in.

## Vacuity fixes — both halves of the RC9 experiment run by me

I backed up `TeamMemberPinResolver.cs` and `SetTeamMembersHandlerGuardTests.cs`, plus the full
working-tree diff, before touching anything.

**Half A — new `PublishedTeam` fixture, the `ItemType` clause deleted from
`api/E3A.Application/Teams/Shared/TeamMemberPinResolver.cs:27`:**

    Failed: 1, Passed: 9, Total: 10

The single failure is
`SetTeamMembersHandlerGuardTests.Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsATeamVersion`
(`SetTeamMembersHandlerGuardTests.cs:107`). It bites.

**Half B — the one that matters. Old `QueuedTeam` fixture restored, clause still deleted:**

    Failed: 0, Passed: 10, Total: 10

Confirmed directly: the old test could not have caught the defect. With a `Queued` fixture the
status-not-Published clause was already true, so the test named for the ItemType rule proved nothing
about the ItemType rule. The `PublishedTeam` fixture (`ItemVersionFactory.cs:39-45`) makes the ItemId
and Published-status clauses both hold, so only the ItemType clause can fire. This is a real fix to a
real second vacuous test in this slice.

**Restore verified byte-exact.** Both files match their pre-experiment sha256, and `git diff` of the
whole tree is byte-identical to the pre-experiment capture. Full suite re-run after restore:
`Failed: 0, Passed: 614`.

**RC8** — the fix bites too. `CheckTeamSlugAvailabilityQueryHandlerTests.cs:48` stubs the generator to
return a value with a trailing hyphen, and `:53` now asserts equality against `SuffixedSlug`, which has
none. Deleting the trailing-hyphen trim at `SlugResolver.cs:22` makes the returned value differ from
the constant, so the assertion fails. The old `NotBeNullOrEmpty` / `NotBe(TypedSlug)` pair could not
distinguish them.

## RC7 — the analysis is correct, and it generalises

I reproduced it. `api/E3A.Application/Publishing/Shared/PublishedTeamCollector.cs:36` has no `ItemType`
clause to delete, so I added a deliberately wrong one restricting the version query to
`ItemType.Engineer`, which under a correct fixture should exclude every team version and break the
class:

    Failed: 0, Passed: 3

The predicate is never executed. `RegenerateMarketplaceTeamTests.cs:37` stubs the repository `FindAsync`
with an `Arg.Any<Expression<...>>` matcher returning a canned list — NSubstitute treats the expression
as an argument to match on and never compiles or invokes it. Mutation reverted; tree byte-identical.

**Stated plainly, because it is a real limitation of this test shape:** a predicate passed to a
substituted repository is never run, so **no** test of this shape can constrain a query predicate. This
is not specific to `RegenerateMarketplaceTeamTests` — it applies to every unit test in this repo that
stubs `FindAsync`, `FirstOrDefaultAsync`, `CountAsync` or `FindPaginatedAsync` with an
`Arg.Any<Expression<...>>` matcher, including the filters in `PublishedEngineerCollector` and
`PublishedTeamCollector.cs:20,36-38`.

**Does it affect confidence in any other test in this slice?** It bounds it, and the bound was already
respected. Nothing in this slice claims a repository predicate as proven — the load-bearing proofs sit
at levels where mutation does bite: `TeamPublishBuilder` (round 1 replacement test),
`TeamMemberPinResolver` (verified above), the version-increment path (`PublishTeamHandler.cs:60`), and
the three validators. Repository predicates in this codebase are covered only by integration testing,
which does not exist yet. That is carried debt, correctly not invented into this PR, and the
implementer was right to refuse to claim more for the RC7 change than fixture realism.

## Build, tests, and process integrity — all verified

| Check | Claimed | Observed |
|---|---|---|
| `dotnet build api/E3A.slnx --no-incremental` | 0 errors, 9 warnings, all `api/core-libraries` | **Confirmed.** `Build succeeded. 9 Warning(s) 0 Error(s)` — Core.Validation 2 x CS8602, Core.OTP 2 x CS8618, Core.Notifications 5 x CS8618. None in `api/E3A.*`. |
| `dotnet test api/E3A.slnx` | 614, up from 604 | **Confirmed.** `Failed: 0, Passed: 614, Skipped: 0, Total: 614`, re-run clean after every mutation was reverted. |

**Append-only exception used correctly.** `git diff` on `.process/teams/00-acceptance.md` and
`01-plan.md` shows **pure tail appends** — 18 and 14 added lines respectively, zero deletions, zero
modifications above them. Decision 1 (`00-acceptance.md:39-40`) and decision 10 (`01-plan.md:70`) are
textually intact; the corrections are dated 2026-08-29, are explicitly labelled as appended with the
decision above unchanged, state which rationale was falsified, and state why the decision itself still
stands. This is exactly the shape the exception was authorised for.

`03-review.md` and `03-review-r2.md` do not appear in `git status` — untouched, as required.

**Scope discipline confirmed.** `git status` shows no file under `api/core-libraries` (RC6 stays
escalated), no `IValidateOptions` / `ValidateOnStart` / options validation of any kind (RC5 stays
rejected), no migration, no `.resx`, no `AzureOptions` member, no Azure resource. Reusing
`ErrorCodes.EngineerSlugReserved` kept both resx files untouched, so no en/ar key drift.

**Postman.** Untouched and correctly so — this rework changed no endpoint, method, URL, auth mode or
body shape; it changed only which slug *values* a validator accepts.
`postman/e3a.postman_collection.json` still covers all team endpoints (`/api/teams`, `/mine`,
`/{teamId}`, `/{teamId}/members`, `/{teamId}/publish`, `/slug-availability`), and neither slug example
(`dive-backend-engineer`, `dotnet-product-squad`) is affected by the new rule. No stale, missing or
orphaned request.

## Docs sync — all three agree with the code

- `docs/plugin-spec.md:70-78` and `docs/implementation-plan.md:53` now state that republishing on its
  own adopts nothing, and that the owner must send a new `pinnedVersionId` to
  `PUT /api/teams/{teamId}/members` first. This matches `TeamMemberPinResolver.cs:47`, where an
  existing member with a null selection falls back to the **existing pin**, never to the engineer
  latest version. The deferred `team-compile-merge` target is kept named in both, so no not-yet-built
  work was deleted. Divergence closed.
- `docs/architecture.md:55-59` is narrowed to a build-failure path, which matches
  `ProcessPublishJobHandler.cs`: the zip uploads at `:60`, the pinned marketplace at `:69`, the
  terminal save at `:72`, so a throw in either of the last two genuinely leaves a public zip with the
  version still `Building`.
- `docs/implementation-plan.md:51` now carries the naming contract — engineer slugs may not begin
  `team-` — which is the divergence trigger under `.claude/rules/docs-sync.md` (naming/format
  contract). Present and accurate.

**On the stated-twice concern: not a finding.** The added sentence ends with "exactly as described
above", which *is* the cross-reference the implementer worried a reviewer would prefer. The narrowed
claim needs a counterweight in its own paragraph or it reads as "no public write ever", which was the
original defect. Leave it as written.

## Deviation 1 — Theory demoted to Fact: sound, not a workaround

The constraint is real. Because triage item 1 deliberately reuses `ErrorCodes.EngineerSlugReserved` to
avoid resx churn, a parameterised form of the new test would have a body byte-identical to the existing
`Validate_ShouldFail_WhenSlugIsReserved` — same command construction, same error-code assertion — which
trips Sonar **S4144** and, under `TreatWarningsAsErrors`, fails the build. The alternatives were worse:
folding `team-alpha` into the reserved-list theory would name the test after the wrong rule;
suppressing S4144 adds a pragma to dodge a real duplication signal; a distinct error code reintroduces
the resx churn and en/ar drift the triage explicitly avoided.

**No coverage was lost.** The only case a parameterised version would have added at validator level is
mixed case (`Team-Alpha`), and all three validators call `SlugGenerator.NormalizeTypedSlug` — which
lowercases (`SlugGenerator.cs:39-41`) — *before* `IsTeamNamespaced`. A validator-level mixed-case test
would therefore exercise `NormalizeTypedSlug`, not the new rule, and normalization already has its own
test (`Validate_ShouldPass_WhenSlugDiffersOnlyByCaseOrWhitespace`). Case-insensitivity is tested where
the rule actually lives, at `PluginNameTests.cs:19`. Correct call, correctly reasoned.

## Non-blocking observations

- `CreateEngineerValidator.cs:47`, `UpdateEngineerValidator.cs:50`,
  `CheckSlugAvailabilityQueryValidator.cs:47` — the new rule shares `ErrorCodes.EngineerSlugReserved`
  with the reserved-list rule above it, so the three tests cannot by assertion alone distinguish which
  rule fired. I closed that by mutation (all three fail when the predicate is neutralised), and the
  shared code is a deliberate, well-argued trade against resx churn. No action; recorded so a future
  reader does not have to re-derive it.
- `PluginName.cs:25` — the case-insensitive comparison is redundant at all three current call sites,
  since they pass an already-lowercased slug. Keeping it is the right default for a public member whose
  invariant should not depend on caller discipline. No action.
- Repository query predicates are unconstrained by any test in this repo (see RC7 above). Carried debt
  for whenever integration testing lands; not this PR to open.

## Verified claims from 07-coderabbit-rework.md

Every claim in the rework report that could be checked, I checked, and all of them held:

- All 8 implement items present; RC5 rejected and RC6 escalated with no code written for either.
- RC9 mutation results reproduced exactly, both halves, including the `Failed: 0` on the old fixture.
- RC7 analysis reproduced exactly, including `Failed: 0` with a deliberately wrong predicate.
- Build 0 errors / 9 warnings and test count 614 reproduced independently.
- The appends to `00-acceptance.md` and `01-plan.md` are appends; `03-review*.md` untouched.
- No core-libraries, migration, resx, Azure or Postman change.

Three separate mutations were run during this review. All were restored, and the working tree was
confirmed byte-identical to its pre-review state after each one, with a final full-suite pass at 614.

No blocking findings. Merge.
