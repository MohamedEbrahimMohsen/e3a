VERDICT: CHANGES_REQUESTED

# Review — Create Team (workspace team composition, frontend wiring)

Scope containment holds: `git diff --stat main -- api/` and `-- postman/` are both empty, and nothing
was smuggled elsewhere — the only non-`web/` change is the single `docs/design-prompt.md` line the plan
authorised. Six new files at exactly the planned paths, no extras. Every test-plan row 1–34 exists with
the exact `it` name the plan specifies. Two of the four declared deviations are correct resolutions of
plan self-contradictions, and I reproduced two of the fourteen mutation checks byte-for-byte.

Two blocking findings: one real user-visible defect the plan created and the implementer chose to ship
as specified, and one vacuous test whose absence of bite I proved by mutation.

## Blocking

### 1. First save of a new team races its own reload; the losing order blanks the roster and the next save deletes it server-side

**Where:** `web/src/features/composer/TeamComposerPage.tsx:79` (the `navigate`) against
`web/src/features/composer/TeamComposerPage.tsx:46` + `:56` (Effect A's `getTeam` then `setMembers`),
with the damage landing at `web/src/features/composer/TeamComposerPage.tsx:81`.

**Rule:** plan Goal ("add them … save"); `conventions/react-feature.md` §3 (a load effect must not
clobber state a concurrent write owns). The plan's `persist()` step 3 is the defect — `01-plan.md:308`.

**Problem:** `persist()` navigates to `/workspace/teams/{saved.id}` *before* awaiting `setTeamMembers`.
The navigation flips `routeTeamId` from `null` to the new id, Effect A re-fires, and a `GET /api/teams/{id}`
runs concurrently with the members `PUT`. Effect A's success path calls
`setMembers(toMemberDrafts(team.members))` unconditionally — no guard, no request-sequence check, no
`useRef` suppressing a self-triggered reload. Whichever response lands last wins the roster state.

The implementer calls this "a display-only regression; the server data is already correct"
(`02-implementation.md:122-130`). That is true for one click only. The blanked roster is the input to the
*next* write: `persist()` sends `toMemberSelections(members)` from component state, and
`SetTeamMembersHandler.ResolvePinsAsync`
(`api/E3A.Application/Teams/SetTeamMembers/SetTeamMembersHandler.cs:47-50`) explicitly accepts an empty
list and hands `[]` to `Team.ReplaceMembers`, which clears the roster. `TEAM_EMPTY` is only thrown at
publish time, so nothing stops the empty save.

**Failure:** Sign in, **+ New Team**, name it, add two published engineers, **Save draft**. The
`PUT /api/teams/{id}/members` is issued first and the `GET /api/teams/{id}` a tick later; if the GET
response arrives after the PUT response (a read queued behind the write, EF plan compilation, or ordinary
variance — `GetTeamQueryHandler` reads `asNoTracking` and is usually but not reliably faster), the GET
carries the pre-save roster. Result: the Members panel renders empty, the counter reads `0 / 10`, Publish
goes disabled (`TeamComposerPage.tsx:141`), and the creator — reasonably concluding the save failed —
presses **Save draft** again. That second save `PUT`s `{ members: [] }` and the roster is genuinely
destroyed on the server. No test covers this, and V2, the only check that would expose it, was not run.

**Fix:** move the `navigate(...)` on `TeamComposerPage.tsx:79` to after the `await setTeamMembers(...)`
on line 81 — `setTeamId` on line 78 already makes subsequent saves take the update path, so nothing
depends on navigating first. A `useRef` guard suppressing the self-triggered reload is equivalent.

### 2. `should map every team error code to readable text` cannot fail when a code is missing — proven

**Where:** `web/src/lib/errorMessages.test.ts:51-57`

**Rule:** `conventions/dotnet-testing.md` §9 (prove the test bites); plan Test plan row 34; plan
Definition of Done ("The 24 error codes in the Error codes table are present in `lib/errorMessages.ts`").

**Problem:** the three assertions are `message.length > 0`, `message !== code`, and
`!message.includes('_')`. For an unmapped code, `messageForErrorCode` returns
`GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.'` (`web/src/lib/errorMessages.ts:3`
and `:62`), which satisfies all three. The test is green whether or not any given code is in the map — it
is named as the proof of a property it does not test. The plan's mutation table M1–M14 covers only the
three new pure modules, so this row was never bite-checked, and `02-implementation.md:105` leans on it:
"The mapping itself (`TEAM_EMPTY` → 'Add at least one member before publishing this team.') is covered by
test row 34." It is not.

**Failure:** I deleted the entire `TEAM_EMPTY: 'Add at least one member before publishing this team.',`
line from `web/src/lib/errorMessages.ts` and ran the suite: **`Test Files 15 passed (15)` ·
`Tests 136 passed (136)`** — zero failures. A creator hitting `400 TEAM_EMPTY` would read "Something went
wrong. Please try again." and the suite would still be green. (File restored; `cmp` byte-identical and
`git diff --stat` back to `24 ++++`.)

