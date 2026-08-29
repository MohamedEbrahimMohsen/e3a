VERDICT: APPROVED

# Review round 2 — GitHub OAuth Login for Creators

Scoped verification pass over the round-1 rework. I re-ran the mutation experiment myself rather than
accepting the report transcript, established scope containment from filesystem evidence rather than the
report claim, and parsed the changed config with the real .NET configuration loader.

Round 1's single blocking finding is resolved. No new blocking findings.

## Blocking

None.

## Verified

### 1. The new test genuinely bites — mutation experiment reproduced first-hand

I backed up `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs`, transposed the two blocks so
the expiry comparison (was `:55-58`) runs before the `FixedTimeEquals` check (was `:48-53`), and ran the
suite. Result, verbatim:

    Failed ...OAuthStateProtectorTamperTests.Validate_ShouldReturnInvalid_WhenExpiryIsMovedIntoThePastWithoutResigning [517 ms]
    Error Message:
     Expected result to be OAuthStateStatus.Invalid {value: 1}, but found OAuthStateStatus.Expired {value: 2}.
    Failed!  - Failed:     1, Passed:   419, Skipped:     0, Total:   420

Exactly one failure, and it is the new test, with precisely the expiry-oracle symptom round 1 predicted. The
implementer's transcript is accurate in every particular. `Failed: 1, Passed: 419` independently re-confirms
round 1's central claim: the other 419 tests are collectively blind to the check order, and this one test is
the entire guard.

The test at `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs:77-87` is the right shape.
It is the only input in the suite that is past-expiry plus bad-signature, the only shape that separates the
two orderings. It takes a real `_sut.Create()`, replaces segment 1 with a past unix-seconds value and leaves
the signature untouched, so the signature is genuinely invalid for the mutated payload.

Restore verified byte-identical. `md5sum` after restore is `1a11a5e1fcdcc225ace79ee2c325dfc4`, matching the
claimed hash, and `cmp` against my own pre-mutation backup reports IDENTICAL. Re-ran after restore:
`Build succeeded. 9 Warning(s) 0 Error(s)` and `Failed: 0, Passed: 420`. The tree I am approving is
byte-for-byte the tree round 1 read, plus the two intended additions.

Production order confirmed still correct in the restored file: `FixedTimeEquals` at
`OAuthStateProtector.cs:50`, expiry comparison at `:55`.

### 2. Scope containment — established from mtimes, not from the report

Round 1's `03-review.md` was written at `14:46:36`. Exactly three source files have a later mtime:

- `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs` — `14:48:13` (the added test)
- `api/E3A.Api/appsettings.Development.json` — `14:48:30` (the log-level floor)
- `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs` — `14:50:00` (mutation then restore;
  content proven identical above)

Plus `.process/github-oauth/02-implementation.md` and `04-metrics.md`, which are pipeline artifacts. Every
other production file, test file, the Postman collection and both docs predate the round-1 review and are
therefore untouched. No production code changed, no other test edited, no out-of-scope refactor. This is
stronger evidence than a diff, because it also rules out a change-and-revert that a diff would hide.

### 3. The ordering-claim sweep — I believe it

Spot-checked the rows the implementer graded, reading each test body against the plan's Asserts column:

- Row 48 (`CompleteGitHubLoginHandlerFailureTests.cs:54-62`) — genuine lock.
  `DidNotReceive().ExchangeCodeForAccessTokenAsync` under a state status of Invalid fails the moment the
  exchange moves above the state check. Confirmed against `CompleteGitHubLoginHandler.cs:20-32`.
- Row 50 (`CompleteGitHubLoginHandlerFailureTests.cs:74-84`) — genuine. `DidNotReceive().GetProfileAsync`
  with a null access token locks exchange-before-profile.
- Row 56 (`GetCurrentUserQueryHandlerTests.cs:41-52`) — genuine. `DidNotReceive().GetByIdAsync` locks
  guard-first ordering.
- Rows 42/44 (`CompleteGitHubLoginHandlerReturningUserTests.cs:31-32, 37-43, 55-62`) — genuine. The stub
  compiles the captured predicate and evaluates it against the stored user, so a handler matching on
  `GitHubLogin` gets null (profile login is `octocat-renamed`), calls `AddAsync`, and fails the test.
- Row 8 (`UserTests.cs:83-94`) — the "weak but not vacuous" grading is exactly right.
  `UpdateGitHubProfile(string?, string?)` never receives the identity and the setters are private, so the
  test's real value is catching a future body that assigns identity from the profile fields it does have.
- Row 21 (`OAuthStateProtectorTamperTests.cs:99-107`) — holds; a throw fails rather than passes.

The sweep missed three rows, all of which hold, so no second false claim exists:

- Row 23 (`GitHubAuthorizationUrlGeneratorTests.cs:11-19`) — the "open-redirect guard" claim. The `StartWith`
  assertion is real and is reinforced by the escaping test at `:34-43`.
- Rows 4 and 7 (`UserTests.cs:38-47, 72-81`) — the `BeOnOrAfter(before)` claims hold; an unstamped date is
  `DateTimeOffset.MinValue` and fails.

### 4. The log-level floor is committed, parses, and is effective

`api/E3A.Api/appsettings.Development.json` is tracked (`git ls-files` confirms it; `appsettings.json` is
not), so the floor reaches every clone and CI run. The `//` comment has in-repo precedent at
`api/E3A.Api/appsettings.json:3`.

