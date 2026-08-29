# Plan — Frontend Auth & Workspace Wiring

## Convention note (read first)

`.claude/skills/dotnet-feature/SKILL.md` governs `api/` only and **does not apply to `web/`**.
**The repo has no frontend convention document** — `conventions/` contains only `dotnet-testing.md`.
The rules below are therefore derived from `docs/constitution.md` §0.3/§5, `docs/design-prompt.md`,
and the code already in `web/src`. The implementer follows these and nothing from the .NET skill:

| Rule | Source |
|---|---|
| Feature folders `src/features/<area>/<Name>Page.tsx`; shared UI in `src/components`; contexts in `src/app/*Context.tsx`; helpers in `src/lib/<lowercase>.ts` | existing tree |
| Named function exports (`export function X()`), no default exports | every existing file |
| `interface` for object shapes, `type` for unions; `type`-only imports use `import type` (`verbatimModuleSyntax: true`) | `lib/api.ts`, `lib/types.ts` |
| Inline `style={{}}` objects with `var(--token)` colours; class names from `index.css` (`btn-primary`, `btn-secondary`, `card`, `mono`, `link-quiet`, `hover-row`, `fade-in`, `code-block`, `input-field`) | every page |
| No comments explaining *what*; full words, no abbreviations (`engineerId` not `id`, `cancelled` not `c`) | constitution §3 |
| Timing/animation constants = module-level `SCREAMING_SNAKE` consts (`TOAST_DURATION_MS`); environment- or server-mirrored values = Vite env through `lib/config.ts` | `ToastContext.tsx`, `PublishStatusPage.tsx`, constitution §5 |
| Data fetching = `useEffect` + `let cancelled = false` guard + `.then/.catch`, cleanup sets `cancelled = true`. No data library is installed; do not add one | `CatalogPage.tsx`, `EngineerDetailPage.tsx` |
| Failure UI = heading + subline + `Retry` button driven by a `reloadToken` counter | `CatalogPage.tsx` lines 93–98 |

## Goal

After this ships a creator can click **Sign in with GitHub** in the nav, complete the real GitHub
round trip, land in `/workspace` showing the engineers that actually exist in the database, create a
new engineer, drop their zipped `.claude` folder on the composer, read the real import manifest the
API produced, press Publish, and watch the real `Queued → Building → Published` status poll to
completion with a copy-ready install command — all against `https://localhost:62935/api`, with the
JWT delivered in the URL fragment, stored in `localStorage`, and attached as a bearer token. Signing
out clears it. Anonymous catalog browsing is byte-for-byte unchanged.

## Scope

**In:**
- Real `AuthContext` (token + `GET /api/auth/me` session), `/auth/callback` route, fragment read + clear, sign-out.
- Authenticated transport: `lib/http.ts` with bearer attachment, typed `ApiError`, `401` handling.
- Route guarding for `/workspace*`.
- Workspace list (`GET /api/engineers/mine`), engineer composer (create → upload → manifest → publish),
  publish status polling (`GET /api/publish/{versionId}/status`).
- `installCommand` team form; `vitest` + a pure-logic test suite.
- Docs: one line in `docs/plugin-spec.md`, one clause in `docs/architecture.md`.

**Out:**
- Any change under `api/`. Any Azure resource (**none needed — this is browser code**).
- Team composer / team API, catalog redesign, reports, likes, install counts.
- `ProfilePage`, `HomePage`, `CatalogPage`, `EngineerDetailPage`, `TeamDetailPage` bodies (untouched
  except the one-argument `installCommand` fix in `TeamDetailPage`).
- Component/DOM tests, jsdom, any UI test library.

**Deferred (with why):**

| Item | Why |
|---|---|
| Wiring `/u/:login` to a real profile | Not in the acceptance scope list; needs a public per-creator endpoint that does not exist. Fixtures stay (decision 6). |
| Workspace limits meters (`12 / 50 engineers`) | No endpoint exposes `EngineersOptions.MaxEngineersPerCreator`. Reporting a finding, not inventing an endpoint (acceptance "Out of scope" clause). |
| Workspace `Version` column | `EngineerResult` carries `LatestVersionId` but no semantic version; per-row `GET /publish/{id}/status` would be N+1. Finding, not an API change. |
| Per-file scan findings on a rejected publish | `PublishStatusResult.FailureReason` is a single string in this branch. `docs/design-prompt.md` §10c describes the richer panel — that is the target, code lags = incompleteness. |
| `GET /api/engineers/slug-availability` | `CreateEngineerHandler` already auto-uniquifies via `EngineerSlugResolver`; the response's `slug` is authoritative. A pre-check would add a debounce and a second source of truth for zero gain. |
| `unlist` / `relist` / `DELETE` engineer actions | Endpoints exist but are not in the dev's confirmed flow (acceptance answer 2). Second use case → next slice. |
| Team surfaces beyond `installCommand` | `teams` slice, separate worktree/branch. |
| `"strict": true` in `tsconfig.app.json` | Constitution §5 says strict; the flag is absent today. Enabling it is a repo-wide change that would fail on untouched files — separate chore, reported as a finding. |

