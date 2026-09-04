# Plan — Domain consistency (`e3a.dev` → `e3a.ai`)

## Goal

A visitor can copy the install commands rendered by the site and they will actually work. Today the
frontend renders `/plugin marketplace add https://e3a.dev/marketplace.json` while the backend publishes
`marketplace.json`, zip URLs and `plugin.json` author URLs under `https://e3a.ai`, so anyone following
the UI installs from a host the pipeline never writes to. After this slice the single frontend literal
that decides the rendered host says `e3a.ai`, the publishing test fixtures no longer encode any real
hostname at all, and `docs/design-prompt.md` stops asserting the dead domain.

## Scope

**In:**
- `web/src/lib/config.ts` — correct the `siteUrl` fallback literal to `https://e3a.ai`.
- `web/.env.example` — `VITE_SITE_URL=https://e3a.ai`.
- `web/src/lib/config.test.ts` — add 3 tests pinning the rendered commands (existing 2 tests untouched).
- `api/E3A.Tests/Publishing/Shared/` — repoint the fixture const to a reserved fake host and make the
  five hardcoded assertions derive from it (5 files).
- `docs/design-prompt.md` — the 2 install-command examples.

**Out (verified, no change needed — do not touch):**

| Path | Why no change |
|---|---|
| `api/E3A.Api/appsettings.json` | `Publishing.PublicSiteUrl` is already `https://e3a.ai`. Also **gitignored** (`.gitignore:23`) — untracked, must not be committed. |
| `api/E3A.Jobs/appsettings.json` | Already `https://e3a.ai` (line 18); tracked. This is the only committed source of truth for the real domain and stays so. |
| `README.md` | Uses the placeholder `https://<domain>/marketplace.json` (line 8). No concrete domain. |
| `docs/architecture.md`, `docs/plugin-spec.md`, `docs/implementation-plan.md` | All use `<domain>` placeholders (`api.<domain>`, `https://<domain>/z/...`). Placeholders are not divergence. |
| `docs/constitution.md`, `docs/security-scan.md` | No domain reference. |
| `web/staticwebapp.config.json` | Contains only `navigationFallback` + `globalHeaders`. No domain. |
| `postman/e3a.postman_collection.json` | Only URL is the Postman schema URL. No e3a host. |
| `postman/e3a.local.postman_environment.json` | `baseUrl` is `https://localhost:62935`. Local API, not the public site. |
| `infra/`, `.github/workflows/*` | Zero matches for either domain. |
| `api/**` production C# | Zero hardcoded hostnames; every URL is composed from `PublishingOptions.PublicSiteUrl` (`PublicCatalogUrl`, `PublishBlobPaths.ZipUrl`, `MarketplaceDocumentGenerator`). |
| `https://e3a.blob.core.windows.net` / `https://e3a.queue.core.windows.net` | Azure storage account hostnames, unrelated to the public site domain. |
| `.process/**` | Frozen historical audit trail. Never rewritten. |
| `web/README.md` | Unmodified Vite scaffold text; no domain. |

**Deferred:**

| # | Item | Why deferred |
|---|---|---|
| D1 | `pinnedMarketplaceCommand` in `config.ts` emits a **scheme-less** URL (`e3a.ai/m/{plugin}/{version}/marketplace.json`) because it uses `siteHost = config.siteUrl.replace(/^https?:\/\//, '')`. `docs/implementation-plan.md:7` records that `/plugin marketplace add` requires an https URL, and `docs/design-prompt.md:18` shows the command *with* `https://`. | A missing scheme is a different defect class from a wrong host. Fixing it changes what the command string is, not which domain it points at, and needs its own slice + doc check. **This slice does not make the pinned-version command installable.** |
| D2 | `.github/workflows/web.yml` sets **no** `VITE_*` variables and runs `npm run build` directly, and Vite never reads `.env.example`. Every deployed value therefore comes from the `config.ts` fallbacks — including `apiBaseUrl`, which a CI build resolves to `https://localhost:62935/api`. | Wiring build-time env vars into the deploy (and deciding repo variables vs. secrets) is a CI slice, not a domain slice. It is also the precondition for D3. |
| D3 | Making `VITE_SITE_URL` build-time-required with no fallback (fail the build when unset). | Blocked on D2 — with today's workflow it would break the production deploy on the first push. See Decision 1. |