**Fix:** add one assertion inside the `it.each` at `errorMessages.test.ts:51`:
`expect(message).not.toBe(GENERIC_ERROR_MESSAGE);` — the symbol is already imported in this file and
already used at line 62.

## Non-blocking

- `web/src/lib/errorMessages.test.ts:43-49` — the pre-existing `callbackErrorCodes` block has the identical
  vacuity. Outside this slice, but the one-line fix for finding 2 applies verbatim.
- `web/src/features/composer/TeamComposerPage.tsx:150` — deviation #1's promoted dismiss button adds
  `fontSize: 13` where the original `<span>` inherited `12.5` from the banner
  (`EngineerComposerPage.tsx:147-149`). "Styling identical" is very slightly overstated. `index.css:42`
  already gives buttons `font-family: var(--font-ui)`, so no font swap occurs.
- `web/src/features/composer/TeamComposerPage.tsx:166` — the promoted tag chip adds inline `border: 'none'`,
  suppressing `.tag-chip`'s `border: 1px solid var(--border)` (`index.css:96`). That border's colour equals
  the inline `background: var(--border)`, so it is invisible; the only effect is a 2px box-size delta.
  Necessary to kill the UA button border, but it is not a pure semantics swap.
- `web/src/features/composer/TeamComposerPage.tsx:223-225` — `aria-label="Move {slug} up"` on a button whose
  visible content is `↑` is, read literally, the `react-feature.md` §6 / WCAG 2.5.3 case flagged in the
  report's note 2. I agree with keeping the labels: an unlabelled arrow is strictly worse and §6's intent is
  speech-input parity with a *textual* label. Not a finding.
- `web/src/features/composer/TeamComposerPage.tsx:214` and `:231` — `<label>` elements with neither `htmlFor`
  nor a wrapped control ("Members", "Structure preview"). They are section headings; a `<span>` would be
  more honest. oxlint does not flag it.
- `web/src/features/workspace/WorkspacePage.tsx:63` — the empty-state subline still reads "Compose your first
  engineer — upload your .claude folder and publish it." while that state now offers **+ New Team** beside
  it. The plan said keep the subline, so this is the plan's call, not drift.

## Verified

Numbers I observed myself, not taken from the report:

- `npm run test` → **`Test Files 15 passed (15)` · `Tests 136 passed (136)`** — the plan's predicted counts exactly.
- `npm run build` (`tsc -b && vite build`) → **zero TypeScript errors**, `✓ 72 modules transformed`, built in 194ms.
- `npx oxlint` → **exactly 8 warnings**, same rules/files/lines as the claimed `main` baseline:
  `react(only-export-components)` at ToastContext:33, AuthContext:75, ReportContext:69, CatalogPage:12;
  `react(set-state-in-effect)` at AuthContext:61, AuthCallbackPage:36, EngineerDetailPage:23;
  `react-hooks(exhaustive-deps)` at CatalogPage:39. None of those eight files appears in this diff, so all
  eight are provably pre-existing. No 9th warning; no export leaked into a `.tsx`.