## Decisions

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | How does the login navigation guarantee the `SameSite=Lax`, `HttpOnly`, `Secure`, `Path=/api/auth` nonce cookie is set and returned? | The affordance is a **plain `<a href={gitHubLoginUrl()}>` styled `btn-primary`** — not `fetch`, not `<Link>`/`<NavLink>` (client-side routing), not `window.open`, not an iframe, not `fetch(..., {redirect:'manual'})`. `gitHubLoginUrl()` returns the absolute `${config.apiBaseUrl}/auth/github/login`. | Only a real top-level navigation to the API origin lets the browser (a) store a `Secure` cookie set by the API origin and (b) send it back on the GitHub→API callback: `SameSite=Lax` sends cookies on top-level cross-site **GET navigations** and on nothing else. A `fetch` would additionally fail CORS on the 302 to github.com, and `Program.cs` line 85 configures CORS **without `AllowCredentials`**, so no XHR path can ever carry the cookie. An anchor also survives JS being unavailable and supports middle-click. This is the failure mode that passes every unit test and dies in a browser — it is a Definition-of-Done line. |
| 2 | Token storage | `localStorage`, key `e3a.token`, via `lib/tokenStorage.ts` (the only module that touches `localStorage`) | Acceptance decision 1. Single choke point makes the XSS-readable trade-off auditable and the module testable. |
| 3 | Fragment clearing | `clearAuthFragment()` calls `window.history.replaceState(null, '', pathname + search)` **immediately after parsing, before any await**, on both the token and the error path | Acceptance decision 2. Clearing on the error path too keeps a reload from re-rendering a stale failure. |
| 4 | Expired / missing token | No refresh, no JWT decoding, no expiry pre-check. On boot: token present → `GET /api/auth/me`; `401`/`403` → `clearToken()` + signed out. Any **other** failure (network, 5xx) → signed out for this session but the token is **kept**. | Acceptance decision 3. Distinguishing the two stops a transient API outage from forcing a GitHub round trip on the next page load. |
| 5 | Extend `getJson` or add a sibling? | **Extract**: new `lib/http.ts` owns `ApiError`, `requestJson`, `setUnauthorizedHandler`. `lib/api.ts` keeps its exported catalog functions, drops its private `getJson` and its local `ApiError` class, and re-exports `export { ApiError } from './http'`. | `getJson` is private, GET-only, body-less and header-less; the workspace needs POST/PUT, JSON and `FormData` bodies, an `Authorization` header, a server error `code`/`message`, and a `401` hook. Growing the private helper would make `api.ts` the transport *and* the catalog client and would drag auth concerns into the anonymous path. The re-export keeps `EngineerDetailPage.tsx:4`'s `import { ApiError } from '../../lib/api'` compiling — no unrelated file is touched. |
| 6 | Is the bearer token attached to catalog calls too? | Yes — `requestJson` attaches `Authorization` whenever a token exists, with no opt-out flag. | Fewer knobs. Catalog endpoints are `[AllowAnonymous]`, so a stale token cannot 401 them (failed bearer validation on an anonymous endpoint leaves the user unauthenticated, it does not reject). Signed out → no token → no header → anonymous browsing provably unchanged. |
| 7 | `401` behaviour | `requestJson` on `401`: `clearToken()`, invoke the single registered unauthorized handler, then `throw new ApiError(401, code, message)`. `AuthProvider` registers `signOut` as that handler in an effect. | One place clears the token; React learns about it without every page catching 401. One module-level slot + one setter is the smallest thing that works and is node-testable. |
| 8 | Guarded route while signed out | `RequireAuth` renders a centred **"Sign in to continue"** panel containing the same sign-in anchor. It does **not** auto-navigate to GitHub. | "Redirect to sign-in" via auto-navigation creates a silent loop when the API 401s for a non-session reason (clock skew, revoked user): guard → GitHub → fresh token → 401 → guard → GitHub, with no error ever visible. One click costs nothing and the guard stays deterministic. |
| 9 | Landing after a successful callback | `/workspace`, `navigate(..., { replace: true })` | `docs/design-prompt.md` §Interactivity: "Sign in → My workspace". `replace` keeps `/auth/callback` out of the back stack. |
| 10 | `PublishStatusResult` shape | Type it as the **new** contract — `{ versionId, itemId, itemType, versionNumber, semanticVersion, status, zipUrl, zipSha256, sizeBytes, failureReason, updatedAt }` — and make no UI depend on `itemId` succeeding: the install block is rendered only if the follow-up `getEngineer(status.itemId)` resolves. | Acceptance contract note is binding ("build against the new shape"). But the `teams` rename is **not in this branch** — `api/E3A.Application/Publishing/Shared/PublishStatusResult.cs` still declares `EngineerId`, so today's JSON key is `engineerId`. Guarding the one dependent read makes the page correct in either merge order without a `itemId ?? engineerId` fallback that would have to be removed later. |
| 11 | Which `VersionIncrement` does Publish send? | A `<select>` in the composer footer, values `Patch | Minor | Major`, default `Patch`. | `PublishEngineerRequest.Increment` is `[JsonRequired]` — a value must be chosen. `SemanticVersionCalculator.Next(null, _)` returns `1.0.0` for a first publish regardless, so `Patch` is a safe default. `docs/design-prompt.md` §8 does not answer "which increment", so adding the control is not divergence. |
| 12 | Slug | Derived client-side by `toSlug(displayName)` (pure, mirrors `EngineerSlugGenerator.Normalize`), shown as a mono preview, sent on create. The **server's returned `slug` is authoritative** and replaces the preview. Slug is not editable in this slice. | `docs/design-prompt.md` §8: "slug preview in mono". `CreateEngineerHandler` normalizes and uniquifies; a client-side availability check would be a second source of truth. |
| 13 | Error text for API failures | The API already returns a **localized** `message` (`Core.Exceptions.ErrorResponseHandler` → `{ code, message }`). Show `message` when present. The client-side map in `lib/errorMessages.ts` covers only codes that arrive **without** a message: the six `#error=` callback codes, `USER_NOT_AUTHENTICATED` (ASP.NET returns an empty body for an unauthenticated `[Authorize]` hit), and the two client-side pre-upload checks. | Copying 60 resx strings into the SPA guarantees drift. `422` bodies join codes with `,` and messages with `" , "` — the message is human-readable, the joined code is not, so message-first is also correct there. |
| 14 | Unrecognised error code | `messageForErrorCode` returns the exported `GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.'`. **The raw code is never rendered** and never interpolated into the text. | Acceptance decision 4: "a raw code shown to a user is a dead end." Asserted by test. |
| 15 | Test runner setup | `vitest` dev dependency (acceptance decision 7), new `web/vitest.config.ts` with `environment: 'node'`, `include: ['src/**/*.test.ts']`. **No jsdom, no happy-dom, no testing-library.** DOM globals are faked per-test with `vi.stubGlobal`. `vite.config.ts` is left untouched. | Every unit under test is pure or touches exactly one global (`localStorage`, `window`, `fetch`). Adding a DOM implementation buys nothing for this slice and exceeds the authorised dependency. |
| 16 | Test file naming/placement | `<module>.test.ts`, beside the module. `describe('<exportName>')` + `it('should <outcome> when <condition>')`. | Mirrors `conventions/dotnet-testing.md` §2 naming philosophy (`Method_Should[Outcome]_When[Condition]`) in idiomatic vitest, and vitest's default `include` finds them without config gymnastics. |
| 17 | Upload input | `.zip` only (`accept=".zip"`), with a pure client-side pre-check for extension and size. Directory drops are not zipped in the browser. | `UploadEngineerDraftValidator` accepts exactly `[".zip"]`. Client-side zipping needs a new runtime dependency — out of scope. The dropzone label states the real requirement. |
| 18 | Max upload size shown in the UI | `config.maxUploadMegabytes` from `VITE_MAX_UPLOAD_MEGABYTES`, default `20`; `.env.example` updated. Display + pre-check only; `UploadsOptions.MaxZipSizeMegabytes` remains the enforcing authority. | Constitution §5: tunables come from Vite env with `.env.example` committed. Keeps `docs/design-prompt.md` §8's "max 20 MB" label truthful without a literal in a component. |
| 19 | Polling cadence | Module constants in `PublishStatusPage.tsx`: `POLL_INTERVAL_MS = 2000`, `POLL_MAX_ATTEMPTS = 60`. Chained `setTimeout`, never `setInterval`. | Mirrors the existing `STAGE_BUILDING_DELAY_MS` / `TOAST_DURATION_MS` precedent: pure client timings are named constants; env config is for values that must agree with a server or environment (decision 18). Chained timeouts cannot stack requests on a slow API; the attempt cap prevents an infinite poll on a stuck job. |
| 20 | Workspace "+ New Team" button | Removed. `/workspace/new-team` route and `TeamComposerPage` stay, untouched and still fixture-driven (reachable by URL); its `ComposerShell` gets `onPublish` = a toast instead of the now-dead `/workspace/publish` navigation. | The team API is not in this branch. A signed-in creator must not be walked into a mock that cannot publish. Removing a button the design doc lists is *incompleteness* (teams unbuilt), which `.claude/rules/docs-sync.md` explicitly says not to "fix" by editing docs. |
| 21 | Fixtures | Delete only the exports that become **entirely unreferenced** because this slice owns their surface: `workspaceRows`, `scanFindings`, `pickableSkills` (`lib/catalog.ts`) and `WorkspaceRow`, `ScanFinding`, `DraftSkill` (`lib/types.ts`). Everything else in both files stays. | Acceptance decision 6 protects fixtures for surfaces this slice does not own; these three back surfaces it does own and would be dead code. Verified unreferenced after the rewrites (`findByName` is still used by `TeamDetailPage` — it stays). |
| 22 | `docs/plugin-spec.md` edit | Add the team plugin-name form to §Naming. | Decision 8 makes `web/` emit `e3a-team-{slug}`; §Naming currently answers "what is a plugin called" with `e3a-{slug}` only. Code and doc would give two answers → divergence under `.claude/rules/docs-sync.md`. One line, `teams`-slice conflict risk accepted as trivial. |

