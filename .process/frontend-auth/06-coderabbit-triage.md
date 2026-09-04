TRIAGE: 7 to implement, 5 rejected, 1 dev-decisions

# CodeRabbit triage — PR #9 (`frontend-auth`), final cycle

Judged against `docs/constitution.md`, `docs/design-prompt.md`, `00-acceptance.md` and the plan's
derived frontend conventions (`01-plan.md:10-19`). `.claude/skills/dotnet-feature/SKILL.md` governs
`api/` only and was **not** applied. Every claim below was checked against the tree at `6baf338`
(working tree clean apart from `05-coderabbit-comments.md`).

Gates measured before triage, so the rework has a baseline: `npm run test` gives `Test Files 10
passed (10) · Tests 58 passed (58)`; `npx oxlint` gives **8 warnings, 0 errors**. Both must be
unchanged after the rework. No fix below adds a test — the runner is `environment: node` with no DOM
library, so none of the seven is unit-testable; say that in the rework report rather than inflating
the count.

---

## IMPLEMENT

### 1. RC4 — `completeSignIn` resolves even when the session could not be established

**Where:** `web/src/app/AuthContext.tsx:39-45` (the `catch` that swallows every `/auth/me` failure and
sets `signedOut`), `AuthContext.tsx:54-57` (`completeSignIn` awaits it and resolves `void`),
`web/src/features/auth/AuthCallbackPage.tsx:24-27` (`.then(() => navigate('/workspace'))`).

**Verified:** correct as reported, and worse than reported. `loadSession` never rethrows, so the
`.catch` on `AuthCallbackPage.tsx:26` is an **unreachable branch** — `messageForApiError` can never
run on the token path. On any non-401 failure the token is deliberately *kept* (plan decision 4,
`01-plan.md:69`), so the callback stores a token, reports success, navigates to `/workspace`, and
`RequireAuth.tsx:24` renders the "Sign in to continue" panel (`RequireAuth.tsx:9-11`) whose only
control is another Sign-in-with-GitHub anchor. Round trip, new token, same failure, same panel. That
is precisely the silent loop plan decision 8 (`01-plan.md:73`) was written to prevent: "with no error
ever visible". The creator is told nothing.

**Failure:** API up but `GET /api/auth/me` returns 500, or the network drops mid-callback. Land on
`/auth/callback#token=...`, get sent to `/workspace`, see "Sign in to continue" forever, with a valid
token in `localStorage` and no message anywhere.

**Fix (smallest that resolves it):** make the result explicit rather than throwing.
- `AuthContext.tsx`: `loadSession` returns `AuthStatus` (`setStatus(x); return x;` on all three
  exits); `completeSignIn` returns that value; widen the interface to
  `completeSignIn: (token: string) => Promise<AuthStatus>` and the default context value at
  `AuthContext.tsx:22` to `async () => 'signedOut'`.
- `AuthCallbackPage.tsx:24-27`: in the `.then`, navigate only when the resolved status is
  `signedIn`, otherwise `setErrorMessage(GENERIC_ERROR_MESSAGE)`. Keep the existing `.catch`.
  `GENERIC_ERROR_MESSAGE` is already exported from `lib/errorMessages.ts:3`.

Keep that `setErrorMessage` inside the `.then` callback — a synchronous `setState` in the effect body
would add a third `react(set-state-in-effect)` warning and breach the cap of 8.

### 2. RC7 — the Save-draft button accepts concurrent clicks and creates duplicate engineers

**Where:** `web/src/features/composer/EngineerComposerPage.tsx:74-90` (no in-flight guard),
`web/src/features/composer/ComposerShell.tsx:45` (`<button onClick={onSaveDraft}>` with no
`disabled`; `saving` only feeds the cosmetic `statusLabel` chip, `EngineerComposerPage.tsx:138`).

**Verified:** real and reachable. `engineerId` is still `null` during the first `POST /engineers`, so
a second click takes the `createEngineer` branch again (`EngineerComposerPage.tsx:77`).

**Failure:** double-click **Save draft** on `/workspace/new-engineer` with the name "Payments
Engineer". Two `POST /engineers` fire; `EngineerSlugResolver` uniquifies the second, so the creator
ends up with `payments-engineer` **and** `payments-engineer-2`, and whichever response resolves last
wins the `navigate(..., { replace: true })` at line 85 — the other draft is orphaned and the creator
never learns it exists.

