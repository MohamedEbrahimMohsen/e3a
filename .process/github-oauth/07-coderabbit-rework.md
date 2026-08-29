# Stage 4 rework — CodeRabbit round 1, PR #5 (`github-oauth`)

All three IMPLEMENT items from `06-coderabbit-triage.md` are done. Every REJECT stayed rejected;
no code was touched outside these three items. No Azure resource, no `az` command, no migration,
no new NuGet package, no new error code, no resx churn.

## 1 → what changed → where

| # | Triage item | What I changed | File:line |
|---|---|---|---|
| 1 | Bind OAuth `state` to the initiating browser | `Create()` now returns `OAuthState(Value, Nonce)`; `Validate(state, nonce)` rejects a missing or mismatched nonce with `FixedTimeEquals` before any expiry/signature decision, so the failure stays `OAuthStateStatus.Invalid` | `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:19-46`, `api/E3A.Application/Authentication/Shared/IOAuthStateProtector.cs:5-6`, `api/E3A.Application/Authentication/Shared/OAuthState.cs:3` |
| 1 | …nonce surfaced to the Api layer | `GetGitHubLoginUrlQuery` now returns `GitHubLoginUrlResult(RedirectUrl, StateNonce)` | `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandler.cs:8-16`, `.../Shared/GitHubLoginUrlResult.cs:3`, `.../GetGitHubLoginUrl/GetGitHubLoginUrlQuery.cs:6` |
| 1 | …cookie issued on login | `Response.Cookies.Append(StateCookieName, result.StateNonce, …)` with `HttpOnly`, `Secure`, `SameSite=Lax`, `IsEssential`, `Path=/api/auth`, `MaxAge = StateExpirationMinutes` | `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:17-27`, `api/E3A.Api/Controllers/Authentication/OAuthStateCookieOptionsGenerator.cs:5-19` |
| 1 | …cookie read, consumed and cleared on callback | cookie read, then `Response.Cookies.Delete(...)` **unconditionally before** `mediator.Send`, then passed as `CompleteGitHubLoginCommand(code, state, nonce)` | `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:29-40`, `.../CompleteGitHubLogin/CompleteGitHubLoginCommand.cs:6` |
| 1 | …handler compares it | `oAuthStateProtector.Validate(request.State, request.Nonce)` — still above the code exchange, still `#error=AUTHENTICATION_STATE_INVALID` | `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs:21` |
| 1 | Cookie name in Options | `StateCookieName` with a class default, mirroring `StateNonceSize` (§8.1) | `api/E3A.Application/Options/GitHubAuthenticationOptions.cs:20` |
| 1 | Docs sync (blocking) | `architecture.md:28` no longer says the state is stateless; it now describes the nonce cookie and keeps "never a cookie" for the **token**. `implementation-plan.md:63` given the same correction | `docs/architecture.md:28`, `docs/implementation-plan.md:63` |
| 2 | `Is…ExistsAsync` + suffix loop (§8.3) | `IsUserNameExistsAsync(normalizedUserName, ct)` on the repository interface, implemented with **`IgnoreQueryFilters()`** so it sees the soft-deleted row that still holds `UserNameIndex` | `api/E3A.Domain/Identity/IUserRepository.cs:7`, `api/E3A.Infrastructure/Identity/UserRepository.cs:10-14` |
| 2 | Resolver mirroring `EngineerSlugResolver` | `UserNameResolver.ResolveUniqueAsync` — login if free, else `generator.Generate(prefix:, size:)` loop (§8.2, never `Random`) | `api/E3A.Application/Authentication/Shared/UserNameResolver.cs:9-25` |
| 2 | Suffix size in Options | `UserNameSuffixSize` with a class default, mirroring `EngineersOptions.SlugSuffixSize` | `api/E3A.Application/Options/GitHubAuthenticationOptions.cs:21` |
| 2 | Entity takes the resolved name | `CreateFromGitHub(gitHubId, gitHubLogin, userName, displayName, avatarUrl)`; `GitHubLogin` keeps the raw login, `UserName`/`NormalizedUserName` take the resolved one | `api/E3A.Domain/Identity/User.cs:41-55` |
| 2 | Wired into the callback | resolver called only on the create branch, before `AddAsync`; still exactly one `SaveChangesAsync`, success path only, no `try`/`catch` | `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs:53-58` |
| 3 | Escape `\|\|` in the two broken table rows | pipes escaped on lines 129 and 344 only; both rows now have the same delimiter count as their neighbours (5 and 4) | `.process/github-oauth/01-plan.md:129,344` |

