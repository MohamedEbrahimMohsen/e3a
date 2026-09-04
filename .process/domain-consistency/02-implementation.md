# Implementation — Domain consistency (`e3a.dev` → `e3a.ai`)

## Files created

**None.** The plan authorises no new file, and none was created. No file was deleted.

| Path | Lines | Purpose |
|---|---|---|
| — | — | — |

## Files modified

| Path | Change |
|---|---|
| `web/src/lib/config.ts` | Line 4 only: `?? 'https://e3a.dev'` → `?? 'https://e3a.ai'`. `apiBaseUrl`, `githubOrgUrl`, `maxUploadMegabytes`, `siteHost`, `marketplaceAddCommand`, `installCommand` and `pinnedMarketplaceCommand` are byte-identical (diff is a single `-`/`+` line pair). |
| `web/.env.example` | Line 2: `VITE_SITE_URL=https://e3a.ai`. Other three lines and the header comment unchanged. |
| `web/src/lib/config.test.ts` | Import widened to `{ config, installCommand, marketplaceAddCommand, pinnedMarketplaceCommand }`; three `describe` blocks appended (W1–W3). The existing `describe('installCommand')` block and its two tests are untouched. |
| `api/E3A.Tests/Publishing/Shared/PublishingOptionsFactory.cs` | Line 7: `PublicSiteUrl = "https://example.test"`. No other member changed. |
| `api/E3A.Tests/Publishing/Shared/MarketplaceDocumentGeneratorTests.cs` | Two assertions now interpolate `PublishingOptionsFactory.PublicSiteUrl` (zip URL, owner `url`). |
| `api/E3A.Tests/Publishing/Shared/PluginJsonGeneratorTests.cs` | Author `url` assertion interpolates `PublishingOptionsFactory.PublicSiteUrl`. |
| `api/E3A.Tests/Publishing/Shared/PublishStatusResultGeneratorTests.cs` | `ZipUrl` assertion interpolates `PublishingOptionsFactory.PublicSiteUrl`. |
| `api/E3A.Tests/Publishing/Shared/PublicCatalogUrlTests.cs` | Added `private const string SiteUrl = PublishingOptionsFactory.PublicSiteUrl;` as first class member; three bodies rewritten verbatim as the plan specifies. No new `using`. |
| `docs/design-prompt.md` | Lines 16 and 18 only: host `e3a.dev` → `e3a.ai`; both keep the `https://` prefix and every other word. |

Untouched as required, verified by `git status`: `api/E3A.Api/appsettings.json` (gitignored, not staged, not present as untracked), `api/E3A.Jobs/appsettings.json`, `postman/`, `infra/`, `.github/`, `README.md`, `web/staticwebapp.config.json`, all other `/docs` files.

## Deviations

`None.`

Two facts worth stating explicitly rather than as deviations:

1. `.process/domain-consistency/04-metrics.md` shows as ` M` in `git status`. That modification **pre-existed my first tool call** — it is the orchestrator's own bookkeeping (`**Base branch:** main · **Feature branch:** feature/domain-consistency` inserted above the table). I did not touch it, and no other file under `.process/` is modified.
2. `.env.local` exists in `web/` (gitignored) but contains **zero** occurrences of `VITE_SITE_URL`, so the vitest run reads the committed fallback. This is the precondition Decision 9 assumes, and it is now independently confirmed by M1: W1 flipped from pass to fail when the fallback literal changed, which is only possible if the fallback is what resolves.

## Build & test

All commands run from a clean tree; no API or Functions host was started, and no MSB3027/MSB3021 lock error occurred.

**Baselines measured before any edit:**

| Gate | Baseline |
|---|---|
| `npm run test` | `Test Files 10 passed (10)` · `Tests 58 passed (58)` |
| `npx oxlint` | 8 warning lines, 0 error lines |
| `dotnet build api/E3a.slnx` | `Build succeeded.` · `9 Warning(s)` · `0 Error(s)` — all 9 in `api/core-libraries` (Core.Validation CS8602 ×2, Core.OTP CS8618 ×2, Core.Notifications CS8618 ×5) |
| `dotnet test api/E3A.Tests/E3A.Tests.csproj` | `Passed! - Failed: 0, Passed: 777, Skipped: 0, Total: 777` |

