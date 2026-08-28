---
name: dotnet-feature
description: "The engineering handbook for implementing ANY backend work in the E3A solution (.NET 10 · ASP.NET Core API · MediatR 14 · FluentValidation 12 · EF Core 10 · vendored core-libraries Core.*) in Mohamed's DDD/CQRS style. Use whenever the user asks to implement, add, create, or scaffold anything in the E3A API: a feature, command, query, handler, validator, entity, repository, controller, endpoint, migration, or error code. Trigger on: new command, new query, add handler, add entity, wire endpoint, add error code. The feature pipeline agents (feature-planner/implementer/reviewer) follow this skill. All cross-cutting types come from the vendored api/core-libraries (Core.DDD, Core.CQRS, Core.EntityFrameworkCore, Core.Errors, Core.Validation, etc.) — no external extensions packages."
---

# E3A Feature Implementation Handbook

Target stack: **.NET 10 · EF Core 10 (Azure SQL) · MediatR 14 · FluentValidation 12 · vendored `api/core-libraries` (`Core.*`)**

Companion documents: `docs/constitution.md` (wins on conflict) · `conventions/dotnet-testing.md` · `.claude/rules/docs-sync.md` · `.process/todo-api/` (worked pipeline example).

**Core-first, always.** Before writing any helper, middleware, validator extension, client, or base type — check `api/core-libraries/`. Re-implementing an existing `Core.*` capability is a defect, not a style choice. **Mirror, don't modernize** — when in doubt, open a neighboring slice; consistency beats cleverness.

---

## 1. Non-Negotiable Style Rules

Apply everywhere, every file, no exceptions.

### Namespaces — always file-scoped
```csharp
// ✅
namespace E3A.Domain.Tenants;
// ❌
namespace E3A.Domain.Tenants { }
```

### Types
- Commands and queries: `sealed record` — classes PROHIBITED
- Validators: `sealed class` — separate file from command, same folder
- Handlers: `sealed class`
- Value objects: `sealed record` (simple) or class with behavior
- Result types: `sealed record` (simple) or `sealed class` (rich)

### Class Definition Style
One line, however long — never wrap parameters (constitution §1.1).
```csharp
public sealed class CreateTenantHandler(ITenantRepository tenantRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<CreateTenantCommand, TenantResult>
```

### No Comments
Zero comments unless the WHY is a hidden invariant. Never explain what code does.

### No Long Files
~80–100 lines max. Extract to extensions, generators, or helpers. One responsibility per file.

### Properties / Computed Members — inline
```csharp
public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;
public decimal AmountDue => Amount + ServiceFees;
```

### LINQ / Fluent Chains — new line per operator
```csharp
return subscriptions
    .Where(x => x.IsActive())
    .Select(TenantResultGenerator.Generate)
    .ToList();
```

### Switch Expressions — always expression form
```csharp
return status switch
{
    TenantStatus.Active => "Active",
    TenantStatus.Suspended => "Suspended",
    _ => "Unknown",
};
```

### Collections — use `[]`
```csharp
public List<TenantSubscription> Subscriptions { get; init; } = [];
```

### ConfigureAwait — always on every await outside controllers
```csharp
var tenant = await tenantRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
```

### DateTime — ALWAYS DateTimeOffset, NEVER DateTime
```csharp
// ✅
public DateTimeOffset CreatedAt { get; init; }
// ❌
public DateTime CreatedAt { get; init; }
```

### Names — no abbreviations (constitution §3)
`SemanticVersion` not `SemVer`, `request` not `req`, `cancellationToken` full name, threaded to every downstream async call.

### No magic values (constitution §0.3)
Tunables → `[Topic]Options` + `SectionName` const bound from configuration; invariants → named constants with a WHY comment.

---

## 2. Naming Reference

