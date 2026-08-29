# Plan — GitHub OAuth Login for Creators

## Goal

After this ships, a visitor can hit `GET /api/auth/github/login`, authorise the e3a GitHub App,
and land back on the web app at `WebRedirectUrl#token=<e3a JWT>`. That JWT is the same
`Core.Identity` HS256 token every existing `[Authorize]` endpoint already validates, so the
creator can immediately call `POST /api/engineers`, upload a draft, publish, and poll
`GET /api/publish/{versionId}/status` — all of which today have no way to obtain a token at all.
A first-time visitor gets a `User` row created just-in-time from their GitHub profile; a returning
visitor gets their display name and avatar refreshed. `GET /api/auth/me` returns the authenticated
creator's profile. Browsing and installing remain fully anonymous.

## Scope

**In**
1. `User` gains `GitHubId` (unique, filtered index), `GitHubLogin`, `DisplayName`, `AvatarUrl`, plus a
   `CreateFromGitHub` factory and an `UpdateGitHubProfile` domain method.
2. EF migration `oauth004` (4 columns + 1 filtered unique index on `AspNetUsers`).
3. `GET /api/auth/github/login` → 302 to GitHub's authorize URL with `client_id`, `redirect_uri`,
   `scope`, `state`.
4. `GET /api/auth/github/callback` → validate `state`, exchange `code` server-side, read the profile,
   create-or-update the user, issue the e3a JWT, 302 to `WebRedirectUrl` with the token in the fragment.
5. `GET /api/auth/me` → the authenticated creator's profile.
6. `GitHubAuthenticationOptions` bound to the existing `GitHubAuthentication` section.
7. `IGitHubOAuthClient` (Application contract) + typed-`HttpClient` implementation (Infrastructure).
8. `IOAuthStateProtector` — stateless, signed, expiring anti-CSRF `state`.
9. Six error codes with EN + AR resx entries.
10. Postman: new `Authentication` folder with three requests.
11. Docs sync: `docs/architecture.md` + `docs/implementation-plan.md` record the auth contract.
12. Tests per `conventions/dotnet-testing.md` — every branch enumerated in §Test plan.

**Out**
- Refresh tokens, logout, token revocation (`jti` deliberately not emitted).
- Linking/unlinking a GitHub account to an existing e3a user.
- Roles, policies, `DefaultCodes` — none exists in this repo and none is introduced.
- `IsBlocked` (acceptance decision 8).
- The engineer slug on first login (acceptance decision 7).
- E-mail / `user:email` scope (acceptance decision 6).
- Frontend sign-in surfaces (feature 4 of this run).
- Any change to `ICurrentUserService`, `CurrentUserService`, `ITokenService`, or `JwtTokenService`.
- Migrating seeded engineers' owner rows (dev answer 4).

**Deferred** (with why)

| Item | Why deferred |
|------|--------------|
| Binding `state` to the browser (short-lived `SameSite=Lax` nonce cookie set on `/login`, compared on callback) | Closes the residual login-CSRF hole a purely stateless `state` cannot close. Acceptance decision 2 fixed the mechanism as stateless-signed-expiring; changing it is the dev's call, not the implementer's. Disclosed in §The `state` parameter. |
| GitHub login rename sync (`GitHubLogin` / `UserName` refresh) | Decision 5 authorises refreshing display name and avatar only. `UserName` carries publish attribution and has a unique Identity index; rewriting it is a separate, riskier slice. |
| Single-use `state` (replay elimination) | Needs a server-side or distributed store. `Core.Cache` is an empty placeholder and a distributed cache is an Azure resource — forbidden this run. |
| Client-secret rotation before the repo goes public | Carried debt from the acceptance (decision 3); not a code change. |

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Token issuance | Inject `Core.Identity.Tokens.AccessToken.ITokenService` and call `GenerateTokenAsync(List<Claim>)` | Acceptance decision 1. Registered already by `AddCoreIdentity` in `Program.cs`; the JWT bearer validation parameters in `Core.Identity/DependencyInjection.cs` already accept exactly this token. |
| 2 | Which claim carries the user id | `CurrentUserService.Constants.UserIdClaimType` (= `ClaimTypes.NameIdentifier`), value `user.Id.ToString()` | `CurrentUserService.UserId` does `Guid.TryParse(User?.FindFirst(Constants.UserIdClaimType)?.Value, …)`. `GetPublishStatusQueryHandler`, `ListMyEngineersQueryHandler`, `CreateEngineerHandler` and every other engineer handler read `currentUserService.UserId` and compare it to `Engineer.OwnerUserId`. Emitting the constant (not a re-typed `ClaimTypes` literal) binds emitter to reader. |
| 3 | Role claim | **Not emitted.** No `ClaimTypes.Role` claim in the token | Every e3a endpoint uses bare `[Authorize]`. Emitting `RoleNames.User` would silently open `MapCoreUserNotificationEndpoints()` (`RequireRole(RoleNames.User)`) to every GitHub visitor — that is "roles/permissions beyond what already exists", explicitly out of scope. |
| 4 | `state` construction | `{nonce}.{expiresAtUnixSeconds}.{Base64Url(HMACSHA256(CoreJwt:Key, "{nonce}.{expiresAtUnixSeconds}"))}` | Acceptance decision 2: stateless, signed, expiry carried inside. Zero new config keys, zero new resources. |
| 5 | Signing key for `state` | `JwtOptions.Key` (`CoreJwt:Key`) via `IOptions<JwtOptions>` | Already a server-only secret, already registered by `AddCoreIdentity`, never leaves the process. Signing with the GitHub `ClientSecret` would spread that secret across a second code path for no benefit. |
| 6 | Order of `state` checks | Signature verified **before** expiry | A tampered expiry must be reported as `INVALID`, not `EXPIRED`; never branch on unauthenticated data. |
| 7 | `state` replay | Accepted, disclosed, tested | Statelessness means a `state` is reusable inside its window. Inert in practice: it is worthless without a matching unused GitHub `code`, which GitHub makes single-use and short-lived. `StateExpirationMinutes` (10, already configured) bounds the window. A named test documents the behaviour so nobody "fixes" it by accident. |
| 8 | Where the state logic lives | `E3A.Application/Authentication/Shared/OAuthStateProtector.cs` (interface + implementation), registered in `AddApplication` | Pure crypto with no external IO — same placement rationale as `PluginBuilder`/`MarketplaceGenerator` living in `Application/Publishing/Shared` per `docs/architecture.md`. Directly unit-testable; `E3A.Tests` references Application but **not** Infrastructure. |
| 9 | Naming of the state type | `IOAuthStateProtector` / `OAuthStateProtector` with `Create()` and `Validate(state)` | `Service` (use-case logic), `Manager`, `Helper`, `Utils` are prohibited names. `Protector` mirrors the in-repo `RefreshTokenProtector` already used by `JwtTokenService`. |
| 10 | Callback failure shape | Handler returns an `AuthenticationRedirectResult`; every failure branch is a 302 to `WebRedirectUrl#error=<ERROR_CODE>`. No exceptions thrown, no validator registered | Acceptance decision 10. A thrown `*CoreException` would be turned into a JSON body by `CoreExceptionMiddleware`, and a FluentValidation rule would 422 through `ValidationBehaviour` — both dead ends for a browser mid-redirect. This is the one place the skill's "never validate manually in a handler" deliberately does not apply, because these are branches, not validation failures. |
| 11 | "User creation failure" branch | = GitHub returned a profile with a non-positive `id` or a blank `login` → `#error=GITHUB_PROFILE_INVALID`. A database failure on `SaveChangesAsync` stays a 500 through `CoreExceptionMiddleware` | Handlers carry no `try`/`catch` anywhere in this solution, and inventing one for `DbUpdateException` would be a new pattern. The reachable, testable creation failure is a bad payload. |
| 12 | Redirect target | Always `GitHubAuthenticationOptions.WebRedirectUrl`; the authorize target is always `AuthorizationUrl` + `CallbackUrl`, all from configuration | Acceptance decision 9. The callback action binds exactly two query values, `code` and `state`, neither of which reaches a URL-building call. No `returnUrl` parameter exists anywhere in the slice. |
| 13 | User matching | `FirstOrDefaultAsync(x => x.GitHubId == profile.Id, …)` | Acceptance decision 4. A test asserts the compiled predicate matches a stored user whose *login has changed*, so matching-by-login cannot pass. |
| 14 | Profile refresh | `UpdateGitHubProfile(displayName, avatarUrl)` only | Acceptance decision 5, applied literally. `UserName`/`NormalizedUserName` carry a unique Identity index and publish attribution (`ProcessPublishJobHandler` reads `user.UserName`); rewriting them on every login adds an index-collision failure mode for no in-scope benefit. `GitHubLogin` therefore stays equal to `UserName` for the life of the row — no two-fields-disagree state. |
| 15 | `GitHubId` nullability | `long?`, unique index filtered `[GitHubId] IS NOT NULL AND [IsDeleted] = 0` | Pre-existing/seeded user rows (dev answer 4 keeps them) have no GitHub identity. A non-nullable `0` sentinel would collide on the unique index the moment there are two of them. The partial-index filter stays with the index per skill §8.5. |
| 16 | Where the new caps live | Inside `GitHubAuthenticationOptions` (existing `GitHubAuthentication` section), each with a class default | Skill §8.1 forbids entity constants. A brand-new section would bind empty on the dev's machine and on CI (constitution §2: appsettings is git-ignored, fresh clones have no defaults) — `HasMaxLength(0)` would break the migration. Class defaults mirror `JwtOptions` (`ExpirationHours = 72`). |
| 17 | Outbound HTTP | Typed client: `services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>(…)` in `AddInfrastructure`, `HttpClient` injected by primary constructor | Core-first: never `new HttpClient()`. Handlers depend on `IGitHubOAuthClient` only, so every callback branch is substitutable in tests. |
| 18 | New NuGet packages | **None.** `AddHttpClient`, `QueryHelpers` and `ILogger<T>` all come from the `Microsoft.AspNetCore.App` shared framework, reaching Application/Infrastructure transitively via `Core.Validation`'s `FrameworkReference` | Central package management would otherwise need a new `PackageVersion` entry — avoidable churn. Fallback if resolution fails: add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `E3A.Infrastructure.csproj`; do **not** add a `PackageReference`. |
| 19 | Client failure semantics | `IGitHubOAuthClient` returns `null` on any non-2xx, GitHub `error` payload, malformed JSON, transport failure, or timeout. The `try`/`catch` lives in the Infrastructure adapter, never in a handler | Without it, a `TaskCanceledException` from the 10s timeout would escape as a JSON 500, breaking acceptance decision 10. |
| 20 | Controller name vs route | Class `AuthenticationController` in `Controllers/Authentication/`, route `api/auth` | The route is fixed by the registered GitHub App callback (`…/api/auth/github/callback`, dev answer 1). Constitution §3 forbids abbreviated identifiers, so the *class* spells it out. |
| 21 | Authorization attributes | Class-level `[Authorize]`; `[AllowAnonymous]` on `github/login` and `github/callback` | Mirrors `EngineersController` exactly (`[Authorize]` on the class, `[AllowAnonymous]` on `GetEngineer`). No `DefaultCodes` class exists in this repo; none is created. |
| 22 | Validators | **None in this slice.** No `*Validator.cs` files | `GetGitHubLoginUrlQuery` and `GetCurrentUserQuery` have no input. `CompleteGitHubLoginCommand`'s inputs are handled as redirect branches (decision 10). The reviewer should read the absence as deliberate, not missed. |
| 23 | `scope` value | `GitHubAuthenticationOptions.Scope`, class default `"read:user"`, always appended | Acceptance in-scope item 2 requires `scope` on the authorize URL; decision 6 forbids `user:email`. `read:user` reads the public profile only. Unconditional append keeps the generator branch-free. |
| 24 | Postman auth for the two redirect requests | Item-level `"auth": {"type":"noauth"}` and `"protocolProfileBehavior": {"followRedirects": false}` | The collection-level bearer would otherwise be sent to an anonymous endpoint, and following redirects would send the tester to github.com instead of showing the `Location` header being tested. |

