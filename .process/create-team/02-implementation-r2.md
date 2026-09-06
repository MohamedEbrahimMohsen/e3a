# Rework — Create Team (round 1 → round 2)

Contract: `.process/create-team/03-review.md`, verdict `CHANGES_REQUESTED`, two blocking findings.
Both addressed. Nothing else was touched — no refactor, no extra test, no new file.
`02-implementation.md` is unmodified (round-1 audit trail).

## Findings addressed

| # | Finding | What I changed | Where |
|---|---|---|---|
| 1 | First save of a new team races its own reload; the losing order blanks the roster and the next save deletes it server-side | Moved `navigate('/workspace/teams/{id}', { replace: true })` out of the pre-write block and to **after** `await setTeamMembers(...)`, so the self-triggered `GET /api/teams/{id}` can no longer be in flight concurrently with the members `PUT`. `setTeamId(saved.id)` stays where it was (before the members write) so a retry after a failed member save still takes the update path and cannot create a second team. Added the minimum guard the reviewer named as equivalent — a `useRef` holding the id this component just created — so the now-redundant self-triggered reload is not issued at all. | `web/src/features/composer/TeamComposerPage.tsx:1` (`useRef` import), `:40` (`selfSavedTeamId` ref), `:43` (Effect A early-return guard), `:74`–`:88` (`persist()`: `isNewTeam` local, `setTeamId` at `:79`, `navigate` moved to `:87`) |
| 2 | `should map every team error code to readable text` cannot fail when a code is missing — proven | Added the one prescribed assertion inside the team `it.each`. `GENERIC_ERROR_MESSAGE` was already imported (`:2`) and already used (`:62`). | `web/src/lib/errorMessages.test.ts:57` |

Non-blocking items from the review (`callbackErrorCodes` vacuity, `fontSize: 13` on the dismiss button,
`border: 'none'` on the tag chip, the `aria-label` glyph question, the two heading `<label>`s, the
workspace empty-state subline) were deliberately **not** touched.

## Finding 1 — how I satisfied myself the race is actually closed, not moved

`persist()` now reads:

```
const isNewTeam = teamId === null;
const saved = await (teamId ? updateTeam(...) : createTeam(input));
setServerSlug(saved.slug);
if (isNewTeam) { setTeamId(saved.id); }
const detail = await setTeamMembers(saved.id, toMemberSelections(members));
setMembers(toMemberDrafts(detail.members));
setLastSaved('just now');
if (isNewTeam) { selfSavedTeamId.current = saved.id; navigate(`/workspace/teams/${saved.id}`, { replace: true }); }
return detail;
```

**Does the ordering alone close it?** Yes for the defect as described. `navigate` is the only thing that
flips `routeTeamId` from `null`, and it now runs after the members `PUT` has *resolved*. So the `GET` is
issued strictly after the write completed server-side: there is no interleaving in which the `GET` can
carry a pre-save roster. The reviewer's failure path — blanked roster → creator presses Save draft again →
`PUT { members: [] }` → `Team.ReplaceMembers([])` destroys the roster — is unreachable, because the roster
in state at that moment is either the `PUT` response or a server read taken after it.

