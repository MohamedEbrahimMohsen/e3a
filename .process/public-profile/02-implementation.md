# Implementation — Public creator profile (`/u/{login}`)

## What was built
`GET /api/catalog/creators/{login}` (anonymous) returning creator header + published engineers +
published teams, and `ProfilePage` rewritten onto it. Backend, tests, frontend, Postman and docs all
follow `01-plan.md`. Two deviations, both forced and both recorded below.

## Deviation 1 — login matching uses an explicit collation, not `ToUpper()`
The plan specified `x.GitHubLogin.ToUpper() == normalizedLogin`. That form does not compile here:
`TreatWarningsAsErrors` turns CA1304, CA1311 and CA1862 into build errors on it. CA1862's own
suggestion — `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` — has no EF Core translation
and would throw `InvalidOperationException` on every request, so following the analyzer produces a
runtime break instead of a build one.

Shipped instead:

```csharp
private const string CaseInsensitiveCollation = "SQL_Latin1_General_CP1_CI_AS";
...
EF.Functions.Collate(x.GitHubLogin, CaseInsensitiveCollation) == login
```

This keeps Decision 2's actual goal — case-insensitivity is a property of the code, not of an
unstated database default — while being translatable and analyzer-clean, and it needs no migration.
It does name a SQL Server collation inside the Application layer; the alternative that removes that
coupling is `.UseCollation(...)` on the column in `AppDbContext`, which the plan ruled out because it
requires a migration.

## Deviation 2 — `DetailHeader.tsx` also needed the optional-installs gate
The plan named only `EngineerCard.tsx` as affected by `CatalogItem.installs` becoming optional.
`tsc -b` caught a second construction site, `features/detail/DetailHeader.tsx:31`, passing
`number | undefined` to `formatInstalls`. Fixed the same way as the card: the install `<span>` (and
its separator) render only when `installs !== undefined`. This is exactly the compile-error-not-
silent-behaviour-change the plan predicted for the widening.

## Mutation checks (all three run, all three killed their named test)
| Mutation | Expected | Observed |
|---|---|---|
| Delete `.Where(x => x.Status == EngineerStatus.Published)` | test 3 fails | `Handle_ShouldReturnOnlyPublishedEngineers_…` FAILED, 7 others passed |
| Delete `.Where(x => x.Status == TeamStatus.Published)` | test 4 fails | `Handle_ShouldReturnOnlyPublishedTeams_…` FAILED, 7 others passed |
| `OrderByDescending` → `OrderBy` (engineers) | test 5 fails | `Handle_ShouldOrderEngineersByNewestFirst_…` FAILED, 7 others passed |

Handler restored after each; final state verified byte-identical with `cmp`.

## Stated as unproven by unit tests
The two repository **predicates** — the collation comparison on `GitHubLogin` and
`OwnerUserId == ownerUserId` — are not constrained by any test here, because NSubstitute matches the
expression argument without invoking it. Case-insensitive matching and ownership isolation are
therefore verified only manually, via the Postman `Get Creator Profile` request (mixed-case login
`MohamedEbrahimMohsen`) against a running API. No test may be cited as proof of either.

## Verification
- `dotnet test api/E3a.slnx` — 794 passed, 0 failed.
- `npm run build` — clean. `npm run test` — 11 files, 68 passed. `npx oxlint src` — no new warnings.
- Not run: the manual browser pass and the Postman request (both need a running API + database).

## Pre-existing flakiness noticed (not caused by this slice)
On a cold, CPU-loaded first run, `ScanRulesDangerousCommandTests` and `ScanRulesScriptTierTests` each
failed one case; both passed on re-run and in isolation. The scanner's regex match timeout is
wall-clock based, so scan outcomes depend on machine load — a real defect in the scanner's test
determinism, on `main`, unrelated to this feature.
