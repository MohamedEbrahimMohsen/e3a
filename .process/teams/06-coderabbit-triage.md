TRIAGE: 8 to implement, 1 rejected, 1 dev-decisions

# CodeRabbit triage — PR #7 (teams)

11 inline comments (RC1–RC11) → **8 implement items** (RC3 and RC4 are one defect, one item; RC9's two
sites are one item), **1 rejected** (RC5), **1 escalated as a dev decision** (RC6).

**Nothing CodeRabbit marked Major was downgraded.** Both Majors (RC3, RC4) are upheld, and RC4 is
**upgraded to Critical** — it is a reachable cross-creator plugin-name hijack that falsifies the stated
rationale of acceptance decision 1 and plan decision 10. RC3's *diagnosis* is upheld; RC3's *proposed
fix* is rejected as ineffective (see item 1).

Baseline reproduced in this worktree before triage: `dotnet test api/E3A.slnx` gives **Failed: 0,
Passed: 604**, and the 9 warnings are all in `api/core-libraries` (Core.Validation 2, Core.OTP 2,
Core.Notifications 5). No Azure resource is involved in any item below.

---

## IMPLEMENT

### 1. **CRITICAL** — the `e3a-` / `e3a-team-` namespaces overlap; one creator can hijack another's install name (RC4, RC3)

**CodeRabbit is right, and it is worse than it says.**

**Reachable today, verified from the code, not from the claim:**

- `api/E3A.Application/Publishing/Shared/PluginName.cs:13,18` — `ForEngineer("team-alpha")` returns
  `e3a-team-alpha`; `ForTeam("alpha")` returns `e3a-team-alpha`. Identical strings.
- Nothing rejects an engineer slug beginning `team-`. The format gate is
  `api/E3A.Domain/SharedKernel/SlugGenerator.cs:11`, the kebab-case regex, which `team-alpha` matches.
  The reserved gate is `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs:38`
  (and `UpdateEngineerValidator.cs:41`, `CheckSlugAvailabilityQueryValidator.cs:38`), an **exact-match**
  `options.ReservedSlugs.Contains(...)` over the 15 reserved words at
  `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs:59`. `team-alpha` is not among them, and no prefix
  rule exists anywhere.
- Team slug uniqueness is scoped to the `Teams` table only (plan decision 10, `01-plan.md:70`), engineer
  slug uniqueness to `Engineers`. The two never consult each other — deliberately, because collision was
  believed impossible.

**Consequence, traced end to end.** Engineer `team-alpha` (creator A) and team `alpha` (creator B) both
publish `1.0.0`:

- **Zip — silent artifact adoption, not a blocked overwrite.** `PublishBlobPaths.Zip`
  (`PublishBlobPaths.cs:22-25`) gives `z/e3a-team-alpha/1.0.0.zip` for both. The second publish hits the
  existence check at
  `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:55-61`, finds the blob,
  **skips the upload**, and then runs `version.MarkPublished(zipBlobPath, zipped.Sha256,
  zipped.SizeBytes)` at `:63` with its own locally computed sha256 and size. The `overwrite: false`
  guard never fires, because the upload is never attempted. Result: creator B's team version is recorded
  `Published`, its row stores a sha256 that does **not** match the bytes served at that URL, and
  `/plugin install e3a-team-alpha@e3a` installs creator A's engineer plugin. No exception, no failed
  version, no log.
- **Pinned marketplace — overwritten.** `PublishBlobPaths.PinnedMarketplace` gives
  `m/e3a-team-alpha/1.0.0/marketplace.json` for both, uploaded with `overwrite: true`
  (`ProcessPublishJobHandler.cs:69`). The later publish replaces the earlier one, so the pinned metadata
  describes one plugin while the zip beside it holds the other's content.
- **Root `marketplace.json` — duplicate identities.**
  `api/E3A.Application/Publishing/RegenerateMarketplace/RegenerateMarketplaceHandler.cs:23-26`
  concatenates the two collectors and orders by `Name`; nothing de-duplicates, so two entries ship with
  `"name": "e3a-team-alpha"` and Claude Code resolves whichever it reaches first.

