VERDICT: APPROVED

# Stage 4 verification — PR #9 (`frontend-auth`), CodeRabbit rework

Independent re-run of every gate, plus a full read of all ten changed files. I did not edit anything.
The rework report (`07-coderabbit-rework.md`) is accurate on every claim I could check, including the
one it flags as its own weakest.

## 1. Gates, re-measured

Run by me in `D:\Personal\_e3a\web`, not copied from the report.

| Gate | Claimed | Measured | Verdict |
|---|---|---|---|
| `npm run build` | 0 TS errors, `index-KNzsuVkz.css` 4.69 kB, `index-DvXbMnFT.js` 314.23 kB, 67 modules | byte-identical: `index-KNzsuVkz.css 4.69 kB / gzip 1.41 kB`, `index-DvXbMnFT.js 314.23 kB / gzip 92.25 kB`, 67 modules transformed | matches |
| `npm run test` | `Test Files 10 passed (10)` / `Tests 58 passed (58)` | identical | matches |
| `npx oxlint` | 8 warnings, 0 errors | 8 warnings (counted), exit code 0 | matches |

**The CSS-hash argument holds, and the report states it at the right width.** `index-KNzsuVkz.css` is a
content hash over the emitted stylesheet; I reproduced it exactly from a clean build, and
`git diff --name-only -- web/src/index.css` is empty. So it does prove *no stylesheet changed*. It
does **not** prove no visual change — every style this rework added is an inline `React.CSSProperties`
object compiled into the JS bundle, whose hash did move (`index-Butcx_Ua` to `index-DvXbMnFT`,
+0.84 kB). The report never claims otherwise. I checked visual parity separately, in section 4.

**Test count and untouched tests, both confirmed.** `find src -name "*.test.ts*"` returns 10 —
matching `Test Files 10`, so no file was deleted to hold the number down. `git diff --name-only`
contains no path matching `test` or `spec`. `vitest.config.ts` is unmodified and still
`environment: 'node'`, `include: ['src/**/*.test.ts']`, which is why none of the seven fixes is
reachable by the runner. Holding at 58 without a DOM library is the correct call, not a coverage gap.

**Lint sites, rule by rule.** The 8 are `react(only-export-components)` x4 (`ToastContext.tsx:33`,
`ReportContext.tsx:52`, `AuthContext.tsx:75`, `CatalogPage.tsx:12`), `react(set-state-in-effect)` x3
(`AuthCallbackPage.tsx:36`, `AuthContext.tsx:61`, `EngineerDetailPage.tsx:23`), and
`react-hooks(exhaustive-deps)` x1 (`CatalogPage.tsx:39`). Exactly the composition claimed. The three
moves claimed (`AuthContext.tsx:59 to :61`, `:73 to :75`, `AuthCallbackPage.tsx:30 to :36`) are the
only sites in changed files; the other five sit in files this rework never opened, so they could not
have moved. Finding 1's `setErrorMessage` is inside the `.then` callback (`AuthCallbackPage.tsx:30`),
so it added no fourth `set-state-in-effect` — the cap of 8 held for the stated reason.
Grepping `web/src` for `eslint-disable`, `oxlint-disable`, `@ts-ignore` and `@ts-expect-error` returns
**zero**. Nothing was silenced to buy a clean run.

## 2. The four risk areas from the brief

**Sign-out still works.** `NavBar.tsx:43` is now
`<button type="button" onClick={handleSignOut} className="link-quiet" style={signOutStyle}>Sign out</button>`.
It still calls `handleSignOut` (`:21-25`), which calls `signOut()` and thus `clearToken()`
(`AuthContext.tsx:50-54`), then toasts and navigates to `/`. `type="button"` is present, and
independently there is **no `<form>` element anywhere in `web/src`** (grepped), so the submit-default
hazard cannot fire here at all. The only client-side token clear is intact.

