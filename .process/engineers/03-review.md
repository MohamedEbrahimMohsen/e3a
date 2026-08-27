VERDICT: CHANGES_REQUESTED

# Review — Engineer Drafts Management

## Blocking

### 1. Engineer-cap policy diverges from `docs/implementation-plan.md` — "published" vs any status
**Where:** `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs:25-30` vs `docs/implementation-plan.md:40` (§ Data model, "Limits enforced in handlers")
**Rule:** `.claude/rules/docs-sync.md` — "Policy changes: limits" is a divergence trigger; plan Decision 8 + Revision 5.
**Problem:** The handler counts **all** non-deleted engineers the caller owns, any status (`CountAsync(cancellationToken, x => x.OwnerUserId == ownerUserId)`), and this is the locked product rule — Revision 5 records Mohamed confirming "Limit counts all non-deleted engineers of any status — the stricter reading stands." But `docs/implementation-plan.md:40` still says "Limits enforced in handlers: ≤50 **published** engineers". The plan's own Decision 8 acknowledged the doc "phrases it as published", yet the Decision 6 doc edit fixed only the data-model bullet (line 34) and explicitly changed nothing else, leaving line 40 giving the old answer. Code and doc now answer "when does a creator hit the engineer cap?" differently. (`docs/architecture.md:26` and `docs/implementation-plan.md:9` say "50 engineers per creator" with no qualifier, so line 40 is the lone stale statement.)
**Failure:** A creator with 50 drafts and 0 published engineers: the doc says they may create more; `POST /api/engineers` returns 400 `ENGINEER_LIMIT_REACHED`.
**Fix:** Edit `docs/implementation-plan.md:40` to state the confirmed rule, e.g. "Limits enforced in handlers: ≤50 engineers per creator (any status, non-deleted), ≤10 teams/user, ≤50 versions/item." Nothing else changes.

## Non-blocking