| Thing | Convention | Example |
|-------|-----------|---------|
| File | PascalCase | `CreateTenantHandler.cs` |
| Namespace | file-scoped, mirrors folder | `namespace E3A.Application.Tenants.CreateTenant;` |
| Command | `[Verb][Noun]Command` sealed record | `CreateTenantCommand` |
| Query | `[Verb][Noun]Query` sealed record | `GetTenantsQuery` |
| Handler | `[Verb][Noun]Handler` sealed class | `CreateTenantHandler` |
| Validator | `[Command]Validator` sealed class, separate file | `CreateTenantValidator` |
| API input | `[Feature]Request` sealed record | `CreateTenantRequest` |
| CQRS output | `[Noun]Result` sealed record or class | `TenantResult`, `TenantAdminResult` |
| Result generator | `[Noun]ResultGenerator` static class | `TenantResultGenerator` |
| Entity | PascalCase class | `Tenant`, `TenantSubscription` |
| Value object | `sealed record` | `TenantAddress` |
| Enum | PascalCase | `TenantStatus` |
| Enum extensions | `[Enum]Extensions` static class, same file as enum | `TenantStatusExtensions` |
| Domain extensions | `[Entity]Extensions` static class | `TenantSubscriptionExtensions` |
| Repo interface | `I[Entity]Repository : IRepository<T>` | `ITenantRepository` |
| Repo impl | `[Entity]Repository : Repository<T>` | `TenantRepository` |
| Error code const | SCREAMING_SNAKE_CASE string value | `"TENANT_NOT_FOUND"` |
| Error codes class | `ErrorCodes` flat static, comment-grouped | `ErrorCodes.TenantNotFound` |
| Lifecycle status enum value | `Deleted` — never `Removed`; domain method `Delete()` pairs with `SoftDelete()` | `EngineerStatus.Deleted` |
| Area options | `[Area]Options` + `SectionName` const; ALL schema/business caps live here | `EngineersOptions` |
| Policy names | `DefaultCodes` static class, PascalCase | `DefaultCodes.AdminTenantsRead` |
| DbSet | plural PascalCase, `{ get; set; }` | `public DbSet<Tenant> Tenants { get; set; }` |
| Private ctor | `private Entity(Guid id, Guid? createdBy)` | `private Tenant(Guid id, Guid? createdBy)` |
| Factory method | `static [Entity] Create(...)` | `Tenant.Create(...)` |
| Domain method | verb, mutates state, sets UpdationDate | `tenant.Suspend()`, `tenant.Update(name)` |

**PROHIBITED names**: `DTO`, `Response` (for CQRS outputs), `Model` (for results), `Service` (for use-case logic).

---

## 3. Project & Folder Structure

```
api/
├── core-libraries/             Core.DDD · Core.CQRS · Core.EntityFrameworkCore · Core.Errors
│                               Core.Exceptions · Core.Validation · Core.Identity · Core.Localization
│                               Core.Auditing · Core.Azure · Core.Queues · Core.Logging · Core.Cache
│                               Core.Notifications · Core.OTP · Core.Utilities
├── E3A.Domain/
│   ├── Identity/               User.cs, Role.cs, RoleNames.cs (template)
│   └── {Area}/                 Entity + ValueObjects + Enums (+ext same file) + Extensions
│                               + I{Entity}Repository — aggregate folder holds all of it
├── E3A.Application/
│   ├── {Area}/{UseCase}/       Command|Query + Validator + Handler (folder-per-use-case)
│   ├── {Area}/Shared/          shared Results + ResultGenerators for the area
│   ├── Exceptions/             ErrorCodes.cs
│   └── DependencyInjection.cs
├── E3A.Infrastructure/
│   ├── Data/Context/           AppDbContext.cs  ← ONE shared context; all areas add DbSets here
│   ├── {Area}/                 TenantRepository.cs …
│   └── DependencyInjection.cs
├── E3A.Api/                    ← ALL controllers live here only
│   ├── Controllers/{Area}/     TenantsController.cs (+ Requests.cs when HTTP shape differs)
│   ├── Resources/              Messages.ar.resx, Messages.en.resx
│   └── Program.cs
└── E3A.Tests/                  xUnit + NSubstitute + FluentAssertions
```

**Tests are REQUIRED** per `conventions/dotnet-testing.md` — entity branches, every handler branch, every validator rule (repository implementations and controllers are out of scope). This supersedes any older no-tests rule.

---

## 4. Domain Layer

### 4.1 Entity Base Classes (from Core.DDD)

| Class | Inherits | Adds |
|-------|----------|------|
| `Entity(Guid id)` | — | `Id`, `IsDeleted`, `SoftDelete()`, domain events |
| `AuditEntity(Guid id, Guid? createdBy)` | `Entity` | `CreatedBy`, `CreationDate`, `UpdatedBy`, `UpdationDate` |
| `AggregateRoot(Guid id, Guid? createdBy)` | `AuditEntity` | aggregate boundary marker |

Use `AggregateRoot` for top-level aggregates. `AuditEntity` for child entities needing audit. `Entity` for simple children.
All entities implement `ISoftDeletable` — call `.SoftDelete()`, never set `IsDeleted` directly.