## Existing code touched

| File | Change |
|------|--------|
| `api/E3A.Domain/Identity/User.cs` | Add 4 properties (`long? GitHubId`, `string? GitHubLogin`, `string? DisplayName`, `string? AvatarUrl`, all `{ get; private set; }`), add `static User CreateFromGitHub(...)`, add `void UpdateGitHubProfile(...)`. Leave `User()`, `User(Guid)`, `Create(Guid?)`, `MarkDeleted()` untouched. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Add an `// Authentication` group with 6 constants (see §Error codes). Touch nothing else. |
| `api/E3A.Application/DependencyInjection.cs` | Add `services.Configure<GitHubAuthenticationOptions>(...)` after the `PublishingOptions` line and `services.AddScoped<IOAuthStateProtector, OAuthStateProtector>();` before `return services;`. |
| `api/E3A.Infrastructure/DependencyInjection.cs` | Add the typed `AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>` registration (body below). |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | Add `IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions` as the 4th primary-constructor parameter; call `ConfigureUsers(modelBuilder);` first in `OnModelCreating` after `base.OnModelCreating`; add the private `ConfigureUsers` method. The `User` line already in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` stays as is. |
| `api/E3A.Api/Resources/Messages.en.resx` | Add 6 `<data>` entries before `</root>`. |
| `api/E3A.Api/Resources/Messages.ar.resx` | Add the same 6 keys, Arabic values, no tashkeel. |
| `postman/e3a.postman_collection.json` | Insert an `Authentication` folder as the **first** element of `item`, containing 3 requests. |
| `docs/architecture.md` | Add one bullet under `## Principles` recording the auth contract (§Docs sync). |
| `docs/implementation-plan.md` | Replace the `Auth:` clause of the `## API surface (/api/*)` paragraph (§Docs sync). |

Generated, not hand-written: `api/E3A.Infrastructure/Data/Migrations/*_oauth004.cs` and
`*_oauth004.Designer.cs` plus the updated `AppDbContextModelSnapshot.cs`, produced by
`dotnet ef migrations add oauth004 --project api/E3A.Infrastructure --startup-project api/E3A.Api`.
The migration must add exactly: `GitHubId bigint NULL`, `GitHubLogin nvarchar(100) NULL`,
`DisplayName nvarchar(200) NULL`, `AvatarUrl nvarchar(500) NULL` on `AspNetUsers`, and
`IX_AspNetUsers_GitHubId UNIQUE … WHERE [GitHubId] IS NOT NULL AND [IsDeleted] = 0`.
If it contains anything else, the model was changed by accident — fix the model, not the migration.

## Files to create

Namespaces are exact. Every type is `sealed` unless stated. File-scoped namespaces, no comments except
the two WHY comments called out below, `.ConfigureAwait(false)` on every non-test await,
`DateTimeOffset` only, braces on every `if`, one-line type declarations, block-bodied methods.

| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/E3A.Application/Options/GitHubAuthenticationOptions.cs` | `sealed class GitHubAuthenticationOptions` · ns `E3A.Application.Options` | `public const string SectionName = "GitHubAuthentication";` then, all `{ get; set; }`: `string AppId = string.Empty`, `string ClientId = string.Empty`, `string ClientSecret = string.Empty`, `string AuthorizationUrl = string.Empty`, `string AccessTokenUrl = string.Empty`, `string UserProfileUrl = string.Empty`, `string CallbackUrl = string.Empty`, `string WebRedirectUrl = string.Empty`, `int StateExpirationMinutes = 10`, `string Scope = "read:user"`, `string UserAgent = "e3a"`, `int HttpTimeoutSeconds = 10`, `int StateNonceSize = 16`, `int GitHubLoginMaxLength = 100`, `int DisplayNameMaxLength = 200`, `int AvatarUrlMaxLength = 500`. |
| 2 | `api/E3A.Application/Authentication/Shared/OAuthStateStatus.cs` | `enum OAuthStateStatus` · ns `E3A.Application.Authentication.Shared` | `{ Valid, Invalid, Expired }`. No extension methods needed, so none is written. |
| 3 | `api/E3A.Application/Authentication/Shared/IOAuthStateProtector.cs` | `interface IOAuthStateProtector` · same ns | `string Create();` and `OAuthStateStatus Validate(string? state);` |
| 4 | `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs` | `sealed class OAuthStateProtector(IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions, IOptions<JwtOptions> jwtOptions, IGenerator generator) : IOAuthStateProtector` | Exact bodies in §The `state` parameter. Two private consts: `private const char PayloadSeparator = '.';` with the WHY comment *"neither the nanoid alphabet nor Base64Url contains '.', so a segment can never swallow the separator"*, and `private const int ExpectedSegmentCount = 3;`. |
| 5 | `api/E3A.Application/Authentication/Shared/GitHubProfile.cs` | `sealed record GitHubProfile(long Id, string Login, string? Name, string? AvatarUrl)` · same ns | The Application-facing shape of GitHub's `/user` payload. Not a CQRS output, so no `Result` suffix. |
| 6 | `api/E3A.Application/Authentication/Shared/IGitHubOAuthClient.cs` | `interface IGitHubOAuthClient` · same ns | `Task<string?> ExchangeCodeForAccessTokenAsync(string code, CancellationToken cancellationToken);` · `Task<GitHubProfile?> GetProfileAsync(string accessToken, CancellationToken cancellationToken);` Contract: **returns null, never throws**, for non-2xx, a GitHub `error` payload, malformed JSON, transport failure, or timeout. |
| 7 | `api/E3A.Application/Authentication/Shared/GitHubAuthorizationUrlGenerator.cs` | `static class GitHubAuthorizationUrlGenerator` · same ns | `public static string Generate(GitHubAuthenticationOptions options, string state)` → builds `Dictionary<string, string?> { ["client_id"] = options.ClientId, ["redirect_uri"] = options.CallbackUrl, ["scope"] = options.Scope, ["state"] = state }` and returns `QueryHelpers.AddQueryString(options.AuthorizationUrl, parameters)` (`using Microsoft.AspNetCore.WebUtilities;`). No parameter of this method may come from an HTTP request. |
| 8 | `api/E3A.Application/Authentication/Shared/AuthenticationRedirectUrlGenerator.cs` | `static class AuthenticationRedirectUrlGenerator` · same ns | `private const string TokenFragmentKey = "token";` `private const string ErrorFragmentKey = "error";` · `public static string Success(string webRedirectUrl, string token)` returns `$"{webRedirectUrl}#{TokenFragmentKey}={Uri.EscapeDataString(token)}"` · `public static string Failure(string webRedirectUrl, string errorCode)` returns `$"{webRedirectUrl}#{ErrorFragmentKey}={Uri.EscapeDataString(errorCode)}"`. |
| 9 | `api/E3A.Application/Authentication/Shared/UserClaimsGenerator.cs` | `static class UserClaimsGenerator` · same ns | `public const string GitHubLoginType = "GitHub";` (WHY comment: *"the only login path e3a has; surfaced downstream as ICurrentUserService.LoginType"*). `public static List<Claim> Generate(User user)` returns exactly the four claims in §API surface, in that order, using `CurrentUserService.Constants.*` for every claim type. |
| 10 | `api/E3A.Application/Authentication/Shared/AuthenticationRedirectResult.cs` | `sealed record AuthenticationRedirectResult(string RedirectUrl)` · same ns | Shared by both redirect use cases. Client-facing; no `LocalizedText` exists anywhere in this slice, so no `.Localized()` call appears. |
| 11 | `api/E3A.Application/Authentication/Shared/CurrentUserResult.cs` | `sealed record CurrentUserResult(Guid Id, long? GitHubId, string? GitHubLogin, string? DisplayName, string? AvatarUrl, DateTimeOffset CreatedAt)` · same ns | Client-facing. Mapped inline in the handler — the mapping is a flat copy, so no `ResultGenerator` (skill §5.8: generators are for non-trivial mapping). |
| 12 | `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQuery.cs` | `sealed record GetGitHubLoginUrlQuery : IRequest<AuthenticationRedirectResult>;` · ns `E3A.Application.Authentication.GetGitHubLoginUrl` | No properties. |
| 13 | `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandler.cs` | `sealed class GetGitHubLoginUrlQueryHandler(IOAuthStateProtector oAuthStateProtector, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : IRequestHandler<GetGitHubLoginUrlQuery, AuthenticationRedirectResult>` | `Handle`: (1) `var state = oAuthStateProtector.Create();` (2) `var authorizationUrl = GitHubAuthorizationUrlGenerator.Generate(gitHubAuthenticationOptions.Value, state);` (3) `return Task.FromResult(new AuthenticationRedirectResult(authorizationUrl));`. Not `async` — there is nothing to await. |
| 14 | `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginCommand.cs` | `sealed record CompleteGitHubLoginCommand(string? Code, string? State) : IRequest<AuthenticationRedirectResult>;` · ns `E3A.Application.Authentication.CompleteGitHubLogin` | Both nullable — absence is a redirect branch, not a validation error. **Not** `IAuditableCommand`. No validator file. |
| 15 | `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs` | `sealed class CompleteGitHubLoginHandler(IGitHubOAuthClient gitHubOAuthClient, IOAuthStateProtector oAuthStateProtector, IUserRepository userRepository, ITokenService tokenService, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : IRequestHandler<CompleteGitHubLoginCommand, AuthenticationRedirectResult>` | Ordered steps and failure branches in §Callback sequence. Private helper `private AuthenticationRedirectResult Failure(string errorCode)` returning `new AuthenticationRedirectResult(AuthenticationRedirectUrlGenerator.Failure(gitHubAuthenticationOptions.Value.WebRedirectUrl, errorCode))`. Exactly one `SaveChangesAsync`, on the success path only. |
| 16 | `api/E3A.Application/Authentication/GetCurrentUser/GetCurrentUserQuery.cs` | `sealed record GetCurrentUserQuery : IRequest<CurrentUserResult>;` · ns `E3A.Application.Authentication.GetCurrentUser` | No properties. |
| 17 | `api/E3A.Application/Authentication/GetCurrentUser/GetCurrentUserQueryHandler.cs` | `sealed class GetCurrentUserQueryHandler(IUserRepository userRepository, ICurrentUserService currentUserService) : IRequestHandler<GetCurrentUserQuery, CurrentUserResult>` | (1) `var userId = currentUserService.UserId;` (2) `if (userId == null \|\| userId == Guid.Empty) { throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated); }` — guard first, mirroring `ListMyEngineersQueryHandler` (3) `var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken, asNoTracking: true).ConfigureAwait(false);` (4) `if (user == null) { throw new NotFoundCoreException(ErrorCodes.UserNotFound); }` (5) return `new CurrentUserResult(user.Id, user.GitHubId, user.GitHubLogin, user.DisplayName, user.AvatarUrl, user.CreationDate)`. |
| 18 | `api/E3A.Infrastructure/Authentication/GitHubOAuthClient.cs` | `sealed class GitHubOAuthClient(HttpClient httpClient, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions, ILogger<GitHubOAuthClient> logger) : IGitHubOAuthClient` · ns `E3A.Infrastructure.Authentication` | Exact bodies in §Outbound HTTP to GitHub. Two private consts: `private const string GitHubJsonMediaType = "application/vnd.github+json";` and `private const string BearerScheme = "Bearer";`. |
| 19 | `api/E3A.Infrastructure/Authentication/GitHubAccessTokenPayload.cs` | `sealed record GitHubAccessTokenPayload([property: JsonPropertyName("access_token")] string? AccessToken, [property: JsonPropertyName("error")] string? Error);` · same ns | GitHub returns HTTP 200 with an `error` field on a bad code — the payload must be inspected, not just the status. |
| 20 | `api/E3A.Infrastructure/Authentication/GitHubProfilePayload.cs` | `sealed record GitHubProfilePayload([property: JsonPropertyName("id")] long Id, [property: JsonPropertyName("login")] string? Login, [property: JsonPropertyName("name")] string? Name, [property: JsonPropertyName("avatar_url")] string? AvatarUrl);` · same ns | `Login` nullable here so a hostile or truncated payload cannot produce a nullable-reference warning under `TreatWarningsAsErrors`. |
| 21 | `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs` | `[ApiController] [Route("api/auth")] [Authorize] public class AuthenticationController(IMediator mediator) : ControllerBase` · ns `E3A.Api.Controllers.Authentication` | Three actions exactly as in §API surface. No `Requests.cs` — the callback binds two `[FromQuery]` scalars, so there is no request record. |

DI registration bodies (copy verbatim):

`AddApplication` — after the `PublishingOptions` line:

```csharp
services.Configure<GitHubAuthenticationOptions>(configuration.GetSection(GitHubAuthenticationOptions.SectionName));
```

and before `return services;`:

```csharp
services.AddScoped<IOAuthStateProtector, OAuthStateProtector>();
```

`AddInfrastructure` — after the `IUserRepository` line:

```csharp
services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<GitHubAuthenticationOptions>>().Value;
    httpClient.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});