## Decisions

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Fix the `config.ts` literal, or make `VITE_SITE_URL` a required build-time env var with no fallback? | **Fix the literal.** `siteUrl: (import.meta.env.VITE_SITE_URL as string \| undefined) ?? 'https://e3a.ai'`. Leave the other three fallbacks structurally as they are. | The "silent default" framing is wrong for this repo: `.github/workflows/web.yml` passes no `VITE_*` at all and Vite does not read `.env.example`, so **the fallbacks *are* the production configuration**, not a dev convenience. Removing the fallback would fail the very next deploy. Adding CI env wiring first is a separate slice (D2/D3). Correcting the literal keeps exactly one place in the whole repo where the frontend's host is decided, which is the smallest change that closes the split. |
| 2 | Do the other three `config.ts` fallbacks change? | **No.** Only `siteUrl`. | `apiBaseUrl`, `githubOrgUrl` and `maxUploadMegabytes` are not wrong and are not part of the reported split. `conventions/react-feature.md` §2 is already satisfied — the value comes from Vite env via `lib/config.ts` with `.env.example` committed; the convention does not forbid a default. Touching them is scope creep and would collide with D2. |
| 3 | Does `.env.example` alone fix the bug? | **No — it is documentation only, and is updated for consistency, not for effect.** | Vite reads `.env`, `.env.local`, `.env.[mode]`; it never reads `.env.example`. The implementer must not treat the `.env.example` edit as the fix. |
| 4 | Publishing test fixtures: real domain or obviously-fake host? | **Fake, reserved host: `https://example.test`.** `PublishingOptionsFactory.PublicSiteUrl = "https://example.test"`, and **all five hardcoded assertions are rewritten to derive from that const** by interpolation. | `.test` is reserved by RFC 2606 and can never resolve, so it can never be mistaken for config. The generators under test only concatenate whatever host they are handed — pinning the production hostname in six assertions makes the test suite a second, silent source of truth for production config, which is exactly the split being removed, and guarantees another six-file edit the next time the domain moves. After this change, one const line is the only host in the test project. |
| 5 | What is lost by decision 4? | **The .NET suite no longer asserts the production domain anywhere, and must not claim to.** It asserts URL *composition* (`{siteUrl}/e/{slug}`, `{siteUrl}/z/{path}`, owner `url` = `siteUrl` verbatim). The real domain lives in `api/E3A.Jobs/appsettings.json` only, and configuration is not unit-testable in this pipeline. | Stated openly per `conventions/dotnet-testing.md` §9 — a test must not be presented as proof of a property it does not constrain. |
| 6 | `PublicCatalogUrlTests` passes literals directly and does not use the factory. Does it change? | **Yes.** Add `private const string SiteUrl = PublishingOptionsFactory.PublicSiteUrl;` to the class (same namespace, no new using) and interpolate. | Otherwise a stray `e3a.dev` literal survives in the tree and the slice fails its own completeness goal. The const keeps the three expression-bodied one-liners readable. |
| 7 | Which `/docs` files must change per `.claude/rules/docs-sync.md`? | **`docs/design-prompt.md` only** — lines 16 and 18. | It is the only doc asserting a concrete host, and the doc-ownership map routes "any UI page's content" there. The install block is UI content whose value is changing, so a stale `e3a.dev` there is divergence, not incompleteness. The other docs use `<domain>` placeholders and are unaffected. |
| 8 | Does `docs/design-prompt.md:18` keep its `https://` prefix even though the code emits a scheme-less string? | **Yes — change only the host, keep `https://`.** | The doc describes the target and the target is correct (Claude Code requires an https URL). Code lagging a doc is incompleteness, not divergence. This is tracked as D1. |
| 9 | Do the new frontend tests assert literal strings, given they read `import.meta.env` through Vitest? | **Yes, literals.** Assert the exact copy-paste strings. | Deriving from `config.siteUrl` (as `http.test.ts:63` does for the API base) would make the assertion vacuous for the one property this slice exists to fix. The tests read the committed fallback because `web/.env.local` does not set `VITE_SITE_URL` and `.env.example` — which is what a developer copies — will carry the same value. Accepted risk, stated here: a developer who sets `VITE_SITE_URL` to something else locally will see these three tests fail. |
| 10 | Should the fix export a `DEFAULT_SITE_URL` const to make the test env-independent? | **No.** | It is a refactor introduced solely to serve a test, it changes the module's public surface, and it does not remove the env sensitivity (`config.siteUrl` still resolves through `import.meta.env`). `config.ts` already mixes a named const with inline literals; mirror it, do not modernize it. |

