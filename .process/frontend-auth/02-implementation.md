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
