VERDICT: CHANGES_REQUESTED

# Review — Frontend Auth & Workspace Wiring

Judged against `.process/frontend-auth/00-acceptance.md`, `01-plan.md`, `02-implementation.md`, and the
working tree. `.claude/skills/dotnet-feature/SKILL.md` was **not** applied — it governs `api/` only, and
`api/` is untouched. Frontend conventions were judged against `docs/constitution.md` and the existing
`web/src` code, per the plan's Convention note.

One blocking finding. Everything else in the plan — including all five declared deviations, the three
login anchors, the build, the 54 tests and the lint baseline — was independently verified and holds.

## Blocking

### 1. The publish-failure panel renders the API's raw SCREAMING_SNAKE error codes to the creator

**Where:** `web/src/features/publish/PublishStatusPage.tsx:121`

    <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>{status.failureReason ?? GENERIC_ERROR_MESSAGE}</span>

**Rule:** acceptance decision 4 ("a raw code shown to a user is a dead end") · plan decision 14
(`01-plan.md:79` — "The raw code is **never** rendered and never interpolated into the text") · plan
Definition of Done (`01-plan.md:557` — "no rendered string ever contains a SCREAMING_SNAKE code").

**Problem:** `PublishStatus.failureReason` is not an `ErrorResponse.message`. It is a raw column on
`ItemVersion` that never passes through `Core.Exceptions.ErrorResponseHandler` or `ILocalizer`, so it is
never localized and never turned into prose. Every value the pipeline can write into it is an
`ErrorCodes` constant:

- `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:37` → `ENGINEER_NOT_FOUND`
- `ProcessPublishJobHandler.cs:54` → `ENGINEER_SNAPSHOT_EMPTY`
- `ProcessPublishJobHandler.cs:62` → `ENGINEER_DRAFT_NOT_UPLOADED`
- `ProcessPublishJobHandler.cs:73` → `string.Join(", ", errors)`, where every element comes from
  `api/E3A.Application/Publishing/Shared/PluginStructureValidator.cs:25,30,35,40,45,50`
  (`PLUGIN_MANIFEST_ASSET_MISSING`, `PLUGIN_NO_INSTALLABLE_CONTENT`, `PLUGIN_UNSAFE_PATH`,
  `PLUGIN_SKILL_MISSING_SKILL_FILE`, `PLUGIN_TOO_MANY_FILES`, `PLUGIN_TOO_LARGE`)

There is no path that writes prose. The `?? GENERIC_ERROR_MESSAGE` fallback only fires when
`failureReason` is null — which, for a `Rejected`/`Failed` version, it never is. So this is not an edge
case: it is the **only** behaviour of the failure panel.

The rest of the slice gets this right. `messageForApiError` is correct because the API's
`ErrorResponse.message` genuinely is localized prose — verified: all the publish and engineer codes have
entries in `api/E3A.Api/Resources/Messages.en.resx`, and `Core.Localization.Localizer.GetMessage` falls
back to the exception message, never to the code. `failureReason` is the one field that bypasses that
pipeline entirely, and it was missed.

This originates in the plan, not in sloppy implementation: `01-plan.md:360` specifies
`status.failureReason ?? GENERIC_ERROR_MESSAGE` verbatim, and the implementer followed it. The fix is
therefore a deviation to declare, not a correction of carelessness — but the Definition-of-Done line it
violates is a plan non-negotiable, so it gates.

**Failure:** upload a `.claude` zip containing only `settings.local.json` (everything gets stripped) and
press Publish. The worker reaches `PluginStructureValidator` with no installable content, writes
`FailureReason = "PLUGIN_NO_INSTALLABLE_CONTENT"`, and `/workspace/publish?versionId=…` renders, as the
entire human explanation of why the publish failed:

> **Publish failed**
> PLUGIN_NO_INSTALLABLE_CONTENT

**Fix:** route `failureReason` through the existing client map instead of rendering it. Smallest change
that also handles the comma-joined multi-code case, in `PublishStatusPage.tsx`:

    function failureText(failureReason: string | null): string {
      if (!failureReason) { return GENERIC_ERROR_MESSAGE; }
      return failureReason
        .split(',')
        .map(part => part.trim())
        .filter(part => part.length > 0)
        .map(part => (/^[A-Z0-9_]+$/.test(part) ? messageForErrorCode(part) : part))
        .join(' ');
    }

