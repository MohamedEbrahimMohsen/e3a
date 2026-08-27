# e3a Constitution

The rules for ALL implementation work in this repo. e3a's stack (revised 2026-08-27): **.NET 10 · ASP.NET Core API (controllers) · MediatR 14 + FluentValidation 12 via vendored `api/core-libraries` (Core.DDD, Core.CQRS, Core.EntityFrameworkCore, Core.Errors, …) · EF Core 10 + Azure SQL · Azure Container Apps hosting · React 18 + TS · Bicep**. Backend patterns are detailed in `.claude/skills/dotnet-feature/SKILL.md`; the feature pipeline (`feature-planner` → `feature-implementer` → `feature-reviewer`, artifacts in `.process/`) is the standard way features are built. When a rule here conflicts with a generic "best practice", this file wins. New code must be indistinguishable from existing code.

---

## 0. Prime Directives

1. **Ask before implementing.** Plan/design approval is not a build trigger. Do not write code until Mohamed explicitly says implement (an explicit instruction like "fix X" in a message counts for that task).
2. **Mirror, don't modernize.** The repo's way IS the standard. Open a neighboring file and match it.
3. **No magic values — configuration or named constants, nothing inline.**
   - Every **tunable or environment-dependent value** (limits, caps, URLs, container/queue names, feature switches, allowed extensions) lives in `appsettings.json`, bound through a `[Topic]Options` class with a `public const string SectionName`.
   - Every **true invariant** (values that must never change or the product breaks — e.g. the deterministic-zip timestamp, the `e3a-` plugin name prefix) is a **named constant with a WHY comment**, never an inline literal.
   - If unsure which it is: options.
4. **Secrets are NEVER committed.** Not in `appsettings.json`, not in code, not in Bicep params. Local secrets go in `local.settings.json`/`.env` (gitignored); deployed secrets go in Azure app settings (`Section__Key` doubles-underscore convention) or Key Vault.
5. **The core engine stays tested.** `PluginBuilder`, `PackageComposer`, `StructureValidator`, `SecurityScanner`, `MarketplaceGenerator` — every behavior change lands with its test; every scanner rule has a malicious fixture.
6. **Zero-cost bias.** Any change that adds an always-on resource or paid tier needs explicit approval first.

---

## 1. C# Style — Non-Negotiable

### 1.1 Type/member definitions — ALWAYS one line, never wrap

```csharp
// ✅ DO — one line, however long
public sealed class PublishHandler(IPluginBuilder pluginBuilder, ISecurityScanner securityScanner, IBlobStore blobStore) : IPublishHandler

// ❌ DON'T — parameter wrapping
public sealed class PublishHandler(
    IPluginBuilder pluginBuilder,
    ISecurityScanner securityScanner) : IPublishHandler
```

Exception: multi-property **records** with many fields may list one parameter per line — mirror the file you're extending.

### 1.2 Method bodies — block-bodied with braces, never expression-bodied

Expression bodies are for **properties/accessors only**.

```csharp
// ✅ DO
public string GetBlobPath(string prefix)
{
    return $"{prefix}/{PluginName}/{SemVer}.zip";
}

// ✅ DO — computed property
public bool IsBlocked => Hits.Any(h => h.Severity == ScanSeverity.Block);

// ❌ DON'T
public string GetBlobPath(string prefix) => $"{prefix}/{PluginName}/{SemVer}.zip";
```

### 1.3 Braces on every `if` — even one-liners

```csharp
// ✅ DO
if (package.Find(PluginJsonPath) is null)
{
    errors.Add("Missing plugin.json.");
}

// ❌ DON'T
if (x is null) throw new InvalidOperationException();
var y = x ?? throw new InvalidOperationException();
```

### 1.4 Everything else

- File-scoped namespaces matching folders (`namespace E3a.Core.Infrastructure.Plugins;`).
- `var` everywhere.
- Records for data shapes (`sealed record`); services/handlers/validators `sealed class`; primary constructors preferred.
- Collection expressions: `[]` for empty, `?? []` for null-coalescing, `[.. spread]`.
- `DateTimeOffset` ONLY — `DateTime` is PROHIBITED. `DateTimeOffset.UtcNow` for stamps.
- `CancellationToken cancellationToken` — full name, never `ct`, threaded to every downstream async call.
- `.ConfigureAwait(false)` on awaits in `E3a.Core`.
- No comments explaining WHAT. A comment is allowed only for a hidden WHY-invariant.
- These rules apply to test code too.

---

## 2. Configuration Pattern

```csharp
// E3a.Core/Options/PublishingOptions.cs
public sealed class PublishingOptions
{
    public const string SectionName = "Publishing";

    public int MaxFilesPerSkill { get; set; }
    public long MaxBytesPerSkill { get; set; }
    // ...
}
```

