# Plan — Engineer Drafts Management

## Goal
An authenticated creator can manage their own engineer drafts over HTTP against the E3A solution: create a draft from a display name, description and tags (the server derives the slug, rejects a taken slug, and refuses to exceed the configured per-creator engineer cap), edit that draft's metadata, list every engineer they own, fetch one by id, and soft-delete one — all owner-scoped, all persisted in Azure SQL through the shared `AppDbContext`. Today `E3A` has no product entity at all: `E3A.Domain` contains only `Identity/`, `E3A.Application` only `Exceptions/ErrorCodes.cs` + `DependencyInjection.cs`, `AppDbContext` declares no `DbSet`, and `E3A.Api` has no `Controllers/` folder and no migrations.

## Scope
**In:** the `Engineer` aggregate (+ `EngineerStatus`, `EngineerSlugGenerator`, `IEngineerRepository`); five MediatR use cases (`CreateEngineer`, `UpdateEngineer`, `ListMyEngineers`, `GetEngineer`, `DeleteEngineer`); `EngineerLimitsOptions` bound from configuration; `EngineerRepository` over the real shared `AppDbContext` (DbSet + named private config method + soft-delete filter registration); the **first EF migration** in the repo; `EngineersController`; 12 new error codes with `ar`/`en` resource strings; FluentValidation registration for the `E3A.Application` assembly; unit tests for the entity, the slug generator, every handler branch and every validator rule.

**Out:** publishing, versions, uploads, `DraftManifestJson` population, `LatestVersionId`/`InstallCount` mutation, teams, the scan pipeline, the anonymous catalog, GitHub OAuth and any change to `User`; `DefaultCodes`/authorization policies; auditing (`IAuditableCommand`); pagination; caching; queues; blob storage; any edit to `core-libraries/`, to `Directory.Packages.props`, to any `.csproj`, to `E3A.slnx`, or to the middleware order in `Program.cs`; running `dotnet ef database update` (no database is provisioned).

**Deferred:**

