# Implementation — Frontend Auth & Workspace Wiring

Branch `feature/frontend-auth`. Nothing under `api/` was touched.

## Files created

26 files, exactly the list in *Files to create*.

| Path | Lines | Purpose |
|---|---|---|
| `web/src/lib/http.ts` | 77 | `ApiError`, `requestJson`, `setUnauthorizedHandler`; bearer attachment, 401 hook, non-JSON error-body tolerance |
| `web/src/lib/tokenStorage.ts` | 13 | The only module in `web/src` that touches `localStorage` (`e3a.token`) |
| `web/src/lib/authFragment.ts` | 13 | `parseAuthFragment` / `clearAuthFragment` (`history.replaceState`) |
| `web/src/lib/errorMessages.ts` | 28 | `GENERIC_ERROR_MESSAGE`, `messageForErrorCode`, `messageForApiError` |
| `web/src/lib/slug.ts` | 11 | `toSlug` — mirrors `EngineerSlugGenerator.Normalize` minus truncation |
| `web/src/lib/initials.ts` | 10 | `initialsFor` for avatar fallbacks |
| `web/src/lib/authApi.ts` | 19 | `CurrentUser`, `gitHubLoginUrl()`, `getCurrentUser()` |
| `web/src/lib/workspaceApi.ts` | 99 | Engineer / manifest / publish types + the 8 authenticated call sites |
| `web/src/app/RequireAuth.tsx` | 25 | Route guard; "Sign in to continue" panel with the anchor |
| `web/src/features/auth/AuthCallbackPage.tsx` | 49 | `/auth/callback`; `useRef`-guarded effect, fragment cleared before any `await` |
| `web/src/features/composer/UploadDropzone.tsx` | 51 | `.zip` dropzone with disabled / busy variants |
| `web/src/features/composer/ImportManifestPanel.tsx` | 72 | Imported / Converted / Skipped, hook warnings, snippet, stripped paths |
| `web/src/features/composer/importManifestStructure.ts` | 6 | `toStructurePaths` |
| `web/src/features/composer/uploadFileValidation.ts` | 11 | `validateUploadFile` (extension before size) |
| `web/src/features/publish/publishStage.ts` | 27 | `PUBLISH_STEP_LABELS`, `stepIndexFor`, `isTerminalStatus`, `isFailedStatus` |
| `web/vitest.config.ts` | 8 | `environment: 'node'`, `include: ['src/**/*.test.ts']` |
| `web/src/lib/authFragment.test.ts` | 50 | Plan rows 1–7 |
| `web/src/lib/tokenStorage.test.ts` | 38 | Rows 8–10 |
| `web/src/lib/errorMessages.test.ts` | 48 | Rows 11–16 |
| `web/src/lib/http.test.ts` | 139 | Rows 17–26 |
| `web/src/lib/slug.test.ts` | 20 | Rows 27–30 |
| `web/src/lib/initials.test.ts` | 16 | Rows 31–33 |
| `web/src/lib/config.test.ts` | 12 | Rows 34–35 |
| `web/src/features/composer/importManifestStructure.test.ts` | 43 | Rows 36–39 |
| `web/src/features/composer/uploadFileValidation.test.ts` | 20 | Rows 40–43 |
| `web/src/features/publish/publishStage.test.ts` | 37 | Rows 44–48 |

## Files modified