- Bound in the layer's `DependencyInjection.cs` (`E3A.Application` / `E3A.Infrastructure`) — the ONLY registration points.
- Injected as `IOptions<PublishingOptions>` via primary constructor; read `.Value` once into a field/local.
- `appsettings.json` (committed, non-secret defaults) → Azure App Configuration + Container Apps settings per environment → user-secrets/`appsettings.Development.json` (gitignored) for local secrets.
- Tests construct options directly: `Options.Create(new PublishingOptions { ... })` — test values mirror the committed defaults unless the test targets a limit.

---

## 3. Naming Taxonomy

| Thing | Convention | Example |
|---|---|---|
| Use-case slice | `E3A.Application/{Area}/{UseCase}/{Command\|Query, Handler, Validator, Result}` | `Engineers/PublishEngineer/` |
| Command / Query | `[Verb][Noun]Command` / `Get[Noun]Query`, `List[Nouns]Query` | `PublishEngineerCommand`, `ListEngineersQuery` |
| Handler | `[Name minus Command]Handler` / `[QueryName]Handler` | `PublishEngineerHandler` |
| Result | `[Noun]Result` (+ `[Noun]ResultGenerator` when non-trivial), area `Shared/` when reused | `EngineerResult` |
| Options | `[Topic]Options` + `SectionName` const | `PublishingOptions` |
| Controller | `[Nouns]Controller` in `E3A.Api/Controllers/{Area}/`, kebab-case plural routes | `EngineersController`, `api/engineers` |
| Entity + repo interface | aggregate folder `E3A.Domain/{Area}/` | `Engineer`, `IEngineerRepository` |
| Repo implementation | `E3A.Infrastructure/{Area}/` | `EngineerRepository` |

**PROHIBITED names:** `DTO`, `Response` (for internal results), `Model`, `Manager`, `Helper`, `Utils`.

**No abbreviated identifiers.** Full descriptive names always, even when longer — `SemanticVersion` not `SemVer`, `request` not `req`, `document` not `doc`, `cancellationToken` not `ct`, `configuration` not `config`/`cfg`. The only tolerated short forms are universal lambda placeholders (`x =>`, `f =>`) and loop indexers (`i`). Don't swing to the other extreme either — names should be normal words, not sentences.

---

## 4. Layer Rules

- **`E3A.Api`** — thin controllers only: map Request → Command/Query → `mediator.Send` → `Ok(result)`. No business logic. Resources (`Messages.ar/en.resx`) live here.
- **`E3A.Application`** — vertical slices `{Area}/{UseCase}/`, `Exceptions/ErrorCodes.cs`, options, MediatR pipeline behaviors from `Core.CQRS`.
- **`E3A.Domain`** — aggregates (Core.DDD bases: private ctor + `Create` factory, guarded domain methods setting `UpdationDate`), enums + extensions, repository interfaces in aggregate folders.
- **`E3A.Infrastructure`** — ONE `AppDbContext` (named private config methods, global soft-delete filters, no `ApplyConfigurationsFromAssembly`), repositories (`Repository<T>` base, never saving), Azure clients.
- **`E3A.Tests`** — xUnit + NSubstitute + FluentAssertions per `conventions/dotnet-testing.md`: entity branches, every handler branch, every validator rule; repos/controllers out of scope.
- Errors: no `try`/`catch` in handlers — throw `Core.Errors` exceptions; `Core.Exceptions` middleware formats responses. Never invent an exception type for a covered status code.
- One `SaveChangesAsync` per handler, at the end. Full backend detail: `.claude/skills/dotnet-feature/SKILL.md`.

---

## 5. Frontend (web/)

- React 18 + TypeScript strict + Vite. Feature-folder structure (`src/features/<area>/`).
- No magic values: API base URL and tunables come from Vite env (`import.meta.env.VITE_*`) with `.env.example` committed, real `.env.local` gitignored.
- Tailwind conventions once introduced; match existing component patterns.

---

## 6. Definition of Done (per change)

- Style checklist (§1) passes on every touched file.
- No inline magic values (§0.3); options wired through `AddE3aCore`.
- `dotnet build` clean — zero new warnings. `dotnet test` green.
- Core-engine changes carry tests (§0.5).
- No secrets in the diff (§0.4).
- **Docs sync** (full rule: `.claude/rules/docs-sync.md`): a change that alters business
  logic, scope, architecture, policies, or contracts must update the owning doc in `/docs`
  in the same change — implementation and docs may never give two different answers to the
  same question. Incompleteness is fine (plan says 10 features, 6 built — normal);
  divergence is a blocking finding. Reviewers must check this distinction explicitly.
