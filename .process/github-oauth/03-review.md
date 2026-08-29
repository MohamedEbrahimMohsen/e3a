VERDICT: CHANGES_REQUESTED

# Review — GitHub OAuth Login for Creators

Scope reviewed: every file in `git status` on `feature/github-oauth` vs `main` @ `6def01a`, read end to end —
21 new production files, 2 generated migration files, 14 new test files, 8 modified production files,
2 modified docs, the Postman collection. Build and test run independently (results in §Verified).

The slice is materially correct. The `state` protector, the callback branch table, the claim set, the
open-redirect posture and the secret handling all hold up under trace. One blocking finding: the test that
the plan designates as the lock on the signature-before-expiry order does not actually lock it.

## Blocking

### 1. No test distinguishes signature-before-expiry from expiry-before-signature — the order is unguarded

**Where:** `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs:66-75`
(`Validate_ShouldReturnInvalid_WhenExpiryIsExtendedWithoutResigning`), against
`api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:48-58`.

**Rule:** plan §Decisions #6 and §Definition of done line 577 ("`state` is verified signature-first,
expiry-second … a tampered expiry yields `Invalid`, not `Expired`"); plan §Test plan row 19, whose Asserts
column claims this test "locks the signature-before-expiry order"; `conventions/dotnet-testing.md` §5.

**Problem:** The production code is correct — `Validate` runs `FixedTimeEquals` at line 50 and the expiry
comparison at line 55. But no test in the suite fails if those two blocks are transposed. Test 19 pushes the
expiry a **year into the future**; under a swapped order that input is "not expired", so control still falls
through to the signature check and still returns `Invalid`. I walked all fourteen inputs across
`OAuthStateProtectorTests.cs` and `OAuthStateProtectorTamperTests.cs`: every one either fails the
null/segment-count/parse guards (which precede both checks), or carries a **future** expiry, or
(`Validate_ShouldReturnExpired_WhenExpiryHasPassed`, line 67) carries a past expiry with a **valid**
signature. Not one of them is `past expiry + bad signature`, which is the only shape that separates the two
orderings. The implementation report §Notes for review item 2 repeats the plan's claim that test 19 locks the
order; that claim is false, and an unverified claim is itself a finding.

**Failure:** Transpose lines 50-53 and 55-58 of `OAuthStateProtector.cs` and all 419 tests still pass. The
regression that ships green is an expiry oracle: for
`state = $"{nonce}.{DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()}.{signatureOfTheOriginalPayload}"`
the swapped order returns `Expired`, which the handler turns into `#error=AUTHENTICATION_STATE_EXPIRED` —
telling an attacker who is mutating the expiry segment that their forgery was rejected for staleness rather
than for a bad signature, i.e. branching the response on unauthenticated data. Under the shipped order the
same input returns `Invalid`.

**Fix:** One added test method in `OAuthStateProtectorTamperTests.cs`, e.g.
`Validate_ShouldReturnInvalid_WhenExpiryIsMovedIntoThePastWithoutResigning` — take `_sut.Create()`, replace
segment 1 with a past unix-seconds value, leave segment 2 untouched, assert `OAuthStateStatus.Invalid`
(explicitly not `Expired`). Nothing in production code needs to change.

## Non-blocking