```

`AppDbContext.ConfigureUsers`:

```csharp
private void ConfigureUsers(ModelBuilder modelBuilder)
{
    var gitHubAuthenticationSchema = gitHubAuthenticationOptions.Value;

    modelBuilder.Entity<User>(builder =>
    {
        builder.Property(x => x.GitHubLogin).HasMaxLength(gitHubAuthenticationSchema.GitHubLoginMaxLength);
        builder.Property(x => x.DisplayName).HasMaxLength(gitHubAuthenticationSchema.DisplayNameMaxLength);
        builder.Property(x => x.AvatarUrl).HasMaxLength(gitHubAuthenticationSchema.AvatarUrlMaxLength);
        builder.HasIndex(x => x.GitHubId).IsUnique().HasFilter("[GitHubId] IS NOT NULL AND [IsDeleted] = 0");
    });
}
```

`Program.cs`: **no change.** `ITokenService`, `ICurrentUserService` and `IOptions<JwtOptions>` come from
`AddCoreIdentity`; `IGenerator` from `AddCoreUtilities`; CORS already allows `localhost:5173/5174`, and
the callback is a top-level navigation rather than an XHR, so there is no CORS work.

## Error codes

| Constant | Value | Thrown / emitted by | Exception type | HTTP |
|----------|-------|---------------------|----------------|------|
| `AuthenticationCodeMissing` | `AUTHENTICATION_CODE_MISSING` | `CompleteGitHubLoginHandler` step 1 | none — fragment `#error=` | 302 |
| `AuthenticationStateInvalid` | `AUTHENTICATION_STATE_INVALID` | `CompleteGitHubLoginHandler` step 2 (`OAuthStateStatus.Invalid`) | none — fragment `#error=` | 302 |
| `AuthenticationStateExpired` | `AUTHENTICATION_STATE_EXPIRED` | `CompleteGitHubLoginHandler` step 2 (`OAuthStateStatus.Expired`) | none — fragment `#error=` | 302 |
| `GitHubTokenExchangeFailed` | `GITHUB_TOKEN_EXCHANGE_FAILED` | `CompleteGitHubLoginHandler` step 3 | none — fragment `#error=` | 302 |
| `GitHubProfileFetchFailed` | `GITHUB_PROFILE_FETCH_FAILED` | `CompleteGitHubLoginHandler` step 4 | none — fragment `#error=` | 302 |
| `GitHubProfileInvalid` | `GITHUB_PROFILE_INVALID` | `CompleteGitHubLoginHandler` step 5 | none — fragment `#error=` | 302 |
| `UserNotAuthenticated` *(already exists)* | `USER_NOT_AUTHENTICATED` | `GetCurrentUserQueryHandler` step 2 | `UnauthorizedCoreException` | 401 |
| `UserNotFound` *(already exists)* | `USER_NOT_FOUND` | `GetCurrentUserQueryHandler` step 4 | `NotFoundCoreException` | 404 |

Resource strings — add all six to **both** files (the two existing codes already have entries):

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|-----|--------------------|--------------------|
| `AUTHENTICATION_CODE_MISSING` | GitHub did not send an authorization code. | لم ترسل جيت هب كود التفويض. |
| `AUTHENTICATION_STATE_INVALID` | The sign-in request could not be verified. Please try again. | تعذر التحقق من طلب تسجيل الدخول. برجاء المحاولة مرة اخرى. |
| `AUTHENTICATION_STATE_EXPIRED` | The sign-in request has expired. Please try again. | انتهت صلاحية طلب تسجيل الدخول. برجاء المحاولة مرة اخرى. |
| `GITHUB_TOKEN_EXCHANGE_FAILED` | We could not complete the sign-in with GitHub. | تعذر اكمال تسجيل الدخول عن طريق جيت هب. |
| `GITHUB_PROFILE_FETCH_FAILED` | We could not read your GitHub profile. | تعذر قراءة ملفك الشخصي على جيت هب. |
| `GITHUB_PROFILE_INVALID` | Your GitHub profile is missing the details we need. | ملفك الشخصي على جيت هب ينقصه البيانات المطلوبة. |

None of these carries a runtime placeholder, so there is nothing to keep intact across the two languages.

## Domain behaviour

`api/E3A.Domain/Identity/User.cs` — new members, exact bodies:

```csharp
public long? GitHubId { get; private set; }
public string? GitHubLogin { get; private set; }
public string? DisplayName { get; private set; }
public string? AvatarUrl { get; private set; }

public static User CreateFromGitHub(long gitHubId, string gitHubLogin, string? displayName, string? avatarUrl)
{
    var id = Guid.NewGuid();

    return new User(id)
    {
        Id = id,
        GitHubId = gitHubId,
        GitHubLogin = gitHubLogin,
        DisplayName = displayName,
        AvatarUrl = avatarUrl,
        UserName = gitHubLogin,
        NormalizedUserName = gitHubLogin.ToUpperInvariant(),
        SecurityStamp = Guid.NewGuid().ToString(),
    };
}

public void UpdateGitHubProfile(string? displayName, string? avatarUrl)
{
    DisplayName = displayName;
    AvatarUrl = avatarUrl;
    UpdationDate = DateTimeOffset.UtcNow;
}
```

Invariants, and why each line is there:

- The existing `private User(Guid id)` already stamps `CreationDate` and `UpdationDate`; the factory does
  not repeat that.
- `NormalizedUserName` **must** be set: the default Identity model puts a unique `UserNameIndex` on it and
  SQL Server allows only one NULL in a unique index — a second GitHub user would otherwise fail to insert.
- `SecurityStamp` **must** be set: `JwtTokenService.GenerateTokenAsync(refreshToken, claims)` calls
  `SignInManager.ValidateSecurityStampAsync`, which treats a null stamp as invalid.
- `UpdateGitHubProfile` sets `UpdationDate` (skill §4.2: guard, mutate, stamp). There is no guard because
  there is no illegal profile refresh — **no** `BusinessRuleViolationException` is introduced anywhere in
  this slice.
- `GitHubId`, `GitHubLogin`, `UserName`, `NormalizedUserName` are write-once by construction: no method
  mutates them (decision 14).
- `User` is an `IdentityUser<Guid>` + `IAuditEntity`, **not** a `Core.DDD.Entity`, so it has
  `MarkDeleted()` rather than `SoftDelete()` and raises no domain events. Do not change that.

## API surface

| Method | Route | Authorization | Request | Response |
|--------|-------|---------------|---------|----------|
| GET | `/api/auth/github/login` | `[AllowAnonymous]` | none | `302` with `Location:` the GitHub authorize URL (`Redirect(result.RedirectUrl)`) |
| GET | `/api/auth/github/callback` | `[AllowAnonymous]` | `[FromQuery] string? code`, `[FromQuery] string? state` | `302` with `Location:` `{WebRedirectUrl}#token=…` or `{WebRedirectUrl}#error=CODE` |
| GET | `/api/auth/me` | class-level `[Authorize]` | none | `200` `CurrentUserResult`; `401 USER_NOT_AUTHENTICATED`; `404 USER_NOT_FOUND` |

No policy constant is used or created (decision 21). Every action takes
`CancellationToken cancellationToken` and passes it to `mediator.Send`. Controller bodies are two lines
each: send, return.

**JWT claims** (order as emitted by `UserClaimsGenerator.Generate`):

| # | Claim type (constant used) | Underlying type | Value | Read back by |
|---|-----------------------|--------------|-------|--------------|
| 1 | `CurrentUserService.Constants.UserIdClaimType` | `ClaimTypes.NameIdentifier` | `user.Id.ToString()` | `ICurrentUserService.UserId` → `GetPublishStatusQueryHandler`, `ListMyEngineersQueryHandler`, `CreateEngineerHandler`, `UpdateEngineerHandler`, `PublishEngineerHandler`, `UploadEngineerDraftHandler`, `Unlist`/`Relist`/`DeleteEngineerHandler`, `GetEngineerQueryHandler`, `GetImportManifestQueryHandler`, `CheckSlugAvailabilityQueryHandler`, and the new `GetCurrentUserQueryHandler` |
| 2 | `CurrentUserService.Constants.UserNameClaimType` | `ClaimTypes.Name` | `user.UserName ?? string.Empty` | `ICurrentUserService.UserName` |
| 3 | `CurrentUserService.Constants.LoginTypeClaimType` | `"login_type"` | `UserClaimsGenerator.GitHubLoginType` (`"GitHub"`) | `ICurrentUserService.LoginType` |
| 4 | `CurrentUserService.Constants.CreatedAtUnixTimeSecondsClaimType` | `"created_at_unix_seconds"` | `user.CreationDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)` | `ICurrentUserService.CreatedAtUnixTimeSeconds` |