### 4.2 Aggregate Root

```csharp
namespace E3A.Domain.Tenants;

public class Tenant : AggregateRoot
{
    public LocalizedText Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public TenantStatus Status { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public List<TenantSubscription> Subscriptions { get; private set; } = [];

    private Tenant(Guid id, Guid createdBy) : base(id, createdBy) { }

    public static Tenant Create(LocalizedText name, string slug, Guid ownerUserId, Guid createdBy)
    {
        return new Tenant(Guid.NewGuid(), createdBy)
        {
            Name = name,
            Slug = slug,
            OwnerUserId = ownerUserId,
            Status = TenantStatus.Active,
        };
    }

    public void Update(LocalizedText name, string slug)
    {
        Name = name;
        Slug = slug;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Suspended)
        {
            throw new BusinessRuleViolationException(ErrorCodes.TenantAlreadySuspended);
        }
        Status = TenantStatus.Suspended;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
```

Rules:
- Private constructor. `static Create(...)` is the only way to construct from application code.
- All state changes through named domain methods. Direct property mutation from handlers is PROHIBITED.
- Methods that mutate state MUST set `UpdationDate = DateTimeOffset.UtcNow`.
- Guard THEN mutate THEN stamp. Braces on every `if`.

### 4.3 Child Entity

```csharp
namespace E3A.Domain.Tenants;

public class TenantSubscription : AuditEntity
{
    public Guid TenantId { get; private set; }
    public Guid SubscriptionTierId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public SubscriptionStatus Status { get; private set; }

    private TenantSubscription(Guid id, Guid createdBy) : base(id, createdBy) { }

    public static TenantSubscription Create(Guid tenantId, Guid tierId, DateTimeOffset startsAt, DateTimeOffset expiresAt, Guid createdBy)
    {
        return new TenantSubscription(Guid.NewGuid(), createdBy)
        {
            TenantId = tenantId,
            SubscriptionTierId = tierId,
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            Status = SubscriptionStatus.Active,
        };
    }

    public bool IsExpired() => ExpiresAt < DateTimeOffset.UtcNow;
}
```

### 4.4 Value Objects

```csharp
namespace E3A.Domain.Tenants;

public sealed record TenantAddress(string Country, string City);
public sealed record TenantContact(string Email, string? Phone)
{
    public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
}
```

### 4.5 LocalizedText (from Core.DDD)

Every genuinely bilingual field MUST use `LocalizedText`. Plain `string` for bilingual fields is PROHIBITED. (e3a is currently EN-only — plain `string` is correct until a field is genuinely bilingual.)

```csharp
public LocalizedText Name { get; private set; } = default!;

var name = new LocalizedText(arabic: "مجلس الإدارة", english: "Board of Directors");

// EF mapping — ALWAYS ConfigureLocalized, never inline OwnsOne
builder.ConfigureLocalized(x => x.Name);

// Client-facing result — active language only
Name = entity.Name.Localized()

// Admin-facing result — expose both
NameArabic = entity.Name.Arabic,
NameEnglish = entity.Name.English,
```

Never call `.Localized()` in an admin result. Never return raw `LocalizedText` in a client result.

### 4.6 Enums + Extensions (same file)

```csharp
namespace E3A.Domain.Tenants;

public enum TenantStatus { Active, Suspended, Terminated }

public static class TenantStatusExtensions
{
    public static bool IsOperational(this TenantStatus status)
    {
        return status is TenantStatus.Active;
    }

    public static string ToDisplayString(this TenantStatus status)
    {
        return status switch
        {
            TenantStatus.Active => "Active",
            TenantStatus.Suspended => "Suspended",
            TenantStatus.Terminated => "Terminated",
            _ => "Unknown",
        };
    }
}
```

### 4.7 Domain Extensions (complex calculations only)

```csharp
namespace E3A.Domain.Tenants;

public static class TenantSubscriptionExtensions
{
    public static int DaysRemaining(this TenantSubscription subscription)
    {
        return Math.Max(0, (int)(subscription.ExpiresAt - DateTimeOffset.UtcNow).TotalDays);
    }

    public static bool IsAboutToExpire(this TenantSubscription subscription, int thresholdDays)
    {
        return !subscription.IsExpired() && subscription.DaysRemaining() <= thresholdDays;
    }
}
```

### 4.8 Domain Exception — domain layer only

```csharp
// throw inside entity/domain methods only; never from handlers
throw new BusinessRuleViolationException(ErrorCodes.TenantAlreadySuspended);
```

