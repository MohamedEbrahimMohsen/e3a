## Goal

Creators can sign in with GitHub. `GET /api/auth/github/login` redirects to GitHub; the callback exchanges the code **server-side**, creates the user just-in-time, and returns an e3a JWT in the URL fragment. `GET /api/auth/me` returns the profile.

Browsing and installing stay fully anonymous — this only gates creating and publishing, which until now had no way to obtain a token at all.

## The `state` parameter

Anti-CSRF `state` is **signed, expiring and browser-bound**: `{nonce}.{expiresAt}.{HMACSHA256(CoreJwt:Key, payload)}`, and the same `nonce` is also written to a short-lived `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/api/auth` cookie. The **server** keeps no state — `Core.Cache` is an empty placeholder and a distributed cache would have meant an Azure resource, which was off-limits for this run — so the binding is done by fixed-time comparing that cookie against segment 0 of the state on the callback.

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
| `dotnet test api/E3A.slnx` | **437 / 437** (baseline 354) |

Migration `oauth004` adds exactly four columns on `AspNetUsers` plus one unique index filtered `[GitHubId] IS NOT NULL AND [IsDeleted] = 0`.

## What is NOT verified

**The live round trip against real GitHub is unverified.** It needs a human at a consent screen. Every branch is unit-tested and the emitted request was driven through the real client against a local `HttpListener` — proving it is well-formed (`User-Agent`, `Accept`, secret in the body and never in a URL, all five failure modes collapsing to `null`). That proves nothing about whether GitHub *accepts* it: the stub validates no credentials, no registered callback, no scope.

Please smoke-test the flow before relying on it.

## Login-CSRF: found late, fixed, verified

The first version of this slice shipped a **stateless** `state`, so a signed state was not bound to the browser that started the flow — classic login-CSRF. Two internal review rounds approved it; **CodeRabbit found it on the PR.** Worth recording as-is: the pipeline's own reviewers missed a real security hole and an external reviewer caught it.

What shipped in response:

- `/api/auth/github/login` sets `e3a_oauth_state` — `Secure`, `HttpOnly`, `SameSite=Lax`, `IsEssential`, `Path=/api/auth`, `Max-Age` = the state expiry — carrying the same nonce that is signed into the state.
- The callback compares the cookie against segment 0 of the state with `CryptographicOperations.FixedTimeEquals`, **before** the code exchange. A mismatch or a missing cookie is `#error=AUTHENTICATION_STATE_INVALID` and GitHub is never called.
- The cookie is cleared **only on callbacks where the nonce actually matched** — including ones that fail later, so the state stays single-use per browser. A callback that validated nothing (no `code`, or a nonce that did not match) leaves the cookie alone, so an attacker cannot kill a victim's in-flight login with a bare `GET /api/auth/github/callback`. That gap was CodeRabbit round 2; it is fixed here and pinned by three handler tests plus mutation probes.
- Verified independently by mutation: deleting the nonce comparison turns the guarding test red and only that test.

**The residual risk that remains** is narrower and is follow-up **F1** below: the nonce travels verbatim as segment 0 of `state`, and there is no PKCE. An attacker who obtains a victim's **in-flight** state value (never sent to the attacker — it lives in the victim's address bar, history and GitHub's logs) could pair it with an attacker-owned `code`. Hashing the nonce into the state does **not** close that path — the victim's browser supplies the matching cookie either way. Only PKCE does. Do not treat the hashing idea, which appears in our own verification note, as a remedy.

## Correction carried forward from the rework

`07-coderabbit-rework.md:84` justifies the `null` `MaxAge` on the cookie-deletion path with a reason that is **wrong**, and that file is append-only, so the correction lives here. On this repo's SDK — **10.0.400**, TFM `net10.0` — `ResponseCookies.Delete(string, CookieOptions)` copies the options and then forces `Expires = DateTimeOffset.UnixEpoch` and `MaxAge = null` unconditionally. A `MaxAge` left on the options could never have emitted `Max-Age`. The code is correct either way and `OAuthStateCookieOptionsGenerator.Generate(TimeSpan? maxAge = null)` stays as the safer shape — but nobody should later "simplify" the delete path on the strength of the stated reason.

## Follow-ups leaving with this PR

| # | Item | Why it is not here |
|---|---|---|
| **F1** | **PKCE** (`S256` challenge + browser-bound verifier) on the GitHub flow, with a regression test proving a `code` from another flow cannot complete a login. | Not reachable without a leaked in-flight `state`; it changes the outbound GitHub contract — the one part of this slice nobody here can verify (see *What is NOT verified*); and it is a slice in its own right, not a patch. |
| **F2** | Atomic first-login user creation: resolve the `IX_AspNetUsers_GitHubId` conflict at the **repository/persistence boundary**, so the loser of a concurrent first login gets `#error=` instead of a JSON 500. Never a handler `try`/`catch` — that is banned by `docs/constitution.md:130`. | Millisecond race needing two separately-completed authorization flows for one new account; the index holds, no duplicate row, no data loss, retry succeeds. The fix changes shared persistence behaviour and there is no review pass left after this one. |
| **F3** | Soft-deleted account re-login semantics — restore, block, or new account — plus the `docs/implementation-plan.md` entry recording the answer. | Product decision, still open. Unreachable today: nothing calls `User.MarkDeleted()`. |
| **F4** | Split `CompleteGitHubLoginHandlerFailureTests.cs` (147 lines) and `OAuthStateProtectorTamperTests.cs` (124) against the ~100-line rule. | Pure refactor. |
| **F5** | `UserRepository.IgnoreQueryFilters()` is executed by no test; pinning it needs an EF InMemory package, which the testing convention puts out of scope. | House rule, not a shortfall — check by hand during the smoke test. |
| **F6** | The live round trip against real GitHub — see *What is NOT verified* above. | Needs a human at a consent screen. |

## Pipeline artifacts

- [`00-acceptance.md`](.process/github-oauth/00-acceptance.md) — proxied scope, 10 product decisions
- [`01-plan.md`](.process/github-oauth/01-plan.md) — 24 decisions, state design, callback sequence
- [`02-implementation.md`](.process/github-oauth/02-implementation.md) — implementation + rework round 1
- [`03-review.md`](.process/github-oauth/03-review.md) · [`03-review-r2.md`](.process/github-oauth/03-review-r2.md) — **APPROVED**
- [`04-metrics.md`](.process/github-oauth/04-metrics.md) — run log
- [`05-coderabbit-comments.md`](.process/github-oauth/05-coderabbit-comments.md) · [`05-coderabbit-comments-r2.md`](.process/github-oauth/05-coderabbit-comments-r2.md) — CodeRabbit inline comments, rounds 1 and 2
- [`06-coderabbit-triage.md`](.process/github-oauth/06-coderabbit-triage.md) · [`06-coderabbit-triage-r2.md`](.process/github-oauth/06-coderabbit-triage-r2.md) — accept/reject verdicts with reasons
- [`07-coderabbit-rework.md`](.process/github-oauth/07-coderabbit-rework.md) — what was changed in each round
- [`08-coderabbit-verify.md`](.process/github-oauth/08-coderabbit-verify.md) — independent verification of round 1, including the mutation probes

🤖 Generated with [Claude Code](https://claude.com/claude-code)