I did not settle for a comment-stripping approximation. I built a throwaway probe against the real
`Microsoft.Extensions.Configuration.Json` package and pointed it at the actual file:

    PARSED OK by Microsoft.Extensions.Configuration.Json
      Logging:LogLevel:Default = Debug
      Logging:LogLevel:Microsoft.AspNetCore = Information
      Logging:LogLevel:System.Net.Http.HttpClient = Warning

The file parses and the key binds at the exact path the logging filter reads. Startup will not break.

One thing worth recording that the report did not establish: the floor is still effective despite
`Core.Logging/DependencyInjection.cs:41-42` calling `builder.ClearProviders()` then `AddSerilog(logger)`.
`ClearProviders()` removes providers but not `LoggerFilterOptions` rules, and MEL evaluates category filter
rules before dispatching to any provider, so `System.Net.Http.HttpClient` at Warning suppresses the
`LoggingScopeHttpMessageHandler` Trace output regardless of the Serilog sink levels. It is in fact
belt-and-braces: `CreateBaseConfiguration` (`Core.Logging/DependencyInjection.cs:52`) already pins Serilog at
`MinimumLevel.Debug()`.

### 5. Build, tests, and non-regression

- `dotnet build api/E3A.slnx --no-incremental` gives `Build succeeded. 9 Warning(s) 0 Error(s)`. All 9 are in
  `api/core-libraries` (Core.Validation x2 CS8602, Core.OTP x2 CS8618, Core.Notifications x5 CS8618). Zero in
  any `E3A.*` project. As claimed.
- `dotnet test api/E3A.slnx` gives `Failed: 0, Passed: 420, Skipped: 0, Total: 420`. As claimed.
- Round-1 non-blocking items left alone, per instruction — confirmed: the four failure branches at
  `CompleteGitHubLoginHandlerFailureTests.cs:44-52, 64-72, 86-95, 111-122` still lack
  `DidNotReceive().SaveChangesAsync`, `GitHubOAuthClient` still has no committed test, and
  `api/E3A.Infrastructure/DependencyInjection.cs` is untouched. Not findings.
- Nothing round 1 verified was re-opened. Signature before expiry (`OAuthStateProtector.cs:50` then `:55`);
  no open redirect (`CompleteGitHubLoginHandler.cs` binds only Code and State, neither reaching a URL
  builder); `NormalizedUserName` set (`UserTests.cs:21-28` passing); no role claim
  (`UserClaimsGeneratorTests.cs`, plan row 34, passing); seven callback branches still 302-with-fragment
  (`CompleteGitHubLoginHandler.cs:15-49`), single `SaveChangesAsync` on the success path only at `:64`, no
  `throw` and no `try`/`catch` in the handler.
- Postman still mirrors the API surface. Collection untouched since round 1; re-parsed and cross-checked
  against `AuthenticationController.cs:11-31`. `GitHub Login` GET `/api/auth/github/login` noauth,
  `GitHub Callback` GET `/api/auth/github/callback?code&state` noauth (both matching `[AllowAnonymous]`),
  `Get Current User` GET `/api/auth/me` inherit (matching the class-level `[Authorize]`). Nothing missing,
  stale or orphaned.
- Docs sync: no divergence. `docs/` and `postman/` have no file modified since the round-1 review. Neither
  rework change alters behaviour, scope, architecture, policy or a contract. I independently confirmed the
  implementer's grep: no file in `/docs` mentions logging, log level or trace, so the log-level floor enters
  an area no doc describes — nothing to diverge from. No doc created outside `/docs`.
- No Azure resource, no `az` command. A test method and a config line could not have introduced one.
- Skill absolutes hold on both changed files: the test class is already `sealed`, file-scoped namespace, no
  comment in the added method, `DateTimeOffset` (not `DateTime`) at `OAuthStateProtectorTamperTests.cs:81`,
  no `ConfigureAwait` in a test body (correct per convention). The `//` in the JSON is config, not C#.

## Non-blocking

- `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs` — now 123 lines, further past the
  ~100 guidance in `conventions/dotnet-testing.md` section 9 (line 227). This is the same deviation 4
  declared and accepted in round 0; the added method makes it larger, not differently wrong, and splitting
  would break the class names the plan fixes verbatim. Leave it.
- `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs:86` — `NotBe(Expired)` is logically
  implied by `Be(Invalid)` on line 85. Permitted by `conventions/dotnet-testing.md` section 3 (line 109):
  multiple `.Should()` calls are fine when they describe the same behaviour. It also documents the intent —
  this test exists to separate two rejection reasons, not merely to reject. Keep it.
- The plan's stale test count (57 vs the suite's 58). My ruling, since it was asked for: not worth
  correcting. `01-plan.md` is the approved, signed-off artifact, and its integrity as a record of what was
  approved is worth more than numeric agreement with a reviewer-mandated addition made afterwards. The delta
  is already recorded in `02-implementation.md`, section "Notes for review — round 1", which is the right
  place for it. Editing an approved plan to match later work erases the very gap the audit trail exists to
  show. Artifact-accuracy note only; no code defect.

## Test quality

Only one test class changed. `OAuthStateProtectorTamperTests` now constrains the check order, which was
precisely the round-1 gap. This is not an assertion I took on trust: I mutated the production code and
watched this test — and only this test — fail with the predicted message. It is the strongest kind of test in
the slice, because it guards an invariant that is invisible to every other input in the suite and whose
violation would ship silently green.

All other test classes retain the round-1 grading; none of them were touched.
