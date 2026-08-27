# Implementation — Engineer Drafts Management

## Files created

36 hand-written files (24 production + 12 test) plus 3 tool-generated migration files.

### Domain

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Domain/Engineers/EngineerStatus.cs` | 8 | `Draft \| Published \| Removed`. No extensions class. |
| `api/E3A.Domain/Engineers/Engineer.cs` | 58 | `AuditEntity` aggregate: five schema constants, private ctor, `Create`, `UpdateMetadata`, `Remove`. |
| `api/E3A.Domain/Engineers/EngineerSlugGenerator.cs` | 32 | Kebab-case slug from display name, `StringBuilder`, no regex. |
| `api/E3A.Domain/Engineers/IEngineerRepository.cs` | 5 | `IRepository<Engineer>`, empty body. |

### Application

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Application/Options/EngineerLimitsOptions.cs` | 8 | `SectionName` + `MaxEngineersPerCreator`. |
| `api/E3A.Application/Engineers/Shared/EngineerResult.cs` | 3 | The single client-facing result record. |
| `api/E3A.Application/Engineers/Shared/EngineerResultGenerator.cs` | 11 | Entity → result mapping. |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerCommand.cs` | 6 | |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs` | 32 | Five rules in the mandated order. |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs` | 47 | Auth → cap → slug → conflict → create → save. |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerCommand.cs` | 6 | |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerValidator.cs` | 34 | Id rule + the same five. |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs` | 41 | Slug never recomputed. |
| `api/E3A.Application/Engineers/ListMyEngineers/ListMyEngineersQuery.cs` | 6 | No validator, by Decision 18. |
| `api/E3A.Application/Engineers/ListMyEngineers/ListMyEngineersQueryHandler.cs` | 29 | `FindAsync`, handler-side `OrderByDescending`, no `?? []`. |
| `api/E3A.Application/Engineers/GetEngineer/GetEngineerQuery.cs` | 6 | |
| `api/E3A.Application/Engineers/GetEngineer/GetEngineerQueryValidator.cs` | 13 | |
| `api/E3A.Application/Engineers/GetEngineer/GetEngineerQueryHandler.cs` | 36 | |
| `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerCommand.cs` | 5 | `IRequest`, no result. |
| `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerValidator.cs` | 13 | |
| `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerHandler.cs` | 38 | `Remove()` + `Update`; `Delete` never called. |

### Infrastructure

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Infrastructure/Engineers/EngineerRepository.cs` | 7 | Empty body over `Repository<Engineer>`. |
| `api/E3A.Infrastructure/Data/Migrations/20260827082800_initial.cs` | tool-generated | Identity + Core baseline + `Engineers`. |
| `api/E3A.Infrastructure/Data/Migrations/20260827082800_initial.Designer.cs` | tool-generated | |
| `api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` | tool-generated | |

Verified in the generated migration, unedited:

```csharp
migrationBuilder.CreateIndex(
    name: "IX_Engineers_Slug", table: "Engineers", column: "Slug",
    unique: true, filter: "[IsDeleted] = 0");
```

plus `IX_Engineers_OwnerUserId`, and `Tags nvarchar(400) NOT NULL`, `Status nvarchar(50)`, `Slug/DisplayName nvarchar(100)`, `Description nvarchar(500) NULL`.

### API

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Api/Controllers/Engineers/Requests.cs` | 5 | Two request records with `List<string>? Tags`. |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | 51 | Five thin actions, `[Authorize]`, 201/200/200/200/204. |

### Tests

| Path | Lines |
|------|-------|
| `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` | 21 |
| `api/E3A.Tests/Engineers/EngineerTests.cs` | 95 |
| `api/E3A.Tests/Engineers/EngineerSlugGeneratorTests.cs` | 44 |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerValidatorTests.cs` | 87 |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerHandlerTests.cs` | 79 |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerValidatorTests.cs` | 96 |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerHandlerTests.cs` | 78 |
| `api/E3A.Tests/Engineers/ListMyEngineers/ListMyEngineersQueryHandlerTests.cs` | 59 |
| `api/E3A.Tests/Engineers/GetEngineer/GetEngineerQueryValidatorTests.cs` | 28 |
| `api/E3A.Tests/Engineers/GetEngineer/GetEngineerQueryHandlerTests.cs` | 72 |
| `api/E3A.Tests/Engineers/DeleteEngineer/DeleteEngineerValidatorTests.cs` | 28 |
| `api/E3A.Tests/Engineers/DeleteEngineer/DeleteEngineerHandlerTests.cs` | 75 |

