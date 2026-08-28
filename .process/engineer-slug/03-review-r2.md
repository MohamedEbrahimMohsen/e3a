VERDICT: APPROVED

# Review r2 — Creator-Typed Engineer Slug

Fresh reviewer, no involvement in round 1. Both rework items are correctly and completely
resolved, no regression was introduced, and the scope extension is justified and correctly
bounded. Zero blocking findings.

## Item 1 — README.md:9 (round-1 blocking finding) — RESOLVED

`README.md:9` now reads `/plugin install e3a-<slug>@e3a`. The diff is exactly
`README.md | 2 +-`: one line, nothing else in the file touched.

The placeholder form was the right choice over the concrete `e3a-mmohsen`. The paired line
`README.md:8` is `/plugin marketplace add https://<domain>/marketplace.json`, also a placeholder,
so the copy-paste block keeps one register. The concrete form survives where it is a worked
example (`docs/design-prompt.md:16`, `docs/plugin-spec.md:87`).

The contract now agrees at every tracked mention. A repo-wide `git grep` for "plugin install"
excluding `.process/` returns exactly three lines, all three the two-part form:

- `README.md:9` — `/plugin install e3a-<slug>@e3a`
- `docs/design-prompt.md:16` — `/plugin install e3a-mmohsen@e3a`
- `web/src/lib/config.ts:14` — the template literal returning `/plugin install e3a-` + slug + `@e3a`

## Item 2 — installCommand scope extension — JUSTIFIED AND CORRECTLY BOUNDED

Judged explicitly, as asked, against the accepted scope line "Out of scope: frontend"
(`00-acceptance.md:50`).

**Justified.** `web/src/lib/config.ts:13-15` is not code lagging behind an unbuilt target. It is
code built to the *superseded* specification. `docs/plugin-spec.md:11` says the plugin name is
`e3a-{slug}` and that GitHub login is no longer part of it; the old two-argument `installCommand`
returned `e3a-{author}-{name}`. Two different answers to one question, which is exactly the
divergence test in `.claude/rules/docs-sync.md`. I confirmed the "live pages" half of the rationale
independently rather than taking it from `04-metrics.md`: `web/src/features/home/HomePage.tsx:19`
calls `getCatalog(...)` from `lib/api`, and line 40 feeds the first real API slug into the install
block; `web/src/features/detail/EngineerDetailPage.tsx:66` does the same with `engineer.slug`. Both
rendered a structurally unresolvable command to visitors before this fix. Blocking a one-line README
for this defect class while shipping the function that renders the same wrong string to real users
would have been incoherent.

**Correctly bounded.** Five lines across five files: the signature plus four call sites. Nothing
else in `web/` changed. `git status --porcelain web/src` lists exactly `lib/config.ts`,
`features/detail/EngineerDetailPage.tsx`, `features/detail/TeamDetailPage.tsx`,
`features/home/HomePage.tsx`, with the gitignored `features/publish/PublishStatusPage.tsx` as the
fifth edit on disk. No new frontend behaviour, no API wiring, no component added.

**The incompleteness boundary was drawn in the right place.** The three surviving three-part
constructions are all hardcoded prototype strings on unbuilt surfaces:
`EngineerComposerPage.tsx:27,28` and `TeamComposerPage.tsx:38`, display literals in a composer that
submits nothing. Leaving them is correct under the never-flag-missing-implementation clause of
`.claude/rules/docs-sync.md`; touching them would have been the frontend rewrite the exclusion
actually protects against.

**The type system did the call-site verification.** This is an arity change (2 args to 1), so any
missed call site is a compile error, not a silently wrong string. `npm run build`
(= `tsc -b && vite build`) passes clean, which is a hard guarantee that all four call sites were
updated, including the gitignored one, which is type-checked and bundled even though git cannot
see it.

## The four specific checks

1. **No three-part `e3a-{author}-{name}` anywhere in tracked source.** Confirmed two ways. A
   `git grep -i -E` for an `e3a-` prefix followed by author/creator/login/githublogin, excluding
   `.process/`, returns no matches (exit 1). A second `git grep -i -E` for `<creator>`,
   `{creator}`, `githublogin`, `e3a-mohamed-dive` returns one hit, `docs/implementation-plan.md:33`,
   the `users` table column list. Not stale: `docs/plugin-spec.md:11` removes the login from the
   *plugin name* and explicitly keeps attribution in the `author` field, which requires that column
   to survive. A plain grep over `web/src` (which reaches the gitignored file) adds only the three
   composer literals named above.
