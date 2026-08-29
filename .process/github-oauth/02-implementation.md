# Implementation — GitHub OAuth Login for Creators

## Files created

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Application/Options/GitHubAuthenticationOptions.cs` | 22 | Binds the existing `GitHubAuthentication` section; holds every cap and tunable (skill §8.1 — no entity constants) |
| `api/E3A.Application/Authentication/Shared/OAuthStateStatus.cs` | 3 | `Valid` / `Invalid` / `Expired` |
| `api/E3A.Application/Authentication/Shared/IOAuthStateProtector.cs` | 7 | `Create()` / `Validate(string?)` |
| `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs` | 69 | Stateless HMAC-signed expiring `state`; signature verified before expiry, `CryptographicOperations.FixedTimeEquals` |
| `api/E3A.Application/Authentication/Shared/GitHubProfile.cs` | 3 | Application-facing shape of GitHub's `/user` payload |
| `api/E3A.Application/Authentication/Shared/IGitHubOAuthClient.cs` | 7 | Outbound contract; returns null, never throws |
| `api/E3A.Application/Authentication/Shared/GitHubAuthorizationUrlGenerator.cs` | 20 | Builds the authorize URL from configuration only |
| `api/E3A.Application/Authentication/Shared/AuthenticationRedirectUrlGenerator.cs` | 17 | `#token=` / `#error=` fragment construction |
| `api/E3A.Application/Authentication/Shared/UserClaimsGenerator.cs` | 23 | The four claims, typed from `CurrentUserService.Constants`; no role claim |
| `api/E3A.Application/Authentication/Shared/AuthenticationRedirectResult.cs` | 3 | Shared redirect result |
| `api/E3A.Application/Authentication/Shared/CurrentUserResult.cs` | 3 | `/api/auth/me` result |
| `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQuery.cs` | 6 | — |
| `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandler.cs` | 17 | Non-async; creates state, builds URL |
| `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginCommand.cs` | 6 | `(string? Code, string? State)` |
| `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs` | 75 | The ten-step callback sequence; six failure branches, all 302; one `SaveChangesAsync` |
| `api/E3A.Application/Authentication/GetCurrentUser/GetCurrentUserQuery.cs` | 6 | — |
| `api/E3A.Application/Authentication/GetCurrentUser/GetCurrentUserQueryHandler.cs` | 30 | Guard-first unauthorized, then not-found |
| `api/E3A.Infrastructure/Authentication/GitHubOAuthClient.cs` | 99 | Typed `HttpClient`; the only `try`/`catch` in the slice |
| `api/E3A.Infrastructure/Authentication/GitHubAccessTokenPayload.cs` | 5 | `access_token` + `error` |
| `api/E3A.Infrastructure/Authentication/GitHubProfilePayload.cs` | 5 | `id` / `login` / `name` / `avatar_url` |
| `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs` | 37 | Three thin actions on `api/auth` |
| `api/E3A.Infrastructure/Data/Migrations/20260829112516_oauth004.cs` | 71 | Generated — 4 columns + 1 filtered unique index |
| `api/E3A.Infrastructure/Data/Migrations/20260829112516_oauth004.Designer.cs` | — | Generated |

Test files (per `conventions/dotnet-testing.md`; exact class/method names from §Test plan):

