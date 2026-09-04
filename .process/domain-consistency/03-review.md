VERDICT: APPROVED

# Review — Domain consistency (e3a.dev to e3a.ai)

Nine files changed, none created, none deleted. Every plan item is present, every claim in
`02-implementation.md` that I could test independently held, and `Deviations: None.` is accurate.

## Blocking

None.

## Non-blocking

- `web/src/lib/config.test.ts:27` — `it('should pin a version on the production host')` locks a string
  the plan itself records as un-installable (D1: no scheme prefix). The name is honest about what it
  pins (the *host*), and the plan and report both flag it, so it is not a finding. But the next reader
  of this file has nothing in-tree telling them the expectation is a characterization of a known defect.
  When D1 is done, that row must change, not just be re-baselined.
- `.process/domain-consistency/01-plan.md:5` — the Goal reads "the install commands rendered by the site
  will actually work", which the slice knowingly does not deliver for the pinned-version command (D1).
  The deferral is explicit and disclosed in three places, so nothing is hidden; the Goal sentence is
  simply wider than the slice. Worth tightening in the next plan rather than reworking this one.

## Verified

Claims from `02-implementation.md`, independently re-run — not taken from the report:

- `git diff main -- web/src/lib/config.ts` is exactly one -/+ pair (`web/src/lib/config.ts:4`).
  `apiBaseUrl`, `githubOrgUrl`, `maxUploadMegabytes` (`config.ts:5-7`), `siteHost` (`config.ts:10`),
  `marketplaceAddCommand` (`config.ts:14-16`), `installCommand` (`config.ts:18-20`) and
  `pinnedMarketplaceCommand` (`config.ts:22-24`) are byte-identical to `main`. Decision 2 honoured;
  Decision 10 honoured (no `DEFAULT_SITE_URL` export introduced).
- `git grep -i 'e3a\.dev'` over the tree (excluding `node_modules`, `dist`, `bin`, `obj`, `.git`,
  `.process`) returns exit 1 — **zero matches**. A wider filesystem `grep -ril` with the same exclusions,
  which also covers gitignored files (`web/.env.local`, `api/E3A.Api/appsettings.json`), also returns
  zero.
- `git diff main --name-status` shows 10 `M`, zero `A`, zero `D`. Under `.process/` only
  `04-metrics.md` is modified, and its diff is orchestrator bookkeeping (base/feature branch line plus
  the stage-2 row). The implementer's attribution is correct. `02-implementation.md` is untracked — that
  is this stage's own deliverable, not a rogue file.
- `postman/`, `infra/`, `.github/`, `README.md`, `web/staticwebapp.config.json`,
  `api/E3A.Jobs/appsettings.json` and `.gitignore` all show an empty diff against `main`.
  `api/E3A.Api/appsettings.json` is confirmed gitignored (`.gitignore:23`) and not committed; both
  appsettings hold `"PublicSiteUrl": "https://e3a.ai"` (`api/E3A.Jobs/appsettings.json:18`,
  `api/E3A.Api/appsettings.json:26`), so the fixed frontend literal and the backend config now agree.
- Fixture decision (Decisions 4 and 6) held: `api/E3A.Tests/Publishing/Shared/PublishingOptionsFactory.cs:7`
  is `"https://example.test"`, and it is the **only** host literal in
  `api/E3A.Tests/Publishing/Shared/` apart from the four `https://e3a.blob.core.windows.net`
  `AzureOptions.StorageAccountUrl` values (`DraftSnapshotFreezerTests.cs:14`,
  `TeamPublishBuilderTests.cs:26`, `TeamPublishBuilderFailureTests.cs:25`,
  `TeamSnapshotReaderTests.cs:14`), which the plan scoped out as storage-account hosts. Every changed
  assertion interpolates the const (`MarketplaceDocumentGeneratorTests.cs:24,39`,
  `PluginJsonGeneratorTests.cs:26`, `PublishStatusResultGeneratorTests.cs:23`,
  `PublicCatalogUrlTests.cs:9,13,17,21`). The pre-existing
  `Publishing/GetPublishStatus/GetPublishStatusQueryHandlerTests.cs:47` already derived from the const
  and correctly needed no edit.
- The fix reaches the UI: `web/src/components/InstallBlock.tsx:8` calls `marketplaceAddCommand()` and
  `web/src/components/VersionHistory.tsx:28` calls `pinnedMarketplaceCommand(...)`; no component
  hardcodes a host (the only other `https://` in `web/src` is the GitHub profile link at
  `web/src/features/profile/ProfilePage.tsx:77`).
- Gates, all re-run by me on this tree: `npm run build` clean (67 modules transformed, zero TS errors) ·
  `npm run test` **61 passed / 10 files** · `npx oxlint` **8 warnings, 0 errors** (matches the claimed
  baseline; no `oxlint-disable` or `@ts-ignore` in the diff) · `dotnet build api/E3a.slnx` **9 warnings,
  0 errors**, all nine in `api/core-libraries`, i.e. no new warnings · `dotnet test` **777 passed,
  0 failed**. No MSB3027/MSB3021 lock errors.
