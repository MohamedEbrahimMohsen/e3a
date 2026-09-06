# Implementation — Create Team (workspace team composition, frontend wiring)

Frontend-only slice, as planned. Zero files under `api/` touched, `postman/e3a.postman_collection.json`
unmodified. Every backend claim the plan rests on was re-verified before writing a line (see
**Plan claims re-checked**), and all of them held.

## Files created

| Path | Lines | Purpose |
|---|---|---|
| `D:\Personal\_e3a\web\src\features\composer\teamMembers.ts` | 72 | Roster state machine: `toMemberDrafts`, `isMember`, `addMember`, `removeMemberDraft`, `moveMemberDraft`, `toMemberSelections`, `memberVersionLabel`, `teamStructurePaths`. |
| `D:\Personal\_e3a\web\src\features\composer\teamMembers.test.ts` | 152 | 18 cases (plan rows 1–18). |
| `D:\Personal\_e3a\web\src\features\workspace\workspaceRows.ts` | 70 | Merges engineers + teams into one sorted `WorkspaceRow[]`, plus `formatInstallCount` / `formatUpdated`. |
| `D:\Personal\_e3a\web\src\features\workspace\workspaceRows.test.ts` | 95 | 11 cases (plan rows 19–29). |
| `D:\Personal\_e3a\web\src\features\publish\publishTarget.ts` | 13 | `publishTargetFor(itemType, itemId)` → install kind, composer path, noun. |
| `D:\Personal\_e3a\web\src\features\publish\publishTarget.test.ts` | 24 | 3 cases (plan rows 30–32). |

No other new file exists. `git status` shows exactly these six untracked source files (plus `.process/create-team/`).

## Files modified

| Path | Change |
|---|---|
| `D:\Personal\_e3a\web\src\lib\workspaceApi.ts` | Added `TeamInput`, `TeamMember`, `TeamDetail`, `TeamMemberSelectionInput`; added `getTeam`, `createTeam`, `updateTeam`, `setTeamMembers`, `publishTeam` — signatures verbatim from the plan, every interpolated id through `encodeURIComponent`. `Team` and `listMyTeams` untouched. |
| `D:\Personal\_e3a\web\src\lib\config.ts` | `DEFAULT_MAX_TEAM_MEMBERS = 10` + `maxTeamMembers` on `config`. |
| `D:\Personal\_e3a\web\.env.example` | `VITE_MAX_TEAM_MEMBERS=10`. |
| `D:\Personal\_e3a\web\src\lib\config.test.ts` | One `it` for the `maxTeamMembers` fallback (plan row 33). |
| `D:\Personal\_e3a\web\src\lib\errorMessages.ts` | 24 new entries (22 `TEAM_*` + `PLUGIN_SECURITY_SCAN_BLOCKED` + `MARKETPLACE_TEAM_LIMIT_EXCEEDED`), prose exactly as in the plan's table. |
| `D:\Personal\_e3a\web\src\lib\errorMessages.test.ts` | `teamErrorCodes` array + one `it.each` block (plan row 34, 24 cases). |
| `D:\Personal\_e3a\web\src\lib\catalog.ts` | Deleted the `memberSearchPool` export. |
| `D:\Personal\_e3a\web\src\lib\types.ts` | Deleted the `CrewMember` interface. |
| `D:\Personal\_e3a\web\src\App.tsx` | Added `/workspace/teams/:teamId` inside `ComposerLayout` → `RequireAuth`, directly after `/workspace/new-team`. No new import. |
| `D:\Personal\_e3a\web\src\features\composer\TeamComposerPage.tsx` | Full rewrite against the real API: load effect, debounced catalog member search, `persist()` (metadata then members), save/publish, add/remove/reorder, structure preview. Zero `lib/catalog.ts` imports. |
| `D:\Personal\_e3a\web\src\features\workspace\WorkspacePage.tsx` | `Promise.all([listMyEngineers(), listMyTeams()])` → `toWorkspaceRows`; renders `WorkspaceRow[]`; `+ New Engineer` **and** `+ New Team`; local `formatUpdated` removed; `statusChipStyle` kept local. |
| `D:\Personal\_e3a\web\src\features\publish\PublishStatusPage.tsx` | `engineer` state → `publishedSlug`; `publishTargetFor` drives noun, install kind, "Fix and republish" link; team publishes load their slug via `getTeam`; "View in catalog" only for `Engineer`. No hard-coded `/workspace/engineers/` path and no unconditional `getEngineer` remain. |
| `D:\Personal\_e3a\docs\design-prompt.md` | §33 line 33 only: `draggable ordered member list` → `ordered member list with keyboard-accessible move up/down controls (drag-and-drop deferred)`. No other doc touched. |