and add the six `PLUGIN_*` codes plus `ENGINEER_SNAPSHOT_EMPTY` / `ENGINEER_NOT_FOUND` /
`ENGINEER_DRAFT_NOT_UPLOADED` to `lib/errorMessages.ts` — the English text already exists in
`Messages.en.resx` and can be copied. The `^[A-Z0-9_]+$` guard keeps the branch open for prose should the
API ever start sending it, and unrecognised codes fall to `GENERIC_ERROR_MESSAGE` rather than being
displayed, which is the behaviour decision 14 already asserts by test.

Add one test row under `features/publish/` covering it: a SCREAMING_SNAKE `failureReason` must not appear
in the returned string. Without that assertion the same regression returns silently.

## Non-blocking

- `web/src/lib/initials.test.ts:9` — the case named "should handle a single-word name" passes
  `'mohamed-dive'`, which `initialsFor` splits into *two* words on the hyphen. The genuine single-word
  branch (`'mohamed'` → `'M'`) is untested. The plan wrote the row this way (`01-plan.md:474`); the
  implementer matched it faithfully. Worth one extra case later.
- `web/src/lib/http.ts:72` — the `204` branch (`return undefined as T`) has no test. The plan's test plan
  omitted it and nothing in this slice calls a `204` endpoint (`DELETE /engineers/{id}` is deferred), so
  it is unreachable today. Cover it when the delete action ships.
- `web/.gitignore:26` — `!src/features/publish/` fixes the symptom in the right place, but the root
  `.gitignore:20` `publish/` rule still swallows any future `publish/` directory anywhere else in `web/`
  or `seed/`. A separate chore narrowing that rule to .NET output would remove the trap rather than patch
  one instance of it. Out of scope here.
- `web/src/lib/config.ts:7` — `Number(import.meta.env.VITE_MAX_UPLOAD_MEGABYTES ?? 20)` yields `NaN` for a
  malformed env value, which silently disables the client-side size pre-check and prints "max NaN MB". The
  server's `UploadsOptions.MaxZipSizeMegabytes` still enforces, so nothing unsafe passes. The implementer
  flagged this himself (report note 6) and the plan specified `Number(...)` verbatim.
- `web/src/features/composer/EngineerComposerPage.tsx:70` — `loadStatus = 'failed'` renders the normal
  form plus the inline error bar, with no distinct failure screen. The plan declared the state but never
  specified a UI for it. Flagged, correctly, rather than invented (report note 3).
- `web/src/lib/catalog.ts` — `allItems`, `filterTagNames`, `homeStats`, `featuredEngineers`,
  `engineerVersions`, `engineerMeta` are unreferenced. I checked the `git archive HEAD` baseline: they were
  **already** unreferenced before this slice. Pre-existing, not orphaned here. The plan's DoD phrase
  "every other fixture export is untouched and still referenced" was inaccurate about the repo's prior
  state; the implementer correctly left them alone under acceptance decision 6.

## Verified

Claims from `02-implementation.md` I independently confirmed:

- **`npm run build`** — ran it. `tsc -b && vite build`, 67 modules, zero TypeScript errors. Matches the
  quoted output byte for byte (`index-BqFlrsk4.js`, 312.55 kB).
- **`npm run test`** — ran it. `Test Files 10 passed (10) · Tests 54 passed (54)`. I mapped all 48 plan
  rows to `it` blocks: 7+3+6+10+4+3+2+4+4+5 = 48 rows, 54 cases because row 11 is an `it.each` over 7
  codes. No row added, none dropped. Deviation 5 is accurate.
- **`npm run lint` = 8 warnings, baseline 6** — I did not accept the number. I extracted
  `git archive HEAD web` into a scratch directory and ran the same oxlint binary against it: **6 warnings**.
  Current tree: **8 warnings, 0 errors**. The two new ones are exactly the two claimed —
  `AuthContext.tsx:59` and `AuthCallbackPage.tsx:30`, both `react(set-state-in-effect)`, a rule already
  firing at baseline in `EngineerDetailPage.tsx:23`. No disable comments anywhere in `web/src`.