## Files created

| Path | Lines | Purpose |
|---|---|---|
| `api/E3A.Application/Authentication/Shared/OAuthState.cs` | 3 | `sealed record OAuthState(string Value, string Nonce)` — the protector must hand the nonce back so the Api layer can cookie it |
| `api/E3A.Application/Authentication/Shared/GitHubLoginUrlResult.cs` | 3 | login-query result carrying the redirect URL **and** the nonce |
| `api/E3A.Application/Authentication/Shared/UserNameResolver.cs` | 26 | §8.3 exists-check + suffix loop for the local `UserName` |
| `api/E3A.Api/Controllers/Authentication/OAuthStateCookieOptionsGenerator.cs` | 20 | one definition of the nonce cookie's attributes, shared by append and delete |
| `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorNonceTests.cs` | 66 | missing / mismatched / matching nonce, plus one-browser-state-in-another-browser |
| `api/E3A.Tests/Authentication/Shared/UserNameResolverTests.cs` | 63 | free login, suffixed candidate, retry, normalized lookup |

## Files modified

| Path | Change |
|---|---|
| `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs` | `Create` returns `OAuthState`; `Validate` takes and fixed-time-compares the nonce |
| `api/E3A.Application/Authentication/Shared/IOAuthStateProtector.cs` | both signatures |
| `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQuery.cs` / `…QueryHandler.cs` | return `GitHubLoginUrlResult` |
| `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginCommand.cs` | third member `string? Nonce` |
| `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs` | `IGenerator` injected; nonce passed to `Validate`; resolver call on the create branch |
| `api/E3A.Application/Options/GitHubAuthenticationOptions.cs` | `StateCookieName`, `UserNameSuffixSize` |
| `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs` | `IOptions<GitHubAuthenticationOptions>` injected; cookie append on login, read + unconditional delete on callback |
| `api/E3A.Domain/Identity/IUserRepository.cs` | `IsUserNameExistsAsync` |
| `api/E3A.Infrastructure/Identity/UserRepository.cs` | implementation with `IgnoreQueryFilters()` |
| `api/E3A.Domain/Identity/User.cs` | `CreateFromGitHub` takes `userName` |
| `api/E3A.Tests/…/OAuthStateProtectorTests.cs` | call sites moved to `state.Value` / `Validate(value, nonce)`; replay test updated, not deleted (see below) |
| `api/E3A.Tests/…/OAuthStateProtectorTamperTests.cs` | call sites only — every tamper case now also supplies the browser nonce |
| `api/E3A.Tests/…/GetGitHubLoginUrlQueryHandlerTests.cs` | `Create()` stub returns `OAuthState`; new test that the nonce is surfaced and **not** leaked into the redirect URL |
| `api/E3A.Tests/…/CompleteGitHubLoginHandlerTests.cs` | `IGenerator` substitute; new suffixed-`UserName` creation test |
| `api/E3A.Tests/…/CompleteGitHubLoginHandlerFailureTests.cs` | `IGenerator` substitute; new `Handle_ShouldRedirectWithStateInvalid_WhenTheBrowserNonceIsMissing` asserting the `#error=` redirect **and** `DidNotReceive().ExchangeCodeForAccessTokenAsync` |
| `api/E3A.Tests/…/CompleteGitHubLoginHandlerReturningUserTests.cs` | call sites + `IGenerator` substitute |
| `api/E3A.Tests/Identity/Shared/UserFactory.cs` | optional `userName` parameter, defaults to the login |
| `api/E3A.Tests/Identity/UserTests.cs` | the login→`UserName` test became a resolved-name test |
| `docs/architecture.md`, `docs/implementation-plan.md` | stateless-state wording corrected |
| `.process/github-oauth/01-plan.md` | two pipes escaped, nothing else |
| `postman/e3a.postman_collection.json` | `description` added to GitHub Login and GitHub Callback recording the nonce cookie and its failure mode |