## Existing code touched

| # | File | Change |
|---|---|---|
| 1 | `web/src/lib/config.ts` | Line 4: `?? 'https://e3a.dev'` → `?? 'https://e3a.ai'`. **Nothing else in the file changes** — not `apiBaseUrl`, not `githubOrgUrl`, not `siteHost`, not `pinnedMarketplaceCommand`. |
| 2 | `web/.env.example` | Line 2: `VITE_SITE_URL=https://e3a.dev` → `VITE_SITE_URL=https://e3a.ai`. Other three lines unchanged. |
| 3 | `web/src/lib/config.test.ts` | Add imports `config`, `marketplaceAddCommand`, `pinnedMarketplaceCommand` to the existing `./config` import; add two new `describe` blocks (see Test plan W1–W3). The existing `describe('installCommand')` and its two tests are unchanged. |
| 4 | `api/E3A.Tests/Publishing/Shared/PublishingOptionsFactory.cs` | Line 7: `public const string PublicSiteUrl = "https://e3a.dev";` → `"https://example.test";`. No other member changes. |
| 5 | `api/E3A.Tests/Publishing/Shared/MarketplaceDocumentGeneratorTests.cs` | Line 24 → `plugin.Source.Url.Should().Be($"{PublishingOptionsFactory.PublicSiteUrl}/z/e3a-dive-backend-engineer/1.0.0.zip");`  ·  Line 39 → `json.Should().Contain($"\"url\": \"{PublishingOptionsFactory.PublicSiteUrl}\"");` |
| 6 | `api/E3A.Tests/Publishing/Shared/PluginJsonGeneratorTests.cs` | Line 26 → `json.Should().Contain($"\"url\": \"{PublishingOptionsFactory.PublicSiteUrl}/e/dive-backend-engineer\"");` |
| 7 | `api/E3A.Tests/Publishing/Shared/PublishStatusResultGeneratorTests.cs` | Line 23 → `result.ZipUrl.Should().Be($"{PublishingOptionsFactory.PublicSiteUrl}/z/e3a-dive-backend-engineer/1.0.0.zip");` |
| 8 | `api/E3A.Tests/Publishing/Shared/PublicCatalogUrlTests.cs` | Add `private const string SiteUrl = PublishingOptionsFactory.PublicSiteUrl;` as the first class member; rewrite the three bodies (see below). No new `using`. |
| 9 | `docs/design-prompt.md` | Line 16: `https://e3a.dev/marketplace.json` → `https://e3a.ai/marketplace.json`  ·  Line 18: `https://e3a.dev/m/{plugin}/{version}/marketplace.json` → `https://e3a.ai/m/{plugin}/{version}/marketplace.json`. Keep both `https://` prefixes and every other word on those lines. |

`PublicCatalogUrlTests` bodies after the change:

