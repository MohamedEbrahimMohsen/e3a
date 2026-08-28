VERDICT: CHANGES_REQUESTED

# Review — Creator-Typed Engineer Slug

One blocking finding. It is a docs-sync divergence, not a code defect: the code, the tests, the
Postman collection and all three `/docs` files are correct and internally consistent.
`README.md` was missed.

## Blocking

### 1. `README.md` still documents the superseded plugin-name contract

**Where:** `README.md:9`
**Rule:** `.claude/rules/docs-sync.md` — "Policy changes: … naming/format contracts"; plan Decision #14
**Problem:** The repo's front-page install block reads `/plugin install e3a-<creator>-<engineer>@e3a`.
That is the `e3a-{githublogin}-{item-slug}` scheme this slice deletes. Code and doc now give two
different answers to the same question — what is an engineer's plugin name?

- `docs/plugin-spec.md:11` — `e3a-{slug}`, "GitHub login is no longer part of the plugin name"
- `docs/implementation-plan.md:44` — `e3a-{slug}`
- `docs/design-prompt.md:16` — `/plugin install e3a-mmohsen@e3a`
- `README.md:9` — `e3a-<creator>-<engineer>`  <-- stale

This is divergence, not incompleteness. The README is not describing unbuilt work; it states a
naming contract that this change redefined, so the old form is now wrong rather than merely
ahead of the code.

Plan Decision #14 states the governing principle explicitly — docs-sync "classes naming/format-contract
changes as blocking divergence regardless of which doc holds them" — and then applies it only inside
`docs/`. The rule's own scope line is "All docs live in `/docs` only (plus root README.md)", so
`README.md` is in scope. The implementer's verification grep
(`grep -rn "githublogin\|e3a-mohamed-dive-backend-engineer" docs/`) missed it twice over: the pattern
does not match the `<creator>` form, and the path excludes the repo root.

**Failure:** A new contributor follows README.md and runs
`/plugin install e3a-mohamed-dive-backend-engineer@e3a`. No such plugin name can exist after this
change; every published engineer is `e3a-{slug}`. The documented command cannot resolve.
**Fix:** One line. `README.md:9` becomes `/plugin install e3a-<slug>@e3a` (or `e3a-mmohsen@e3a`, to
match the example already used in `docs/design-prompt.md:16` and `docs/plugin-spec.md:87`). Nothing
else in README.md is in this change's blast radius — the stale "isolated Azure Functions" row at
`README.md:21` is pre-existing and out of scope here.

## Non-blocking

- `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandler.cs:26,31`
  — when the slug is taken, `IsSlugExistsAsync` runs twice for the same value: once here, then
  again as the resolver's first step. Two round-trips where one would do. The plan specified this
  ordering verbatim (steps 3-4), so it is faithfully implemented; worth collapsing when the
  composer starts polling this endpoint per keystroke.
- `api/E3A.Application/DependencyInjection.cs:14` — `EngineersOptions` is bound with a plain
  `services.Configure<>`, no `ValidateOnStart`/`ValidateDataAnnotations`. In an environment
  provisioned without the two new keys, `SlugMinLength` silently defaults to `0` and
  `ReservedSlugs` to `[]`, so the slug rules fail OPEN: `"a"` passes the length rule and
  `"admin"`/`"api"` become claimable, producing plugin names like `e3a-api`. This is a real
  deployment condition here — `api/E3A.Api/appsettings.json` is gitignored (`.gitignore:23`), so
  the values do not travel with the repo. Not blocking: the code is correct given correct config,
  the plan mandated the config location, and every other `EngineersOptions` value has carried the
  same exposure since it was introduced. Flagging because the dev is away and this is the one part
  of the slice that cannot be verified from the commit.
- `docs/design-prompt.md:32` — the composer form is described as "name, slug preview in mono".
  Reads fine under the new model (a preview of the normalized/suggested slug is exactly what
  `SlugAvailabilityResult` returns), so not divergence. Worth a wording pass to "slug input +
  availability check" when the create form actually lands with the OAuth slice.
- `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerValidator.cs:26,32,38,44` — the
  `x.Slug != null &&` clause is logically implied by `!string.IsNullOrWhiteSpace(x.Slug)`. The
  implementer flagged this and kept it because the plan specified that exact condition. Agreed
  call; it documents the "null means leave unchanged" intent at the call site.

## Verified

Independently re-run, not taken from `02-implementation.md`:

- **Build.** `dotnet build api/E3a.slnx` produced `Build succeeded. 9 Warning(s), 0 Error(s)`.
  All 9 are pre-existing and originate in `api/core-libraries/`: `Core.Validation` CS8602 x2
  (`RequiredValidationExtensions.cs:52,57`), `Core.OTP` CS8618 x2 (`OTP.cs:30`),
  `Core.Notifications` CS8618 x5 (`Notification.cs:35`, `NotificationTemplate.cs:15`). Zero
  warnings from any `E3A.*` project. Report confirmed exactly.