`ICurrentUserService.NationalId` and `.PhoneNumber` return null for e3a users — correct, nothing reads
them. `issuer`, `audience` and `exp` are added by `JwtTokenService` from `CoreJwt`; the handler does not
set them. No role claim (decision 3). No `jti` — nothing consumes it and revocation is out of scope.

## The `state` parameter

**Construction** (`OAuthStateProtector.Create`):

1. `var options = gitHubAuthenticationOptions.Value;`
2. `var nonce = generator.Generate(options.StateNonceSize);` — **positional argument**. `IGenerator` has
   two `Generate` overloads and the named form `Generate(size: …)` is ambiguous; a positional `int` binds
   unambiguously to `Generate(int, string)`. Nanoid alphabet `0-9a-z`, 16 characters ≈ 82 bits.
   Randomness comes from `Core.Utilities` per skill §8.2 — never `Random`, never a `Guid` as entropy.
3. `var expiresAtUnixSeconds = DateTimeOffset.UtcNow.AddMinutes(options.StateExpirationMinutes).ToUnixTimeSeconds();`
4. `var payload = $"{nonce}{PayloadSeparator}{expiresAtUnixSeconds}";`
5. `return $"{payload}{PayloadSeparator}{Sign(payload)}";`

**Signing** (`private string Sign(string payload)`):
`HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtOptions.Value.Key), Encoding.UTF8.GetBytes(payload))`,
returned as `System.Buffers.Text.Base64Url.EncodeToString(signature)` (.NET 10 BCL — no new package).
The key is `CoreJwt:Key`, server-only; the client never sees it and cannot forge a state.

**Verification** (`OAuthStateProtector.Validate`), in this exact order:

| Step | Check | Result on failure |
|------|-------|-------------------|
| 1 | `string.IsNullOrWhiteSpace(state)` | `Invalid` |
| 2 | `state.Split(PayloadSeparator).Length != ExpectedSegmentCount` | `Invalid` |
| 3 | `long.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAtUnixSeconds)` fails | `Invalid` |
| 4 | `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign($"{segments[0]}{PayloadSeparator}{segments[1]}")), Encoding.UTF8.GetBytes(segments[2]))` is false | `Invalid` |
| 5 | `DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds) < DateTimeOffset.UtcNow` | `Expired` |
| 6 | otherwise | `Valid` |

Precisely which failure produces which outcome:

- **Tampered nonce, tampered expiry, tampered signature, truncated or extended segment count,
  non-numeric expiry, a state signed with a different key, a state from another deployment** →
  step 2/3/4 → `Invalid` → `#error=AUTHENTICATION_STATE_INVALID`. Because the signature is checked
  **before** the expiry, extending the expiry segment reports `Invalid`, never `Expired`.
- **Correctly signed but older than `StateExpirationMinutes`** → step 5 → `Expired` →
  `#error=AUTHENTICATION_STATE_EXPIRED`.
- `FixedTimeEquals` returns false (it does not throw) on a length mismatch, so a short signature segment
  is a clean `Invalid`. Constant-time comparison denies a signature-forgery oracle.
- **Replay:** a stateless `state` is re-verifiable inside its window, so the handler accepts it again. It
  is inert without an unused GitHub `code` (GitHub codes are single-use and short-lived); a replayed
  state paired with an already-consumed code dies at callback step 3 with
  `#error=GITHUB_TOKEN_EXCHANGE_FAILED`. The window is bounded by `StateExpirationMinutes = 10`, already
  in configuration. A named test documents this so it reads as a decision rather than an oversight.
- **Residual risk, disclosed:** a stateless state is not bound to the browser that started the flow, so
  classic login-CSRF (forcing a victim's browser to finish the attacker's flow) is not fully closed. The
  fix — a `SameSite=Lax` nonce cookie compared on callback — is in Deferred; decision 2 fixed the
  mechanism for this slice.

## Callback sequence

`CompleteGitHubLoginHandler.Handle`, in order. Every failure is `return Failure(<code>);` — a 302 to
`{WebRedirectUrl}#error=<code>`, never JSON, never a throw.

| Step | Action | Failure branch |
|------|--------|----------------|
| 1 | `if (string.IsNullOrWhiteSpace(request.Code))` | `AuthenticationCodeMissing` |
| 2 | `var stateStatus = oAuthStateProtector.Validate(request.State);` then `if (stateStatus == OAuthStateStatus.Invalid)` … then `if (stateStatus == OAuthStateStatus.Expired)` | `AuthenticationStateInvalid` / `AuthenticationStateExpired`. Nothing outbound has happened yet — an invalid state never reaches GitHub. |
| 3 | `var accessToken = await gitHubOAuthClient.ExchangeCodeForAccessTokenAsync(request.Code, cancellationToken).ConfigureAwait(false);` then `if (string.IsNullOrWhiteSpace(accessToken))` | `GitHubTokenExchangeFailed` — covers non-2xx, GitHub's `error` payload (a reused or expired code), malformed JSON, transport failure and timeout, all collapsed to `null` by the client contract. |
| 4 | `var profile = await gitHubOAuthClient.GetProfileAsync(accessToken, cancellationToken).ConfigureAwait(false);` then `if (profile == null)` | `GitHubProfileFetchFailed` |
| 5 | `if (profile.Id <= 0 \|\| string.IsNullOrWhiteSpace(profile.Login))` | `GitHubProfileInvalid` — the reachable "user creation failure" (decision 11) |
| 6 | `var user = await userRepository.FirstOrDefaultAsync(x => x.GitHubId == profile.Id, cancellationToken).ConfigureAwait(false);` — tracked, no `asNoTracking` | — |
| 7a | `if (user == null) { user = User.CreateFromGitHub(profile.Id, profile.Login, profile.Name, profile.AvatarUrl); await userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false); }` | — |
| 7b | `else { user.UpdateGitHubProfile(profile.Name, profile.AvatarUrl); userRepository.Update(user); }` | — |
| 8 | `await userRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);` — the single save, success path only | A database failure surfaces as a 500 through `CoreExceptionMiddleware`, like every other handler in the solution (decision 11). |
| 9 | `var token = tokenService.GenerateTokenAsync(UserClaimsGenerator.Generate(user));` — the synchronous overload, no await | — |
| 10 | `return new AuthenticationRedirectResult(AuthenticationRedirectUrlGenerator.Success(gitHubAuthenticationOptions.Value.WebRedirectUrl, token));` | — |

## Outbound HTTP to GitHub

Client acquisition: `IHttpClientFactory` via the typed-client registration in decision 17 —
`GitHubOAuthClient` receives its `HttpClient` through the primary constructor. `new HttpClient()`
anywhere in the diff is a defect. The handler depends only on `IGitHubOAuthClient`, so every branch is
substitutable with `Substitute.For<IGitHubOAuthClient>()`.

Client-level configuration (set once in DI): `Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds)`
(default 10s), `Accept: application/json`, and `User-Agent: {options.UserAgent}` (default `e3a`) —
GitHub's API rejects requests without a `User-Agent`.

`ExchangeCodeForAccessTokenAsync`:

- `POST options.AccessTokenUrl`, body `FormUrlEncodedContent` of `client_id`, `client_secret`, `code`,
  `redirect_uri` (= `options.CallbackUrl`). The secret goes in the **body**, never in a URL.
- `Accept: application/json` on the request, otherwise GitHub replies form-encoded.
- Returns `payload?.AccessToken` when `payload?.Error` is blank, else `null`.

`GetProfileAsync`:

- `GET options.UserProfileUrl` with `Accept: application/vnd.github+json` and
  `Authorization: Bearer {accessToken}` set per request on the `HttpRequestMessage` — never on
  `DefaultRequestHeaders`, which is shared across callers.
