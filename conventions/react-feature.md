# React feature conventions (`web/`)

The rules for all frontend work in this repo. Derived from the code already in `web/src` and from
`docs/constitution.md` §5; extended with what the `frontend-auth` slice learned the hard way.

**`.claude/skills/dotnet-feature/SKILL.md` does not apply here.** It governs `api/` only. Do not
import .NET idioms — no `Result` suffixes on view models, no layered folders, no `sealed` analogues.
New code must be indistinguishable from the existing `web/src`.

Stack: **React 19 · TypeScript · Vite · react-router-dom 7 · oxlint · vitest**. No data-fetching
library, no component library, no CSS framework — do not add one without the dev's say-so.

---

## 1. Project layout

```
web/src/
  app/          contexts and route guards  — AuthContext.tsx, ToastContext.tsx, RequireAuth.tsx
  components/   shared presentational UI   — NavBar.tsx, InstallBlock.tsx
  features/     one folder per area        — features/composer/EngineerComposerPage.tsx
  lib/          pure helpers and API calls — http.ts, workspaceApi.ts, slug.ts
```

- A page component is `<Name>Page.tsx` inside its feature folder.
- Logic that can be tested without a DOM belongs in `lib/` or a sibling `.ts` in the feature folder —
  **not** inline in a component. The test runner cannot reach a component (§6).
- Routes are declared only in `App.tsx`.

## 2. Style

- **Named exports only.** There is not one `export default` in `web/src`; do not introduce one.
- `interface` for object shapes, `type` for unions (`export type VersionIncrement = 'Patch' | 'Minor' | 'Major'`).
- `verbatimModuleSyntax` is on — type-only imports **must** use `import type { X } from './y'`.
- `noUnusedLocals` and `noUnusedParameters` are on; an unused import fails the build.
- Full words, no abbreviations (constitution §3): `engineerId`, not `id`; `cancelled`, not `c`.
- No comments explaining *what*. A comment earns its place only by explaining *why*.
- Inline `style={{}}` objects using `var(--token)` colours, plus the classes in `index.css`:
  `page` · `card` · `card-clickable` · `btn-primary` · `btn-secondary` · `btn-danger` · `mono` ·
  `fade-in` · `gradient-text` · `link-quiet` · `link-violet` · `link-author`. Reuse before inventing.
- **No magic values.** Pure client timings are module-level `SCREAMING_SNAKE` constants
  (`const TOAST_DURATION_MS = 1800`). Anything that must agree with a server or environment comes from
  Vite env via `lib/config.ts`, with `.env.example` committed and `.env.local` gitignored.

## 3. Data fetching

There is no query library. Every load follows the pattern already in `CatalogPage`,
`EngineerDetailPage`, `EngineerComposerPage` and `PublishStatusPage`:

```tsx
useEffect(() => {
  let cancelled = false;
  loadSomething()
    .then(result => { if (!cancelled) { setResult(result); } })
    .catch(error => { if (!cancelled) { setErrorMessage(messageForApiError(error)); } });
  return () => { cancelled = true; };
}, [dependency]);
```

- Every `.then` and `.catch` checks `cancelled` before touching state.
- Failure UI is a heading, a subline, and a **Retry** button driven by a `reloadToken` counter.
- Polling uses **chained `setTimeout`, never `setInterval`** — chained timeouts cannot stack requests
  on a slow API. Clear the timer in the cleanup.
- **React state read inside a `setTimeout` closure is stale.** A poll attempt counter kept in `useState`
  never increments as the closure sees it; use a local `let` in the effect. This shipped once and made
  an attempt cap silently unreachable.

## 4. Talking to the API

- All calls go through `lib/http.ts` → `requestJson`. Never call `fetch` from a component.
- The bearer token is attached by `requestJson` whenever one exists. Signed out means no header, so
  anonymous browsing is provably unchanged.
- **The API error contract is camelCase `{ code, message }`** on every failure, matching the shape of
  every success body. `ApiError` carries `status`, `code` and `message`.
- On `401` the token is cleared and the registered unauthorized handler runs. `403` does neither.
- Endpoint wrappers live in `lib/<area>Api.ts` and are thin — build the path, call `requestJson`,
  return the typed result. `encodeURIComponent` every interpolated id.

### Never render a raw error code

A SCREAMING_SNAKE code shown to a user is a dead end. Map it to prose in `lib/errorMessages.ts`;
unknown codes fall back to `GENERIC_ERROR_MESSAGE`.

**Check where a field actually comes from before trusting it to be prose.** `PublishStatus.failureReason`
looks like a message and is not — it is a raw database column the API never localizes, so every value
it can hold is an error-code constant. Rendering it directly shipped `PLUGIN_NO_INSTALLABLE_CONTENT`
to creators as the entire explanation of a failed publish.

## 5. Auth and the browser

These rules exist because each one was a live defect.

**The sign-in affordance must be a plain `<a href={gitHubLoginUrl()}>`.** Not `fetch`, not
react-router `<Link>`/`<NavLink>`, not `window.open`, not an iframe.