- `dotnet test api/E3a.slnx` → **`Passed! - Failed: 0, Passed: 815, Skipped: 0, Total: 815`**. Unchanged.
- Mutation spot-check **M9** (`workspaceRows.ts:50`, team `installCount: null` → `0`): reproduced. Observed
  `1 failed | 135 passed (136)`, the failure being row 21
  `should leave a team install count null so no number is rendered`; row 28
  `should render an em dash when there is no install count` stayed green — exactly as claimed. Restored
  from a pre-mutation copy, `cmp` byte-identical.
- Mutation spot-check **M4** (`teamMembers.ts:53-56`, splice move → two-element swap): reproduced. Observed
  `2 failed | 134 passed (136)` — rows 9 and 10, both and only those. Restored, `cmp` byte-identical, full
  suite re-run green.
- Working tree left exactly as found: `13 files changed, 353 insertions(+), 106 deletions(-)`, same
  untracked set. Nothing was edited permanently.

Deviations, one by one:

- **#1 (`<span onClick>` → `<button type="button">`)** — justified. The plan genuinely self-contradicts:
  "copy the `EngineerComposerPage` markup verbatim" versus its own DoD "zero `<div onClick>` /
  `<span onClick>` remain" and `react-feature.md` §6. Both promoted controls keep every class and
  `var(--token)` value; the only additions are `border: 'none'`, `cursor: 'pointer'`, `background: 'none'`
  and one `fontSize` (see non-blocking). `grep` for `span onClick` / `div onClick` in the file returns 0.
  Nothing else moved with them.
- **#2 (10 vs 11 cases in `workspaceRows.test.ts`)** — resolved correctly. Test-plan rows 19–29 are 11 rows,
  and the plan's own arithmetic (18 + 11 + 3 + 1 = 33 single cases, plus a 24-case `it.each` = 57 new,
  79 + 57 = 136) only closes at 11. Eleven shipped; 136 observed.
- **#3 (17 vs 24 error codes)** — resolved correctly. The Error-codes table, test row 34 and the DoD all say
  24; only the *Existing code touched* summary said 17. Exactly 24 entries shipped (22 `TEAM_*` plus
  `PLUGIN_SECURITY_SCAN_BLOCKED` and `MARKETPLACE_TEAM_LIMIT_EXCEEDED`), and I confirmed by parsing
  `ErrorCodes.cs` that **all 24 exist server-side** — none invented.
- **#4 (added `htmlFor`/`id` pairs)** — additive, present, matches the existing `version-increment` pattern.

Other claims confirmed: the eight endpoints in `TeamsController.cs` match the routes, verbs and status codes
the TS wrappers assume (`CreatedAtAction` 201, `Accepted` 202); `TeamMemberResult(EngineerId, EngineerSlug,
PinnedVersionId, PinnedSemanticVersion, SortOrder)` matches the TS `TeamMember` field-for-field, including
`PinnedSemanticVersion` being non-nullable; `grep -c "TEAM_"` really is 27 in both resx files (26 `TEAM_*`
keys plus `MARKETPLACE_TEAM_LIMIT_EXCEEDED` — it is a line count, and the claim as worded is true);
`memberSearchPool` and `CrewMember` are deleted with zero remaining references in `web/src`;
`TeamComposerPage.tsx` imports nothing from `lib/catalog.ts`; every interpolated id in the five new
`workspaceApi` functions passes through `encodeURIComponent`; `PublishStatusPage.tsx` retains no hard-coded
`/workspace/engineers/` path and no unconditional `getEngineer`. `GetTeamQueryHandler` permits the owner to
read a Draft team, so the reopen route is structurally sound.

