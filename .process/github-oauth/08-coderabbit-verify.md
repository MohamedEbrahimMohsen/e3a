VERDICT: APPROVED

# Stage 4 verification — CodeRabbit rework, PR #5 (`github-oauth`)

Fresh reviewer. Verified against the worktree, not against `07-coderabbit-rework.md`.
Three triage items landed, all six rejections held, no blocking findings.

## Blocking

None.

## Verified independently

### Build and tests

| Claim | Result |
|---|---|
| `dotnet build api/E3A.slnx --no-incremental` -> 0 errors, 9 warnings | **Confirmed.** `Build succeeded. 9 Warning(s) 0 Error(s)`. All 9 are `core-libraries`: `Core.Validation/Extensions/RequiredValidationExtensions.cs:52,57` (CS8602), `Core.OTP/Entities/OTP.cs:30` x2, `Core.Notifications/Entities/Notification.cs:35` x2 and `NotificationTemplate.cs:15` x3 (CS8618). Zero `E3A.*` warnings, so `TreatWarningsAsErrors` was not suppressed. |
| `dotnet test api/E3A.slnx` -> 433 passed | **Confirmed.** `Failed: 0, Passed: 433, Skipped: 0, Total: 433`. |
| Nothing deleted (420 -> 433) | **Confirmed** by diff. The only test that changed identity is `UserTests.CreateFromGitHub_ShouldSetUserNameAndNormalizedUserNameFromLogin` -> `...FromTheResolvedName_WhenItDiffersFromTheLogin` (`api/E3A.Tests/Identity/UserTests.cs:22`). The renamed-away behaviour ("a free login becomes the `UserName`") is still pinned, in two places: `UserNameResolverTests.cs:86` and `CompleteGitHubLoginHandlerTests.cs:42` (`user.UserName == profile.Login`). No coverage was traded away. |

### Item 1 — does the CSRF fix actually close the hole?

Traced end to end, not read off the report:

1. **Cookie issued.** `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:24` — `Response.Cookies.Append(options.StateCookieName, result.StateNonce, ...)` with `HttpOnly`, `Secure`, `SameSite=Lax`, `IsEssential`, `Path=/api/auth`, `MaxAge = StateExpirationMinutes` (`OAuthStateCookieOptionsGenerator.cs:7-19`). The nonce reaching the controller is the same one embedded in the state — `GetGitHubLoginUrlQueryHandler.cs:12-15` returns `new GitHubLoginUrlResult(authorizationUrl, state.Nonce)` from a single `Create()` call.
2. **Cookie read.** `AuthenticationController.cs:34` — `Request.Cookies[options.StateCookieName]`. `SameSite=Lax` is the correct mode: GitHub's callback is a top-level cross-site GET, so the cookie is sent; `Strict` would not be. `Path=/api/auth` is a prefix of `/api/auth/github/callback`, so the path match holds.
3. **Fixed-time comparison against the state's own nonce segment.** `OAuthStateProtector.cs:43` — `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(segments[0]), Encoding.UTF8.GetBytes(nonce))` -> `OAuthStateStatus.Invalid`.
4. **Missing cookie lands on Invalid too.** `OAuthStateProtector.cs:31` — `string.IsNullOrWhiteSpace(nonce)` -> `Invalid`, before segment parsing. So no cookie and wrong cookie converge on the same status, and `CompleteGitHubLoginHandler.cs:23-26` maps it to `#error=AUTHENTICATION_STATE_INVALID` **above** the code exchange at `:33`. An attacker-minted state never reaches GitHub.

**Can a state minted in browser A be completed in browser B?** No. B holds either no `e3a_oauth_state` cookie (-> `:31`, `Invalid`) or its own nonce (-> `:43`, `Invalid`). The attacker cannot plant A's nonce into B's cookie jar: the cookie is `HttpOnly` + `Secure` and scoped to the API origin, and knowing the nonce value confers no ability to write it. The fix is not theatre.

**Mutation proof, run not assumed.** I deleted the four-line nonce block at `OAuthStateProtector.cs:43-46` and re-ran the suite:

```
Failed  OAuthStateProtectorNonceTests.Validate_ShouldReturnInvalid_WhenNonceDoesNotMatch
        Expected result to be OAuthStateStatus.Invalid {value: 1}, but found OAuthStateStatus.Valid {value: 0}.
Failed  OAuthStateProtectorNonceTests.Validate_ShouldReturnInvalid_WhenTheStateIsPresentedByAnotherBrowser
        Expected _sut.Validate(firstBrowserState.Value, secondBrowserState.Nonce) to be
        OAuthStateStatus.Invalid {value: 1}, but found OAuthStateStatus.Valid {value: 0}.
Failed!  - Failed: 2, Passed: 431, Total: 433
```