The API sets a `SameSite=Lax`, `HttpOnly`, `Secure` nonce cookie on `Path=/api/auth`, and browsers
return those **only on a top-level cross-site GET navigation**. `Program.cs` also configures CORS
without `AllowCredentials`, so no XHR path could ever carry it. A `fetch`-based login **passes every
unit test and fails in every browser**, with the failure looking like an unrelated auth bug.

- **Clear the URL fragment before any `await`.** `history.replaceState` runs immediately after parsing;
  a slow `/auth/me` would otherwise leave the token in the address bar, in shared links, and in history.
- **StrictMode double-invokes effects in dev.** A one-shot effect that consumes a value — the callback
  token — needs a `useRef` guard, or the second pass sees an already-cleared fragment.
- **`localStorage` may appear in exactly one module** (`lib/tokenStorage.ts`). One choke point keeps
  the XSS trade-off auditable.
- There is no refresh token. A missing or rejected token means signed out; never write an expiry timer
  or decode the JWT.

## 6. Accessibility — semantic elements, not clickable divs

Use real `<button>` and `<a>`/`<Link>` elements. A `<div onClick>` gets no focus, no keyboard
activation, and no announcement — adding `role` and `onKeyDown` to a div is not equivalent.

This is not a nicety. Before it was fixed, a keyboard-only creator reached "Save draft" and **stopped
dead**: the upload selector was a `<div onClick>` firing a `display:none` input, and Publish stays
disabled until a manifest exists. No mouse meant no publish, ever.

- Every promoted `<button>` carries `type="button"` — a bare `<button>` defaults to `type="submit"`.
- Toggles carry `aria-expanded`. With `Record<string, boolean>` state, compare explicitly
  (`aria-expanded={expanded[key] === true}`) — `undefined` makes React omit the attribute entirely.
- Do not add an `aria-label` that does not contain the visible text (WCAG 2.5.3).
- Converting a control is a **semantics** change: the CSS bundle hash should not move.

## 7. Tests

`npm run test` runs **vitest** with `environment: 'node'` and `include: ['src/**/*.test.ts']`.

**There is no DOM runner, and no jsdom or testing-library is authorised.** Components, routing and
effects are therefore *not* unit-testable. That is a deliberate boundary, not a gap to close by adding
a dependency.

- Test file sits beside its module: `slug.ts` → `slug.test.ts`. Note the `.ts` extension — a `.tsx`
  test is not picked up by the `include` glob.
- `describe('<exportName>')` + `it('should <outcome> when <condition>')`, mirroring the .NET
  `Method_Should[Outcome]_When[Condition]` convention.
- Fake globals per test with `vi.stubGlobal`, and always `afterEach(() => vi.unstubAllGlobals())`.
- Test what is pure: parsing, storage, error mapping, header attachment, validation, state machines.
- **When you change something the runner cannot reach, say how you verified it** — reading the emitted
  markup, a negative type check, a mechanical diff — rather than implying coverage that does not exist.
  Never pad the test count to make a component change look tested.

## 8. Gates

Every change must hold all three:

| Command | Bar |
|---|---|
| `npm run build` (`tsc -b && vite build`) | zero TypeScript errors |
| `npm run test` | green; count changes only when tests were genuinely added |
| `npx oxlint` | no new warnings against the measured baseline; **zero** `oxlint-disable` / `@ts-ignore` |

Measure the lint baseline before you start (`git archive HEAD web` into a scratch directory) rather
than assuming the current count is clean.

## 9. Known gaps — do not "fix" silently

- `docs/constitution.md:137` says **TypeScript strict**, but `"strict"` is absent from
  `web/tsconfig.app.json`. Enabling it is a repo-wide change that fails on untouched files, so it needs
  its own slice — do not flip it as a side effect.
- The root `.gitignore` rule `publish/` (meant for .NET output) also matches `web/src/features/publish/`.
  `web/.gitignore` negates it. Any new `publish/` directory elsewhere in `web/` hits the same trap.
- `lib/errorMessages.ts` is a point-in-time enumeration of the codes the API can return. Nothing fails
  mechanically when the API adds one; re-check it whenever the backend error surface changes.

## 10. Checklist

- [ ] Named exports only; `import type` for type-only imports
- [ ] Testable logic in `lib/` or a sibling `.ts`, not inline in a component
- [ ] Fetches use the `cancelled` guard and clean up; polls use chained `setTimeout`
- [ ] All API access through `requestJson`; no `fetch` in a component
- [ ] No raw error code can reach the screen; unknown codes fall back to the generic message
- [ ] Any login affordance is a plain `<a href>` (§5)
- [ ] Fragment cleared before any `await`; one-shot effects guarded against StrictMode
- [ ] `localStorage` only in `lib/tokenStorage.ts`
- [ ] Every interactive control is a `<button>`/`<a>`/`<Link>`, with `type="button"` where applicable
- [ ] Tunables from `lib/config.ts` and Vite env; `.env.example` updated
- [ ] Tests added for pure logic; component-level changes state how they were verified
- [ ] `npm run build`, `npm run test`, `npx oxlint` all hold (§8)
- [ ] `/docs` updated when behaviour or flow changed (`.claude/rules/docs-sync.md`)