- **Tests.** `dotnet test api/E3A.Tests/E3A.Tests.csproj` produced
  `Failed: 0, Passed: 236, Skipped: 0, Total: 236`. Report confirmed exactly.
- `api/core-libraries/` untouched — `git status --porcelain api/core-libraries/` and
  `git diff --stat -- api/core-libraries/` are both empty.
- **Exactly 13 files created, no others.** `git status --porcelain -uall` untracked (excluding
  `.process/`) returns 13 paths, matching the plan's *Files to create* list one-for-one.
- **resx parity.** Both files hold 38 `<data>` keys, the key sets are equal, and the order is
  identical as an ordered list. All 38 `ErrorCodes` constants have a key in both; no orphan keys.
  The 6 new slug keys sit immediately after `ENGINEER_DRAFT_NOT_UPLOADED` in the plan's order in
  both files.
- **Freeze guard ordering** — read, not trusted. `UpdateEngineerHandler.cs:37` calls
  `ResolveSlugChangeAsync` (which throws `EngineerSlugFrozen` at `:68`) BEFORE `UpdateMetadata` at
  `:39` and `ChangeSlug` at `:43`. `SaveChangesAsync` is at `:47`, once, after all mutations.
  Test 35 (`UpdateEngineerSlugHandlerTests.cs:110`) pins the ordering by asserting
  `engineer.DisplayName` is still the original after the throw — it fails if the two are swapped.
- **Skill section 8 catalog, entry by entry.** 8.1 — `SlugMinLength`/`ReservedSlugs` live in
  `EngineersOptions.cs:12,16`; no slug cap or reserved word is an entity/validator/handler
  constant. 8.2 — `EngineerSlugResolver.cs:23` uses the injected `IGenerator`; no hand-rolled
  randomness anywhere. 8.3 — `IsSlugExistsAsync` + suffix loop intact and now shared by three call
  sites; `ConflictCoreException` appears nowhere outside `core-libraries`. 8.4 —
  `EngineerStatus.Deleted` / `Engineer.Delete()` unchanged. 8.5 — no `IsDeleted` predicate added
  anywhere; no infrastructure files touched.
- **Skill section 1 absolutes.** File-scoped namespaces in every new file. `sealed` on every new
  command, query, validator, handler, result and test class. `.ConfigureAwait(false)` on every
  `await` outside the controller and outside test bodies (grepped: zero misses). `DateTimeOffset`
  only. No `try`/`catch` in any handler. `SaveChangesAsync` once per mutating handler, in the
  handler. No comments in new production code beyond the two the plan sanctioned in
  `EngineerSlugResolver.cs:16,22` and the one WHY comment at `EngineerSlugGenerator.cs:9`.
- **Decision #9 is real, and the fix is safe.** `Core.Utilities/Generator/Generator.cs:15` returns
  `{prefix}{separator}{nanoid}{separator}{suffix}` — with `suffix` defaulting to empty, every call
  does emit a trailing hyphen. The default alphabet is `0123456789abcdefghijklmnopqrstuvwxyz`,
  which contains no hyphen, so the resolver's `.TrimEnd('-')` can only ever strip that one
  separator and can never eat a character of the nanoid. The trim is correct, not merely convenient.
