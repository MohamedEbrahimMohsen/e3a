TRIAGE: 0 to implement, 8 rejected, 2 dev-decisions

# Stage 4 Triage — CodeRabbit on PR #3 (`engineer-slug`, commit `a6b953e`)

**Reviewer:** fresh triage agent, no memory of stages 1–3.
**Baseline re-verified independently before triaging:**
`dotnet build api/E3a.slnx` → **Build succeeded, 0 errors, 9 warnings** (all pre-existing, all in
`api/core-libraries/`: `Core.Validation` x2, `Core.OTP` x2, `Core.Notifications` x5).
`dotnet test api/E3A.Tests/E3A.Tests.csproj` → **236 passed, 0 failed, 0 skipped.**
Matches the recorded baseline exactly. Nothing in this triage changes it.

**Nothing is implemented in this cycle.** That is an unusual outcome and it is deliberate: of the
10 actionable items CodeRabbit raised, **four rest on premises that are factually false in this
repository** (RC4, RC5, RC6, and half of RC3), **four target `.process/` audit artifacts**
(RC1, RC2, PC1-N1, PC1-N2), one contradicts the house style (PC1-DOCSTRING), and one asks to
overturn a plan-gate decision with no reachable defect behind it (RC7). Each rebuttal below cites
the file and line I read.

---

## LOUD DOWNGRADE — CodeRabbit's "High" merge-risk banner is rejected

CodeRabbit did **not** label any comment Critical, but its walkthrough banner asserts:

> "the current head can still permit duplicate identities, invalid or reserved slugs, and
> post-publication slug changes through concurrent requests, incomplete global uniqueness checks,
> missing deployment settings, or unguarded domain mutation."

I am downgrading that banner to non-blocking. **The orchestrator must surface this to the dev with
a veto option.** All four of its clauses are contradicted by code I read:

| Banner clause | Evidence against |
|---|---|
| "duplicate identities ... through concurrent requests" | A unique filtered index exists: `api/E3A.Infrastructure/Data/Context/AppDbContext.cs:38` and `api/E3A.Infrastructure/Data/Migrations/20260827082800_initial.cs:365-370` (`unique: true, filter: "[IsDeleted] = 0"`). Duplicates **cannot** persist. |
| "incomplete global uniqueness checks" | The "other" namespace CodeRabbit means is teams. **No `Team` entity, table, repository or DbSet exists anywhere in the repo** — `find api -iname "*team*"` returns nothing. |
| "missing deployment settings" | `api/E3A.Api/appsettings.json:38,42` carries `"SlugMinLength": 3` and the full 15-entry `"ReservedSlugs"` array. The file is gitignored, so CodeRabbit cannot see it. |
| "post-publication slug changes through ... unguarded domain mutation" | `ChangeSlug` has exactly one non-test caller — `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs:43` — reached only after the freeze guard at `:66-69`. No reachable path exists. |

---

## IMPLEMENT

**None.** No item survived verification as an in-slice change.

---

## DEV-DECISION (escalated, not decided; no code change this cycle)

### D1 — Should localized validation messages carry the *configured* limits? (from RC3)

**CodeRabbit's underlying observation is correct and I reproduced it.**
`api/core-libraries/Core.CQRS/Behaviours/ValidationBehaviour.cs:29` calls
`localizer.GetMessage(error.ErrorCode, error.ErrorMessage)`, and
`api/core-libraries/Core.Localization/Localizer.cs:15-16` returns the resx value whenever the key
resolves, using the validator's dynamic message only as a *fallback*. So the interpolated message
built at `CheckSlugAvailabilityQueryValidator.cs:21` (`$"...at least {options.SlugMinLength}..."`)
is discarded, and the client sees the hardcoded "3" from `Messages.en.resx:55`.

**Why I did not implement it in this slice:**

1. **It is the established repo-wide convention, not a slug regression.** Four pre-existing entries
   hardcode a configured limit in exactly the same way: `Messages.en.resx:31` ("100" from
   `DisplayNameMaxLength`), `:37` ("500" from `DescriptionMaxLength`), `:46` ("30" from
   `TagMaxLength`), and `:40` ("10" from `MaxTags`). Changing only the two slug keys would leave the
   resource file internally inconsistent. Skill rule: mirror, don't modernize.
