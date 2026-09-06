VERDICT: APPROVED

# Review (round 2) — Create Team (workspace team composition, frontend wiring)

Fresh reviewer, independent of round 1. I re-read `01-plan.md`, `02-implementation.md`,
`03-review.md` and `02-implementation-r2.md`, then read every changed file end to end, re-ran all
four gates, and independently reproduced the mutation proof for finding 2 with a *third* mapping
line neither previous round used.

Both round-1 blocking findings are genuinely closed. The `useRef` guard that goes beyond the
prescribed fix is sound and does not leak. Nothing in the rework touched the three pure modules or
weakened a round-1 conclusion. No blocking findings.

## Finding 1 — save/reload race: CLOSED

`web/src/features/composer/TeamComposerPage.tsx:74-90` now reads
`isNewTeam` then create/update, `setServerSlug`, `setTeamId` (`:80`), `await setTeamMembers` (`:82`),
`setMembers` (`:83`), `selfSavedTeamId.current = saved.id` (`:86`), `navigate` (`:87`).

**Is the ordering guaranteed rather than probabilistic?** Yes, on both legs.

- *Ordering leg.* `navigate` is the only thing that flips `routeTeamId` off `null`, and it is now
  sequenced after the members `PUT` has resolved. There is no interleaving in which the reload's
  `GET /api/teams/{id}` can carry a pre-save roster. The review's destructive path — blank roster,
  second Save draft, `PUT { members: [] }`, `Team.ReplaceMembers([])` — is unreachable, because
  the roster in state at that instant is the `PUT` response itself (`:83`).
- *Guard leg.* `selfSavedTeamId.current` is a plain ref write, synchronous, on the line before
  `navigate` (`:86` before `:87`). Effects run after commit, so the effect run *caused by* the
  navigation always observes the assigned value. This is a happens-before, not a race: there is no
  scheduler ordering under which `:43` reads `null` for the navigation triggered at `:87`.

**Is the "same component type at the same depth" claim true?** Verified, and it holds.
`web/src/App.tsx:71-72` declares `/workspace/new-team` and `/workspace/teams/:teamId` as sibling
routes under the same `ComposerLayout` then `RequireAuth` chain, both with `element={<TeamComposerPage />}`.
I checked the installed router rather than trusting the claim: react-router 7.18.2's `_renderMatches`
wraps each match in `RenderedRoute`
(`web/node_modules/react-router/dist/development/chunk-62JRHF6Z.mjs:6218`, used at `:6330`) with **no
`key`**, so React reconciles by position and element type. Same type at the same depth means state
preserved and no remount.

**And if it did remount?** Safe too, and I confirmed the reasoning rather than accepting it: a
remount resets the ref to `null`, so Effect A performs a real `getTeam` — but that request is still
issued strictly after the members `PUT` resolved, so it returns the saved roster. `teamId`
re-initialises from `routeTeamId` (`:23`) and `loadStatus` from the same expression (`:37`), so the
remounted instance is internally consistent. The fix is correct under either reconciliation.

**Does the guard leak?** No, in every case I could construct:

- *Different team.* `selfSavedTeamId.current === routeTeamId` is an identity check on the id, not a
  boolean. Navigating to a different `:teamId` fails the comparison and loads normally (`:43`).
- *Reopen from the workspace list.* A fresh mount starts with `current === null`, `loadStatus`
  `'loading'`, and Effect A runs a real load. The guard is only ever set on the create path
  (`isNewTeam`, `:85`), never on the update path.
- *Back/forward and refresh.* `ComposerShell.tsx:31-37` is the only navigation out of the composer
  (`/`, `/workspace`, `/u/{login}`) — all of them change the leaf element type and unmount
  `TeamComposerPage`, so any return trip is a fresh mount with `current === null`. A browser refresh
  is a new JS realm. The ref cannot survive into a context where a reload is owed.