## Deviations

| Plan said | Reality | What I did |
|---|---|---|
| TeamComposerPage: "dismissible error banner (**copy the `EngineerComposerPage` markup verbatim**)"; tags chip input like `EngineerComposerPage`. | `EngineerComposerPage`'s dismiss control and tag chips are `<span onClick>`. The same plan's Definition of Done requires "zero `<div onClick>` / `<span onClick>` remain in `TeamComposerPage.tsx`", and `react-feature.md` §6 forbids them. The two instructions cannot both be satisfied. | Kept the markup, classes and `var(--token)` styling identical but promoted both controls to `<button type="button">` (transparent background, no border). The DoD/§6 side wins; the visual language is unchanged. The dismiss button carries `aria-label="Dismiss error"`; the tag chip has none, so its accessible name stays the visible `"{tag} ×"` (§6 forbids an `aria-label` that does not contain the visible text). |
| *Files to create* summary column: `workspaceRows.test.ts` — "10 cases". | The Test plan table lists rows 19–29 for that file = **11** cases, and the plan's own arithmetic (`rows 1–33 are 33 single cases`; 79 + 33 + 24 = 136) only works with 11. | Wrote 11. `npm run test` reports exactly the planned 136. |
| *Existing code touched* row for `errorMessages.ts`: "Add the **17** `TEAM_*` / team-publish-failure entries". | The **Error codes** table lists **24** rows, and test row 34 plus the DoD both say 24. | Added all 24. Every one of them exists as a constant in `api/E3A.Application/Exceptions/ErrorCodes.cs`, so none is invented. |
| Render spec lists the composer's labels and inputs without ids. | Nothing blocked this; the plan simply did not say. | Added `htmlFor`/`id` pairs (`team-name`, `team-description`, `team-tag`, `member-search`) so each label is programmatically associated, matching the existing `version-increment` pattern. Additive only — flagged here because it is not literally in the contract. |

Nothing in the plan turned out to be impossible, and no part was left unimplemented.

## Plan claims re-checked (all held)

- `api/E3A.Api/Controllers/Teams/TeamsController.cs` exposes all eight endpoints at the routes and verbs the plan lists; `POST /teams` → `CreatedAtAction` (201), `POST /teams/{id}/publish` → `Accepted` (202).
- Wire shapes match the new TS interfaces field-for-field: `TeamResult`, `TeamMemberResult(EngineerId, EngineerSlug, PinnedVersionId, PinnedSemanticVersion, SortOrder)`, `TeamDetailResult`, `CreateTeamRequest`, `UpdateTeamRequest`, `SetTeamMembersRequest`/`TeamMemberRequest`, `PublishTeamRequest`.
- `grep -c "TEAM_"` = **27** in both `Messages.en.resx` and `Messages.ar.resx` (unchanged — no api file was touched).
- All 24 client-mapped codes exist in `ErrorCodes.cs`.
- `git diff --stat main -- api/` → empty. `git diff --stat main -- postman/` → empty.

## Build & test

All commands run from `D:\Personal\_e3a` (web commands in `D:\Personal\_e3a\web`).

| Command | Verbatim outcome |
|---|---|
| `npm run build` (baseline, before changes: n/a) | `tsc -b && vite build` → `✓ 72 modules transformed. ✓ built in 205ms`, **zero TypeScript errors**, first attempt. |
| `npm run test` (baseline on `main`) | `Test Files 12 passed (12)` · `Tests 79 passed (79)` |
| `npm run test` (after) | `Test Files 15 passed (15)` · `Tests 136 passed (136)` — exactly the planned counts. |
| `npx oxlint` (baseline on `main`) | 8 warnings: 4 `react(only-export-components)` (ToastContext, ReportContext, AuthContext, CatalogPage), 3 `react(set-state-in-effect)` (AuthCallbackPage:36, AuthContext:61, EngineerDetailPage:23), 1 `react-hooks(exhaustive-deps)` (CatalogPage:39). |
| `npx oxlint` (after) | **8 warnings — identical rules, identical files, identical lines.** No warning from any file this slice touched. |
| `dotnet test api/E3a.slnx` | `Passed!  - Failed: 0, Passed: 815, Skipped: 0, Total: 815, Duration: 1 s - E3A.Tests.dll (net10.0)` — unchanged from the plan's baseline. |
| `grep -rn "oxlint-disable\|@ts-ignore\|@ts-expect-error" web/src` | no matches. |