| Path | Change |
|---|---|
| `web/package.json` | `vitest@^4.1.11` in `devDependencies`; `"test": "vitest run"` |
| `web/package-lock.json` | resolved `vitest` tree (29 packages, dev-only) |
| `web/.env.example` | `VITE_API_BASE_URL` → `https://localhost:62935/api`; added `VITE_MAX_UPLOAD_MEGABYTES=20` |
| `web/src/lib/config.ts` | `PluginItemType`, `installCommand(slug, itemType = 'Engineer')`, `maxUploadMegabytes` |
| `web/src/lib/api.ts` | dropped private `getJson` and the local `ApiError`; calls `requestJson`; `export { ApiError } from './http'` |
| `web/src/lib/catalog.ts` | deleted `workspaceRows`, `scanFindings`, `pickableSkills` and the now-unused type imports |
| `web/src/lib/types.ts` | deleted `WorkspaceRow`, `ScanFinding`, `DraftSkill` |
| `web/src/app/AuthContext.tsx` | full rewrite — real session, `status`/`user`/`completeSignIn`/`signOut`, unauthorized-handler registration |
| `web/src/App.tsx` | `/auth/callback`, `/workspace/engineers/:engineerId`, two pathless `RequireAuth` wrappers |
| `web/src/components/NavBar.tsx` | plain `<a href={gitHubLoginUrl()}>`, avatar/initials, sign out, loading placeholder |
| `web/src/features/composer/ComposerShell.tsx` | real avatar; `onPublish`, `publishDisabled`, `publishLabel`, `statusLabel` |
| `web/src/features/composer/EngineerComposerPage.tsx` | full rewrite — create → upload → manifest → publish |
| `web/src/features/composer/TeamComposerPage.tsx` | `onPublish` toast (+ the save-draft toast moved here, see deviation 1) |
| `web/src/features/workspace/WorkspacePage.tsx` | full rewrite — real `GET /engineers/mine` list |
| `web/src/features/publish/PublishStatusPage.tsx` | full rewrite — real chained-`setTimeout` polling |
| `web/src/features/detail/TeamDetailPage.tsx` | `installCommand(item.name, 'Team')` |
| `web/.gitignore` | **not in the plan** — un-ignores `src/features/publish/`. See deviation 4; this one matters. |
| `docs/plugin-spec.md` | §Naming: the `e3a-team-{slug}` sentence |
| `docs/architecture.md` | "Auth is a fragment handoff." bullet: the SPA storage/session clause |

## Deviations

| # | Plan said | Reality | What I did |
|---|---|---|---|
| 1 | `ComposerShell` keeps `onSaveDraft(); showToast('Draft saved')`, and `EngineerComposerPage.handleSaveDraft` also toasts `'Draft saved'` on success | Both fire → the toast appears twice, and the shell's copy fires before the request resolves, so a *failed* save would still say "Draft saved" | Removed the unconditional toast from `ComposerShell` (it now just calls `onSaveDraft`) and moved it into each caller's success path. `TeamComposerPage` therefore changed two things, not one: `onPublish` **and** `onSaveDraft` (which now toasts itself, preserving its previous behaviour exactly). |
| 2 | `PublishStatusPage` state includes `attempts`, checked as `if (attempts >= POLL_MAX_ATTEMPTS)` | React state read inside a `setTimeout` closure is stale — the counter would always read `0` and the cap would never trip | Kept the cap, but the counter is a local `let attempts = 0` inside the poll effect. Behaviour is what the plan intended; the value is not React state because nothing renders it. |
| 3 | `AuthCallbackPage` effect deps are `[]` | oxlint `react-hooks(exhaustive-deps)` flags it as a **new** lint finding, and the task requires no new findings | Deps are `[completeSignIn, navigate]`. Both are stable (`useCallback` on a stable `loadSession`; RRv7 `navigate`), and the mandated `handledRef` guard makes a re-run a no-op regardless — behaviour is identical to `[]`. |
| 4 | Only `web/`, the two docs and `.process/` may be touched | **`web/src/features/publish/` is excluded by the root `.gitignore:20` rule `publish/`** (intended for .NET `dotnet publish` output). `git ls-files web/src/features/publish/` is empty: `PublishStatusPage.tsx` has *never been committed*, on any branch. My `publishStage.ts` and `publishStage.test.ts` would have vanished the same way, and a fresh clone does not build — `App.tsx` imports a module that is not in the repo. | Appended `!src/features/publish/` to **`web/.gitignore`** (inside `web/`, so still within the allowed path set) rather than editing the root `.gitignore`, whose `publish/` rule still legitimately covers .NET output elsewhere. Verified: `git check-ignore` no longer matches, and the three files now appear as untracked. **This is a pre-existing repo defect, not one this slice introduced** — but it would have silently swallowed a third of the publish surface. |
| 5 | Test plan is 48 rows | Row 11 is an `it.each` over 7 error codes, which vitest counts as 7 cases | 54 test cases from the 48 planned rows. No row added, none dropped. |

Everything else in the plan was implemented as written, including all signatures.

## Build & test

Run in `D:\Personal\_e3a\web`. All three were actually executed; the output below is verbatim.

```
$ npm install -D vitest
added 29 packages, and audited 61 packages in 17s
found 0 vulnerabilities
→ vitest ^4.1.11, devDependencies only. package.json + package-lock.json updated.
```