```csharp
public void ForEngineer_ShouldBuildEngineerPageUrl_WhenCalled()
    => PublicCatalogUrl.ForEngineer(SiteUrl, "x").Should().Be($"{SiteUrl}/e/x");

public void ForTeam_ShouldBuildTeamPageUrl_WhenCalled()
    => PublicCatalogUrl.ForTeam(SiteUrl, "x").Should().Be($"{SiteUrl}/t/x");

public void ForTeam_ShouldNotDoubleSlash_WhenSiteUrlHasATrailingSlash()
    => PublicCatalogUrl.ForTeam($"{SiteUrl}/", "x").Should().Be($"{SiteUrl}/t/x");
```

## Files to create

**None.** This slice is a value correction across nine existing files. No new module, page, helper, fixture
or test file is authorised — creating one is a review finding.

## Error codes

**None.** No new failure mode exists: nothing throws, nothing is validated, no API response shape changes.
`ErrorCodes.cs`, `Messages.ar.resx` and `Messages.en.resx` are not touched, and
`web/src/lib/errorMessages.ts` is not touched because the backend error surface is unchanged.

## Domain behaviour

**None.** No entity, no state transition, no invariant, no `BusinessRuleViolationException`, no
`UpdationDate`. `api/E3A.Domain` is not touched. The only C# edited is in `api/E3A.Tests`.

## API surface

**None.** No endpoint added, changed or removed; no controller, command, query, handler, validator,
result, `DefaultCodes` constant, `AppDbContext` change or migration. `postman/` is unchanged (verified:
neither collection nor environment file references either domain), so the SKILL §9 Postman checklist item
is satisfied by "no endpoint changed".

## Test plan

Frontend — all rows go in the **existing** `web/src/lib/config.test.ts`, appended below the current
`describe('installCommand')` block, which stays exactly as it is.

| # | Test file · describe | Test name | Asserts |
|---|---|---|---|
| W1 | `web/src/lib/config.test.ts` · `describe('config')` | `it('should fall back to the production site url when VITE_SITE_URL is unset')` | `expect(config.siteUrl).toBe('https://e3a.ai')` |
| W2 | `web/src/lib/config.test.ts` · `describe('marketplaceAddCommand')` | `it('should emit the marketplace add command for the production domain')` | `expect(marketplaceAddCommand()).toBe('/plugin marketplace add https://e3a.ai/marketplace.json')` — the exact string `InstallBlock` renders and the user copies |
| W3 | `web/src/lib/config.test.ts` · `describe('pinnedMarketplaceCommand')` | `it('should pin a version on the production host')` | `expect(pinnedMarketplaceCommand('e3a-payments-engineer', '1.2.3')).toBe('/plugin marketplace add e3a.ai/m/e3a-payments-engineer/1.2.3/marketplace.json')` — note the **absent scheme is current behaviour** (D1); this row asserts the host only and must be updated by the slice that fixes D1 |

No other frontend test is added. `InstallBlock.tsx` and `VersionHistory.tsx` are **not** modified and are
not reachable by the runner (`environment: 'node'`, `include: ['src/**/*.test.ts']`, no jsdom) — do not add
a DOM dependency, and do not write a `.tsx` test.

Backend — **no test is added or removed.** The five files in `api/E3A.Tests/Publishing/Shared/` are edited
in place; the suite must still report **777 passed**. The edited assertions keep their existing names:

| # | Test class | Test method | Asserts after the change |
|---|---|---|---|
| B1 | `MarketplaceDocumentGeneratorTests` | `GeneratePlugin_ShouldBuildArchiveSource_WhenVersionIsPublished` | zip URL = `{PublicSiteUrl}/z/e3a-dive-backend-engineer/1.0.0.zip`; name, `source`, sha256, keywords unchanged |
| B2 | `MarketplaceDocumentGeneratorTests` | `Generate_ShouldWrapPluginsWithNameAndOwner_WhenCalled` | serialized owner `"url": "{PublicSiteUrl}"` (verbatim, no path); other four `Contain` assertions unchanged |
| B3 | `PluginJsonGeneratorTests` | `Generate_ShouldEmitPrefixedNameAndAuthor_WhenCalled` | author `"url": "{PublicSiteUrl}/e/dive-backend-engineer"`; name/version/author-name assertions unchanged |
| B4 | `PublishStatusResultGeneratorTests` | `Generate_ShouldBuildAbsoluteZipUrl_WhenVersionIsPublished` | `ZipUrl` = `{PublicSiteUrl}/z/e3a-dive-backend-engineer/1.0.0.zip`; status/updatedAt/itemId/itemType unchanged |
| B5 | `PublicCatalogUrlTests` | all three methods | engineer `/e/`, team `/t/`, trailing-slash normalisation — all composed from `SiteUrl` |