2. **CodeRabbit's proposed alternative does not work here.** It suggests "parameterized English and
   Arabic resources". `Localizer.GetMessage` only substitutes `{placeholder}` tokens when a
   `context` dictionary is passed (`Localizer.cs:19-25`), and `ValidationBehaviour.cs:29` passes
   none. A parameterized resx would render `{Min}` literally to the user. A real fix therefore
   requires editing `api/core-libraries/Core.CQRS/` — vendored, shared, and deliberately outside
   this slice's blast radius on the same grounds as plan Decision #9.

**Question for the dev:** do you want a cross-cutting change to `Core.CQRS.ValidationBehaviour` to
forward FluentValidation placeholder values as the `context` dictionary, then parameterize all six
affected `ar`/`en` message pairs? That is its own slice. Until then, changing a configured limit in
`appsettings.json` will produce a message that states the old number — in Arabic and English, for
slug **and** for display name, description and tags alike.

### D2 — Should `EngineersOptions` fail closed at startup? (from RC6)

**CodeRabbit's literal ask is already satisfied** — see the REJECT entry for RC6 below.
**The residual concern is real and is new to this slice**, and it was already logged twice
internally as a non-blocking follow-up (`03-review.md:52-53`, `03-review-r2.md:142-145`). I am
escalating rather than deciding it because the only clean fix changes application startup
behaviour.

The asymmetry is the point. Every pre-existing `EngineersOptions` property fails **closed** when
absent — `SlugMaxLength = 0`, `DisplayNameMaxLength = 0` and `MaxEngineersPerCreator = 0` all
reject everything, loudly. The two properties this slice adds fail **open**
(`api/E3A.Application/Options/EngineersOptions.cs:12,16`): `SlugMinLength` defaults to `0`, so a
1-character slug passes `CheckSlugAvailabilityQueryValidator.cs:20`; `ReservedSlugs` defaults to
`[]`, so `admin` passes `:38`. On a surface whose whole point is that the slug becomes a permanent
plugin identity, silent degradation is the wrong failure mode.

**Question for the dev:** add
`services.AddOptions<EngineersOptions>().Bind(...).Validate(...).ValidateOnStart()` in
`api/E3A.Application/DependencyInjection.cs:14`? It is ~3 lines of stock .NET with no new
abstraction, but (a) the app would refuse to start on an under-provisioned environment, and
(b) consistency argues for applying it to `UploadsOptions`, `AzureOptions` and `CatalogOptions`
at `DependencyInjection.cs:15-17` too — which makes it a small slice of its own, not a triage fix.

**Deployment note that must not be lost:** `api/E3A.Api/appsettings.json` is gitignored
(constitution section 2, deploy-time only). Whoever provisions any environment other than this
machine must add `SlugMinLength` and `ReservedSlugs` by hand.

---

## REJECT

### R1 — RC1 · plan heading says `6–11`, table lists `6–13` (Minor)

**The claim is factually correct.** `.process/engineer-slug/01-plan.md:166` reads
`### 6–11. Test files — see Test plan.` and the table immediately below it numbers rows 6 through
13.

**Rejected anyway.** `.process/` is the pipeline's immutable audit trail — a frozen record of what
was planned, implemented and reviewed *at the time*. Editing the plan now would falsify it: the
implementer worked from this exact text, read the 8-row table correctly, and created all 13 files
(`02-implementation.md` "Files created" lists 13; I counted 13 rows). The heading typo demonstrably
misled no one. A comparable comment was rejected on this same ground on PR #2. Zero runtime,
schema, contract or test impact.

### R2 — RC2 · MD040, add language identifiers to fenced blocks (Minor)

Same audit-trail ground as R1, plus: **the repo has no markdownlint configuration.** I searched for
`.markdownlint*` and `*markdownlint*` at the repo root and two levels down — nothing. MD040 is
CodeRabbit's own imported default, not a rule this project adopted. Rewriting seven fences in a
frozen record to satisfy a linter the repo does not run is churn against the audit trail.