All 50 planned test methods exist with exactly the planned class and method names. No extra test methods. Largest file is 96 lines.

## Files modified

| Path | Change |
|------|--------|
| `api/E3A.Application/DependencyInjection.cs` | Signature → `AddApplication(this IServiceCollection services, IConfiguration configuration)`; MediatR lambda parameter renamed to `mediatRConfiguration`; added `AddValidatorsFromAssembly` and `Configure<EngineerLimitsOptions>`. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Appended the `// Engineers` group with 12 constants. `// Identity` untouched. |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | Added `DbSet<Engineer> Engineers`, `ConfigureEngineers` (verbatim from the plan, including `new List<string>()` inside the expression tree), the `ConfigureEngineers` call before the global filter, and the `Engineer` query-filter registration. XML doc comment kept. |
| `api/E3A.Infrastructure/DependencyInjection.cs` | Registered `IEngineerRepository` → `EngineerRepository`. Open generic not re-registered. |
| `api/E3A.Api/Program.cs` | One line: `AddApplication(builder.Configuration)`. Middleware order untouched. |
| `api/E3A.Api/appsettings.json` | Added `"EngineerLimits": { "MaxEngineersPerCreator": 50 }`. |
| `api/E3A.Api/Resources/Messages.en.resx` | 12 new `<data>` elements. |
| `api/E3A.Api/Resources/Messages.ar.resx` | The same 12 keys, Arabic, `{limit}` intact. |
| `docs/implementation-plan.md` | Replaced the `engineers` / `teams` data-model bullet per Decision 6. Nothing else. |
| **`api/.editorconfig`** | **Not in the plan's touched-files list.** Three analyzer suppressions — see Deviations 2–4. |

`core-libraries/`, every `.csproj`, `Directory.Packages.props`, `Directory.Build.props`, `E3A.slnx`, `appsettings.Development.json` and `E3A.Domain/Identity/*` are byte-identical.

## Deviations