## Existing code touched

| File | Change |
|---|---|
| `web/package.json` | add `vitest` to `devDependencies` (install with `npm install -D vitest@latest`, commit the resolved version and `package-lock.json`); add `"test": "vitest run"` to `scripts` |
| `web/.env.example` | fix `VITE_API_BASE_URL` to `https://localhost:62935/api` (matches `lib/config.ts` default and the running API); add `VITE_MAX_UPLOAD_MEGABYTES=20` |
| `web/src/lib/config.ts` | `installCommand(slug, itemType)`; add `maxUploadMegabytes` |
| `web/src/lib/api.ts` | delete private `getJson` + local `ApiError`; call `requestJson`; `export { ApiError } from './http'` |
| `web/src/lib/catalog.ts` | delete `workspaceRows`, `scanFindings`, `pickableSkills` and their now-unused type imports |
| `web/src/lib/types.ts` | delete `WorkspaceRow`, `ScanFinding`, `DraftSkill` |
| `web/src/app/AuthContext.tsx` | full rewrite — real session |
| `web/src/App.tsx` | add `/auth/callback` and `/workspace/engineers/:engineerId` routes; wrap creator routes in `RequireAuth` |
| `web/src/components/NavBar.tsx` | real sign-in anchor, real avatar/login, sign-out, loading slot |
| `web/src/features/composer/ComposerShell.tsx` | real user avatar/login; new optional props `onPublish`, `publishDisabled`, `publishLabel`, `statusLabel` |
| `web/src/features/composer/EngineerComposerPage.tsx` | full rewrite — upload-only |
| `web/src/features/composer/TeamComposerPage.tsx` | one line: pass `onPublish={() => showToast('Team publishing is not wired up yet')}` |
| `web/src/features/workspace/WorkspacePage.tsx` | full rewrite — real list |
| `web/src/features/publish/PublishStatusPage.tsx` | full rewrite — real polling |
| `web/src/features/detail/TeamDetailPage.tsx` | one line: `installCommand(item.name, 'Team')` |
| `docs/plugin-spec.md` | §Naming: append the team plugin-name sentence |
| `docs/architecture.md` | bullet on line 28: append the SPA-side clause |

## Files to create

### Library

**1 · `web/src/lib/http.ts`**
```ts
export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  constructor(status: number, code: string | null, message: string);
}
export interface RequestOptions { method?: string; body?: unknown; formData?: FormData; signal?: AbortSignal }
export function setUnauthorizedHandler(handler: (() => void) | null): void;
export async function requestJson<T>(path: string, options?: RequestOptions): Promise<T>;
```
`requestJson` steps, in order:
1. `const headers: Record<string, string> = {}`.
2. `const token = readToken(); if (token) { headers.Authorization = \`Bearer ${token}\`; }`.
3. Body: `options.formData` → pass through and **set no `Content-Type`** (the browser must add the multipart boundary). Else `options.body !== undefined` → `JSON.stringify(options.body)` + `headers['Content-Type'] = 'application/json'`. Else no body.
4. `const response = await fetch(\`${config.apiBaseUrl}${path}\`, { method: options.method ?? 'GET', headers, body, signal: options.signal })`.
5. `if (response.status === 401) { clearToken(); unauthorizedHandler?.(); }` — then fall into step 6.
6. `if (!response.ok)` → `const body = await readErrorBody(response)` → `throw new ApiError(response.status, body.code ?? null, body.message ?? \`API request failed with status ${response.status}\`)`.
7. `if (response.status === 204) { return undefined as T; }`.
8. `return (await response.json()) as T`.

Private `async function readErrorBody(response: Response): Promise<{ code?: string; message?: string }>` — `try { return await response.json() } catch { return {} }`. **Required**: ASP.NET returns an empty body for an unauthenticated `[Authorize]` hit and for a 403, so `response.json()` throws there.
Module state: `let unauthorizedHandler: (() => void) | null = null;`
Constant: `ApiError.message` default text is exactly today's string so the catalog path's behaviour is unchanged.

**2 · `web/src/lib/tokenStorage.ts`**
```ts
const TOKEN_STORAGE_KEY = 'e3a.token';
export function readToken(): string | null;   // localStorage.getItem(TOKEN_STORAGE_KEY)
export function writeToken(token: string): void;
export function clearToken(): void;           // localStorage.removeItem(...)
```
`localStorage` is read **inside** each function (never at module scope) so a test can stub it. This is the only file in `web/src` allowed to reference `localStorage`.

**3 · `web/src/lib/authFragment.ts`**
```ts
export interface AuthFragment { token: string | null; errorCode: string | null }
export function parseAuthFragment(hash: string): AuthFragment;
export function clearAuthFragment(): void;
```
`parseAuthFragment`: strip a leading `#`, `new URLSearchParams(...)`, return `{ token: params.get('token'), errorCode: params.get('error') }`. `URLSearchParams` performs the `Uri.EscapeDataString` decode that `AuthenticationRedirectUrlGenerator` requires. Empty/absent hash → `{ token: null, errorCode: null }`.
`clearAuthFragment`: `window.history.replaceState(null, '', \`${window.location.pathname}${window.location.search}\`)`.

**4 · `web/src/lib/errorMessages.ts`**
```ts
export const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';
export function messageForErrorCode(code: string | null | undefined): string;
export function messageForApiError(error: unknown): string;
```
Private `const errorMessages: Record<string, string>` — exactly these keys (values in the Error-code table below): `AUTHENTICATION_CODE_MISSING`, `AUTHENTICATION_STATE_INVALID`, `AUTHENTICATION_STATE_EXPIRED`, `GITHUB_TOKEN_EXCHANGE_FAILED`, `GITHUB_PROFILE_FETCH_FAILED`, `GITHUB_PROFILE_INVALID`, `USER_NOT_AUTHENTICATED`.
`messageForErrorCode(code)` → `errorMessages[code] ?? GENERIC_ERROR_MESSAGE` (guard `null`/`undefined` first).
`messageForApiError(error)` → `error instanceof ApiError` ? (`error.message.trim()` if non-empty, else `messageForErrorCode(error.code)`) : `GENERIC_ERROR_MESSAGE`.

**5 · `web/src/lib/slug.ts`**
```ts
export function toSlug(displayName: string): string;
```
Mirrors `EngineerSlugGenerator.Normalize` minus truncation: lowercase ASCII letters/digits kept; every other run collapses to a single `-`; no leading `-`; trailing `-` trimmed. Non-ASCII characters behave as separators.

**6 · `web/src/lib/initials.ts`**
```ts
export function initialsFor(name: string): string;
```
Split on `/[^a-z0-9]+/i`, take the first character of each non-empty word, uppercase, first two characters. Empty input → `''`. Consumed by `NavBar` and `ComposerShell`.

**7 · `web/src/lib/authApi.ts`**
```ts
export interface CurrentUser { id: string; gitHubId: number | null; gitHubLogin: string | null; displayName: string | null; avatarUrl: string | null; createdAt: string }
export function gitHubLoginUrl(): string;                 // `${config.apiBaseUrl}/auth/github/login`
export function getCurrentUser(): Promise<CurrentUser>;   // requestJson('/auth/me')
```

