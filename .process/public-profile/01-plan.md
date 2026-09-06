# Plan — Public creator profile (`/u/{login}`)

## Goal
An anonymous visitor who clicks a creator's `@login` anywhere in the app lands on `/u/{login}` and sees that creator's real profile: avatar, display name, GitHub link, join date, total installs, and two tabs listing every engineer and team they have **published** — served by one new anonymous endpoint, `GET /api/catalog/creators/{login}`. Today that page only works for the signed-in creator's own login and shows a "not available yet" dead end to everyone else.

## Scope
**In:**
- `GET /api/catalog/creators/{login}` — anonymous, one payload: creator header + published engineers + published teams.
- New CQRS slice `E3A.Application/Catalog/GetCreatorProfile/` (query, validator, handler) + `Catalog/Shared` results/generators.
- 2 new error codes + both resx files; Postman request; `docs/implementation-plan.md` API-surface line.
- `web/`: `ProfilePage.tsx` switched to the public endpoint; `lib/api.ts` wrapper; a testable sibling `features/profile/profileItems.ts` + its test; `CatalogItem.installs` made optional so team cards do not display a fabricated install count.

**Out:**
- Any change to `GET /api/catalog`, `GetCatalogQuery`, `CatalogEngineerResult`, `/engineers/mine`, `/teams/mine`, or the `Engineer`/`Team`/`User` entities. No migration, no new DbSet, no new entity config, no `DefaultCodes` class (none exists in this repo), no new repository method, no new exception type.
- Vote/report/follow affordances on the profile.