### 4.9 Repository Interface

```csharp
namespace E3A.Domain.Tenants;

// Simple — generic base covers everything
public interface ITenantRepository : IRepository<Tenant> { }

// With custom queries — only add what base cannot express
public interface ITenantSubscriptionRepository : IRepository<TenantSubscription>
{
    Task<List<TenantSubscription>> GetExpiredAsync(CancellationToken cancellationToken);
}
```

`IRepository<T>` (Core.DDD) provides: `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `AddRangeAsync`, `Update`, `UpdateRange`, `Delete`, `DeleteRange`, `FindAsync`, `FirstOrDefaultAsync`, `FindPaginatedAsync`, `CountAsync`, `SaveChangesAsync`.
Only add custom methods when base methods genuinely cannot express the query.

---

## 5. Application Layer

### 5.1 ErrorCodes — Flat Static Class

Single flat class in `E3A.Application/Exceptions/ErrorCodes.cs`. No nesting. Group with comment separators only.

```csharp
namespace E3A.Application.Exceptions;

public static class ErrorCodes
{
    // TENANTS
    public const string TenantNotFound = "TENANT_NOT_FOUND";
    public const string TenantSlugTaken = "TENANT_SLUG_TAKEN";
    public const string TenantAlreadySuspended = "TENANT_ALREADY_SUSPENDED";

    // AUTH
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";
}
```

### 5.2 Command

```csharp
namespace E3A.Application.Tenants.CreateTenant;

public sealed record CreateTenantCommand(string NameArabic, string NameEnglish, string Slug, Guid OwnerUserId) : IRequest<TenantResult>;
```

### 5.3 Query

```csharp
namespace E3A.Application.Tenants.GetTenants;

public sealed record GetTenantsQuery(string? Search, TenantStatus? Status, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<TenantResult>>;
```

### 5.4 Validator (separate file, same folder as command)

```csharp
namespace E3A.Application.Tenants.CreateTenant;

public sealed class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.NameEnglish)
            .ValidateRequired(ErrorCodes.TenantNameRequired)
            .ValidateMaxLength(200, ErrorCodes.TenantNameTooLong);

        RuleFor(x => x.Slug)
            .ValidateRequired(ErrorCodes.TenantSlugRequired)
            .ValidateMaxLength(100, ErrorCodes.TenantSlugTooLong);

        RuleFor(x => x.OwnerUserId).ValidateRequired(ErrorCodes.OwnerRequired);
    }
}
```

**Core.Validation extensions** — use over raw `.NotEmpty()` / `.Must()` wherever they fit:

| Extension | Use Case |
|-----------|----------|
| `ValidateRequired(errorCode?)` | string?, Guid, int, IFormFile? |
| `ValidateMaxLength(max, errorCode?)` / `ValidateMinLength(min, errorCode?)` | string |
| `ValidatePositive` / `ValidateGreaterThanZero` / `ValidateMax` / `ValidateMin` | numeric |
| `ValidateEmail` / `ValidateUrl` / `ValidateOnlyNumbers` | string |
| `ValidateOnlyEnglishText` / `ValidateOnlyArabicText` | string |
| `ValidateImageExtensions` / `ValidateMaxFileSize` | IFormFile |
| `ValidateDateRequired` | DateOnly/DateTimeOffset |
| `ValidateListContainOneItem` | collection |

(Verify exact names in `core-libraries/Core.Validation` before use — the vendored set is the truth.)

### 5.5 Command Handler

```csharp
namespace E3A.Application.Tenants.CreateTenant;

public sealed class CreateTenantHandler(ITenantRepository tenantRepository, ICurrentUserService currentUserService) : IRequestHandler<CreateTenantCommand, TenantResult>
{
    public async Task<TenantResult> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var slugTaken = await tenantRepository.FirstOrDefaultAsync(x => x.Slug == request.Slug, cancellationToken).ConfigureAwait(false);
        if (slugTaken is not null)
        {
            throw new ConflictCoreException(ErrorCodes.TenantSlugTaken);
        }

        var name = new LocalizedText(arabic: request.NameArabic, english: request.NameEnglish);
        var tenant = Tenant.Create(name, request.Slug, request.OwnerUserId, currentUserService.UserId.Value);