**Was a guard still needed?** Yes, for a narrower but real residual, which is why I added one rather than
stopping at the move. Between `navigate(...)` and the reload's `getTeam` resolving there is one network
round trip in which Effect A's success path still runs `setDisplayName`, `setDescription`, `setTags`,
`setServerSlug` and `setMembers(toMemberDrafts(team.members))` **unconditionally**. Anything the creator
types or adds in that window is overwritten by server state that is merely equal-or-staler — exactly the
`react-feature.md` §3 rule the review cites ("a load effect must not clobber state a concurrent write
owns"). The reload is also entirely redundant: `setTeamMembers` already returns the full `TeamDetail` and
line 82 already applies it. The guard is three tokens of state and one clause:

- `const selfSavedTeamId = useRef<string | null>(null);` (`:40`)
- `if (!routeTeamId || selfSavedTeamId.current === routeTeamId) { return; }` (`:43`)
- `selfSavedTeamId.current = saved.id;` immediately before the `navigate` (`:86`)

The ref is assigned synchronously *before* `navigate`, and effects run after commit, so the guard is
always visible to the effect run that the navigation causes. Ordering is guaranteed, not probabilistic.
The guard is scoped to the self-created id only: reopening `/workspace/teams/{id}` from the workspace
list starts with `selfSavedTeamId.current === null`, so that load path is untouched (`loadStatus` still
initialises to `'loading'` and Effect A still runs). The guard never suppresses a reload of a *different*
team, and it is never set on the update path (`isNewTeam === false`).

**Remount vs. state-preserving reconciliation.** `/workspace/new-team` and `/workspace/teams/:teamId` are
sibling routes at the same depth rendering the same component type (`App.tsx:71-72`), so React reconciles
by type and preserves state — which is what makes the review's race real in the first place. I checked the
other branch too: if react-router *did* remount, the ref would reset to `null`, Effect A would run a real
load, and that load would still be strictly after the members write, so the data is correct either way.
The fix does not depend on which reconciliation happens.

**Is the URL still correct with `replace: true`?** Yes. Nothing else navigates between the create and the
`navigate` — `persist()` is only called from `handleSaveDraft` and `handlePublish`, both of which
early-return on `saving || publishing`. So the entry being replaced is still `/workspace/new-team`, giving
`/workspace/teams/{id}` with no dead "new team" entry in history. On the publish path, `persist()`'s
replace runs first and the subsequent `navigate('/workspace/publish?versionId=…')` pushes on top, so Back
from the publish status page lands on the team composer — unchanged from round 1's intent.

**Behaviour change I am declaring, because the ordering has one:** if `createTeam` succeeds but
`setTeamMembers` rejects, `navigate` no longer runs, so the URL stays `/workspace/new-team` while `teamId`
is set in state. A retry updates the existing team (no duplicate); a browser *reload* at that moment would
lose the id from the URL, and the draft team would have to be reopened from the workspace list. I judge
this strictly better than round 1, where that same failure navigated to a team URL and then reloaded a
blank roster — i.e. straight into the destructive second save the finding describes.

**Not verified at runtime.** There is no live API or database available and I did not start one, so V2 /
V5 / V6 remain unperformed exactly as in round 1. The evidence above is static: the ordering argument, the
ref/effect ordering guarantee, `tsc -b` clean, and oxlint unchanged. I did not observe the fixed flow in a
browser and this report should not be read as claiming otherwise.

## Finding 2 — proof the test now bites

Method: copied `web/src/lib/errorMessages.ts` to the scratch directory first; mutated with `sed`; ran the
suite; restored **from the copy**, never retyped; verified with `cmp`.

**Mutation A — deleted line 30, `TEAM_EMPTY: 'Add at least one member before publishing this team.',`**
(the exact line the reviewer deleted, which round 1 left green at 136/136):

```
 ❯ src/lib/errorMessages.test.ts (36 tests | 1 failed) 20ms
     × should map every team error code to readable text 6ms

 FAIL  src/lib/errorMessages.test.ts > messageForErrorCode > should map every team error code to readable text
AssertionError: expected 'Something went wrong. Please try agai…' not to be 'Something went wrong. Please try agai…' // Object.is equality
 ❯ src/lib/errorMessages.test.ts:57:25

 Test Files  1 failed | 14 passed (15)
      Tests  1 failed | 135 passed (136)
```

**Mutation B — deleted `TEAM_ROSTER_INVALID` (line 48) instead**, to show the failure tracks whichever
mapping is missing rather than being an artefact of one line: `Tests 1 failed | 135 passed (136)`, same
test, same assertion at `:57`. Exactly one of the 24 `it.each` cases fails per missing mapping.

**Restore:** `cmp` byte-identical against the pre-mutation copy after each mutation, and
`git diff --stat -- web/src/lib/errorMessages.ts` is back to `24 ++++++++++++++++++++++++` — the same
`24 ++++` the reviewer recorded. Full suite re-run after restore: `Test Files 15 passed (15)` ·
`Tests 136 passed (136)`.

**One honesty note on the wording of the instruction.** The suite now fails, but it does **not print the
offending code**: the plan fixes row 34's title as `'should map every team error code to readable text'`
with no `%s` placeholder, so vitest repeats that title verbatim for all 24 cases. Adding `%s` would rename
a test the plan names exactly and goes beyond finding 2's prescribed one-line fix, so I did not do it. What
the failure *does* name is the file, the test, and the assertion line (`errorMessages.test.ts:57`,
`not.toBe(GENERIC_ERROR_MESSAGE)`), and the `1 failed | 135 passed` count identifies it as a single missing
mapping. Attribution to the specific code is by re-running after restoring, as in mutation B above.

## Gates — observed this round

| Command | Observed | Bar |
|---|---|---|
| `npm run build` (`tsc -b && vite build`) | `✓ 72 modules transformed`, `✓ built in 187ms`, **zero TypeScript errors** | zero errors — held |
| `npm run test` | `Test Files 15 passed (15)` · `Tests 136 passed (136)` | **unchanged at 136** — correct and expected: finding 2 *strengthens an existing assertion inside an existing `it.each`*, it does not add a case. A change in this number would have meant I added or dropped a test. |
| `npx oxlint` | **exactly 8 warnings** — `react(only-export-components)` at ToastContext:33, AuthContext:75, ReportContext:69, CatalogPage:12; `react(set-state-in-effect)` at AuthCallbackPage:36, AuthContext:61, EngineerDetailPage:23; `react-hooks(exhaustive-deps)` at CatalogPage:39. Same rules, files and lines as the baseline; none from `TeamComposerPage.tsx`. | exactly 8 — held. The added `useRef` produces no `exhaustive-deps` warning (refs are stable and exempt), and no export leaked into a `.tsx`. |
| `dotnet test api/E3a.slnx` | `Passed!  - Failed: 0, Passed: 815, Skipped: 0, Total: 815, Duration: 1 s - E3A.Tests.dll (net10.0)` | 815 — held (no `api/` file was touched this round either) |

Scope re-confirmed: `git diff --stat main -- api/ postman/` is **empty**. Working tree is the same 13
modified files and the same 6 untracked source files the reviewer left it with; the stat moved from
`353 insertions(+)` to `359 insertions(+)` — the +6 being 5 net lines in `TeamComposerPage.tsx` and 1 in
`errorMessages.test.ts`. No file created, no file removed.

## Deviations from the plan

| Plan said | Reality | What I did |
|---|---|---|
| `01-plan.md:308` — `persist()` step 3: on create, `setTeamId(saved.id); navigate('/workspace/teams/{saved.id}', { replace: true });` **before** step 4's `await setTeamMembers(...)` | The plan is wrong here, and the reviewer demonstrated the consequence: the navigation flips `routeTeamId`, re-fires Effect A, and races a `GET` against the members `PUT`; the losing order blanks the roster in state, and because `SetTeamMembersHandler.ResolvePinsAsync` accepts an empty list and `TEAM_EMPTY` only fires at publish time, the creator's next Save draft deletes the roster server-side. | Deviated as the review directs: `setTeamId` stays in step 3, `navigate` moved to after step 5. Added a `useRef` self-reload guard the plan does not describe (the reviewer names it as an equivalent fix; I use it as the additional minimum for the residual clobber window justified above). Plan-conformance impact: `persist()` no longer matches `01-plan.md:305-311` line for line; every other signature and step is verbatim. |

Round 1's four declared deviations stand unchanged and were re-confirmed correct by the reviewer.

## Notes for review

1. The `selfSavedTeamId` ref is intentionally **not** cleared after it is consumed. Within one component
   instance `routeTeamId` never changes again after the self-navigation, so leaving it set is a no-op; and
   if a Fast Refresh or remount did re-run the effect, both branches are safe (set → suppressed reload of
   data we already hold; reset → a real reload that lands after the write). Clearing it would add a line
   and a StrictMode question for no behavioural gain.
2. Two `if (isNewTeam)` blocks now bracket the members write. That is deliberate, not sloppiness:
   `setTeamId` must stay *before* the write so a failed member save retries as an update rather than
   creating a duplicate team, while `navigate` must be *after* it. `isNewTeam` is captured once at the top
   because `teamId` in the closure would otherwise read the same stale value twice anyway — hoisting it
   makes that explicit.
3. The non-blocking twin of finding 2 — the identical vacuity in the pre-existing `callbackErrorCodes`
   block at `errorMessages.test.ts:43-49` — is still there. The review classed it non-blocking and outside
   this slice, and the instruction for this round was two findings and nothing else, so I left it. The
   same one-line fix applies verbatim whenever someone is authorised to touch it.
4. Runtime verification of finding 1 is still owed: V2 (full happy path), V5 (workspace round-trip) and V6
   (reopen after publish) all need a live API. The specific unobserved claim is that after **Save draft**
   on a new team the Members panel keeps its rows and the counter reads `2 / 10` rather than `0 / 10`.