**Fix:** `if (saving) { return; }` as the first line of `handleSaveDraft`, plus a
`saveDisabled?: boolean` prop on `ComposerShell` (mirroring the existing `publishDisabled`,
defaulting to `false` so `TeamComposerPage` is untouched) applied to the button at
`ComposerShell.tsx:45`, passed as `saveDisabled={saving}`.

### 3. RC8 — the upload selector is not keyboard operable, which hard-blocks the publish flow

**Where:** `web/src/features/composer/UploadDropzone.tsx:31-39` — a `<div onClick>` whose only job is
to click a `display: none` input (line 39). Neither element can take focus.

**Verified, and this is the finding that decides the accessibility cluster.** Walk the dev's
confirmed flow (`00-acceptance.md:41`) with a keyboard only: the sign-in anchor works, `+ New
Engineer` is a real `<button>` (`WorkspacePage.tsx:37`), name/description/tags are real inputs, Save
draft is a real button — then it **stops**. There is no focusable path to the file picker, and
Publish stays disabled while `manifest === null` (`EngineerComposerPage.tsx:136`). A creator who
cannot use a mouse cannot publish at all. That is a functional gap on this slice's own surface, not
polish, and "the codebase does this elsewhere" does not excuse it on the one control that gates the
product's core action.

**Fix:** keep the `<div>` for drag-and-drop (dropping is inherently pointer-only and that is fine),
and put a real control inside it for the picker: a `<button type="button">` that is
`disabled={disabled || busy}` and whose handler calls `event.stopPropagation()` before
`inputRef.current?.click()`. The `stopPropagation` is required — without it the click bubbles to the
div's own handler on line 32 and the file dialog opens twice. Style it from a module-level
`React.CSSProperties` constant that resets background/border/padding and sets `fontFamily: 'inherit'`
plus an explicit `fontSize`; never use the `font` shorthand, or React warns about shorthand/longhand
conflicts on re-render. `index.css` stays untouched.

### 4. RC8 (second half) — the three manifest expanders

**Where:** `ImportManifestPanel.tsx:17` (section header `<div onClick>`), `:57` (the "view snippet"
`<span onClick>`), `:63` (the "N local files stripped" `<span onClick>`).

**Verified:** all three toggle `expanded` state and none is focusable. Reviewing the import manifest
is step 3 of the dev's confirmed flow (`00-acceptance.md:41`), so a keyboard user could upload (after
finding 3) but still could not open Imported / Converted / Skipped to see what was actually taken.

**Fix:** `<button type="button" aria-expanded={...} onClick={() => toggle(key)}>` at all three sites.
The section header additionally needs `width: 100%` and `textAlign: left` folded into the existing
`sectionHeaderStyle` constant (`ImportManifestPanel.tsx:4`) along with the same background/border/font
reset; the two inline expanders keep `className="link-quiet"` and their existing inline colour and
size. No visual change intended.

### 5. RC5 — sign-out and profile navigation in the nav bar

**Where:** `web/src/components/NavBar.tsx:43` (`<span onClick={handleSignOut}>`), `:45` (`<img
onClick={openProfile}>`), `:46` (`<div onClick={openProfile}>`).

**Verified:** correct. A signed-in keyboard user can neither sign out nor reach their profile.
Sign-out is also the only client-side way to clear the token (`AuthContext.tsx:48-52`), so this is a
security affordance, not just a convenience.

**Fix:** sign-out becomes `<button type="button" onClick={handleSignOut} className="link-quiet">`
with the reset style plus its current `fontSize` and `color`. The profile avatar gets wrapped in a
react-router `<Link>` to the same `/u/{login}` target with an `aria-label` such as "Open your
profile" and `display: inline-flex`; both `onClick={openProfile}` handlers and the now-unused
`openProfile` helper on line 25 are deleted (`useNavigate` stays — `handleSignOut` still navigates to
`/`). Keep `alt=""` on the `<img>`: it is decorative inside a labelled link.