**The upload path still works both ways.** `UploadDropzone.tsx:38-45` keeps the `<div>` with
`onDragOver` / `onDragLeave` / `onDrop`; `handleDrop` (`:15-21`) is unchanged and still reachable,
because React synthetic drag events from the new child button bubble to the div handler. The button
(`:52`) does not swallow the drop: it only intercepts `click`, via `openPicker` (`:30-33`), which calls
`event.stopPropagation()` before `inputRef.current?.click()` — that is what stops the div own
`onClick` at `:39` from opening the dialog a second time. `disabled` genuinely closes **both** paths:
the button carries `disabled={disabled || busy}`, the div `onClick` is guarded by `!disabled && !busy`,
`handleDrop` is guarded by `!disabled && !busy` (`:18`), and `handleFile` bails on
`if (!engineerId) return` (`EngineerComposerPage.tsx:96-98`). Three independent guards; no path
uploads before an engineer exists. `handleDrop` still calls `preventDefault()` unconditionally, so a
drop on a disabled zone does not navigate the browser to the file.

**The workspace row actions target the right routes.** The diff at `WorkspacePage.tsx:78-80` is a pure
`<span onClick={() => navigate(X)}>` to `<Link to={X}>` swap with **X byte-identical** on all three,
including both conditional branches:

- `:78` Edit — `/workspace/engineers/${engineer.id}`
- `:79` `engineer.latestVersionId ? /workspace/publish?versionId=... : /workspace/engineers/${engineer.id}`, with the label branch `'View status' : 'Publish'` unchanged
- `:80` `engineer.status === 'Published' ? /e/${engineer.slug} : /workspace/engineers/${engineer.id}`

I checked all three resolve against `App.tsx:56,62,63,70`: `/e/:name`, `/workspace`,
`/workspace/publish` and `/workspace/engineers/:engineerId` all exist. react-router 7 parses a string
`to` into path plus search, so the `?versionId=` query survives. Class names and inline styles are
unchanged in the diff. `useNavigate` is retained for `+ New Engineer` (`:37`).

**Finding 1 fix is real, and I confirmed it without the mutation trick.** `AuthContext.tsx:30-48`
returns `AuthStatus` on all three exits (`'signedOut'` no-token, `'signedIn'` success, `'signedOut'`
catch, with the 401/403 `clearToken()` preserved); `completeSignIn` (`:56-59`) returns it; the
interface (`:13`) and the default context value (`:22`) are widened. `AuthCallbackPage.tsx:25-31`
navigates **only** on `status === 'signedIn'` and otherwise calls
`setErrorMessage(GENERIC_ERROR_MESSAGE)` (exported at `errorMessages.ts:3`), which renders the
"Sign-in failed" card at `:43-53` with a real message and a Try-again anchor. The silent
navigate-to-workspace-then-see-"Sign in to continue" loop is broken.

The `.catch` at `:32` is retained per the triage. The report calls it unreachable; strictly it is now
*reachable*, since `writeToken` calling `localStorage.setItem` can throw `QuotaExceededError` or
`SecurityError` inside the async `completeSignIn`, rejecting the promise. Either way it is defensive
and harmless. Not a finding.

## 3. Do the verification techniques prove what they claim?

**The negative type check (finding 1): sound, but I did not need to trust it.** `TS2367` naming
`'AuthStatus'` as one operand does distinguish a real `AuthStatus` from a `Promise<void>` (which would
have named `'void'`). I verified the same fact directly from the source instead — `AuthContext.tsx:13`
declares `completeSignIn: (token: string) => Promise<AuthStatus>`, so `status` in the `.then` is
`AuthStatus` by declaration — and my own `tsc -b` passed, which proves `'signedIn'` *does* overlap the
union. Reading the type is stronger evidence than the mutation, and both agree.

**The `renderToStaticMarkup` harness (findings 3-5): proves what it claims, and nothing more.** It
establishes the emitted element type, `type="button"`, `aria-expanded="false"`, `disabled`, the anchor
real `href`, and the inline style strings. It cannot establish click behaviour, `stopPropagation` or
drag — the report does not claim it does, and I verified those by reading, above. The harness is
genuinely gone: `find . -name "*verify*"` outside `node_modules` returns nothing, `git status` shows
only the ten intended files plus the three `.process/` documents, and the harness never matched the
`include` glob in `vitest.config.ts`, so it could not have influenced the 58.