**8 · `web/src/lib/workspaceApi.ts`**
```ts
import type { HookWarning } from './api';

export interface Engineer { id: string; slug: string; displayName: string; description: string | null; tags: string[]; status: string; latestVersionId: string | null; installCount: number; createdAt: string; updatedAt: string }
export interface EngineerInput { slug: string; displayName: string; description: string | null; tags: string[] }
export interface ImportedItem { sourcePath: string; targetPath: string; category: string }
export interface ConvertedItem { sourcePath: string; targetPath: string; reason: string }
export interface SkippedItem { sourcePath: string; reason: string }
export interface ImportManifest { imported: ImportedItem[]; converted: ConvertedItem[]; skipped: SkippedItem[]; strippedPaths: string[]; hookWarnings: HookWarning[]; claudeMdSnippet: string | null; uploadedAt: string }
export type VersionIncrement = 'Patch' | 'Minor' | 'Major';
export interface PublishStatus { versionId: string; itemId: string; itemType: string; versionNumber: number; semanticVersion: string; status: string; zipUrl: string | null; zipSha256: string | null; sizeBytes: number; failureReason: string | null; updatedAt: string }

export function listMyEngineers(): Promise<Engineer[]>;                                  // GET  /engineers/mine
export function getEngineer(engineerId: string): Promise<Engineer>;                      // GET  /engineers/{id}
export function createEngineer(input: EngineerInput): Promise<Engineer>;                 // POST /engineers
export function updateEngineer(engineerId: string, input: EngineerInput): Promise<Engineer>; // PUT /engineers/{id}
export function uploadEngineerDraft(engineerId: string, file: File): Promise<ImportManifest>; // POST /engineers/{id}/upload
export function getImportManifest(engineerId: string): Promise<ImportManifest>;          // GET  /engineers/{id}/import-manifest
export function publishEngineer(engineerId: string, increment: VersionIncrement): Promise<PublishStatus>; // POST /engineers/{id}/publish
export function getPublishStatus(versionId: string): Promise<PublishStatus>;             // GET  /publish/{versionId}/status
```
`uploadEngineerDraft` builds `const formData = new FormData(); formData.append('file', file);` — the field name **must** be `file` (`[FromForm] IFormFile file`).
`publishEngineer` sends `{ increment }`. All ids are interpolated with `encodeURIComponent`.

### App shell

**9 · `web/src/app/RequireAuth.tsx`** — exports only `RequireAuth`.
```tsx
export function RequireAuth(): ReactElement
```
`const { status } = useAuth();` → `status === 'loading'` renders a centred muted `Loading…`; `status === 'signedIn'` renders `<Outlet />`; otherwise renders the local `SignInRequired` component: centred `card`, heading **"Sign in to continue"**, subline **"Creator tools need a GitHub account."**, and `<a className="btn-primary" href={gitHubLoginUrl()}>Sign in with GitHub</a>`.

**10 · `web/src/features/auth/AuthCallbackPage.tsx`**
```tsx
export function AuthCallbackPage(): ReactElement
```
State: `errorMessage: string | null`. `const handledRef = useRef(false)`.
`useEffect(() => { ... }, [])` — **StrictMode double-invokes effects; the ref guard is mandatory**:
1. `if (handledRef.current) { return; } handledRef.current = true;`
2. `const fragment = parseAuthFragment(window.location.hash);`
3. `clearAuthFragment();` — before any `await`.
4. `if (fragment.token) { completeSignIn(fragment.token).then(() => navigate('/workspace', { replace: true })).catch(error => setErrorMessage(messageForApiError(error))); return; }`
5. `setErrorMessage(messageForErrorCode(fragment.errorCode));` — covers `#error=CODE` **and** a bare visit with no fragment (both land on `GENERIC_ERROR_MESSAGE` for the latter).

Render: while `errorMessage === null` → centred `Completing sign-in…`. Otherwise a `card`: heading **"Sign-in failed"**, the message, and `<a className="btn-primary" href={gitHubLoginUrl()}>Try again</a>` plus a `<Link to="/">Back to home</Link>`.

### Composer

**11 · `web/src/features/composer/UploadDropzone.tsx`**
```tsx
export function UploadDropzone({ onFile, disabled, busy, maxMegabytes }: { onFile: (file: File) => void; disabled: boolean; busy: boolean; maxMegabytes: number }): ReactElement
```
Dashed-border panel (mirrors the existing upload-modal styling). Hidden `<input type="file" accept=".zip" />` triggered by click; `onDragOver`/`onDrop` with `event.preventDefault()` and a `dragActive` state for the violet border. Label: `Drop your zipped .claude folder`, subline `` `.zip · max ${maxMegabytes} MB` ``. `disabled` renders the muted variant with subline `Save the draft first` and ignores clicks/drops. `busy` renders `<span className="spinner" />` and `Uploading…`.

**12 · `web/src/features/composer/ImportManifestPanel.tsx`**
```tsx
export function ImportManifestPanel({ manifest, onReplace }: { manifest: ImportManifest; onReplace: () => void }): ReactElement
```
Three sections in this order, each a header row `label · count` with a chevron toggling an expanded file list (`expanded: Record<string, boolean>` state):
- **Imported** — `var(--success)` ✓; rows `sourcePath → targetPath` with a `category` chip.
- **Converted** — `var(--accent)` ⓘ; rows `sourcePath → targetPath` with `reason`.
- **Skipped** — muted; rows `sourcePath` with `reason`.
Below: when `manifest.hookWarnings.length > 0`, an amber banner `⚠ Includes {n} hooks that run automatically` listing `event` · `matcher` · `command` (mono). When `manifest.claudeMdSnippet` is non-null, a `view snippet` expander showing it in a `code-block`. When `manifest.strippedPaths.length > 0`, a muted line `{n} local files stripped` expanding to the paths.
Footer: ghost button **Replace upload** → `onReplace()`.

**13 · `web/src/features/composer/importManifestStructure.ts`**
```ts
export function toStructurePaths(manifest: ImportManifest): string[];
```
Collect `targetPath` from `manifest.imported` and `manifest.converted`, de-duplicate, sort ascending (`localeCompare`), return. Empty manifest → `[]`. Rendered through the existing `StructureTree` as `{ label: path, indent: path.includes('/') }`.

**14 · `web/src/features/composer/uploadFileValidation.ts`**
```ts
export function validateUploadFile(file: { name: string; size: number }, maxMegabytes: number): string | null;
```
Returns `null` when acceptable. Not `.zip` (case-insensitive suffix) → `'Only .zip archives are accepted.'`. `size > maxMegabytes * 1024 * 1024` → `` `That file is larger than the ${maxMegabytes} MB limit.` ``. Extension is checked before size.

### Publish

**15 · `web/src/features/publish/publishStage.ts`**
```ts
export const PUBLISH_STEP_LABELS = ['Queued', 'Building', 'Published'] as const;
export function stepIndexFor(status: string): number;      // Queued→0, Building→1, Published→2, anything else→-1
export function isTerminalStatus(status: string): boolean;  // Published | Rejected | Failed
export function isFailedStatus(status: string): boolean;    // Rejected | Failed
```
Implemented as `switch` on the exact strings the API emits (`ItemVersionStatus.ToString()`): `Queued`, `Building`, `Published`, `Rejected`, `Failed`. An unknown string is non-terminal, non-failed, index `-1`.

### Config

**16 · `web/vitest.config.ts`**
```ts
import { defineConfig } from 'vitest/config';
export default defineConfig({ test: { environment: 'node', include: ['src/**/*.test.ts'] } });
```

### Tests (10 files)

`web/src/lib/authFragment.test.ts`, `tokenStorage.test.ts`, `errorMessages.test.ts`, `http.test.ts`, `slug.test.ts`, `initials.test.ts`, `config.test.ts`,
`web/src/features/composer/importManifestStructure.test.ts`, `uploadFileValidation.test.ts`, `web/src/features/publish/publishStage.test.ts`.
Contents enumerated in **Test plan**.

