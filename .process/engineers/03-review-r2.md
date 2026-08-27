VERDICT: APPROVED

# Review r2 — Engineer Drafts Management

## Blocking

None.

## Round-1 finding resolution

### 1. Engineer-cap policy diverges from `docs/implementation-plan.md` — RESOLVED
Both sides now give the same answer to "when does a creator hit the engineer cap?":

- **Doc:** `docs/implementation-plan.md:40` — `Limits enforced in handlers: ≤50 engineers per creator (any status, non-deleted), ≤10 teams/user, ≤50 versions/item.` This is round 1's suggested wording, verbatim.
- **Code:** `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs:25-30` — `CountAsync(cancellationToken, x => x.OwnerUserId == ownerUserId)` with no status predicate; soft-deleted rows are excluded by the global query filter registered in `AppDbContext`. Unchanged since round 1, correctly — the code was the side that matched Decision 8 / Revision 5 ("stricter reading stands"); the doc moved.

Swept `/docs` for any other statement of the cap: `docs/implementation-plan.md:9` and `docs/architecture.md:26` say "50 engineers per creator" with no status qualifier (consistent, not divergent); `docs/design-prompt.md:20` is UI copy with no qualifier. No remaining divergence.

## Non-blocking

Carried forward from round 1, deliberately unaddressed in this rework (as instructed) and still accurate:

- `api/.editorconfig:249-253` — the CA1716 suppression sits in the file-wide section rather than scoped to `Shared` namespaces; a `[**/Shared/*.cs]` section would confine it.
- `api/core-libraries/Core.Validation/Extensions/StringValidationExtensions.cs:17` — vendored `ValidateMaxLength` appends a `.When` whose argument is the command, not the property, so the condition is vacuously true. Harmless here; candidate for a core-libraries fix slice.

## Verified

- **Rework scope is exactly what the report claims.** `git diff -- docs/implementation-plan.md` shows the file at `4 ++--`: two changed lines total — line 34 (the predecessor's Decision 6 data-model bullet, already verified in round 1) and line 40 (this fix). No production code, no test, no `.editorconfig` change since round 1; the modified/untracked file set is identical to the set round 1 inventoried (the plan's 9 files + `api/.editorconfig`, plus the user's own `.claude/agents/*`, `.claude/commands/feature.md`, and `docs/feature-pipeline.svg`, which predate the slice).
- **Rework build claim:** independently re-ran `dotnet build E3A.slnx` — 0 errors, 9 warnings, all pre-existing CS8618 inside `core-libraries` (Core.Notifications et al.). Zero warnings in the five E3A projects.
- **Rework test claim:** independently re-ran `dotnet test E3A.slnx` — 56/56 passed, 0 skipped.
- **Spot-checked untracked production and test files against the plan** (git cannot prove untracked files unchanged since round 1, so sampled across layers): `api/E3A.Domain/Engineers/Engineer.cs` (constants, private ctor, `Create` with the declared Deviation-1 `DraftManifestJson = null` / `LatestVersionId = null`, `[.. tags]` copies, `UpdationDate` stamps, `Remove()` → `SoftDelete()`), `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs` (guard → cap → slug → conflict → create → single `SaveChangesAsync`, `.ConfigureAwait(false)` throughout), `api/E3A.Application/Engineers/DeleteEngineer/DeleteEngineerHandler.cs` (`Remove()` + `Update`, `Delete` never called), `api/E3A.Api/Controllers/Engineers/EngineersController.cs` (plan's action order, `CreatedAtAction(nameof(GetEngineer))`, `NoContent()`, `?? []` at the boundary only), `api/E3A.Tests/Engineers/DeleteEngineer/DeleteEngineerHandlerTests.cs` (plan tests 47–50 exactly, including `DidNotReceive().Delete`), and the three scoped `.editorconfig` suppressions with justifying comments. All match what round 1 verified.
- Round 1's remaining verifications (full file inventory, all 50 test rows, skill §8 sweep, error codes and both resx files, migration indexes, plan decisions 2/5/8/9/14/16/17/19/20/25/28) stand — the tree differs from what round 1 reviewed by exactly one Markdown line, and nothing observed contradicts them.

## Test quality

Unchanged since round 1 (no test was touched in the rework). Round 1's per-class assessment stands: no vacuous test — every substitute-returned value is either transformed by real code before assertion (e.g. the create happy path asserts the slug computed by the real `EngineerSlugGenerator`) or paired with `Received(1)`/`DidNotReceive()` interaction assertions. Spot-check of `DeleteEngineerHandlerTests` confirms: success path asserts real entity state (`IsDeleted`, `Status`) plus `Received(1)` on `Update`/`SaveChangesAsync` and `DidNotReceive().Delete`; all three throwing paths assert the `ErrorCodes.*` constant plus `DidNotReceive().SaveChangesAsync`.