**After the change:**

```
> npm run build
  tsc -b && vite build
  ✓ 67 modules transformed.
  dist/assets/index-KNzsuVkz.css    4.69 kB │ gzip:  1.41 kB
  dist/assets/index-D6U6px6O.js   316.04 kB │ gzip: 92.71 kB
  ✓ built in 209ms
```
Zero TypeScript errors.

```
> npm run test
 Test Files  10 passed (10)
      Tests  61 passed (61)
```
61 / 10 files — exactly the expected 58 + W1 + W2 + W3.

```
> npx oxlint
8 warning lines, 0 error lines
```
Unchanged against the measured baseline of 8 warnings / 0 errors. No `oxlint-disable`, no `@ts-ignore` introduced.

```
> dotnet build E3a.slnx
Build succeeded.
    9 Warning(s)
    0 Error(s)
```
Identical to baseline — no new warnings (`TreatWarningsAsErrors` still satisfied).

```
> dotnet test E3A.Tests/E3A.Tests.csproj
Passed!  - Failed:     0, Passed:   777, Skipped:     0, Total:   777, Duration: 936 ms - E3A.Tests.dll (net10.0)
```
Total unchanged at 777; no backend test added or removed.

### M1 — frontend mutation (`config.ts` literal reverted to `'https://e3a.dev'`)

Backed up first, byte-exact: `cmp` clean, `md5sum 90bdc8e11a09c2167017e70afe8a86e9`.

Observed outcome — **as expected**:

```
 FAIL  src/lib/config.test.ts > config > should fall back to the production site url when VITE_SITE_URL is unset
 FAIL  src/lib/config.test.ts > marketplaceAddCommand > should emit the marketplace add command for the production domain
 FAIL  src/lib/config.test.ts > pinnedMarketplaceCommand > should pin a version on the production host
 Test Files  1 failed | 9 passed (10)
      Tests  3 failed | 58 passed (61)
```

Exactly W1, W2, W3 failed; the other 58 passed. Sample assertion diff:
`Expected "/plugin marketplace add https://e3a.ai/marketplace.json"` / `Received "/plugin marketplace add https://e3a.dev/marketplace.json"`.

Restored by `cp` from the backup (not re-edited from memory). `cmp` clean; `md5sum` back to `90bdc8e11a09c2167017e70afe8a86e9`. Re-run: `Test Files 10 passed (10)` · `Tests 61 passed (61)`.

### M2 — backend mutation (`PublicCatalogUrl.EngineerSegment` `"e"` → `"x"`)

Backed up first, byte-exact: `cmp` clean, `md5sum 58111e2ab8d1c02909e8673b1ee741b0`.

Observed outcome — **as expected**:

```
  Failed E3A.Tests.Publishing.Shared.PluginJsonGeneratorTests.Generate_ShouldEmitPrefixedNameAndAuthor_WhenCalled [128 ms]
  Failed E3A.Tests.Publishing.Shared.PublicCatalogUrlTests.ForEngineer_ShouldBuildEngineerPageUrl_WhenCalled [3 ms]
Failed!  - Failed:     2, Passed:   775, Skipped:     0, Total:   777
```

Exactly the two predicted tests failed and nothing else. B3 and B5 therefore still bite after their assertions started deriving from the const — the const removed the *hostname* from the assertion, not the *path composition* the test exists to constrain.

Restored by `cp` from the backup. `cmp` clean; `md5sum` back to `58111e2ab8d1c02909e8673b1ee741b0`. Re-run: `Passed! - Failed: 0, Passed: 777, Skipped: 0, Total: 777`.

### Definition-of-done grep

```
> git grep -i -n 'e3a\.dev' -- . ':(exclude).process' ':(exclude)node_modules' ':(exclude)dist' ':(exclude)bin' ':(exclude)obj'
grep exit: 1  (1 = zero matches)
```