**Deferred (with why):**
| Deferred | Why |
|---|---|
| `AuthorLogin` on `CatalogEngineerResult` + `author` on `CatalogPage.toCatalogItem` | This is the *second* use case in the request. Real catalog cards currently set no `author` at all (`web/src/features/catalog/CatalogPage.tsx:12` omits it), so the `@author` link only renders for the mock rows in `lib/catalog.ts`. Making it real changes the catalog contract and needs an owner join in the catalog query — its own slice. This slice makes the destination work first. |
| Public team detail / team browse in the catalog | `CatalogTeamResult` created here is the reusable foundation; browse is a separate endpoint. |
| Index on `Users.GitHubLogin` | The lookup compares `UPPER(GitHubLogin)`, which is not SARGable against a plain index; doing it properly needs a persisted computed column + migration. Users is small and unindexed on this column today; a perf slice, not a correctness one. |
| Owner-only extras (drafts/unlisted) on one's own profile | See Decision 6 — everyone sees the same public view. |

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | One endpoint vs. author filter on `/api/catalog` + a user lookup | **One endpoint: `GET /api/catalog/creators/{login}`**, returning `{ id, gitHubLogin, displayName, avatarUrl, createdAt, totalInstalls, engineers[], teams[] }` | The page needs header data the catalog result cannot carry, plus **two** lists. An author filter would still need a second call for the header and a third for teams, giving three loading states and no atomic "unknown creator" answer. Route lives under `api/catalog` because that controller is already the anonymous public-read surface (`[AllowAnonymous]` at class level, `CatalogController.cs:13`) and the docs define `/catalog` as the anon browse area. Two path segments (`creators/{login}`) cannot collide with the existing one-segment `[HttpGet("{slug}")]`. |
| 2 | Login matching + unknown-login behaviour | Match **case-insensitively in SQL** via `x.GitHubLogin != null && x.GitHubLogin.ToUpper() == normalizedLogin`, where `normalizedLogin = request.GitHubLogin.Trim().ToUpperInvariant()`. Unknown login → **404** `NotFoundCoreException(ErrorCodes.UserNotFound)` (existing constant, already in both resx files). | Checked the column configuration rather than assuming: `AppDbContext.ConfigureUsers` (`AppDbContext.cs:42-53`) sets only `HasMaxLength` — no `UseCollation` anywhere in `api/` (grep: zero hits), so case-insensitivity would rest on an unstated database-level default. An explicit `UPPER()` comparison makes the behaviour a property of the code. `NormalizedUserName` is **not** usable: `UserNameResolver` may suffix a taken login, so `UserName != GitHubLogin` in general. 404 is right: e3a logins *are* public GitHub logins, so the "existence oracle" leaks only that a public GitHub account has or has not signed into e3a — already inferable from the catalog. 200-with-empty would be worse: it makes a typo'd login indistinguishable from a real creator who has published nothing (a state that genuinely exists). No new error code — `USER_NOT_FOUND` ("We couldn't find that account.") is exactly this meaning. |
| 3 | Published only, or Unlisted too | **`Status == Published` only**, for engineers and teams | `Unlist()` (`Engineer.cs:71`) exists precisely to remove an item from public browse while keeping it installable by direct link; `GetCatalogQueryHandler` and `GetCatalogEngineerQueryHandler` both filter `Status == EngineerStatus.Published`. A profile is public browse, so it obeys the same rule. Soft-deleted rows: **verified** `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` registers `User`, `Engineer`, `Team`, `TeamMember` (`AppDbContext.cs:128-135`) — the implementer MUST NOT add `.Where(x => !x.IsDeleted)` anywhere (SKILL §8.5). `Delete()` also sets `Status = Deleted`, so those rows fail the Published test regardless. |
| 4 | Paging | **No paging.** Both lists are returned whole. | `appsettings.json` caps `Engineers.MaxEngineersPerCreator = 50` and `Teams.MaxTeamsPerCreator = 10`, so the response is hard-bounded at 60 rows *before* the Published filter. `ListMyEngineersQueryHandler`/`ListMyTeamsQueryHandler` already return unpaged `List<T>` over the same bounded sets. Paging would also break the tab counts, which the page renders for both tabs simultaneously. Ordering is `CreationDate` descending, then `Id` (deterministic tiebreak), mirroring the `ListMy*` handlers. |
| 5 | Team install counts | Teams have **no** install count in the domain (`Team.cs` has no `InstallCount`; only `Engineer` does). `CatalogTeamResult` gets **no install field**, `CreatorProfileResult.TotalInstalls` sums **published engineers only**, and the team card renders **no install text at all** — achieved by making `CatalogItem.installs` optional in `web/src/lib/types.ts` and gating the install `<span>` in `EngineerCard`. | Rendering `0 installs` for teams would state something false. The header's "total installs" already means engineers only in today's code (`ProfilePage.tsx:63`), so this preserves its meaning rather than changing it. `CatalogTeamResult` instead carries `MemberSlugs`, which makes the card's existing "Team · N engineers" line true from real data rather than the current hardcoded `members: undefined` → "0 engineers". |
| 6 | Signed-in creator viewing their own profile | **Identical public view for everyone.** No drafts, no unlisted, no `useAuth` in `ProfilePage`. | Simplest, one code path, and drafts already have a home (`/workspace`). The page becomes provably anonymous-safe: `requestJson` attaches a bearer only when one exists, and the endpoint ignores it. |
| 7 | Where does the mirror-slice structure come from | Mirror `api/E3A.Application/Catalog/` and `api/E3A.Tests/Catalog/`. | The brief points at `api/E3A.Application/Reports/` as a recently merged sibling — **it does not exist** (verified: no `Reports` folder in `E3A.Application`, no report entity/controller/test anywhere in `api/`; only an unused `"Reports"` section sits in `appsettings.json`). Do not create one. The endpoint belongs in the existing Catalog area anyway (Decision 1). |
| 8 | Authorization attribute | Rely on the class-level `[AllowAnonymous]` on `CatalogController`; add **no** attribute to the new action, and create **no** policy constant. | `DefaultCodes` does not exist in this repo (verified: no file matches `DefaultCodes*.cs`); controllers use plain `[Authorize]` / `[AllowAnonymous]`. Mirror, don't modernize. |
| 9 | Engineer result type for the profile | **Reuse** `CatalogEngineerResult` + `CatalogEngineerResultGenerator.Generate`. | Its fields are exactly a card's needs, the frontend already has the matching `CatalogEngineer` interface in `lib/api.ts`, and reuse keeps the slice at zero new engineer mapping code. |