| # | Plan said | Reality | What I did |
|---|-----------|---------|------------|
| 1 | `Engineer.Create` sets `Status`, `InstallCount = 0` and nothing else; `DraftManifestJson`/`LatestVersionId` are left to default. | SonarAnalyzer **S1144** fails the build: `error S1144: Remove the unused private setter 'set_DraftManifestJson'` (and `set_LatestVersionId`). Nothing in this slice writes them — by design, they are deferred to P2/P3 — so their private setters are dead under `TreatWarningsAsErrors=true`. | Added `DraftManifestJson = null,` and `LatestVersionId = null,` to the `Create` object initialiser. This is exactly the invariant the plan documents ("the two nullables `null`"), keeps the setters for the future publish/upload slices, and needed no analyzer suppression. The only change to the plan's verbatim `Engineer.cs` block. |
| 2 | Result types live in `{Area}/Shared/`, namespace `E3A.Application.Engineers.Shared` (plan file #6/#7; skill §3). | **CA1716** fails the build: `Rename namespace E3A.Application.Engineers.Shared so that it no longer conflicts with the reserved language keyword 'Shared'`. `Shared` is a Visual Basic keyword. This is the repo's first area folder, so the conflict had never surfaced. | Did **not** rename — the `Shared` folder is a solution-wide convention that every future area inherits, and nothing here is consumed from VB. Set `dotnet_diagnostic.CA1716.severity = none` in `api/.editorconfig` with a justifying comment. Alternatives were renaming away from the skill, or editing a `.csproj` (explicitly out of scope). |
| 3 | Test methods are named `Method_Should[Outcome]_When[Condition]` (`conventions/dotnet-testing.md` §2). | **CA1707** fails the build with 53 errors — `Remove the underscores from member name …`. Direct, unavoidable collision between the mandated naming convention and the analyzer gate. | Scoped `dotnet_diagnostic.CA1707.severity = none` to `[E3A.Tests/**.cs]` in `api/.editorconfig`. Production code still enforces CA1707. |
| 4 | The migration is named `initial` (Revision 6, Morabh convention) and the generated files are "never hand-written, never hand-edited". | The generated class `initial` trips compiler **CS8981** (`The type name 'initial' only contains lower-cased ascii characters`) in both `_initial.cs` and `_initial.Designer.cs`, and the generated Identity seed array trips **CA1861**. Three build errors. The plan's two constraints — this name, and no hand-editing — cannot both hold without a suppression. | Added `[E3A.Infrastructure/Data/Migrations/**.cs]` to `api/.editorconfig` with `generated_code = true`, `CS8981` and `CA1861` at `none`. The migration files are untouched and the `initial` name is preserved. This section also covers every future `initial-NNN` migration. |
| 5 | Definition of done: "Exactly **25** hand-written new files … plus the three tool-generated migration files". | The plan's own *Files to create* tables list 24 non-test files (items 1–22 minus the migration row, plus 24–25) and 12 test files = **36** hand-written files. The "25" appears to be a miscount that omits the test files and double-counts the migration row. | Created exactly the 36 paths in the tables, no more and no fewer. Flagging the arithmetic so the reviewer does not read a passing count as a failure. |

Nothing else deviates. No planned file was left unimplemented.

## Build & test

Baseline was captured before any edit: `dotnet build api/E3A.slnx` succeeded with 9 warnings, all pre-existing and all inside `core-libraries/` (CS8618/CS8602 in `Core.Notifications`, `Core.OTP`, `Core.Validation`).

Final, verbatim:

```
$ dotnet build E3A.slnx -v q --nologo
Build succeeded.
    9 Warning(s)
    0 Error(s)
Time Elapsed 00:00:17.52
```

The 9 warnings are the identical pre-existing `core-libraries` set. **Zero warnings and zero errors in `E3A.Domain`, `E3A.Application`, `E3A.Infrastructure`, `E3A.Api` and `E3A.Tests`.**

```
$ dotnet test E3A.slnx --nologo -v q
Passed!  - Failed: 0, Passed: 56, Skipped: 0, Total: 56, Duration: 1 s - E3A.Tests.dll (net10.0)
```

56 executed cases = the 50 planned methods, three of which are `[Theory]` carrying 3 `InlineData` rows each (+6).

Migration generated with the plan's exact recipe:

```
$ dotnet tool restore                     # dotnet-ef 10.0.5
$ ConnectionStrings__DbConnectionString=... CoreFirebaseServiceAccountJson={} \
  dotnet ef migrations add initial --project E3A.Infrastructure --startup-project E3A.Api --output-dir Data/Migrations
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

Both environment variables were set only in that one shell invocation. Neither value is committed anywhere. `dotnet ef database update` was **not** run.

Mechanical checks, all clean:

- No literal `50` in any new `.cs` file (the cap lives only in `appsettings.json`).
- No bare `DateTime` anywhere in new production or test code.
- Exactly one comment in new production code — the constants note on `Engineer`.
- Every `await` in `E3A.Application/Engineers` carries `.ConfigureAwait(false)`; no `.ConfigureAwait` anywhere in `E3A.Tests/Engineers`.
- No file over 100 lines (largest: `UpdateEngineerValidatorTests.cs` at 96, `EngineerTests.cs` at 95).

## Notes for review

1. **The `api/.editorconfig` edit is the thing to scrutinise.** Three separate analyzer conflicts (Deviations 2–4) all landed in one file that the plan never anticipated touching. Each suppression is scoped as narrowly as I could make it and carries a comment. If you would rather absorb any of them differently — renaming the `Shared` namespace, renaming the migration to `Initial`, or moving the suppressions into `Directory.Build.props` — that is a one-line change in each case, but all three alternatives contradict something the plan or the skill states explicitly.

2. **`ValidateMaxLength`'s trailing `.When(...)` is a latent bug in the vendored `Core.Validation`.** It reads `.When(x => !string.IsNullOrWhiteSpace(x?.ToString()))` where `x` is the *command*, not the property — so the condition is the record's `ToString()` and is always true. Harmless here (it means required + max-length both run, which is what the plan wants and what the tests confirm), but it is not doing what its author intended, and a future validator that relies on that condition will be surprised. Out of scope to fix; `core-libraries/` is off limits.

3. **`FluentAssertions` is 6.12.2**, per the deliberate pin. Every assertion used is 6.x-compatible; I did not upgrade.

4. **NSubstitute stubs match the handlers' optional-argument defaults positionally.** For example the handler calls `FirstOrDefaultAsync(predicate, cancellationToken, asNoTracking: true)` and the stub is `FirstOrDefaultAsync(Arg.Any<...>(), Arg.Any<CancellationToken>(), asNoTracking: true)` — `include` and `orderBy` default to `null` on both sides. If a handler's call site ever changes its `asNoTracking` value, the stub silently stops matching and the test fails loudly rather than passing vacuously, which is the behaviour I wanted; but it is a coupling worth knowing about.

5. **`docs/feature-pipeline.svg` shows as untracked in `git status`.** It is not mine — it predates my first write in this session. I left it alone.

6. **`Core.DDD.Entity.SoftDelete()` still sets `DeletedAt = null`** while setting `IsDeleted = true`, as the plan noted under Deferred. `Remove()` therefore produces a row with `IsDeleted = 1, DeletedAt = NULL`. The query filter reads `IsDeleted`, so behaviour is correct, but the audit column is useless until the vendored defect is fixed.

7. **Ownership is enforced per-row in three handlers, not by a policy.** There is no `DefaultCodes` and no named policy, per Decision 12 — a documented, deliberate deviation from skill §7.1. `[Authorize]` alone is on the controller.

## Rework r1

| Finding # | What I changed | Where |
|-----------|----------------|-------|
| 1 — Engineer-cap policy diverges from `docs/implementation-plan.md` ("published" vs any status) | Replaced the stale limits sentence so the doc states the confirmed rule (Decision 8 + Revision 5). Was: `Limits enforced in handlers: ≤50 published engineers, ≤10 teams/user, ≤50 versions/item.` Now: `Limits enforced in handlers: ≤50 engineers per creator (any status, non-deleted), ≤10 teams/user, ≤50 versions/item.` The reviewer's suggested wording, verbatim. No code change — `CreateEngineerHandler.cs:25` already counts all non-deleted engineers of any status, which is the locked rule; the doc was the side that was wrong. | `docs/implementation-plan.md:40` |

One line of one Markdown file. Nothing else was touched: no production code, no test, no `.editorconfig`, and the rest of `docs/implementation-plan.md` is unchanged (`git diff --stat` shows the file at `4 +--` — two lines total, the predecessor's Decision 6 edit at line 34 plus this one).

Non-blocking review items (the `[*.cs]`-wide CA1716 suppression scope, and the vendored `ValidateMaxLength` `.When` defect) were **not** addressed — out of scope for this rework, as instructed. Both remain accurate as written.

### Rework build & test

Re-run after the edit, verbatim:

```
$ dotnet build E3A.slnx -v q --nologo
Build succeeded.
    9 Warning(s)
    0 Error(s)
Time Elapsed 00:00:22.07
```

```
$ dotnet test E3A.slnx --nologo -v q
Passed!  - Failed:     0, Passed:    56, Skipped:     0, Total:    56, Duration: 354 ms - E3A.Tests.dll (net10.0)
```

Identical to the pre-rework outcome: the same 9 pre-existing `core-libraries` CS8618/CS8602 warnings, zero errors, 56/56 green. No regression. (A Markdown-only change could not have affected either, but both were run rather than assumed.)