| Path | Lines | Tests |
|------|-------|-------|
| `api/E3A.Tests/Identity/Shared/UserFactory.cs` | 17 | support |
| `api/E3A.Tests/Authentication/Shared/GitHubAuthenticationOptionsFactory.cs` | 37 | support |
| `api/E3A.Tests/Authentication/Shared/GitHubProfileFactory.cs` | 12 | support |
| `api/E3A.Tests/Identity/UserTests.cs` | 95 | 1–8 |
| `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTests.cs` | 88 | 9–14 |
| `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs` | 111 | 15–22 |
| `api/E3A.Tests/Authentication/Shared/GitHubAuthorizationUrlGeneratorTests.cs` | 46 | 23–25 |
| `api/E3A.Tests/Authentication/Shared/AuthenticationRedirectUrlGeneratorTests.cs` | 47 | 26–29 |
| `api/E3A.Tests/Authentication/Shared/UserClaimsGeneratorTests.cs` | 60 | 30–34 |
| `api/E3A.Tests/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs` | 47 | 35–37 |
| `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs` | 73 | 38–41 |
| `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerReturningUserTests.cs` | 72 | 42–45 |
| `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | 134 | 46–54 |
| `api/E3A.Tests/Authentication/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs` | 68 | 55–57 |

All 57 planned test methods exist with the planned names. Theories expand to 65 executed cases.

## Files modified

| Path | Change |
|------|--------|
| `api/E3A.Domain/Identity/User.cs` | Added `GitHubId`/`GitHubLogin`/`DisplayName`/`AvatarUrl` (private setters), `CreateFromGitHub`, `UpdateGitHubProfile`. `User()`, `User(Guid)`, `Create`, `MarkDeleted` untouched. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Added the `// Authentication` group with the six constants. |
| `api/E3A.Application/DependencyInjection.cs` | `Configure<GitHubAuthenticationOptions>` + `AddScoped<IOAuthStateProtector, OAuthStateProtector>`. |
| `api/E3A.Infrastructure/DependencyInjection.cs` | `AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>` with timeout, `Accept`, `User-Agent`. |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | 4th primary-constructor parameter; `ConfigureUsers` called first; column widths from options; filtered unique index. |
| `api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` | Regenerated — +19 lines, all four properties and the one index, nothing else. |
| `api/E3A.Api/Resources/Messages.en.resx` | Six new keys. |
| `api/E3A.Api/Resources/Messages.ar.resx` | Same six keys, Arabic, no tashkeel. |
| `api/E3A.Api/appsettings.json` (git-ignored, on disk) | Added the seven optional keys **before** generating the migration, so the widths bound to 100/200/500 rather than 0. |
| `postman/e3a.postman_collection.json` | New `Authentication` folder as the first `item`, three requests. |
| `docs/architecture.md` | One new `## Principles` bullet — the fragment handoff + stateless `state`. |
| `docs/implementation-plan.md` | Replaced the `Auth:` clause of `## API surface (/api/*)`. |