It is not accidental-only: an attacker can deliberately register the engineer slug `team-{victimTeam}`
to shadow an existing published team, or a team slug `{x}` to shadow an existing engineer `team-{x}`.
Cross-owner publishing is explicitly allowed (acceptance decision 7) and slugs are globally unique per
table, so nothing stops it.

**This falsifies the plan's own reasoning, so it is not a preference.** `00-acceptance.md:39-40`
("collision between a team slug and an engineer slug becomes structurally impossible"), `01-plan.md:70`
(decision 10 leans on that to scope team uniqueness to the Teams table), the WHY comment at
`PluginName.cs:8`, and `pr-body.md:19` all assert an invariant the code does not hold. Decision 1
stays — `e3a-team-{slug}` is the dev's locked answer — but the namespace has to actually be a namespace.

**RC3's proposed fix does not work; do not apply it.** Adding `"team-"` to
`EngineersOptions.ReservedSlugs` rejects only the literal slug `team-`, because the rule is an
exact-match `Contains` (`CreateEngineerValidator.cs:38`) — and `team-` is not even a valid slug format,
so the entry would be dead. RC4's second half (a cross-repository "does this team's name already belong
to an engineer" lookup) is also rejected: it is heavier, it re-couples the two areas that decision 1
deliberately decoupled, and it is unnecessary — see below.

**Smallest correct fix: reserve the `team-` prefix on the engineer side only.** A team slug can only
ever produce `e3a-team-{x}`; the sole way an engineer can produce that string is a slug starting
`team-`. Close that and the two namespaces are disjoint in both directions, with no cross-area query.

1. `api/E3A.Application/Publishing/Shared/PluginName.cs` — keep `TeamSegment` private and add
   `public static bool IsTeamNamespaced(string slug)` returning
   `slug.StartsWith(TeamSegment, StringComparison.OrdinalIgnoreCase)`. One source of truth shared with
   `ForTeam`, so the guard and the namer can never drift. This is a true invariant, so it stays a named
   constant with the existing WHY comment (constitution 0.3) — not an option value.
2. Add one rule to each of the three engineer slug validators, in the shape of the neighbouring reserved
   rule (`CreateEngineerValidator.cs:37-41`, `UpdateEngineerValidator.cs:40-44`,
   `CheckSlugAvailabilityQueryValidator.cs:37-41`):
   `.Must(slug => !PluginName.IsTeamNamespaced(SlugGenerator.NormalizeTypedSlug(slug)))`, with
   **`ErrorCodes.EngineerSlugReserved`** and the existing "is reserved." message. Reusing the existing
   code keeps `Messages.en.resx` and `Messages.ar.resx` at 87 keys each — no resx churn, no en/ar drift
   risk. Keep each rule's existing `.When(...)` guard.
3. Tests — one failing case per validator asserting `ErrorCodes.EngineerSlugReserved` for slug
   `team-alpha`, in `CreateEngineerSlugValidatorTests`, `UpdateEngineerSlugValidatorTests` and
   `CheckSlugAvailabilityQueryValidatorTests`; plus a true/false pair in `PluginNameTests` if
   `IsTeamNamespaced` is added as a new public member.
4. Docs — a naming/format contract change is a divergence trigger under `.claude/rules/docs-sync.md`,
   and `docs/implementation-plan.md` owns "Naming". Extend `docs/implementation-plan.md:51` to state
   that engineer slugs may not begin `team-`, which is reserved for the team namespace, and that this is
   what makes the two plugin namespaces disjoint.

No migration and no data fix are needed — nothing is deployed (`00-acceptance.md:61`), so no engineer
holds a `team-` slug today.

### 2. Two team-version fixtures make tests pass for the wrong reason (RC9)

**Site b is the real one, and it matters more than its Minor label.**
`api/E3A.Tests/Teams/SetTeamMembers/SetTeamMembersHandlerGuardTests.cs:100-108`,
`Handle_ShouldThrowBusinessRuleViolation_WhenPinnedVersionIsATeamVersion`, builds its fixture with
`ItemVersionFactory.QueuedTeam(member.Engineer.Id)` (`:104`) — status `Queued`. The guard it targets is
a four-way `OR` at `api/E3A.Application/Teams/Shared/TeamMemberPinResolver.cs:27`:

    if (version == null || version.ItemType != ItemType.Engineer || version.ItemId != engineer.Id || version.Status != ItemVersionStatus.Published)