**Postman (review order #7):** no endpoint added, changed or removed, and `postman/e3a.postman_collection.json`
is untouched. I parsed it anyway: all eight team requests exist with correct verbs and URLs
(`GET /api/teams/mine`, `GET /api/teams/slug-availability`, `GET /api/teams/{{teamId}}`, `POST /api/teams`,
`PUT /api/teams/{{teamId}}`, `PUT /api/teams/{{teamId}}/members`, `POST /api/teams/{{teamId}}/publish`,
`DELETE /api/teams/{{teamId}}`), plus `GET /api/catalog` (`noauth`) and `GET /api/publish/{{versionId}}/status`.
Nothing missing, stale or orphaned.

**Docs (review order #8 / `.claude/rules/docs-sync.md`):** the judgment is right. The one behaviour this change
alters that a doc answered differently is "how do you reorder team members?" — `docs/design-prompt.md:33` said
"draggable ordered member list" and now says "ordered member list with keyboard-accessible move up/down
controls (drag-and-drop deferred)", agreeing with `TeamComposerPage.tsx:223-224`. That is the only `/docs` line
changed. Everything else deferred (D1 version-pin dropdown, D4 public team surface, D5 limits meters) is
docs-ahead-of-code incompleteness, which the rule says never to flag and never to trim.
`docs/implementation-plan.md:9` keeps the team composer in locked v0.1 scope — unchanged and still true.
`docs/plugin-spec.md` merge rules are untouched and the `--` namespacing in `teamStructurePaths` matches them.
No doc was created outside `/docs`.

**Skill compliance (review order #4):** the vendored `.claude/skills/dotnet-feature/SKILL.md` governs `api/`,
and this slice adds no C#; its §8 DO/DON'T catalog and §9 checklist have no surface here, and
`conventions/react-feature.md` §6 is explicit that .NET idioms must not be imported into `web/`. Judged against
`react-feature.md` instead: named exports only, `import type` throughout, testable logic in sibling `.ts`
modules, `cancelled` guards and cleanup on both effects, all API access via `requestJson`, no raw error code
reachable on screen, tunable via `lib/config.ts` with `.env.example` committed, no `oxlint-disable` /
`@ts-ignore` anywhere, every interactive control a `<button type="button">` or `<Link>`. Each touched `.tsx`
exports exactly one symbol.

## Test quality

**`teamMembers.test.ts` (18 cases)** — constrains its module. Every assertion is on a concrete literal
output, never on a value handed back by a stub. Rows 9/10 pick orderings where a swap and a move give
different answers, which I confirmed empirically via M4. Row 4's `expect(problem).toContain('2')` is weak
alone (any message containing a "2" passes), but the paired `toHaveLength(2)` carries the invariant and M1
does break it. Rows 2 and 3 assert reference identity on the returned array — the sharpest available way to
pin "no copy on a problem path".

**`workspaceRows.test.ts` (11 cases)** — constrains its module. Rows 20, 23 and 24 use two fixtures with
distinct `updatedAt` values and then index by position, so each pins the sort *and* the per-branch mapping;
that is why M10 and M12 break exactly one test apiece. Row 25 asserting both `viewPath === editPath` and
`!startsWith('/t/')` is the right belt-and-braces for Decision 9.

**`publishTarget.test.ts` (3 cases)** — constrains its module; row 32's unknown-string case is what makes the
`else` branch meaningful.

**`config.test.ts` row 33** — real: it fails if `maxTeamMembers` is absent or defaults differently.

**`errorMessages.test.ts` row 34 — does NOT constrain the implementation.** See blocking finding 2. It is the
one test in this slice named as the proof of a property that cannot fail when that property is violated.
Twenty-four `it.each` cases inflate the count by 24 while proving only that `messageForErrorCode` returns
*some* prose — which the pre-existing `should return the generic message for an unknown code` already covers
in one line.

**Coverage honesty (review order #5).** The report is unusually straight about what it did not do: V2, V5 and
V6 marked NOT performed, V3 "performed only statically", V4 "half performed", and the *What no automated test
in this slice proves* section restates the caveat without hedging. I re-read the components and confirm the
wiring the report claims is structurally correct: Publish is disabled on an empty roster
(`TeamComposerPage.tsx:141`), ↑ passes `index - 1` and ↓ passes `index + 1` (`TeamComposerPage.tsx:223-224`),
`handleAddMember` calls `addMember` with `config.maxTeamMembers` (`:112`), and `WorkspacePage` renders
`WorkspaceRow[]` from `toWorkspaceRows` (`WorkspacePage.tsx:26`, `:80-95`). The single overstatement anywhere
in the report is the V4 line at `02-implementation.md:105` claiming the `TEAM_EMPTY` mapping is covered by
test row 34 — disproved above. Note also that the report's "Notes for review" item 1 correctly identifies the
race in finding 1 but understates it as display-only; being flagged is not the same as being acceptable.
