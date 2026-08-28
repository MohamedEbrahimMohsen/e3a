# Implementation — Creator-Typed Engineer Slug

## Files created

Exactly the 13 files listed in the plan's *Files to create*. No others.

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Application/Engineers/Shared/EngineerSlugResolver.cs` | 28 | `IsSlugExistsAsync` + `IGenerator` race guard, extracted once for three call sites; trims the trailing separator Core's `IGenerator` emits |
| `api/E3A.Application/Engineers/Shared/SlugAvailabilityResult.cs` | 3 | `sealed record SlugAvailabilityResult(string Slug, bool IsAvailable, string? SuggestedSlug)` |
| `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQuery.cs` | 6 | `sealed record … : IRequest<SlugAvailabilityResult>` |
| `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryValidator.cs` | 43 | Canonical slug rule block, ungated |
| `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandler.cs` | 35 | Auth guard → normalize → exists? → resolve suggestion. No `SaveChangesAsync`, no `try`/`catch` |
| `api/E3A.Tests/Engineers/EngineerSlugTests.cs` | 36 | Tests 1–3 (`ChangeSlug`, `IsSlugMutable`) |
| `api/E3A.Tests/Engineers/EngineerSlugGeneratorTypedInputTests.cs` | 47 | Tests 4–7 (`NormalizeTypedSlug`, `IsValidFormat`) |
| `api/E3A.Tests/Engineers/Shared/EngineerSlugResolverTests.cs` | 68 | Tests 8–11 (free / trailing-separator / retry / prefix shortening) |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerSlugValidatorTests.cs` | 78 | Tests 17–22 |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerSlugValidatorTests.cs` | 80 | Tests 23–29 |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerSlugHandlerTests.cs` | 114 | Tests 30–35 (see Deviation 6) |
| `api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryValidatorTests.cs` | 73 | Tests 36–41 |
| `api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandlerTests.cs` | 78 | Tests 42–45 |

## Files modified

| Path | Change |
|------|--------|
| `api/E3A.Domain/Engineers/Engineer.cs` | `IsSlugMutable => LatestVersionId == null;` under `InstallCount`; `ChangeSlug(string slug)` after `UpdateMetadata` (sets `Slug`, stamps `UpdationDate`). `Slug` stays `private set` |
| `api/E3A.Domain/Engineers/EngineerSlugGenerator.cs` | `SlugFormatRegex` + timeout constant; `NormalizeTypedSlug`, `IsValidFormat`. `Normalize(displayName, maxLength)` untouched |
| `api/E3A.Application/Options/EngineersOptions.cs` | `SlugMinLength` (after `SlugSuffixSize`), `ReservedSlugs` (last, `= []`) |
| `api/E3A.Api/appsettings.json` | `"SlugMinLength": 3` and the 15-entry `"ReservedSlugs"` array in the `Engineers` section (see Deviation 5) |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | 6 constants after `EngineerDraftNotUploaded`, in plan order |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerCommand.cs` | `(string Slug, string DisplayName, string? Description, List<string> Tags)` |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs` | Canonical slug rule block above the `DisplayName` rules |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs` | `GenerateUniqueSlugAsync` deleted; resolver call on `NormalizeTypedSlug(request.Slug)`. Constructor unchanged |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerCommand.cs` | `(Guid EngineerId, string? Slug, string DisplayName, string? Description, List<string> Tags)` |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerValidator.cs` | Same block, every rule additionally `.When(x => x.Slug != null …)` |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs` | Constructor gains `IGenerator`, `IOptions<EngineersOptions>`; `ResolveSlugChangeAsync` runs (and throws `EngineerSlugFrozen`) **before** `UpdateMetadata`; one `SaveChangesAsync` |
| `api/E3A.Api/Controllers/Engineers/Requests.cs` | `Slug` added first on both request records (`string` / `string?`) |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | `GET slug-availability` action after `ListMyEngineers`; `request.Slug` threaded into both commands |
| `api/E3A.Api/Resources/Messages.en.resx` | 6 entries after `ENGINEER_DRAFT_NOT_UPLOADED` |
| `api/E3A.Api/Resources/Messages.ar.resx` | Same 6 keys, same order, Arabic without tashkeel |
| `postman/e3a.postman_collection.json` | `Check Slug Availability` added; `slug` first field of the Create and Update bodies |
| `docs/plugin-spec.md` | Lines 11, 87, 94. Line 90 (`author`) untouched |
| `docs/implementation-plan.md` | Data-model `Slug (...)` parenthetical, `**Naming**` bullet, API-surface `[auth/owner]` list |
| `docs/design-prompt.md` | Install command example → `e3a-mmohsen@e3a` |
| `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` | `DefaultReservedSlug = "admin"`; options gain `SlugMinLength = 3` + full `ReservedSlugs` |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerHandlerTests.cs` | 2 renamed/retargeted, 1 added, `Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken` deleted, 2 signature-only |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerValidatorTests.cs` | 8 call sites gain `EngineerFactory.DefaultSlug` (no test added/removed) |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerHandlerTests.cs` | Constructor wires `_generator` + options; 4 call sites gain `null` slug |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerValidatorTests.cs` | 9 call sites gain `null` slug (no test added/removed) |

