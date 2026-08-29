# Stage 0 — Workflow Acceptance (PROXIED)

**Date:** 2026-08-29
**Feature slug:** `frontend-auth`
**Pipeline snapshot:** `00-pipeline.svg`

## Dev authorisation

Blanket authority for four consecutive features (feature 6 in the dev's numbering, feature 4 of this run):

> go ahead and do the fearures 1, 2, 3 and 6, regarding the feature 4 & 5 skip it for now.
> ... I grant you all the permissions to commit, create PR, merge PR, anything will not block the
> implementation do it unless you needed to create any resource in Azure, that's only my job.

**Standing prohibition:** no Azure resource. This slice creates none — it is browser code.

## Models

All stages run on **OPUS 5**.

## Dev's answers, already given (binding — do not re-decide)

| # | Question | ANSWER |
|---|----------|--------|
| 1 | Token delivery | **URL fragment.** The API redirects to `/auth/callback#token=…`. Never sent to a server, never in logs. Explicitly **not** an httpOnly cookie. |
| 2 | Workspace flow | **Confirmed:** create engineer → upload `.claude` zip → review import manifest → publish → poll status. The old compose-from-parts mock design is superseded. |

## Feature request

Wire the frontend auth surfaces to the real API. Today `web/` runs entirely on static fixtures
(`web/src/lib/catalog`) and a mock composer. After this slice a real creator can sign in with GitHub,
land in a workspace, create an engineer, upload their `.claude` folder, review what was imported,
publish, and watch the status.

## In scope

1. **Auth plumbing** — a "Sign in with GitHub" affordance, the `/auth/callback` route that reads the
   token from the URL fragment, token storage, an authenticated fetch wrapper, and sign-out.
2. **Session state** — who is signed in (`GET /api/auth/me`), surfaced in the header; anonymous
   browsing must keep working untouched.
3. **Workspace** — list my engineers, create one, and the confirmed flow: upload zip → review the
   import manifest → publish → poll `GET /api/publish/{versionId}/status`.
4. **Route guarding** — creator-only routes redirect to sign-in; the public catalog stays anonymous.
5. Real API types matching the shipped contracts, replacing fixtures on the surfaces this slice owns.

## Out of scope

- The team composer UI. Teams ship their API in the `teams` slice; the dev's confirmed workspace flow
  does not include a team composer, and inventing one would be designing product unattended.
- Catalog redesign, install counts, reports, likes.
- Any change to the API. If a surface needs an endpoint that does not exist, that is a finding to
  report, not a reason to add one here.

## Proxied product decisions (dev veto list)

| # | Decision | Call | Rationale |
|---|----------|------|-----------|
| 1 | Token storage | **`localStorage`**, read once from the fragment then the fragment is cleared from the URL | The dev chose fragment delivery precisely so the token never reaches a server. `sessionStorage` would drop the session on every tab open; a cookie contradicts the dev's explicit answer. Trade-off recorded: `localStorage` is XSS-readable, which is the accepted cost of a fragment-delivered bearer token. |
| 2 | Clearing the fragment | **`history.replaceState`** immediately after reading | Otherwise the token sits in the address bar, gets copied into shared links, and lands in browser history. |
| 3 | Expired or missing token | **Treat as signed out**, no refresh flow | The OAuth slice deliberately shipped no refresh tokens; the JWT's own expiry is the session. |
| 4 | `#error=` from the callback | **Render the error and offer retry**, mapped to readable text | The API redirects failures as `#error=<CODE>`; a raw code shown to a user is a dead end. |
| 5 | API base URL | **From existing config**, never hardcoded | `web/src/lib/config.ts` already owns this. |
| 6 | Fixtures | **Left in place for surfaces this slice does not own** | Deleting them would break the public catalog, which is out of scope. |
| 7 | Test runner | **Add `vitest` as a dev dependency** | `web/package.json` has no test runner at all today, so the token-fragment parsing, storage and error-mapping logic would ship untested. Untested auth-token handling in a portfolio piece is a worse outcome than one dev-dependency. `vitest` is the standard Vite runner and adds no runtime weight. |
| 8 | Team surfaces | **`installCommand` gains a team form**, since teams are now `e3a-team-{slug}` | The helper is already shared; leaving it engineer-only would silently emit wrong install commands once teams appear in the catalog. |

## Known constraint

**The live GitHub round trip cannot be verified without the dev** — it needs a human at a consent
screen. Everything up to the redirect and everything after the token arrives is testable; the report
must say plainly which parts were exercised and which were not.

## Contract note — a response shape changed under this slice

The `teams` slice renamed `PublishStatusResult.EngineerId` to **`ItemId`** and added an `ItemType` string.
`GET /api/publish/{versionId}/status` and both publish `202` bodies now return:

```
{ versionId, itemId, itemType, versionNumber, semanticVersion, status,
  zipUrl, zipSha256, sizeBytes, failureReason, updatedAt }
```

Build against the new shape. Nothing in `web/` consumes that endpoint yet, which is why the rename was
safe to make.

## Known state of the frontend today

`web/src/app/AuthContext.tsx` is **entirely mocked** — a `signedIn` boolean and a hardcoded
`MOCK_LOGIN`. `web/src/lib/api.ts` has real anonymous catalog calls and no authenticated path.
The routes, the workspace page, the publish-status page and the profile page all exist as shells.
This slice replaces the mock with the real flow; it does not redesign the pages.
