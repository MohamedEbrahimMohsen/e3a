VERDICT: CHANGES_REQUESTED

# Review round 2 — Frontend Auth & Workspace Wiring

Scoped verification of the round-1 fix (`failureText` + nine mapped codes) and the `origin/main` merge.
I re-ran the build, the tests and oxlint myself, re-enumerated the API's `FailureReason` surface on the
**merged** tree, and re-checked every invariant round 1 signed off.

The fix is the right shape and every claim in the report is true *as written*. One thing is not: the code
set the fix enumerates was complete against the pre-merge tree and is **not** complete against the tree the
implementer then merged in. One engineer-path code that the SPA can reach today is missing.

## Blocking

### 1. `PLUGIN_DUPLICATE_PATH` reaches the failure panel with no client mapping — the merge added a seventh validator code and the client map still has six

**Where:** `web/src/lib/errorMessages.ts:16-21` (map ends at `PLUGIN_TOO_LARGE`, no `PLUGIN_DUPLICATE_PATH`)
· emitted at `api/E3A.Application/Publishing/Shared/PluginStructureValidator.cs:40`
· `api/E3A.Application/Exceptions/ErrorCodes.cs:107` · rendered through `web/src/features/publish/PublishStatusPage.tsx:121`

**Rule:** acceptance decision 4 ("a raw code shown to a user is a dead end") · `01-plan.md:557` Definition of
Done · round-1 finding 1, whose remedy was defined as *the codes the pipeline can write into `FailureReason`*.

**Problem:** the round-1 fix enumerated `PluginStructureValidator` before the merge, where it emitted six
`PLUGIN_*` codes. `origin/main` (teams) added a seventh. `ErrorCodes.cs` at the merge base has **no**
`PLUGIN_DUPLICATE_PATH`; `ErrorCodes.cs:107` on HEAD does. It is raised by
`PluginStructureValidator.Validate(files, options)` — the overload the **engineer** path calls through
(`EngineerPublishBuilder.cs:41` → `PluginStructureValidator.cs:29` chains into it), so this is not a
teams-only code. `errorMessages.ts` never got it, so `messageForErrorCode` misses and `failureText`
collapses it to `GENERIC_ERROR_MESSAGE`.

The report's rework table states the fix added "the nine codes the pipeline can write into `FailureReason` —
the **six** `PLUGIN_*` from `PluginStructureValidator` and the three `ENGINEER_*`"
(`02-implementation.md:159`). On the merged tree that is seven and ten, not six and nine. The claim of
completeness is the finding as much as the missing string is.

To be precise about the blast radius, because it differs from round 1: this does **not** render a raw code.
`failureText` sends every `^[A-Z0-9_]+$` token through `messageForErrorCode`, which returns the generic
message for anything unmapped, so the DoD line "no rendered string ever contains a SCREAMING_SNAKE code"
still holds. What breaks is the purpose behind it — the creator is given an apology instead of the reason,
and the API already has the exact sentence for it sitting unused.

**Failure:** upload a `.claude` zip whose manifest maps two entries onto one target path — e.g.
`agents/Reviewer.md` and `agents/reviewer.md`, which collide under the `StringComparer.OrdinalIgnoreCase`
`HashSet` at `PluginStructureValidator.cs:34-38`. `paths.Count != files.Count` fires,
`FailureReason = "PLUGIN_DUPLICATE_PATH"`, and `/workspace/publish?versionId=…` renders:

> **Publish failed**
> Something went wrong. Please try again.

while `api/E3A.Api/Resources/Messages.en.resx:247` holds *"The plugin contains two files with the same
path."* — the sentence the creator needed. Worse in the compound case: a tree that is both duplicated and
has no installable root yields `"PLUGIN_DUPLICATE_PATH, PLUGIN_NO_INSTALLABLE_CONTENT"`, which
`failureText` renders as *"Something went wrong. Please try again. The plugin has no agents, skills or
commands to install."* — a generic apology welded onto a specific sentence.

**Fix:** one line in `web/src/lib/errorMessages.ts`, copied from `Messages.en.resx:247` exactly as the other
nine were:

    PLUGIN_DUPLICATE_PATH: 'The plugin contains two files with the same path.',

and add it to the existing multi-code assertion at `publishStage.test.ts:46` so the enumeration is pinned by
a test rather than by a reviewer re-reading the validator after every merge.

## Non-blocking