**Finding 6, the one with the thinnest evidence: I verified it myself and it holds.** The report is
right that SSR cannot render the rows, and right to flag that. But the diff is decisive on its own —
the three `to` expressions are character-for-character the old `navigate()` arguments, the routes
exist, and a `<Link>` inside a grid `<span>` is unremarkable. I am satisfied without a DOM runner.

## 4. Visual parity, checked by specificity rather than screenshot

The report argues parity; I re-derived it, since promoting a `<span>` to an `<a>` or `<button>` changes
UA defaults and no test in this repo can catch a regression there.

- `index.css:27` `* { box-sizing: border-box }` confirmed — so the `width: 100%` plus `11px 14px`
  padding in `sectionHeaderStyle` occupies exactly the box the `<div>` did, with no overflow inside
  the `overflow: hidden` card.
- `index.css:42` `button { font-family: var(--font-ui); cursor: pointer }`, and every promoted button
  sets `fontFamily: 'inherit'` — the face is unchanged. Every reset uses the `fontFamily` and
  `fontSize` **longhands**; I found no `font` shorthand in any of the three new style constants
  (`NavBar.tsx:10`, `UploadDropzone.tsx:3`, `ImportManifestPanel.tsx:4-5`), so the React
  shorthand/longhand re-render warning cannot fire.
- Buttons default to `text-align: center`; `sectionHeaderStyle` and `inlineToggleStyle` both set
  `textAlign: 'left'`, which matters because both sit in `flexDirection: 'column'` containers with
  default `align-items: stretch` and therefore render full-width. Correctly handled.
- The anchor colour question: `a { color: var(--primary) }` (`index.css:37`, specificity 0-0-1) loses
  to `.link-violet` (0-1-0), and the values are identical anyway. On hover, `a:hover` (`:38`) and
  `.link-violet:hover` (`:86`) tie on specificity, so the later rule wins — and again the value is the
  same. `.link-quiet:hover` (`:84`) and `.link-accent-hover:hover` (`:92`) carry `!important`, so they
  beat `a:hover` outright, and both those rows also pin their colour inline. `a { text-decoration:
  none }` means no new underline. Parity holds on every one of the three rows.
- `UploadDropzone.tsx:52` sits in an `alignItems: 'center'` column, so the button shrinks to content
  and stays centred exactly as the `<span>` did.

## 5. Rejections and scope

Every rejection held, verified in the tree rather than in the report:

- **RC1** — `git status` does not list `.process/frontend-auth/01-plan.md`, nor any `03-review*.md`.
  Closed artifacts stayed closed.
- **RC9** — `publishStage.ts:33` still reads `.split(',')`; the file is unmodified. The round 1
  blocking fix is intact.
- **RC13** — `workspaceApi.ts:55` still declares `itemId: string`; the file is unmodified. The triage
  was right that CodeRabbit read a stale pre-merge base, and nothing was "fixed" into
  `/engineers/undefined`.
- **RC12** — `tokenStorage.ts` is unmodified and still uses `localStorage.setItem('e3a.token', ...)`.
  Escalated to the dev, not silently implemented and not silently dropped.
- **RC6, RC10** — no corresponding edits anywhere in the diff.

Scope: `git diff --name-only -- api postman docs` is **empty**. `web/package.json` and
`web/package-lock.json` are unmodified — no new package. The full working tree is the ten intended
files plus the three new `.process/` documents. Nothing stray.

## 6. Deviations — both sound

**Dropzone accessible name.** Reusing the visible "Drop your zipped .claude folder" text rather than
adding `aria-label="Choose a .zip file"` is the correct call. An `aria-label` *replaces* the accessible
name, and one that does not contain the visible label text fails WCAG 2.5.3 (Label in Name) — a real
failure traded for a wording preference. The control has an accessible name either way, so 4.1.2 is
satisfied. The wording is drop-oriented for a control that opens a picker; that is the lesser defect,
and the report says so openly rather than burying it.