- **Decision 1 — three login affordances, all plain anchors, no fourth.** `grep -rn gitHubLoginUrl src/`
  returns exactly the three call sites plus the definition. All three are plain anchors with
  `className="btn-primary"` and `href={gitHubLoginUrl()}`: `NavBar.tsx:40`, `RequireAuth.tsx:11`,
  `AuthCallbackPage.tsx:43`. Grep for `window.open`, `iframe`, `sessionStorage` and `document.cookie`
  across `web/src` returns **zero hits**. `auth/github` appears in exactly one place, `authApi.ts:14`. No
  react-router `Link`/`NavLink` and no `fetch` on any login path. I also re-confirmed the premise:
  `Program.cs:85` configures CORS with `.WithOrigins(...).AllowAnyHeader().AllowAnyMethod()` and **no**
  `AllowCredentials`, so no XHR path could ever carry the nonce cookie — the anchor is not a preference,
  it is the only thing that works.
- **`clearAuthFragment()` before any `await`** — `AuthCallbackPage.tsx:21`, immediately after
  `parseAuthFragment` on line 20 and before the `completeSignIn` call on line 24. The token cannot survive
  in the address bar or in history across a slow `/auth/me`.
- **`useRef` StrictMode guard present and actually guarding** — `AuthCallbackPage.tsx:12,15-18`. The ref is
  checked and set before anything else in the effect body; the effect has no cleanup, so the double
  invoke's second pass returns at line 16 and the token is consumed once.
- **`localStorage` in exactly one production file** — `tokenStorage.ts:4,8,12`. The only other hits are the
  two test stubs (`http.test.ts:15`, `tokenStorage.test.ts:11`).
- **`401` vs `403`** — `http.ts:62-65` clears the token and invokes the handler on 401 only, then falls
  through to line 67 and throws `ApiError`. 403 skips the block entirely. Both directions are asserted by
  `http.test.ts:87` and `http.test.ts:120`, the latter checking `handler` not called **and** `removeItem`
  not called.
- **Upload field named `file`** — `workspaceApi.ts:85` `formData.append('file', file)`, matching
  `EngineersController.cs:59` `[FromForm] IFormFile file`.
- **`ENGINEER_DRAFT_NOT_UPLOADED` renders the dropzone** — `EngineerComposerPage.tsx:63-66` catches it and
  calls `setManifest(null)`, which routes to the `UploadDropzone` at line 182. It never reaches
  `setErrorMessage`. `GetImportManifestQueryHandler.cs:36-38` confirms that code is the "no draft yet" 404
  and that ownership is checked first, so a non-owner gets `ENGINEER_NOT_OWNED`, which correctly *does*
  surface as an error.
- **No raw code rendered on the `#error=` / unrecognised-code path** — `errorMessages.ts:15-20` never
  interpolates `code`; `messageForApiError` falls back to the map when the server message is empty
  (`errorMessages.ts:26-27`). Asserted by `errorMessages.test.ts:24-29`, which checks the result does not
  *contain* the input code. This is the one place the requirement is met; finding 1 is the place it is not.
- **Deviation 1 (double toast)** — real and correctly fixed. `ComposerShell.tsx` no longer imports
  `useToast` at all; both callers now toast on their own success path (`EngineerComposerPage.tsx:82` inside
  `.then`, `TeamComposerPage.tsx:32` in `onSaveDraft`). There are exactly two `ComposerShell` callers, so
  no caller was left silent. A failed save no longer says "saved".
- **Deviation 2 (poll cap) — terminates.** `PublishStatusPage.tsx:25,40-44`: `attempts` is a local `let` in
  the effect closure, incremented only on the non-terminal branch and checked before scheduling the next
  `setTimeout`. The diagnosis was right — React state read in a `setTimeout` closure is stale, so the cap
  would have read `0` forever — and the loop provably ends: terminal status returns at line 38, the cap
  returns at line 43, an error returns via the `catch`, and unmount clears the timer at line 54. Chained
  `setTimeout`, never `setInterval`, as required.
- **Deviation 3 (effect deps) — behaviour identical.** `completeSignIn` is a `useCallback` over
  `loadSession`, which is a `useCallback` over `[]` — stable. `navigate` comes from react-router-dom
  `^7.18.2` and is stable. So `[completeSignIn, navigate]` fires exactly once, same as `[]`; and if it ever
  did re-fire, `handledRef` makes it a no-op. Confirmed identical.
- **Deviation 4 (gitignore) — the right fix, the right file, and it fully works.** The claim checks out:
  `git ls-files web/src/features/publish/` returns nothing, and the `git archive HEAD` baseline I extracted
  has no `src/features/publish/` directory at all — `PublishStatusPage.tsx` has never been committed on any
  branch. The negation is correctly placed: git resolves patterns from the deepest `.gitignore` first, and
  the negated pattern in `web/.gitignore` un-excludes the *directory*, so git descends into it.
  Empirically: `git check-ignore -v` on all three files exits 1 (no match), and
  `git add --dry-run web/src/features/publish/` stages all three. Keeping the root `publish/` rule intact
  for .NET output was the right call, and the edit stays inside the sanctioned `web/` path set.