### Mutation checks M1–M14 — all performed, all bit

Method: the three production modules were copied to the scratch directory first; each mutation was applied
programmatically, `npm run test` run, the file restored **from the copy** (never retyped), and the restore
verified with `filecmp` per mutation and finally with `cmp` for all three files. Full suite re-run green
afterwards (`15 files / 136 tests`).

| # | Mutation | Expected to break | Observed failing test(s) | Suite line | Restored byte-identical |
|---|---|---|---|---|---|
| M1 | `drafts.length >= maxMembers` → `>` | #4 | `should refuse a new member when the roster is at the cap` | `1 failed \| 135 passed (136)` | yes |
| M2 | deleted the `latestVersionId === null` branch | #2 | `should refuse an engineer with no published version` | `1 failed \| 135 passed` | yes |
| M3 | deleted the duplicate-member branch | #3 | `should refuse an engineer already on the roster` | `1 failed \| 135 passed` | yes |
| M4 | splice move → two-element swap | #9 and #10 | `should move a member to an earlier index and shift the others down`, `should move a member to a later index` | `2 failed \| 134 passed` | yes |
| M5 | dropped `toIndex >= drafts.length` guard | #11 | `should leave the roster unchanged when an index is out of range` | `1 failed \| 135 passed` | yes |
| M6 | removed the `sortOrder` sort | #12 | `should order members by sortOrder rather than array order` | `1 failed \| 135 passed` | yes |
| M7 | `pinnedVersionId: draft.pinnedVersionId` → `null` | #14 | `should send each member's explicit pinned version id in roster order` | `1 failed \| 135 passed` | yes |
| M8 | `--` → `-` in the skills path | #17 | `should namespace each member's skills folder with a double hyphen` | `1 failed \| 135 passed` | yes |
| M9 | team `installCount: null` → `0` | #21 only (#28 must still pass) | `should leave a team install count null so no number is rendered` — #28 stayed green | `1 failed \| 135 passed` | yes |
| M10 | removed the descending `updatedAt` sort | #20 | `should order rows by updatedAt descending across both item types` | `1 failed \| 135 passed` | yes |
| M11 | team `viewPath` → `/t/${team.slug}` | #25 | `should point a team view link at the composer because no public team page exists` | `1 failed \| 135 passed` | yes |
| M12 | action label always `'Publish'` | #23 | `should label the action View status when a latest version exists and Publish when it does not` | `1 failed \| 135 passed` | yes |
| M13 | `'Team'` case falls through to engineer | #30 | `should route a team publish back to the team composer` | `1 failed \| 135 passed` | yes |
| M14 | `'—'` → `'0'` | #28 | `should render an em dash when there is no install count` | `1 failed \| 135 passed` | yes |

No mutation broke nothing; none broke more than the plan predicted. Final `cmp` of all three modules
against the pre-mutation copies: `ALL THREE BYTE-IDENTICAL`.

### Manual verification V1–V7 — what was and was not performed

