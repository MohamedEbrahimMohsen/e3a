## Goal

Creators can sign in with GitHub. `GET /api/auth/github/login` redirects to GitHub; the callback exchanges the code **server-side**, creates the user just-in-time, and returns an e3a JWT in the URL fragment. `GET /api/auth/me` returns the profile.

Browsing and installing stay fully anonymous — this only gates creating and publishing, which until now had no way to obtain a token at all.

## The `state` parameter

Anti-CSRF `state` is **stateless, signed and expiring**: `{nonce}.{expiresAt}.{HMACSHA256(CoreJwt:Key, payload)}`. `Core.Cache` is an empty placeholder and a distributed cache would have meant an Azure resource, which was off-limits for this run.

The signature is verified **before** the expiry, deliberately — so a tampered expiry reports `Invalid`, never `Expired`. The comparison is `CryptographicOperations.FixedTimeEquals`.

**That ordering is the interesting part of this PR.** Round 1's review found that the test claiming to lock it did not: it used a *future* expiry, so it passed under either ordering. Transposing the two blocks left all 419 tests green while shipping an expiry oracle — a forger could learn whether a rejection was staleness or a bad signature. The fix was one added test (`Validate_ShouldReturnInvalid_WhenExpiryIsMovedIntoThePastWithoutResigning`), no production change, proven by mutation: transposing the blocks fails that test and **only** that test.

## Security properties worth reviewing

- **No open redirect.** The callback binds exactly `code` and `state`; every redirect target comes from configuration. No `returnUrl` parameter exists anywhere in the slice.
- **No role claim is emitted.** `Program.cs` maps notification endpoints behind `RequireRole(RoleNames.User)`; emitting the role would have silently opened them to every GitHub visitor.
- **`NormalizedUserName` is set on just-in-time creation.** `AspNetUsers` has a unique index on it and SQL Server permits only one NULL — unset, the *second* user to ever sign in would fail at the database, and no unit test with a substituted repository could catch it.
- **Every callback failure is a 302 with an error code in the fragment**, never JSON. A browser mid-redirect cannot use a 400 body.
- `"System.Net.Http.HttpClient": "Warning"` added to `appsettings.Development.json` — `Microsoft.Extensions.Http` logs outbound headers at `Trace`, which would include the GitHub access token. GitHub OAuth App tokens do not auto-expire, so this was worth a line.

## Verification

| Check | Result |
|---|---|
| `dotnet build api/E3A.slnx --no-incremental` | 0 errors, 9 warnings — all pre-existing, all in `core-libraries` |
| `dotnet test api/E3A.slnx` | **420 / 420** (baseline 354) |

Migration `oauth004` adds exactly four columns on `AspNetUsers` plus one unique index filtered `[GitHubId] IS NOT NULL AND [IsDeleted] = 0`.

## What is NOT verified

**The live round trip against real GitHub is unverified.** It needs a human at a consent screen. Every branch is unit-tested and the emitted request was driven through the real client against a local `HttpListener` — proving it is well-formed (`User-Agent`, `Accept`, secret in the body and never in a URL, all five failure modes collapsing to `null`). That proves nothing about whether GitHub *accepts* it: the stub validates no credentials, no registered callback, no scope.

Please smoke-test the flow before relying on it.

## Known residual risk

A stateless `state` is not bound to the browser that started the flow, so classic login-CSRF is not fully closed. The fix is a `SameSite=Lax` nonce cookie compared on callback. Deferred deliberately — acceptance decision 2 fixed the mechanism for this slice.

## Pipeline artifacts

- [`00-acceptance.md`](.process/github-oauth/00-acceptance.md) — proxied scope, 10 product decisions
- [`01-plan.md`](.process/github-oauth/01-plan.md) — 24 decisions, state design, callback sequence
- [`02-implementation.md`](.process/github-oauth/02-implementation.md) — implementation + rework round 1
- [`03-review.md`](.process/github-oauth/03-review.md) · [`03-review-r2.md`](.process/github-oauth/03-review-r2.md) — **APPROVED**
- [`04-metrics.md`](.process/github-oauth/04-metrics.md) — run log

🤖 Generated with [Claude Code](https://claude.com/claude-code)