- **Postman (review order #7).** `Check Slug Availability` exists in the `Engineers` folder directly
  after `List My Engineers`: `GET`,
  `{{baseUrl}}/api/engineers/slug-availability?slug=dive-backend-engineer`, `host`/`path`/`query`
  split correctly, empty `header`, no `auth` override so it inherits the collection bearer token —
  correct, since the endpoint is not `[AllowAnonymous]`. `Create Engineer` and `Update Engineer`
  bodies both carry `"slug"` as the first field. All 8 controller actions have exactly one request
  each; no stale or orphaned entries; the file parses as JSON.
- **Docs (review order #8).** `plugin-spec.md:11,87,94`, `implementation-plan.md:34,44,56` and
  `design-prompt.md:16` all updated and mutually consistent. `plugin-spec.md:90` (`author`:
  `@mohamed-dive`) correctly left alone — slug and GitHub login are now independent, which is the
  point. The only surviving stale statement is finding #1.
- Plan scope honoured: `EngineerSlugGenerator.Normalize(displayName, maxLength)` unchanged
  (`EngineerSlugGenerator.cs:13-37`) and `EngineerSlugGeneratorTests.cs` untouched;
  `CreateEngineerHandler.GenerateUniqueSlugAsync` gone;
  `Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken` absent from the whole tree; the
  two validator test files are signature-only (zero `[Fact]`/`[Theory]` attributes added or removed).
- All 45 enumerated tests exist with exactly the planned names, read individually.

### Deviations — each judged on merit

1. **Regex match timeout as a domain constant.** Right call. Verified, not accepted on trust:
   `api/Directory.Build.props` sets `TreatWarningsAsErrors=true` and adds the
   `SonarAnalyzer.CSharp` package, so S6444 does fail the build. `EngineerSlugGenerator.cs:10` is
   a named constant with a one-line WHY comment, which skill 8.1 explicitly permits for true
   invariants ("True invariants … stay as a named constant WITH a WHY comment"). Keeping it out of
   `EngineersOptions` is also forced — `E3A.Domain` cannot see `E3A.Application.Options` — and
   correct on merit: a ReDoS bound on a non-backtracking pattern is not a product tunable.
2. **5 slug rules, not 4.** Right call. The plan's prose and its canonical block disagreed; the
   block plus the rule-to-error-code map both list 5, and the plan names the block as the
   authority. All 5 are present in all three validators, each with a failing test.
3. **Postman index 2 vs "position 2".** Right call. The plan's two statements were mutually
   exclusive; the semantic one ("right after List My Engineers") is the one a reader means.
4. **`implementation-plan.md` line 56 not 55.** Right call. Line 55 is blank; line 56 is the
   API-surface sentence the plan describes. Editing the described sentence beats editing the
   described line number.
5. **`appsettings.json` invisible in the diff.** Accurate and correctly surfaced. Confirmed on
   disk: `"SlugMinLength": 3` and the full 15-entry `ReservedSlugs` array are present in the
   `Engineers` section, and `.gitignore:23` does exclude the file. Not treated as a missing change.
   See the second non-blocking item for the environment-provisioning consequence.
6. **114-line `UpdateEngineerSlugHandlerTests.cs`.** Right call. The two constraints genuinely
   cannot both hold: the plan says "Create exactly these files and no others" (hard, enumerated)
   while the size rule is written as "~80-100 lines" (soft, tilde). Choosing the hard contract is
   correct, and the precedent is real — `api/E3A.Tests/Engineers/EngineerTests.cs` is 151 lines.
   Every other new file is <= 80. The right resolution is a plan amendment next round, not a
   silent 14th file.

The report's Deviations table is complete — I found no undeclared deviation from the plan.

## Test quality

Would each test fail if the code were wrong? I checked every new test class for the
substitute-returns-what-you-told-it failure mode. None of them is vacuous.

- `EngineerSlugResolverTests` — the strongest file here. `ShouldStripTrailingSeparator` feeds
  `"mmohsen-ab12-"` and demands `"mmohsen-ab12"`, so deleting `.TrimEnd('-')` fails it.
  `ShouldShortenPrefix` asserts `prefix.Length == 95` via `Arg.Is<string>`, pinning the
  `SlugMaxLength - SlugSuffixSize - 1` arithmetic — an off-by-one in either direction fails.
  `ShouldRetry` uses NSubstitute's multi-value `Returns` plus `Received(2)`, constraining the
  `do`/`while` rather than just its output.
- `UpdateEngineerSlugHandlerTests` — constrains ordering, which is the actual risk in this file.
  Test 35's `engineer.DisplayName.Should().Be(EngineerFactory.DefaultDisplayName)` after the throw
  is the one assertion that proves the freeze guard precedes mutation; test 34 (published engineer,
  same slug, no throw, `IsSlugExistsAsync` `DidNotReceive()`) proves the no-op check precedes the
  freeze check. Both fail if the four steps of `ResolveSlugChangeAsync` are reordered.
- `CheckSlugAvailabilityQueryHandlerTests` — `DidNotReceive().Generate(...)` on the available path
  is what stops `SuggestedSlug` being computed unconditionally; the unauthorized test's
  `IsSlugExistsAsync` `DidNotReceive()` pins the guard-first ordering.
- `CreateEngineerHandlerTests` — asserts through `AddAsync(Arg.Is<Engineer>(x => x.Slug == …))`
  rather than only on the returned result, so it constrains what is persisted, not just what is
  mapped back.
- The three slug validator classes — one failing case per rule bound to `ErrorCodes.*` constants
  (never message strings), plus a passing case.
  `Validate_ShouldPass_WhenSlugDiffersOnlyByCaseOrWhitespace` is the one that pins Decision #3:
  drop the normalization and `"  MMohsen  "` starts failing the format rule.
- `EngineerSlugGeneratorTypedInputTests` — the `IsValidFormat` negative theory covers all the
  boundary shapes (empty, leading/trailing hyphen, double hyphen, uppercase, underscore, space,
  punctuation). Together with the empty-string row it also documents why the format rule must stay
  gated behind the required rule.
- `EngineerSlugTests` — `before` is captured after `EngineerFactory.Draft(...)`, so
  `UpdationDate.Should().BeOnOrAfter(before)` genuinely fails if `ChangeSlug` stops stamping.

Coverage contract (`conventions/dotnet-testing.md` section 5): every `throw` in the new and modified
handlers has a test — `CheckSlugAvailabilityQueryHandler` x1, `UpdateEngineerHandler` x4
(unauthorized, not-found, forbidden, frozen), `CreateEngineerHandler` x2 — and every one asserts
`SaveChangesAsync` `DidNotReceive()`; both mutating happy paths assert `Received(1)`. Every
validator rule has a failing case and every validator has a passing case. No reflection, no `new`
on an entity, no wall-clock equality, no inter-test ordering.