2. **`pinnedMarketplaceCommand` genuinely needs no change.** Verified by reading the whole chain,
   not the claim. `config.ts:17-19` interpolates `pluginName` whole into
   `/m/{pluginName}/{version}/marketplace.json`; it never composes a name from parts. Its only
   consumer is `components/VersionHistory.tsx:28`, which takes `pluginName` as a prop, and the only
   place that prop is supplied is `TeamDetailPage.tsx:48` — the item name plus a `-team` suffix,
   two-part and author-free. Correct to leave alone.
3. **`TeamDetailPage` has no orphaned `author` binding.** There is no destructured `author` local in
   the file; `item.author` was read at exactly one line (the old line 21) and that read is gone. The
   remaining `.author` at `TeamDetailPage.tsx:33` is `member.author`, a different object
   (`TeamMemberInfo`). `CatalogItem.author` stays legitimately used at `DetailHeader.tsx:26,28`,
   `EngineerCard.tsx:27,36,39` and `ProfilePage.tsx:13,14`, so the type field still earns its place.
   `npm run lint` (oxlint) reports zero unused-binding warnings; all six warnings it emits are
   pre-existing and in unrelated files.
4. **`api/core-libraries/` untouched.** `git status --porcelain api/core-libraries/` and
   `git diff --stat -- api/core-libraries/` both return empty.

## Independently re-run

| Command | Result | vs baseline |
|---|---|---|
| `dotnet build api/E3a.slnx` | `Build succeeded. 9 Warning(s), 0 Error(s)` | matches |
| `dotnet test api/E3A.Tests/E3A.Tests.csproj` | `Failed: 0, Passed: 236, Skipped: 0, Total: 236, Duration: 516 ms` | matches |
| `npm run build` in `web/` | `tsc -b` clean, 52 modules transformed, built in 443ms | matches |
| `npm run lint` in `web/` (extra, not requested) | 6 warnings, all pre-existing, none in a changed file | — |

All 9 build warnings originate in `api/core-libraries/` and are the same pre-existing set:
`Core.Validation` CS8602 x2 (`RequiredValidationExtensions.cs:52,57`), `Core.OTP` CS8618 x2
(`OTP.cs:30`), `Core.Notifications` CS8618 x5 (`NotificationTemplate.cs:15` x3,
`Notification.cs:35` x2). Zero warnings from any `E3A.*` project.

## No-regression evidence

- **Neither rework round touched the API side.** Not inferred from the reports — file mtimes: every
  `api/` file sits at 13:29–13:30 (implementation), `README.md` at 13:48:58, the four tracked `web/`
  files at 13:53:12–13:53:23, all after `03-review.md` was written at 13:47:55.
- **File-count arithmetic closes exactly.** 13 untracked source files (5 under
  `api/E3A.Application/Engineers/`, 8 under `api/E3A.Tests/`), unchanged from round 1 — no 14th file
  appeared during rework. 28 modified tracked files = the 23 the implementation report lists, plus
  `README.md`, plus 4 `web/` files. Nothing unaccounted for.
- Spot-read of the highest-risk API files to put my own eyes on them:
  `EngineerSlugResolver.cs:11-26` (exists-check first, re-normalized prefix at
  `SlugMaxLength - SlugSuffixSize - 1`, trailing-separator trim inside the retry loop),
  `UpdateEngineerHandler.cs:37-47` (freeze guard resolves before `UpdateMetadata`, single
  `SaveChangesAsync` after all mutations), `CheckSlugAvailabilityQueryHandler.cs:19-33` (auth guard
  first, suggestion computed only on the taken path), `EngineerSlugGenerator.cs:9-11,39-47`. All
  read as round 1 described. I did not re-litigate the settled API review.
- The two gitignored files were verified on disk, not assumed. `.gitignore:20` is `publish/` and
  `.gitignore:23` is `/api/E3A.Api/appsettings.json`. `appsettings.json` carries
  `"SlugMinLength": 3` (line 38) and the full 15-entry `"ReservedSlugs"` array (line 42), matching
  `00-acceptance.md:59` exactly. `PublishStatusPage.tsx:76` is
  `installCommand('payments-engineer')` — the edit is real and single-argument.
- Postman spot-check held: the `slug-availability` request at
  `postman/e3a.postman_collection.json:76-87`, and `"slug"` as the first field of both the Create
  (line 33) and Update (line 124) bodies.

## Non-blocking