**Extend the same fix to `ComposerShell.tsx:38-39`** — the identical avatar `<img>`/`<div>`
`onClick={openProfile}` pair, in a file this slice already rewrote. CodeRabbit did not flag it
because it reviewed the NavBar hunk only. Leaving a known-broken duplicate of the control we are
fixing, in a file we are editing in the same commit, is not defensible.

### 6. RC11 — workspace row actions

**Where:** `web/src/features/workspace/WorkspacePage.tsx:78`, `:79`, `:80` — Edit, Publish / View
status, and View, all `<span onClick={() => navigate(...)}>`.

**Verified:** correct. These three spans are the only route into the composer and the publish-status
page for an engineer that already exists, so with finding 3 fixed a keyboard user could create and
publish one engineer but never re-open or check one.

**Fix:** all three are plain navigations with no side effect, so use react-router `<Link>` with the
existing class names (`link-violet`, `link-accent-hover`, `link-quiet`) rather than buttons — that
also restores middle-click and copy-link. The conditional targets on lines 79-80 stay exactly as
written. `useNavigate` is still needed for `+ New Engineer` on line 37; do not remove the import.

### 7. RC2 + RC3 — two stale labels in the live process documents

**Where:** `.process/frontend-auth/02-implementation.md:346`, `:347-348`, and — CodeRabbit missed this
one — `:352`, all three saying "round 2" where the referent is round 1. The `index-nYcqelUk.js` build
and the 58-test result are recorded in the **Rework round 1** section at `02-implementation.md:224`
and `:232`. Also `.process/frontend-auth/04-metrics.md:3`, whose "teams still in review as PR #7" was
true when written and went stale when `fa77271` merged `origin/main`.

**Verified:** both claims check out. These two are the documents this repo treats as **live** working
artifacts, so they may be corrected in place — unlike `01-plan.md` and the completed reviews.

**Fix:** in `02-implementation.md` change "round 2" to "round 1" at lines 346, 348 **and 352**; in
`04-metrics.md:3` relabel the header as a run-start snapshot (CodeRabbit's own suggestion, dated
2026-08-29), which keeps the historical fact and removes the ambiguity rather than rewriting it to
the merged state.

---

## REJECTED

### RC1 — escape the pipes in `01-plan.md:76`

The rendering claim is technically right: GFM splits a table row on `|` even inside a code span, so
the Decision 11 row renders as six cells with the tail dropped. It is rejected on the repo's standing
ruling — **`01-plan.md` and completed review documents are closed and append-only**. Corrections go
in the current document, never retroactively into a signed-off artifact; a plan that changes after
the gate stops being evidence of what was approved. For the record, so the correction exists
somewhere: Decision 11's select offers the values Patch, Minor and Major, default Patch, which is
exactly what `EngineerComposerPage.tsx:16` implements.

### RC6 — reset draft state when the route loses its engineer id

The mechanism is real: `EngineerComposerPage.tsx:42-43` returns early without clearing `engineerId`,
so a route change from `/workspace/engineers/:engineerId` to `/workspace/new-engineer` on a preserved
component instance would make the next save call `updateEngineer` on the previous draft. But it is
**unreachable**. The only navigation to `/workspace/new-engineer` anywhere in the SPA is
`WorkspacePage.tsx:37`, and `WorkspacePage` lives under `StandardLayout` while the composer lives
under `ComposerLayout` (`App.tsx:65-72`) — React unmounts across that boundary, so the composer always
mounts fresh with `engineerId = null`. Typing the URL or reloading is a full document load. The
opposite direction, new-engineer to engineers/:id, is the `navigate(replace)` at
`EngineerComposerPage.tsx:85`, which re-runs the load effect correctly.

Against that, CodeRabbit's prescribed fix — clear the fields and set `loadStatus` inside the effect —
means synchronous `setState` calls in an effect body, which adds `react(set-state-in-effect)`
warnings and breaches the cap of 8. It is the same rule the implementer already restructured
`WorkspacePage` to avoid (`02-implementation.md:110`).

**Follow-up for the dev, and it is a real one:** the moment any in-composer affordance links to
`/workspace/new-engineer`, this becomes a silent cross-draft overwrite. The durable fix is not a
state reset but forcing the remount at the route — a two-line wrapper component that reads
`useParams()` and renders the composer with a `key` of the engineer id (or the string "new"), wired
into both routes in `App.tsx`. Zero new lint warnings, no state juggling. It is left out here only
because nothing today can reach the bug, and this cycle's changes must be defensible on evidence.