- `api/E3A.Infrastructure/DependencyInjection.cs:24-30` — the disclosed `Microsoft.Extensions.Http` `Trace`
  exposure. **Ruling: acceptable as a disclosed risk in this slice; do not add a redacting handler now.**
  Reasoning, since the brief asked for mine rather than the implementer's: (a) `LoggingScopeHttpMessageHandler`
  is the *outermost* handler, so the obvious mitigation — an `AddHttpMessageHandler` that strips
  `Authorization` — would not work, because the header is logged before any added handler runs; the only real
  fixes are `.RemoveAllLoggers()` on the typed-client builder or a `"System.Net.Http.HttpClient": "Warning"`
  floor under `Logging:LogLevel`, neither of which the plan scoped. (b) Nothing in the repo sets `Trace`: the
  committed `api/E3A.Api/appsettings.Development.json:2-3` sets `Default: Debug`, and
  `CoreLogging:Console:MinimumLevel` is `Debug`. (c) Blast radius is bounded — the token is `read:user` scoped
  (public profile read only, no repo access, no writes) and the e3a JWT is not in that log at all. (d) Turning
  verbosity to `Trace` is already a decision to log every inbound `Authorization: Bearer <e3a JWT>` header;
  redacting only the GitHub one would buy false assurance. The aggravating factor — GitHub OAuth App tokens do
  not auto-expire, so a leaked one is durable — is why this belongs on the debt list next to the client-secret
  rotation rather than being dropped. Recommend the one-line `Logging:LogLevel` floor be carried with that
  rotation before the app is public. The plausibility of someone enabling `Trace` while debugging this very
  flow is not negligible, which is exactly why it should be written down rather than fixed half-way.
- `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs:44-72, 86-95,
  111-122` — four of the seven failure branches (state invalid, state expired, profile fetch failed, blank
  login) assert the redirect URL but not `_userRepository.DidNotReceive().SaveChangesAsync(...)`. The exact
  string equality on `RedirectUrl` already catches a fall-through, so this is not vacuous; it is one line each
  to make the coverage contract uniform.
- `api/E3A.Infrastructure/Authentication/GitHubOAuthClient.cs` — the one production class in the slice with
  real branching (error-payload inspection at line 34, three catch clauses at lines 77-91) and no committed
  test. The plan declared it out of scope and `api/E3A.Tests/E3A.Tests.csproj:17-18` confirms the test project
  references only Application and Domain, so the exclusion is structural rather than an omission. But the only
  evidence about it is the uncommitted `HttpListener` harness — see §On the unverified parts.
- `api/E3A.Domain/Identity/User.cs:41-56` — a latent 500 the plan's Deferred list implies but nobody states
  plainly: `CreateFromGitHub` sets `UserName`/`NormalizedUserName` from the GitHub login and decision 14 never
  refreshes them. GitHub logins are reusable, so if user A renames away from `octocat` and user B takes it,
  B's first sign-in inserts a row whose `NormalizedUserName` collides with A's on the unfiltered unique
  `UserNameIndex` (`AppDbContextModelSnapshot.cs:500-503`) — `DbUpdateException` → JSON 500, not the `#error=`
  redirect acceptance decision 10 promises for everything else. Plan decision 11 explicitly accepts DB
  failures as 500s, so this is plan-sanctioned, not a defect; worth recording as known behaviour.
- Same code path, second-order: `MarkDeleted()` (`api/E3A.Domain/Identity/User.cs:65`) is never called from
  production code today, so soft-deleted users cannot yet exist; when they can, the same unfiltered
  `UserNameIndex` will collide on re-registration while the filtered `IX_AspNetUsers_GitHubId` will not.
- `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTests.cs:46-55`
  (`Create_ShouldProduceDifferentStates_WhenCalledTwice`) — with the generator stubbed to return two different
  nonces, this only re-proves that segment 0 is the nonce, which line 31 already asserts. Harmless, but it is
  not evidence about entropy.
- `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTamperTests.cs` (111 lines) and
  `CompleteGitHubLoginHandlerFailureTests.cs` (134 lines) exceed the ~100-line guidance in
  `conventions/dotnet-testing.md` §9. Declared as deviation 4; the justification (the plan fixes these class
  names verbatim) is reasonable and file length is not on the absolutes list. Leave it.

## Verified

Claims from `02-implementation.md` confirmed independently:

- **Build.** `dotnet build api/E3A.slnx --no-incremental` gives `Build succeeded. 9 Warning(s) 0 Error(s)`.
  All 9 are in `api/core-libraries` (Core.Validation x2 CS8602, Core.OTP x2 CS8618, Core.Notifications x5
  CS8618). Zero in any `E3A.*` project. Exactly as claimed.
- **Tests.** `dotnet test api/E3A.slnx` gives `Failed: 0, Passed: 419, Skipped: 0, Total: 419`. As claimed.
- **All 57 planned test methods exist with the planned names.** Extracted and counted: 57, no additions, no
  renames, no omissions.
- **Migration.** `20260829112516_oauth004.cs` contains exactly `AvatarUrl nvarchar(500) NULL`,
  `DisplayName nvarchar(200) NULL`, `GitHubId bigint NULL`, `GitHubLogin nvarchar(100) NULL` and
  `IX_AspNetUsers_GitHubId` unique with `filter: "[GitHubId] IS NOT NULL AND [IsDeleted] = 0"` (lines 13-45),
  with a symmetric `Down`. Nothing else. `AppDbContextModelSnapshot.cs` adds +19 lines, all four properties
  and the one index.
- **21 production files, no extras.** Counted 20 files under the three new `Authentication` folders plus
  `Options/GitHubAuthenticationOptions.cs` = 21, matching the plan's Files-to-create table one-for-one on
  path, namespace and signature. No `*Validator.cs` (decision 22 held). No `Requests.cs`. `Program.cs`
  untouched (`git diff main -- api/E3A.Api/Program.cs` is empty). `Directory.Packages.props` untouched, no
  new `PackageReference`, no `FrameworkReference`.
- **Deviation 1 (CA1848 to `[LoggerMessage]`) is sound.** `api/Directory.Build.props:7-9` sets
  `TreatWarningsAsErrors=true` with `AnalysisLevel=latest-recommended`, so CA1848 is a build error, and there
  was no in-repo `ILogger` precedent to mirror. The class stays `sealed` (`GitHubOAuthClient.cs:10`), and the
  same information is still logged: `LogUnsuccessfulResponse` carries `{RequestPath}` and `{StatusCode}`,
  `LogFaultedRequest` carries `{RequestPath}` plus the exception object, which is strictly more than the four
  `LogWarning` calls the plan specified. The three catch clauses remain separate as planned.
- **Deviation 2 (`using var request` moved to the call sites)** is verified at `GitHubOAuthClient.cs:19` and
  `:44`. The plan's version does not compile, and the request and its `FormUrlEncodedContent` are still
  deterministically disposed on every path, including a fault before `SendAsync`.
- **Deviation 3 (no `DefaultCodes`)** is verified: no such class exists in the repo, and
  `EngineersController.cs:20-21` and `PublishController.cs:10-11` both use a bare class-level `[Authorize]`.
  `AuthenticationController.cs:12,16,24` mirrors that exactly.
- **No secret in the diff.** `api/E3A.Api/appsettings.json` is git-ignored (`.gitignore:23`) and untracked.
  The only `ClientSecret` values in tracked or new source are `string.Empty` (options default),
  `options.ClientSecret` (the form-body read) and `"dummy-client-secret"` (test factory).
- **No Azure resource, no `az` command.** Nothing in the diff touches infrastructure.

Plan items confirmed present and correct — the risk centre:

1. **`state` protector.** `OAuthStateProtector.cs:29-61`: null or whitespace gives `Invalid`; segment count
   not equal to 3 gives `Invalid`; a failed `long.TryParse(NumberStyles.None, InvariantCulture)` gives
   `Invalid`; then `CryptographicOperations.FixedTimeEquals` on the recomputed signature gives `Invalid`;
   **then** the expiry comparison gives `Expired`. The signature genuinely precedes the expiry. The
   comparison is `FixedTimeEquals` over UTF-8 bytes, and because segments 0 and 1 cannot contain the
   separator (nanoid alphabet `0-9a-z` per `Core.Utilities/Generator.cs:18`, Base64Url signature), rejoining
   segment 0 and segment 1 reproduces the signed payload byte-for-byte, so there is no canonicalisation
   ambiguity. The signing key is `JwtOptions.Key`, server-only. A different key, a truncated signature, an
   over-long or short segment count and a non-numeric expiry each land on `Invalid`, and each has a named
   passing test (`OAuthStateProtectorTamperTests.cs:24-105`). The nonce comes from `IGenerator` (skill 8.2),
   and `Generate(options.StateNonceSize)` binds positionally to the `Generate(int, string)` overload as the
   plan required.