## Rewritten files — contracts

**`web/src/app/AuthContext.tsx`**
```ts
export type AuthStatus = 'loading' | 'signedIn' | 'signedOut';
interface AuthContextValue {
  status: AuthStatus;
  signedIn: boolean;            // status === 'signedIn' — kept so NavBar/ComposerShell/TeamComposerPage compile
  login: string;                // user?.gitHubLogin ?? '' — kept for the same reason
  user: CurrentUser | null;
  completeSignIn: (token: string) => Promise<void>;
  signOut: () => void;
}
export function AuthProvider({ children }: { children: ReactNode }): ReactElement
export function useAuth(): AuthContextValue
```
- `const [user, setUser] = useState<CurrentUser | null>(null); const [status, setStatus] = useState<AuthStatus>('loading');`
- `loadSession = useCallback(async () => { if (!readToken()) { setUser(null); setStatus('signedOut'); return; } try { setUser(await getCurrentUser()); setStatus('signedIn'); } catch (error) { if (error instanceof ApiError && (error.status === 401 || error.status === 403)) { clearToken(); } setUser(null); setStatus('signedOut'); } }, [])`
- `signOut = useCallback(() => { clearToken(); setUser(null); setStatus('signedOut'); }, [])`
- `completeSignIn = useCallback(async (token: string) => { writeToken(token); await loadSession(); }, [loadSession])`
- `useEffect(() => { void loadSession(); }, [loadSession])`
- `useEffect(() => { setUnauthorizedHandler(signOut); return () => setUnauthorizedHandler(null); }, [signOut])`
- Context default value: `{ status: 'loading', signedIn: false, login: '', user: null, completeSignIn: async () => undefined, signOut: () => undefined }`.

**`web/src/App.tsx`** — routes after the change:
```
StandardLayout:  /  ·  /catalog  ·  /e/:name  ·  /t/:name  ·  /u/:login  ·  /how  ·  /auth/callback  ·  *
StandardLayout > RequireAuth:  /workspace  ·  /workspace/publish
ComposerLayout > RequireAuth:  /workspace/new-engineer  ·  /workspace/engineers/:engineerId  ·  /workspace/new-team
```
`RequireAuth` is a pathless `<Route element={<RequireAuth />}>` wrapper. No public route changes.

**`web/src/components/NavBar.tsx`** — `const { status, signedIn, login, user, signOut } = useAuth();`
- `status === 'loading'` → render a 32×32 empty placeholder in the auth slot (no sign-in-button flash).
- signed out → `<a className="btn-primary" style={{ padding: '8px 18px', fontSize: 13.5 }} href={gitHubLoginUrl()}>Sign in with GitHub</a>`. **Not a button, not `<Link>`** (decision 1).
- signed in → `Sign out` ghost link (`signOut()`, `showToast('Signed out')`, `navigate('/')`) then the avatar: `user.avatarUrl` → `<img src={user.avatarUrl} alt="" width={32} height={32} style={{ borderRadius: '50%' }} />`, else the existing circle with `initialsFor(user?.displayName ?? login)`; both `onClick={() => navigate(\`/u/${login}\`)}`.
- `My workspace` link condition unchanged (`signedIn`). The old `handleSignIn` mock is deleted.

**`web/src/features/composer/ComposerShell.tsx`** — props become
`{ title, lastSaved, onSaveDraft, children, onPublish?, publishDisabled?, publishLabel?, statusLabel? }`.
`onPublish ?? (() => navigate('/workspace/publish'))` preserves current behaviour for any caller that omits it; `publishLabel ?? 'Publish'`; `statusLabel ?? 'Draft'` fills the header chip; `publishDisabled` renders the primary button with `disabled` + muted styling. Header avatar uses `initialsFor` + `user.avatarUrl` exactly as `NavBar`.

**`web/src/features/workspace/WorkspacePage.tsx`**
State: `engineers: Engineer[]`, `status: 'loading' | 'ready' | 'failed'`, `reloadToken: number`.
Effect: `listMyEngineers()` with the `cancelled` guard; `.catch` → `status = 'failed'`.
Header: `My workspace` + a single `+ New Engineer` primary button → `/workspace/new-engineer`. **No limits meters, no New Team button** (decisions/deferred).
Table columns (grid `2.4fr 0.9fr 1.1fr 0.9fr 1.1fr 1.6fr`): `Name` (`emojiFor(slug)` + mono slug + muted displayName) · `Type` (literal `Engineer`) · `Status` chip · `Installs` (`installCount.toLocaleString('en-US')`) · `Updated` (`new Date(updatedAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })`) · `Actions`.
`statusChipStyle(status: string)`: `Published` → success; `Unlisted` → warning; anything else → neutral.
Actions: `Edit` → `/workspace/engineers/{id}` · `latestVersionId ? 'View status' → /workspace/publish?versionId={latestVersionId} : 'Publish' → /workspace/engineers/{id}` · `View` → `status === 'Published' ? '/e/{slug}' : '/workspace/engineers/{id}'`.
Empty (`ready` + 0 rows): centred `Nothing here yet` / `Compose your first engineer — upload your .claude folder and publish it.` + `+ New Engineer`.
Failure: `Could not load your workspace` / `The API is unreachable. Check that it is running, then retry.` + `Retry` (bumps `reloadToken`).