### R3 — RC4 · "Check availability across engineers and teams" (Major, "Heavy lift")

**The premise is false. There are no teams.** I verified directly:

- `find api -iname "*team*"` (excluding `bin`/`obj`) returns **no results**.
- `api/E3A.Domain/` contains exactly two aggregate folders: `Engineers` and `Identity`.
- No `Team` entity, no `Teams` table, no DbSet, no repository, no migration.

CodeRabbit asserts the handler "can therefore approve a slug already used by a team". There is no
team, no team slug, and no code path that could produce one. This is exactly the failure mode the
triage rules warn about: asserting behaviour the code plainly does not have.

The *forward-looking* half — that `e3a-{slug}` will need a shared namespace once teams ship — is
already recorded, ahead of CodeRabbit, as **DEV-DECISION #1 in `01-plan.md`** ("Engineer/team slug
collision", with the three options enumerated and teams noted as P5). CodeRabbit adds nothing to
it. Building a cross-table registry for a table that does not exist is speculative architecture.

### R4 — RC5 · "Make slug allocation atomic" (Major, "Heavy lift")

**The load-bearing claim is false.** CodeRabbit writes: *"Without that constraint, duplicate plugin
identities can persist."* The constraint exists and has existed since the initial migration:

- `api/E3A.Infrastructure/Data/Context/AppDbContext.cs:38` —
  `builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");`
- `api/E3A.Infrastructure/Data/Migrations/20260827082800_initial.cs:365-370` —
  `unique: true, filter: "[IsDeleted] = 0"`

Duplicate slugs cannot persist. The residual is a narrow TOCTOU window in which the loser of a race
receives a `DbUpdateException` instead of an auto-resolved candidate.

Rejected for three reasons:

1. **It asks to overturn a codified house pattern.** The `Is...ExistsAsync` + suffix-loop shape is
   `SKILL.md` section 8.3's own DO example, reproduced almost verbatim in
   `EngineerSlugResolver.cs:11-24`. Section 8.3 is also explicit that an auto-resolvable collision
   must never surface as a Conflict. CodeRabbit's retry-on-uniqueness-violation design requires
   catching a persistence exception in or around a handler; `SKILL.md` section 9 (Application)
   states handlers carry **no `try`/`catch`**. The suggestion cannot be implemented without
   violating two house rules.