### RC9 — only split `failureReason` on commas when every part is an error code

Rejected: the observation is right, the remedy is a regression. `failureText`
(`publishStage.ts:31-38`) maps each part matching the code shape through the code map and passes
prose parts through. Under CodeRabbit's rule — if any part is not a code, return the input unchanged
— a mixed reason such as "PLUGIN_UNSAFE_PATH, retried twice" would be rendered verbatim, putting a
raw SCREAMING_SNAKE code back in front of the creator. That is exactly round 1's blocking finding
(`03-review.md:15`), acceptance decision 4, plan decision 14 (`01-plan.md:79`) and the Definition of
Done line at `01-plan.md:557`. The existing tests would not catch the regression, because every input
they use is already all-codes.

The split is still right. The lost comma is also unreachable: round 2's completeness sweep
(`02-implementation.md:297-321`) shows every value the pipeline writes into `FailureReason` is an
`ErrorCodes` constant or a comma-join over them — `MarkFailed` has exactly one caller and no path
produces prose.

**Follow-up:** if the API ever starts writing prose into `FailureReason`, the correct change is to
re-join the pass-through parts with their comma while still mapping the code-shaped ones — never to
abandon mapping for the whole string.

### RC10 — abort in-flight requests on unmount

Rejected: no correctness impact, and it fights the house convention.
`PublishStatusPage.tsx:23,30-32,54` implements the repo's data-fetching pattern verbatim
(`01-plan.md:18` — `useEffect` plus a `cancelled` flag, cleanup sets it), the same one `CatalogPage`
and `EngineerDetailPage` use. I traced the unmount race: if the status request is in flight when
cleanup runs, `cancelled` is already true, so line 30 returns before any `setState` and before line
45 can schedule another timer. No state update after unmount, no orphan timer, no runaway poll. The
only residue is one GET finishing into a discarded promise.

Adopting `AbortSignal` here would widen `getPublishStatus` and `getEngineer` beyond the signatures
the plan pins (`01-plan.md:201,207`) for one page, leaving the other three fetch sites on the old
pattern — inconsistency bought with no behaviour change. **Follow-up:** if abort is wanted, do it
once across all fetch sites; `requestJson` already accepts a `signal` (`01-plan.md:122`), so it is a
call-site change only.

### RC13 — "the API returns `engineerId`, not `itemId`"

**Wrong — CodeRabbit is reading a stale base.** On this branch
`api/E3A.Application/Publishing/Shared/PublishStatusResult.cs:3` declares
`(Guid VersionId, Guid ItemId, string ItemType, ...)`, and `PublishStatusResultGenerator.cs:12`
populates it from `version.ItemId` and `version.ItemType.ToString()`. The `teams` slice performed
that rename, merged to `main` as `38ced01`, and this branch merged `origin/main` at `fa77271`.
`workspaceApi.ts:53-65` is typed against the merged shape and is **correct as written**;
`PublishStatusPage.tsx:36` and `:125` are correct.

The comment cites `01-plan.md:75` as its authority — that is decision 10, which said the rename was
*not yet* in this branch. True at planning time, and the reason the plan required the guarded
engineer lookup in the first place. Implementing this comment would break the page it claims to fix:
`result.engineerId` does not exist on the response, so the lookup would request
`/engineers/undefined`. **Do not implement. Do not touch `api/`.**

---

## DEV-DECISION (escalate — do not implement, do not silently reject)

### RC12 — "Do not persist the bearer token in `localStorage`"

**Where:** `web/src/lib/tokenStorage.ts:8`. **Status: escalated to the dev, unchanged in this cycle.**

CodeRabbit is not wrong about the risk. Any script executing on the SPA origin can read `e3a.token`
and replay it until the JWT expires, and clearing it on sign-out or on a 401 does not undo a theft
that already happened. But that trade-off is stated openly in `00-acceptance.md:58` and is a
**proxied product decision the dev owns**, not an implementation slip: decision 1 chose URL-fragment
delivery precisely so the token never reaches a server or a log, and rejected an httpOnly cookie **by
name**. A reviewer agent does not overturn that; the dev does. CodeRabbit is right about the risk and
wrong about whose call it is.