**Zero matches.** A second, wider filesystem `grep -ril` over the same exclusions (which also covers gitignored files such as `web/.env.local` and `api/E3A.Api/appsettings.json`) returned zero content matches; its non-zero exit code came only from five `api/.vs/**/FileContentIndex/*.vsidx` files locked by a running Visual Studio instance — binary editor index files, not source.

## Notes for review

**Required plain statements from the plan:**

1. **The backend suite no longer asserts the production domain anywhere** (Decision 5). After this slice the .NET tests constrain URL *composition* only — `{siteUrl}/e/{slug}`, `{siteUrl}/t/{slug}`, `{siteUrl}/z/{path}`, and owner `url` = `siteUrl` verbatim — against the reserved fake host `https://example.test`. **No test in either suite constrains the value `e3a.ai` on the backend.** That value is asserted only by `api/E3A.Jobs/appsettings.json` (configuration, not unit-testable in this pipeline) and, on the frontend, by W1/W2/W3.
2. **Deferred item D1 leaves the pinned-version command still un-installable.** `pinnedMarketplaceCommand` emits `e3a.ai/m/{plugin}/{version}/marketplace.json` — a **scheme-less** URL, because it derives from `siteHost = config.siteUrl.replace(/^https?:\/\//, '')`. Claude Code's `/plugin marketplace add` requires an `https://` URL, so this slice fixes *which host* the pinned command names but does not make it work. W3 deliberately asserts the scheme-less string as current behaviour and must be updated by the slice that fixes D1. `docs/design-prompt.md:18` intentionally keeps `https://` because it describes the target (Decision 8) — that is code lagging a doc, i.e. incompleteness, not divergence.

**Things I want a second pair of eyes on:**

- **Remaining `https://` literals in `api/E3A.Tests/Publishing/Shared/`.** Four sibling files (`DraftSnapshotFreezerTests`, `TeamPublishBuilderTests`, `TeamPublishBuilderFailureTests`, `TeamSnapshotReaderTests`) still contain `StorageAccountUrl = "https://e3a.blob.core.windows.net"`. These are Azure **storage account** hostnames, explicitly listed in the plan's "Out" table as unrelated to the public site domain, and those four files are not in *Existing code touched* — so I left them alone. Within the five in-scope files, `https://example.test` in `PublishingOptionsFactory.cs:7` is the only host literal, as the DoD requires.
- **`docs/design-prompt.md` line 16 also contains `/plugin install e3a-mmohsen@e3a`.** I changed only the host in the first backticked command and left that second command and every other word on the line intact, per the plan.
- **Frontend test placement.** All three new tests are `.ts` (not `.tsx`) and live in the existing `web/src/lib/config.test.ts`, so they are inside the runner's `include: ['src/**/*.test.ts']` glob with `environment: 'node'`. No jsdom, no testing-library, no DOM dependency added. `InstallBlock.tsx` and `VersionHistory.tsx` were not modified and remain unreachable by the runner; the rendered-command change reaches them only through `config.siteUrl`, which W2/W3 pin at the function level.
- **Env-sensitivity of W1–W3, accepted per Decision 9.** A developer who sets `VITE_SITE_URL` to something else in `web/.env.local` will see these three tests fail. I verified `.env.local` currently has no `VITE_SITE_URL` key.
- **`.process/domain-consistency/04-metrics.md` is dirty but not by me** — see Deviations note 1. If the reviewer's checklist treats any `.process/` modification as blocking, that entry needs attributing to the orchestrator, not this stage.
- **SKILL.md applicability.** `web/` changes were written against `conventions/react-feature.md` (named exports, `describe('<exportName>')` + `it('should … when …')`, no magic values outside `lib/config.ts`); `api/` changes against `SKILL.md` + `conventions/dotnet-testing.md`. No .NET idiom crossed into `web/`. No production C# was changed at all, so the §8 DO/DON'T catalog had no new surface to apply to — and nothing in the plan prescribed a §8 DON'T, so no deviation-toward-the-catalog was needed.