2. **It is pre-existing, not introduced here.** The identical loop shipped in `main` inside
   `CreateEngineerHandler.GenerateUniqueSlugAsync`; this slice extracted it unchanged (plan
   Decision #7) and only added `.TrimEnd('-')`.
3. **Scope.** Two creators typing the same slug in the same millisecond is exactly what the new
   availability endpoint makes rare, per the accepted rule set (`00-acceptance.md:61`).

### R5 — RC7 · "Enforce the frozen-slug invariant in `Engineer.ChangeSlug`" (Major)

**No reachable defect.** `grep -rn "ChangeSlug" api --include=*.cs` returns exactly one non-test
production caller: `UpdateEngineerHandler.cs:43`, and it is reached only after
`ResolveSlugChangeAsync` throws `BusinessRuleViolationCoreException(ErrorCodes.EngineerSlugFrozen)`
at `UpdateEngineerHandler.cs:66-69` for any engineer with `LatestVersionId != null`. CodeRabbit's
"a direct caller can invoke this method after `MarkPublished`" describes a caller that does not
exist.

**This was decided at the plan gate.** `01-plan.md` Decision #5 puts the freeze guard in the
handler, and its stated reason holds up: `BusinessRuleViolationException` (the domain-only type
`SKILL.md` section 4.8 asks for) **does not exist in this repository** — I grepped; only
`BusinessRuleViolationCoreException` in `Core.Errors` exists. A domain-side guard would have to
hardcode the literal `"ENGINEER_SLUG_FROZEN"` inside `E3A.Domain`, because
`E3A.Application.Exceptions.ErrorCodes` is not visible from there — which breaks the single-source
error-code rule in `SKILL.md` section 9. (For accuracy: `E3A.Domain` *can* see `Core.Errors`
transitively via `Core.DDD.csproj`, so only the error-code half of the plan's reasoning binds — but
it binds.) The repo precedent cited in the plan is real: `GetImportManifestQueryHandler.cs:35-37`
throws its business-rule exception from the handler on an entity-state check.

**Consistency clinches it.** Every other mutator on this aggregate is an unguarded setter with the
same theoretical exposure — `MarkPublished` (`Engineer.cs:52`), `RecordInstallCount` (`:59`),
`ReplaceDraftManifest` (`:65`), `Delete` (`:71`). Guarding only `ChangeSlug` would make the
aggregate inconsistent for zero behavioural gain. Decision #5 is already on the dev's proxy veto
list, so the dev already holds the lever on this.

### R6 — PC1 nitpick · MD028, blank line inside the `00-acceptance.md` blockquote (Trivial)

`.process/` audit artifact (see R1) and no markdownlint config in the repo (see R2). Additionally:
the blank line at `00-acceptance.md:74` separates **two distinct dev quotes** captured verbatim at
different moments. Merging them would misrepresent the record of what the dev actually said, which
is the one thing an acceptance document exists to preserve.

### R7 — PC1 nitpick · MD038, spaces inside code spans at `01-plan.md:396-397` (Trivial)

**Rejected, and the suggested fix would be actively harmful.** Those two table rows carry the
replacement text for `docs/plugin-spec.md` lines 87 and 94, and the code span on each begins with
literal leading spaces before `"name":` and `"url":`. Those leading spaces are **the JSON
indentation the doc edit was required to reproduce** in `docs/plugin-spec.md`. CodeRabbit asks to
"move those spaces outside the delimiters while preserving the displayed JSON content" — moving
them outside is precisely what destroys the displayed content. Plus R1 and R2 both apply.

### R8 — PC1 pre-merge check · "Docstring coverage is 0.00%, threshold 80.00%"

**Rejected outright; do not action the CodeRabbit autofix checkbox.** `SKILL.md` treats
comment-free code as an absolute house rule — comments are permitted only as WHY comments on hidden
invariants. This slice already spends its two sanctioned WHY comments deliberately, at
`EngineerSlugResolver.cs:16` and `:22`, both pre-authorized by the plan. Generating docstrings for
98 functions would violate the style rule the internal reviewer gates on, across 34 files, for a
threshold this repo never adopted. CodeRabbit's own passed checks confirm the PR title, description
and scope are fine; this one check is a generic default.

---

## Verification summary

| Item | Severity claimed | Claim verified? | Disposition |
|------|------------------|-----------------|-------------|
| RC1 | Minor | Yes — heading typo is real (`01-plan.md:166`) | REJECT (audit trail immutable) |
| RC2 | Minor | Partly — no markdownlint config in repo | REJECT (audit trail + no such repo rule) |
| RC3 | Minor | Yes — resx wins over dynamic message | DEV-DECISION D1 (repo-wide, needs Core change) |
| RC4 | Major | **No — no Team entity exists in the repo** | REJECT (false premise; = plan DEV-DECISION #1) |
| RC5 | Major | **No — unique filtered index exists** (`AppDbContext.cs:38`) | REJECT (false premise; 8.3 house pattern) |
| RC6 | Major | **No — config present at `appsettings.json:38,42`** | DEV-DECISION D2 (residual fail-open only) |
| RC7 | Major | **No — no unguarded call path exists** | REJECT (plan Decision #5, gate-approved) |
| PC1-N1 | Trivial | Yes — blank line exists | REJECT (audit trail; separates two quotes) |
| PC1-N2 | Trivial | Yes — spaces exist | REJECT (spaces are significant JSON indentation) |
| PC1-Docstrings | pre-merge check | Yes — 0% coverage | REJECT (violates skill no-comments rule) |

**Recommendation to the orchestrator:** PR #3 is mergeable as-is. Build and tests re-confirmed green
on the working tree at `a6b953e` (0 errors, 9 pre-existing warnings, 236/236). Two items (D1, D2)
go on the dev's return list alongside the existing veto items; neither blocks this slice. The
`.gitignore:20` `publish/` defect and the `Core.Utilities.IGenerator` trailing-separator bug remain
separately spun-off work and were correctly not raised by CodeRabbit.
