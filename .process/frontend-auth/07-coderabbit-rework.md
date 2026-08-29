# CodeRabbit rework — PR #9 (`frontend-auth`)

Implements the 7 IMPLEMENT items from `06-coderabbit-triage.md`. Every REJECT stayed rejected; the
dev-decision (RC12) is untouched. `01-plan.md`, `03-review.md`, `publishStage.ts`, `workspaceApi.ts`
and everything under `api/` were not opened for edit.

## 1. What changed, per finding

| # | Finding | What I changed | Where |
|---|---|---|---|
| 1 | RC4 — `completeSignIn` resolves on a failed session | `loadSession` now returns `AuthStatus` on all three exits; `completeSignIn` returns it; the interface and the default context value widened to `Promise<AuthStatus>` / `async () => 'signedOut'` | `web/src/app/AuthContext.tsx:13`, `:22`, `:30`, `:34`, `:39`, `:46`, `:56` |
| 1 | RC4 — callback navigates regardless | `.then` now navigates only on `status === 'signedIn'`, else `setErrorMessage(GENERIC_ERROR_MESSAGE)` inside the `.then` callback; existing `.catch` kept | `web/src/features/auth/AuthCallbackPage.tsx:6`, `:25-31` |
| 2 | RC7 — duplicate engineers on double-click | `if (saving) { return; }` as the first statement of `handleSaveDraft`; `saveDisabled={saving}` passed to the shell | `web/src/features/composer/EngineerComposerPage.tsx:75-77`, `:141` |
| 2 | RC7 — Save control has no `disabled` | new optional `saveDisabled?: boolean` prop (default `false`, so `TeamComposerPage` is unaffected) applied to the Save-draft button | `web/src/features/composer/ComposerShell.tsx:14`, `:20`, `:47` |
| 3 | RC8 — upload selector not keyboard operable | `<div>` kept for drag-and-drop; the "Drop your zipped .claude folder" text is now a real `<button type="button">`, `disabled={disabled \|\| busy}`, whose handler calls `event.stopPropagation()` before `inputRef.current?.click()`; styled from a module-level `pickerStyle` constant (background/border/padding reset, `fontFamily` + `fontSize` longhands) | `web/src/features/composer/UploadDropzone.tsx:3`, `:30-33`, `:52` |
| 4 | RC8 (second half) — three manifest expanders | section header `<div onClick>` becomes `<button type="button" aria-expanded>`, with `width: 100%`, `textAlign: left`, background/border/font/colour reset folded into `sectionHeaderStyle`; the two inline expanders become `<button type="button" aria-expanded>` keeping `className="link-quiet"` and their inline colour/size, plus a shared `inlineToggleStyle` reset | `web/src/features/composer/ImportManifestPanel.tsx:4`, `:5`, `:18`, `:58`, `:64` |
| 5 | RC5 — sign-out and profile in the nav bar | `<span onClick={handleSignOut}>` becomes `<button type="button" className="link-quiet" style={signOutStyle}>`; the avatar `<img>`/`<div>` wrapped in a react-router `<Link>` to `/u/{login}` with `aria-label="Open your profile"` and `display: inline-flex`; both `onClick={openProfile}` handlers and the `openProfile` helper deleted; `useNavigate` kept for `handleSignOut` | `web/src/components/NavBar.tsx:1`, `:10`, `:43`, `:44-48` (helper removed at old `:25`) |
| 5 | RC5 extended — the same avatar in `ComposerShell` | identical `<Link>` wrapper; `openProfile` deleted; `useNavigate` kept for the default publish handler | `web/src/features/composer/ComposerShell.tsx:37-41` (helper removed at old `:24`) |
| 6 | RC11 — workspace row actions | the three `<span onClick={() => navigate(...)}>` become `<Link to={...}>` with the same class names, inline styles and conditional targets; `useNavigate` kept for `+ New Engineer` | `web/src/features/workspace/WorkspacePage.tsx:2`, `:78`, `:79`, `:80` |
| 7 | RC2 — stale round labels | "round 2" to "round 1" at the three referent sites, including the one CodeRabbit missed. The section heading at `:340` ("Build & test — round 2") and `:274`/`:366`/`:375` were left alone — those referents really are round 2 | `.process/frontend-auth/02-implementation.md:346`, `:348`, `:352` |
| 7 | RC3 — ambiguous base-branch line | relabelled as a run-start snapshot, keeping the historical fact | `.process/frontend-auth/04-metrics.md:3` |

## 2. How each fix was verified

There is no DOM test library and the runner is `environment: 'node'`, so **none of these seven is
covered by a unit test** and none was added. What follows is what I actually did instead. Nothing
below should be read as test coverage.

**Finding 1 — type-level proof, not a runtime test.** I mutated `status === 'signedIn'` to
`status === 'signedInTYPO'` and ran `npx tsc -b --force`:

```
src/features/auth/AuthCallbackPage.tsx(26,15): error TS2367: This comparison appears to be
unintentional because the types 'AuthStatus' and '"signedInTYPO"' have no overlap.
```