        await tenantRepository.AddAsync(tenant, cancellationToken).ConfigureAwait(false);
        await tenantRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TenantResultGenerator.Generate(tenant);
    }
}
```

### 5.6 Query Handler

List handlers return `List<TResult>` with `?? []` — never null. Use `FindPaginatedAsync`/`PageData<T>` only when the endpoint is genuinely paginated (admin grids); there is NO blanket PageData mandate. Language-aware ordering via `ILocalizer.IsCurrentLanguageArabic` when bilingual fields exist.

Handler rules:
- `sealed class`. No `try`/`catch`. Throw core exceptions directly.
- `SaveChangesAsync` called **once in handler** after all mutations — never inside repo CRUD methods.
- `.ConfigureAwait(false)` on every `await`.
- `ICurrentUserService.UserId` null-checked with `UnauthorizedCoreException` (guard current user FIRST).
- Business rule checks live in the DOMAIN (entity methods throw); the handler orchestrates.
- Never validate manually in handlers — the `Core.CQRS` `ValidationBehaviour` pipeline enforces validators → 422.
- Auditing: business mutations opt in via `IAuditableCommand` (Core.Auditing) — pipeline-side; handlers are never modified for auditing.

### 5.7 Results

```csharp
// Client-facing — .Localized() on all LocalizedText fields
public sealed record TenantResult(Guid Id, string Name, string Slug, string Status, DateTimeOffset CreatedAt);

// Admin-facing — both language values, no .Localized()
public sealed record TenantAdminResult(Guid Id, string NameArabic, string NameEnglish, string Slug, string Status, DateTimeOffset CreatedAt);
```

### 5.8 Result Generator (when mapping is non-trivial)

```csharp
namespace E3A.Application.Tenants.Shared;

public static class TenantResultGenerator
{
    public static TenantResult Generate(Tenant tenant)
    {
        return new TenantResult(tenant.Id, tenant.Name.Localized(), tenant.Slug, tenant.Status.ToString(), tenant.CreationDate);
    }
}
```

### 5.9 Application Exceptions (from Core.Errors) — reuse only

No `try`/`catch`. Throw directly. `CoreExceptionMiddleware` (Core.Exceptions) catches and formats.

| Exception | HTTP | When |
|-----------|------|------|
| `UnauthorizedCoreException(code)` | 401 | no authenticated user |
| `ForbiddenCoreException(code)` | 403 | authenticated but lacks permission |
| `NotFoundCoreException(code)` | 404 | entity not found |
| `ConflictCoreException(code)` | 409 | duplicate / concurrency conflict |
| `BadRequestCoreException(code, context?)` | 400 | general bad input |
| `BusinessRuleViolationCoreException(code, context?)` | 400 | business rule violation |
| `ApplicationValidationCoreException(code)` | 422 | validation (auto from pipeline) |
| `RateLimitExceededCoreException(code)` | 429 | rate limit |
| `InternalServerErrorCoreException(code)` | 500 | unexpected |
| `BusinessRuleViolationException(code)` | — (domain only) | inside entity domain methods |
| `InfrastructureCoreException(errorCode)` | 500, masked | infrastructure unhandled failures only |

NEVER create a new exception class for a status code already in this list.

With context dict:
```csharp
throw new BadRequestCoreException(ErrorCodes.DownPaymentTooLow, context: new Dictionary<string, object> { ["minimum"] = plan.MinimumDownPayment });
```

### 5.10 Context access

Handlers depend on repositories (aggregate-folder interfaces), never on concrete `AppDbContext`. If a use case genuinely needs direct context access, introduce/extend an `IAppDbContext` abstraction in `Application/Shared/Context` — repository-first remains the default (mirrors the template).

### 5.11 DependencyInjection.cs (Application)

```csharp
namespace E3A.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        return services;
    }
}
```
(Mirror the actual signature already in `E3A.Application/DependencyInjection.cs` — the repo is the truth.)

---

## 6. Infrastructure Layer

### 6.1 Repository Implementation

```csharp
namespace E3A.Infrastructure.Tenants;

// Simple — generic base covers everything
public class TenantRepository(AppDbContext context) : Repository<Tenant>(context), ITenantRepository { }