| Item | Why |
|------|-----|
| `POST /api/engineers/{id}/publish` and the version aggregate | Separate slice (implementation-plan P3). This slice deliberately leaves `Status` reachable only as `Draft → Removed`, and `LatestVersionId`/`InstallCount` write-only-by-future-code. |
| `.claude` folder upload → `DraftManifestJson` | Separate slice (P2 upload pipeline). The column is mapped now so the upload slice adds no migration for it, but nothing writes it. |
| `{githublogin}` in the slug, and `User.GitHubId`/`GitHubLogin` | GitHub OAuth is not built; `User` is the untouched template `IdentityUser<Guid>`. Adding columns to the identity user is an auth-slice change. See Decision 6. |
| `DefaultCodes` + named authorization policies | e3a v0.1 has no permission model; the only rule is ownership, and it is data-dependent (per-row), which a policy cannot express. See Decision 12. |
| Auditing via `IAuditableCommand` | `AddCoreAuditing` registers `AuditBehaviour` only when `CoreAuditing:Enabled` is true; that section does not exist in `appsettings.json`, so the interface would be inert. Enabling it is a cross-cutting decision, not an engineers decision. |
| Fixing `Core.DDD.Entity.SoftDelete()` (it sets `DeletedAt = null`) | A vendored-library defect. `core-libraries/` is out of scope for a feature slice; `IsDeleted` is what the query filter reads, so behaviour here is correct regardless. |
| Anonymous catalog list/detail (`GET /api/catalog…`) | Different audience (anonymous), different filter (published only), different result shape. Belongs to P4. |
| Tests for `EngineerRepository`, `EngineersController`, EF configuration, DI wiring | `conventions/dotnet-testing.md` §5 puts them explicitly out of scope. |

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | One slice, but the request lists five operations. | Plan **all five**. | They are one vertical slice over one aggregate: shared `Engineer`, `IEngineerRepository`, `EngineerResult`, error-code group, controller, migration. Splitting produces four follow-up plans each re-editing the same six files, and an intermediate API that 404s the resource it just created. Genuinely separable work is under **Deferred**. |
| 2 | Entity base class — skill §4.1 says `AggregateRoot`. | **`public class Engineer : AuditEntity`**. | Verified: `Core.DDD/Entities/AggregateRoot.cs` is `AggregateRoot(Guid id) : Entity(id)` — it carries **no** audit fields, and `Repository<T>.SaveChangesAsync` stamps only `ChangeTracker.Entries<AuditEntity>()`. `Engineer` needs `CreatedBy`/`CreationDate`/`UpdationDate`. The skill's table describes a base class the vendored code does not have. Do **not** add `IAuditEntity` — nothing in the solution consumes it for non-identity entities. |
| 3 | Does the domain throw `BusinessRuleViolationException`? | **No domain exception in this slice.** `UpdateMetadata` and `Remove` have no guards. | Two facts: (a) `BusinessRuleViolationException` does not exist — `Core.Errors` has `BusinessRuleViolationCoreException`; (b) `E3A.Domain` cannot reference `E3A.Application.Exceptions.ErrorCodes` (the reference runs Application → Domain), so any domain guard forces a second error-code registry in `E3A.Domain`, contradicting skill §5.1's single flat `ErrorCodes`. No invariant in this slice justifies that: a removed engineer is soft-deleted, so the global query filter makes "update a removed engineer" unreachable through the repository. If a later slice needs a true domain invariant, that slice introduces the domain-side registry deliberately. |
| 4 | Where do business rules live then? | Limit, slug-uniqueness and ownership checks live in the **handlers**, throwing `Core.Errors` exceptions. | All three are cross-entity/repository facts (a count, a lookup, the caller's identity) — they cannot be expressed inside the aggregate. This is the same shape as skill §5.5's `CreateTenantHandler` slug check. |
| 5 | Slug source. | `EngineerSlugGenerator.Generate(displayName)` — kebab-case of the **display name only**. Slug is **immutable** after creation; `UpdateEngineer` never recomputes it. | `docs/plugin-spec.md` fixes the published plugin name as `e3a-{githublogin}-{item-slug}` — the login is a *separate* segment, so `item-slug` must not contain it. Immutability: published zips live at immutable URLs keyed by plugin name; letting a rename move the slug would break installed marketplaces. |
| 6 | `docs/implementation-plan.md` says `Slug (unique {githublogin}-{name})`. | Follow `plugin-spec.md`, and **update the stale line in `implementation-plan.md`** in the same change. | The two docs disagree; `plugin-spec.md` is the naming authority and the task brief cites it. `User` has no `GitHubLogin` today, so the other reading is unimplementable. Per `.claude/rules/docs-sync.md` a naming/format contract change is divergence, not incompleteness → the doc moves with the code. Same bullet also still says `LikeCount, DislikeCount`, which the locked scope replaced with `InstallCount`. |
| 7 | Slug uniqueness scope. | **Globally unique**, enforced by a filtered unique index `IX_Engineers_Slug` `WHERE [IsDeleted] = 0`, and by a pre-insert `FirstOrDefaultAsync` check → `ConflictCoreException(ENGINEER_SLUG_TAKEN)`. | The locked data model says `Slug (unique)`. The index **must** be filtered: the repository check runs under the global soft-delete filter, so an unfiltered index would let the handler report "available" and then fail the INSERT with a 500. Consequence, accepted: a soft-deleted draft releases its slug — it was never published, so no immutable URL exists. |
| 8 | The 50-engineer cap. | `EngineerLimitsOptions { public const string SectionName = "EngineerLimits"; public int MaxEngineersPerCreator { get; set; } }`, default `50` in `appsettings.json`, injected as `IOptions<EngineerLimitsOptions>`. Counts **all** non-deleted engineers the caller owns, any status. | Constitution §0.3/§2: a tunable limit is options, never a literal. Counting all statuses (not just published, as `implementation-plan.md` phrases it) is the only rule enforceable in this slice — nothing can be published yet — and it is the stricter, safer reading. |
| 9 | Field-length caps (display name, description, tag count, tag length, slug). | **Named `public const int` on `Engineer`**, not options: `DisplayNameMaxLength = 100`, `DescriptionMaxLength = 500`, `SlugMaxLength = 100`, `MaxTags = 10`, `TagMaxLength = 30`. | They are schema invariants: the same number must appear in the validator and in `HasMaxLength` in the EF configuration. Constants on the aggregate keep the two in lockstep and give both layers one symbol. Constitution §0.3 routes invariants to named constants; tunables go to options (Decision 8). |
| 10 | `Tags` storage. | `List<string> Tags` on the entity, mapped with `HasConversion` to a JSON string, `HasMaxLength(400)`. | Locked data model says `Tags(json)`. Mirrors the existing pattern in `CoreDbContext.OnModelCreating` for `Notification.Data`. Verified by building the model: column is `nvarchar(400) NOT NULL`, no comparer warning, no model error. `MaxTags * (TagMaxLength + 4)` fits inside 400. |
| 11 | Is anything `LocalizedText`? | **No.** Every string is a plain `string`. | Skill §4.5: e3a is EN-only; `LocalizedText` requires both languages non-empty, which would make the documented request contract impossible. Consequence: no `.Localized()` call anywhere, and no admin-vs-client result split — one `EngineerResult`. |
| 12 | Authorization. | `[Authorize]` on the controller (no policy, no role). Ownership enforced in every handler: `ForbiddenCoreException(ENGINEER_NOT_OWNED)`. No `DefaultCodes`, no `Program.cs` change. | Verified: `AddCoreIdentity` calls `AddAuthorization()` / `AddAuthorizationBuilder()` and registers **zero named policies**; `[Authorize(Policy = "…")]` against an unregistered policy is a runtime `InvalidOperationException` → 500. Ownership is per-row data, which no policy can express. JWT bearer is the default scheme and `WebApplication` auto-inserts `UseAuthentication`/`UseAuthorization`, so `[Authorize]` works as-is. Documented deviation from skill §7.1. |
| 13 | 403 or 404 when a non-owner asks for someone else's engineer? | **403 `ENGINEER_NOT_OWNED`.** | The rule being enforced is ownership, and the exception table maps "authenticated but lacks permission" to `ForbiddenCoreException`. Ids are GUIDs, so existence disclosure is not a practical enumeration risk. One code, one branch, one test per handler. |
| 14 | Delete semantics. | **Soft delete**: `engineer.Remove()` (sets `Status = Removed` + `SoftDelete()`), then `engineerRepository.Update(engineer)` + one `SaveChangesAsync`. `IRepository.Delete` is never called. | `Repository<T>.Delete` is `_context.Remove` — a hard delete. The request says soft-delete, the data model has a `Removed` status, and skill §6.4 makes the global filter the single enforcement point (registered here). A handler test asserts `Delete` is **not** received. |
| 15 | List endpoint shape. | `GET /api/engineers` → `List<EngineerResult>`, owner-scoped, newest first. No `PageData`. | Skill §5.6: no blanket pagination mandate; the result set is hard-capped at `MaxEngineersPerCreator` (50) by Decision 8. `implementation-plan.md` puts the anonymous browse experience on `/api/catalog`, so `/api/engineers` is free to mean "mine". |
| 16 | Ordering — repository `orderBy` or handler LINQ? | **Handler LINQ**: `.OrderByDescending(x => x.CreationDate)`. | An `orderBy` delegate handed to a substituted `IEngineerRepository` never executes, so the ordering test would assert nothing. Sorting in memory is safe precisely because the set is capped at 50. |
| 17 | `?? []` after the list query. | Use `FindAsync` (returns non-nullable `List<T>`) and **do not** null-coalesce. | Verified signature: `Task<List<T>> FindAsync(...)`. `GetAllAsync` is the nullable one and is not used here. Tests always stub `FindAsync` with a real list. |
| 18 | Validators for queries with no input. | `ListMyEngineersQuery` gets **no** validator; the other four use cases do. | An empty validator is a registered no-op and a test with nothing to assert. |
| 19 | Are `E3A.Application` validators executed today? | **No — add `services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);` to `AddApplication`.** | `AddCoreCQRS` calls `AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())` — the **Core.CQRS** assembly. Without this line every validator in this plan is dead code. Skill §5.11 prescribes exactly this line. |
| 20 | How does `AddApplication` see configuration? | Change the signature to `AddApplication(this IServiceCollection services, IConfiguration configuration)` and update the single call site in `Program.cs`. **Rename the existing MediatR lambda parameter** from `configuration` to `mediatRConfiguration`. | Constitution §2: options are bound in the layer's `DependencyInjection.cs`, the only registration point. The file already carries `using Microsoft.Extensions.Configuration;`. Without the rename the new parameter collides with the lambda parameter → **CS0136, build failure**. |
| 21 | Can an EF migration be generated here? | **Yes — plan it in.** Verified: `api/.config/dotnet-tools.json` pins `dotnet-ef 10.0.5`, and `dotnet ef dbcontext info --project E3A.Infrastructure --startup-project E3A.Api` succeeds **once two environment variables are set** (`ConnectionStrings__DbConnectionString`, `CoreFirebaseServiceAccountJson`). | Without them the design-time host throws: `AddCoreNotifications` requires `CoreFirebaseServiceAccountJson`, and the fallback activation path fails because `AppDbContext(DbContextOptions, IMediator)` is not activatable without DI. Exact recipe in **Migration**. No `IDesignTimeDbContextFactory` is needed, so none is created. |
| 22 | The migration is the repo's first — it will contain the whole Identity/OTP/Notifications/Audit baseline. | Accept, and name it **`initial`** per Morabh's proven convention (verified at `D:/Personal/Morabh/repos/apis/Morabh.Infrastructure/Migrations`): the first migration is `initial` and carries the full baseline; subsequent migrations are named `initial-002`, `initial-003`, … | There is no earlier migration to build on; splitting the baseline out would mean hand-writing a migration, which is worse. The file is tool-generated and must not be hand-edited. Future slices follow the same `initial-NNN` naming. |
| 23 | A JSON body missing `displayName` entirely returns ASP.NET's 400 ProblemDetails, not our 422 code. | **Accept.** Request records keep non-nullable `string DisplayName`; `SuppressModelStateInvalidFilter` is **not** configured. | `[ApiController]` implicit model-state validation runs before MediatR. Empty/whitespace values — the realistic client error — still reach the validator and produce `ENGINEER_DISPLAY_NAME_REQUIRED` at 422. Suppressing the filter is a solution-wide API-contract change, out of scope. |
| 24 | Validation failure status is 422, not 400. | **422.** | `Core.CQRS.ValidationBehaviour` throws `ValidationBehaviourException`; `CoreExceptionMiddleware` writes its status verbatim. Changing it means editing a vendored library shared with Identity/OTP/Notifications. |
| 25 | `Tags` may be absent in the HTTP body. | API request record uses `List<string>? Tags`; the controller maps `request.Tags ?? []`. The **command** property is non-nullable `List<string> Tags`. | Keeps every downstream layer (validator, entity, EF) free of null handling, and puts the one null check at the boundary where it belongs. |
| 26 | Display names with no ASCII letters or digits (e.g. Arabic-only) would derive an empty slug. | Validator rule: display name must contain at least one `char.IsAsciiLetterOrDigit` → `ENGINEER_DISPLAY_NAME_INVALID` (422). No regex. | Guarantees `EngineerSlugGenerator` never returns an empty string, so the handler needs no empty-slug branch. A regex would risk SonarAnalyzer S6444 under `TreatWarningsAsErrors=true`; `char.IsAsciiLetterOrDigit` has no such risk. |
| 27 | Which Core.Validation extensions actually exist? | `ValidateRequired`, `ValidateMaxLength`, `ValidateMinLength`, `ValidateListMaxItems`, `ValidateNotEmptyList` (+ email/url/digits/phone/file/numeric). **`ValidateOnlyEnglishText` and `ValidateListContainOneItem` named in the skill do NOT exist.** Do not call them. | Read from `core-libraries/Core.Validation/Extensions/`. Compile-verified that `RuleFor(x => x.Tags).ValidateListMaxItems(10, code)` binds against a `List<string>` property (`IRuleBuilder<T, out TProperty>` is covariant) and that `RuleForEach(x => x.Tags).ValidateRequired(code).ValidateMaxLength(30, code)` compiles clean under `TreatWarningsAsErrors=true`. |
| 28 | `DbSet` property and CS8618 under `TreatWarningsAsErrors=true`. | `public DbSet<Engineer> Engineers { get; set; }` — **no** `= null!`. | Compile-probed: EF Core ships a diagnostic suppressor for uninitialized `DbSet` properties; zero warnings. Matches `CoreDbContext` exactly (skill §6.2). |
| 29 | Test project changes. | **None.** `E3A.Tests.csproj` is untouched. | It already references `E3A.Application` + `E3A.Domain` and carries xUnit / NSubstitute / FluentAssertions **6.12.2**. `NotFoundCoreException`, `ICurrentUserService`, `IOptions<T>` and `Options.Create` all resolve transitively. Do not "upgrade" FluentAssertions to 7.x — the repo pins 6.12.2 deliberately and every assertion in this plan is 6.x-compatible. |
| 30 | Test factory vs. `CreationDate` determinism. | `EngineerFactory.Draft(..., DateTimeOffset? creationDate = null)` assigns `engineer.CreationDate` when supplied. | `AuditEntity.CreationDate` has a public setter — this is not reflection and not forbidden. Two engineers created in the same test otherwise share a timestamp and make the ordering assertion flaky (conventions §8). |

## Existing code touched

| File | Change |
|------|--------|
| `api/E3A.Domain/` | — nothing modified; new folder `Engineers/` only. |
| `api/E3A.Application/DependencyInjection.cs` | Signature → `AddApplication(this IServiceCollection services, IConfiguration configuration)`. Rename the MediatR lambda parameter `configuration` → `mediatRConfiguration` (Decision 20). Add, before `return services;`: `services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);` and `services.Configure<EngineerLimitsOptions>(configuration.GetSection(EngineerLimitsOptions.SectionName));`. Add `using E3A.Application.Options;` and `using FluentValidation;`. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Append an `// Engineers` comment-separated group with the 12 constants from **Error codes**. Do not touch the `// Identity` group. |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | Add `public DbSet<Engineer> Engineers { get; set; }`; call `ConfigureEngineers(modelBuilder);` in `OnModelCreating` **before** `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);`; add the private static `ConfigureEngineers` method (body in **Persistence**); add `modelBuilder.Entity<Engineer>().HasQueryFilter(x => !x.IsDeleted);` inside `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. Add `using E3A.Domain.Engineers;` and `using System.Text.Json;`. Keep the existing XML doc comments. |
| `api/E3A.Infrastructure/DependencyInjection.cs` | Add `using E3A.Domain.Engineers;` + `using E3A.Infrastructure.Engineers;`; inside `AddInfrastructure`, before `return services;`: `services.AddScoped<IEngineerRepository, EngineerRepository>();`. Do **not** re-register the open generic `IRepository<>` — `AddCoreEntityFrameworkCore` already does. |
| `api/E3A.Api/Program.cs` | **One line only**: `builder.Services.AddApplication();` → `builder.Services.AddApplication(builder.Configuration);`. Middleware order, regions and every other line unchanged. |
| `api/E3A.Api/appsettings.json` | Add a top-level `"EngineerLimits": { "MaxEngineersPerCreator": 50 }` section. |
| `api/E3A.Api/Resources/Messages.en.resx` | Append the 12 `<data>` elements from **Error codes**, before `</root>`, in the existing element shape. |
| `api/E3A.Api/Resources/Messages.ar.resx` | Append the same 12 keys with the Arabic values. |
| `docs/implementation-plan.md` | Replace the `engineers / teams` data-model bullet with: `` - `engineers` / `teams` (separate tables, same shape): Id, OwnerUserId, Slug (unique, kebab-case of DisplayName; the `{githublogin}` segment lives in the plugin name `e3a-{githublogin}-{item-slug}`, not in the row), DisplayName, Description, Tags(json), Status(Draft|Published|Removed), DraftManifestJson, LatestVersionId, InstallCount `` — per `.claude/rules/docs-sync.md` (Decision 6). Change nothing else in the file. |

No other existing file is modified. `core-libraries/`, every `.csproj`, `Directory.Packages.props`, `E3A.slnx`, `Directory.Build.props`, `appsettings.Development.json` and `E3A.Domain/Identity/*` are byte-identical afterwards.

## Files to create

All paths relative to `D:/Personal/_e3a/`. Every file: file-scoped namespace matching its folder; no comments except the one noted on `Engineer`; `DateTimeOffset` only; `[]` / `[.. spread]` for collections; `.ConfigureAwait(false)` on every `await` outside test method bodies; type declarations on one line (constitution §1.1); block-bodied methods with braces (§1.2); braces on every `if` (§1.3).

### Domain

| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/E3A.Domain/Engineers/EngineerStatus.cs` | `public enum EngineerStatus` — `namespace E3A.Domain.Engineers;` | `{ Draft, Published, Removed }`. **No** `EngineerStatusExtensions` — nothing in this slice consumes one and dead code is a defect. |
| 2 | `api/E3A.Domain/Engineers/Engineer.cs` | `public class Engineer : AuditEntity` — `using Core.DDD.Entities;` | Constants, properties, `private Engineer(Guid id, Guid? createdBy) : base(id, createdBy) { }`, `static Create`, `UpdateMetadata`, `Remove`. Full text in **Domain behaviour**. |
| 3 | `api/E3A.Domain/Engineers/EngineerSlugGenerator.cs` | `public static class EngineerSlugGenerator` — `namespace E3A.Domain.Engineers;` | `public static string Generate(string displayName)`. Algorithm in **Domain behaviour**. No regex. |
| 4 | `api/E3A.Domain/Engineers/IEngineerRepository.cs` | `public interface IEngineerRepository : IRepository<Engineer>` — `using Core.DDD.Repositories;` | Empty body `{ }`. **No added members** — `CountAsync`, `FindAsync`, `FirstOrDefaultAsync`, `GetByIdAsync`, `AddAsync`, `Update`, `SaveChangesAsync` cover every need. |

### Application

| # | Path | Type | Contract |
|---|------|------|----------|
| 5 | `api/E3A.Application/Options/EngineerLimitsOptions.cs` | `public sealed class EngineerLimitsOptions` — `namespace E3A.Application.Options;` | `public const string SectionName = "EngineerLimits";` · `public int MaxEngineersPerCreator { get; set; }`. Nothing else. |
| 6 | `api/E3A.Application/Engineers/Shared/EngineerResult.cs` | `public sealed record EngineerResult` — `namespace E3A.Application.Engineers.Shared;` | One line: `EngineerResult(Guid Id, string Slug, string DisplayName, string? Description, List<string> Tags, string Status, Guid? LatestVersionId, int InstallCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)`. All fields client-facing; no `LocalizedText`, therefore no `.Localized()` and no admin variant. `DraftManifestJson` is deliberately **not** exposed. |
| 7 | `api/E3A.Application/Engineers/Shared/EngineerResultGenerator.cs` | `public static class EngineerResultGenerator` — same namespace, `using E3A.Domain.Engineers;` | `public static EngineerResult Generate(Engineer engineer)` → `return new EngineerResult(engineer.Id, engineer.Slug, engineer.DisplayName, engineer.Description, engineer.Tags, engineer.Status.ToString(), engineer.LatestVersionId, engineer.InstallCount, engineer.CreationDate, engineer.UpdationDate);` |
| 8 | `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerCommand.cs` | `namespace E3A.Application.Engineers.CreateEngineer;`, `using MediatR;` | `public sealed record CreateEngineerCommand(string DisplayName, string? Description, List<string> Tags) : IRequest<EngineerResult>;` |
| 9 | `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs` | `public sealed class CreateEngineerValidator : AbstractValidator<CreateEngineerCommand>` | Rules in **Validators**. |
| 10 | `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs` | `public sealed class CreateEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IOptions<EngineerLimitsOptions> engineerLimitsOptions) : IRequestHandler<CreateEngineerCommand, EngineerResult>` | Steps in **Handlers**. |
| 11 | `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerCommand.cs` | `namespace E3A.Application.Engineers.UpdateEngineer;` | `public sealed record UpdateEngineerCommand(Guid EngineerId, string DisplayName, string? Description, List<string> Tags) : IRequest<EngineerResult>;` |
| 12 | `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerValidator.cs` | `public sealed class UpdateEngineerValidator : AbstractValidator<UpdateEngineerCommand>` | Rules in **Validators**. |
| 13 | `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs` | `public sealed class UpdateEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<UpdateEngineerCommand, EngineerResult>` | Steps in **Handlers**. |
| 14 | `api/E3A.Application/Engineers/ListMyEngineers/ListMyEngineersQuery.cs` | `namespace E3A.Application.Engineers.ListMyEngineers;` | `public sealed record ListMyEngineersQuery : IRequest<List<EngineerResult>>;` (no parameters). |
| 15 | `api/E3A.Application/Engineers/ListMyEngineers/ListMyEngineersQueryHandler.cs` | `public sealed class ListMyEngineersQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<ListMyEngineersQuery, List<EngineerResult>>` | Steps in **Handlers**. |
| 16 | `api/E3A.Application/Engineers/GetEngineer/GetEngineerQuery.cs` | `namespace E3A.Application.Engineers.GetEngineer;` | `public sealed record GetEngineerQuery(Guid EngineerId) : IRequest<EngineerResult>;` |
| 17 | `api/E3A.Application/Engineers/GetEngineer/GetEngineerQueryValidator.cs` | `public sealed class GetEngineerQueryValidator : AbstractValidator<GetEngineerQuery>` | `RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);` |
| 18 | `api/E3A.Application/Engineers/GetEngineer/GetEngineerQueryHandler.cs` | `public sealed class GetEngineerQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<GetEngineerQuery, EngineerResult>` | Steps in **Handlers**. |
| 19 | `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerCommand.cs` | `namespace E3A.Application.Engineers.DeleteEngineer;` | `public sealed record DeleteEngineerCommand(Guid EngineerId) : IRequest;` (no result). |
| 20 | `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerValidator.cs` | `public sealed class DeleteEngineerValidator : AbstractValidator<DeleteEngineerCommand>` | `RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);` |
| 21 | `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerHandler.cs` | `public sealed class DeleteEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<DeleteEngineerCommand>` — `public async Task Handle(DeleteEngineerCommand request, CancellationToken cancellationToken)` | Steps in **Handlers**. |

Usings required by the Application files: `Core.Errors`, `Core.Identity.Tokens.CurrentUser`, `Core.Validation.Extensions`, `E3A.Application.Exceptions`, `E3A.Application.Engineers.Shared`, `E3A.Application.Options`, `E3A.Domain.Engineers`, `FluentValidation`, `MediatR`, `Microsoft.Extensions.Options`.

### Infrastructure

| # | Path | Type | Contract |
|---|------|------|----------|
| 22 | `api/E3A.Infrastructure/Engineers/EngineerRepository.cs` | `public class EngineerRepository(AppDbContext context) : Repository<Engineer>(context), IEngineerRepository { }` — `namespace E3A.Infrastructure.Engineers;`, `using Core.EntityFrameworkCore.Repositories; using E3A.Domain.Engineers; using E3A.Infrastructure.Data.Context;` | Empty body. `Repository<T>` supplies all 13 members. No `SaveChangesAsync` override. |
| 23 | `api/E3A.Infrastructure/Data/Migrations/*` | **Tool-generated** — three files: `<timestamp>_initial.cs`, `<timestamp>_initial.Designer.cs`, `AppDbContextModelSnapshot.cs` | Produced by the command in **Migration**. Never hand-written, never hand-edited. |

### API

| # | Path | Type | Contract |
|---|------|------|----------|
| 24 | `api/E3A.Api/Controllers/Engineers/Requests.cs` | `namespace E3A.Api.Controllers.Engineers;` | `public sealed record CreateEngineerRequest(string DisplayName, string? Description, List<string>? Tags);`<br>`public sealed record UpdateEngineerRequest(string DisplayName, string? Description, List<string>? Tags);` |
| 25 | `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | `[ApiController] [Route("api/engineers")] [Authorize] public class EngineersController(IMediator mediator) : ControllerBase` | Five actions, listed in **API surface**. Thin: map → `mediator.Send` → result. No business logic, no try/catch, no `ICurrentUserService`. `CancellationToken cancellationToken` on every action, passed to `Send`. |

### Tests

Namespace mirrors the folder (`E3A.Tests.Engineers.CreateEngineer`, …). `sealed class` test classes.

| # | Path |
|---|------|
| 26 | `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` |
| 27 | `api/E3A.Tests/Engineers/EngineerTests.cs` |
| 28 | `api/E3A.Tests/Engineers/EngineerSlugGeneratorTests.cs` |
| 29 | `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerValidatorTests.cs` |
| 30 | `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerHandlerTests.cs` |
| 31 | `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerValidatorTests.cs` |
| 32 | `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerHandlerTests.cs` |
| 33 | `api/E3A.Tests/Engineers/ListMyEngineers/ListMyEngineersQueryHandlerTests.cs` |
| 34 | `api/E3A.Tests/Engineers/GetEngineer/GetEngineerQueryValidatorTests.cs` |
| 35 | `api/E3A.Tests/Engineers/GetEngineer/GetEngineerQueryHandlerTests.cs` |
| 36 | `api/E3A.Tests/Engineers/DeleteEngineer/DeleteEngineerValidatorTests.cs` |
| 37 | `api/E3A.Tests/Engineers/DeleteEngineer/DeleteEngineerHandlerTests.cs` |

`EngineerFactory` — `public static class EngineerFactory`, `namespace E3A.Tests.Engineers.Shared;`:

```csharp
public const string DefaultDisplayName = "Dive Backend Engineer";
public const string DefaultSlug = "dive-backend-engineer";

public static Engineer Draft(Guid ownerUserId, string displayName = DefaultDisplayName, string slug = DefaultSlug, DateTimeOffset? creationDate = null)
{
    var engineer = Engineer.Create(ownerUserId, slug, displayName, "A backend engineer.", ["dotnet", "ddd"]);

    if (creationDate != null)
    {
        engineer.CreationDate = creationDate.Value;
    }

    return engineer;
}
```

No `new Engineer(...)`, no reflection, anywhere in the test project.

Every handler test class follows conventions §3 exactly — substitutes as `private readonly` field initialisers, constructor wires `_sut` (and the default `_currentUserService.UserId.Returns(_ownerUserId)`) and nothing else:

```csharp
private readonly IEngineerRepository _engineerRepository = Substitute.For<IEngineerRepository>();
private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
private readonly Guid _ownerUserId = Guid.NewGuid();
private readonly CreateEngineerHandler _sut;
```

`CreateEngineerHandlerTests` additionally passes `Options.Create(new EngineerLimitsOptions { MaxEngineersPerCreator = 2 })`.

## Error codes

All 12 go into the existing `api/E3A.Application/Exceptions/ErrorCodes.cs` under a single `// Engineers` comment separator, as `public const string`.

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `EngineerNotFound` | `ENGINEER_NOT_FOUND` | `GetEngineerQueryHandler`, `UpdateEngineerHandler`, `DeleteEngineerHandler` | `NotFoundCoreException` | 404 |
| `EngineerNotOwned` | `ENGINEER_NOT_OWNED` | `GetEngineerQueryHandler`, `UpdateEngineerHandler`, `DeleteEngineerHandler` | `ForbiddenCoreException` | 403 |
| `EngineerSlugTaken` | `ENGINEER_SLUG_TAKEN` | `CreateEngineerHandler` | `ConflictCoreException` | 409 |
| `EngineerLimitReached` | `ENGINEER_LIMIT_REACHED` | `CreateEngineerHandler` | `BusinessRuleViolationCoreException` with `context: new Dictionary<string, object> { ["limit"] = … }` | 400 |
| `EngineerIdRequired` | `ENGINEER_ID_REQUIRED` | `UpdateEngineerValidator`, `GetEngineerQueryValidator`, `DeleteEngineerValidator` | `ValidationBehaviourException` | 422 |
| `EngineerDisplayNameRequired` | `ENGINEER_DISPLAY_NAME_REQUIRED` | `CreateEngineerValidator`, `UpdateEngineerValidator` | `ValidationBehaviourException` | 422 |
| `EngineerDisplayNameTooLong` | `ENGINEER_DISPLAY_NAME_TOO_LONG` | both create/update validators | `ValidationBehaviourException` | 422 |
| `EngineerDisplayNameInvalid` | `ENGINEER_DISPLAY_NAME_INVALID` | both create/update validators | `ValidationBehaviourException` | 422 |
| `EngineerDescriptionTooLong` | `ENGINEER_DESCRIPTION_TOO_LONG` | both create/update validators | `ValidationBehaviourException` | 422 |
| `EngineerTooManyTags` | `ENGINEER_TOO_MANY_TAGS` | both create/update validators | `ValidationBehaviourException` | 422 |
| `EngineerTagRequired` | `ENGINEER_TAG_REQUIRED` | both create/update validators | `ValidationBehaviourException` | 422 |
| `EngineerTagTooLong` | `ENGINEER_TAG_TOO_LONG` | both create/update validators | `ValidationBehaviourException` | 422 |

Reused, already present in `ErrorCodes` **and** both resx files — do not duplicate: `UserNotAuthenticated` (`USER_NOT_AUTHENTICATED`, `UnauthorizedCoreException`, 401), thrown by all five handlers.

Resource strings — the key **is** the code value. `{limit}` is substituted at runtime by `Localizer.GetMessage` from the exception `Context`; keep the placeholder intact in both languages. Arabic without tashkeel.

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|-----|--------------------|--------------------|
| `ENGINEER_NOT_FOUND` | `We couldn't find that engineer.` | `لم نتمكن من العثور على هذا المهندس.` |
| `ENGINEER_NOT_OWNED` | `This engineer belongs to another creator.` | `هذا المهندس يخص منشئا اخر.` |
| `ENGINEER_SLUG_TAKEN` | `An engineer with a similar name already exists. Choose a different display name.` | `يوجد مهندس بنفس الاسم بالفعل. اختر اسم عرض مختلفا.` |
| `ENGINEER_LIMIT_REACHED` | `You have reached the limit of {limit} engineers.` | `لقد وصلت الى الحد الاقصى وهو {limit} مهندس.` |
| `ENGINEER_ID_REQUIRED` | `An engineer identifier is required.` | `معرف المهندس مطلوب.` |
| `ENGINEER_DISPLAY_NAME_REQUIRED` | `A display name is required.` | `اسم العرض مطلوب.` |
| `ENGINEER_DISPLAY_NAME_TOO_LONG` | `A display name must not exceed 100 characters.` | `يجب الا يتجاوز اسم العرض 100 حرف.` |
| `ENGINEER_DISPLAY_NAME_INVALID` | `A display name must contain at least one English letter or digit.` | `يجب ان يحتوي اسم العرض على حرف انجليزي او رقم واحد على الاقل.` |
| `ENGINEER_DESCRIPTION_TOO_LONG` | `A description must not exceed 500 characters.` | `يجب الا يتجاوز الوصف 500 حرف.` |
| `ENGINEER_TOO_MANY_TAGS` | `An engineer must not have more than 10 tags.` | `يجب الا يزيد عدد الوسوم عن 10 وسوم.` |
| `ENGINEER_TAG_REQUIRED` | `A tag must not be empty.` | `الوسم لا يمكن ان يكون فارغا.` |
| `ENGINEER_TAG_TOO_LONG` | `A tag must not exceed 30 characters.` | `يجب الا يتجاوز الوسم 30 حرفا.` |

Element shape, matching the existing entries exactly:

```xml
<data name="ENGINEER_NOT_FOUND" xml:space="preserve">
  <value>We couldn't find that engineer.</value>
</data>
```

## Domain behaviour

`Engineer.cs` — exact shape. One comment is permitted, on the constants block, because the WHY is hidden (the numbers are shared with the EF column widths).

```csharp
public class Engineer : AuditEntity
{
    // Schema invariants: the validator and the EF column widths must never disagree.
    public const int DisplayNameMaxLength = 100;
    public const int DescriptionMaxLength = 500;
    public const int SlugMaxLength = 100;
    public const int MaxTags = 10;
    public const int TagMaxLength = 30;

    public Guid OwnerUserId { get; private set; }
    public string Slug { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? Description { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public EngineerStatus Status { get; private set; }
    public string? DraftManifestJson { get; private set; }
    public Guid? LatestVersionId { get; private set; }
    public int InstallCount { get; private set; }

    private Engineer(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Engineer Create(Guid ownerUserId, string slug, string displayName, string? description, List<string> tags)
    {
        return new Engineer(Guid.NewGuid(), ownerUserId)
        {
            OwnerUserId = ownerUserId,
            Slug = slug,
            DisplayName = displayName,
            Description = description,
            Tags = [.. tags],
            Status = EngineerStatus.Draft,
            InstallCount = 0,
            CreationDate = DateTimeOffset.UtcNow,
            UpdationDate = DateTimeOffset.UtcNow,
        };
    }

    public void UpdateMetadata(string displayName, string? description, List<string> tags)
    {
        DisplayName = displayName;
        Description = description;
        Tags = [.. tags];
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Remove()
    {
        Status = EngineerStatus.Removed;
        SoftDelete();
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
```

Invariants the implementer must preserve:

- `Create` is the only construction path; the constructor is `private`.
- `CreationDate`/`UpdationDate` are set explicitly from `DateTimeOffset.UtcNow` because `AuditEntity`'s field initialisers use local `DateTimeOffset.Now`.
- `Tags` is always **copied** (`[.. tags]`), never aliased to the caller's list.
- `Slug`, `OwnerUserId`, `DraftManifestJson`, `LatestVersionId` and `InstallCount` have no mutator in this slice — a new draft is always `Draft`, `InstallCount = 0`, the two nullables `null`.
- Every mutating method sets `UpdationDate = DateTimeOffset.UtcNow`; `Remove()` calls `SoftDelete()` and never assigns `IsDeleted` directly.
- Handlers never assign a property on `Engineer` — only `Create`, `UpdateMetadata`, `Remove`.

`EngineerSlugGenerator.Generate(string displayName)` — exact algorithm, no regex (Decision 26):

1. Build a `System.Text.StringBuilder`; iterate `displayName` character by character.
2. If `char.IsAsciiLetterOrDigit(character)` → append `char.ToLowerInvariant(character)`.
3. Otherwise → append `'-'` **only if** the builder is non-empty and its last character is not already `'-'` (this collapses runs and drops any leading separator).
4. After the loop, `TrimEnd('-')` the result.
5. If the result is longer than `Engineer.SlugMaxLength`, take the first `SlugMaxLength` characters and `TrimEnd('-')` again.
6. Return the string.

Worked examples the tests pin: `"Dive Backend Engineer"` → `"dive-backend-engineer"`; `"  .NET  DDD/CQRS Engineer! "` → `"net-ddd-cqrs-engineer"`; `"--Hello--"` → `"hello"`; `"a@@@b"` → `"a-b"`; `"مهندس Backend"` → `"backend"`; `new string('a', 150)` → 100 `a` characters.

## Validators

`CreateEngineerValidator` — four `RuleFor` chains plus one `RuleForEach`, in this order:

```csharp
RuleFor(x => x.DisplayName)
    .ValidateRequired(ErrorCodes.EngineerDisplayNameRequired)
    .ValidateMaxLength(Engineer.DisplayNameMaxLength, ErrorCodes.EngineerDisplayNameTooLong);

RuleFor(x => x.DisplayName)
    .Must(displayName => displayName.Any(char.IsAsciiLetterOrDigit))
    .WithMessage("{PropertyName} must contain at least one English letter or digit.")
    .WithErrorCode(ErrorCodes.EngineerDisplayNameInvalid)
    .When(x => !string.IsNullOrWhiteSpace(x.DisplayName));

RuleFor(x => x.Description)
    .ValidateMaxLength(Engineer.DescriptionMaxLength, ErrorCodes.EngineerDescriptionTooLong);

RuleFor(x => x.Tags)
    .ValidateListMaxItems(Engineer.MaxTags, ErrorCodes.EngineerTooManyTags);

RuleForEach(x => x.Tags)
    .ValidateRequired(ErrorCodes.EngineerTagRequired)
    .ValidateMaxLength(Engineer.TagMaxLength, ErrorCodes.EngineerTagTooLong);
```

The `.When(...)` **must** stay on its own single-rule `RuleFor` — appended to the first chain it would disable the required rule as well.

`UpdateEngineerValidator` — the identical five rules, preceded by:

```csharp
RuleFor(x => x.EngineerId).ValidateRequired(ErrorCodes.EngineerIdRequired);
```

No shared base validator, no rule-set extraction: duplication across two use-case folders is the codebase's shape.

`GetEngineerQueryValidator` / `DeleteEngineerValidator` — the `EngineerId` rule only.

## Handlers

Every handler: `sealed class`, primary constructor on one line, no `try`/`catch`, `.ConfigureAwait(false)` on every `await`, exactly one `SaveChangesAsync` on the success path and none on any throwing path. The current-user guard is always step 1:

```csharp
var userId = currentUserService.UserId;

if (userId == null || userId == Guid.Empty)
{
    throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
}

var ownerUserId = userId.Value;
```

**`CreateEngineerHandler.Handle`**

1. Current-user guard (above).
2. `var limits = engineerLimitsOptions.Value;`
3. `var ownedEngineerCount = await engineerRepository.CountAsync(cancellationToken, x => x.OwnerUserId == ownerUserId).ConfigureAwait(false);`
4. `if (ownedEngineerCount >= limits.MaxEngineersPerCreator) { throw new BusinessRuleViolationCoreException(ErrorCodes.EngineerLimitReached, context: new Dictionary<string, object> { ["limit"] = limits.MaxEngineersPerCreator }); }`
5. `var slug = EngineerSlugGenerator.Generate(request.DisplayName);`
6. `var existingEngineer = await engineerRepository.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
7. `if (existingEngineer != null) { throw new ConflictCoreException(ErrorCodes.EngineerSlugTaken); }`
8. `var engineer = Engineer.Create(ownerUserId, slug, request.DisplayName, request.Description, request.Tags);`
9. `await engineerRepository.AddAsync(engineer, cancellationToken).ConfigureAwait(false);`
10. `await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);`
11. `return EngineerResultGenerator.Generate(engineer);`

Order matters and is tested: the cap is checked **before** the slug lookup.

**`UpdateEngineerHandler.Handle`**

1. Current-user guard.
2. `var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken).ConfigureAwait(false);` (tracking)
3. `if (engineer == null) { throw new NotFoundCoreException(ErrorCodes.EngineerNotFound); }`
4. `if (engineer.OwnerUserId != ownerUserId) { throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned); }`
5. `engineer.UpdateMetadata(request.DisplayName, request.Description, request.Tags);`
6. `engineerRepository.Update(engineer);`
7. `await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);`
8. `return EngineerResultGenerator.Generate(engineer);`

The slug is **not** recomputed (Decision 5).

**`ListMyEngineersQueryHandler.Handle`**

1. Current-user guard.
2. `var engineers = await engineerRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
3. ```csharp
   return engineers
       .OrderByDescending(x => x.CreationDate)
       .Select(EngineerResultGenerator.Generate)
       .ToList();
   ```

No `?? []` (Decision 17). No status filter — the global soft-delete filter already hides removed rows, and the owner sees every remaining status.

**`GetEngineerQueryHandler.Handle`**

1. Current-user guard.
2. `var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken, asNoTracking: true).ConfigureAwait(false);`
3. `null` → `NotFoundCoreException(ErrorCodes.EngineerNotFound)`.
4. not owner → `ForbiddenCoreException(ErrorCodes.EngineerNotOwned)`.
5. `return EngineerResultGenerator.Generate(engineer);`

**`DeleteEngineerHandler.Handle`** (returns `Task`)

1. Current-user guard.
2. `GetByIdAsync(request.EngineerId, cancellationToken)` (tracking).
3. `null` → `NotFoundCoreException`. 4. not owner → `ForbiddenCoreException`.
5. `engineer.Remove();`
6. `engineerRepository.Update(engineer);` — **never** `engineerRepository.Delete(engineer)`.
7. `await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);`

## Persistence

`ConfigureEngineers` — exact body to add to `AppDbContext`, verified against a real EF Core 10 SqlServer model build:

```csharp
private static void ConfigureEngineers(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Engineer>(builder =>
    {
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Engineer.SlugMaxLength);
        builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.OwnerUserId);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(Engineer.DisplayNameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Engineer.DescriptionMaxLength);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(x => x.Tags)
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                   v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
               .HasMaxLength(400);
    });
}
```

Notes the implementer must not "improve":

- `new List<string>()` — **not** `[]`. A collection expression is illegal inside an expression tree and will not compile.
- The unique index **must** carry `HasFilter("[IsDeleted] = 0")` (Decision 7).
- No FK to `AspNetUsers` from `OwnerUserId`: `User` is an `IdentityUser<Guid>` outside the aggregate, and the locked model treats the link as an id, not a navigation. A plain index is the whole mapping.
- Register the query filter in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` and nowhere else; never append `.Where(x => !x.IsDeleted)` in a query.

Resulting table (from the verified model build): `Engineers(Id, OwnerUserId, Slug nvarchar(100), DisplayName nvarchar(100), Description nvarchar(500) NULL, Tags nvarchar(400), Status nvarchar(50), DraftManifestJson nvarchar(max) NULL, LatestVersionId uniqueidentifier NULL, InstallCount int, IsDeleted bit, DeletedAt datetimeoffset NULL, CreatedBy, CreationDate, UpdatedBy, UpdationDate)`.

## Migration

Run **after** the entity, the `DbSet` and `ConfigureEngineers` compile. From `D:/Personal/_e3a/api`:

PowerShell:
```powershell
dotnet tool restore
$env:ConnectionStrings__DbConnectionString = "Server=localhost;Database=E3A;Trusted_Connection=True;TrustServerCertificate=True"
$env:CoreFirebaseServiceAccountJson = "{}"
dotnet ef migrations add initial --project E3A.Infrastructure --startup-project E3A.Api --output-dir Data/Migrations
```

Both variables are throwaway design-time placeholders — `migrations add` never opens a connection. Do **not** put them in any committed file, do **not** run `dotnet ef database update`, and do **not** hand-edit the generated files.

Expected: a first migration containing the ASP.NET Identity tables, the `Core.*` tables (`Otps`, `UserDevices`, `Notifications`, `NotificationTemplates`, `AuditLogs`) and `Engineers` with `IX_Engineers_Slug` (unique, filtered) and `IX_Engineers_OwnerUserId`. That baseline breadth is expected and correct — no migration existed before this slice (Decision 22).

Migration naming convention for this repo, established here: the first migration is `initial`; every subsequent migration in any future slice is `initial-002`, `initial-003`, … (zero-padded three digits), mirroring the proven Morabh convention.

## API surface

| Method | Route | Policy | Request record | MediatR message | Success | Failures |
|--------|-------|--------|----------------|-----------------|---------|----------|
| POST | `/api/engineers` | `[Authorize]` | `CreateEngineerRequest` | `CreateEngineerCommand(request.DisplayName, request.Description, request.Tags ?? [])` → `EngineerResult` | `201 Created` + `Location`, via `CreatedAtAction(nameof(GetEngineer), new { engineerId = result.Id }, result)` | 401 `USER_NOT_AUTHENTICATED` · 409 `ENGINEER_SLUG_TAKEN` · 400 `ENGINEER_LIMIT_REACHED` · 422 display-name/description/tag codes |
| GET | `/api/engineers` | `[Authorize]` | — | `ListMyEngineersQuery` → `List<EngineerResult>` | `200 Ok` | 401 |
| GET | `/api/engineers/{engineerId:guid}` | `[Authorize]` | — (`[FromRoute]`) | `GetEngineerQuery` → `EngineerResult` | `200 Ok` | 401 · 403 `ENGINEER_NOT_OWNED` · 404 `ENGINEER_NOT_FOUND` · 422 `ENGINEER_ID_REQUIRED` |
| PUT | `/api/engineers/{engineerId:guid}` | `[Authorize]` | `UpdateEngineerRequest` | `UpdateEngineerCommand(engineerId, request.DisplayName, request.Description, request.Tags ?? [])` → `EngineerResult` | `200 Ok` | 401 · 403 · 404 · 422 |
| DELETE | `/api/engineers/{engineerId:guid}` | `[Authorize]` | — (`[FromRoute]`) | `DeleteEngineerCommand` (no result) | `204 No Content` | 401 · 403 · 404 · 422 |

Action declaration order: `ListMyEngineers`, `GetEngineer`, `CreateEngineer`, `UpdateEngineer`, `DeleteEngineer`. The action name referenced by `CreatedAtAction` **must** be exactly `GetEngineer`; a mismatch throws `InvalidOperationException` at runtime. `GET /api/engineers` and `GET /api/engineers/{engineerId:guid}` do not conflict — the `:guid` constraint disambiguates.

`EngineerResult` JSON: `{ "id": guid, "slug": string, "displayName": string, "description": string|null, "tags": string[], "status": "Draft", "latestVersionId": guid|null, "installCount": int, "createdAt": ISO-8601 offset, "updatedAt": ISO-8601 offset }`.

## Test plan

Write exactly these, and only these. Naming, `_sut`/substitute conventions, unlabelled AAA blocks, no `.ConfigureAwait(false)` inside test method bodies, per `conventions/dotnet-testing.md`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `EngineerTests` | `Create_ShouldReturnDraftEngineer_WhenDataIsProvided` | `OwnerUserId`, `Slug`, `DisplayName`, `Description`, `Tags` equal the inputs; `Status` is `EngineerStatus.Draft`; `InstallCount` 0; `LatestVersionId` null; `DraftManifestJson` null; `IsDeleted` false; `Id` not `Guid.Empty`; `CreatedBy` equals the owner id |
| 2 | `EngineerTests` | `Create_ShouldStampUtcAuditDates_WhenEngineerIsCreated` | capture `var before = DateTimeOffset.UtcNow;`, then `CreationDate.Should().BeOnOrAfter(before)` and `UpdationDate.Should().BeOnOrAfter(before)` |
| 3 | `EngineerTests` | `Create_ShouldCopyTags_WhenSourceListIsMutatedAfterwards` | mutate the source `List<string>` after `Create`; `engineer.Tags` unchanged |
| 4 | `EngineerTests` | `UpdateMetadata_ShouldReplaceMetadata_WhenCalled` | `DisplayName`, `Description`, `Tags` updated; `Slug` and `OwnerUserId` unchanged; `UpdationDate.Should().BeOnOrAfter(before)` |
| 5 | `EngineerTests` | `UpdateMetadata_ShouldCopyTags_WhenSourceListIsMutatedAfterwards` | mutate the source list after the call; `engineer.Tags` unchanged |
| 6 | `EngineerTests` | `Remove_ShouldMarkRemovedAndSoftDeleted_WhenCalled` | `Status` is `EngineerStatus.Removed`; `IsDeleted` true; `UpdationDate.Should().BeOnOrAfter(before)` |
| 7 | `EngineerSlugGeneratorTests` | `Generate_ShouldReturnKebabCaseSlug_WhenDisplayNameHasMixedCaseAndSpaces` | `"Dive Backend Engineer"` → `"dive-backend-engineer"` |
| 8 | `EngineerSlugGeneratorTests` | `Generate_ShouldCollapseAndTrimSeparators_WhenDisplayNameHasPunctuation` — `[Theory]` `[InlineData("  .NET  DDD/CQRS Engineer! ", "net-ddd-cqrs-engineer")] [InlineData("--Hello--", "hello")] [InlineData("a@@@b", "a-b")]` | result equals the expected slug |
| 9 | `EngineerSlugGeneratorTests` | `Generate_ShouldDropNonAsciiCharacters_WhenDisplayNameIsNotEnglish` | `"مهندس Backend"` → `"backend"` |
| 10 | `EngineerSlugGeneratorTests` | `Generate_ShouldTruncateToMaxLength_WhenDisplayNameIsTooLong` | `new string('a', 150)` → length equals `Engineer.SlugMaxLength`; result has no trailing `'-'` |
| 11 | `CreateEngineerValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 12 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenDisplayNameIsMissing` — `[Theory]` `[InlineData(null)] [InlineData("")] [InlineData("   ")]` | `IsValid` false; `Errors` contains `ErrorCodes.EngineerDisplayNameRequired` |
| 13 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenDisplayNameExceedsMaxLength` | `new string('a', 101)`; contains `ErrorCodes.EngineerDisplayNameTooLong` |
| 14 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenDisplayNameHasNoAsciiLetterOrDigit` | `"مهندس"`; contains `ErrorCodes.EngineerDisplayNameInvalid` |
| 15 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenDescriptionExceedsMaxLength` | `new string('a', 501)`; contains `ErrorCodes.EngineerDescriptionTooLong` |
| 16 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenTagCountExceedsMaximum` | 11 tags; contains `ErrorCodes.EngineerTooManyTags` |
| 17 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenATagIsEmpty` | `["dotnet", "  "]`; contains `ErrorCodes.EngineerTagRequired` |
| 18 | `CreateEngineerValidatorTests` | `Validate_ShouldFail_WhenATagExceedsMaxLength` | one tag of `new string('a', 31)`; contains `ErrorCodes.EngineerTagTooLong` |
| 19 | `CreateEngineerHandlerTests` | `Handle_ShouldCreateEngineer_WhenSlugIsFreeAndUnderLimit` | `CountAsync` stubbed 0, `FirstOrDefaultAsync` stubbed `(Engineer?)null`; result `Slug` is `"dive-backend-engineer"`, `Status` is `nameof(EngineerStatus.Draft)`, `InstallCount` 0; `Received(1).AddAsync(Arg.Any<Engineer>(), Arg.Any<CancellationToken>())`; `Received(1).SaveChangesAsync(Arg.Any<CancellationToken>())` |
| 20 | `CreateEngineerHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `_currentUserService.UserId.Returns((Guid?)null)`; `ThrowAsync<UnauthorizedCoreException>().Where(x => x.ErrorCode == ErrorCodes.UserNotAuthenticated)`; `DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>())` |
| 21 | `CreateEngineerHandlerTests` | `Handle_ShouldThrowBusinessRuleViolation_WhenCreatorReachedTheLimit` | `CountAsync` stubbed to the configured maximum (2); `ThrowAsync<BusinessRuleViolationCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerLimitReached)`; `DidNotReceive().AddAsync(...)`; `DidNotReceive().SaveChangesAsync(...)` |
| 22 | `CreateEngineerHandlerTests` | `Handle_ShouldThrowConflict_WhenSlugIsAlreadyTaken` | `FirstOrDefaultAsync` returns an existing engineer; `ThrowAsync<ConflictCoreException>().Where(x => x.ErrorCode == ErrorCodes.EngineerSlugTaken)`; `DidNotReceive().AddAsync(...)`; `DidNotReceive().SaveChangesAsync(...)` |
| 23 | `UpdateEngineerValidatorTests` | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid` true |
| 24 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | `Guid.Empty`; contains `ErrorCodes.EngineerIdRequired` |
| 25 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenDisplayNameIsMissing` — `[Theory]` `[InlineData(null)] [InlineData("")] [InlineData("   ")]` | contains `ErrorCodes.EngineerDisplayNameRequired` |
| 26 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenDisplayNameExceedsMaxLength` | contains `ErrorCodes.EngineerDisplayNameTooLong` |
| 27 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenDisplayNameHasNoAsciiLetterOrDigit` | contains `ErrorCodes.EngineerDisplayNameInvalid` |
| 28 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenDescriptionExceedsMaxLength` | contains `ErrorCodes.EngineerDescriptionTooLong` |
| 29 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenTagCountExceedsMaximum` | contains `ErrorCodes.EngineerTooManyTags` |
| 30 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenATagIsEmpty` | contains `ErrorCodes.EngineerTagRequired` |
| 31 | `UpdateEngineerValidatorTests` | `Validate_ShouldFail_WhenATagExceedsMaxLength` | contains `ErrorCodes.EngineerTagTooLong` |
| 32 | `UpdateEngineerHandlerTests` | `Handle_ShouldUpdateMetadata_WhenCallerIsOwner` | entity `DisplayName`/`Description`/`Tags` updated; `Slug` unchanged (`EngineerFactory.DefaultSlug`); result mirrors the entity; `Received(1).Update(engineer)`; `Received(1).SaveChangesAsync(...)` |
| 33 | `UpdateEngineerHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` with `ErrorCodes.UserNotAuthenticated`; `DidNotReceive().SaveChangesAsync(...)` |
| 34 | `UpdateEngineerHandlerTests` | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `GetByIdAsync` → `(Engineer?)null`; `NotFoundCoreException` with `ErrorCodes.EngineerNotFound`; `DidNotReceive().SaveChangesAsync(...)` |
| 35 | `UpdateEngineerHandlerTests` | `Handle_ShouldThrowForbidden_WhenCallerIsNotOwner` | engineer built for another owner id; `ForbiddenCoreException` with `ErrorCodes.EngineerNotOwned`; `DidNotReceive().Update(Arg.Any<Engineer>())`; `DidNotReceive().SaveChangesAsync(...)` |
| 36 | `ListMyEngineersQueryHandlerTests` | `Handle_ShouldReturnOwnedEngineersNewestFirst_WhenEngineersExist` | two engineers from `EngineerFactory.Draft` with explicit `creationDate` values (older first in the stub); result count 2 and `result[0].Id` is the newer engineer's id |
| 37 | `ListMyEngineersQueryHandlerTests` | `Handle_ShouldReturnEmptyList_WhenCallerOwnsNoEngineers` | `FindAsync` returns `[]`; result `.Should().BeEmpty()` |
| 38 | `ListMyEngineersQueryHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` with `ErrorCodes.UserNotAuthenticated` |
| 39 | `GetEngineerQueryValidatorTests` | `Validate_ShouldPass_WhenEngineerIdIsProvided` | `IsValid` true |
| 40 | `GetEngineerQueryValidatorTests` | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | contains `ErrorCodes.EngineerIdRequired` |
| 41 | `GetEngineerQueryHandlerTests` | `Handle_ShouldReturnEngineer_WhenCallerIsOwner` | result `Id`, `Slug`, `DisplayName`, `Status`, `CreatedAt`, `UpdatedAt` match the entity |
| 42 | `GetEngineerQueryHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` with `ErrorCodes.UserNotAuthenticated` |
| 43 | `GetEngineerQueryHandlerTests` | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `NotFoundCoreException` with `ErrorCodes.EngineerNotFound` |
| 44 | `GetEngineerQueryHandlerTests` | `Handle_ShouldThrowForbidden_WhenCallerIsNotOwner` | `ForbiddenCoreException` with `ErrorCodes.EngineerNotOwned` |
| 45 | `DeleteEngineerValidatorTests` | `Validate_ShouldPass_WhenEngineerIdIsProvided` | `IsValid` true |
| 46 | `DeleteEngineerValidatorTests` | `Validate_ShouldFail_WhenEngineerIdIsEmpty` | contains `ErrorCodes.EngineerIdRequired` |
| 47 | `DeleteEngineerHandlerTests` | `Handle_ShouldSoftDeleteEngineer_WhenCallerIsOwner` | entity `IsDeleted` true and `Status` is `EngineerStatus.Removed`; `Received(1).Update(engineer)`; `Received(1).SaveChangesAsync(...)`; `DidNotReceive().Delete(Arg.Any<Engineer>())` |
| 48 | `DeleteEngineerHandlerTests` | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UnauthorizedCoreException` with `ErrorCodes.UserNotAuthenticated`; `DidNotReceive().SaveChangesAsync(...)` |
| 49 | `DeleteEngineerHandlerTests` | `Handle_ShouldThrowNotFound_WhenEngineerDoesNotExist` | `NotFoundCoreException` with `ErrorCodes.EngineerNotFound`; `DidNotReceive().SaveChangesAsync(...)` |
| 50 | `DeleteEngineerHandlerTests` | `Handle_ShouldThrowForbidden_WhenCallerIsNotOwner` | `ForbiddenCoreException` with `ErrorCodes.EngineerNotOwned`; `DidNotReceive().Update(Arg.Any<Engineer>())`; `DidNotReceive().SaveChangesAsync(...)` |

Requirement → test mapping: create → 1, 2, 7–10, 19; slug derivation → 7–10, 19; slug uniqueness → 22; per-creator limit → 21; update → 4, 5, 32; owner-only → 35, 44, 50; list mine → 36, 37; get one → 41, 43; soft delete → 6, 47; authentication → 20, 33, 38, 42, 48; validation → 11–18, 23–31, 39, 40, 45, 46.

Do **not** write tests for `EngineersController`, `EngineerRepository`, `EngineerResultGenerator`, `AppDbContext`/EF configuration, DI registration, or the MediatR pipeline (conventions §5, out of scope).

## Definition of done

- [ ] `dotnet build api/E3A.slnx` succeeds with zero warnings (`TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`, SonarAnalyzer active).
- [ ] `dotnet test api/E3A.slnx` is green; all 50 planned tests exist with exactly those class and method names and no extra test methods.
- [ ] Exactly 25 hand-written new files at exactly the paths in **Files to create**, plus the three tool-generated migration files — no others.
- [ ] Exactly the 9 existing files in **Existing code touched** are modified — no others. `core-libraries/`, every `.csproj`, `Directory.Packages.props`, `E3A.slnx` and `E3A.Domain/Identity/*` are byte-identical.
- [ ] `Program.cs` differs by exactly one line (`AddApplication(builder.Configuration)`); middleware order unchanged.
- [ ] `AddApplication` binds `EngineerLimitsOptions` from `EngineerLimitsOptions.SectionName` and calls `AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly)`; the MediatR lambda parameter is renamed so the file compiles.
- [ ] `appsettings.json` contains `"EngineerLimits": { "MaxEngineersPerCreator": 50 }`; the number 50 appears in **no** `.cs` file.
- [ ] `Engineer` extends `AuditEntity`, has a private constructor, a `static Create`, and no public property setter; `Slug` has no mutator.
- [ ] Every mutating domain method sets `UpdationDate = DateTimeOffset.UtcNow`; `Remove()` calls `SoftDelete()` and no code assigns `IsDeleted`.
- [ ] `Tags` is copied with `[.. tags]` in both `Create` and `UpdateMetadata`.
- [ ] The five field-length caps are `const` on `Engineer` and are referenced by both the validators and `ConfigureEngineers` — no duplicated literals.
- [ ] `IEngineerRepository` adds no members to `IRepository<Engineer>`; `EngineerRepository` has an empty body.
- [ ] `AppDbContext` declares `public DbSet<Engineer> Engineers { get; set; }`, calls `ConfigureEngineers`, and registers `Engineer` in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`; no `ApplyConfigurationsFromAssembly`; no `.Where(x => !x.IsDeleted)` in any query.
- [ ] `IX_Engineers_Slug` is unique **and** filtered on `[IsDeleted] = 0`; `IX_Engineers_OwnerUserId` exists.
- [ ] A single migration named `initial` exists under `E3A.Infrastructure/Data/Migrations`, is unedited, and creates the `Engineers` table with both indexes. No connection string or Firebase value is committed anywhere.
- [ ] Each of the five handlers guards `ICurrentUserService.UserId` first with `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`, contains no `try`/`catch`, and calls `SaveChangesAsync` at most once.
- [ ] Ownership is enforced in `GetEngineer`, `UpdateEngineer` and `DeleteEngineer` with `ForbiddenCoreException(ErrorCodes.EngineerNotOwned)`.
- [ ] No new exception type; only `UnauthorizedCoreException`, `NotFoundCoreException`, `ForbiddenCoreException`, `ConflictCoreException` and `BusinessRuleViolationCoreException` are thrown.
- [ ] All 12 new error-code constants exist in `E3A.Application/Exceptions/ErrorCodes.cs` and as keys in **both** `Messages.en.resx` and `Messages.ar.resx`; `{limit}` survives in both languages.
- [ ] `EngineersController` is routed `api/engineers`, is `[Authorize]` with no policy or role string, contains no business logic, and returns 201/200/200/200/204; `CreatedAtAction` names the action `GetEngineer`.
- [ ] No `DateTime` anywhere in the new code — `DateTimeOffset` only.
- [ ] Every `await` outside a test method body carries `.ConfigureAwait(false)`.
- [ ] File-scoped namespaces everywhere, matching folder paths; no new file exceeds ~100 lines.
- [ ] Commands and queries are `sealed record : IRequest<…>`; handlers and validators are `sealed class` with one-line primary constructors.
- [ ] Every test entity is built through `EngineerFactory`; no `new Engineer(...)`, no reflection.
- [ ] Every exception test asserts on an `ErrorCodes.*` constant, never on a message string.
- [ ] `SaveChangesAsync` is asserted `Received(1)` on tests 19, 32, 47 and `DidNotReceive()` on tests 20, 21, 22, 33, 34, 35, 48, 49, 50.
- [ ] No wall-clock equality assertions; date assertions use `BeOnOrAfter(before)` with a captured `before`.
- [ ] `docs/implementation-plan.md`'s `engineers / teams` data-model bullet matches the implemented slug and `InstallCount` reality.
- [ ] Exactly one comment exists in the new production code: the constants note on `Engineer`.

## Revisions

**2026-08-27** — the six open questions from the initial plan were resolved by Mohamed:

1. Slug = kebab-case of DisplayName, globally unique — confirmed as planned, including the docs-sync edit to the stale `docs/implementation-plan.md` bullet (and its `LikeCount/DislikeCount` → `InstallCount` fix).
2. Slug immutable on rename — confirmed.
3. Plain `[Authorize]`, `DefaultCodes` deferred — confirmed.
4. Non-owner access returns 403 `ENGINEER_NOT_OWNED` — confirmed.
5. Limit counts all non-deleted engineers of any status — confirmed (stricter reading stands).
6. Migration naming — resolved: the first migration is named `initial` (not `InitialCreate`), carrying the full Identity/Core baseline, per the proven Morabh convention; future migrations follow `initial-002`, `initial-003`, … Decision 22, the Files-to-create table, the `dotnet ef` command, and the Definition of done were updated accordingly.