- *Suppressed-but-needed data.* The reload it suppresses is strictly redundant: `setTeamMembers`
  returns the full `TeamDetail` and `:83` already applies it; `displayName` / `description` / `tags`
  are the user's own in-state values; `setServerSlug` was applied at `:78`; `loadStatus` was already
  `'ready'` from `:37` on the create path, so suppressing the effect cannot strand the page on
  `Loading…` (`:135`). No field Effect A would have written is left stale.

**The declared behaviour change — `createTeam` succeeds, members `PUT` fails, no navigation while
`teamId` is set.** Retry is safe and cannot create a second team: `setTeamId(saved.id)` at `:80` ran
before the rejection, `persist` is re-created on the next render, and `:75`/`:77` therefore take the
`updateTeam` branch. The residual is only that a browser *reload* in that window loses the id from
the URL, leaving a draft team that must be reopened from the workspace list — visible there via
`toWorkspaceRows` and not data loss. Strictly better than round 1, which navigated into the
destructive state.

## Finding 2 — vacuous test: CLOSED, proof independently reproduced

`web/src/lib/errorMessages.test.ts:57` — `expect(message).not.toBe(GENERIC_ERROR_MESSAGE);`.

I did not re-use either line the previous rounds mutated. I copied `web/src/lib/errorMessages.ts` to
the scratch directory (md5 `84c8556115489e04c597fbe613362bb6`), deleted `TEAM_MEMBER_DUPLICATE`
(`errorMessages.ts:43`) and ran the suite:

    ❯ src/lib/errorMessages.test.ts (36 tests | 1 failed)
        × should map every team error code to readable text
    AssertionError: expected 'Something went wrong. Please try agai…' not to be 'Something went wrong. Please try agai…'
     ❯ src/lib/errorMessages.test.ts:57:25
     Test Files  1 failed | 14 passed (15)
          Tests  1 failed | 135 passed (136)

Restored from the copy, `cmp` byte-identical, md5 back to `84c8556115489e04c597fbe613362bb6`,
`git diff --stat -- web/src/lib/errorMessages.ts` back to `24 ++++`, full suite re-run
`15 passed / 136 passed`. Exactly one case fails per missing mapping, so the count is itself a
signal.

**Does the missing `%s` matter enough to block?** No. The bar is whether the test can fail when the
property is violated; it now can, and it does so at a named file, test and assertion line. Adding
`%s` would rename a test whose exact title the plan fixes (`01-plan.md:458`) and the Definition of
Done checks, so declining it was the right call. Recorded below as a non-blocking follow-up.

## Non-blocking

- `web/src/lib/errorMessages.test.ts:43-49` — the twin vacuity in `callbackErrorCodes` is
  **confirmed pre-existing**, not introduced by this slice: `git show main:web/src/lib/errorMessages.test.ts`
  contains that block verbatim, and `git diff main` on the file shows only the added `teamErrorCodes`
  array and the new `it.each`. Deleting any of those seven mappings leaves the suite green.
  Follow-up: the same one-line `not.toBe(GENERIC_ERROR_MESSAGE)` applies verbatim.
- `web/src/features/composer/TeamComposerPage.tsx:60` + `:135` — when Effect A's `getTeam` *fails*
  (`loadStatus === 'failed'`) the composer still renders an editable, empty form at a real `teamId`
  (`:23`), with Save draft enabled (`:145`). An empty `displayName` is rejected server-side by the
  team validator before any mutation, so a bare Save is harmless — but a creator who retypes the name
  into the blank form and saves would `PUT { members: [] }`. Not raised as blocking: the plan
  explicitly specified Effect A to mirror `EngineerComposerPage` and prescribed no failed-load
  branch, the shape is unchanged from round 1 and untouched by the rework, and unlike the round-1
  defect the screen is not falsely reporting success — the error banner is up and the roster is
  visibly empty. Worth a dedicated `loadStatus === 'failed'` branch that does not offer Save, later.