`Status != Published` is already true for this fixture, so **deleting the
`version.ItemType != ItemType.Engineer` clause leaves this test green.** The test named for the ItemType
rule proves nothing about the ItemType rule. Fix: make the fixture a **published** team version, so
`ItemId == engineer.Id` and `Status == Published` both hold and only the ItemType clause can fire.

**Site a** — `api/E3A.Tests/Teams/PublishTeam/PublishTeamHandlerTests.cs:69-70` stubs `latest` as a
`Queued` team version. That state is unreachable in production: a `Queued` latest version would have
been caught by the in-progress conflict check in `PublishTeamHandler` and thrown
`PublishAlreadyInProgress`; the test only gets past it because the substitute is matched on the presence
of an `orderBy` argument (`:70`). The increment assertions do still bite, so this is fixture realism
rather than a wrong-reason pass — fix it in the same edit.

Add `ItemVersionFactory.PublishedTeam(Guid teamId, ...)` beside `QueuedTeam`
(`api/E3A.Tests/Publishing/Shared/ItemVersionFactory.cs:18-21`), mirroring `Published` (`:31-37`), and
use it at both sites and in item 3.

### 3. `RegenerateMarketplaceTeamTests` publishes teams with engineer versions (RC7)

`api/E3A.Tests/Publishing/RegenerateMarketplace/RegenerateMarketplaceTeamTests.cs:95` —
`ItemVersionFactory.Published(team.Id, ...)` creates an **`ItemType.Engineer`** version whose `ItemId`
is a team id (`ItemVersionFactory.cs:31-37` calls `Queued`, which hard-codes `ItemType.Engineer`). Every
"published team" in this class is therefore persisted state that cannot exist.

It does not currently mask a defect — `PublishedTeamCollector.cs:36` filters on id and status only, and
the substitute ignores the predicate anyway — but it is exactly the fixture that would keep passing if
an `ItemType` filter were later added wrong or dropped. Use the new `ItemVersionFactory.PublishedTeam`
from item 2, keeping the same `zipBlobPath`.

### 4. The team slug-suggestion test accepts a malformed suggestion (RC8)

`api/E3A.Tests/Teams/CheckTeamSlugAvailability/CheckTeamSlugAvailabilityQueryHandlerTests.cs:53-54`
asserts only `NotBeNullOrEmpty` and `NotBe(TypedSlug)`. The stub at `:48` deliberately returns
`"dotnet-product-squad-ab12-"` **with a trailing hyphen**, because the behaviour under test is the
`TrimEnd('-')` at `api/E3A.Application/Shared/SlugResolver.cs:22` (the documented Core `IGenerator`
quirk at `:21`). Delete that `TrimEnd` and this test still passes.

The engineer mirror already does it right —
`api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandlerTests.cs:54` asserts
`result.SuggestedSlug.Should().Be(SuffixedSlug)`. This is the "mirror, don't modernize" case
(constitution 0.2) and the plan's own instruction that the team handler mirrors the engineer one
(`01-plan.md:193`). Replace both assertions with `result.SuggestedSlug.Should().Be(SuffixedSlug);`.

### 5. Docs say republishing adopts newer member versions; the code pins explicitly (RC11) — divergence