## Existing code touched
| File | Change |
|------|--------|
| `api/E3A.Api/Controllers/Catalog/CatalogController.cs` | Add one action `GetCreatorProfile` (see API surface). Add `using E3A.Application.Catalog.GetCreatorProfile;`. Place it **after** `GetCatalogTags` and **before** `GetCatalogEngineer`. No other edit. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Add 2 constants to the existing `// Catalog` group (after `CatalogSlugRequired`). |
| `api/E3A.Api/Resources/Messages.en.resx` | Add 2 `<data>` entries. |
| `api/E3A.Api/Resources/Messages.ar.resx` | Add the same 2 keys, Arabic, no tashkeel. |
| `postman/e3a.postman_collection.json` | Add 1 request to the `Catalog` folder (see Postman). |
| `docs/implementation-plan.md` | §`API surface (/api/*)`, the "Catalog (anon):" sentence — append the new route. |
| `web/src/lib/types.ts` | `CatalogItem.installs: number` → `installs?: number`. Nothing else. |
| `web/src/components/EngineerCard.tsx` | Sparkline guard becomes `(item.installs ?? 0) >= SPARKLINE_THRESHOLD`; the installs `<span>` renders only when `item.installs !== undefined`. No other edit. |
| `web/src/lib/api.ts` | Add `CreatorProfile` / `CreatorProfileTeam` interfaces and `getCreatorProfile(login)`. |
| `web/src/lib/errorMessages.ts` | Add `USER_NOT_FOUND` and `CATALOG_CREATOR_LOGIN_TOO_LONG` entries to the `errorMessages` map (react-feature §9: re-check this file whenever the backend error surface changes). |
| `web/src/features/profile/ProfilePage.tsx` | Rewrite the data path (see Frontend). |

Verified to exist; no other production file is touched. `E3A.Infrastructure/DependencyInjection.cs` already registers `IUserRepository`, `IEngineerRepository`, `ITeamRepository` — **no DI change**.

## Files to create

### 1. `api/E3A.Application/Catalog/GetCreatorProfile/GetCreatorProfileQuery.cs`
```csharp
namespace E3A.Application.Catalog.GetCreatorProfile;

public sealed record GetCreatorProfileQuery(string GitHubLogin) : IRequest<CreatorProfileResult>;
```
Usings: `E3A.Application.Catalog.Shared`, `MediatR`.

### 2. `api/E3A.Application/Catalog/GetCreatorProfile/GetCreatorProfileQueryValidator.cs`
Namespace `E3A.Application.Catalog.GetCreatorProfile`. `public sealed class GetCreatorProfileQueryValidator : AbstractValidator<GetCreatorProfileQuery>`, constructor `GetCreatorProfileQueryValidator(IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions)`.

| Rule | Extension | Error code |
|---|---|---|
| `RuleFor(x => x.GitHubLogin)` | `.ValidateRequired(ErrorCodes.CatalogCreatorLoginRequired)` | `CATALOG_CREATOR_LOGIN_REQUIRED` |
| same chain | `.ValidateMaxLength(options.GitHubLoginMaxLength, ErrorCodes.CatalogCreatorLoginTooLong)` | `CATALOG_CREATOR_LOGIN_TOO_LONG` |

`var options = gitHubAuthenticationOptions.Value;` first, exactly as `GetCatalogQueryValidator` does with `CatalogOptions`. Use `GitHubAuthenticationOptions.GitHubLoginMaxLength` (already the source of the column width in `AppDbContext.ConfigureUsers`) — do **not** add a duplicate cap to `CatalogOptions`.

### 3. `api/E3A.Application/Catalog/GetCreatorProfile/GetCreatorProfileQueryHandler.cs`
```csharp
public sealed class GetCreatorProfileQueryHandler(IUserRepository userRepository, IEngineerRepository engineerRepository, ITeamRepository teamRepository) : IRequestHandler<GetCreatorProfileQuery, CreatorProfileResult>
```
`Handle` steps, in order:
1. `var normalizedLogin = request.GitHubLogin.Trim().ToUpperInvariant();`
2. `var creator = await userRepository.FirstOrDefaultAsync(x => x.GitHubLogin != null && x.GitHubLogin.ToUpper() == normalizedLogin, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
3. `if (creator == null) { throw new NotFoundCoreException(ErrorCodes.UserNotFound); }`
4. `var ownerUserId = creator.Id;`
5. `var engineers = await engineerRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
6. `var teams = await teamRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, include: query => query.Include(x => x.Members), asNoTracking: true).ConfigureAwait(false);`
7. `var publishedEngineers = engineers.Where(x => x.Status == EngineerStatus.Published).OrderByDescending(x => x.CreationDate).ThenBy(x => x.Id).ToList();` (one operator per line, SKILL §1)
8. `var publishedTeams = teams.Where(x => x.Status == TeamStatus.Published).OrderByDescending(x => x.CreationDate).ThenBy(x => x.Id).ToList();`
9. `return CreatorProfileResultGenerator.Generate(creator, publishedEngineers, publishedTeams);`