2. **Open redirect — traced, not taken on trust.** The only request-bound values in the slice are `code` and
   `state` (`AuthenticationController.cs:25`). `code` reaches only a `FormUrlEncodedContent` dictionary
   (`GitHubOAuthClient.cs:25`); `state` reaches only `Validate`. Both URL builders take their base from
   `IOptions`: `GitHubAuthorizationUrlGenerator.cs:12-18` (`AuthorizationUrl` plus `CallbackUrl`) and
   `AuthenticationRedirectUrlGenerator.cs:8-16` (`WebRedirectUrl`), whose only variable parts are a
   server-issued token and an `ErrorCodes` constant, each passed through `Uri.EscapeDataString`. Both
   `Redirect(...)` calls take `result.RedirectUrl` and nothing else. There is no `returnUrl`, `redirect_uri`
   or `next` parameter anywhere in the slice, on the success path or on any of the seven failure paths.
3. **Secret and token leakage.** I read `api/core-libraries/Core.Logging/RequestLoggingMiddleware.cs` end to
   end: it logs inbound `Method`, `Path`, `QueryString`, `Host`, client IP, a fixed Cloudflare header set,
   `User-Agent` and `StatusCode` (lines 41-66). No request body, no response body, and **no response
   headers** — so the issued JWT in the `Location` header does not reach the request log, and the client
   secret (outbound POST body only) is structurally out of reach. The middleware order at `Program.cs:96` is
   unchanged. The disclosed residual, the inbound callback `QueryString` carrying the single-use `code` and
   the `state`, is real and is written down in both plan and report.
4. **`NormalizedUserName` on just-in-time creation.** `User.cs:52` sets
   `NormalizedUserName = gitHubLogin.ToUpperInvariant()` alongside `UserName`, and `SecurityStamp` at line
   53. `UserTests.cs:22-28` pins it with a mixed-case login (`"OctoCat"` to `"OCTOCAT"`), so a regression to
   a null normalized name — which SQL Server would only surface on the *second* GitHub user, via the unique
   `UserNameIndex` — fails at unit-test time.
5. **JWT claims.** `UserClaimsGenerator.cs:15-21` emits exactly four claims, every type taken from
   `CurrentUserService.Constants` rather than a re-typed literal, so the emitter is bound to the reader
   (`api/core-libraries/Core.Identity/Tokens/CurrentUser/CurrentUserService.cs:10-15,19`). `UserIdClaimType`
   is `ClaimTypes.NameIdentifier` carrying `user.Id.ToString()`, which is what `ICurrentUserService.UserId`
   parses and what the engineer and publish handlers compare to `Engineer.OwnerUserId`. **Decision 3 held:
   no role claim.** `Program.cs:112-114` still gates the notification endpoints behind
   `RequireRole(RoleNames.Admin/User)`, and `UserClaimsGeneratorTests.cs:53-59` asserts that no
   `ClaimTypes.Role` claim is emitted, so a GitHub visitor cannot reach them.