`Program.cs` unchanged, as the plan requires. No new NuGet package, no `Directory.Packages.props` change, no `FrameworkReference` needed (`QueryHelpers` reached Application transitively through `Core.Validation`'s `FrameworkReference`).

## Deviations

| Plan said | Reality | What I did |
|---|---|---|
| `GitHubOAuthClient` logs via `logger.LogWarning(...)` in four places, class declared `sealed class` | `TreatWarningsAsErrors` + `AnalysisLevel=latest-recommended` turns **CA1848** ("use LoggerMessage delegates") into four build errors. There is no other `ILogger` usage anywhere in the E3A projects, so there was no in-repo precedent to mirror. | Declared the class `sealed partial` and added two source-generated `[LoggerMessage]` static partial methods — `LogUnsuccessfulResponse(logger, requestPath, statusCode)` and `LogFaultedRequest(logger, exception, requestPath)`. The three catch clauses stay separate exactly as planned; they share one message. Same information logged (status code, path with query stripped), file still 99 lines. |
| Shared helper does `using var request` **inside** `SendAsync` | You cannot apply a `using` declaration to a method parameter in C#; only the block form works, which nests the whole method body one level deeper. | The `using var request` lives at both call sites instead. The request (and its `FormUrlEncodedContent`) is still deterministically disposed, and now also if `SendAsync` faults before entering its `try`. |
| Skill §7.1/§7.3: `[Authorize(Policy = DefaultCodes.X)]` on every action | No `DefaultCodes` class exists in this repo; `EngineersController` and `PublishController` both use a bare class-level `[Authorize]`. Plan decision 21 says to mirror them and create no `DefaultCodes`. | Followed the plan and the repo: class-level `[Authorize]`, `[AllowAnonymous]` on the two redirect actions. Flagging it because the reviewer walks §8/§7 entry by entry and this looks like a miss until you check that the constant class does not exist. |
| Skill/testing convention: no file over ~100 lines | `OAuthStateProtectorTamperTests.cs` is 111 lines (8 planned methods) and `CompleteGitHubLoginHandlerFailureTests.cs` is 134 lines (9 planned methods). | Left as-is. Splitting them would change the class names the plan fixes verbatim and break the "same class names" contract. Production files are all ≤ 99 lines. |

No other deviation. Nothing in the plan turned out to be impossible; no method it assumed was missing.

**No Azure resource was created and no `az` command was run.** The slice needs none.

## Build & test

```
dotnet build api/E3A.slnx --no-incremental
  Build succeeded.
      9 Warning(s)
      0 Error(s)
```

All 9 warnings are in `api/core-libraries` (`Core.Validation` ×2 CS8602, `Core.OTP` ×2 CS8618, `Core.Notifications` ×5 CS8618) — identical to the stated baseline. Zero warnings in any `E3A.*` project.

```
dotnet test api/E3A.slnx
  Passed!  - Failed: 0, Passed: 419, Skipped: 0, Total: 419, Duration: 1 s
```

354 baseline + 65 new = 419. Exact match, no pre-existing test disturbed.

```
dotnet ef migrations add oauth004 --project api/E3A.Infrastructure --startup-project api/E3A.Api
  Done.
```

`20260829112516_oauth004.cs` contains exactly: `AvatarUrl nvarchar(500) NULL`, `DisplayName nvarchar(200) NULL`, `GitHubId bigint NULL`, `GitHubLogin nvarchar(100) NULL`, and
`IX_AspNetUsers_GitHubId` UNIQUE with `filter: "[GitHubId] IS NOT NULL AND [IsDeleted] = 0"`. Nothing else. The migration was **not** applied to a database.

### Out-of-band verification of the outbound calls (throwaway harness, not committed)

I ran the real `GitHubOAuthClient`, resolved through the real `AddInfrastructure` typed-client registration, against a local `HttpListener` standing in for github.com. Scratch project lives in the session scratchpad only; nothing was added to the repo.

Observed request to `AccessTokenUrl`:

```
POST /login/oauth/access_token
  Content-Type: application/x-www-form-urlencoded
  Accept: application/json
  User-Agent: e3a
  BODY: client_id=…&client_secret=…&code=probe-code&redirect_uri=https%3A%2F%2Flocalhost%3A62935%2Fapi%2Fauth%2Fgithub%2Fcallback
```

Observed request to `UserProfileUrl`:

```
GET /user
  Accept: application/vnd.github+json
  Authorization: Bearer gho_probe_token
  User-Agent: e3a
```

Failure paths, same harness: HTTP 500 → null; `{"error":"bad_verification_code"}` with HTTP 200 → null; non-JSON body → null; response slower than `HttpTimeoutSeconds` → null; listener down (transport failure) → null; **caller cancellation → `OperationCanceledException` rethrown**, not swallowed. No branch threw.

The warning log lines read `GitHub request to http://127.0.0.1:53998/login/oauth/access_token returned status 500.` — the `?tracking=1` query I deliberately put on the configured URL is absent, confirming `GetLeftPart(UriPartial.Path)`. Grepping the whole probe output for the secret value returned nothing.

**What this proves:** the request e3a emits is well-formed, carries the `User-Agent` GitHub requires and the two `Accept` headers, puts the secret in the body and never in a URL, and collapses every transport/protocol failure to `null`.
**What it does not prove:** that GitHub accepts it. The listener is a stub — it does not validate the client credentials, the registered callback URL, or the scope grant.

## The live round trip is UNVERIFIED

**The end-to-end flow against real GitHub has not been run and cannot be run here** — it needs a human at a GitHub consent screen. I am not describing this slice as working or verified end to end.

Verified: every callback branch through `Substitute.For<IGitHubOAuthClient>()`, the whole `state` contract (tamper, expiry, truncation, wrong key, replay), the claim set against the exact constants `CurrentUserService` reads, both URL builders as strings, and the outbound request shape above.

Unverified, and needing the dev to sign in once:
- that the GitHub App's registered callback matches `CallbackUrl` (`https://localhost:62935/api/auth/github/callback`) — dev answer 1 says it is registered, but nothing here can confirm it;
- that the `ClientId`/`ClientSecret` on disk are live and the App is installed;
- that the `read:user` grant returns a profile with a usable `name`/`avatar_url`;
- that the SPA at `http://localhost:5174/auth/callback` receives and reads the fragment (that is feature 4).

## Configuration to announce to the dev

Seven optional keys were added to `api/E3A.Api/appsettings.json` under the existing `GitHubAuthentication` section. Each has a class default, so a fresh clone or CI with none of them still binds correctly — but they must be added to Azure App Configuration if you ever want to tune them in production:

`Scope` = `read:user` · `UserAgent` = `e3a` · `HttpTimeoutSeconds` = `10` · `StateNonceSize` = `16` · `GitHubLoginMaxLength` = `100` · `DisplayNameMaxLength` = `200` · `AvatarUrlMaxLength` = `500`

The last three are schema widths. Changing them requires a migration, not just a config edit.

**Carried debt (not fixed here, from acceptance decision 3):** the client secret is live in a git-ignored file and still needs rotating before this repo goes public.

## Notes for review

**Risk areas the brief called out, and where to look:**

1. **`NormalizedUserName`.** `User.CreateFromGitHub` sets `UserName = gitHubLogin` and `NormalizedUserName = gitHubLogin.ToUpperInvariant()`. Without it the second just-in-time user would violate Identity's unique `UserNameIndex` on the NULL. Test 2 (`CreateFromGitHub_ShouldSetUserNameAndNormalizedUserNameFromLogin_WhenCalled`) locks it. `SecurityStamp` is set for the same class of reason.
2. **Signature before expiry.** `OAuthStateProtector.Validate` does length → parse → `FixedTimeEquals` → expiry, in that order. Test 19 pushes the expiry segment a year out without re-signing and asserts `Invalid` — not `Expired`, not `Valid`. Tests 15–22 cover missing/short/long segment counts, non-numeric expiry, tampered nonce, tampered signature, truncated signature and cross-key forgery as separately named tests.
3. **Open redirect.** The only two bound request values are `code` and `state`. `code` goes into a form body; `state` goes into `Validate`. Neither reaches a URL builder. Both generators take their base URL from `IOptions`. Tests 23, 29 and 54 assert the produced URLs start with the configured base and (54) contain no GitHub host.
4. **Secret exposure.** I read `CoreRequestLoggingMiddleware` (`api/core-libraries/Core.Logging/RequestLoggingMiddleware.cs`): it logs inbound `Method`, `Path`, `QueryString`, a fixed header set and the status code — no bodies, no response headers, and it never sees outbound `HttpClient` traffic. The secret only exists in an outbound POST body, so it is out of reach. The response `Location` header is not logged either, so the issued JWT does not land in the request log.

**Residual risks no test covers — please read these:**

- **Login-CSRF is not fully closed.** A stateless `state` is not bound to the browser that started the flow. Deferred deliberately (plan §Deferred); acceptance decision 2 fixed the mechanism. The fix is a `SameSite=Lax` nonce cookie set on `/login` and compared on callback.
- **`state` replay inside its 10-minute window is accepted**, and test 14 documents it as a decision rather than an oversight. It is inert without an unused GitHub `code`.
- **The inbound callback query string — containing the single-use `code` and the `state` — is written to the request log** by the vendored Core middleware. Known and accepted in the plan; the code is already consumed by the time the line is written. Changing it means changing a vendored library.
- **`Microsoft.Extensions.Http`'s own `LoggingScopeHttpMessageHandler` logs outbound request headers at `Trace` level.** At `Trace` that would include `Authorization: Bearer <GitHub access token>` on the profile call. It never logs bodies, so the client secret is unaffected. Current config (`CoreLogging:Console:MinimumLevel = Debug`, `Logging:LogLevel:Default = Information`) is above `Trace`, so nothing is emitted today — but turning logging down to `Trace`/`Verbose` in any environment would surface GitHub access tokens in logs. Not something I changed; worth knowing before anyone raises log verbosity.
- **The EF configuration and the migration are untested by construction** (`conventions/dotnet-testing.md` §5 puts EF configs and controllers out of scope, and `E3A.Tests` does not reference Infrastructure). I verified the migration by reading it and by diffing the snapshot — 19 added lines, all four properties and the one index, nothing else. It has not been applied to a database.
- **`GitHubOAuthClient` itself has no unit tests** for the same scoping reason. The out-of-band harness above is the only evidence about it, and it is not committed.

**Two things a reviewer might read as misses but which are deliberate:**

- **No validator files anywhere in this slice** (plan decision 22). The two queries have no input; the command's inputs are redirect branches, not validation failures. A FluentValidation rule would 422 through `ValidationBehaviour`, which is a dead end for a browser mid-redirect.
- **The callback handler branches on inputs in the handler**, which normally violates "never validate manually in a handler". Plan decision 10 makes this the one deliberate exception: these are redirect branches, and a `*CoreException` would become a JSON body via `CoreExceptionMiddleware`. There is still no `try`/`catch` in any handler — the only one in the slice is in the Infrastructure adapter (plan decision 19).

---

# Rework round 1

Addressing `03-review.md` (`CHANGES_REQUESTED`): blocking finding 1, plus the one approved non-blocking item.
Nothing else was touched. The three out-of-scope non-blocking items (missing `DidNotReceive().SaveChangesAsync`
on four callback tests, `GitHubOAuthClient`'s structural test exclusion, the reused-`GitHubLogin`
`UserNameIndex` collision) remain as recorded follow-ups.

| # | Finding | What I changed | Where |
|---|---------|----------------|-------|
| 1 | Blocking — no test distinguishes signature-before-expiry from expiry-before-signature | Added one test method, `Validate_ShouldReturnInvalid_WhenExpiryIsMovedIntoThePastWithoutResigning`: takes `_sut.Create()`, replaces segment 1 with `DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()`, leaves segment 2 (the signature) untouched, asserts `Be(OAuthStateStatus.Invalid)` **and** `NotBe(OAuthStateStatus.Expired)`. No production change — `OAuthStateProtector.Validate` was already correct. | `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs:77-87` |
| 2 | Non-blocking, approved — log-level floor for outbound HTTP | Added `"System.Net.Http.HttpClient": "Warning"` to the `Logging:LogLevel` block, with a WHY comment above it. | `api/E3A.Api/appsettings.Development.json:3-4` |

## Finding 1 — proof that the new test bites

The reviewer's diagnosis was exactly right, and I verified it by mutation rather than by argument.

**Step 1 — transposed production code.** I temporarily swapped the two blocks in
`api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs` so the expiry comparison ran before
`FixedTimeEquals` (i.e. old lines 55-58 moved above old lines 48-53), then ran `dotnet test api/E3A.slnx`:

```
  Failed E3A.Tests.Authentication.Shared.OAuthStateProtectorTamperTests.Validate_ShouldReturnInvalid_WhenExpiryIsMovedIntoThePastWithoutResigning [795 ms]
  Error Message:
   Expected result to be OAuthStateStatus.Invalid {value: 1}, but found OAuthStateStatus.Expired {value: 2}.
Failed!  - Failed:     1, Passed:   419, Skipped:     0, Total:   420
```

The new test failed with precisely the expiry-oracle symptom the review predicted — `Expired` where `Invalid`
was expected — and it was the **only** failure. `Failed: 1, Passed: 419` is independent confirmation of the
review's central claim: the pre-existing 419 tests are collectively blind to the check order, and the one
added test is the whole guard.

**Step 2 — restored production code.** Restored from a byte-exact pre-mutation copy; `md5sum` of the restored
file matches the backup (`1a11a5e1fcdcc225ace79ee2c325dfc4`), so `OAuthStateProtector.cs` is identical to what
the reviewer read. Re-ran build and tests: `0 Error(s)`, `Failed: 0, Passed: 420`.

Both outcomes were observed first-hand, not inferred.

## Sweep — do any other planned tests claim an ordering or invariant they do not constrain?

I re-read the plan's §Test plan Asserts column for every row that claims to *lock*, *prove*, *guard* or
otherwise constrain an ordering, and checked each claim against the shipped test body. **One weak row, no
second false claim.** Detail either way, as asked:

| Row | Claim | Verdict |
|---|---|---|
| 19 | "locks the signature-before-expiry order" | **Was false.** The claim now holds only because of the test added above; row 19's own test (future expiry) still does not lock the order and never could. |
| 8 | `UpdateGitHubProfile_ShouldNotChangeGitHubIdentity` "locks decision 14" | **Weak but not vacuous.** `UpdateGitHubProfile(string? displayName, string? avatarUrl)` never receives the identity and the setters are private, so no implementation of that signature *could* change `GitHubId`/`GitHubLogin` by accident. What the test genuinely catches is a future body that assigns identity fields from the profile fields it does have — e.g. `UserName = displayName`. Real, but a narrower guard than "locks decision 14" implies. |
| 48 | `Handle_ShouldNotCallGitHub_WhenStateIsInvalid` "proves the state is verified before any outbound call" | **Holds.** `DidNotReceive().ExchangeCodeForAccessTokenAsync(...)` under `Validate => Invalid` fails the moment the exchange moves above the state check. Genuine ordering lock. |
| 50 | "the profile is never fetched" | **Holds.** `DidNotReceive().GetProfileAsync(...)` with a null access token locks exchange-before-profile. |
| 56 | "the repository is never queried" | **Holds.** `DidNotReceive().GetByIdAsync(...)` with a null `UserId` locks guard-first ordering in `GetCurrentUserQueryHandler`. |
| 21 | truncated signature "proves `FixedTimeEquals` returns false on unequal lengths instead of throwing" | **Holds.** A throw fails the test rather than passing it. |
| 42, 44 | "locks decision 4" / "locks decision 14" | **Hold** — the reviewer traced both independently (compiled predicate evaluated against the stored user). |
| 34 | "locks decision 3" (no role claim) | **Holds.** |
| 11 | `Create_ShouldProduceDifferentStates_WhenCalledTwice` | The reviewer called this weak, and it is — but the **plan claims only** "the two states differ", which the test does assert. No false claim to correct; left alone (out of scope). |

## Finding 2 — the log-level floor

`api/E3A.Api/appsettings.Development.json` now reads:

```json
{
  "Logging": {
    // System.Net.Http.HttpClient is floored at Warning because LoggingScopeHttpMessageHandler writes outbound request headers at Trace, which would include the GitHub "Authorization: Bearer" access token.
    "LogLevel": { "Default": "Debug", "Microsoft.AspNetCore": "Information", "System.Net.Http.HttpClient": "Warning" }
  }
}
```

The WHY comment **was** possible: `Microsoft.Extensions.Configuration.Json` parses with comments allowed, and
there is in-repo precedent — `api/E3A.Api/appsettings.json:3` already carries a `//` line. So the reason is
recorded in the file, not only in this report.

Why this is worth a line rather than a shrug: **GitHub OAuth App access tokens do not auto-expire.** Unlike
the e3a JWT, a token that lands in a log file stays valid until it is explicitly revoked or the App's
authorisation is removed, so a single `Trace` debugging session leaves a durable credential behind. The token
is `read:user` scoped, which bounds the damage, but does not make it self-healing.

Why this file: it is **committed** (`git ls-files` confirms it; `appsettings.json` is git-ignored), so the
floor reaches every clone and CI run, and it survives someone setting `Default` to `Trace` while debugging
this very flow — the scenario the review called non-negligible. A category floor beats relying on the
`Default` level precisely because `Default` is what a debugging developer edits.

I did **not** add a redacting `AddHttpMessageHandler`; the review ruled against it and its reasoning is
correct — `LoggingScopeHttpMessageHandler` is outermost, so an added handler runs after the header is already
logged. `api/E3A.Infrastructure/DependencyInjection.cs` is untouched this round.

## Build & test

```
dotnet build api/E3A.slnx --no-incremental
  Build succeeded.
      9 Warning(s)
      0 Error(s)
```

All 9 warnings are the same `api/core-libraries` ones as the baseline (Core.Validation x2 CS8602, Core.OTP x2
CS8618, Core.Notifications x5 CS8618). Zero in any `E3A.*` project. `TreatWarningsAsErrors` is on, so this is
a hard gate and the added test compiles clean under it.

```
dotnet test api/E3A.slnx
  Passed!  - Failed: 0, Passed: 420, Skipped: 0, Total: 420, Duration: 1 s
```

419 → **420**, exactly one new test, as predicted. (The intermediate mutation run reported `Failed: 1,
Passed: 419, Total: 420`; that state was reverted and re-verified.)

**No Azure resource was created and no `az` command was run.**

## Docs

No doc moved. Neither change alters product behaviour or design: the test change covers behaviour that was
already documented and already shipped, and the log-level floor changes no runtime behaviour of the API. I
grepped all of `/docs` for `logging`, `log level`, `loglevel` and `trace` — **zero matches** — so no doc
describes logging policy and there is nothing to diverge from. Per `.claude/rules/docs-sync.md` that is not
even incompleteness worth reporting; it is simply an area no doc enters.

## Notes for review — round 1

- The new test carries two assertions, `Be(Invalid)` and `NotBe(Expired)`. The second is logically implied by
  the first. I kept it because the brief asked for it explicitly and because it states the intent — this test
  exists to separate the two rejection reasons, not merely to reject. `conventions/dotnet-testing.md` §3
  permits multiple `.Should()` calls describing one behaviour.
- `OAuthStateProtectorTamperTests.cs` grew from 111 to **123 lines**, further past the ~100-line guidance in
  `conventions/dotnet-testing.md` §9. This is the same deviation 4 already declared and accepted in round 0
  (splitting the class would break the plan's verbatim class names); the added method makes it larger, not
  differently wrong. Flagging it rather than letting the reviewer rediscover it.
- The plan's §Test plan and §Definition of done still describe **57** tests; the suite now has **58** methods.
  I did not edit the plan — it is the approved artefact and the extra test is a reviewer-mandated addition,
  recorded here. If the plan's count is meant to stay authoritative, this is the line to reconcile.
- Item 2 of round 0's §Notes for review ("Test 19 … locks the order") was wrong, as the review found. The
  claim is now true of the suite as a whole, but via the new test, not via test 19. The original text is left
  in place above as the audit trail rather than being silently corrected.