Nothing here gates. Recorded because the dev is away and these would otherwise be raised verbally.

- `web/src/features/detail/TeamDetailPage.tsx:21` vs `:48` — the same page now gives two answers for
  a team plugin name: the install block renders `e3a-full-stack-squad` while `VersionHistory`
  receives `full-stack-squad-team` as the plugin name for the pin URL. **Pre-existing**: before this
  change it was `e3a-{author}-{name}` vs `{name}-team`, equally inconsistent. Not this slice to
  settle either — `docs/plugin-spec.md:11` defines a naming rule for engineers only, and the "Team
  plugin layout" section (lines 68-79) defines no team name at all. Teams are unbuilt, so this is
  incompleteness, not divergence. It needs a decision when the teams slice lands, not now.
- `web/src/features/composer/EngineerComposerPage.tsx:27,28`,
  `web/src/features/composer/TeamComposerPage.tsx:38` — the last three-part `e3a-{login}-...`
  constructions in the tree. Correctly left as prototype incompleteness; noted so the OAuth/composer
  slice picks them up rather than rediscovering them.
- `api/E3A.Application/DependencyInjection.cs:14` — the fail-open config item from round 1, still
  open and still worth the attention of the dev on return. `EngineersOptions` binds with a plain
  `services.Configure<>`, no `ValidateOnStart`. In an environment provisioned without the two new
  keys, `SlugMinLength` defaults to `0` and `ReservedSlugs` to `[]`, so `"a"` passes the length rule
  and `"admin"` becomes claimable, producing plugin names like `e3a-api`. Sharper now than at round
  1, because the slug rules are the first `EngineersOptions` values whose absence changes which
  names can exist *permanently*, and `appsettings.json` is gitignored so the values do not travel
  with the repo. Still not blocking: the code is correct given correct config, the plan mandated the
  config location, and the exposure predates this slice.
- `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandler.cs:26,31` —
  the double `IsSlugExistsAsync` on the taken path from round 1. Unchanged, still faithful to the
  plan, still worth collapsing when the composer polls this endpoint per keystroke.
- `web/` has no test infrastructure at all (`package.json:6-11` has no `test` script; zero
  `*.test.*` files). The `installCommand` contract is guarded only by `tsc`. That guard is real for
  this particular change — an arity change cannot silently miss a call site — but it would not catch
  a wrong string template. Not a finding against this slice; the repo has never had web tests.
- `.process/engineer-slug/04-metrics.md:38,39` — the logged start times (13:50, 13:55) postdate the
  actual file mtimes (13:48:58, 13:53:12). Audit-log drift of one to two minutes, no bearing on the
  code. Mentioned only because `.process/` is meant to be the durable record.

The `.gitignore:20` `publish/` defect is not restated as a finding here, per the task: it is
pre-existing, equally broken on `main`, and already logged as separate work. It is real, and it does
mean a fresh clone cannot build `web/` (`web/src/App.tsx` imports a module git does not carry), so
it is worth doing early — but it is not the debt of this slice.

## Test quality

No tests were added or removed in either rework round; the 236 count is unchanged from round 1, and
round 1 assessed the suite in depth. I re-checked only the question the rework could have changed:
could either edit have broken a test in a way the suite would not catch? No. The README edit is
markdown, and the `web/` edit lives in a project with no test suite, where `tsc -b` is the only
enforcement and is sufficient for an arity change. The API suite still passes 236/236 unmodified,
which is itself evidence that the rework never reached into `api/`.

The round-1 conclusion that no test class is vacuous is accepted; I found no cause to reopen it.

## Verified claims from the reports

Each independently confirmed, not read off `02-implementation.md`:

- "Diff is exactly one line, one file" for the README round — confirmed via `git diff -- README.md`.
- "Five lines across five files" for the `installCommand` round — confirmed; four in the diff, the
  fifth (`PublishStatusPage.tsx:76`) read directly on disk.
- "`item.author` was read only at that one line in `TeamDetailPage`" — confirmed by grep across
  `web/src`.
- "`pinnedMarketplaceCommand` ... no change needed" — confirmed by reading the full prop chain.
- "Nothing under `api/`, `docs/`, `README.md` ... was touched this round" — confirmed by mtimes.
- "Build succeeded, 9 warnings" / "236/236" / "web build clean" — all three re-run here with
  identical results.
- The `04-metrics.md` claim that two call sites are live pages on the real API — confirmed by
  reading `HomePage.tsx:19,40` and `EngineerDetailPage.tsx:66`.

Ship it.