then restored the file and re-ran `tsc` clean. That proves the resolved value is genuinely typed
`AuthStatus` (a `Promise<void>` would not have produced that message) and that the navigate is now
behind a checked comparison. I traced the three `loadSession` exits by reading them: no-token gives
`'signedOut'`, success gives `'signedIn'`, catch gives `'signedOut'`; the 401/403 `clearToken()` and
the deliberate token-keeping on other failures are unchanged. **Not verified at runtime:** I did not
drive a live 500 from `/auth/me` — no API instance was started for this rework.

**Findings 3-6 — I read the emitted markup, not the source.** I rendered the changed components with
`react-dom/server`'s `renderToStaticMarkup` inside a `MemoryRouter`, using a throwaway harness
(`src/markup.verify.tsx` plus `vitest.verify.config.ts`) that matched **no** pattern in the committed
`vitest.config.ts` (`include: ['src/**/*.test.ts']`) and was run under its own config. Both files
were deleted afterwards; `git status` shows only the ten intended files. The emitted HTML, verbatim
in the relevant parts:

- NavBar: `<button type="button" class="link-quiet" style="padding:0;background:none;border:none;font-family:inherit;font-weight:400;font-size:13.5px;color:var(--text-secondary)">Sign out</button>`
  and `<a aria-label="Open your profile" style="display:inline-flex" href="/u/octocat">` wrapping the
  avatar. Both are natively focusable; the anchor carries a real `href`, so middle-click and
  copy-link work.
