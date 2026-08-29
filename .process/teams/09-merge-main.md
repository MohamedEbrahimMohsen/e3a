# Merge `origin/main` into `feature/teams`

**Merge commit**: `3f9df57` (parents `cbce682` teams, `5bfa7cd` main)
**Pushed**: `origin/feature/teams` (`cbce682..3f9df57`)
**Merge base**: `6def01a`

PR #5 (`github-oauth`) merged to `main` after this branch was cut. Five shared files
conflicted. All five are additive collisions — both slices legitimately adding to the
same file — so every resolution is a union. Nothing was dropped from either side and
no refactor was made while in these files.

## The five conflicts

| File | Conflict | Resolution |
|---|---|---|
| `api/E3A.Api/Resources/Messages.en.resx` | One region at the tail: 28 team keys (HEAD) vs 6 authentication keys (main). The region ended mid-entry, so the trailing `</data>` was shared context. | Kept all 34. Emitted teams block, a closing `</data>`, then the OAuth block. **53 base + 34 = 87 keys.** |
| `api/E3A.Api/Resources/Messages.ar.resx` | Same shape, Arabic. | Same union, 87 keys. |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | Only the primary-constructor line conflicted; git auto-merged the whole body. | Constructor now takes **both** `IOptions<TeamsOptions>` and `IOptions<GitHubAuthenticationOptions>`, ordered to preserve both sides' relative sequences. |
| `api/E3A.Infrastructure/DependencyInjection.cs` | Only the `using` block; the method body auto-merged with both registrations. | Kept `E3A.Domain.Teams` and `E3A.Infrastructure.Authentication`, alphabetical. |
| `docs/architecture.md` | Adjacent-line collision: main inserted the Auth bullet, teams edited the Limits line. | Kept main's browser-bound-state Auth bullet **and** teams' Limits line (`10 members per team`). |

`AppDbContext.cs` verified to carry both parameters, both `ConfigureUsers`/`ConfigureTeams`
calls from `OnModelCreating`, and all five registrations in
`ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries` (User, Engineer, Team, TeamMember, ItemVersion).

## Resource file checks

Both files parse as valid XML. `en` and `ar` key sets are **identical** (87 each), no duplicates,
no empty values. All 6 OAuth-added and all 28 teams-added keys present; all 53 base keys preserved.
No tashkeel or tatweel in any Arabic value. Placeholders (`{limit}`, `{engineerId}`, `{path}`)
match one-for-one between `en` and `ar`.

## Union integrity

Line-level diff of the merged result against both parents and the base: every line either
side added is present in the merge, and no line appears that came from neither parent. The
single exception is the `AppDbContext` primary-constructor line, which necessarily became a
new combined line holding both parameter sets — that is the intended union, not an invention.

## Migration snapshot — verified, not assumed

`AppDbContextModelSnapshot.cs` auto-merged with **no conflict marker**, so it was checked directly:

```
dotnet ef migrations add mergecheck --project api/E3A.Infrastructure --startup-project api/E3A.Api
```

The generated migration had **empty `Up` and `Down`** — the snapshot already describes the union
of both models. No regeneration or hand-editing was needed.

Confirmed present in the snapshot: the four `AspNetUsers` GitHub columns (`GitHubId`,
`GitHubLogin`, `DisplayName`, `AvatarUrl`) and the filtered unique index
`[GitHubId] IS NOT NULL AND [IsDeleted] = 0` from `oauth004`, plus the
`E3A.Domain.Teams.Team` and `E3A.Domain.Teams.TeamMember` entities from `teams005`.

The scratch migration was **deleted and is not committed**. Note: `dotnet ef migrations remove`
could not be used because the empty migration itself fails SonarAnalyzer `S1186` ("add a nested
comment explaining why this method is empty"), which broke the build the tool needs — itself
extra confirmation the migration was empty. The two files were deleted directly; the snapshot's
SHA-256 was identical before and after the scratch run (`608fcfa94ac762ee`), so nothing was disturbed.

## Migration ordering

Both real migrations survive with their original timestamps:

| Migration | Timestamp | Operations |
|---|---|---|
| `oauth004` | `20260829112516` | 4 `AddColumn` + 1 `CreateIndex` on `AspNetUsers` |
| `teams005` | `20260829124339` | `CreateTable` `Teams`, `CreateTable` `TeamMembers` |

`20260829112516 < 20260829124339`, so `oauth004` applies before `teams005`. They touch
disjoint tables, so apply order is safe either way.

## Build and test

```
dotnet build api/E3A.slnx --no-incremental
  0 Error(s), 9 Warning(s)
```
All 9 warnings are pre-existing and in `api/core-libraries`: Core.Notifications (5),
Core.OTP (2), Core.Validation (2). Matches the expected 0 / 9.

```
dotnet test api/E3A.slnx
  Passed! - Failed: 0, Passed: 604, Skipped: 0, Total: 604
```

**604 — exactly the predicted 521 + 437 − 354.** Both suites compose cleanly with no
overlap and no adjustment to the expectation was needed.

## Other files both branches touched (auto-merged, spot-checked)

`ErrorCodes.cs`, `E3A.Application/DependencyInjection.cs`, `docs/implementation-plan.md` and
`postman/e3a.postman_collection.json` merged without conflict. The Postman collection is valid
JSON and contains both the OAuth **Authentication** folder (3 requests) and the **Teams** folder
(8 requests). `implementation-plan.md` retains main's Auth API-surface rewrite and teams'
data-model, plugin-spec and P5 edits.

## Judgment calls

None required beyond mechanical union. One observation, not a defect:

- `20260829124339_teams005.Designer.cs` does not contain the GitHub columns, and
  `20260829112516_oauth004.Designer.cs` does not contain the Teams entities. Each per-migration
  Designer captures the model as it stood on its own branch. This is the normal artefact of
  merging two branches that each added a migration; EF diffs new migrations against
  `AppDbContextModelSnapshot.cs` (verified correct above), not against these. Regenerating
  `teams005` would rewrite an already-reviewed migration and change its timestamp, so it was
  left alone — consistent with the instruction to regenerate only if the mergecheck migration
  came out non-empty. It did not.