6. **All seven callback branches are 302s, reachable and tested.** `CompleteGitHubLoginHandler.cs:15-49`:
   code missing (test 46), state invalid (47-48, including "GitHub is never called"), state expired (49),
   exchange failed (50), profile fetch failed (51), profile id not positive (52), blank login (53), plus
   "always the configured web URL, never a GitHub host" (54). Every one returns `Failure(...)`, i.e.
   `{WebRedirectUrl}#error=<CODE>`. The handler has no `throw` and no `try`/`catch`, and the failure tests
   assert exact string equality bound to `ErrorCodes.*` constants, not literals. The single
   `SaveChangesAsync` (line 64) is on the success path only, asserted `Received(1)` for both the create and
   the update path.

- **Handlers are `try`/`catch`-free.** The only `try`/`catch` in the slice is `GitHubOAuthClient.cs:63-91`
  (Infrastructure, plan decision 19), with caller cancellation correctly re-thrown via
  `when (!cancellationToken.IsCancellationRequested)`.
- **Skill 8 catalog:** 8.1 all caps live in `GitHubAuthenticationOptions`, none on the entity; 8.2
  `IGenerator` supplies the nonce; 8.3 and 8.4 are not applicable; 8.5 the partial-index SQL filter stays
  with the index in `AppDbContext.ConfigureUsers` (`AppDbContext.cs:37-48`) and the global soft-delete method
  is untouched (line 95 already carried `User`). No DON'T pattern is present in the diff.
- **Style absolutes:** file-scoped namespaces in every hand-written file (the generated migration keeps EF's
  block form, as generated); `sealed` on every new class and record; `DateTimeOffset` throughout, no
  `DateTime`; `.ConfigureAwait(false)` on every non-controller, non-test await; exactly two comments in the
  slice, both the WHY comments the plan authorised (`OAuthStateProtector.cs:14`, `UserClaimsGenerator.cs:10`).
- **Error codes and resources.** Six new `ErrorCodes` constants, each with a matching key in **both**
  `Messages.en.resx` and `Messages.ar.resx`, Arabic without tashkeel, no runtime placeholders in either.
- **Postman.** `postman/e3a.postman_collection.json` parses as valid JSON; the item list is
  `['Authentication', 'Engineers', 'Catalog', 'Publishing']` with `Authentication` first. `GitHub Login` and
  `GitHub Callback` both carry `"auth": {"type": "noauth"}` and
  `"protocolProfileBehavior": {"followRedirects": false}`; `Get Current User` inherits the collection bearer.
  URLs, methods and the `code`/`state` query array match the shipped routes. Nothing stale, nothing orphaned.
- **Docs sync.** Both planned edits were actually made: `docs/architecture.md:28` (the fragment-handoff
  principle) and `docs/implementation-plan.md:63` (the rewritten `Auth (anon):` clause). I read every
  auth-adjacent line in `/docs` and found no divergence: no doc now describes a cookie, a query-string token,
  a server-side `state` store, or a JSON error on the callback. `docs/implementation-plan.md:40` still lists
  `IsBlocked` and `docs/plugin-spec.md:114-116` still anticipates GitHub profile URLs in attribution — both
  are planned-but-unbuilt, i.e. incompleteness, which per `.claude/rules/docs-sync.md` is never a finding and
  must not be trimmed. No doc was created outside `/docs`.

## On the unverified parts — stated precisely

The live GitHub round trip is not verified and **is not a finding**; it needs a human at a consent screen.
The report says so plainly and does not overclaim.

On the throwaway `HttpListener` harness: judged against the code I read, it proves what the report says it
proves and no more. `GitHubOAuthClient.cs:19-30` does set `Accept: application/json` per request on the
exchange and does put `client_secret` in a `FormUrlEncodedContent` body; lines 44-47 do set
`Accept: application/vnd.github+json` and a **per-request** `Authorization: Bearer` header (never on
`DefaultRequestHeaders`, which is shared across callers); `DependencyInjection.cs:26-29` does set the
timeout, the default `Accept` and the `User-Agent` GitHub requires; line 61 does strip the query via
`GetLeftPart(UriPartial.Path)` before logging. So the harness corroborates a request shape that is
independently readable from the source, and it adds one thing source reading alone cannot: that the
DI-composed pipeline actually produces that shape end to end, and that each fault mode collapses to `null`
rather than escaping.