The cross-browser test (`api/E3A.Tests/Authentication/Shared/OAuthStateProtectorNonceTests.cs:57-65`) does fail when the comparison is removed. File restored from backup and confirmed byte-identical (`git diff --numstat` back to `9 4`).

### Item 1 — is the cookie actually deleted?

`AuthenticationController.cs:36` deletes unconditionally, before `mediator.Send` at `:38`, so success and every failure branch alike consume it.

The implementer's stated rationale — "`Cookies.Delete` copies the options, and a surviving `Max-Age` would beat the epoch `Expires`" (`07-coderabbit-rework.md:84`) — I checked empirically rather than by reading, since it is the kind of claim that is either load-bearing or wrong. Against a real `DefaultHttpContext` on this SDK:

```
DELETE with MaxAge=null  -> e3a_oauth_state=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/auth; secure; samesite=lax; httponly
DELETE with MaxAge=10min -> e3a_oauth_state=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/auth; secure; samesite=lax; httponly
APPEND                   -> e3a_oauth_state=abc; max-age=600; path=/api/auth; secure; samesite=lax; httponly
```

**The cookie is genuinely cleared** — epoch `Expires`, `Path` identical to the append, no `Max-Age`. So a captured state cannot be replayed in the same browser. The *reasoning* is wrong on this framework version (`ResponseCookies.Delete` drops `MaxAge` regardless), but the code is correct either way and `Generate(TimeSpan?)` with a null default is the safer shape. Non-blocking, listed below.

### Item 2 — `IgnoreQueryFilters()`

Present and correct: `api/E3A.Infrastructure/Identity/UserRepository.cs:13` — `_dbSet.IgnoreQueryFilters().AnyAsync(x => x.NormalizedUserName == normalizedUserName, ...)`. Without it the check could not see the soft-deleted row that still holds `UserNameIndex` (unique, filtered only on `NormalizedUserName IS NOT NULL`), which is the whole point. This is not a section 8.5 violation: 8.5 bans ad-hoc `!x.IsDeleted` predicates in queries; this is one named repository method deliberately reading what the database index reads, carrying its WHY comment at `:12`.

Normalization is consistent: the resolver asks with `.ToUpperInvariant()` (`UserNameResolver.cs:11,22`) and `User.CreateFromGitHub` writes `NormalizedUserName = userName.ToUpperInvariant()` (`api/E3A.Domain/Identity/User.cs:53`) — the same transform Identity's `UpperInvariantLookupNormalizer` applies. The resolver runs only on the create branch (`CompleteGitHubLoginHandler.cs:54-59`), `GitHubLogin` keeps the raw login (`User.cs:49`), and `SaveChangesAsync` remains a single call at `:66` on the success path.

**On the disclosed coverage gap — not blocking, and nothing else is wanted instead.** The implementer flagged that no executing test covers `UserRepository.cs:13` and that pinning it would have needed a new EF InMemory package. That is not a shortfall; it is the house rule. `conventions/dotnet-testing.md:156` puts "controllers, EF Core entity configurations, the `Repository<T>` base" explicitly out of scope, `SKILL.md:898` makes it a section 9 checklist line ("Repositories and controllers NOT tested"), and `docs/constitution.md` section 4 repeats it. Adding the package to pin one repository line would have violated the convention *and* the stated no-new-package constraint. What exists is what I would accept: the resolver's reaction to a `true`/`false` answer is pinned by `UserNameResolverTests.cs:96-120`, the wiring is pinned by `CompleteGitHubLoginHandlerTests.cs:48-59` (suffixed `UserName` on `AddAsync` plus `Received(1)` on `SaveChangesAsync`), and the query carries a WHY comment. The same reasoning covers the controller's cookie append/read/delete being untested.

### Item 3 — escaped pipes

`git diff .process/github-oauth/01-plan.md` is exactly two lines: `:129` and `:344`, each changing the bare pipe pair to an escaped one inside backticks. Zero words of approved content altered. RC1's requested rewrite of decision 7 at `01-plan.md:62` was **not** made — correct, the append-only ruling held.

### Scope containment

23 files modified, 6 created, and every one maps to item 1, 2 or 3. Independently confirmed on the diff, not on the report:

- **No `try`/`catch` added anywhere.** Grepping the added lines of `git diff -U0` for a `try {` or `catch (` returns empty. RC5 and RC8 were rejected on `docs/constitution.md:130`; reintroducing one would have been self-defeating. It was not.
- **RC11 held**: `User.UpdateGitHubProfile` (`api/E3A.Domain/Identity/User.cs:58-63`) still sets exactly `DisplayName` and `AvatarUrl`, per acceptance decision 5.
- **RC3 held**: `AppDbContext.cs` is not in the diff at all; the Options-driven `HasMaxLength` convention is untouched.
- **RC6 held**: `03-review-r2.md` is unmodified.
- **No Azure resource, no `az`, no new package, no migration**: `Directory.Packages.props`, every `.csproj`, `.slnx` and `Data/Migrations/` are all absent from `git status`.
- `IGenerator` was already DI-registered (`Core.Utilities/DependencyInjection.cs:10`) and is the established handler-injection pattern (`CreateEngineerHandler`, `UpdateEngineerHandler`), so the new constructor parameter resolves at runtime. Both new helper types are `static`, so nothing new needs registering.

Skill absolutes spot-checked on every touched file: file-scoped namespaces throughout; `sealed` on `OAuthState`, `GitHubLoginUrlResult` and both new test classes; `DateTimeOffset` only; `.ConfigureAwait(false)` on every new non-test `await` (`UserNameResolver.cs:11,22`, `UserRepository.cs:13`, `CompleteGitHubLoginHandler.cs:56`) and correctly *absent* in the controller per `SKILL.md:76` ("always on every await outside controllers"). `UserNameResolver` is a line-for-line mirror of the `SKILL.md:817-822` section 8.3 exemplar and of the merged `EngineerSlugResolver`, comment included.

### Docs sync

- `docs/architecture.md:28` — the false clause is gone. It now reads that the state "is bound to the initiating browser: `/api/auth/github/login` also sets a short-lived `Secure`, `HttpOnly`, `SameSite=Lax` cookie holding that nonce, and the callback rejects the state unless the cookie matches, then clears it. The cookie carries the nonce only — never the token — and the server keeps no state, so no cache and no extra Azure resource are needed." That matches `AuthenticationController.cs:24,34,36` and `OAuthStateProtector.cs:31,43` exactly, keeps the "never a cookie" guarantee for the **token** without ambiguity, and the no-Azure-resource claim is still true. No divergence.
- `docs/implementation-plan.md:63` — "a signed anti-CSRF `state` bound to the initiating browser by a `Secure`, `HttpOnly`, `SameSite=Lax` nonce cookie the callback compares and clears". Agrees with both the code and `architecture.md`. The `#error=<ERROR_CODE>` contract it promises still holds, and item 2 is what makes it hold on the collision path.

### Postman

`postman/e3a.postman_collection.json` — three auth requests, unchanged URLs, methods, query params and auth modes; no endpoint added or removed, so nothing stale and nothing orphaned. The two added `description` fields on **GitHub Login** and **GitHub Callback** are correct and, on my reading, more than cosmetic: the callback's contract *did* change — it now silently requires a cookie the login sets, and a collection that does not say so walks the next person into `#error=AUTHENTICATION_STATE_INVALID` with no explanation. This is review order 7's "changed contracts are reflected". Keep them.

### The replay test

`OAuthStateProtectorTests.Validate_ShouldReturnValid_WhenTheSameStateIsValidatedTwice` (`api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTests.cs:80-90`) now passes `state.Nonce` on both calls. The update is honest: it asserts only that the protector is deliberately stateless — the *same browser* presenting its *own* nonce twice still validates — which is a true statement about the component. It no longer implies the false property. And the false property is now explicitly falsified elsewhere: `OAuthStateProtectorNonceTests.cs:57-65` pins the cross-browser rejection, and my mutation run proves that test bites. Kept-not-deleted was the right call and the meaning was genuinely repaired, not relabelled.

### `Secure = true` unconditional

Judged: needs nothing beyond a note, and the note now exists in two places (`07-coderabbit-rework.md:122-126` and, in effect, `docs/architecture.md:28`, which states the attribute). The registered callback is `https://localhost:62935/...`, so HTTPS is the expected dev setup and the failure mode is a first-run misconfiguration, not a defect. I agree with declining a config switch: a flag that can turn `Secure` off is exactly the flag that survives into production. No change wanted.

## Non-blocking