```
$ npm run build          # tsc -b && vite build
vite v8.2.2 building client environment for production...
✓ 67 modules transformed.
dist/index.html                   0.93 kB │ gzip:  0.50 kB
dist/assets/index-KNzsuVkz.css    4.69 kB │ gzip:  1.41 kB
dist/assets/index-BqFlrsk4.js   312.55 kB │ gzip: 91.76 kB
✓ built in 123ms
```
Zero TypeScript errors.

```
$ npm run test           # vitest run
Test Files  10 passed (10)
     Tests  54 passed (54)
```

```
$ npm run lint           # oxlint
```
**8 warnings, 0 errors.** Baseline (oxlint run against a `git archive HEAD` checkout of `web/`) was **6 warnings**. So **2 new warnings**, both `react(set-state-in-effect)` — a rule that already fires at baseline in `EngineerDetailPage.tsx:23`:

- `src/app/AuthContext.tsx:59` — `useEffect(() => { void loadSession(); }, [loadSession])`; `loadSession`'s no-token branch sets state before its first `await`. This is the plan's verbatim `AuthContext` contract.
- `src/features/auth/AuthCallbackPage.tsx:30` — `setErrorMessage(messageForErrorCode(fragment.errorCode))`. Unavoidable: the Definition of Done mandates a `useRef`-guarded effect that reads the hash and clears it before any `await`, so the error path has to land in state from inside that effect.