- `api/.editorconfig:249` — the CA1716 suppression lands in the `[*.cs]` section, so it is disabled for every C# file in the solution, not just the `Shared` namespaces that triggered it (CA1716 also guards method and property names against keywords). A `[**/Shared/*.cs]` section would confine it. The deviation itself is well reasoned and documented; only the scope is broader than the report's "as narrowly as I could" suggests.
- `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs:14` (and the Update twin) — the vendored `ValidateMaxLength` appends a `.When` whose argument is the command, not the property, so the condition is vacuously true (implementer's Note 2, confirmed at `api/core-libraries/Core.Validation/Extensions/StringValidationExtensions.cs:17`). Harmless here; worth a core-libraries fix slice later.

## Verified

Independently re-ran and confirmed the report's claims:

- **Build:** `dotnet build E3A.slnx` — 0 errors, 9 warnings, all pre-existing CS8618/CS8602 inside `core-libraries` (Core.Notifications, Core.OTP, Core.Validation). Zero warnings in the five E3A projects.
- **Tests:** `dotnet test E3A.slnx` — 56/56 passed. Math checks: 47 `[Fact]` + 3 `[Theory]` × 3 `InlineData` = 56 cases = the 50 planned methods. Every one of the plan's 50 test rows exists with the exact class and method name; no extra test methods.
- **File inventory:** all 36 hand-written files (24 production + 12 test) exist at exactly the planned paths; 3 tool-generated migration files present; no unplanned production file. The modified set is exactly the plan's 9 files plus `api/.editorconfig` (declared Deviations 2–4). The `.claude/agents/*`, `.claude/commands/feature.md` and `docs/feature-pipeline.svg` changes in the working tree are the user's own pipeline edits, predating this slice — not the implementer's.
- **All five declared deviations are truthful and correctly scoped:** (1) `DraftManifestJson = null` and `LatestVersionId = null` in `Create` match the plan's stated invariant; (2–4) the three `.editorconfig` suppressions are real analyzer/convention collisions (CA1707 vs mandated test naming, CS8981 vs the confirmed `initial` migration name), each commented, migration files unedited; (5) the plan's "25 hand-written files" is indeed a miscount — its own tables list 36.
- **Migration:** `20260827082800_initial.cs:365-370` has `IX_Engineers_Slug` unique with `filter: "[IsDeleted] = 0"` (Decision 7), `IX_Engineers_OwnerUserId`, and column widths `Slug`/`DisplayName` nvarchar(100), `Description` nvarchar(500) NULL, `Tags` nvarchar(400) NOT NULL, `Status` nvarchar(50) — all matching the `Engineer` constants. No connection string or Firebase value committed anywhere.
- **Plan decisions honoured in code:** Decision 2 (`AuditEntity` — confirmed the vendored `AggregateRoot` truly has no audit fields), 5 (slug never recomputed in `UpdateEngineerHandler`), 8 (no literal `50` in any `.cs`; cap bound from the `EngineerLimits` section), 9 (five consts referenced by validators and `ConfigureEngineers`), 14 (`Remove()` + `Update` + one `SaveChangesAsync`; `Delete` never called, and test 47 asserts `DidNotReceive().Delete`), 16/17 (handler-side `OrderByDescending`, non-nullable `FindAsync`, no null-coalescing), 19/20 (`AddValidatorsFromAssembly` + options binding + `mediatRConfiguration` rename; `Program.cs` differs by exactly one line), 25 (`Tags` null-coalesced at the controller boundary only), 28 (`DbSet` without a null-forgiving initialiser).
- **Skill §8 sweep:** file-scoped namespaces, `sealed record` commands/queries, `sealed class` handlers/validators, one-line declarations, `.ConfigureAwait(false)` on every production `await`, no `try`/`catch`, `DateTimeOffset` only, guard-current-user-first in all five handlers, `SaveChangesAsync` once per success path, exactly one production comment (the `Engineer` constants note), no file over 100 lines (max 96), query filter registered in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`, explicit named config method, open-generic `IRepository<>` correctly not re-registered (already in `Core.EntityFrameworkCore/DependencyInjection.cs:21`). `[Authorize]` without policy is the plan's approved Decision 12 deviation from skill §7.1, confirmed by Revision 3.
- **Error codes and resources:** 12 constants in `ErrorCodes.cs` under `// Engineers`; the same 12 keys in both resx files; `{limit}` placeholder intact in both languages.
- **Docs edit:** the Decision 6 rewrite of the data-model bullet at `docs/implementation-plan.md:34` was made verbatim, including the `InstallCount` fix.
- **Controller contract:** route `api/engineers`, action order per plan, `CreatedAtAction(nameof(GetEngineer), ...)` for 201, `NoContent()` for delete, `CancellationToken` on every action, no business logic and no `ICurrentUserService` in the controller.

## Test quality

- `EngineerTests` — genuinely constraining: copy-semantics tests mutate the source list after the call, date tests use a captured `before` with `BeOnOrAfter`, `Remove` asserts both `Status` and `IsDeleted`. Good.
- `EngineerSlugGeneratorTests` — pins the real algorithm on six worked inputs including truncation-then-trim and non-ASCII. Good.
- `CreateEngineerHandlerTests` — not vacuous: the happy path asserts the result slug, which is computed by the real `EngineerSlugGenerator` from the command's display name, not stubbed. Each throwing path asserts the error-code constant plus `DidNotReceive()` on `AddAsync`/`SaveChangesAsync`. Good.
- `UpdateEngineerHandlerTests` / `DeleteEngineerHandlerTests` — assert mutations on the real entity returned by the substitute (slug unchanged, `IsDeleted` true), `Received(1)` on `Update`/`SaveChangesAsync` on success, `DidNotReceive` on all three throwing paths. Good.
- `ListMyEngineersQueryHandlerTests` — the ordering test works precisely because ordering is handler-side LINQ over a stubbed list (Decision 16 anticipated this); distinct `creationDate` values keep it deterministic. Good.
- Validator tests — real validators, no substitutes, one passing case plus a failing case per rule, all asserting `ErrorCodes.*` constants. Good.
- No vacuous test found: every substitute-returned value is either transformed by real code before assertion or paired with an interaction assertion.