`docs/plugin-spec.md:73-74` — "the team owner adopts newer member versions by republishing the team" —
and the same claim at `docs/implementation-plan.md:53` ("republish the team to adopt newer member
versions"). The code does not do this:

- `api/E3A.Application/Teams/Shared/TeamMemberPinResolver.cs:47` resolves
  `selection.PinnedVersionId ?? existingPin ?? engineer.LatestVersionId` — for a member who is
  **already** on the team, a null pin falls back to the **existing pin**, never to the engineer's newer
  version. That is plan decision 5 (`01-plan.md:65`) and it is deliberate: "re-pinning stays an explicit
  act".
- `PublishTeamHandler` freezes the current roster into `FrozenManifestJson` and resolves nothing.

So republishing alone changes nothing about member versions. Adoption requires the owner to first call
`PUT /api/teams/{teamId}/members` (`api/E3A.Api/Controllers/Teams/TeamsController.cs:56`) with the new
`pinnedVersionId`, and then publish. Automatic adoption is the deferred `team-compile-merge` flow
(`00-acceptance.md:58`, `01-plan.md:52`).

Under `.claude/rules/docs-sync.md` this is divergence, not incompleteness: doc and code give two
different answers to "what does republishing do to member versions". Correct both lines to state the
explicit re-pin-then-republish workflow, and keep the automatic prompt named as deferred — do **not**
delete the deferred target.

### 6. `docs/architecture.md` overpromises the failure guarantee (RC10, first half only)

`docs/architecture.md:55-57` — "**No write to the public container happens on any failure path**, for
either item type: every build failure returns before the zip is created."

The justification clause is true; the claim above it is broader than its own justification and broader
than the code. `ProcessPublishJobHandler.cs` uploads the public zip at `:60`, the pinned marketplace at
`:69`, then persists at `:72`. If the pinned-marketplace upload or the terminal `SaveChangesAsync`
throws, the version stays `Building` in the database while a zip already sits in the public container —
a failure path with a public write. The doc says exactly that two paragraphs earlier (`:50-52`, "a
failed artefact write leaves the version `Building`"), so as written the section answers one question
two ways.

Fix: scope the sentence to build failures (for example "no write to the public container happens on any
**build**-failure path"), leaving `:50-52` as the statement of what a post-zip failure leaves behind.
One clause; no code change.

**RC10's second half is rejected** — see Rejected.

### 7. Missing fence language in `pr-body.md` (RC2)

`.process/teams/pr-body.md:39` opens a fenced block with no language identifier (markdownlint MD040).
`pr-body.md` is the live PR description, so unlike the closed pipeline artifacts it may be edited. Use
`text` — the block is pseudocode, not valid JSON.

### 8. `pr-body.md`'s pinning argument overclaims (RC1, PR-body half only)

`.process/teams/pr-body.md:12` — "`TeamPublishBuilder` takes **no `IEngineerRepository`**. It cannot
read live member state even by accident, because it has no way to."

The first sentence is true and independently verified (`TeamPublishBuilder.cs:15`). The second
overstates: the builder does take `ITeamRepository` and loads the team at `:17`, so live `TeamMember`
rows are reachable in principle — today that call passes no `Include` and `team.Members` is never read,
but the argument as phrased is not the structural guarantee it claims. What the missing
`IEngineerRepository` actually rules out is reading a member engineer's *current* `LatestVersionId`.

The real proof is already in the bullets on either side (`:11` roster read from `FrozenManifestJson`,
`:13` content read from the immutable `snapshots/{pinnedVersionId}/` prefix), matching the code at
`TeamPublishBuilder.cs:24,43,57`. Soften `:12` to that — dependency absence as supporting evidence, the
pinned data flow as the proof.

**RC1's other half is rejected** — see Rejected.

---

## REJECTED

### RC5 — validate `SlugMaxLength > SlugSuffixSize + 1`

The mechanism is real: with `SlugMaxLength <= SlugSuffixSize + 1`, `SlugResolver.cs:16` passes a
non-positive length into `SlugGenerator.Normalize`, and `SlugGenerator.cs:33` (`slug[..maxLength]`)
throws on a negative one. But:

- It is reachable **only** through an invalid deployment configuration. Shipped values are
  `SlugMaxLength = 100` and `SlugSuffixSize = 4` for both areas (`01-plan.md:166`,
  `api/E3A.Tests/Teams/Shared/TeamFactory.cs:65-73`, `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs:51-59`).
- The code is **pre-existing and moved verbatim**: plan decision 9 (`01-plan.md:69`) moves
  `EngineerSlugResolver` to `SlugResolver` with the body unchanged, precisely so the suffix-length
  invariant is not duplicated. This slice neither introduced the exposure nor widened it.
- There is no options-validation mechanism in this repo to extend — no `IValidateOptions`, no
  `ValidateDataAnnotations`, no `ValidateOnStart` anywhere. Introducing one for two option classes
  inside a Stage 4 triage would be a new cross-cutting pattern chosen by a review bot rather than by the
  plan, and would leave the other option classes inconsistent.

Recorded as carried debt for a config-governance slice — the same place the schema-drift exposure ruled
on at `03-review.md:189-201` belongs. Not this PR.

### RC10, second half — "exclude root `marketplace.json` from the before-persistence claim"

Rejected. `docs/architecture.md:50` concerns the publish job's own artefacts, and both of them — the zip
(`ProcessPublishJobHandler.cs:60`) and the pinned marketplace (`:69`) — genuinely precede the terminal
save at `:72`. The root document is written by a **separate** command dispatched after the publish job
completes (`api/E3A.Jobs/Functions/ProcessPublishJobFunction.cs:19-20`), and the pipeline sequence at
`docs/architecture.md:33-38` already lists "regenerate the root `marketplace.json`" last, after "persist
Published". The doc is already correct on this point.

### RC1, first half — edit `.process/teams/03-review.md:95-98`

Rejected on process, not substance. `03-review.md` is a **closed, append-only pipeline artifact**; the
standing ruling in this repo is that corrections go in the current document, never retroactively into a
closed one. The substance is captured in implement item 8 and here. Note also that the review's own
wording is narrower than the PR body's and is not wrong on its face: its load-bearing evidence at
`03-review.md:98` is already the `FrozenManifestJson` data flow, with the dependency list as support.

---

## DEV-DECISION (escalated)

### RC6 — `Core.DDD.Entities.Entity.SoftDelete()` discards the deletion timestamp

CodeRabbit's reading of the code is correct. `api/core-libraries/Core.DDD/Entities/Entity.cs:21-25` sets
`IsDeleted = true` and **`DeletedAt = null`**, so `Team.Delete()` (`api/E3A.Domain/Teams/Team.cs:68-73`)
— and `Engineer.Delete()`, and every other aggregate on the template — loses when the row was deleted.

It is escalated rather than implemented because it is neither this slice's defect nor this slice's code:

- `api/core-libraries` is **vendored** (`docs/constitution.md:3`). Changing it changes behaviour for
  every aggregate in the product, and for every other repo built on the same template.
- The behaviour is pre-existing on `main`; `Team.Delete()` merely calls the same helper
  `Engineer.Delete()` has always called. Teams introduced nothing here.
- Nothing in e3a reads `DeletedAt` today — it exists as a column on every table
  (`AppDbContextModelSnapshot.cs`) and as a property, with no query, report or retention job consuming
  it. So there is no current wrong answer, only a fact that is not being recorded.

**Question for the dev:** fix `Entity.SoftDelete()` in the vendored `Core.DDD` to stamp
`DateTimeOffset.UtcNow` (one line, benefits every aggregate, but it is a change to shared vendored code
and should carry its own test), or leave it and accept that `DeletedAt` is unused? The e3a-local
alternative — stamping it in each aggregate's own `Delete()` — is explicitly **not** recommended: it
would diverge from the template and leave two soft-delete behaviours in one codebase.

---

## Expected state after the fixes

| Check | Before | After |
|---|---|---|
| `dotnet build api/E3A.slnx --no-incremental` | 0 errors, 9 warnings (all `core-libraries`) | unchanged: 0 / 9 |
| `dotnet test api/E3A.slnx` | Failed: 0, Passed: 604 | Failed: 0, Passed: **607-609** |

Item 1 adds 3 validator tests, plus a 2-case `PluginNameTests` pair if `IsTeamNamespaced` is added as a
new public member. Items 2, 3 and 4 edit fixtures and assertions in place and add one test-factory
method, so they move no count. Items 5 to 8 are documentation and PR-body text.

Production files touched: `PluginName.cs`, `CreateEngineerValidator.cs`, `UpdateEngineerValidator.cs`,
`CheckSlugAvailabilityQueryValidator.cs`. No resx change (item 1 reuses `EngineerSlugReserved`), no
migration, no `AzureOptions` member, **no Azure resource**.

Docs touched: `docs/implementation-plan.md` (items 1 and 5), `docs/plugin-spec.md` (item 5),
`docs/architecture.md` (item 6), `.process/teams/pr-body.md` (items 7 and 8). `03-review.md` and
`03-review-r2.md` stay closed and untouched.