I did **not** silence either with a disable comment or restructure them away, because both shapes are explicitly specified. I fixed the two new findings that were *not* mandated (a `setStatus('loading')` in `WorkspacePage`'s effect, moved to the Retry handler; and the `exhaustive-deps` one, deviation 3).

Diff surface — `git status` touches only `web/`, `docs/plugin-spec.md`, `docs/architecture.md` (and this file under `.process/`). No file under `api/` is modified. No new runtime dependency. No Azure resource, no `az` command.

## What was and was not exercised of the GitHub round trip

**The live round trip was not completed. It cannot be, without a human at the GitHub consent screen.**

| Segment | Status |
|---|---|
| Building the login URL | Verified by reading the rendered `href` and by test 19's base-URL assertion. Not exercised against a live server. |
| Nonce cookie set by `/api/auth/github/login` and returned on the callback | **Not exercised.** Structural guarantee only — the affordance is a plain `<a href>` (below). |
| GitHub consent → `/api/auth/github/callback` → `302 …#token=` | **Not exercised.** Requires a human and a registered OAuth app. |
| Token arriving in the fragment | Tests 1–5, replaying the exact strings `AuthenticationRedirectUrlGenerator.Success/Failure` emit. |
| Fragment cleared | Tests 6–7. |
| Storage, bearer attachment, 401 handling | Tests 8–10, 17–26. |
| `#error=` rendering | Tests 11–13, against the six real `ErrorCodes` constants (verified present in `ErrorCodes.cs`). |
| Workspace → upload → publish → poll | **Not exercised.** See below. |

**I did not run the manual JWT pass.** I probed `https://localhost:62935/api/catalog/tags` and the API is not running on this machine (`curl` → connection failed), so there was no server to paste a token against and no database row to authenticate as. Nothing in this slice has been executed against a live API — the workspace, composer and publish-status pages have never rendered against real data. **Do not read this slice as working end to end.**

Decision 1 was implemented exactly: `grep -rn gitHubLoginUrl src/` shows three call sites (`NavBar.tsx:40`, `RequireAuth.tsx:11`, `AuthCallbackPage.tsx:43`) and all three are a plain `<a className="btn-primary" href={gitHubLoginUrl()}>`. There is no `fetch`, no `<Link>`/`<NavLink>`, no `window.open` and no iframe on any login path. `window.open` and `iframe` appear nowhere in `web/src`.

## Notes for review

1. **The publish-success install block will not render until the `teams` slice merges.** `PublishStatusResult.cs` on this branch still declares `EngineerId`, so today's JSON has no `itemId`; `getEngineer(status.itemId)` will 400/404 and the `.catch(() => undefined)` leaves `engineer === null`, which hides the `InstallBlock` and the "View in catalog" button. The stepper, the version badge and the "Published" panel all still render. Per plan decision 10 there is deliberately **no** `itemId ?? engineerId` fallback. Same applies to "Fix and republish" on a failed publish, whose link target is `/workspace/engineers/{status.itemId}`. **This is expected, not a bug.**
2. **`web/.gitignore`** (deviation 4) is the one change outside the sanctioned file list. If you would rather fix it at the root, the equivalent is narrowing `.gitignore:20` from `publish/` to a rooted or `.NET`-scoped pattern — but note that `PublishStatusPage.tsx` is currently untracked on `main` too, so whichever way it is fixed, that file needs to be added in this commit or the branch will not build from a clean clone.
3. `EngineerComposerPage` sets `loadStatus = 'failed'` when `GET /engineers/{id}` fails, but the render path for `'failed'` is the normal form plus the inline error bar — there is no dedicated failure screen. The plan lists the state but never specifies a distinct failure UI for it. Flagging rather than inventing one.
4. `GET /api/engineers/{engineerId}` is `[AllowAnonymous]` on the controller. The composer relies on it returning a *draft* engineer to its owner; I read the controller but did not read `GetEngineerQueryHandler` closely enough to confirm it does not filter drafts out for the owner. If it does, editing an unpublished engineer will 404. Worth one check.
5. `messageForApiError` prefers the server's localized `message` over the client map. For a 422 the API joins codes with `,` and messages with `" , "` — the joined *message* is what gets shown, which is readable but will look like one long run-on sentence. Acceptable per decision 13; noting it because it is the one place the user-visible string is not hand-written.
6. `config.maxUploadMegabytes` is `Number(...)`; a malformed `VITE_MAX_UPLOAD_MEGABYTES` yields `NaN`, which makes `validateUploadFile`'s size comparison always false (no client-side size rejection) and prints "max NaN MB". The server's `UploadsOptions.MaxZipSizeMegabytes` remains the enforcing authority, so nothing unsafe gets through — but it is an ugly failure mode. The plan specified `Number(...)` verbatim; I did not add a guard.
7. `vitest.config.ts` is not in any `tsconfig` project (`tsconfig.node.json` includes only `vite.config.ts`), so it is never type-checked. Adding it would have meant modifying `tsconfig.node.json`, which is not on the touched-files list. Left alone deliberately.
8. Anonymous browsing: `requestJson` attaches `Authorization` only when a token exists, and `catalog.ts`'s surviving fixtures are all still referenced (`findByName` by `TeamDetailPage`, `memberSearchPool` by `TeamComposerPage`, etc.). `/`, `/catalog`, `/e/:name`, `/t/:name`, `/how`, `/u/:login` and `/auth/callback` are outside `RequireAuth`. The only behavioural change on the anonymous path is that catalog `ApiError`s now carry a server `code`/`message` when the API sends one; the default message string is byte-identical to the old one, so `EngineerDetailPage`'s `error.status === 404` check is unaffected.

---

## Rework round 1

Addressed the single blocking finding from `03-review.md`, plus the merge of `origin/main`
requested with it. Nothing else was touched: every non-blocking review item (the `initials`
single-word row, the untested `204` branch, the root `.gitignore` `publish/` trap, `Number(...)`
to `NaN`, the absent `loadStatus: 'failed'` screen, the pre-existing unreferenced fixtures) is
left exactly as it was.

### Findings addressed

| # | What I changed | Where |
|---|---|---|
| 1 | Added a pure, exported `failureText(failureReason)`: splits on `,`, trims, drops empties, maps any token matching `^[A-Z0-9_]+$` through `messageForErrorCode`, passes anything else through unchanged, joins with a space, and returns `GENERIC_ERROR_MESSAGE` when nothing is left. | `web/src/features/publish/publishStage.ts:29-36` |
| 1 | Added the nine codes the pipeline can write into `FailureReason` to the client map — the six `PLUGIN_*` from `PluginStructureValidator` and the three `ENGINEER_*` from `ProcessPublishJobHandler` — using the **exact English wording from `api/E3A.Api/Resources/Messages.en.resx`**, so the SPA and the API say the same sentence. | `web/src/lib/errorMessages.ts:13-21` |
| 1 | The failure panel now renders `{failureText(status.failureReason)}` instead of `{status.failureReason ?? GENERIC_ERROR_MESSAGE}`; the now-unused `GENERIC_ERROR_MESSAGE` import was dropped. | `web/src/features/publish/PublishStatusPage.tsx:5,7,121` |
| 1 | Added four `it` cases under `describe('failureText')`: no SCREAMING_SNAKE survives (single code, and the three-code comma-joined case, asserted with `not.toMatch(/[A-Z0-9]+_[A-Z0-9_]*/)`); the exact joined prose for two codes; a prose reason passes through unchanged; `null` and whitespace fall to the generic message. | `web/src/features/publish/publishStage.test.ts:39-63` |

`failureText` lives in `publishStage.ts` rather than in the component, so it is covered by the
existing `environment: 'node'` runner — the component itself remains untestable under it, which
is why the transformation could not stay inline. No new file was created: the function and its
tests went into the two files this slice already owns in `features/publish/`.

### Deviation from the plan (finding 1's origin)

| # | Plan said | Reality | What I did |
|---|---|---|---|
| 6 | `01-plan.md:360` specifies the failure panel verbatim as `{status.failureReason ?? GENERIC_ERROR_MESSAGE}` | `PublishStatus.failureReason` is a raw `ItemVersion` column. It never passes through `ErrorResponseHandler` or `ILocalizer`, so it is never localized prose. Every value the pipeline writes is an `ErrorCodes` constant — `ENGINEER_NOT_FOUND` / `ENGINEER_SNAPSHOT_EMPTY` / `ENGINEER_DRAFT_NOT_UPLOADED`, or `string.Join(", ", errors)` over `PluginStructureValidator`'s six `PLUGIN_*` codes. No path writes prose, and the `?? GENERIC_ERROR_MESSAGE` fallback only fires on null, which a `Rejected`/`Failed` version never is. So the plan's expression renders a raw code **every time the panel is shown**, contradicting the same plan's decision 14 (`01-plan.md:79`) and its Definition of Done (`01-plan.md:557`). | Corrected the plan: the value goes through `failureText` before it reaches the DOM. This is a plan defect I am deviating from, not an implementation slip — the previous round followed the plan as written. Decision 14 and the DoD line are the authority the plan itself set, and they win over line 360. |

### Merge of `origin/main`

The working tree was uncommitted, so a merge was impossible without a commit first. I committed
the round-1 work plus this round's fix as `b611ab0`, then ran `git merge origin/main`, resolved,
and committed the merge as `fa77271`. Both are on `feature/frontend-auth`; nothing was pushed.

One conflict, but **not** the one predicted. `docs/plugin-spec.md` auto-merged; the textual
conflict landed in `docs/architecture.md` "Principles" instead, where main added "10 members per
team" to the **Limits** bullet while this branch appended the SPA session clause to the adjacent
**Auth is a fragment handoff** bullet. Resolved by keeping both: main's Limits line verbatim and
this branch's auth line (whose prefix is byte-identical to main's).