- Docs sync (`.claude/rules/docs-sync.md`): `docs/design-prompt.md:16` and `docs/design-prompt.md:18` now
  read `e3a.ai` and both retain the `https://` prefix; the rest of both lines (including
  `/plugin install e3a-mmohsen@e3a`) is untouched. Decision 7's argument holds — I checked every other
  doc: `docs/architecture.md:12`, `docs/implementation-plan.md:17,24,55` and
  `docs/plugin-spec.md:105,111,115,124,134` plus `README.md:8` all use the `<domain>` placeholder, and
  `docs/constitution.md` and `docs/security-scan.md` carry no host at all. No divergence anywhere;
  nothing else in `/docs` needed to move. Decision 8 is right: keeping `https://` at
  `docs/design-prompt.md:18` while the code emits a scheme-less string is code lagging the doc —
  incompleteness (tracked as D1), not divergence, and per the rule I do not flag it.
- Postman (review order #7): no endpoint added, changed or removed — the only C# in the diff is under
  `api/E3A.Tests`, with no controller, command, query, handler, validator, result or migration touched.
  Every request in `postman/e3a.postman_collection.json` targets `{{baseUrl}}/api/...`, so no request is
  stale, missing or orphaned. Nothing to sync.
- Skill compliance: no production C# changed, so SKILL.md §8's DO/DON'T catalog has no surface here — I
  walked all seven entries against the diff and none applies, which matches the report rather than being
  taken from it. The edited test files keep file-scoped namespaces, `sealed` test classes, no comments,
  no `DateTime`, and the plan-mandated method names (`conventions/dotnet-testing.md` §2). `web/` was
  correctly judged against `conventions/react-feature.md`, not SKILL.md: named exports,
  `describe('<exportName>')`, `.ts` rather than `.tsx` so the `include: ['src/**/*.test.ts']` glob picks
  them up, and no jsdom or testing-library dependency added.

## Test quality

**`web/src/lib/config.test.ts` (W1-W3) — these bite.** I did not take M1 on trust and did not need to
edit anything to check it: running `VITE_SITE_URL=https://mutant.example npx vitest run
src/lib/config.test.ts` makes exactly W1, W2 and W3 fail (received
`/plugin marketplace add mutant.example/m/...`) while the two pre-existing `installCommand` tests pass.
That proves, through a channel independent of the implementer's M1, both halves of the claim: the three
assertions genuinely resolve through `config.siteUrl`, and they pin the exact rendered string — host and
composition — rather than restating a value handed to them. `web/.env.local` contains only
`VITE_API_BASE_URL`, so the committed fallback is what the runner reads; Decision 9's precondition is
real, as is its accepted risk that a developer overriding `VITE_SITE_URL` locally sees these three go
red. W2 and W3 are the ones that matter: they assert the literal a visitor copies, which is the exact
property this slice exists to fix.

**`PublicCatalogUrlTests`, `PluginJsonGeneratorTests`, `PublishStatusResultGeneratorTests`,
`MarketplaceDocumentGeneratorTests` — still bite, on composition.** After the change the host in each
expected string comes from the same const the generator is fed, so the hostname half of those assertions
is now tautological by construction. What survives is path composition — `/e/{slug}`, `/t/{slug}`,
`/z/{path}`, and owner `url` equal to the site url verbatim — and that is exactly what M2 exercises. The
claimed M2 outcome is consistent with the code I read: `EngineerSegment`
(`api/E3A.Application/Publishing/Shared/PublicCatalogUrl.cs:5`) feeds only `ForEngineer`
(`PublicCatalogUrl.cs:9-12`), whose only assertions live at `PublicCatalogUrlTests.cs:13` and
`PluginJsonGeneratorTests.cs:26` — exactly the two the report says failed. Nothing else could have
failed: `MarketplaceDocumentGeneratorTests.cs:39` asserts the owner url with a closing quote
(`"url": "https://example.test"`), which the author url `https://example.test/e/...` does not contain as
a substring, and `PublishedTeamCollectorTests.cs:40` and `TeamTreeAssemblerTests.cs:110` assert the
`/t/` segment. `ForTeam_ShouldNotDoubleSlash_WhenSiteUrlHasATrailingSlash`
(`PublicCatalogUrlTests.cs:21`) still constrains `TrimEnd('/')` independently of the host.

**On the trade in Decision 5 — I agree, and the report states it accurately.** The .NET suite no longer
asserts the production domain anywhere, and the report says so plainly rather than implying coverage it
does not have (`conventions/dotnet-testing.md` §9). That loses nothing real: the old literal was a
second source of truth, not a check on the first, and it had already silently diverged —
`api/E3A.Jobs/appsettings.json:18` said `e3a.ai` while six test assertions said `e3a.dev` and the suite
stayed green through it. A duplicate that cannot detect its own divergence is worse than no duplicate.
The domain now lives in configuration only, with W1-W3 pinning the frontend side, and that split is
stated rather than papered over.