**`web/src/features/composer/EngineerComposerPage.tsx`** (serves both `/workspace/new-engineer` and `/workspace/engineers/:engineerId`)
State: `engineerId: string | null` (from `useParams`), `displayName`, `description`, `tags: string[]`, `tagDraft`, `serverSlug: string | null`, `manifest: ImportManifest | null`, `increment: VersionIncrement`, `saving`, `uploading`, `publishing`, `errorMessage: string | null`, `lastSaved: string`, `loadStatus: 'loading' | 'ready' | 'failed'`.
Derived: `slug = serverSlug ?? toSlug(displayName)`.
Load effect (only when `engineerId` is set): `getEngineer(engineerId)` → fill form + `serverSlug`; then `getImportManifest(engineerId)` → `setManifest`, and **`.catch(error => { if (error instanceof ApiError && error.code === 'ENGINEER_DRAFT_NOT_UPLOADED') { setManifest(null); return; } setErrorMessage(messageForApiError(error)); })`** — that 404 is the normal "no upload yet" state, not a failure.
`handleSaveDraft`: `const input = { slug, displayName, description: description || null, tags };` → `engineerId ? updateEngineer(engineerId, input) : createEngineer(input)`; on success `setServerSlug(result.slug)`, `setLastSaved('just now')`, `showToast('Draft saved')`, and when it was a create `navigate(\`/workspace/engineers/${result.id}\`, { replace: true })`. On failure `setErrorMessage(messageForApiError(error))`.
`handleFile(file)`: `const problem = validateUploadFile(file, config.maxUploadMegabytes); if (problem) { setErrorMessage(problem); return; }` → `uploadEngineerDraft(engineerId, file)` → `setManifest(result)`, `showToast('Upload imported')`.
`handlePublish`: `publishEngineer(engineerId, increment)` → `navigate(\`/workspace/publish?versionId=${result.versionId}\`)`.
Layout — `ComposerShell` with `title={engineerId ? displayName || 'Engineer' : 'New engineer'}`, `onPublish={handlePublish}`, `publishDisabled={!engineerId || manifest === null || publishing}`, and a two-pane grid:
- Left: `Name` input (slug preview mono `slug: {slug}`), `Description` textarea, `Tags` chip input (Enter/comma adds `toSlug`-normalised tag, `×` removes). **No persona editor** — `docs/design-prompt.md` §8 (upload-only) does not have one.
- Right: `manifest === null` → `<UploadDropzone disabled={engineerId === null} ... />`; else `<ImportManifestPanel manifest={manifest} onReplace={() => setManifest(null)} />` followed by `Structure preview` → `StructureTree` fed by `toStructurePaths(manifest)`.
- The increment `<select>` sits in the footer next to Publish (passed as part of `ComposerShell`'s children? No — rendered inside the right pane directly above the manifest, labelled `Version increment`, so `ComposerShell` gains no extra prop).
- `errorMessage` renders as a danger-tinted inline bar at the top of the left pane with a `×` that clears it.

**`web/src/features/publish/PublishStatusPage.tsx`**
`const versionId = useSearchParams()[0].get('versionId');`
State: `status: PublishStatus | null`, `engineer: Engineer | null`, `errorMessage: string | null`, `attempts`.
No `versionId` → centred `No publish selected` + `← Back to workspace`.
Poll effect (`[versionId]`): `let cancelled = false; let timer: number | undefined;` — `const tick = async () => { const result = await getPublishStatus(versionId); if (cancelled) return; setStatus(result); if (isTerminalStatus(result.status)) { if (result.status === 'Published') { getEngineer(result.itemId).then(setEngineer).catch(() => undefined); } return; } if (attempts >= POLL_MAX_ATTEMPTS) { setErrorMessage('This publish is taking longer than expected. Refresh to check again.'); return; } timer = window.setTimeout(tick, POLL_INTERVAL_MS); }` — errors from `getPublishStatus` → `setErrorMessage(messageForApiError(error))` and stop. Cleanup clears the timer and sets `cancelled`.
Render: stepper from `PUBLISH_STEP_LABELS` + `stepIndexFor(status.status)` (existing markup kept); success panel (green) with `v{semanticVersion}` badge and, **only when `engineer !== null`**, `<InstallBlock single line2={installCommand(engineer.slug, 'Engineer')} />` plus `View in catalog`; failure panel (red) when `isFailedStatus`, showing `status.failureReason ?? GENERIC_ERROR_MESSAGE` and `Fix and republish` → `/workspace/engineers/{status.itemId}`.
The `?mode=rejected` demo toggle and the `scanFindings` import are deleted.

**`web/src/lib/config.ts`**
```ts
export type PluginItemType = 'Engineer' | 'Team';
export function installCommand(slug: string, itemType: PluginItemType = 'Engineer'): string {
  return `/plugin install e3a-${itemType === 'Team' ? 'team-' : ''}${slug}@e3a`;
}
```
plus `maxUploadMegabytes: Number(import.meta.env.VITE_MAX_UPLOAD_MEGABYTES ?? 20)` inside the `config` object.

## Error codes

No new error codes — `api/E3A.Application/Exceptions/ErrorCodes.cs` is **not touched**. The table below is the
client-side mapping (`web/src/lib/errorMessages.ts`), i.e. the codes that reach the browser with **no**
server-rendered message.

| Constant (key) | Where it arrives | Client string |
|---|---|---|
| `AUTHENTICATION_CODE_MISSING` | `#error=` on `/auth/callback` | `GitHub did not send an authorization code. Please try signing in again.` |
| `AUTHENTICATION_STATE_INVALID` | `#error=` | `We could not verify that sign-in request. Please try again.` |
| `AUTHENTICATION_STATE_EXPIRED` | `#error=` | `That sign-in request expired. Please try again.` |
| `GITHUB_TOKEN_EXCHANGE_FAILED` | `#error=` | `We could not complete the sign-in with GitHub. Please try again.` |
| `GITHUB_PROFILE_FETCH_FAILED` | `#error=` | `We could not read your GitHub profile. Please try again.` |
| `GITHUB_PROFILE_INVALID` | `#error=` | `Your GitHub profile is missing details we need (a login and an id).` |
| `USER_NOT_AUTHENTICATED` | 401 body from a handler guard | `Your session has ended. Please sign in again.` |
| *(anything else / null)* | — | `GENERIC_ERROR_MESSAGE` = `Something went wrong. Please try again.` |

Verified against `api/E3A.Api/Resources/Messages.en.resx` lines 168–186: the six auth codes exist there
with equivalent English text; the SPA copies are intentional (the fragment carries a code only).
**No `.resx` file is edited — this slice adds no server error code, so `Messages.ar.resx` needs nothing.**
API errors reaching `messageForApiError` render `ErrorResponse.message`, already localized by
`Core.Exceptions.ErrorResponseHandler` via `ILocalizer`.

Codes with special handling (not user-visible errors):

| Code | Where | Behaviour |
|---|---|---|
| `ENGINEER_DRAFT_NOT_UPLOADED` | 404 from `GET /engineers/{id}/import-manifest` | Treated as "no upload yet" → render the dropzone. Never shown as an error. |

## Client behaviour — token lifecycle

| Step | Where | Exact behaviour |
|---|---|---|
| Issued | API | `AuthenticationRedirectUrlGenerator.Success` → 302 to `{WebRedirectUrl}#token={urlEncoded}` |
| Read | `AuthCallbackPage` effect, step 2 | `parseAuthFragment(window.location.hash)`; `URLSearchParams` decodes the escaping |
| Fragment cleared | same effect, step 3, **before any await** | `clearAuthFragment()` → `history.replaceState(null, '', pathname + search)` |
| Stored | `completeSignIn` → `writeToken` | `localStorage['e3a.token']` |
| Attached | `requestJson` step 2 | `Authorization: Bearer {token}` on **every** API call while a token exists; absent when signed out |
| Validated | `AuthProvider.loadSession` on every mount | `GET /api/auth/me` → `CurrentUser`; failure paths per decision 4 |
| Expired / rejected mid-session | `requestJson` step 5 | `clearToken()` + unauthorized handler → `signOut()` → `RequireAuth` shows the sign-in panel. **No refresh flow exists and none is added.** |
| Cleared on sign-out | `signOut` | `clearToken()`, `setUser(null)`, `setStatus('signedOut')`, toast, navigate `/` |

Anonymous invariant: no token in `localStorage` ⇒ no `Authorization` header ⇒ `/`, `/catalog`, `/e/:name`,
`/t/:name`, `/how`, `/u/:login` behave exactly as today.

## API surface consumed

| Method · route | Auth | Request | Response type consumed |
|---|---|---|---|
| `GET /api/auth/github/login` | anon | top-level navigation (anchor) | 302 → GitHub (sets the `e3a_oauth_state` nonce cookie) |
| `GET /api/auth/github/callback` | anon | browser-driven, never called by SPA code | 302 → `{web}/auth/callback#token=` \| `#error=` |
| `GET /api/auth/me` | bearer | — | `CurrentUser` |
| `GET /api/engineers/mine` | bearer | — | `Engineer[]` |
| `GET /api/engineers/{engineerId}` | bearer (draft) | — | `Engineer` |
| `POST /api/engineers` | bearer | `{ slug, displayName, description, tags }` | `201` `Engineer` |
| `PUT /api/engineers/{engineerId}` | bearer | same | `Engineer` |
| `POST /api/engineers/{engineerId}/upload` | bearer | `multipart/form-data`, field `file` | `ImportManifest` |
| `GET /api/engineers/{engineerId}/import-manifest` | bearer | — | `ImportManifest` (404 `ENGINEER_DRAFT_NOT_UPLOADED` = no draft) |
| `POST /api/engineers/{engineerId}/publish` | bearer | `{ increment }` | `202` `PublishStatus` |
| `GET /api/publish/{versionId}/status` | bearer | — | `PublishStatus` |

Base URL always `config.apiBaseUrl` (acceptance decision 5) — no literal host anywhere in `web/src`.

## Test plan

Runner `vitest`, `environment: 'node'`, no DOM library. Globals faked with `vi.stubGlobal` and restored
by `afterEach(() => vi.unstubAllGlobals())`. Bodies are unlabelled arrange/act/assert blocks with no
comments. The implementer writes exactly these — a branch with no row here will not be tested.

| # | Test file | `describe` › `it` | Asserts |
|---|---|---|---|
| 1 | `lib/authFragment.test.ts` | `parseAuthFragment` › should return the token when the fragment carries one | `parseAuthFragment('#token=abc.def')` → `{ token: 'abc.def', errorCode: null }` |
| 2 | " | " › should decode a percent-encoded token | `'#token=a%2Bb%3Dc'` → `token === 'a+b=c'` |
| 3 | " | " › should return the error code when the fragment carries one | `'#error=AUTHENTICATION_STATE_EXPIRED'` → `errorCode` equals it, `token` null |
| 4 | " | " › should return nulls when the fragment is empty | `''` and `'#'` → `{ token: null, errorCode: null }` |
| 5 | " | " › should ignore unrelated fragment parameters | `'#state=x&token=t'` → `token === 't'` |
| 6 | " | `clearAuthFragment` › should replace the URL without the fragment | stub `window` with `history.replaceState` spy + `location { pathname: '/auth/callback', search: '' }`; spy called once with `(null, '', '/auth/callback')` |
| 7 | " | " › should preserve the query string | `search: '?next=1'` → called with `'/auth/callback?next=1'` |
| 8 | `lib/tokenStorage.test.ts` | `tokenStorage` › should return null when no token is stored | stub `localStorage` (Map-backed); `readToken()` → `null` |
| 9 | " | " › should return the token that was written | `writeToken('jwt')` then `readToken()` → `'jwt'`; `setItem` called with `('e3a.token', 'jwt')` |
| 10 | " | " › should remove the token on clear | write, `clearToken()`, `readToken()` → `null` |
| 11 | `lib/errorMessages.test.ts` | `messageForErrorCode` › should map every callback error code to readable text | `it.each` over the 7 keys: result is non-empty, `!== code`, and does **not** contain `'_'` |
| 12 | " | " › should return the generic message for an unknown code | `messageForErrorCode('NOPE_NOT_REAL')` → `GENERIC_ERROR_MESSAGE`; result does not contain `'NOPE_NOT_REAL'` |
| 13 | " | " › should return the generic message for null | `messageForErrorCode(null)` → `GENERIC_ERROR_MESSAGE` |
| 14 | " | `messageForApiError` › should prefer the server message | `new ApiError(409, 'PUBLISH_ALREADY_IN_PROGRESS', 'A publish is already running.')` → that message |
| 15 | " | " › should fall back to the code map when the message is empty | `new ApiError(401, 'USER_NOT_AUTHENTICATED', '')` → the mapped string |
| 16 | " | " › should return the generic message for a non-ApiError | `messageForApiError(new Error('boom'))` → `GENERIC_ERROR_MESSAGE` |
| 17 | `lib/http.test.ts` | `requestJson` › should attach the bearer token when one is stored | stub `localStorage` with a token + `fetch` returning `{ ok: true, status: 200, json: async () => ({}) }`; `fetch` init `headers.Authorization === 'Bearer jwt'` |
| 18 | " | " › should send no authorization header when signed out | empty `localStorage`; `'Authorization' in headers` is `false` |
| 19 | " | " › should prefix the path with the configured API base URL | `fetch` called with `` `${config.apiBaseUrl}/auth/me` `` |
| 20 | " | " › should serialize a json body and set the content type | `{ method: 'POST', body: { increment: 'Patch' } }` → `body === '{"increment":"Patch"}'`, `headers['Content-Type'] === 'application/json'` |
| 21 | " | " › should send form data without a content type header | `{ formData }` → init `body` is the same `FormData` instance, `'Content-Type' in headers` is `false` |
| 22 | " | " › should clear the token and notify the handler on 401 | `fetch` → `{ ok: false, status: 401, json: rejects }`; expect rejection `ApiError` with `status 401`; `localStorage.removeItem` called with `'e3a.token'`; registered handler called once |
| 23 | " | " › should carry the server code and message on a failed response | status 409 body `{ code: 'X', message: 'Y' }` → `error.code === 'X'`, `error.message === 'Y'` |
| 24 | " | " › should survive a non-json error body | status 500, `json()` rejects → `ApiError`, `code === null`, message contains `'500'` |
| 25 | " | " › should not call the unauthorized handler on a 403 | status 403 → handler not called, token not cleared, throws `ApiError` |
| 26 | " | " › should return the parsed body on success | `json` → `{ id: '1' }`; result deep-equals it |
| 27 | `lib/slug.test.ts` | `toSlug` › should lowercase and hyphenate a display name | `'Payments Engineer'` → `'payments-engineer'` |
| 28 | " | " › should collapse runs of separators | `'A -- B__C'` → `'a-b-c'` |
| 29 | " | " › should trim leading and trailing separators | `'  Hello!  '` → `'hello'` |
| 30 | " | " › should return an empty string for input with no ascii alphanumerics | `'…—'` → `''` |
| 31 | `lib/initials.test.ts` | `initialsFor` › should take the first letter of the first two words | `'Mohamed Mohsen'` → `'MM'` |
| 32 | " | " › should handle a single-word name | `'mohamed-dive'` → `'MD'` |
| 33 | " | " › should return an empty string for an empty name | `''` → `''` |
| 34 | `lib/config.test.ts` | `installCommand` › should emit the engineer form by default | `installCommand('payments-engineer')` → `'/plugin install e3a-payments-engineer@e3a'` |
| 35 | " | " › should emit the team form for a team | `installCommand('full-stack-squad', 'Team')` → `'/plugin install e3a-team-full-stack-squad@e3a'` |
| 36 | `features/composer/importManifestStructure.test.ts` | `toStructurePaths` › should list imported and converted target paths sorted | manifest with `skills/b/SKILL.md`, `agents/a.md`, converted `skills/house-rules/SKILL.md` → `['agents/a.md', 'skills/b/SKILL.md', 'skills/house-rules/SKILL.md']` sorted ascending |
| 37 | " | " › should de-duplicate repeated target paths | duplicate target in imported + converted → one entry |
| 38 | " | " › should return an empty array for an empty manifest | `[]` |
| 39 | " | " › should ignore skipped and stripped entries | manifest with only `skipped`/`strippedPaths` → `[]` |
| 40 | `features/composer/uploadFileValidation.test.ts` | `validateUploadFile` › should accept a zip within the limit | `{ name: 'claude.zip', size: 1024 }, 20` → `null` |
| 41 | " | " › should reject a file that is not a zip | `{ name: 'claude.tar.gz', size: 10 }` → message mentioning `.zip` |
| 42 | " | " › should reject a zip over the limit | `{ name: 'a.zip', size: 21 * 1024 * 1024 }, 20` → message containing `'20'` |
| 43 | " | " › should accept an uppercase extension | `{ name: 'A.ZIP', size: 10 }` → `null` |
| 44 | `features/publish/publishStage.test.ts` | `stepIndexFor` › should map each pipeline status to its step | `Queued`→0, `Building`→1, `Published`→2 |
| 45 | " | " › should return -1 for an unknown status | `'Nonsense'` → `-1` |
| 46 | " | `isTerminalStatus` › should be true for finished statuses | `Published`, `Rejected`, `Failed` → true |
| 47 | " | " › should be false while the job is running | `Queued`, `Building` → false |
| 48 | " | `isFailedStatus` › should be true only for rejected and failed | `Rejected`/`Failed` true; `Published`/`Queued`/`Building` false |

**Deliberately not tested** (and why, so the reviewer does not read it as a gap): React components and
routing (no DOM runner is authorised; the value of this slice is the plumbing, per planning requirement 7);
`gitHubLoginUrl()` (a one-line string concat already covered by test 19's base-URL assertion);
`workspaceApi` functions (thin `requestJson` call sites — the transport is covered by tests 17–26).

## Docs sync (`.claude/rules/docs-sync.md`)

Judged **divergence** (must change in this slice):

| Doc | Section | Edit |
|---|---|---|
| `docs/plugin-spec.md` | `## Naming` | Append: `Team plugin name: `e3a-team-{slug}` — the `team-` infix keeps team and engineer plugin names in one flat namespace.` Required because `installCommand` now emits that name (decision 22). |
| `docs/architecture.md` | line 28 bullet **"Auth is a fragment handoff."** | Append: `The SPA reads the token once from the fragment, strips the fragment with `history.replaceState`, and keeps it in `localStorage`; there is no refresh token, so the JWT's own expiry ends the session and a 401 signs the creator out.` Required because this slice decides a storage/session policy the bullet leaves open. |

Judged **incompleteness** (code lags the target — no doc edit, per the rule's explicit instruction):

- `docs/design-prompt.md` §8 already reads **"Engineer composer (upload-only)"** with dropzone → import
  manifest → structure preview → sticky footer. The current `EngineerComposerPage.tsx` is the superseded
  skill-picking mock; this slice moves code **towards** the doc. No edit.
- §7 lists two limits meters and a "New Team" button; both are dropped for lack of an endpoint / an API.
- §10(c) describes a per-file scan-report panel; the API exposes one `failureReason` string today.
- §8's "Drop your .claude folder or .zip" — only `.zip` is accepted (browser cannot zip a dropped folder
  without a new dependency). The label states the real requirement.

`docs/implementation-plan.md` is unaffected: no feature is added, dropped, or re-scoped (line 9 already
records "skill-picking composer (deferred)"). `docs/security-scan.md` and `docs/constitution.md` are
unaffected. **No doc is created anywhere outside `/docs`** (this plan lives in `.process/`, which is the
pipeline artifact location, not documentation).

## Azure / infrastructure

**None.** This slice is browser code plus one dev dependency. No resource, no queue, no blob, no
configuration section is created. Two **existing** settings must already be correct on the dev machine
for a manual smoke test — reported, not changed:

- `GitHubAuthentication:WebRedirectUrl` must be `{web origin}/auth/callback` (e.g. `http://localhost:5173/auth/callback`).
- `Program.cs` line 85 allows CORS origins `http://localhost:5173` / `:5174` only; `web/.env.local` already
  points at `https://localhost:62935/api`, which matches the `lib/config.ts` default.

## What cannot be verified without a human

The **live GitHub round trip** — `GET /api/auth/github/login` → GitHub consent screen → `GET /api/auth/github/callback`
→ `302 …#token=` — requires a person to click "Authorize" on github.com and a registered OAuth app with a
matching callback URL. No agent can complete it.

What the plan does instead, and what the report must state plainly:

| Segment | How it is exercised here |
|---|---|
| Building the login URL | `gitHubLoginUrl()` asserted implicitly via test 19's base-URL rule; verified by reading the rendered `href` |
| Cookie set / returned on the callback | **Not exercised.** Guaranteed structurally by decision 1 (anchor = top-level navigation). Reviewer checks the anchor, not a test. |
| Token arriving in the fragment | Tests 1–5 replay the exact strings `AuthenticationRedirectUrlGenerator.Success/Failure` produce |
| Fragment cleared | Tests 6–7 |
| Storage, attachment, 401 | Tests 8–10, 17–26 |
| `#error=` rendering | Tests 11–13 against the six real `ErrorCodes` values |
| Everything after the token exists (workspace, upload, publish, poll) | Reachable manually by pasting a valid JWT into `localStorage['e3a.token']`; the implementer must record in `02-implementation.md` whether this manual pass was run and against what |

## Definition of done

- [ ] The sign-in affordance is a plain `<a href>` to `${config.apiBaseUrl}/auth/github/login`; `grep -rn "fetch\|Link\|window.open" web/src/components/NavBar.tsx web/src/app/RequireAuth.tsx` shows no fetch/router navigation for login.
- [ ] `/auth/callback` route exists; the effect reads the hash, calls `clearAuthFragment()` before any `await`, and is guarded by a `useRef` against StrictMode's double invoke.
- [ ] `localStorage` appears in exactly one file: `web/src/lib/tokenStorage.ts`.
- [ ] `requestJson` attaches `Authorization: Bearer` iff a token exists, and on `401` clears the token, calls the unauthorized handler, and throws `ApiError`.
- [ ] `AuthProvider` registers `signOut` as the unauthorized handler and unregisters it on unmount.
- [ ] No refresh-token code, no JWT decoding, no expiry timer anywhere.
- [ ] `messageForErrorCode` covers the 7 listed codes and returns `GENERIC_ERROR_MESSAGE` otherwise; no rendered string ever contains a SCREAMING_SNAKE code.
- [ ] `ENGINEER_DRAFT_NOT_UPLOADED` from the manifest GET renders the dropzone, not an error.
- [ ] `/workspace`, `/workspace/publish`, `/workspace/new-engineer`, `/workspace/engineers/:engineerId`, `/workspace/new-team` are inside `RequireAuth`; `/`, `/catalog`, `/e/:name`, `/t/:name`, `/how`, `/u/:login`, `/auth/callback` are not.
- [ ] The composer performs create → upload → manifest → publish against the endpoints named in **API surface consumed**, with the upload field named `file`.
- [ ] `PublishStatusPage` polls with chained `setTimeout` at `POLL_INTERVAL_MS`, stops on `isTerminalStatus`, caps at `POLL_MAX_ATTEMPTS`, and clears its timer on unmount.
- [ ] `PublishStatus` is typed with `itemId`/`itemType`; nothing breaks if the engineer lookup fails.
- [ ] `installCommand` emits `e3a-{slug}` for engineers and `e3a-team-{slug}` for teams; `TeamDetailPage` passes `'Team'`.
- [ ] No file under `api/` is modified; `git diff --name-only` touches only `web/`, `docs/plugin-spec.md`, `docs/architecture.md`, `.process/frontend-auth/`.
- [ ] No literal API host in `web/src`; `maxUploadMegabytes` comes from Vite env and `.env.example` is updated.
- [ ] The 26 files listed in **Files to create** (16 modules + 10 test files) exist and no others; the 17 in **Existing code touched** are the only modifications.
- [ ] `workspaceRows`, `scanFindings`, `pickableSkills`, `WorkspaceRow`, `ScanFinding`, `DraftSkill` are deleted; every other fixture export is untouched and still referenced.
- [ ] `npm run build` (`tsc -b && vite build`) passes with zero errors; `npm run lint` (oxlint) reports no new findings; `npm run test` runs the 48 cases green.
- [ ] `docs/plugin-spec.md` and `docs/architecture.md` carry the two edits above and nothing else.
- [ ] No Azure resource, no new runtime dependency; `vitest` is the only added package (dev).
- [ ] `02-implementation.md` states explicitly which segments of the GitHub round trip were exercised and which were not.