### Mutation checks (`conventions/dotnet-testing.md` §9 — required, both outcomes recorded in the report)

| # | Mutation | Expected |
|---|---|---|
| M1 | In `web/src/lib/config.ts`, revert the literal to `'https://e3a.dev'`; run `npm run test` | W1, W2 and W3 fail; the other 58 tests pass. Restore from a byte-exact copy and verify with `cmp`. |
| M2 | In `api/E3A.Application/Publishing/Shared/PublicCatalogUrl.cs`, change `EngineerSegment` from `"e"` to `"x"`; run `dotnet test` | `PublicCatalogUrlTests.ForEngineer_...` and `PluginJsonGeneratorTests.Generate_ShouldEmitPrefixedNameAndAuthor_WhenCalled` fail; restore and verify with `cmp`. |

M2 is what makes B3/B5 non-vacuous after the assertions start deriving from the const. The report must
also state plainly that **no test in either suite constrains the value `e3a.ai` on the backend** — that
value is asserted only by `api/E3A.Jobs/appsettings.json` and by W1/W2/W3 on the frontend.

## Definition of done

- [ ] `git grep -i e3a\.dev` over the tree excluding `node_modules`, `dist`, `bin`, `obj`, `.git` and `.process` returns **zero** matches.
- [ ] `web/src/lib/config.ts` line 4 fallback is `'https://e3a.ai'`; `apiBaseUrl`, `githubOrgUrl`, `maxUploadMegabytes`, `siteHost`, `marketplaceAddCommand`, `installCommand` and `pinnedMarketplaceCommand` are byte-identical to before.
- [ ] `web/.env.example` line 2 is `VITE_SITE_URL=https://e3a.ai`; its other three lines are unchanged.
- [ ] `api/E3A.Tests/Publishing/Shared/PublishingOptionsFactory.cs` `PublicSiteUrl` is `"https://example.test"`.
- [ ] No `https://example.test` or any other host literal appears in the five publishing test files outside that one const — every assertion interpolates `PublishingOptionsFactory.PublicSiteUrl` (or the `SiteUrl` alias in `PublicCatalogUrlTests`).
- [ ] `docs/design-prompt.md` lines 16 and 18 say `e3a.ai`, both retain `https://`, and nothing else in `/docs` changed.
- [ ] No file under `.process/` was modified.
- [ ] No file was created; no file was deleted.
- [ ] `api/E3A.Api/appsettings.json` was not committed (it is gitignored) and `api/E3A.Jobs/appsettings.json` was not modified.
- [ ] `postman/`, `infra/`, `.github/`, `README.md`, `web/staticwebapp.config.json` untouched.
- [ ] `npm run build` — zero TypeScript errors.
- [ ] `npm run test` — **61 passed / 10 files** (baseline 58; exactly W1, W2, W3 added).
- [ ] `npx oxlint` — **8 warnings, 0 errors** (measured baseline), no `oxlint-disable`, no `@ts-ignore`.
- [ ] `dotnet build` — no new warnings versus baseline.
- [ ] `dotnet test` — **777 passed, 0 failed**, unchanged total.
- [ ] M1 and M2 were performed, both observed outcomes recorded, and both files restored and verified byte-exact.
- [ ] The report states that the backend suite does not assert the production domain (Decision 5) and that D1 leaves the pinned-version command still un-installable.