| # | Check | Status | Evidence |
|---|---|---|---|
| V1 | `npm run build` | **Performed** | Zero TS errors, first attempt. This is the proof that `setTeamMembers(saved.id, toMemberSelections(members))` type-checks as `TeamMemberSelectionInput[]` and that `installCommand(publishedSlug, target.itemType)` accepts `PluginItemType`. |
| V2 | Full happy path against a locally running API | **NOT performed** | Needs a running API + database, which I was instructed not to start. I have **no** response bodies for `POST /api/teams`, `PUT /api/teams/{id}/members`, `POST /api/teams/{id}/publish` or the status poll, and I did not see the rendered `/plugin install e3a-team-{slug}@e3a` line. Nothing in this report should be read as evidence that the flow works end to end. |
| V3 | Keyboard-only pass on the composer | **Performed only statically** | The composer cannot be reached without a signed-in session against a live API, so no real Tab pass was made. What I did verify by reading the emitted markup: every interactive control is a native `<input>`, `<textarea>`, `<select>`, `<button type="button">` or `<Link>`; `grep` for `span onClick` / `div onClick` in `TeamComposerPage.tsx` returns **0**; the ↑/↓ buttons use `disabled` (not removal) at the ends, so focus order is stable. DOM order is name → description → tag input → increment select → member search → each **Add** → each ↑/↓/× → the shell's Save draft/Publish. That is a structural argument, not an observed keyboard pass. |
| V4 | Empty-roster publish | **Half performed** | Static half: `publishDisabled={publishing \|\| saving \|\| members.length === 0 \|\| displayName.trim().length === 0}` is present, and `ComposerShell` renders a real `disabled` button in that branch. Live half **not** performed — provoking a `400 TEAM_EMPTY` and reading the rendered prose needs the API. The mapping itself (`TEAM_EMPTY` → "Add at least one member before publishing this team.") is covered by test row 34. |
| V5 | Workspace round-trip | **NOT performed** | Needs the API. Unproven: that a saved team shows as a `Team` row with `—` installs, and that Edit reloads the roster with `v…` pins. |
| V6 | Reopen after publish | **NOT performed** | Needs a published team. Unproven: that Save draft on a published team avoids `TEAM_SLUG_FROZEN` (Decision 5's reasoning — `updateTeam` always sends `serverSlug` and `UpdateTeamHandler.ResolveSlugChangeAsync` short-circuits on an unchanged slug — was read in the source but not exercised). |
| V7 | `npx oxlint` | **Performed** | 8 warnings, byte-for-byte the same rules/files/lines as the measured `main` baseline. No 9th warning, so no export leaked into a `.tsx`. |

### What no automated test in this slice proves

Restating the plan's own caveat, because it is the honest shape of this change: **no test here touches a
component.** `vitest` runs `environment: 'node'` with `include: ['src/**/*.test.ts']`, so nothing proves
that `TeamComposerPage` actually calls `addMember`, that Publish is really disabled on an empty roster,
that the ↑ button passes `index - 1`, that `persist()` saves metadata before members, or that
`WorkspacePage` renders `WorkspaceRow[]` at all. The 57 new tests constrain pure functions over already-correct
inputs; the defect channel is the caller. V1/V7 (run) and V3/V4-static (read) are the only evidence for the
component layer, and V2/V5/V6 — the checks that would actually exercise the wiring — were **not run**.

## Notes for review

1. **Possible reload race after first save (worth a reviewer's eye).** The plan's `persist()` does
   `createTeam` → `navigate('/workspace/teams/{id}', { replace: true })` → `setTeamMembers(...)`. The
   navigation changes `routeTeamId` from `null` to the new id, which re-fires load Effect A, so `getTeam`
   is in flight concurrently with the members `PUT`. If `getTeam` resolves *after* `setTeamMembers`, its
   `setMembers(toMemberDrafts(team.members))` would overwrite the freshly saved roster with the server's
   pre-save (empty) one — a display-only regression; the server data is already correct. In the ordering I
   expect (read issued later but faster than the write, so it lands first) the correct value wins. I
   implemented the flow exactly as specified rather than inventing a `useRef` load guard the plan does not
   describe. V2 is the check that would expose it, and V2 could not be run.
2. **`aria-label` vs visible text on the ↑ / ↓ / × buttons.** The plan mandates
   `aria-label={\`Move ${draft.engineerSlug} up\`}` etc.; the visible content is the glyph `↑`, which the
   label does not contain. `react-feature.md` §6 says not to add an `aria-label` that does not contain the
   visible text (WCAG 2.5.3). I read that rule as aimed at *textual* labels and followed the plan, since an
   unlabelled arrow button is strictly worse for a screen reader. Flagging it because a reviewer walking §6
   literally will stop here.
3. **`ComposerShell`'s own buttons still lack `type="button"`.** Save draft / Publish / the disabled
   Publish variant are bare `<button>`s in a file this slice does not touch. Not fixed — out of *Existing
   code touched*. They are not inside a `<form>`, so the default `type="submit"` is inert today.
4. **`loadStatus === 'failed'` has no dedicated render branch** in the composer — the error banner carries
   the message and the form renders empty. That mirrors `EngineerComposerPage` exactly, so it is existing
   behaviour rather than something introduced here.
5. **Member search fires on mount with an empty query** (debounce `0` when `memberQuery` is empty), which
   is what populates the initial six candidates. That is the plan's Effect B verbatim; it means one
   `GET /api/catalog?page=1&pageSize=6` per composer open.
6. **`emojiFor(candidate.slug)` replaces the prototype's `@author` line** with `candidate.displayName`,
   because `CatalogEngineer` carries no author field — this is what the plan's render spec asks for, noted
   only so the visual diff is not mistaken for drift.
7. **Docs.** Per `.claude/rules/docs-sync.md` the only divergence this change creates is §33's reorder
   wording, which is updated. The version-pin dropdown (D1), limits meters (D5) and public team surface
   (D4) remain described in `/docs` as targets — that is incompleteness, not divergence, so those lines
   were deliberately left alone.