- ComposerShell: the same anchor, plus `<button disabled="" class="btn-secondary">Save draft</button>`
  when `saveDisabled` is set (finding 2's rendered half).
- UploadDropzone enabled: `<button type="button" style="...;cursor:pointer">Drop your zipped .claude
  folder</button>`. Disabled: the same button with `disabled=""` and `cursor:default`, inside a
  container that still carries `opacity:0.55`.
- ImportManifestPanel: three `<button type="button" aria-expanded="false" class="hover-row"
  style="...;width:100%;text-align:left;background:none;border:none;font-family:inherit;color:inherit">`
  section headers and two `<button type="button" aria-expanded="false" class="link-quiet">` inline
  expanders.

Focus order reasoning for the flow finding 3 unblocks: within the composer's right-hand column the
DOM order is version-increment `<select>`, then the dropzone `<button>`, then (after upload) the
manifest `<button>`s, then footer Save draft and Publish. Every step of `00-acceptance.md:41` now has
a focusable control, so tabbing from the name field to Publish no longer dead-ends at the dropzone.

Visual-parity reasoning, since I could not take a screenshot: `index.css` already sets
`button { font-family: var(--font-ui); cursor: pointer; }` and `* { box-sizing: border-box; }`, so the
promoted buttons inherit the same face and the section header's `width:100%` plus `11px 14px` padding
occupies exactly the box the `<div>` did. Each reset uses the `fontFamily`/`fontSize` longhands, never
the `font` shorthand. For the WorkspacePage links the global `a { color: var(--primary);
text-decoration: none }` matches `.link-violet`'s own colour, and the `.link-quiet` /
`.link-accent-hover` hover rules carry `!important`, so they still beat `a:hover`. `index.css` is
unchanged, which the unchanged CSS bundle hash in section 4 confirms independently.

**Finding 6 is the weakest verification and I want that on the record.** `WorkspacePage` renders its
rows only after an effect resolves, and `renderToStaticMarkup` does not run effects, so I could not
capture the row markup itself. What I have is: the edit is a mechanical `<span onClick={() =>
navigate(X)}>` to `<Link to={X}>` swap with className, inline style, children and both conditional
targets byte-identical (visible in the diff); `tsc` accepts all three `to` expressions; and the same
harness proved that react-router `<Link>` emits `<a href ... class ... style ...>` in this exact app.
I did **not** observe the three rendered row anchors.

**Finding 2 — reasoning, plus the rendered `disabled` above.** Two independent layers: the button
carries `disabled` while `saving`, so a second click dispatches no event at all; and
`handleSaveDraft` returns early on `saving`. React flushes discrete click events synchronously, so
the second click's handler closure already sees `saving === true` even if the attribute were
bypassed. `setSaving(false)` remains in the existing `.finally`, so the control re-enables on both
outcomes. The default `saveDisabled = false` keeps `TeamComposerPage` behaviourally identical — I
checked it passes no such prop. **Not verified:** I did not reproduce the original double-POST
against a live API.

**Finding 7 — read back after editing.** `grep -n "round 2"` now returns only `:274`, `:340`, `:366`
and `:375`, all of which correctly refer to round 2. `04-metrics.md:3` reads
`**Base branch at run start (2026-08-29):** ...`.

## 3. Rejects held

RC1, RC6, RC9, RC10, RC13 were not implemented, and RC12 remains the dev's call. Specifically:
`git diff --name-only` shows `.process/frontend-auth/01-plan.md` unmodified; `publishStage.ts` is
unmodified, so the comma split in `failureText` is untouched; `workspaceApi.ts` is unmodified, so
`itemId` still matches `PublishStatusResult.cs` on this tree and no `/engineers/undefined` was
introduced; `tokenStorage.ts` is unmodified. `git diff --name-only -- api postman docs` is **empty**.

## 4. Gates

Run in `D:\Personal\_e3a\web`, after deleting the verification harness.

```
$ npm run build          # tsc -b && vite build
✓ 67 modules transformed.
dist/index.html                   0.93 kB │ gzip:  0.50 kB
dist/assets/index-KNzsuVkz.css    4.69 kB │ gzip:  1.41 kB
dist/assets/index-DvXbMnFT.js   314.23 kB │ gzip: 92.25 kB
✓ built in 111ms
```
Zero TypeScript errors. The CSS bundle hash is **unchanged** (`index-KNzsuVkz.css`, 4.69 kB) —
independent evidence that no stylesheet changed. The JS grew 0.84 kB over round 2's
`index-Butcx_Ua.js` (313.39 kB to 314.23 kB): the `<Link>` call sites, the three style constants and
the two new guards.

```
$ npm run test           # vitest run
Test Files  10 passed (10)
     Tests  58 passed (58)
```
**58, exactly as before.** No test was added, removed or edited — `git diff --name-only` lists no
file under a test path. As stated above, none of the seven fixes is reachable from an
`environment: 'node'` runner, and no DOM library was added to make them reachable.

```
$ npx oxlint
```
**8 warnings, 0 errors** — the same eight rules on the same eight sites as the pre-rework baseline I
measured before starting (`react(only-export-components)` x4, `react(set-state-in-effect)` x3,
`react-hooks(exhaustive-deps)` x1). Only line numbers moved: `AuthContext.tsx:59` to `:61`, `:73` to
`:75`, `AuthCallbackPage.tsx:30` to `:36`. Finding 1's `setErrorMessage` sits inside the `.then`
callback, so it added no fourth `set-state-in-effect`. Grepping `web/src` for `eslint-disable`,
`oxlint-disable`, `@ts-ignore` and `@ts-expect-error` returns **0**.

Constraint checks: `git diff --name-only -- api` empty; `web/package.json` unmodified (no new
package); no Azure resource; no `/docs` change. On the docs-sync rule — no doc in `/docs` describes
the SPA sign-in callback, the nav bar or the workspace row actions, and none of these seven changes
alters what the product does (finding 1 makes an already-specified error state actually reachable
rather than changing the spec), so there is no divergence to record.

## 5. Deviations from the triage

| Triage said | What I did | Why |
|---|---|---|
| Finding 3: put a real control inside the dropzone `<div>` | Promoted the existing "Drop your zipped .claude folder" line to the button, rather than adding a new control | Adding a separate "Choose file" control would have changed the rendered appearance, which the triage forbids. The trade-off: the accessible name is the drop-oriented visible text. I deliberately did **not** add an `aria-label` such as "Choose a .zip file" — an accessible name that does not contain the visible label breaks WCAG 2.5.3 (label in name) and would be worse than the wording mismatch. |
| Finding 4: `aria-expanded={...}` | Wrote `aria-expanded={expanded[key] === true}` (and `expanded.snippet === true`, `expanded.stripped === true`) | `expanded` is `Record<string, boolean>`, so an unset key is `undefined`; React omits the attribute entirely for `undefined`, leaving the collapsed state unannounced. The explicit `=== true` emits `aria-expanded="false"`, which the rendered markup above confirms. |

Nothing else deviates. Both are narrowings within the prescribed fix, not substitutions for it.

## 6. Notes for review

1. **Finding 6 has the thinnest evidence in this report** — see section 2. If the reviewer wants the
   three row anchors observed rather than argued, that needs a DOM runner, which this cycle may not
   add.
2. **`btn-secondary` has no `:disabled` rule**, so the Save-draft button looks identical while
   disabled; the only feedback during an in-flight save is the existing "Saving…" chip
   (`EngineerComposerPage.tsx:142`). That is unchanged behaviour, but it means finding 2's fix is
   invisible to the creator. A `.btn-secondary:disabled` style would be an `index.css` change, which
   the constraints exclude from this cycle. Recording it as a follow-up rather than doing it.
3. **The dropzone `<div>` keeps its own `onClick`**, so a pointer user can still click anywhere in the
   box. `event.stopPropagation()` in `openPicker` is what stops the button click from bubbling into
   that handler and opening the file dialog twice. If anyone later removes the div's handler, the
   `stopPropagation` becomes dead but harmless.
4. **`AuthCallbackPage`'s `.catch` is still unreachable** on the token path, exactly as the triage
   describes — `loadSession` never rethrows. The triage said to keep it, so I did, but a reviewer
   walking the file should know it is defensive, not live.
5. **The failed-callback message is the generic one.** When `/auth/me` fails with a non-401, the token
   is still kept (plan decision 4) and the creator sees "Something went wrong. Please try again."
   with a Try again button. The loop is broken and a message is shown, which is what decision 8
   requires, but the message does not distinguish "API down" from "token rejected".
6. **The avatar `<Link>` wraps `<img alt="">`** — the image stays decorative and the anchor carries
   the name, which is the correct pattern, but it does mean the accessible name is the literal string
   "Open your profile" rather than the user's login.