- `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:24,26` — the nonce is carried **verbatim** as segment 0 of the state, so the value that binds the browser also travels in the authorize URL's query string: GitHub sees it, and it lands in browser history and any referrer or access log. This does not reopen item 1 (an attacker still cannot write the victim's cookie), but it means an attacker who obtains a victim's *in-flight* state value regains the login-CSRF. Hardening for a later slice: put `SHA-256(nonce)` in the state and keep the raw nonce only in the cookie. Same shape, same test surface, strictly less exposure.
- `api/E3A.Tests/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs:44` — `result.RedirectUrl.Should().NotContain("state-nonce")` passes only because the substitute returns the unrelated pair `("signed-state", "state-nonce")`. With the real protector the nonce **is** a substring of the state and therefore of the URL. The assertion documents a property that is false in production; the useful half of that test is line 43. Drop the line or reword the test.
- `.process/github-oauth/07-coderabbit-rework.md:84` — the deviation's rationale ("`Cookies.Delete` copies the passed `CookieOptions`, so a `MaxAge` left on it would emit `Max-Age` and an epoch `Expires`") does not hold on this framework version; `Delete` emits epoch `Expires` and no `Max-Age` either way, as probed above. Correct outcome, wrong reason. Worth correcting in the PR body so nobody later "simplifies" on the strength of it.
- `api/E3A.Tests/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` is now 147 lines and `OAuthStateProtectorTamperTests.cs` 124, against `SKILL.md:902` ("no file exceeds ~100 lines"). Both were already over before this rework and both passed two internal review rounds at that size; splitting them is a refactor outside the numbered findings, so declining was right. Fold into the next slice that touches these files.
- `api/E3A.Application/Authentication/Shared/UserNameResolver.cs:19-22` — the `do`/`while` has no iteration ceiling. Identical to the merged `EngineerSlugResolver.cs:20-24`, so this is house pattern, not drift; if a bound is ever wanted it belongs in both places at once.

## Test quality

Per class, on the question that matters — would it fail if the code were wrong?

- **`OAuthStateProtectorNonceTests`** (new, 6 cases) — the strongest work in this rework. `WhenNonceDoesNotMatch:38` and `WhenTheStateIsPresentedByAnotherBrowser:57` both go red when the comparison is deleted; I ran it. `WhenTheStateIsPresentedByAnotherBrowser` is the one that states the security property in the language of the threat, and its second assertion (`:64`, first browser still `Valid`) stops the naive "always return Invalid" mutation from passing. `WhenNonceIsMissing:28` is a `[Theory]` over null, empty and whitespace and constrains the `:31` guard. Real tests against a real protector — no substitute is being asked to confirm its own stub.
- **`UserNameResolverTests`** (new, 4 cases) — constrains the implementation. `ShouldRetry:109` is the one with teeth: three distinct `IsUserNameExistsAsync` answers plus `_generator.Received(2)` pins the loop rather than a single-shot suffix. `ShouldReturnTheGitHubLogin:86` adds `_generator.DidNotReceive()`, which is what stops an "always suffix" implementation from passing. `ShouldAskForTheNormalizedUserName:123` pins the `ToUpperInvariant()` at the call boundary — the detail that makes the lookup line up with what the DB index stores.
- **`CompleteGitHubLoginHandlerTests`** — `Handle_ShouldCreateTheUserWithASuffixedUserName:48` is a genuine wiring test: it asserts `UserName == "octocat-ab12"` **and** `GitHubLogin == profile.Login` in the same `Arg.Is`, so it fails both if the resolver is bypassed and if the raw login is overwritten. `Received(1)` on `SaveChangesAsync` is present.
- **`CompleteGitHubLoginHandlerFailureTests`** — `Handle_ShouldRedirectWithStateInvalid_WhenTheBrowserNonceIsMissing:60` is the **weakest of the new tests**, and I want it on the record: it configures the substitute to return `Invalid` for a whitespace nonce and then asserts the mapping. It is not worthless — it does fail if the handler forwards anything other than `request.Nonce` (e.g. `request.State`), because the conditional setup would stop matching and the constructor's `Valid` default would take over — and its `DidNotReceive().ExchangeCodeForAccessTokenAsync` assertion is real, pinning the guard above the exchange. But the actual security property lives one layer down in `OAuthStateProtectorNonceTests`, and that is the right division. No change asked.
- **`GetGitHubLoginUrlQueryHandlerTests`** — `Handle_ShouldSurfaceTheNonceForTheBrowserCookie:39`: line 43 constrains the plumbing; line 44 does not (see Non-blocking).
- **`UserTests` / `UserFactory`** — the renamed test at `UserTests.cs:22` now asserts the three-way split (`GitHubLogin` raw, `UserName` resolved, `NormalizedUserName` upper-cased) with `login` and `userName` deliberately different, which is exactly the invariant item 2 introduced. `UserFactory.GitHub`'s new `userName = null` default preserves every existing call site's meaning.
- **Regression churn** (`...ReturningUserTests`, `...TamperTests`, and the call-site edits in the failure tests) — mechanical signature updates only; no assertion was weakened to `Arg.Any` where it was previously specific.

## Bottom line

The Critical login-CSRF hole two internal rounds missed is genuinely closed, and I proved the test that guards it bites rather than trusting the assertion. The cookie really is cleared. The `UserNameIndex` collision is fixed with the repo's own section 8.3 pattern, and the `IgnoreQueryFilters()` that makes it work is present. Scope held to the three items, all six rejections held, no handler `try`/`catch`, no package, no migration, no Azure resource, and both owning docs moved with the code. Ready to merge.