`tools/E3A.Seeder/Program.cs` verified untouched and unaffected — it calls `Engineer.Create(...)` (unchanged) and initialises `EngineersOptions` by object initialiser, so the two new properties are non-breaking. No `web/` code references `api/engineers`.

## Deviations

| Plan said | Reality | What I did |
|---|---|---|
| 1. `private static readonly Regex SlugFormatRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);` verbatim | This does not compile in this repo. `api/Directory.Build.props` sets `TreatWarningsAsErrors=true` with the SonarAnalyzer package, and **S6444 ("Pass a timeout to limit the execution time") is raised as an error** on that exact line: `E3A.Domain/Engineers/EngineerSlugGenerator.cs(9,53): error S6444`. It is the first `Regex` in the solution, so there was no precedent to mirror | Added a named constant with a WHY comment (skill §1 "invariants → named constant with a WHY comment") and passed it: `private static readonly TimeSpan SlugFormatMatchTimeout = TimeSpan.FromMilliseconds(100);` then `new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled, SlugFormatMatchTimeout)`. The timeout is a defence-in-depth invariant on a non-backtracking pattern, not a product tunable, so it stays a constant rather than moving to `EngineersOptions` (which `E3A.Domain` cannot see anyway) |
| 2. *Existing code touched* says "Add the **4** slug rules" for both `CreateEngineerValidator` and `UpdateEngineerValidator` | The canonical rule block and the rule-to-error-code map both define **5** rules (required, too short, too long, invalid format, reserved) | Implemented all 5 in every one of the three validators. The "4" appears to count only the `.Must` rules and exclude `ValidateRequired`; the canonical block is the authority per the plan's own wording |
| 3. Postman: "`Check Slug Availability` to the `Engineers` folder (position 2, after `List My Engineers`)" vs. "inserted at index 1 (right after `List My Engineers`)" — the two statements contradict each other (`List My Engineers` is itself at index 1) | Only one of the two can be honoured | Followed the semantic instruction ("right after `List My Engineers`") — the new request is at index 2. Folder order is now: Create Engineer, List My Engineers, Check Slug Availability, Get Engineer, Update Engineer, Upload Engineer Draft, Get Import Manifest, Delete Engineer |
| 4. `docs/implementation-plan.md` **line 55** — add the availability route to the `Engineers:` `[auth/owner]` list | Line 55 is blank; the API-surface paragraph is line 56 | Edited line 56 (the sentence the plan describes). Lines 34 and 44 matched the plan's numbering exactly |
| 5. `api/E3A.Api/appsettings.json` — add `SlugMinLength` and `ReservedSlugs` | The file is **gitignored** (`.gitignore:23 /api/E3A.Api/appsettings.json`); only `appsettings.Development.json` (logging only) is tracked | Applied the edit exactly as specified. It works locally and at runtime, but **it will not appear in the commit or the review diff** — flagging so the reviewer does not read its absence as a missing change, and so whoever provisions other environments adds the same two keys there |
| 6. `UpdateEngineerSlugHandlerTests.cs` — 6 tests, one file; and "no new file exceeds ~100 lines" | The 6 mandated tests come to 114 lines. Splitting would create a 14th file and break "create exactly the 13 listed files" | Kept the single file at 114 lines. Both constraints cannot hold at once; I chose the hard file-count contract over the soft "~100" guideline. Repo precedent exists (`EngineerTests.cs` is 151 lines). Every other new file is ≤ 80 lines |