The plugin-spec duplication was still real, just silent — the auto-merge left main's
"separate namespaces" sentence *and* this branch's "one flat namespace" sentence in the same
section, which **contradict each other**. Per instruction I deleted this branch's sentence and
kept main's wording. `docs/plugin-spec.md` is now byte-identical to `origin/main`.

`origin/main` touched **no file under `web/`** since the merge base (`git diff --stat <base> MERGE_HEAD -- web`
is empty), so the merge could not have broken the SPA, and `git diff origin/main...HEAD -- api`
is empty — confirming again that no file under `api/` is modified by this branch.

### The previously-dark paths, verified by reading the merged contract

I could not render these; the API is not running here (see below). What I did was read the merged
source:

- `api/E3A.Application/Publishing/Shared/PublishStatusResult.cs` now declares
  `(Guid VersionId, Guid ItemId, string ItemType, int VersionNumber, string SemanticVersion, string Status, string? ZipUrl, string? ZipSha256, long SizeBytes, string? FailureReason, DateTimeOffset UpdatedAt)`
  — field for field, in order, the `PublishStatus` interface at `web/src/lib/workspaceApi.ts:53-65`.
  `PublishStatusResultGenerator.Generate` populates `ItemId` from `version.ItemId`, so for an
  engineer publish it is the engineer id `getEngineer` expects.
- Every endpoint the SPA calls still exists post-merge: `EngineersController` lines 23, 37, 45,
  52, 59, 66, 73 and `PublishController.cs:13` (`GET api/publish/{versionId:guid}/status`).
  `EngineerResult`, `CurrentUserResult`, `ImportManifestResult` and `HookWarningResult` are
  unchanged and still match their TypeScript interfaces field for field.