What the alternative would actually cost, so the decision can be re-made on facts rather than on a
severity label:

- The API issues **no refresh token** (acceptance decision 3, `00-acceptance.md:60`) and sets **no
  session cookie for the SPA**. The only cookie in the system is the short-lived OAuth nonce, scoped
  to `Path=/api/auth` and consumed during the handshake.
- `api/E3A.Api/Program.cs:85` configures CORS with two localhost origins, `AllowAnyHeader` and
  `AllowAnyMethod`, and **no `AllowCredentials`** — so no cookie could ride any SPA XHR even if one
  were issued.
- Switching means: change the OAuth callback to set a cookie instead of redirecting with a token
  fragment (a new contract replacing `AuthenticationRedirectUrlGenerator.Success`); add cookie
  authentication alongside JWT bearer; add `AllowCredentials` and an exact-origin list; add CSRF
  defence for engineer create, update, the multipart upload and publish; and add a server-side
  sign-out endpoint to clear the cookie. Every one of those is a file under `api/`.
- This slice may not touch `api/` (`00-acceptance.md:51` and the standing rule for this cycle), so it
  is not a frontend fix at any size. It is a separate auth-hardening slice.

**Recommendation to the dev:** keep `localStorage` for this slice — the decision is coherent and its
cost is recorded — and scope a server-managed session cookie deliberately, alongside the CSRF and
CORS work it drags in. Two mitigations that need no API change and are worth their own follow-up: a
strict `Content-Security-Policy` on the SPA, which is the practical XSS control here, and a shorter
JWT lifetime.

---

## Not implemented, at a glance (hand-off list)

| # | Item | Why it is being left |
|---|---|---|
| RC1 | Pipe escaping in `01-plan.md:76` | Closed artifact; the correction is recorded above instead |
| RC6 | Draft reset on route change | Unreachable today; the prescribed fix breaches the lint cap; keyed-route follow-up recorded |
| RC9 | Conditional comma split | The prescribed remedy re-opens round 1's blocking defect; the input it guards against is unreachable |
| RC10 | AbortSignal plumbing | No correctness impact; do it repo-wide or not at all |
| RC12 | httpOnly session cookie | The dev's call; needs an `api/` slice this cycle may not touch |
| RC13 | Revert itemId to engineerId | Factually wrong against `PublishStatusResult.cs:3` |
| — | Clickable spans in the other 12 files | `HowItWorksPage`, `EngineerCard`, `DetailHeader`, `VersionHistory`, `ModalOverlay`, `ReportContext`, `ProfilePage`, `TeamDetailPage`, `MetaPanel`, `Footer` and `TeamComposerPage` carry the same pattern. Pre-existing, on surfaces this slice does not own, outside the plan's touched-file list. Findings 3-6 fix it on this slice's own surfaces, where it blocks the creator flow; the rest is a repo-wide accessibility chore that deserves its own slice. |

## Constraints for the rework

- `npm run build` — zero TypeScript errors. `npm run test` — **58**, unchanged: none of the seven
  fixes is reachable by the `environment: node` runner, and no DOM library may be added to chase
  coverage. State that plainly rather than padding the count.
- `npx oxlint` — **8 warnings, 0 errors**, with no `eslint-disable`, `oxlint-disable`, `@ts-ignore`
  or `@ts-expect-error` anywhere. Finding 1's `setErrorMessage` must stay inside the `.then` callback
  for exactly this reason.
- No file under `api/` may be modified. No new package. No Azure resource. `index.css` should not
  need to change — use module-level `React.CSSProperties` constants, the pattern already used at
  `ImportManifestPanel.tsx:4-7` and `EngineerComposerPage.tsx:17`.
- Findings 3-6 must not change the rendered appearance. Reset background, border, padding, font
  family and colour on every element promoted to a button, using the `fontFamily` and `fontSize`
  longhands rather than the `font` shorthand, to avoid React's shorthand-conflict warning.
- Findings 3-6 are the accessibility cluster and land together; findings 1 and 2 are independent
  state defects; finding 7 touches only `.process/` and no code.
