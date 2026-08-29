# Stage 0 — Workflow Acceptance (PROXIED)

**Date:** 2026-08-29
**Feature slug:** `github-oauth`
**Base branch:** `main` @ `6def01a` (tree clean, in sync with origin)
**Pipeline snapshot:** `00-pipeline.svg`

## Dev authorisation

The dev is asleep and granted blanket authority for four consecutive features:

> go ahead and do the fearures 1, 2, 3 and 6, regarding the feature 4 & 5 skip it for now.
> but I will go to slepp now, so ask all your questions or your requirments now and when you're
> ready to go, don't ever stop by any mean untill you finished, I grant you all the permissions to
> commit, create PR, merge PR, anything will not block the implementation do it unless you needed
> to create any resource in Azure, that's only my job.

Stage 0 acceptance, the Stage 1 plan gate, and every product judgment call are **proxied by the
orchestrator** and recorded here as an explicit veto list.

**Standing prohibition:** no Azure resource may be created. This slice needs none.

## Models

All stages run on **OPUS 5**, per the dev's standing instruction.

## Feature request

GitHub OAuth login for creators. A visitor clicks "Sign in with GitHub", authorises the e3a GitHub
App, and returns to the web app holding an e3a JWT. Browsing and installing stay anonymous; only
creating and publishing require the login.

## Dev's answers, already given (binding — do not re-decide)

| # | Question | ANSWER |
|---|----------|--------|
| 1 | GitHub App callback URL | **Confirmed as registered:** `https://localhost:62935/api/auth/github/callback` — already in `appsettings.json`. |
| 2 | Token delivery to the browser | **URL fragment.** The API redirects to `/auth/callback#token=…`. Never sent to a server, never in logs. Explicitly **not** an httpOnly cookie. |
| 3 | First login | Creates the user record **just-in-time**. |
| 4 | Seeded engineers | Keep their existing owner rows; no migration of ownership. |

Credentials are already on disk in `api/E3A.Api/appsettings.json` (git-ignored) under
`GitHubAuthentication`: `AppId`, `ClientId`, `ClientSecret`, `AuthorizationUrl`, `AccessTokenUrl`,
`UserProfileUrl`, `CallbackUrl`, `WebRedirectUrl`, `StateExpirationMinutes`.

## In scope

1. `User` gains GitHub identity fields — GitHub numeric id (unique), login, display name, avatar URL —
   plus a migration (`oauth-004`).
2. `GET /api/auth/github/login` → 302 to GitHub's authorize URL carrying `client_id`, `redirect_uri`,
   `scope`, and an anti-CSRF `state`.
3. `GET /api/auth/github/callback` → validates `state`, exchanges `code` for a GitHub access token
   **server-side**, reads the GitHub profile, creates-or-updates the user, issues an e3a JWT, and
   redirects to `WebRedirectUrl` with the token in the **URL fragment**.
4. `GET /api/auth/me` → the authenticated user's profile.
5. Options class bound to the existing `GitHubAuthentication` section.
6. Postman collection updated — three new requests (blocking pipeline rule).
7. Docs sync per `.claude/rules/docs-sync.md`.

## Out of scope

- Refresh tokens and logout. The JWT's own expiry is the session.
- Linking or unlinking a GitHub account from an existing e3a user.
- Roles/permissions beyond what already exists.
- The frontend sign-in surfaces — that is feature 4 of this run, which consumes these endpoints.
- Any change to how `ICurrentUserService` resolves claims.

## Proxied product decisions (dev veto list)

| # | Decision | Call | Rationale |
|---|----------|------|-----------|
| 1 | Token issuance | **Reuse `Core.Identity.ITokenService.GenerateTokenAsync(List<Claim>)`** | Already in the solution, already what every other authenticated endpoint validates against. Introducing a second token format would split the auth model. |
| 2 | Anti-CSRF `state` | **Stateless, signed, and expiring** — carry the expiry inside the value and sign it; do not add a server-side store | `Core.Cache` is an empty placeholder (`Class1.cs`), and a distributed cache would mean an Azure resource, which is forbidden. `StateExpirationMinutes` is already in config for exactly this. |
| 3 | The GitHub App's client secret | **Read from configuration only.** Never logged, never returned, never placed in a redirect URL | It is already on disk in a git-ignored file. The dev still owes a secret rotation before the repo goes public — carried, not fixed here. |
| 4 | User matching | **By GitHub numeric id, not by login** | GitHub logins are renameable and reusable; the numeric id is stable. Matching on login would let a renamed account collide with a new one. |
| 5 | Profile refresh | **Update display name and avatar on every login** | Cheap, and keeps attribution current without a sync job. |
| 6 | Missing e-mail on the GitHub profile | **Not required.** e3a never sends mail | Requesting the `user:email` scope for data we do not use is a permission we should not ask for. |
| 7 | Slug on first login | **Not auto-assigned.** The engineer slug stays creator-typed, as the `engineer-slug` slice established | Auto-deriving a slug from the GitHub login would silently reintroduce the naming the dev replaced. |
| 8 | `IsBlocked` | **Not added.** The implementation plan lists it, but the abuse/report feature is explicitly skipped | Adding a flag nothing reads is dead schema. Not a docs divergence — incompleteness is never a violation. |
| 9 | Redirect target | **`WebRedirectUrl` from configuration, never from the request** | An attacker-supplied `redirect_uri` is the classic open-redirect in this flow. |
| 10 | Failure handling | **Redirect to the web app with an error code in the fragment**, not a raw API error page | The user is in a browser mid-flow; a JSON 400 is a dead end. |

## Known constraint — stated up front

**The live round trip cannot be verified without the dev.** It needs a human at a GitHub consent
screen. Everything up to and including the redirect is unit-testable and will be tested; the report
must say plainly that the end-to-end flow is unverified.

## Known debts NOT to be fixed here

Everything carried from earlier slices, plus the `security-scan` branch parked at
`.process/security-scan/09-stopped.md`.