**`aria-expanded={expanded[key] === true}`.** Correct and necessary. `expanded` is
`Record<string, boolean>` (`ImportManifestPanel.tsx:11`), which TypeScript types as `boolean` but which
yields `undefined` at runtime for an unset key; React omits an attribute whose value is `undefined`, so
the collapsed state would go unannounced. `=== true` forces `aria-expanded="false"`. It also
type-checks cleanly — no `TS2367`, since both sides are `boolean` — which my build confirms. Both are
narrowings inside the prescribed fix, not substitutions; the report characterises them correctly.

## 7. Docs sync

No `/docs` file changed, and none needed to. Per `.claude/rules/docs-sync.md`, the owning doc for UI
page content and flow is `docs/design-prompt.md`. I read the relevant sections:

- `design-prompt.md:31` specifies the workspace table row actions "(Edit, Publish, View)" — labels,
  destinations and the Publish / View-status branch are all unchanged by this rework, so no divergence.
- `design-prompt.md:32` specifies the dropzone, the three manifest sections and the "view snippet"
  expander — all still present with identical content and behaviour; a `<div>` to `<button>` promotion
  is a presentation-layer change the doc does not speak to.
- `design-prompt.md:42` requires "every nav link, button, card, and text link routes to its correct
  page... if it looks clickable, it does something." Findings 3-6 move the code *toward* this line,
  not away from it.

Finding 1 makes an already-specified error state reachable rather than redefining it. Nothing here
changes business behaviour, scope, architecture, policy or a contract. No divergence to record.

## 8. Correction of the live documents

`02-implementation.md` changed at exactly the three claimed sites (`:346`, `:348`, `:352`), each
"round 2" to "round 1", each referring to `index-nYcqelUk.js`, the 58-test result, and the round 1
lint baseline — all genuinely round 1 referents. `04-metrics.md:3` now reads
`**Base branch at run start (2026-08-29):**` and keeps the historical fact rather than rewriting it to
the merged state. The diff touches nothing else in either document. `01-plan.md`, `03-review.md` and
`03-review-r2.md` were not opened.

## Non-blocking

- `web/src/features/composer/ComposerShell.tsx:47` — the Save-draft button (and `:49`, `:50`,
  `WorkspacePage.tsx:37`, `:51`, `ImportManifestPanel.tsx:69`) carries no `type="button"`. Harmless
  today, since `web/src` contains no `<form>`, and all of it is pre-existing — every button this
  rework actually *promoted* does carry `type="button"`. Worth normalising if a form is ever added.
- `web/src/features/composer/ImportManifestPanel.tsx:18` with `:24` — a section with `count === 0` now
  announces `aria-expanded="true"` on click while `{expanded[key] && count > 0 && ...}` renders
  nothing. Pre-existing toggle behaviour, newly audible. A `disabled={count === 0}` on the header
  would settle it.
- `web/src/features/composer/ComposerShell.tsx:37` — while auth is still loading, `login` is `''` and
  the avatar link renders `href="/u/"`, which falls through to the 404 route. Behaviourally identical
  to the `navigate('/u/')` it replaced, so not a regression, but it is now a visible, copyable href.
- The report own note 2 is right and worth keeping on the follow-up list: `.btn-secondary` has no
  `:disabled` rule, so the finding 2 guard is invisible to the creator apart from the "Saving..." chip
  (`EngineerComposerPage.tsx:142`). Correctly deferred — fixing it means touching `index.css`, which
  this cycle excludes.

## Verified claims from `07-coderabbit-rework.md`

- Build output, test count and lint composition reproduced exactly, including the three moved line
  numbers and the zero disable comments.
- No test file added, edited or deleted; 10 test files on disk match `Test Files 10`.
- Verification harness deleted; nothing stray in `git status`.
- All five rejects held and RC12 untouched, each confirmed in the file rather than in the report.
- `git diff --name-only -- api postman docs` empty; `package.json` untouched; no new package.
- Both declared deviations are sound and correctly reasoned.
- Finding 6, the report self-declared weakest item, verified independently by diff and route table.

All seven IMPLEMENT items are present, correct, and honestly reported. No blocking findings.