What remains genuinely unproven, and should be read as such: the harness is **not committed**, so nothing in
CI reproduces it and it guards against no regression; the listener validated no credentials, no registered
callback URL and no scope grant; and `GitHubOAuthClient` therefore ships with zero durable tests. Also
unproven: that the GitHub App's registered callback matches `CallbackUrl`, that the credentials on disk are
live, and that the SPA consumes the fragment (feature 4).

## Test quality

Per class — does it constrain the implementation?

- `Identity/UserTests.cs` — **constrains.** Test 2 pins `NormalizedUserName` with a mixed-case login, the one
  invariant no substituted repository could ever catch. Test 8 pins the write-once identity fields
  (decision 14). Dates use `BeOnOrAfter(before)`; no `UtcNow` equality, no `Thread.Sleep`, no reflection.
- `Authentication/Shared/OAuthStateProtectorTests.cs` — **constrains**, with one weak member
  (`Create_ShouldProduceDifferentStates_WhenCalledTwice`, noted above). The expired case is produced by
  configuring `StateExpirationMinutes = -1` rather than sleeping — correct and deterministic.
- `Authentication/Shared/OAuthStateProtectorTamperTests.cs` — **constrains tampering, but is blind to the
  check order.** That is blocking finding 1. Everything else here is real: the cross-key test builds a second
  protector with a different `JwtOptions.Key`, and the truncation test proves `FixedTimeEquals` returns false
  on a length mismatch instead of throwing.
- `Authentication/Shared/GitHubAuthorizationUrlGeneratorTests.cs` — **constrains.** The escaping test slices
  the query off the configured base and asserts the raw scheme separator is absent and the percent-encoded
  form present; it would fail on a hand-rolled concatenation.
- `Authentication/Shared/AuthenticationRedirectUrlGeneratorTests.cs` — **constrains.** Exact string equality,
  and the failure case is bound to `ErrorCodes.AuthenticationStateInvalid`, not a literal.
- `Authentication/Shared/UserClaimsGeneratorTests.cs` — **constrains.** Every assertion is typed off
  `CurrentUserService.Constants`, so a renamed claim type breaks the test rather than silently breaking every
  authorised endpoint. The no-role-claim test is a genuine gate on decision 3.
- `Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs` — **constrains.** The substitute
  supplies only the state; the assertion runs through the *real* URL generator, so it is not an echo.
- `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs` — **constrains.** Test 41 is the
  strongest in the slice: it captures the `User` handed to `AddAsync` and the `List<Claim>` handed to
  `GenerateTokenAsync` and proves the id claim parses back to the id of the row actually persisted. That is a
  real invariant, not a restatement of a stub.
- `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerReturningUserTests.cs` — **constrains.**
  Lines 31-32 compile the captured predicate and evaluate it against the stored user, so a handler that
  matched on `GitHubLogin` would get `null` back (the profile login is `octocat-renamed`) and would call
  `AddAsync`, failing the test. Decision 4 is genuinely locked, not merely asserted.
- `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` — **constrains.** Exact
  `RedirectUrl` equality on every branch, plus an ordering proof
  (`Handle_ShouldNotCallGitHub_WhenStateIsInvalid` verifies nothing outbound happens before the state is
  verified). Minor gap noted under Non-blocking.
- `Authentication/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs` — **constrains.** The mapping test looks
  tautological at a glance but is not: the factory's `DisplayName` and `GitHubLogin` differ, so a transposed
  field mapping fails. Both throw branches assert on `ErrorCodes.*`, and the unauthorized case also proves
  the repository is never touched.

Support files (`UserFactory`, `GitHubAuthenticationOptionsFactory`, `GitHubProfileFactory`) build entities
through `User.CreateFromGitHub` and real constructors — no object-initializer `new User`, no reflection.