No `try`/`catch`, no `SaveChangesAsync` (read-only), `.ConfigureAwait(false)` on all three awaits. The status filter is deliberately **in the handler, not in the repository predicate**, mirroring `GetCatalogQueryHandler` (which filters in memory after a repository call) — the set is bounded at 60 rows by the per-creator caps, and it makes the Published-only rule reachable by a unit test (see Test plan note).

### 4. `api/E3A.Application/Catalog/Shared/CreatorProfileResult.cs`
```csharp
namespace E3A.Application.Catalog.Shared;

public sealed record CreatorProfileResult(Guid Id, string GitHubLogin, string? DisplayName, string? AvatarUrl, DateTimeOffset CreatedAt, int TotalInstalls, List<CatalogEngineerResult> Engineers, List<CatalogTeamResult> Teams);
```
Client-facing. No `LocalizedText` anywhere in this feature (e3a is EN-only; every field is a plain `string`), so **no `.Localized()` calls** — SKILL §4.5.

### 5. `api/E3A.Application/Catalog/Shared/CatalogTeamResult.cs`
```csharp
namespace E3A.Application.Catalog.Shared;

public sealed record CatalogTeamResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, List<string> MemberSlugs, Guid? LatestVersionId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
```
Client-facing. No `Status` (only Published rows reach it) and no install count (Decision 5).

### 6. `api/E3A.Application/Catalog/Shared/CatalogTeamResultGenerator.cs`
```csharp
namespace E3A.Application.Catalog.Shared;

public static class CatalogTeamResultGenerator
{
    public static CatalogTeamResult Generate(Team team)
}
```
Body: build `memberSlugs` as `team.Members.OrderBy(x => x.SortOrder).ThenBy(x => x.EngineerId).Select(x => x.EngineerSlug).ToList()` (same ordering as `TeamResultGenerator.GenerateDetail`), then return the record with `team.CreationDate` / `team.UpdationDate` as `CreatedAt` / `UpdatedAt`.

### 7. `api/E3A.Application/Catalog/Shared/CreatorProfileResultGenerator.cs`
```csharp
namespace E3A.Application.Catalog.Shared;

public static class CreatorProfileResultGenerator
{
    public static CreatorProfileResult Generate(User creator, List<Engineer> engineers, List<Team> teams)
}
```
Body:
- `var engineerResults = engineers.Select(CatalogEngineerResultGenerator.Generate).ToList();`
- `var teamResults = teams.Select(CatalogTeamResultGenerator.Generate).ToList();`
- `var totalInstalls = engineerResults.Sum(x => x.InstallCount);`
- return `new CreatorProfileResult(creator.Id, creator.GitHubLogin!, creator.DisplayName, creator.AvatarUrl, creator.CreationDate, totalInstalls, engineerResults, teamResults)`.
- The single permitted comment in this feature, immediately above the `return`, because it records a hidden invariant: `// The creator was matched on GitHubLogin, so it cannot be null here.`

### 8. `web/src/features/profile/profileItems.ts`
Named exports only, no default. Imports: `emojiFor` from `../../lib/api`, `import type { CatalogEngineer, CreatorProfileTeam } from '../../lib/api'`, `import type { CatalogItem } from '../../lib/types'`.

| Export | Signature | Behaviour |
|---|---|---|
| `toEngineerItem` | `(engineer: CatalogEngineer): CatalogItem` | `{ emoji: emojiFor(engineer.slug), name: engineer.slug, description: engineer.description ?? '', tags: engineer.tags, installs: engineer.installCount }` |
| `toTeamItem` | `(team: CreatorProfileTeam): CatalogItem` | `{ emoji: emojiFor(team.slug), name: team.slug, description: team.description ?? '', tags: team.tags, team: true, members: team.memberSlugs.map(emojiFor) }` — **`installs` omitted entirely** |
| `joinedLabel` | `(createdAt: string | null | undefined): string` | `''` for nullish or `Number.isNaN(new Date(createdAt).getTime())`; otherwise `toLocaleDateString('en-US', { month: 'long', year: 'numeric' })`. Moved verbatim from `ProfilePage.tsx:24-30`. |
| `formatTotalInstalls` | `(totalInstalls: number): string` | `totalInstalls.toLocaleString('en-US')` |