## The replay test — updated, not deleted, and its old meaning was wrong

`OAuthStateProtectorTests.Validate_ShouldReturnValid_WhenTheSameStateIsValidatedTwice` is kept with its
name (the triage explicitly said keep it, against RC13's last sentence), but **its meaning has changed
and the old meaning was wrong**. It used to say: this state is reusable, by anyone, and that was
argued to be harmless. It now says: the protector is deliberately stateless, so the *same browser*
presenting its *own* nonce twice still validates. Single-use is no longer the protector's job — it is
the controller's, which deletes the cookie on every callback before the command is sent. The property
the old test was implicitly claiming (any browser may replay) is now explicitly falsified by
`OAuthStateProtectorNonceTests.Validate_ShouldReturnInvalid_WhenTheStateIsPresentedByAnotherBrowser`.

Plan decision 7's "replay is inert in practice: it is worthless without a matching unused GitHub
`code`" was factually wrong — supplying the unused code is the attack. `01-plan.md` is not edited for
this (round 2's append-only ruling, upheld by the triage's RC1 rejection); the correction is recorded
here and belongs in the PR body.

## Deviations

| Plan / triage said | Reality | What I did |
|---|---|---|
| "The existence check must use `IgnoreQueryFilters()` … make sure a test pins it" | `api/E3A.Tests/E3A.Tests.csproj` references only `E3A.Application` and `E3A.Domain`. Pinning `IgnoreQueryFilters()` means constructing `AppDbContext` (which needs `IMediator` + three options) against a real EF provider — that is a new `E3A.Infrastructure` project reference **and** a new `Microsoft.EntityFrameworkCore.InMemory` entry in `api/Directory.Packages.props`, i.e. a NuGet restore of a package the repo has never used. `conventions/dotnet-testing.md` §5 also puts repositories and EF configuration explicitly out of scope, and the triage's own "Tests required" list for item 2 does not include a repository test. | **Not pinned by an executing test.** The call is implemented (`UserRepository.cs:13`) and carries the one-line invariant comment explaining why the filter is bypassed. The nearest coverage is `UserNameResolverTests.ResolveUniqueAsync_ShouldSuffixTheLogin_WhenTheUserNameIsHeldByAnotherRow`, which pins the resolver's reaction to a `true` answer but not the query that produces it. This is the one requirement I did not fully meet — flagging it rather than adding a package. |
| Triage: `GetGitHubLoginUrlQueryHandler` "surfaces the nonce alongside the redirect URL" (shape unspecified) | `AuthenticationRedirectResult(string RedirectUrl)` is shared with the callback, which has no nonce | Added `GitHubLoginUrlResult(RedirectUrl, StateNonce)` rather than making `StateNonce` a nullable member of the shared record — avoids a `!` at the only call site and keeps both members non-nullable. `AuthenticationRedirectResult` is now the callback's alone. One extra 3-line file. |
| Triage: cookie work in the controller | `Response.Cookies.Delete(key, options)` copies the passed `CookieOptions`, so a `MaxAge` left on it would emit `Max-Age` **and** an epoch `Expires`; browsers prefer `Max-Age`, which would silently fail to delete the cookie | `OAuthStateCookieOptionsGenerator.Generate(TimeSpan? maxAge = null)` — the append call passes the expiry, the delete call passes nothing. Same attributes both ways, which is what the browser requires for a delete to match. |
| Triage: `UserNameResolver` should re-normalize a too-long prefix like `EngineerSlugResolver` does | The engineer slug is derived from a free-text display name and can sit at `SlugMaxLength`; a GitHub login is capped at 39 characters by GitHub and the Identity `UserName` column is the default 256 | Left the truncation out. Adding it would be dead code plus a comment explaining dead code. If a login ever exceeded `GitHubLoginMaxLength` (100) the insert would fail on `GitHubLogin` first, independently of this path. |
| Convention: no test file over ~100 lines | `CompleteGitHubLoginHandlerFailureTests.cs` was already 133 lines before this rework; the triage put the nonce-missing case in that file | Added it there (now 147). Splitting the file is a refactor outside the numbered findings, which rework scope forbids. Flagged rather than done. |
| Triage: "Postman needs no change" | True for URL, method, auth mode and query params — none changed | Left the requests structurally identical but added a `description` to each of the two: the callback now *silently* depends on a cookie the login sets, and a collection that does not say so sends the next person into `#error=AUTHENTICATION_STATE_INVALID` with no explanation. Two added lines, no shape change. Say the word and I will drop them. |

Not done, and correctly so: RC1, RC3, RC5, RC6, RC8, RC11 stay rejected — no `try`/`catch` was added to
any handler, no closed audit artifact was rewritten, `UpdateGitHubProfile` still updates exactly the two
fields acceptance decision 5 names. D1 (what a returning soft-deleted account *means*) is untouched and
still the dev's call; today the flow mints a second row for that identity, which is triage option (c).

## Build & test

Run in the worktree `.../scratchpad/wt-oauth`, verbatim outcomes:

```
dotnet build api/E3A.slnx --no-incremental
Build succeeded.
    9 Warning(s)
    0 Error(s)
```

All 9 warnings are the pre-existing `core-libraries` ones (`Core.Validation` ×2, `Core.OTP` ×2,
`Core.Notifications` ×5). No `E3A.*` warning — `TreatWarningsAsErrors` would have failed the build.
Baseline matched exactly before I started.

```
dotnet test api/E3A.slnx
Passed!  - Failed:     0, Passed:   433, Skipped:     0, Total:   433, Duration: 1 s - E3A.Tests.dll (net10.0)
```

420 → **433**, +13: 6 in `OAuthStateProtectorNonceTests` (3 of them `[Theory]` rows), 4 in
`UserNameResolverTests`, 1 in `GetGitHubLoginUrlQueryHandlerTests`, 1 in
`CompleteGitHubLoginHandlerTests`, 1 in `CompleteGitHubLoginHandlerFailureTests`. Nothing deleted.

## Notes for review — residual risk

1. **The `IgnoreQueryFilters()` line is unexecuted by any test.** See Deviations. It is the load-bearing
   half of item 2 and it is the thing I would check by hand first.
2. **`Secure = true` is unconditional.** Correct for the deployed app and for the configured
   `https://localhost:62935` callback, but a developer serving the API over plain HTTP will find the
   cookie silently dropped and every callback failing with `#error=AUTHENTICATION_STATE_INVALID`.
   I did not make it environment-dependent — a config switch that can turn `Secure` off is exactly the
   switch that gets left on in production.
3. **`Path = "/api/auth"`** is a private const in `OAuthStateCookieOptionsGenerator` that must stay equal
   to `[Route("api/auth")]`. It carries the only comment I added in the Api layer. It is not an Option
   because it is a route invariant, not a tunable cap — but that is a judgement call.
4. **`SameSite=Lax` is required, not preference.** `Strict` would not send the cookie on GitHub's
   top-level cross-site GET and would break every login; `None` would restore the hole. Anyone
   "hardening" this to `Strict` breaks the flow.
5. **`UserName` and `GitHubLogin` may now legitimately differ.** That retires plan decision 14's
   "`GitHubLogin` therefore stays equal to `UserName`" rationale, which the triage already flagged as
   making RC11 stronger next slice. Publish attribution
   (`ProcessPublishJobHandler.cs:67`, `RegenerateMarketplaceHandler.cs:62`) reads `UserName`, so a
   collided user is attributed under their suffixed name — correct, but worth a conscious nod.
6. **Nonce comparison is `FixedTimeEquals` on UTF-8 bytes of unequal-length strings**, which returns
   false rather than throwing. That is intended, and it is why the length of the attacker's guess leaks
   nothing useful.
7. The nonce cookie is never logged: the callback reads it into a local and it is not part of any
   result. `System.Net.Http.HttpClient` logging is already floored at Warning for the token, unaffected.

---

## Round 2

Stage 4 rework, round 2 (final CodeRabbit cycle), PR #5. Scope was the three IMPLEMENT items in
`06-coderabbit-triage-r2.md`; RC2-3, RC2-4 and RC2-5 stayed rejected and nothing else was touched.

### Finding -> what changed -> where

| # | Finding | What I changed | Where |
|---|---|---|---|
| 1 | **RC2-2** - the callback deleted the fixed-name nonce cookie unconditionally *before* validation, so one parameterless top-level GET to `/api/auth/github/callback` killed a victim's in-flight login | `AuthenticationRedirectResult` gained `bool StateNonceConsumed`; the handler sets it `false` on the two branches that compare nothing and `true` from the `Expired` guard onward; the controller moved the delete to after `mediator.Send` behind `if (result.StateNonceConsumed)` | `api/E3A.Application/Authentication/Shared/AuthenticationRedirectResult.cs:3` - `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs:18,25,30,37,44,49,70,73` - `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:35-40` |
| 2 | **RC2-2** (tests) - the trap: the code-missing guard returns before `Validate` runs, so it must not report "consumed" | New test file with the three cases the triage named, plus a success-path assertion in the existing handler tests | `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerCookieTests.cs:36,47,57` - `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs:76-83` |
| 3 | **RC2-6** - `RedirectUrl.Should().NotContain("state-nonce")` is vacuously true against the substitute and **false** against the real protector, which puts the nonce verbatim into the state | Deleted that one line; kept `result.StateNonce.Should().Be("state-nonce")` and the test name | `api/E3A.Tests/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs:43` |
| 4 | **RC2-1** - `pr-body.md` is the live PR description and still advertised the login-CSRF hole as open, called the state "stateless", reported 420 tests, and stopped its artifact list at `04-metrics.md` | Replaced the "Known residual risk" section with what actually shipped (and who found it); rewrote the `state` paragraph as signed/expiring/browser-bound; 420 -> 437; added the Stage 4 artifacts for both rounds; folded in the RC2-4 `MaxAge` correction with the exact SDK; added the F1-F6 follow-up table | `.process/github-oauth/pr-body.md:9,28,38-64,66-76` |

### Files created

| Path | Lines | Purpose |
|---|---|---|
| `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerCookieTests.cs` | 59 | Pins `StateNonceConsumed` on the three branches that decide whether the cookie is cleared |

New file rather than additions to `CompleteGitHubLoginHandlerFailureTests.cs` (147 lines), so F4 does not
get worse.

### Files modified

| Path | Change |
|---|---|
| `api/E3A.Application/Authentication/Shared/AuthenticationRedirectResult.cs` | `+ bool StateNonceConsumed` on the record |
| `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs` | `Failure(string errorCode, bool stateNonceConsumed)`; six call sites pass it by **named argument** so the branch reads without a comment; success path passes `StateNonceConsumed: true` |
| `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs` | Delete moved below the `Send` and gated on `result.StateNonceConsumed`; same `OAuthStateCookieOptionsGenerator.Generate()` options, so `Path` still matches |
| `api/E3A.Tests/.../CompleteGitHubLoginHandlerTests.cs` | `+ Handle_ShouldConsumeTheStateCookie_WhenLoginSucceeds` (file now 98 lines, still under ~100) |
| `api/E3A.Tests/.../GetGitHubLoginUrlQueryHandlerTests.cs` | Deleted the false `NotContain` assertion |
| `.process/github-oauth/pr-body.md` | Live PR description corrected - see row 4 above |
| `postman/e3a.postman_collection.json` | GitHub Callback description said "The cookie is cleared on every callback", which this change makes false; corrected to compare-then-clear. URL, method, query params and auth mode unchanged |

### Why match-then-delete costs no security

Round 1 chose unconditional deletion on the reasoning that a surviving cookie is a replayable cookie.
That does not hold: under match-then-delete the cookie survives **only** when the presented nonce failed
to match segment 0 of the state - i.e. only on callbacks that consumed nothing. Every callback whose
nonce did match still deletes, including `Expired`, a failed exchange, a failed profile fetch and
success. Single-use-per-browser is preserved exactly; only the denial-of-login is removed.

The delete now happens on exactly the paths where `OAuthStateProtector.Validate` compared the nonce and
found it equal. The code-missing guard returns before `Validate` runs and reports `false` - that is the
trap the triage named, and the first test below is the one that pins it.

### Verification

Both halves of the branch were mutation-probed, not just asserted:

```
# mutate the code-missing guard to stateNonceConsumed: true  (re-opens the hole)
dotnet test api/E3A.slnx --filter "FullyQualifiedName~CompleteGitHubLoginHandlerCookieTests"
Failed!  - Failed: 1, Passed: 2 - Handle_ShouldNotConsumeTheStateCookie_WhenCodeIsAbsent

# mutate the exchange-failure branch to stateNonceConsumed: false  ("always false" implementation)
dotnet test api/E3A.slnx --filter "FullyQualifiedName~CompleteGitHubLoginHandlerCookieTests"
Failed!  - Failed: 1, Passed: 2 - Handle_ShouldConsumeTheStateCookie_WhenTheStateMatchesButTheExchangeFails
```

Both mutations were reverted before the final run. Without the second test an always-`false`
implementation would pass and single-use-per-browser would be silently lost.

`Handle_ShouldNotConsumeTheStateCookie_WhenCodeIsAbsent` also asserts
`_oAuthStateProtector.DidNotReceive().Validate(...)`, so it fails if someone later reorders the guards to
make the flag look right - the guard order is load-bearing for the `#error=` precedence contract and for
`CompleteGitHubLoginHandlerFailureTests.cs:38-44`.

### Build & test

```
dotnet build api/E3A.slnx --no-incremental
Build succeeded.
    9 Warning(s)
    0 Error(s)
```

All 9 warnings are pre-existing and in `api/core-libraries` (`Core.Validation` CS8602 x2, `Core.OTP`
CS8618 x2, `Core.Notifications` CS8618 x5). No `E3A.*` warning; `TreatWarningsAsErrors` unaffected.

```
dotnet test api/E3A.slnx
Passed!  - Failed: 0, Passed: 437, Skipped: 0, Total: 437 - E3A.Tests.dll (net10.0)
```

**433 -> 437.** Plus four (three in the new cookie file, one success-path case); RC2-6 removed an
assertion, not a test.

### Deviations

| Plan said | Reality | What I did |
|---|---|---|
| Triage: "**Postman needs no change** - three auth requests, unchanged URLs, methods, query params and auth modes. Nothing added, removed or stale." | True for the request *shape*, but the GitHub Callback request's `description` asserted "The cookie is cleared on every callback" - a statement this change makes false. | Corrected that one sentence. No request added, removed, or re-shaped. Flagging it because it is one file outside the triage's stated touch list, and because the Postman-sync rule covers contract text, not just URLs. |

Nothing else deviated: no Azure resource, no `az`, no new package, no migration, no new options key, no
`/docs` edit, no handler `try`/`catch`, no closed `.process/` artifact rewritten.

### Notes for review

1. **The `/docs` divergence closed itself.** `docs/architecture.md:28` ("rejects the state unless the
   cookie matches, then clears it") and `docs/implementation-plan.md:63` ("compares and clears") already
   described compare-then-clear. The code now matches both. Confirmed by reading them; neither was
   edited.
2. **`Failure(errorCode, stateNonceConsumed: ...)` uses named arguments deliberately.** A bare positional
   `true`/`false` at six call sites would have needed a comment to be readable, and comments are banned.
   If a reviewer prefers two named private methods over a bool parameter, that is a style call, not a
   correctness one.
3. **The controller is still untested by house rule** (`conventions/dotnet-testing.md`, "Explicitly out
   of scope: controllers"). So the `if (result.StateNonceConsumed)` line itself has no test - only the
   flag feeding it does. That is the convention working as designed, but it is the one line where an
   inverted condition would not go red. It is three lines, directly above the `Redirect`, and worth a
   human's eye.
4. **`Expired` consumes.** `OAuthStateProtector.Validate` compares the nonce at `:43`, before the expiry
   check at `:60`, so an `Expired` result means the nonce *did* match and the cookie is correctly
   cleared. If that ordering is ever changed, this flag changes meaning with it.
5. **What RC2-2 does not fix, and I am not claiming it does.** An attacker who already holds the
   victim's in-flight `state` value can still land a matching callback and clear the cookie - the same
   precondition as F1, and the same reason F1 is the follow-up worth scheduling first. What is closed is
   the version that needed **no** precondition at all.
6. **`pr-body.md` was the only `.process/` file edited**, per the triage's ruling that it is the live PR
   description rather than a closed artifact. Round 1's section above this line is untouched.