- Maps to `new GitHubProfile(payload.Id, payload.Login ?? string.Empty, payload.Name, payload.AvatarUrl)`;
  the empty-login case is caught by callback step 5.

Shared private helper
`Task<TPayload?> SendAsync<TPayload>(HttpRequestMessage request, CancellationToken cancellationToken) where TPayload : class`:
`using var request` (which disposes the content),
`using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false)`;
`if (!response.IsSuccessStatusCode)` → log a warning carrying the status code and
`request.RequestUri?.GetLeftPart(UriPartial.Path)` only → `return null`; otherwise
`ReadAsStringAsync(cancellationToken)` + `JsonSerializer.Deserialize<TPayload>(content)`.
`catch (HttpRequestException)`, `catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)`
(the timeout, not caller cancellation) and `catch (JsonException)` each log a warning and return `null`.
Caller cancellation is re-thrown, as everywhere else in the solution.

No retry policy, no Polly, no circuit breaker — none exists in this codebase, and a failed login is a
one-click retry for the user.

## Open-redirect safety

- Only two request values are bound anywhere in this slice: `code` and `state`. Neither is ever
  concatenated into a URL. `code` goes into a form body; `state` goes into `Validate`.
- `GitHubAuthorizationUrlGenerator.Generate(options, state)` takes its base URL from
  `options.AuthorizationUrl` and its `redirect_uri` from `options.CallbackUrl` — both configuration.
  `state` is server-generated and lands in a query value encoded by `QueryHelpers.AddQueryString`.
- `AuthenticationRedirectUrlGenerator.Success/Failure` take `webRedirectUrl` from
  `options.WebRedirectUrl` at both call sites; the only variable parts are a server-issued token and an
  `ErrorCodes` constant, each passed through `Uri.EscapeDataString`, so neither can inject a new
  fragment, query or authority.
- There is no `returnUrl`, `redirect_uri` or `next` parameter on any action, and no `Redirect` call in
  the controller takes anything but `result.RedirectUrl`.
- Tests 23, 29 and 54 assert the produced URLs start with the configured base.

## Secret handling

- `ClientSecret` is read in exactly one place: `GitHubOAuthClient.ExchangeCodeForAccessTokenAsync`, from
  `IOptions<GitHubAuthenticationOptions>`. It is never assigned to a field, never returned, and never put
  into a URL, a fragment, an exception message, or an `ErrorCodes` value.
- `CoreRequestLoggingMiddleware` logs **inbound** `Method`, `Path`, `QueryString`, headers and status. It
  never sees outbound `HttpClient` traffic and never logs bodies. The secret only ever appears in an
  outbound POST body, so the middleware cannot reach it. Nothing in the middleware pipeline is changed or
  reordered.
- The client's own logging is limited to a status code and the request path with the query stripped
  (`GetLeftPart(UriPartial.Path)`); request content is never logged.
- Failure redirects carry an `ErrorCodes` constant only — never a GitHub message, never a payload.
- `appsettings.json` is git-ignored (constitution §0.4); this slice adds no committed configuration file
  and no secret to any diff. The test options factory uses a dummy secret value.
- Known, accepted, out of scope: the inbound callback's `QueryString` — containing the single-use `code`
  and the `state` — is logged by the Core middleware. The code is already consumed by the time the log
  line is written, and `Core.Logging` is a vendored library outside this slice.

## Azure resources

**None required.** The `state` is self-contained and signed with the existing `CoreJwt:Key`; no cache, no
Key Vault, no App Configuration change, no new storage. The migration runs against the existing database.
Nothing in this plan needs the dev to create anything in Azure.

## Configuration to announce to the dev

No new section. Seven optional keys may be added under the existing `GitHubAuthentication` section to make
the values explicit; each already has a safe class default, so absence breaks nothing (constitution §2 —
CI and fresh clones bind empty): `Scope` (`read:user`), `UserAgent` (`e3a`), `HttpTimeoutSeconds` (`10`),
`StateNonceSize` (`16`), `GitHubLoginMaxLength` (`100`), `DisplayNameMaxLength` (`200`),
`AvatarUrlMaxLength` (`500`). The implementation report must list these.

## Postman

New folder `Authentication`, inserted as the **first** element of the collection's `item` array:

| Request name | Method | URL | Item settings |
|---|---|---|---|
| `GitHub Login` | GET | `{{baseUrl}}/api/auth/github/login` | `"auth": {"type": "noauth"}`, `"protocolProfileBehavior": {"followRedirects": false}` |
| `GitHub Callback` | GET | `{{baseUrl}}/api/auth/github/callback?code={{gitHubCode}}&state={{gitHubState}}` with a matching `query` array (`code`, `state`) | `"auth": {"type": "noauth"}`, `"protocolProfileBehavior": {"followRedirects": false}` |
| `Get Current User` | GET | `{{baseUrl}}/api/auth/me` | inherits the collection bearer `{{token}}` |

Mirror the existing item shape exactly: `name`, `request.method`, `request.header: []`,
`request.url.raw`, `request.url.host: ["{{baseUrl}}"]`, `request.url.path: [...]`. The file must remain
valid JSON in the same 1-space indentation style already in it.

## Test plan

`E3A.Tests` references Application and Domain only — `GitHubOAuthClient` (Infrastructure), the controller,
the EF configuration and the migration are out of scope by `conventions/dotnet-testing.md` §5. There are
no validators in this slice (decision 22), so there are no validator tests. Every test class is `sealed`,
substitutes are `private readonly` field initialisers, the constructor wires `_sut` only, bodies are three
unlabelled AAA blocks, no `.ConfigureAwait(false)` inside test methods, no file over ~100 lines.

NSubstitute note the implementer must follow: `IRepository<T>` methods carry optional parameters, so stubs
must supply every argument, e.g.
`_userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())`
— the same shape already used in `RegenerateMarketplaceHandlerTests`.