- `web/src/lib/errorMessages.test.ts:51` — the `it.each` title carries no `%s`, so a failure does not
  name the offending code; attribution needs a re-run. Cosmetic diagnosability only, and changing it
  conflicts with the plan-fixed test name.
- Round 1's other non-blocking items (`TeamComposerPage.tsx:155` `fontSize: 13`, `:171` inline
  `border: 'none'`, `:219`/`:236` heading `<label>`s without `htmlFor`, `WorkspacePage.tsx:57`
  empty-state subline) were deliberately left alone, correctly — the round-2 instruction was two
  findings and nothing else.
- V2 / V5 / V6 remain unperformed for want of a live API, and the rework says so plainly rather than
  implying coverage (`react-feature.md` §7). The specific unobserved claim is that after Save draft
  on a new team the Members panel keeps its rows. The static argument above is strong, but it is a
  static argument; run V2 before this reaches a user.

## Verified

Gates, all observed by me this round, not taken from the report:

- `npm run build` (`tsc -b`, then `vite build`) — **zero TypeScript errors**, `✓ 72 modules transformed`,
  built in 315ms.
- `npm run test` — **`Test Files 15 passed (15)` · `Tests 136 passed (136)`**. Unchanged from round 1,
  which is correct: finding 2 strengthened an assertion inside an existing `it.each` rather than
  adding a case.
- `npx oxlint` — **exactly 8 warnings**, identical rules/files/lines to the measured `main` baseline:
  `react(only-export-components)` ToastContext:33, ReportContext:69, AuthContext:75, CatalogPage:12;
  `react(set-state-in-effect)` AuthContext:61, AuthCallbackPage:36, EngineerDetailPage:23;
  `react-hooks(exhaustive-deps)` CatalogPage:39. No warning from the added `useRef`.
- `dotnet test api/E3a.slnx` — **`Passed! - Failed: 0, Passed: 815, Skipped: 0, Total: 815`**.
- `git diff --stat main -- api/ postman/` — **empty**. `git diff --stat main` — 13 files,
  `359 insertions(+), 106 deletions(-)`, matching the rework's declared +6 over round 1's 353.
- `grep` over `web/src`: zero `oxlint-disable` / `@ts-ignore` / `@ts-expect-error`, zero
  `export default`, zero `span onClick` / `div onClick` in `TeamComposerPage.tsx`, zero remaining
  `memberSearchPool` / `CrewMember` references.

Round-1 conclusions re-checked and still standing:

- **The three pure modules were not touched by the rework.** File mtimes: `publishTarget.ts` 22:52:55,
  `workspaceRows.ts` 23:00:29, `teamMembers.ts` 23:00:45 — all before the round-2 edits
  (`TeamComposerPage.tsx` 23:09:22, `errorMessages.test.ts` 23:09:26). Their contents match the plan
  contracts line for line (`01-plan.md:214-291`).
- I re-ran two mutation checks against the *current* tree to confirm the 14 subjects still bite, and
  chose two the round-1 reviewer did not sample: **M13** (`publishTarget.ts:10`, `'Team'` case falls
  through) gave `1 failed | 135 passed`, failing exactly
  `should route a team publish back to the team composer`; **M7** (`teamMembers.ts:60`,
  `pinnedVersionId: null`) gave `1 failed | 135 passed`, failing exactly
  `should send each member's explicit pinned version id in roster order`. Both restored from
  pre-mutation copies, both `cmp` byte-identical, suite re-run `15 / 136`.
- Round 1's four deviations are all still present and still correct: promoted `<button type="button">`
  for the dismiss control (`:155`) and tag chips (`:171`); 11 cases in `workspaceRows.test.ts`; 24
  error-code entries in `errorMessages.ts:25-48`; `htmlFor`/`id` pairs (`team-name`,
  `team-description`, `team-tag`, `member-search`, `version-increment`).