// With custom queries
public class TenantSubscriptionRepository(AppDbContext context) : Repository<TenantSubscription>(context), ITenantSubscriptionRepository
{
    public async Task<List<TenantSubscription>> GetExpiredAsync(CancellationToken cancellationToken)
    {
        return await FindAsync(x => x.ExpiresAt < DateTimeOffset.UtcNow && x.Status == SubscriptionStatus.Active, cancellationToken, asNoTracking: true).ConfigureAwait(false);
    }
}
```

`Repository<T>` (Core.EntityFrameworkCore) base provides all 13 `IRepository<T>` members plus `_dbSet`, `_context`.

**`AddAsync`/`Update`/`Delete` do NOT call `SaveChangesAsync`. Call it once in the handler.**

### 6.2 AppDbContext — Single Shared Context

ONE `AppDbContext` for the whole solution. Per-area DbContext subclasses are PROHIBITED. Actual signature (mirror it):

```csharp
namespace E3A.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantSubscription> TenantSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureTenants(modelBuilder);
        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }
}
```

- ✅ Always `DbSet<T> Xxx { get; set; }` — expression-bodied `=> Set<T>()` is PROHIBITED.
- `CoreDbContext` (Core.EntityFrameworkCore) handles audit-field stamping and domain-event dispatch on `SaveChangesAsync` — never dispatch events or stamp audit fields manually.

### 6.3 Entity Configuration

`ApplyConfigurationsFromAssembly` is PROHIBITED. Apply configs explicitly in named private methods.

```csharp
private static void ConfigureTenants(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Tenant>(builder =>
    {
        builder.ConfigureLocalized(x => x.Name);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasMany(x => x.Subscriptions)
            .WithOne()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    });
}
```

**EF Core Mapping Rules**

| Rule | When |
|------|------|
| `builder.ConfigureLocalized(x => x.Prop)` | ALL `LocalizedText` properties — no exceptions |
| `OwnsOne(x => x.ValueObject)` | simple value objects with no separate table |
| `HasConversion<string>().HasMaxLength(50–100)` | all enums |
| `OnDelete(DeleteBehavior.Restrict)` | default for all FKs |
| `HasQueryFilter(e => !e.IsDeleted)` | every ISoftDeletable entity — in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` only |
| `HasIndex(...).IsUnique()` | unique business keys |
| `ValueGeneratedNever()` | child entities with manually assigned IDs |

### 6.4 Soft-Delete Global Filters — MANDATORY for every entity

Never append `.Where(!IsDeleted)` in queries. The global filter method is the single enforcement point — add each new entity there. Use `IgnoreQueryFilters()` only in explicit admin/audit queries with documented reason.

### 6.5 DependencyInjection.cs (Infrastructure)

```csharp
namespace E3A.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITenantRepository, TenantRepository>();
        return services;
    }
}
```
(No configuration parameter — mirror the actual signature. DbContext/Identity wiring lives in `Program.cs` via the `Core.*` composition already there.)

---

## 7. API Layer

All controllers live in `E3A.Api` ONLY. Module/layer projects have NO controllers.

### 7.1 DefaultCodes — Policy Names

```csharp
namespace E3A.Domain.SharedKernel;

public static class DefaultCodes
{
    // TENANTS
    public const string AdminTenantsRead = "Admin.Tenants.Read";
    public const string AdminTenantsCreate = "Admin.Tenants.Create";
}
```

NEVER use `[Authorize(Roles = "...")]` or raw string literals. Always `[Authorize(Policy = DefaultCodes.Xxx)]` — following whatever policy wiring `Program.cs` (Core.Identity) already establishes; mirror the neighboring controller.

### 7.2 Request Records (API inputs)

API inputs use `Request` suffix, in a `Requests.cs` inside the controller's folder — only when the HTTP shape differs from the command; otherwise bind the command directly with `[FromBody]`.

```csharp
public sealed record CreateTenantRequest(string NameArabic, string NameEnglish, string Slug, Guid OwnerUserId);
```

### 7.3 Controller

```csharp
namespace E3A.Api.Controllers.Tenants;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = DefaultCodes.AdminTenantsRead)]
    public async Task<ActionResult> GetAll([FromQuery] string? search, [FromQuery] TenantStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetTenantsQuery(search, status, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = DefaultCodes.AdminTenantsCreate)]
    public async Task<ActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateTenantCommand(request.NameArabic, request.NameEnglish, request.Slug, request.OwnerUserId), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{tenantId:guid}")]
    [Authorize(Policy = DefaultCodes.AdminTenantsDelete)]
    public async Task<ActionResult> Delete([FromRoute] Guid tenantId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTenantCommand(tenantId), cancellationToken);
        return Ok();
    }
}
```

Controller rules:
- Route: lowercase kebab-case plural nouns (`api/tenants`, `api/subscription-tiers`). Nested: `api/tenants/{tenantId}/subscriptions`.
- `[Authorize(Policy = DefaultCodes.Xxx)]` on every action; `[AllowAnonymous]` for public catalog reads.
- Thin: map Request → Command/Query → send → `Ok(result)` (bare `Ok()` when the command returns nothing). No business logic.
- `[FromBody]` for POST/PUT/PATCH · `[FromRoute]` for path params · `[FromQuery]` for filters.
- `CancellationToken cancellationToken` on every action, passed to `Send`.