So `getEngineer(status.itemId)` should now resolve and the install block plus "Fix and republish"
should light up. **I am asserting the contract, not the rendering** — nothing was executed against
a live API.

### Build & test (after the merge)

Run in `D:\Personal\_e3a\web`. Verbatim.

```
$ npm run build          # tsc -b && vite build
vite v8.2.2 building client environment for production...
✓ 67 modules transformed.
dist/index.html                   0.93 kB │ gzip:  0.50 kB
dist/assets/index-KNzsuVkz.css    4.69 kB │ gzip:  1.41 kB
dist/assets/index-nYcqelUk.js   313.32 kB │ gzip: 92.08 kB
✓ built in 112ms
```
Zero TypeScript errors.

```
$ npm run test           # vitest run
Test Files  10 passed (10)
     Tests  58 passed (58)
```
**58**, up from 54: the four new `failureText` cases. No existing case was changed or removed.

```
$ npx oxlint
```
**8 warnings, 0 errors** — unchanged from round 1, still the same two new ones over the baseline of
6 (`AuthContext.tsx:59`, `AuthCallbackPage.tsx:30`, both `react(set-state-in-effect)`). The cap of
8 is met exactly, not exceeded. No `eslint-disable` or `oxlint-disable` comment was added anywhere.

### Still not exercised against a live API

Unchanged and worth restating plainly: **the manual JWT pass was not re-run and the live GitHub
round trip has still not been completed.** The API is not listening on this machine, so there is no
server to authenticate against and no database row to be. Nothing in this slice — including the
failure panel I just fixed — has rendered against real data. The `failureText` behaviour is proven
by unit test and by reading the values `ProcessPublishJobHandler` writes, not by seeing a failed
publish in a browser.

### Notes for review

1. `failureText` joins mapped sentences with a single space, so a three-code rejection reads as
   three consecutive sentences. That is the reviewer's prescribed shape and is readable, but it is
   the one string in the panel that is assembled rather than authored.
2. The `^[A-Z0-9_]+$` guard means a single-word prose reason that happens to be all-caps with an
   underscore would be treated as a code and, being unmapped, would collapse to the generic
   message rather than being shown. That is deliberate — decision 14 prefers losing an unknown
   token to displaying it — but it is the trade-off.
3. **Not fixed, and not part of finding 1:** `PublishStatusPage` calls `getEngineer(status.itemId)`
   and links "Fix and republish" to `/workspace/engineers/{status.itemId}` without checking
   `status.itemType`. Now that main has landed teams, a **team** version id reaching this page
   would take the engineer path — the lookup 404s into the existing `.catch(() => undefined)` so
   the install block simply stays hidden, but the "Fix and republish" link would point at the wrong
   route. Nothing in the SPA can produce a team version id today (`TeamComposerPage.onPublish` only
   toasts; no team publish call exists in `workspaceApi.ts`), so it is unreachable. I left it alone
   because rework is scoped to the numbered finding — flagging it as a follow-up for whoever wires
   team publishing.
4. The two commits (`b611ab0`, `fa77271`) were made because merging requires a clean tree; the
   commit was a means to the merge that was asked for, not an independent decision to commit.
   Neither was pushed.

## Rework round 2

Scoped to the single blocking finding in `03-review-r2.md`, plus the completeness sweep it asked for.

| # | Finding | What I changed | File:line |
|---|---|---|---|
| 1 | `PLUGIN_DUPLICATE_PATH` reaches the failure panel with no client mapping | Added the code to the client map, wording copied verbatim from `Messages.en.resx:247` | `web/src/lib/errorMessages.ts:22` |
| 1 | ...and the enumeration was pinned by a reviewer rather than a test | Extended the no-raw-code assertion with the single code and the compound `PLUGIN_DUPLICATE_PATH, PLUGIN_NO_INSTALLABLE_CONTENT`, and pinned the exact compound prose in the join test | `web/src/features/publish/publishStage.test.ts:46,48,57-59` |
| 1 | The round-1 claim of "six `PLUGIN_*` ... and the three `ENGINEER_*`" (line 159) is wrong on the merged tree | Correction below; the earlier text is left exactly as written | — |

### Correction to the round-1 claim