- `web/src/lib/errorMessages.ts:5-22` — the six `TEAM_*` codes `TeamPublishBuilder` can write
  (`TEAM_NOT_FOUND`, `TEAM_ROSTER_INVALID`, `TEAM_EMPTY`, `TEAM_MEMBER_VERSION_NOT_PUBLISHED`,
  `TEAM_MEMBER_MANIFEST_INVALID`, `TEAM_MEMBER_SNAPSHOT_EMPTY` — `TeamPublishBuilder.cs:22,29,34,50,57,64`)
  are also unmapped. Unlike finding 1 these are genuinely unreachable: `workspaceApi.ts` has no team
  publish call, `TeamComposerPage.tsx:32` `onPublish` only toasts, and `WorkspacePage` lists engineers
  only — so no team `versionId` can reach `/workspace/publish`. I cannot write a reachable failure line, so
  it does not gate. They belong in the same commit as team publishing.
- `web/src/lib/errorMessages.test.ts:5-13` — `callbackErrorCodes` is a hand-maintained list of 7, so the
  nine (soon ten) publish codes are not covered by the "no underscore in the rendered message" invariant.
  Driving the `it.each` off `Object.keys` of the map would make the whole map self-checking. The
  `failureText` cases cover five of them indirectly, which is why this is not a gap that gates.
- Round 1's five non-blocking items were left alone by instruction and remain untouched — correctly not a
  finding.

## Ruling on the flagged follow-up (report note 3, `02-implementation.md:261-269`)

**Correctly flagged follow-up, not blocking.** `PublishStatusPage.tsx:36,125` use `status.itemId` on the
engineer path without reading `status.itemType`, and teams is now on main. But it is unreachable for the
same reason as the `TEAM_*` codes above — there is no SPA path that produces a team version id. Plan
decision 10 required only that nothing breaks if the engineer lookup fails, and that holds:
`getEngineer(...).catch(() => undefined)` at line 36 leaves `engineer === null` and the install block
simply does not render. Team surfaces beyond `installCommand` are explicitly out of scope
(`01-plan.md:59`). Flagging it rather than fixing it under a rework scoped to a numbered finding was the
right call, and the flag is accurate about both the mechanism and the reachability. Whoever wires team
publishing must branch on `itemType` at both call sites.

## Verified

Independently confirmed, not read off the report:

- **`npm run build`** — ran it. `tsc -b && vite build`, 67 modules, zero TypeScript errors. Output matches
  the report byte for byte, including the post-merge hash `index-nYcqelUk.js` at 313.32 kB.
- **`npm run test` = 58** — ran it. `Test Files 10 passed (10) · Tests 58 passed (58)`. The delta from 54 is
  exactly the four new `it` blocks in `describe('failureText')` (`publishStage.test.ts:40-64`). No existing
  case was changed or removed; the `it.each` at `errorMessages.test.ts:16` still iterates a hardcoded 7, so
  the 54 base is arithmetically unchanged.
- **`npx oxlint` = 8 warnings, 0 errors** — ran it. Identical to round 1: the same two
  `react(set-state-in-effect)` at `AuthContext.tsx:59` and `AuthCallbackPage.tsx:30` over the baseline 6.
  Grep for `eslint-disable`, `oxlint-disable`, `@ts-ignore`, `@ts-expect-error` across `web/src` returns
  **zero hits** — nothing was silenced.
- **The nine strings match `Messages.en.resx` exactly, not paraphrased.** Compared character by character
  against `Messages.en.resx:16, 49, 145, 148, 151, 154, 157, 160, 163`. All nine are identical, including
  the typographic apostrophe in the ENGINEER_NOT_FOUND sentence. The SPA and the API say the same
  sentence, which was the point.
- **`failureText` is pure, exported and correctly guarded** (`publishStage.ts:31-38`). Splits on `,`, trims,
  drops empties, `^[A-Z0-9_]+$` gates the mapping, prose passes through, empty result falls to
  `GENERIC_ERROR_MESSAGE`. `PublishStatusPage.tsx:121` renders `failureText(status.failureReason)` and the
  now-unused `GENERIC_ERROR_MESSAGE` import is gone from line 5.
- **`FailureReason` cannot be truncated mid-code** — `ItemVersion.MarkFailed` (`ItemVersion.cs:56-61`)
  assigns verbatim and `FailureReasonMaxLength` is 500 (`api/E3A.Api/appsettings.json:37`); the longest
  joined code string is well under that. No partial-token hazard.
- **`docs/plugin-spec.md` is byte-identical to `origin/main`** — `git diff origin/main HEAD -- docs/plugin-spec.md`
  is empty. This branch’s contradictory "one flat namespace" sentence is gone; main’s "separate namespaces"
  wording is the single answer. The silent auto-merge contradiction is genuinely closed.
- **No other silent contradiction survived the auto-merge** — `git diff --name-only origin/main HEAD -- docs/`
  returns `docs/architecture.md` and nothing else, so `plugin-spec`, `implementation-plan`, `security-scan`,
  `constitution` and `design-prompt` are all exactly main’s.