- **Deviation 5 (48 rows to 54 cases)** — arithmetic confirmed above.
- **Scope** — `git status` touches only `web/`, `docs/plugin-spec.md`, `docs/architecture.md`, and
  `.process/frontend-auth/` (`02-implementation.md` new, `04-metrics.md` appended). **No file under `api/`
  is modified.** 27 new files under `web/`: the 26 in *Files to create*, exactly, plus
  `PublishStatusPage.tsx`, which is a rewrite of a pre-existing but never-tracked file. Nothing extra.
- **Dependencies** — the `dependencies` block of `package.json` is unchanged; `vitest ^4.1.11` is the sole
  addition, under `devDependencies`. Every one of the 29 new `package-lock.json` entries carries
  `"dev": true`. No runtime dependency, no jsdom, no testing-library, no data-fetching library. No Azure
  resource, no `az` command, no configuration section.
- **No literal API host in `web/src`** — grep for `localhost` and for an `http`/`https` scheme outside
  `config.ts` and the tests returns nothing. `config.ts:5` is the pre-existing env-backed default.
  `.env.example` updated with the corrected `VITE_API_BASE_URL` and `VITE_MAX_UPLOAD_MEGABYTES=20`.
- **No refresh flow, no JWT decoding, no expiry timer** — grep for `refresh`, `atob`, `jwtdecode` and `exp`
  across `web/src` returns one false positive: the word "Refresh" inside a poll-timeout message string.
- **Deleted fixtures genuinely unreferenced** — `workspaceRows`, `scanFindings`, `pickableSkills`,
  `WorkspaceRow`, `ScanFinding`, `DraftSkill`: zero references anywhere in `web/src` after the change.
  `findByName`, `memberSearchPool`, `teamVersions`, `teamMeta`, `formatInstalls`, `squadMembers`,
  `scanCategories`, `faqEntries` and `reportReasons` are all still referenced.
- **Route guarding** — `App.tsx:61-64` and `68-72`: two pathless `RequireAuth` wrappers cover `/workspace`,
  `/workspace/publish`, `/workspace/new-engineer`, `/workspace/engineers/:engineerId` and
  `/workspace/new-team`. `/`, `/catalog`, `/e/:name`, `/t/:name`, `/u/:login`, `/how`, `/auth/callback` and
  the catch-all are outside it. Exactly the plan's route table.
- **Contract fidelity** — every consumed endpoint exists with the signature the SPA assumes:
  `EngineersController.cs:23,37,44,52,59,66,73`, `PublishController.cs:13`,
  `AuthenticationController.cs:18,45`. Result DTOs match the TypeScript interfaces field for field
  (`EngineerResult`, `CurrentUserResult`, `ImportManifestResult`, `HookWarningResult`). `Increment` is sent
  as the string `"Patch"` and binds because `Program.cs:35` registers `JsonStringEnumConverter`. All ids go
  through `encodeURIComponent`.
- **Report note 4 resolved in the implementer's favour** — he asked someone to check whether
  `GET /engineers/{id}` filters drafts away from their owner. It does not:
  `GetEngineerQueryHandler.cs:23-38` returns a published engineer to anyone and a non-published one to its
  owner, with 403 `ENGINEER_NOT_OWNED` otherwise. Editing an unpublished engineer will work. No defect.
- **`ApiError` default message preserved byte-identically** — `http.ts:69` emits the same
  "API request failed with status {status}" string the deleted `api.ts` `getJson` used, so
  `EngineerDetailPage`'s `error.status === 404` check and the anonymous catalog path are unchanged.
  `api.ts` re-exports `ApiError` from `./http`, so no unrelated import broke.
- **Honesty of the report** — the "what was and was not exercised" section is specific and does not
  oversell. It states plainly that the live GitHub round trip was not completed, that the manual JWT pass
  was **not** run because the API was not listening on `https://localhost:62935`, and that "nothing in this
  slice has been executed against a live API". That absence was a known Stage 0 constraint and is **not** a
  finding. The seven review notes are genuine and specific, and two of them — the gitignore trap and the
  stale poll counter — caught real defects the plan had not anticipated.