`02-implementation.md:159` says the fix covered "the nine codes the pipeline can write into
`FailureReason` — the **six** `PLUGIN_*` from `PluginStructureValidator` and the three `ENGINEER_*`".
That was true against the pre-merge tree and is **not** true of the merged tree. On HEAD the engineer
path can write **seven** `PLUGIN_*` codes and **ten** codes in total. The seventh is
`PLUGIN_DUPLICATE_PATH`, added by the teams merge at `ErrorCodes.cs:107` and emitted at
`PluginStructureValidator.cs:40` inside the `Validate(files, options)` overload — which the engineer
path chains into from `PluginStructureValidator.cs:29`, reached from `EngineerPublishBuilder.cs:41`.
It is not a teams-only code. Read "seven and ten", not "six and nine".

### Completeness sweep

Every `ErrorCodes` constant that can reach `ItemVersion.FailureReason` on the merged tree.
`MarkFailed` has exactly one caller — `ProcessPublishJobHandler.FailAsync` (line 96), fed only by
`build.FailureReason` (line 49) — so the reachable set is exactly what the two builders and the two
validator overloads can return. `PUBLISH_VERSION_NOT_FOUND` is excluded: it is thrown as a
`NotFoundCoreException` (`ProcessPublishJobHandler.cs:24`) and never stored.

| Code | Emitted at | SPA-reachable today | In client map | String vs resx |
|---|---|---|---|---|
| `ENGINEER_NOT_FOUND` | `EngineerPublishBuilder.cs:21` | yes | yes | exact |
| `ENGINEER_SNAPSHOT_EMPTY` | `EngineerPublishBuilder.cs:28` | yes | yes | exact |
| `ENGINEER_DRAFT_NOT_UPLOADED` | `EngineerPublishBuilder.cs:35` | yes | yes | exact |
| `PLUGIN_MANIFEST_ASSET_MISSING` | `PluginStructureValidator.cs:25` (manifest overload) | yes | yes | exact |
| `PLUGIN_DUPLICATE_PATH` | `PluginStructureValidator.cs:40` | yes | **yes — added this round** | exact |
| `PLUGIN_NO_INSTALLABLE_CONTENT` | `PluginStructureValidator.cs:45` | yes | yes | exact |
| `PLUGIN_UNSAFE_PATH` | `PluginStructureValidator.cs:50` | yes | yes | exact |
| `PLUGIN_SKILL_MISSING_SKILL_FILE` | `PluginStructureValidator.cs:55` | yes | yes | exact |
| `PLUGIN_TOO_MANY_FILES` | `PluginStructureValidator.cs:60` | yes | yes | exact |
| `PLUGIN_TOO_LARGE` | `PluginStructureValidator.cs:65` | yes | yes | exact |
| `TEAM_NOT_FOUND` | `TeamPublishBuilder.cs:22` | no | **no — deliberate** | n/a |
| `TEAM_ROSTER_INVALID` | `TeamPublishBuilder.cs:29` | no | **no — deliberate** | n/a |
| `TEAM_EMPTY` | `TeamPublishBuilder.cs:34` | no | **no — deliberate** | n/a |
| `TEAM_MEMBER_VERSION_NOT_PUBLISHED` | `TeamPublishBuilder.cs:50` | no | **no — deliberate** | n/a |
| `TEAM_MEMBER_MANIFEST_INVALID` | `TeamPublishBuilder.cs:57` | no | **no — deliberate** | n/a |
| `TEAM_MEMBER_SNAPSHOT_EMPTY` | `TeamPublishBuilder.cs:64` | no | **no — deliberate** | n/a |

Diff after this round: **zero** SPA-reachable codes unmapped. The ten reachable strings were
re-compared character by character against `Messages.en.resx` by script; all ten are identical,
including the typographic apostrophe in `ENGINEER_NOT_FOUND`. The map now holds 17 entries — the
seven auth/session codes from the callback path plus these ten.

### Known-unmapped, and why

The six `TEAM_*` codes above are **deliberately not mapped**. `web/src/lib/workspaceApi.ts` exposes
only `publishEngineer` (line 93) and the status poll (line 98); there is no team publish call
anywhere in `web/src`, `TeamComposerPage.onPublish` only raises a toast, and `WorkspacePage` lists
engineers only. No team `versionId` can reach `/workspace/publish`, so none of these six can render
today. Mapping them now would create the mirror-image drift: strings copied from a resx with no
reachable path and no test able to exercise them, rotting silently until team publishing lands. They
belong in the commit that adds the team publish call, where a real failure line can be written for
each. `TEAM_MEMBER_VERSION_NOT_PUBLISHED` carries a `{engineerId}` placeholder that the client map
has no formatter for — a second reason to defer rather than half-map it. This is a decision, not an
oversight.