- **The `docs/architecture.md` § Principles resolution reads coherently, not as two stapled fragments.**
  The diff against `origin/main` is a single bullet, one line: main’s "Auth is a fragment handoff" text
  verbatim with this branch’s SPA clause appended as a continuing sentence ("The SPA reads the token once
  from the fragment, strips the fragment with `history.replaceState`…"). The **Limits** bullet is main’s,
  including "10 members per team". No duplicated clause, no orphaned half-sentence.
- **`origin/main` is fully merged and `api/` is untouched.** `git merge-base origin/main HEAD` equals
  `git rev-parse origin/main` (`38ced01`), so nothing of main is left out; `git diff origin/main HEAD -- api`
  is empty in both directions — not one `api/` file differs.
- **Scope containment: the fix plus the merge, nothing else.** `git diff --diff-filter=A --name-only origin/main HEAD -- web`
  returns exactly the same **27** files round 1 verified — no file was added this round. The fix touched
  the three files the report names plus the test file, all already owned by this slice.
- **No new package, no Azure resource.** The `package.json` diff is the `test` script and
  `vitest ^4.1.11` under `devDependencies`; the `dependencies` block is unchanged. `postman/` is untouched.
- **Round-1 invariants all still hold post-merge:** three login affordances, all plain
  `<a className="btn-primary" href={gitHubLoginUrl()}>` (`NavBar.tsx:40`, `RequireAuth.tsx:11`,
  `AuthCallbackPage.tsx:43`) with no fourth and no `fetch`/`<Link>`; grep for `window.open`, `<iframe`,
  `sessionStorage`, `document.cookie` across `web/src` returns **zero**. `clearAuthFragment()` at
  `AuthCallbackPage.tsx:21`, before the `completeSignIn` await at line 24. `useRef` StrictMode guard at
  `AuthCallbackPage.tsx:12,15-18`. `localStorage` in exactly one production file (`tokenStorage.ts:4,8,12`;
  the only other hits are the two test stubs). 401 clears + notifies, 403 falls straight through to the
  `!response.ok` throw (`http.ts:62-70`). Upload field is `file` (`workspaceApi.ts:85`).
  `ENGINEER_DRAFT_NOT_UPLOADED` sets `manifest = null` and renders the dropzone
  (`EngineerComposerPage.tsx:63-66`). `git ls-files web/src/features/publish/` returns all **three** files —
  the `.gitignore` fix still holds after the merge.
- **The disclosure has not softened.** `02-implementation.md:243-250` still says plainly that the manual JWT
  pass was **not** re-run, the live GitHub round trip has **not** been completed, and that nothing in this
  slice — including the failure panel just fixed — has rendered against real data, explicitly labelling
  `failureText` as proven by unit test rather than by a browser. That is the known Stage 0 constraint and is
  **not** a finding.

## Docs sync

Nothing in this round alters business behaviour, scope, architecture, policy or a contract beyond what
round 1 already reconciled. The failure panel now renders mapped prose instead of a code — UI copy — and
`docs/design-prompt.md` §10(c)’s richer per-file scan panel remains *incompleteness* (the code lags the
target), which the rule says not to flag and not to trim. `docs/architecture.md` and `docs/plugin-spec.md`
verified above. No doc was created outside `/docs`.

## Postman sync

`postman/e3a.postman_collection.json` untouched, correctly — `git diff origin/main HEAD -- postman/` is
empty and `api/` is byte-identical to main. Zero endpoints added, changed or removed. Nothing to sync.

## Test quality

Only `publishStage.test.ts` changed this round; the other nine files are unchanged from round 1 and that
assessment stands.

- **`features/publish/publishStage.test.ts:40-64` (`describe('failureText')`)** — constrains, and would have
  caught round 1’s defect. `not.toMatch(/[A-Z0-9]+_[A-Z0-9_]*/)` at lines 44-46 is a real invariant on the
  output rather than a restatement of the input, and it is asserted for the single-code *and* the
  three-code comma-joined shapes. Line 53 pins the exact joined prose, so the split/trim/join and the
  wording are both nailed down — drop the space from the join, or paraphrase a resx string, and it goes
  red. Line 57 pins the prose-passthrough branch, line 47 the unknown-code fallback, lines 61-62 the null
  and whitespace-only branches. Every branch of `failureText` has a case. Nothing here is a substitute
  handing back its own configured value.
- The one thing the file does **not** constrain is the *completeness* of the map against the API, which is
  finding 1: `PLUGIN_NOT_A_REAL_CODE` on line 47 asserts the fallback works, but nothing asserts that a code
  the pipeline actually emits is absent from it. Adding `PLUGIN_DUPLICATE_PATH` to the line 46 assertion
  turns that from a manual re-enumeration by the reviewer into a test.

No test in this slice is vacuous.