Nothing in the plan was left unimplemented.

## Build & test

```
dotnet build api/E3a.slnx
Build succeeded.
    9 Warning(s)
    0 Error(s)
```

All 9 warnings are pre-existing and originate in `api/core-libraries/` (`Core.Validation` CS8602 ×2, `Core.OTP` CS8618 ×2, `Core.Notifications` CS8618 ×5). **Zero warnings from any `E3A.*` project** — none new.

```
dotnet test api/E3A.Tests/E3A.Tests.csproj
Passed!  - Failed: 0, Passed: 236, Skipped: 0, Total: 236, Duration: 1 s - E3A.Tests.dll (net10.0)
```

Additional checks run:

- All 45 enumerated test method names verified present by name; `Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken` verified absent.
- Every `ErrorCodes` constant has a key in `Messages.en.resx`; en/ar key sets are identical **and in identical order** (38 keys each).
- `grep -rn "githublogin\|e3a-mohamed-dive-backend-engineer" docs/` → no matches.
- `postman/e3a.postman_collection.json` parses as JSON.
- Nothing under `api/core-libraries/` was modified (`git status` confirms).

## Notes for review

- **Deviation 1 is the one to look at first.** It is the only place I changed a line the plan wrote out verbatim, and it was forced by the build.
- **Deviation 5** — `appsettings.json` will be invisible in the diff. The values are `"SlugMinLength": 3` and the 15-entry reserved array exactly as the plan lists them.
- `EngineerSlugResolver.cs` carries the two WHY comments the plan explicitly sanctioned, and no others. All other new production code has zero comments.
- `UpdateEngineerValidator` writes `.When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug))` as the plan dictates. The first clause is logically redundant (the second implies it); I kept it because the plan specified that exact condition and it documents the "null means leave unchanged" intent at the call site.
- The freeze ordering is covered by test 35, which asserts `engineer.DisplayName` is still the original after the throw — a passing signal that `ResolveSlugChangeAsync` really does run before `UpdateMetadata`.
- Test 11 (`ResolveUniqueAsync_ShouldShortenPrefix_…`) asserts `prefix.Length == 95` from `SlugMaxLength 100 - SlugSuffixSize 4 - 1`. If those option values ever move, this assertion moves with them; it is deliberately literal per the plan.
- No `ConflictCoreException` anywhere in the slice; the `IsSlugExistsAsync` + `IGenerator` loop is intact and now shared by create, update and availability (skill §8.3).
- No EF migration, no new repository method, no new exception type, no `DefaultCodes` introduced (Decision #13 — the class does not exist in this repo, and the new action inherits the controller's class-level `[Authorize]`).

---

## Rework round 1

Rework mode. One blocking finding from `03-review.md`; scope held to the README fix.

### Finding resolution

| # | Finding | What I changed | Where |
|---|---|---|---|
| 1 | `README.md:9` documents the superseded `e3a-<creator>-<engineer>` plugin-name contract, contradicting `docs/plugin-spec.md:11`, `docs/implementation-plan.md:44`, `docs/design-prompt.md:16` | Replaced the install line with `/plugin install e3a-<slug>@e3a` | `README.md:9` |

Chose the placeholder form `e3a-<slug>` over the concrete `e3a-mmohsen` because the paired line
directly above is `/plugin marketplace add https://<domain>/marketplace.json` — also a placeholder.
Mixing a placeholder host with a concrete slug would break the copy-paste block's register; the two
lines now read consistently as "fill in both angle-bracket values". The concrete `e3a-mmohsen` form
remains in `docs/design-prompt.md:16` and `docs/plugin-spec.md:87`, where it is a worked example
rather than a template, so both registers stay correct in their own context.

Diff is exactly one line, one file:

```
-/plugin install e3a-<creator>-<engineer>@e3a
+/plugin install e3a-<slug>@e3a
```

Nothing else touched. The 6 previously-declared deviations are untouched, `.process/` history is
untouched, and the out-of-scope items the reviewer named (`README.md:21` "isolated Azure Functions",
the two non-blocking code items) were deliberately left alone.

### Build & test

Both re-run against the baseline stated in the task.

- `dotnet build api/E3a.slnx` → `Build succeeded. 9 Warning(s), 0 Error(s)`. Matches baseline
  exactly. All 9 warnings are the pre-existing `core-libraries` set: `Core.Validation` CS8602 x2
  (`RequiredValidationExtensions.cs:52,57`), `Core.OTP` CS8618 x2 (`OTP.cs:30`),
  `Core.Notifications` CS8618 x5 (`NotificationTemplate.cs:15` x3, `Notification.cs:35` x2). Zero
  warnings from any `E3A.*` project.
- `dotnet test api/E3A.Tests/E3A.Tests.csproj` →
  `Passed!  - Failed: 0, Passed: 236, Skipped: 0, Total: 236, Duration: 290 ms`. Matches baseline
  exactly.

A markdown-only change cannot move either number; both were run to confirm no drift, not because
the edit could affect them.

### Verification grep — re-run properly

Repo-wide from the repo root, excluding `.process/`, `.git/`, `.vs/`, `node_modules/`, `bin/`,
`obj/`, covering both spellings the original grep missed:

```
grep -rn -i -e '<creator>' -e '{creator}' -e 'githublogin' -e 'e3a-mohamed-dive' .
```

Exit 0, one hit:

```
./docs/implementation-plan.md:33:- `users`: Id, GitHubId (unique), GitHubLogin, DisplayName, AvatarUrl, IsBlocked
```

**Not stale.** That is the `users` table column list, not the plugin-name contract. GitHub login
still legitimately exists as a user attribute — `docs/plugin-spec.md:11` says the login is no longer
part of the *plugin name* and that "attribution lives in the `author` field", which requires the
column to survive. Correct as written.

A second grep for every `/plugin install` occurrence repo-wide confirms `README.md:9` is now the
`e3a-<slug>` form and `docs/design-prompt.md:16` is `e3a-mmohsen@e3a`. The finding is closed.

### New finding — out of scope, not fixed, needs a decision

The repo-wide grep surfaced something the task's premise did not account for. The task stated
`README.md:9` was the only remaining stale reference repo-wide; that is true for the two spellings
searched, but the frontend expresses the same superseded contract in a form neither pattern matches
(`e3a-${author}-${name}`, a template literal). **Four tracked source lines** still build the
three-part name:

| File:line | Code |
|---|---|
| `web/src/lib/config.ts:14` | ``return `/plugin install e3a-${author}-${name}@e3a`;`` |
| `web/src/features/composer/EngineerComposerPage.tsx:27` | ``const draftSlug = `e3a-${login}-${…}`;`` |
| `web/src/features/composer/EngineerComposerPage.tsx:28` | ``const agentFile = `${draftSlug.replace(`e3a-${login}-`, '')}.md`;`` |
| `web/src/features/composer/TeamComposerPage.tsx:38` | ``slug: e3a-{login}-fintech-launch-crew`` |

`installCommand(author, name)` in `config.ts` is the live function behind the site's install block,
so the rendered command is `/plugin install e3a-{author}-{name}@e3a` — the exact string this slice
deleted. (`web/dist/assets/index-DOu2dHns.js` carries the compiled form, but `dist` is gitignored
via `web/.gitignore:11` and untracked, so it is a stale local build artifact, not a source of truth.)

**I did not touch these.** Three reasons, in order of weight: rework mode says address only the
numbered blocking finding; the task scoped me strictly to the README; and no `web/` file appears in
the plan's *Existing code touched*, so editing one would itself be a contract violation.

Whether this is blocking is a judgement call for the reviewer, and I want to be explicit that it is
arguable rather than pretend it is settled:

- **Reads as incompleteness** — the composer is unbuilt prototype UI on hardcoded mock data
  (`EngineerComposerPage.tsx` seeds a literal skill array; the review's own non-blocking note says
  the create form "actually lands with the OAuth slice"). Code lagging a target contract is the
  normal in-progress state `.claude/rules/docs-sync.md` tells us never to flag.
- **Reads as divergence** — `installCommand` is not unbuilt scaffolding; it renders a command to
  visitors today, and that command cannot resolve after this change. It is the same user-visible
  failure the reviewer used to justify blocking on `README.md:9`, one layer down.

My read is that `config.ts:14` is genuinely divergent and the three composer lines are
prototype incompleteness, but I am reporting rather than acting on that. It is a frontend slice's
work either way, not a rewrite I should smuggle into a README fix.

### Notes for review

- The one-line README change is the whole of this round. `git diff --stat` is
  `README.md | 2 +-`, `1 file changed, 1 insertion(+), 1 deletion(-)`.
- Git reports `LF will be replaced by CRLF` on `README.md`. Pre-existing repo line-ending config,
  not something this edit introduced.
- The frontend finding above is the only thing I would want a second opinion on before this merges.

---

## Rework round 1 — item 2

Scoped rework of the frontend divergence surfaced at the end of the README round (the "New finding"
section above), now verified by the orchestrator and accepted as divergence. Same round 1; the fresh
reviewer has not run yet.

### Finding resolution

| # | Finding | What I changed | Where |
|---|---|---|---|
| 1 | `installCommand(author, name)` builds the superseded three-part plugin name `e3a-{author}-{name}`, contradicting `docs/plugin-spec.md:11` | Signature now takes the slug alone; returns `/plugin install e3a-${slug}@e3a` | `web/src/lib/config.ts:13-15` |
| 2 | Four call sites pass a now-meaningless author argument | Dropped the author argument at all four | `EngineerDetailPage.tsx:66`, `HomePage.tsx:40`, `TeamDetailPage.tsx:21`, `PublishStatusPage.tsx:76` |

The whole diff, five lines across five files:

```
-export function installCommand(author: string, name: string): string {
-  return `/plugin install e3a-${author}-${name}@e3a`;
+export function installCommand(slug: string): string {
+  return `/plugin install e3a-${slug}@e3a`;

-installCommand('creator', engineer.slug)                              → installCommand(engineer.slug)
-installCommand('mohamed', engineers[0]?.slug ?? 'dive-backend-...')   → installCommand(engineers[0]?.slug ?? 'dive-backend-...')
-installCommand(item.author ?? 'creator', item.name)                   → installCommand(item.name)
-installCommand('mohamed-dive', 'payments-engineer')                   → installCommand('payments-engineer')
```

`item.author` was read only at that one line in `TeamDetailPage` (line 33 reads `member.author`, a
different object), so dropping it leaves nothing unused. The composer pages' hardcoded mock data was
not touched — no call site of theirs exists.

### `pinnedMarketplaceCommand` — verified, not assumed

`config.ts:17-19` interpolates `pluginName` whole into `/m/{pluginName}/{version}/marketplace.json`;
it never composes a name from parts. Its only call site is `VersionHistory.tsx:28`, which receives
`pluginName` as a prop, and the only place that prop is supplied is `TeamDetailPage.tsx:48` —
`` `${item.name}-team` ``, two-part and author-free. Consistent with the new contract at every hop.
No change needed, as the task predicted.

### Build & test

| Command | Result |
|---|---|
| `npm run build` in `web/` (= `tsc -b && vite build`) | Succeeded. `✓ 52 modules transformed`, `✓ built in 136ms`. Zero TypeScript errors — `tsc -b` is the first half of the script, so the type-check is covered and I did not need a separate `npx tsc --noEmit` |
| `dotnet build api/E3a.slnx` | `Build succeeded. 9 Warning(s), 0 Error(s)` — matches baseline exactly, same 9 pre-existing `core-libraries` warnings (`Core.Validation` CS8602 x2, `Core.OTP` CS8618 x2, `Core.Notifications` CS8618 x5) |
| `dotnet test api/E3A.Tests/E3A.Tests.csproj` | `Passed! - Failed: 0, Passed: 236, Skipped: 0, Total: 236, Duration: 337 ms` — matches baseline exactly |

A frontend-only change cannot move the API numbers; both were run to confirm no drift.

The rebuilt bundle carries the corrected string — `grep` of `web/dist/assets/index-BGAQlNtI.js` gives
`/plugin install e3a-${e}@e3a`. `dist/` is gitignored, so this is a local sanity check only; the
stale `index-DOu2dHns.js` cited in round 1 is superseded by this rebuild.

I could not verify the rendered page in a browser — no preview tool is exposed to this agent. The
vite build plus the bundle grep are the closest available substitute.

### Verification grep — remaining three-part `e3a-` construction

`web/src` grep for `e3a-`, after the change, returns four hits. One is unrelated
(`config.ts:4`, the `github.com/e3a-registry` org URL). `config.ts:14` is now the corrected two-part
form. The remaining **three** are all in the composer pages, explicitly out of scope per the task:

| File:line | Code |
|---|---|
| `web/src/features/composer/EngineerComposerPage.tsx:27` | `` const draftSlug = `e3a-${login}-${…}`; `` |
| `web/src/features/composer/EngineerComposerPage.tsx:28` | `` const agentFile = `${draftSlug.replace(`e3a-${login}-`, '')}.md`; `` |
| `web/src/features/composer/TeamComposerPage.tsx:38` | `slug: e3a-{login}-fintech-launch-crew` |

Left untouched deliberately. These are the hardcoded prototype/mock surfaces pending the OAuth slice,
which both the task and my round-1 write-up classify as incompleteness rather than divergence.

### New finding — `PublishStatusPage.tsx` is not in the repo at all

Worth flagging loudly, because it changes what the reviewer will see in the diff and because it is a
pre-existing repo defect, not something this slice introduced.

While checking why two separate repo-wide `installCommand` greps returned only **three** of the four
call sites, I found the fourth file is invisible to ripgrep because it is git-ignored:

```
git check-ignore -v web/src/features/publish/PublishStatusPage.tsx
.gitignore:20:publish/    web/src/features/publish/PublishStatusPage.tsx
```

`.gitignore:20` is `publish/` under a `# Build artifacts` heading — a .NET `dotnet publish` output
rule. Being a bare directory pattern with no leading slash, it matches **any** directory named
`publish` at any depth, so it also swallows the frontend feature folder `web/src/features/publish/`.
`git ls-files web/src/features/publish/` is empty and `git status --porcelain --ignored web/src`
lists `!! web/src/features/publish/` — the file has never been committed.

Two consequences:

1. **My edit to `PublishStatusPage.tsx:76` is real on disk and is type-checked and bundled by the
   build, but it will not appear in the commit or the review diff.** Same shape as Deviation 5
   (`appsettings.json`). Please do not read its absence as an unimplemented call site — three of the
   four call-site edits are in the diff, the fourth is not, and that is why.
2. **A fresh clone of this repo cannot build the web app.** Tracked file `web/src/App.tsx:59` imports
   and routes `PublishStatusPage`, but the module it imports is not in the repository, so
   `npm run build` on a clean checkout would fail with an unresolved import. It only builds here
   because the file exists in the working tree.

I did **not** fix this. The fix is a one-line `.gitignore` change (`publish/` → `/publish/`, or an
explicit `!web/src/features/publish/` negation) plus `git add -f` of the orphaned file, and
`.gitignore` is outside the two scope items I was given. It is also not caused by the slug slice and
would be equally broken on `main`. Recommending it be handled as its own small fix rather than
smuggled in here.

### Notes for review

- `git status --porcelain web/src` shows exactly four modified files:
  `web/src/lib/config.ts`, `web/src/features/detail/EngineerDetailPage.tsx`,
  `web/src/features/detail/TeamDetailPage.tsx`, `web/src/features/home/HomePage.tsx`. The fifth
  edited file is the ignored `PublishStatusPage.tsx` described above.
- Nothing under `api/`, `docs/`, `README.md`, or `.process/` from earlier slices was touched this
  round; the only `.process/` write is this appended section.
- No `web/` file appears in the original plan's *Existing code touched*. These four edits are
  authorised by the scoped rework task rather than by the plan, which is a deliberate, orchestrator-
  approved widening of that contract — noting it so the file-contract check does not read it as drift.