| # | Test class (file, under `api/E3A.Tests/`) | Test method | Asserts |
|---|-------------------|-------------|---------|
| 1 | `Identity/UserTests.cs` | `CreateFromGitHub_ShouldSetGitHubIdentity_WhenCalled` | `GitHubId`, `GitHubLogin`, `DisplayName`, `AvatarUrl` match the arguments; `Id` is not `Guid.Empty` |
| 2 | `Identity/UserTests.cs` | `CreateFromGitHub_ShouldSetUserNameAndNormalizedUserNameFromLogin_WhenCalled` | `UserName` equals the login; `NormalizedUserName` equals its upper-invariant form |
| 3 | `Identity/UserTests.cs` | `CreateFromGitHub_ShouldSetSecurityStamp_WhenCalled` | `SecurityStamp` is not null or whitespace |
| 4 | `Identity/UserTests.cs` | `CreateFromGitHub_ShouldStampCreationAndUpdationDates_WhenCalled` | both `.Should().BeOnOrAfter(before)` with `before` captured first — no equality on `UtcNow` |
| 5 | `Identity/UserTests.cs` | `CreateFromGitHub_ShouldLeaveEmailUnset_WhenCalled` | `Email` and `NormalizedEmail` are null (no `user:email` scope) |
| 6 | `Identity/UserTests.cs` | `UpdateGitHubProfile_ShouldReplaceDisplayNameAndAvatar_WhenCalled` | both properties take the new values, covering value-to-value and value-to-null |
| 7 | `Identity/UserTests.cs` | `UpdateGitHubProfile_ShouldAdvanceUpdationDate_WhenCalled` | `UpdationDate.Should().BeOnOrAfter(before)` |
| 8 | `Identity/UserTests.cs` | `UpdateGitHubProfile_ShouldNotChangeGitHubIdentity_WhenCalled` | `GitHubId`, `GitHubLogin`, `UserName`, `NormalizedUserName` unchanged — locks decision 14 |
| 9 | `Authentication/Shared/OAuthStateProtectorTests.cs` | `Create_ShouldProduceThreeDotSeparatedSegments_WhenCalled` | `state.Split('.').Length == 3`; segment 0 is the stubbed nonce; segment 1 parses as a long |
| 10 | `Authentication/Shared/OAuthStateProtectorTests.cs` | `Create_ShouldCarryTheConfiguredExpiry_WhenCalled` | segment 1 read as unix seconds is within a second of `UtcNow + StateExpirationMinutes` (`BeCloseTo`) |
| 11 | `Authentication/Shared/OAuthStateProtectorTests.cs` | `Create_ShouldProduceDifferentStates_WhenCalledTwice` | generator stubbed `Returns("nonceone", "noncetwo")`; the two states differ |
| 12 | `Authentication/Shared/OAuthStateProtectorTests.cs` | `Validate_ShouldReturnValid_WhenStateWasJustCreated` | `OAuthStateStatus.Valid` |
| 13 | `Authentication/Shared/OAuthStateProtectorTests.cs` | `Validate_ShouldReturnExpired_WhenExpiryHasPassed` | options built with `StateExpirationMinutes = -1`; result is `Expired`, with no `Thread.Sleep` |
| 14 | `Authentication/Shared/OAuthStateProtectorTests.cs` | `Validate_ShouldReturnValid_WhenTheSameStateIsValidatedTwice` | both calls `Valid` — documents the accepted stateless replay window (decision 7) |
| 15 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenStateIsMissing` | `[Theory]` over `null`, `""`, `"   "` |
| 16 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenSegmentCountIsWrong` | `[Theory]` over one-, two- and four-segment values |
| 17 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenExpiryIsNotANumber` | nonce + `"later"` + a signature |
| 18 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenNonceIsTampered` | segment 0 altered, signature untouched |
| 19 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenExpiryIsExtendedWithoutResigning` | segment 1 pushed far into the future → `Invalid`, not `Valid` and not `Expired` — locks the signature-before-expiry order |
| 20 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenSignatureIsTampered` | last segment altered |
| 21 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenSignatureIsTruncated` | last segment cut in half — proves `FixedTimeEquals` returns false on unequal lengths instead of throwing |
| 22 | `Authentication/Shared/OAuthStateProtectorTamperTests.cs` | `Validate_ShouldReturnInvalid_WhenStateWasSignedWithADifferentKey` | state created by a second `OAuthStateProtector` built with a different `JwtOptions.Key` |
| 23 | `Authentication/Shared/GitHubAuthorizationUrlGeneratorTests.cs` | `Generate_ShouldStartWithTheConfiguredAuthorizationUrl_WhenCalled` | `StartWith(options.AuthorizationUrl)` — open-redirect guard |
| 24 | `Authentication/Shared/GitHubAuthorizationUrlGeneratorTests.cs` | `Generate_ShouldCarryClientIdRedirectUriScopeAndState_WhenCalled` | all four query parameters present with the configured or passed values |
| 25 | `Authentication/Shared/GitHubAuthorizationUrlGeneratorTests.cs` | `Generate_ShouldEscapeTheRedirectUri_WhenItContainsReservedCharacters` | the raw `://` of the callback does not appear un-encoded in the query |
| 26 | `Authentication/Shared/AuthenticationRedirectUrlGeneratorTests.cs` | `Success_ShouldPlaceTheTokenInTheFragment_WhenCalled` | equals `"{webRedirectUrl}#token={token}"` and contains no `?` |
| 27 | `Authentication/Shared/AuthenticationRedirectUrlGeneratorTests.cs` | `Success_ShouldEscapeTheToken_WhenItContainsReservedCharacters` | a token containing `#` and `&` comes back percent-encoded |
| 28 | `Authentication/Shared/AuthenticationRedirectUrlGeneratorTests.cs` | `Failure_ShouldPlaceTheErrorCodeInTheFragment_WhenCalled` | equals `"{webRedirectUrl}#error={ErrorCodes.AuthenticationStateInvalid}"`, bound to the constant |
| 29 | `Authentication/Shared/AuthenticationRedirectUrlGeneratorTests.cs` | `Failure_ShouldStartWithTheConfiguredRedirectUrl_WhenCalled` | `StartWith(webRedirectUrl)` — no request value can prefix it |
| 30 | `Authentication/Shared/UserClaimsGeneratorTests.cs` | `Generate_ShouldEmitAUserIdClaimCurrentUserServiceCanParse_WhenCalled` | a claim of type `CurrentUserService.Constants.UserIdClaimType` exists and `Guid.Parse(value) == user.Id` |
| 31 | `Authentication/Shared/UserClaimsGeneratorTests.cs` | `Generate_ShouldEmitTheUserNameClaim_WhenCalled` | type `CurrentUserService.Constants.UserNameClaimType`, value `user.UserName` |
| 32 | `Authentication/Shared/UserClaimsGeneratorTests.cs` | `Generate_ShouldEmitTheLoginTypeClaim_WhenCalled` | type `CurrentUserService.Constants.LoginTypeClaimType`, value `UserClaimsGenerator.GitHubLoginType` |
| 33 | `Authentication/Shared/UserClaimsGeneratorTests.cs` | `Generate_ShouldEmitTheCreatedAtUnixSecondsClaim_WhenCalled` | type `CurrentUserService.Constants.CreatedAtUnixTimeSecondsClaimType`, value parses to `user.CreationDate.ToUnixTimeSeconds()` |
| 34 | `Authentication/Shared/UserClaimsGeneratorTests.cs` | `Generate_ShouldNotEmitARoleClaim_WhenCalled` | no claim of type `ClaimTypes.Role` — locks decision 3 |
| 35 | `Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs` | `Handle_ShouldRedirectToTheConfiguredAuthorizationUrl_WhenCalled` | `RedirectUrl.Should().StartWith(options.AuthorizationUrl)` |
| 36 | `Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs` | `Handle_ShouldCarryTheStateFromTheProtector_WhenCalled` | protector stubbed to `"signed-state"`; the URL contains `state=signed-state` |
| 37 | `Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs` | `Handle_ShouldRequestANewState_WhenCalledTwice` | `_oAuthStateProtector.Received(2).Create()` |
| 38 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs` | `Handle_ShouldCreateTheUser_WhenTheGitHubIdIsUnknown` | repository returns null; `AddAsync` received once with a user whose `GitHubId`, `GitHubLogin`, `DisplayName`, `AvatarUrl` and `UserName` come from the profile |
| 39 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs` | `Handle_ShouldReturnTheTokenInTheFragment_WhenLoginSucceeds` | token service stubbed to `"jwt-value"`; `RedirectUrl` equals `$"{WebRedirectUrl}#token=jwt-value"` |
| 40 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs` | `Handle_ShouldSaveChangesOnce_WhenLoginSucceeds` | `_userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>())` |
| 41 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerTests.cs` | `Handle_ShouldIssueTheTokenWithTheStoredUserId_WhenTheUserIsCreated` | capture the claims passed to `GenerateTokenAsync`; the `UserIdClaimType` value parses to the id of the user passed to `AddAsync` |
| 42 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerReturningUserTests.cs` | `Handle_ShouldMatchByGitHubNumericIdNotLogin_WhenTheLoginHasChanged` | stored user has `GitHubId = 4242`, login `octocat`; GitHub returns id `4242`, login `octocat-renamed`. The stub returns the stored user only when the captured predicate compiles and matches it, so `AddAsync` `DidNotReceive` proves matching is by id — locks decision 4 |
| 43 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerReturningUserTests.cs` | `Handle_ShouldUpdateDisplayNameAndAvatar_WhenTheUserAlreadyExists` | both properties equal the fresh profile values; `Update` received once |
| 44 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerReturningUserTests.cs` | `Handle_ShouldNotChangeGitHubLoginOrUserName_WhenTheUserAlreadyExists` | both still `octocat` — locks decision 14 |
| 45 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerReturningUserTests.cs` | `Handle_ShouldSaveChangesOnce_WhenTheUserAlreadyExists` | `Received(1)`; `AddAsync` `DidNotReceive` |
| 46 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithCodeMissing_WhenCodeIsAbsent` | `[Theory]` `null`/`""`/`"   "` → `#error=AUTHENTICATION_CODE_MISSING`; `SaveChangesAsync` `DidNotReceive` |
| 47 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithStateInvalid_WhenStateIsInvalid` | protector returns `Invalid` → `#error=AUTHENTICATION_STATE_INVALID` |
| 48 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldNotCallGitHub_WhenStateIsInvalid` | `_gitHubOAuthClient.DidNotReceive().ExchangeCodeForAccessTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())` — proves the state is verified before any outbound call |
| 49 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithStateExpired_WhenStateIsExpired` | protector returns `Expired` → `#error=AUTHENTICATION_STATE_EXPIRED` |
| 50 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithExchangeFailed_WhenNoAccessTokenIsReturned` | client returns null → `#error=GITHUB_TOKEN_EXCHANGE_FAILED`; the profile is never fetched; `SaveChangesAsync` `DidNotReceive` |
| 51 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithProfileFetchFailed_WhenTheProfileIsNull` | `#error=GITHUB_PROFILE_FETCH_FAILED`; no token issued (`_tokenService.DidNotReceive()`) |
| 52 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithProfileInvalid_WhenTheProfileIdIsNotPositive` | `[Theory]` `0` and `-1` → `#error=GITHUB_PROFILE_INVALID`; `AddAsync` and `SaveChangesAsync` `DidNotReceive` |
| 53 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectWithProfileInvalid_WhenTheLoginIsBlank` | `[Theory]` `""`/`"   "` → the same code, and no user created |
| 54 | `Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandlerFailureTests.cs` | `Handle_ShouldRedirectToTheConfiguredWebUrl_WhenAFailureOccurs` | the failure `RedirectUrl` starts with `options.WebRedirectUrl` and contains no GitHub host |
| 55 | `Authentication/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs` | `Handle_ShouldReturnTheProfile_WhenTheUserExists` | every `CurrentUserResult` field maps from the entity, `CreatedAt` from `CreationDate` |
| 56 | `Authentication/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs` | `Handle_ShouldThrowUnauthorized_WhenThereIsNoCurrentUser` | `UserId` null and `Guid.Empty` both → `UnauthorizedCoreException` with `ErrorCodes.UserNotAuthenticated`; the repository is never queried |
| 57 | `Authentication/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs` | `Handle_ShouldThrowNotFound_WhenTheUserRowIsMissing` | `NotFoundCoreException` with `ErrorCodes.UserNotFound` |

Test support files to create:

- `api/E3A.Tests/Identity/Shared/UserFactory.cs` — `public static User GitHub(long gitHubId = 4242, string login = "octocat", string? displayName = "The Octocat", string? avatarUrl = "https://avatars.githubusercontent.com/u/4242")` returning `User.CreateFromGitHub(...)`. No `new User { … }`, no reflection.
- `api/E3A.Tests/Authentication/Shared/GitHubAuthenticationOptionsFactory.cs` — `public static GitHubAuthenticationOptions Default(int stateExpirationMinutes = 10)` returning a fully populated options object (non-empty `ClientId`, `AuthorizationUrl`, `AccessTokenUrl`, `UserProfileUrl`, `CallbackUrl`, `WebRedirectUrl`, `Scope`), mirroring `PublishingOptionsFactory`. `ClientSecret` gets a dummy value; no real secret enters the test project.
- `api/E3A.Tests/Authentication/Shared/GitHubProfileFactory.cs` — `public static GitHubProfile Default(long id = 4242, string login = "octocat", string? name = "The Octocat", string? avatarUrl = "https://avatars.githubusercontent.com/u/4242")`.

## Docs sync

Judged per `.claude/rules/docs-sync.md`.

**Not violations — leave alone:** `docs/implementation-plan.md` line 40 lists `IsBlocked` on `users`
(acceptance decision 8: unbuilt, therefore incompleteness). The superseded Function-App OAuth bullet is
already labelled superseded. Phase P2's unfinished parts stay.

**Divergence to fix in this change** — the slice establishes a URL/format contract (`#token=` / `#error=`
handoff, stateless signed `state`) that the docs do not answer and the frontend slice will consume:

1. `docs/implementation-plan.md`, `## API surface (/api/*)`, first sentence: replace
   ``Auth: `GET login`, `GET callback` (code→JWT), `GET me`.`` with
   ``Auth (anon): `GET /api/auth/github/login` → 302 to GitHub with a stateless signed anti-CSRF `state`; `GET /api/auth/github/callback` → server-side code exchange → e3a JWT returned by a 302 to the web app with the token in the URL fragment (`#token=`), every failure returning `#error=<ERROR_CODE>` instead of a JSON error; `GET /api/auth/me` (auth) → the creator's profile.``
2. `docs/architecture.md`, `## Principles`, add one bullet after the "Public-only in v0.1" bullet:
   ``- **Auth is a fragment handoff.** Creators sign in with GitHub; the API exchanges the code server-side, issues the same `CoreJwt` HS256 token every endpoint already validates, and hands it to the SPA in the URL fragment — never a cookie, never a query string. The anti-CSRF `state` is stateless: a nonce plus an expiry, HMAC-signed with the JWT key, so no cache and no extra Azure resource are needed.``

No other doc changes. `docs/plugin-spec.md`, `docs/security-scan.md`, `docs/constitution.md` and
`docs/design-prompt.md` are untouched by this slice.

## Known limitation — the live round trip is unverifiable here

Completing the flow requires a human clicking Approve on a GitHub consent screen; the implementer cannot
do it and must not claim otherwise. What the plan does instead:

- Every branch of the callback, including all six failure modes, is exercised through
  `Substitute.For<IGitHubOAuthClient>()` (tests 38–54).
- The `state` contract is fully tested against tampering, expiry and cross-key forgery (tests 9–22).
- The claim set is asserted against the very constants `CurrentUserService` reads, so a token issued by
  this code is provably readable by every existing handler (tests 30–34).
- The URLs sent to GitHub and back to the SPA are asserted as strings (tests 23–29).
- Untested by construction and to be named as such in the report: the real GitHub HTTP conversation
  (`GitHubOAuthClient`), whether the GitHub App's registered callback matches `CallbackUrl`, and whether
  the installed App returns the profile fields.
- The implementation report must state plainly: **the end-to-end GitHub round trip is unverified and
  needs the dev to sign in once.**

## Definition of done

- [ ] `User` has the four GitHub properties with private setters, `CreateFromGitHub` and `UpdateGitHubProfile`; `UpdateGitHubProfile` sets `UpdationDate`; no other `User` member changed.
- [ ] Migration `oauth004` exists and contains exactly the four columns and the one filtered unique index; `AppDbContextModelSnapshot.cs` regenerated.
- [ ] All 21 files in §Files to create exist at exactly those paths with exactly those namespaces and signatures; no additional production file was created.
- [ ] No new NuGet package and no `Directory.Packages.props` change.
- [ ] `GET /api/auth/github/login`, `GET /api/auth/github/callback` and `GET /api/auth/me` exist on `AuthenticationController` with the attributes in §API surface; the first two are `[AllowAnonymous]`.
- [ ] Every callback failure branch returns a 302 to `WebRedirectUrl#error=<code>`; there is no `throw` and no validator in the callback path.
- [ ] `state` is verified signature-first, expiry-second, using `CryptographicOperations.FixedTimeEquals`; a tampered expiry yields `Invalid`, not `Expired`.
- [ ] The JWT carries exactly the four claims in §API surface, typed from `CurrentUserService.Constants`, and no role claim.
- [ ] The only redirect targets are `options.AuthorizationUrl` and `options.WebRedirectUrl`; no request-bound value reaches a URL builder; the actions bind only `code` and `state`.
- [ ] `ClientSecret` appears only in the token-exchange form body — in no log statement, no exception, no redirect and no committed file.
- [ ] `GitHubOAuthClient` is a typed `HttpClient` from `AddHttpClient`; it sets `Accept: application/json` on the exchange, `Accept: application/vnd.github+json` plus a per-request `Authorization` header on the profile call, a `User-Agent`, and the configured timeout; it returns null on every failure path.
- [ ] Six new `ErrorCodes` constants exist, each with a key in **both** `Messages.en.resx` and `Messages.ar.resx`.
- [ ] `SaveChangesAsync` is called exactly once, on the success path only.
- [ ] All 57 tests in §Test plan exist with those exact names and pass; failure tests assert `SaveChangesAsync` `DidNotReceive()`; entities are built through `UserFactory`; no reflection, no `Thread.Sleep`, no `UtcNow` equality.
- [ ] Postman has an `Authentication` folder with the three requests, the correct auth and redirect settings, and the file is valid JSON.
- [ ] `docs/implementation-plan.md` and `docs/architecture.md` carry the two edits in §Docs sync, and nothing else in `/docs` changed.
- [ ] `dotnet build` is clean with zero new warnings (`TreatWarningsAsErrors` is on) and `dotnet test` is green.
- [ ] The report announces the seven optional `GitHubAuthentication` keys and states that the live GitHub round trip is unverified.
- [ ] No Azure resource was created or assumed.