- Every plan *Files to create* path exists and nothing extra: the six new source files plus
  `.process/create-team/`. Every `workspaceApi` wrapper matches the plan signature with
  `encodeURIComponent` on each interpolated id (`workspaceApi.ts:151-169`).

**Rework report claims audited.** Every observed number in `02-implementation-r2.md` (build, 136
tests, 8 warnings, 815 backend tests, empty `api/` and `postman/` diff, 359 insertions) reproduced
exactly. The one deviation it declares — `persist()` no longer matching `01-plan.md:305-311` line for
line — is real, disclosed, and is precisely what round 1 directed. No hidden deviation found.

**Postman (review order #7).** No endpoint added, changed or removed;
`postman/e3a.postman_collection.json` is unmodified (`git diff --stat main -- postman/` empty). I
parsed it anyway: all eight team requests present with correct verbs and URLs
(`GET /api/teams/mine`, `GET /api/teams/slug-availability`, `GET /api/teams/{{teamId}}`,
`POST /api/teams`, `PUT /api/teams/{{teamId}}`, `PUT /api/teams/{{teamId}}/members`,
`POST /api/teams/{{teamId}}/publish`, `DELETE /api/teams/{{teamId}}`), plus `GET /api/catalog`
(`noauth`) and `GET /api/publish/{{versionId}}/status` (inherited bearer auth). Nothing missing,
stale or orphaned.

**Docs (review order #8 / `.claude/rules/docs-sync.md`).** The single behavioural question this slice
answers differently from `/docs` is "how are team members reordered?" — `docs/design-prompt.md:33`
now reads "ordered member list with keyboard-accessible move up/down controls (drag-and-drop
deferred)", agreeing with the up/down buttons at `TeamComposerPage.tsx:228-229`. That is the only
`/docs` line changed, and round 2 changed no doc at all — correct, since the rework altered no
behaviour a doc describes. D1/D4/D5 remain described as targets: incompleteness, never a finding, and
not to be trimmed. No doc exists outside `/docs`.

**Skill compliance (review order #4).** The vendored `.claude/skills/dotnet-feature/SKILL.md` governs
`api/` only and this slice adds no C#, so its §8 DO/DON'T catalog and §9 checklist have no surface
here — `conventions/react-feature.md` says so explicitly at its head. Judged against
`react-feature.md`: named exports only, `import type` throughout, testable logic in sibling `.ts`
modules, `cancelled` guard and cleanup on both effects (`:46-61`, `:65-71`), all API access through
`requestJson`, no raw error code reachable on screen, tunables via `lib/config.ts` with
`.env.example` committed, every interactive control a `<button type="button">` or `<Link>`, one
export per `.tsx`. The `useRef` one-shot guard is the pattern §5 itself prescribes.

## Test quality

- **`teamMembers.test.ts` (18)** — constrains its module. Assertions are concrete literals, never a
  value handed back by a stub. Rows 2/3 assert reference identity on the problem paths; rows 9/10
  pick orderings where a swap and a move differ. M7 reproduced by me.
- **`workspaceRows.test.ts` (11)** — constrains its module; the two-fixture-then-index-by-position
  style pins the sort and the per-branch mapping in the same assertion.
- **`publishTarget.test.ts` (3)** — constrains its module; M13 reproduced by me, failing exactly the
  team row.
- **`config.test.ts` row 33** — real: fails if `maxTeamMembers` is absent or defaults differently.
- **`errorMessages.test.ts` team `it.each` — now constrains the implementation.** Proven by deleting
  a mapping the previous rounds did not touch. The pre-existing `callbackErrorCodes` block above it
  still does not; see Non-blocking.
- Unchanged and correctly declared: **no test in this slice reaches a component.** Nothing proves
  that `persist()` orders the two writes as written, that the guard suppresses the reload, or that
  the Members panel survives the first save. The ordering argument in this review is a reading of the
  code, not an execution of it.