## Docs sync

Per `.claude/rules/docs-sync.md`, both planned edits are present and are the only doc changes:

- `docs/plugin-spec.md` § Naming — the `e3a-team-{slug}` sentence, required because `config.ts:20` now
  emits that name. Divergence closed.
- `docs/architecture.md` § "Auth is a fragment handoff." — the SPA storage/session clause (`localStorage`,
  `history.replaceState`, no refresh token, 401 signs out). Divergence closed.

The plan's *incompleteness* calls hold and I am flagging none of them: `docs/design-prompt.md` §8's
upload-only composer, §7's limits meters and New Team button, §10(c)'s per-file scan panel, and the
"folder or .zip" label all describe the target while the code lags. That is the rule's explicit
"never flag missing implementation as a docs problem" case, and no doc should be trimmed to match.

`docs/implementation-plan.md`, `docs/security-scan.md` and `docs/constitution.md` are correctly untouched —
no feature was added, dropped or re-scoped. No doc was created outside `/docs`.

## Postman sync

`postman/e3a.postman_collection.json` is untouched, correctly. This slice adds, changes and removes zero
endpoints — no file under `api/` is modified — so there is no request to add, no contract to reflect and
no orphan to remove. Nothing to sync.

## Test quality

Per file, does it actually constrain the implementation?

- **`lib/http.test.ts`** — the strongest file in the slice and the one that matters most. It pins the
  bearer header's exact value, the absence of the header when signed out, the exact base-URL-plus-path
  concatenation, the serialized JSON body string, the `FormData` instance passed through by identity with
  **no** `Content-Type` (the multipart-boundary trap), 401-clears-and-notifies, 403-does-neither, the
  server `code`/`message` propagation, and the non-JSON error body. Break any one of those in `http.ts` and
  a test goes red. Nothing here is a substitute asserting back its own configured return value.
- **`lib/authFragment.test.ts`** — constrains. The `clearAuthFragment` cases assert `replaceState` was
  called **once** with the exact three arguments including the preserved query string; drop the `search`
  and the test fails.
- **`lib/errorMessages.test.ts`** — the `it.each` row is better than it looks: asserting the result does
  not contain an underscore is a real invariant, not a tautology, and it would catch someone "helpfully"
  interpolating the code into the text. The unknown-code case asserts the code is absent from the output.
- **`lib/tokenStorage.test.ts`** — constrains the storage key (`setItem` asserted with the exact key and
  value). The clear case round-trips through the same Map-backed stub, so it is slightly weaker on its own,
  but the key is pinned by the neighbouring case and `http.test.ts:94` independently asserts
  `removeItem('e3a.token')`.
- **`lib/slug.test.ts`, `lib/initials.test.ts`, `lib/config.test.ts`,
  `features/composer/uploadFileValidation.test.ts`, `features/composer/importManifestStructure.test.ts`,
  `features/publish/publishStage.test.ts`** — pure functions, exact expected values, no stubs to fool. All
  constrain. `uploadFileValidation` covers both rules in both directions plus the case-insensitive
  extension; `publishStage` covers every status the API can emit plus the unknown case;
  `importManifestStructure` covers sort, de-duplication, empty, and the ignore-skipped branch.

**No test in this slice is vacuous.** The gap is coverage, not quality, and it is the subject of finding 1:
nothing asserts that a `failureReason` never reaches the DOM as a raw code. That assertion is what would
have caught it.

## Note for the merge step (not a defect)

`teams` has now merged to `main`. I confirmed on `origin/main` that
`api/E3A.Application/Publishing/Shared/PublishStatusResult.cs` now declares
`(Guid VersionId, Guid ItemId, string ItemType, ...)` — exactly the shape `workspaceApi.ts:53-65` is typed
against. So a `git merge origin/main` on this branch lights up the publish-success install block and the
"Fix and republish" link, both of which are inert today because this branch's API still returns
`engineerId`. Plan decision 10 — guard the lookup, no `itemId ?? engineerId` fallback — was the right call
and needs no change.

Also note that `origin/main`'s `docs/plugin-spec.md` § Naming **already** carries the `e3a-team-{slug}`
rule, worded differently ("`e3a-{slug}` for engineers and `e3a-team-{slug}` for teams ... separate
namespaces"). The merge will conflict textually in that section; the two versions agree on substance, so
take main's wording and drop this branch's added line. That is a merge mechanic, not a review finding.