### Build & test — round 2

Run from `D:\Personal\_e3a\web`.

- `npm run build` — passed. `tsc -b && vite build`, 67 modules transformed, **zero TypeScript
  errors**. `dist/assets/index-Butcx_Ua.js` 313.39 kB (gzip 92.11 kB); the changed hash and the
  0.07 kB growth over round 2's `index-nYcqelUk.js` are the one added map entry.
- `npm run test` — `Test Files 10 passed (10)` · `Tests 58 passed (58)`. **58**, unchanged from round
  2 by design: the fix added assertions inside two existing `it` blocks rather than new blocks, so
  the count the reviewer measured is arithmetically unchanged. I checked the new assertions actually
  bite by deleting the map line and re-running the file: `Tests 1 failed | 8 passed (9)`, then
  restored it. Worth being precise about **which** assertion failed — see note 4.
- `npx oxlint` — **8 warnings, 0 errors**, identical to round 2 (the same two
  `react(set-state-in-effect)` at `AuthContext.tsx:59` and `AuthCallbackPage.tsx:30` over the
  baseline six). Grep for `eslint-disable`, `oxlint-disable`, `@ts-ignore`, `@ts-expect-error`
  across `web/src` returns **zero** — nothing was silenced.
- `git diff --name-only -- api` is **empty**: not one file under `api/` was modified. `package.json`
  untouched — no new package. No Azure resource, no `/docs` change, `postman/` untouched.

Files changed this round, and nothing else:

| Path | Change |
|---|---|
| `web/src/lib/errorMessages.ts` | +1 line: `PLUGIN_DUPLICATE_PATH` mapped verbatim from `Messages.en.resx:247` |
| `web/src/features/publish/publishStage.test.ts` | +5 assertion lines across two existing `it` blocks; no block added or removed |

### Notes for review — round 2

1. I placed `PLUGIN_DUPLICATE_PATH` last among the `PLUGIN_*` entries, matching its position in
   `ErrorCodes.cs` (line 107, after `MARKETPLACE_ENGINEER_LIMIT_EXCEEDED`) rather than its emission
   order in the validator, where it is first. Map order has no behavioural effect; I optimised for
   reading in the same order as the constants file.
2. The completeness sweep is still a **point-in-time enumeration**, now recorded as a table instead
   of a sentence. The test pins the codes it names, but nothing mechanically fails when the API adds
   an eleventh reachable code — the same class of defect could recur on the next merge. The durable
   fix is round 2's non-blocking suggestion (drive `errorMessages.test.ts`'s `it.each` off
   `Object.keys`, or better, generate the map from the resx at build time), which is outside this
   rework's scope. Flagging it rather than doing it.
3. Round 2's other non-blocking items — the hand-maintained `callbackErrorCodes` list of 7, and the
   `status.itemType` branch at `PublishStatusPage.tsx:36,125` — were left untouched by instruction.
   Note 3 of round 1 stands as the follow-up for whoever wires team publishing.
4. The two `not.toMatch(screamingSnake)` assertions I added for `PLUGIN_DUPLICATE_PATH` do **not**
   catch a missing mapping, and I confirmed that empirically: with the map line deleted, they still
   pass, because `failureText` falls back to the generic message, which contains no snake-case
   token. That is the review's own point — the DoD line held while the purpose broke. The assertion
   that actually goes red is the exact-prose compound one at `publishStage.test.ts:56-59`. So the
   enumeration is pinned by the prose assertion, not by the raw-code one; the raw-code additions are
   defence in depth against a future change to the fallback, not the guard. I kept both, but the
   reviewer should read line 56-59 as the load-bearing test.
5. The round-1 disclosure is unchanged and I am not softening it: **the manual JWT pass was not
   re-run and the live GitHub round trip has not been completed.** The duplicate-path panel has
   never rendered in a browser; it is proven by unit test and by reading the path from
   `PluginStructureValidator.cs:40` through `ProcessPublishJobHandler.cs:49,96` to
   `PublishStatusPage.tsx:121`.