### 9. `web/src/features/profile/profileItems.test.ts`
See Test plan. `.ts`, not `.tsx` (the vitest `include` glob).

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.UserNotFound` *(exists — reuse, do not re-add)* | `USER_NOT_FOUND` | `GetCreatorProfileQueryHandler` step 3 | `NotFoundCoreException` | 404 |
| `ErrorCodes.CatalogCreatorLoginRequired` *(new)* | `CATALOG_CREATOR_LOGIN_REQUIRED` | `GetCreatorProfileQueryValidator` → `ValidationBehaviour` | `ApplicationValidationCoreException` | 422 |
| `ErrorCodes.CatalogCreatorLoginTooLong` *(new)* | `CATALOG_CREATOR_LOGIN_TOO_LONG` | `GetCreatorProfileQueryValidator` → `ValidationBehaviour` | `ApplicationValidationCoreException` | 422 |

Resource strings (add to both files, in the same relative position as the constant — after `CATALOG_SLUG_REQUIRED`):

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|---|---|---|
| `CATALOG_CREATOR_LOGIN_REQUIRED` | `A creator login is required.` | `اسم المستخدم للمنشئ مطلوب.` |
| `CATALOG_CREATOR_LOGIN_TOO_LONG` | `That creator login is longer than we allow.` | `اسم المستخدم للمنشئ اطول من المسموح به.` |

`USER_NOT_FOUND` already has both strings (`Messages.en.resx:12`, `Messages.ar.resx:12`) — do not duplicate.

## Domain behaviour
**None. This slice adds no domain code.** `Engineer`, `Team` and `User` are read-only here: no new entity, no new domain method, no state transition, no `BusinessRuleViolationException`, no `UpdationDate` stamping, no migration. Any entity edit in the implementation is out of scope and a review finding. The invariants this feature *relies* on and must not restate:
- Soft-deleted rows are excluded by the global query filter (`AppDbContext.ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`) — never by a `.Where`.
- `Engineer.Delete()` / `Team.Delete()` set `Status = Deleted` **and** `SoftDelete()`, so deleted items fail the Published check twice over.
- `Team.Members` is only populated when the query includes it — hence the explicit `include:` in handler step 6.

## API surface
| Method | Route | Auth | Request | Response |
|---|---|---|---|---|
| `GET` | `/api/catalog/creators/{login}` | class-level `[AllowAnonymous]`; no action attribute, no policy constant | `[FromRoute] string login` (no `Request` record — the HTTP shape is one route parameter, so bind the query directly, SKILL §7.2) | `200` `CreatorProfileResult` · `404` `USER_NOT_FOUND` · `422` validation |

```csharp
[HttpGet("creators/{login}")]
public async Task<ActionResult> GetCreatorProfile([FromRoute] string login, CancellationToken cancellationToken)
{
    var result = await mediator.Send(new GetCreatorProfileQuery(login), cancellationToken);
    return Ok(result);
}
```
Thin, no `.ConfigureAwait` (controllers are exempt, SKILL §1), `cancellationToken` threaded.

**Postman** — add to the existing `Catalog` folder in `postman/e3a.postman_collection.json`, immediately after `Get Catalog Engineer`, mirroring that request's JSON shape exactly:
- name: `Get Creator Profile`
- `request.auth`: `{ "type": "noauth" }`
- method: `GET`, `header: []`
- `url.raw`: `{{baseUrl}}/api/catalog/creators/MohamedEbrahimMohsen`
- `url.host`: `["{{baseUrl}}"]`, `url.path`: `["api", "catalog", "creators", "MohamedEbrahimMohsen"]`

The mixed-case login is deliberate: sending it and getting a `200` with `"gitHubLogin": "mohamedebrahimmohsen"` is the manual proof of Decision 2's case-insensitive match, which no unit test can give (see Test plan).

## Frontend
`web/src/lib/api.ts` — add beside the existing catalog wrappers:
```ts
export interface CreatorProfileTeam { id: string; slug: string; displayName: string; description: string | null; tags: string[]; memberSlugs: string[]; latestVersionId: string | null; createdAt: string; updatedAt: string; }
export interface CreatorProfile { id: string; gitHubLogin: string; displayName: string | null; avatarUrl: string | null; createdAt: string; totalInstalls: number; engineers: CatalogEngineer[]; teams: CreatorProfileTeam[]; }
export function getCreatorProfile(login: string): Promise<CreatorProfile> {
  return requestJson(`/catalog/creators/${encodeURIComponent(login)}`);
}
```

`web/src/features/profile/ProfilePage.tsx` — data path rewrite:
- **Remove**: `useAuth` import and call, `listMyEngineers`/`listMyTeams`/`Engineer`/`Team` imports, `emojiFor` import, `initialsFor` stays, the local `toEngineerItem`/`toTeamItem`/`joinedLabel`, the `PUBLISHED_STATUS` constant, `isOwnProfile`, and the `engineers`/`teams` state.
- **Add**: `import { getCreatorProfile, type CreatorProfile } from '../../lib/api';` and `import { formatTotalInstalls, joinedLabel, toEngineerItem, toTeamItem } from './profileItems';`
- State: `profile: CreatorProfile | null`, plus the existing `tab`, `errorMessage`, `reloadToken`.
- Effect (react-feature §3, unchanged shape):
```tsx
useEffect(() => {
  let cancelled = false;
  getCreatorProfile(login)
    .then(result => { if (!cancelled) { setProfile(result); setErrorMessage(null); } })
    .catch(error => { if (!cancelled) { setErrorMessage(messageForApiError(error)); } });
  return () => { cancelled = true; };
}, [login, reloadToken]);
```
  Both callbacks check `cancelled`; deps are `[login, reloadToken]`; the existing Retry button (`setErrorMessage(null); setReloadToken(reloadToken + 1);`) is kept verbatim.
- Derived: `const engineers = profile?.engineers ?? []; const teams = profile?.teams ?? [];` · `items = tab === 'Engineers' ? engineers.map(toEngineerItem) : teams.map(toTeamItem)` · `counts = { Engineers: engineers.length, Teams: teams.length }` · `loading = profile === null && errorMessage === null` · `joined = joinedLabel(profile?.createdAt)` · `totalInstalls = formatTotalInstalls(profile?.totalInstalls ?? 0)`.
- Header, all from the endpoint: avatar `<img>` when `profile?.avatarUrl`, else the initials circle using `initialsFor(profile?.displayName ?? login)`; `<h1>{profile?.displayName ?? `@${profile?.gitHubLogin ?? login}`}</h1>`; the GitHub link and `github.com/…` text use `profile?.gitHubLogin ?? login` (canonical casing once loaded); the `Joined …` fragment renders when `joined` is non-empty; the `{totalInstalls} total installs` fragment renders when `profile !== null` (replacing the `isOwnProfile &&` guard).
- Empty state: single message for every visitor — `` `@${profile?.gitHubLogin ?? login} hasn't published any ${tab.toLowerCase()} yet.` `` The "Public profiles aren't available yet — sign in to see your own published…" string is deleted.
- Everything else (tabs markup, `type="button"`, `aria-current`, error block, grid, `EngineerCard`) is unchanged. No new route (`/u/:login` already exists in `App.tsx`), no `fetch`, no `localStorage`, no default export.