**REST Method Map**

| HTTP | Route | Operation |
|------|-------|-----------|
| GET | `/resources` | list / paginated search |
| GET | `/resources/{id}` | get single |
| POST | `/resources` | create |
| PUT | `/resources/{id}` | full replace |
| PATCH | `/resources/{id}` | partial update |
| DELETE | `/resources/{id}` | delete |

### 7.4 Program.cs

`E3A.Api/Program.cs` already composes the full `Core.*` pipeline (Azure App Configuration + Managed Identity in production, Core.Identity, Core.CQRS, Core.Exceptions middleware, Core.Localization, Core.Logging, Scalar OpenAPI). Mirror the existing composition when adding registrations — **middleware order is fixed; do not change it**. New policies go into the existing `AddAuthorization` block.

### 7.5 Localization Resources

```
E3A.Api/Resources/
├── Messages.ar.resx    ← Arabic strings; key = ErrorCode constant value
└── Messages.en.resx    ← English strings; key = ErrorCode constant value
```

Every `ErrorCodes` constant MUST have a matching key in both files. Messages never hardcoded in C# — resolved via `ILocalizer.GetMessage(code)`. Keep runtime placeholders (`{minimum}`, `{days}`) intact in BOTH languages; Arabic without tashkeel.

---

## 8. DO / DON'T Catalog — dev-review learnings (every DON'T found in review is BLOCKING)

Practical patterns extracted from Mohamed's real code reviews. The implementer reads this before writing; the reviewer walks it explicitly.

### 8.1 Schema & business caps live in Options, never as entity constants

A cap change must be a config change, not a redeployment.

```csharp
// ❌ DON'T — constants baked into the entity
public class Engineer : AuditEntity
{
    public const int DisplayNameMaxLength = 100;
    public const int MaxTags = 10;
}

// ✅ DO — one [Area]Options bound from appsettings.json; validators AND AppDbContext consume it
public sealed class EngineersOptions
{
    public const string SectionName = "Engineers";
    public int MaxEngineersPerCreator { get; set; }
    public int DisplayNameMaxLength { get; set; }
}

public CreateEngineerValidator(IOptions<EngineersOptions> engineersOptions)
{
    var options = engineersOptions.Value;
    RuleFor(x => x.DisplayName).ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.EngineerDisplayNameTooLong);
}
```

True invariants (e.g. enum-column width) stay as a named constant WITH a WHY comment — but anything a product owner could plausibly tune goes to appsettings.

### 8.2 Identifiers & random values come from Core.Utilities IGenerator — never hand-rolled

```csharp
// ❌ DON'T — invent random/suffix generation in a custom class
var suffix = new Random().Next(1000, 9999).ToString();

// ✅ DO — inject Core.Utilities.Generator.IGenerator (already DI-registered)
candidateSlug = generator.Generate(prefix: baseSlug, size: options.SlugSuffixSize);
```

Custom helpers are allowed ONLY for logic Core genuinely lacks (e.g. kebab-case normalization) — and they should do only that missing part.

### 8.3 Unique-slug pattern: repository `Is…ExistsAsync` + suffix loop — never throw Conflict for an auto-resolvable collision

```csharp
// ❌ DON'T
var existing = await repository.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken).ConfigureAwait(false);
if (existing != null) { throw new ConflictCoreException(ErrorCodes.SlugTaken); }

// ✅ DO — repo exposes the question; the handler auto-uniquifies
public interface IEngineerRepository : IRepository<Engineer>
{
    Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken);
}

var baseSlug = EngineerSlugGenerator.Normalize(displayName, options.SlugMaxLength);
if (!await engineerRepository.IsSlugExistsAsync(baseSlug, cancellationToken).ConfigureAwait(false)) { return baseSlug; }
string candidateSlug;
do
{
    candidateSlug = generator.Generate(prefix: prefix, size: options.SlugSuffixSize);
} while (await engineerRepository.IsSlugExistsAsync(candidateSlug, cancellationToken).ConfigureAwait(false));
```

### 8.4 Lifecycle status naming: `Deleted`, never `Removed`