## Test plan
`api/E3A.Tests/Catalog/GetCreatorProfile/` and `api/E3A.Tests/Catalog/Shared/`, mirroring `api/E3A.Tests/Catalog/`. Substitutes: `IUserRepository _userRepository`, `IEngineerRepository _engineerRepository`, `ITeamRepository _teamRepository` as `private readonly` field initialisers; constructor wires `_sut` only. Entities via `UserFactory.GitHub(...)`, `EngineerFactory.Published/Draft/Unlisted(...)`, `TeamFactory.Published/Draft/WithMembers(...)` — never `new`, never reflection. Repository stubs use `Arg.Any<Expression<Func<T, bool>>>()`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldThrowNotFound_WhenNoUserMatchesTheLogin` | `NotFoundCoreException` where `ErrorCode == ErrorCodes.UserNotFound`; `_engineerRepository.DidNotReceive().FindAsync(...)` and `_teamRepository.DidNotReceive().FindAsync(...)` |
| 2 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldReturnTheCreatorHeader_WhenTheLoginMatches` | `result.Id`, `GitHubLogin`, `DisplayName`, `AvatarUrl`, `CreatedAt` equal the `UserFactory.GitHub()` user's values |
| 3 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldReturnOnlyPublishedEngineers_WhenTheCreatorAlsoHasDraftAndUnlistedOnes` | repo returns `[Published(installCount: 7), Draft, Unlisted]`; `result.Engineers` has exactly 1 item, its `Slug` is the published one, and `result.TotalInstalls == 7` |
| 4 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldReturnOnlyPublishedTeams_WhenTheCreatorAlsoHasDraftOnes` | repo returns `[Published, Draft]`; `result.Teams` has exactly 1 item with the published team's `Slug` |
| 5 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldOrderEngineersByNewestFirst_WhenSeveralArePublished` | three published engineers with explicit `creationDate` values supplied out of order; `result.Engineers.Select(x => x.Slug)` is newest→oldest |
| 6 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldOrderTeamsByNewestFirst_WhenSeveralArePublished` | same for teams |
| 7 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldReturnEmptyLists_WhenTheCreatorHasPublishedNothing` | both repos return `[]`; `Engineers` and `Teams` are empty (not null) and `TotalInstalls == 0` |
| 8 | `GetCreatorProfileQueryHandlerTests` | `Handle_ShouldLoadTeamMembers_WhenQueryingTeams` | `_teamRepository.Received(1).FindAsync(Arg.Any<Expression<Func<Team, bool>>>(), Arg.Any<CancellationToken>(), include: Arg.Is<Func<IQueryable<Team>, IQueryable<Team>>>(x => x != null), Arg.Any<Func<IQueryable<Team>, IOrderedQueryable<Team>>>(), true)` — pins that an include is passed at all |
| 9 | `GetCreatorProfileQueryValidatorTests` | `Validate_ShouldPass_WhenLoginIsValid` | `IsValid` true for `"octocat"` |
| 10 | `GetCreatorProfileQueryValidatorTests` | `Validate_ShouldFail_WhenLoginIsEmpty` | `[Theory]` over `""` and `"   "`; error contains `ErrorCodes.CatalogCreatorLoginRequired` |
| 11 | `GetCreatorProfileQueryValidatorTests` | `Validate_ShouldFail_WhenLoginExceedsTheConfiguredMaxLength` | login of `GitHubLoginMaxLength + 1` chars; error contains `ErrorCodes.CatalogCreatorLoginTooLong` |
| 12 | `CreatorProfileResultGeneratorTests` | `Generate_ShouldMapTheCreatorFields_WhenCalled` | `Id`/`GitHubLogin`/`DisplayName`/`AvatarUrl`/`CreatedAt` mapped from the `User` |
| 13 | `CreatorProfileResultGeneratorTests` | `Generate_ShouldSumTheInstallCountsOfEveryEngineer_WhenCalled` | two engineers with 3 and 4 installs → `TotalInstalls == 7` |
| 14 | `CreatorProfileResultGeneratorTests` | `Generate_ShouldReturnZeroTotalInstalls_WhenTheCreatorHasNoEngineers` | `TotalInstalls == 0` with only teams supplied (proves teams contribute nothing — Decision 5) |
| 15 | `CatalogTeamResultGeneratorTests` | `Generate_ShouldMapTheTeamFields_WhenCalled` | `Id`, `Slug`, `DisplayName`, `Description`, `Tags`, `LatestVersionId`, `CreatedAt`, `UpdatedAt` |
| 16 | `CatalogTeamResultGeneratorTests` | `Generate_ShouldOrderMemberSlugsBySortOrder_WhenTheTeamHasMembers` | `TeamFactory.WithMembers(ownerUserId, Pin("b-engineer"), Pin("a-engineer"))` → `MemberSlugs` is `["b-engineer", "a-engineer"]` (insertion/sort order, not alphabetical) |
| 17 | `profileItems.test.ts` | `describe('toEngineerItem')` → `it('should carry the install count and slug onto the card item')` | `name === slug`, `installs === installCount`, `emoji` non-empty |
| 18 | `profileItems.test.ts` | `describe('toEngineerItem')` → `it('should use an empty description when the engineer has none')` | `description === ''` for `description: null` |
| 19 | `profileItems.test.ts` | `describe('toTeamItem')` → `it('should leave installs undefined so no install count is rendered')` | `item.installs` is `undefined` and `item.team` is `true` |
| 20 | `profileItems.test.ts` | `describe('toTeamItem')` → `it('should map one member emoji per member slug')` | `item.members` has the same length as `memberSlugs` |
| 21 | `profileItems.test.ts` | `describe('joinedLabel')` → `it('should format the date as month and year')` | `joinedLabel('2026-03-14T00:00:00Z')` contains `'2026'` and `'March'` |
| 22 | `profileItems.test.ts` | `describe('joinedLabel')` → `it('should return an empty label when the date is missing or unparseable')` | `''` for `null` and for `'not-a-date'` |
| 23 | `profileItems.test.ts` | `describe('formatTotalInstalls')` → `it('should group thousands with separators')` | `formatTotalInstalls(12345) === '12,345'` |

**Mutation checks (dotnet-testing §9)** — run each, record both observed outcomes, restore from a byte-exact copy verified with `cmp`:
- Delete `.Where(x => x.Status == EngineerStatus.Published)` from handler step 7 → test 3 must fail.
- Delete `.Where(x => x.Status == TeamStatus.Published)` from step 8 → test 4 must fail.
- Change `OrderByDescending` to `OrderBy` in step 7 → test 5 must fail.

**Stated as unproven (§9, do not claim otherwise in the report):** the repository *predicates* — `GitHubLogin.ToUpper() == normalizedLogin` and `OwnerUserId == ownerUserId` — cannot be constrained by any test of this shape, because NSubstitute matches the expression argument without invoking it. Ownership isolation and case-insensitive login matching are therefore verified **manually** via the Postman `Get Creator Profile` request against a running API (mixed-case login returns the creator; the response contains only that creator's items). No test may be named as the proof of either.

Out of scope per dotnet-testing §5: the controller action, EF configuration, `Repository<T>`, the validation pipeline, DI.

**How the component change is verified** (react-feature §7 — there is no DOM runner, and no component test is planned):
1. `npm run build` typechecks every construction site of `CatalogItem` after `installs` becomes optional (`lib/catalog.ts` mock rows, `CatalogPage.toCatalogItem`, `profileItems.ts`) — a widening, so any breakage is a compile error, not a silent behaviour change.
2. Manual browser pass, signed **out**, on `/u/{login}`: Engineers tab shows install counts; Teams tab shows no install text and a "Team · N engineers" line whose N matches `memberSlugs.length` in the Network response; a draft/unlisted item of that creator appears in neither tab; an unknown login shows the not-found message with a working Retry (a second request in the Network tab).
3. Repeat signed **in** as the profile owner: identical output, and the request carries no dependence on the bearer token (Decision 6).

## Definition of done
- [ ] `GET /api/catalog/creators/{login}` returns `200` with creator header + published engineers + published teams, anonymously.
- [ ] Unknown login returns `404` with body `{"code":"USER_NOT_FOUND", ...}` in camelCase (SKILL §8.6 — inspect the emitted JSON, not the object).
- [ ] Only `Status == Published` engineers and teams appear; drafts, unlisted and soft-deleted items do not.
- [ ] No `.Where(x => !x.IsDeleted)` was added anywhere; the global query filter is the only soft-delete enforcement point.
- [ ] Endpoint is unpaged and returns both full lists.
- [ ] `TotalInstalls` sums published engineers only; no team install count exists in any result type; the team card renders no install text.
- [ ] Exactly the 7 backend + 4 backend-test + 2 frontend files listed were created; no others.
- [ ] No new entity, migration, DbSet, repository method, exception type, options class, or `DefaultCodes` was introduced.
- [ ] `ErrorCodes` gained exactly 2 constants, each present in **both** `Messages.en.resx` and `Messages.ar.resx`.
- [ ] Postman `Catalog` folder contains `Get Creator Profile` with `auth: noauth` and the mixed-case login URL.
- [ ] `docs/implementation-plan.md` §`API surface (/api/*)` names the new route. `docs/architecture.md` (no endpoint inventory) and `docs/design-prompt.md` §Pages item 5 (already describes this page as the target) were checked and need no edit.
- [ ] All 23 tests exist with exactly these names and pass.
- [ ] The three mutation checks were run; each broke only its named test; files restored and verified with `cmp`.
- [ ] The report states plainly that the two repository predicates are unproven by unit tests and names the manual verification used instead.
- [ ] `ProfilePage.tsx` makes no authenticated call, imports no `useAuth`, and contains no "Public profiles aren't available yet" string.
- [ ] `dotnet build` zero new warnings · `dotnet test` green · `npm run build` clean · `npm run test` green · `npx oxlint` no new warnings, zero `oxlint-disable`/`@ts-ignore`.