```csharp
// ❌ DON'T
public enum EngineerStatus { Draft, Published, Removed }
public void Remove() { Status = EngineerStatus.Removed; SoftDelete(); }

// ✅ DO — the enum value pairs with ISoftDeletable semantics
public enum EngineerStatus { Draft, Published, Deleted }
public void Delete() { Status = EngineerStatus.Deleted; SoftDelete(); UpdationDate = DateTimeOffset.UtcNow; }
```

### 8.5 Soft-delete filtering has exactly one home

```csharp
// ✅ DO — the query filter lives ONLY in the global method; every new entity adds a line here
private static void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
    modelBuilder.Entity<Engineer>().HasQueryFilter(x => !x.IsDeleted);
}

// ✅ Also DO — a partial-index SQL filter stays WITH the index (it is schema, not querying)
builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");

// ❌ DON'T — ad-hoc IsDeleted checks inside queries or handlers
var engineers = await repository.FindAsync(x => !x.IsDeleted && ..., cancellationToken);
```

---

## 9. Checklist — Adding a New Feature

**Domain**
- [ ] Entity extends `AggregateRoot` / `AuditEntity` / `Entity`; private ctor; `static Create(...)`
- [ ] All state changes via named domain methods; methods set `UpdationDate = DateTimeOffset.UtcNow`
- [ ] `LocalizedText` for every genuinely bilingual field — no plain `string` for bilingual data
- [ ] Value objects as `sealed record`
- [ ] Enums + extensions in same file inside the aggregate folder
- [ ] Complex domain calc in `[Entity]Extensions.cs`
- [ ] Repo interface extends `IRepository<T>`; custom methods only when base is insufficient
- [ ] All date/time: `DateTimeOffset` — no `DateTime`

**Application**
- [ ] `ErrorCodes.cs` — add constants (flat class, SCREAMING_SNAKE_CASE, comment-grouped)
- [ ] Command/Query: `sealed record : IRequest<T>`
- [ ] Validator: separate file, `sealed class : AbstractValidator<T>`, Core.Validation extensions
- [ ] Handler: `sealed class`, no `try`/`catch`, throws `*CoreException` directly
- [ ] `SaveChangesAsync` called once in handler — never inside repo CRUD methods
- [ ] `ICurrentUserService.UserId` null-checked with `UnauthorizedCoreException`
- [ ] Client-facing results `.Localized()`; admin-facing `.Arabic`/`.English` separately
- [ ] `Result` suffix on all CQRS outputs — never `DTO`, `Response`, `Model`
- [ ] Business mutations opt into auditing via `IAuditableCommand`

**Infrastructure**
- [ ] Repo: extends `Repository<T>`, implements interface; no `SaveChangesAsync` in CRUD
- [ ] `AppDbContext`: `DbSet<T> Prop { get; set; }` — no `=> Set<T>()`
- [ ] `ConfigureLocalized(x => x.Prop)` for every `LocalizedText` — no inline `OwnsOne`
- [ ] Entity config in named private method — no `ApplyConfigurationsFromAssembly`
- [ ] New entity added to `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`
- [ ] Repos registered in `AddInfrastructure`; open-generic `IRepository<>` registration present

**API**
- [ ] Controller in `E3A.Api` only — kebab-case plural route
- [ ] `[Authorize(Policy = DefaultCodes.Xxx)]` on every action — never role strings; `[AllowAnonymous]` only for public reads
- [ ] Policy constant added to `DefaultCodes`
- [ ] API input: `Request` suffix record when HTTP shape differs; controller thin: map → send → `Ok(result)`
- [ ] New error codes added to both `Messages.ar.resx` and `Messages.en.resx`
- [ ] `postman/e3a.postman_collection.json` updated: request added/modified/deleted for every endpoint change

**Tests (per conventions/dotnet-testing.md)**
- [ ] Entity: factory + every domain-method branch
- [ ] Every handler branch; every validator rule
- [ ] Repositories and controllers NOT tested (out of scope by convention)
- [ ] Existing tests added/updated/removed as needed to keep the suite true

**Cross-cutting**
- [ ] File-scoped namespaces everywhere; no file exceeds ~100 lines
- [ ] Every DO/DON'T catalog entry (§8) honoured: caps in `[Area]Options` not entity constants · `IGenerator` for identifiers · `Is…ExistsAsync` + suffix loop for unique slugs · `Deleted` not `Removed` · soft-delete filter only in the global method
- [ ] `dotnet build` zero new warnings · `dotnet test` green
- [ ] `/docs` updated when behavior/scope changed (`.claude/rules/docs-sync.md`)
