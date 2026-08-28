# CodeRabbit Rework — Public Catalog (Stage 4)

All seven IMPLEMENT items from `06-coderabbit-triage.md` are done. Every REJECT stayed rejected — no code was written for RC2, RC3, RC5, RC6, RC7, RC8, RC11, RC14, RC15 or PC1.

## Item-by-item

| # | Triage item | What I changed | File:line |
|---|-------------|----------------|-----------|
| 1 | RC4 (Major) — deterministic final sort key | Appended `.ThenBy(x => x.Id)` to both switch arms; split the chains one-operator-per-line per skill §1 (the `_` arm would otherwise carry three operators on one line) | `api/E3A.Application/Catalog/GetCatalog/GetCatalogQueryHandler.cs:29-37` |
| 2 | Reviewer-found — web sends `pageNumber`, API binds `page` | `parameters.set('pageNumber', …)` → `parameters.set('page', …)`. API wire contract untouched | `web/src/lib/api.ts:78` |
| 3 | RC10 (Major) — Retry hangs on "Loading…" | Added `const [reloadToken, setReloadToken] = useState(0)`, added `reloadToken` to the fetch-effect deps, and the Retry handler is now `setLoadFailed(false); setReloadToken(token => token + 1);` — the dead `setQuery(query => query)` and the `setPage(1)` reset are gone, so Retry re-fetches the page the user was actually on | `web/src/features/catalog/CatalogPage.tsx:24` (state), `:44` (deps), `:97` (handler) |
| 4 | RC9 (Major) — CatalogPage keyboard accessibility | Segment tabs, tag chips, sort toggle, Prev/Next and every page number are `<button type="button">` with handlers and styles preserved. `aria-pressed` on segment tabs and tag chips; `aria-current="page"` on the active page number | `CatalogPage.tsx:73` (segments), `:80` (tags), `:84` (sort), `:106-110` (pagination) |
| 5 | RC12 (Major) — EngineerDetailPage keyboard accessibility | Report span → `<button type="button">`; hook-warning toggle → `<button type="button" aria-expanded={hooksOpen}>` on the header row (see Implementation note below) | `EngineerDetailPage.tsx:64` (Report), `:68-73` (hook toggle) |
| 6 | RC13 (Minor) — HomePage fabricated zeros | Added `loadFailed` state, set in both `.catch` handlers (the catalog catch no longer writes `setEngineers([])`, the tags catch no longer writes `setTagCount(0)`). When set, the stats row is replaced by "Catalog stats unavailable — the API is unreachable."; the featured grid simply stays empty | `HomePage.tsx:16` (state), `:20-21` (catches), `:42-52` (render) |
| 7 | RC1 (Minor) — machine-specific path in launch.json | Dropped `"--prefix", "D:\\Personal\\_e3a\\web"` from `runtimeArgs`, added `"cwd": "web"`. `run dev -- --port 5174 --strictPort` preserved | `.claude/launch.json:7-8` |

## Files modified

| Path | Item(s) |
|------|---------|
| `D:\Personal\_e3a\api\E3A.Application\Catalog\GetCatalog\GetCatalogQueryHandler.cs` | 1 |
| `D:\Personal\_e3a\web\src\lib\api.ts` | 2 |
| `D:\Personal\_e3a\web\src\features\catalog\CatalogPage.tsx` | 3, 4 |
| `D:\Personal\_e3a\web\src\features\detail\EngineerDetailPage.tsx` | 5 |
| `D:\Personal\_e3a\web\src\features\home\HomePage.tsx` | 6 |
| `D:\Personal\_e3a\.claude\launch.json` | 7 |

No files created, none deleted. No new tests (triage item 1 explicitly states none is required — `Engineer.Create` assigns `Guid.NewGuid()` internally, so the `Id` tie-break is not deterministically assertable through `EngineerFactory`). No endpoint or contract changed, so `postman/e3a.postman_collection.json` needed no edit. Nothing committed.

## Implementation notes on how items 4/5 were carried out

Both are faithful to the finding; two details are worth the reviewer's eye:

1. **Hook-warning toggle structure (item 5).** The triage pointed at `EngineerDetailPage.tsx:68`, the outer `div` that carries both the click handler *and* the expanded hook list. Turning that exact element into a `<button>` would nest block-level `<div>`s (the disclosure panel) inside a button — invalid HTML content model, and it would fold the panel text into the button's accessible name. I instead left the outer element as a non-interactive container (it keeps background/border/radius/padding, and lost `onClick` + `cursor: 'pointer'`) and made the header row the `<button type="button" aria-expanded={hooksOpen}>`. The panel now sits as a sibling after the button. Same keyboard fix the finding asked for, valid markup, `aria-expanded` on the control that actually toggles.

2. **UA style neutralisation.** `<button>` brings default background/border/padding/font-size that `<span>` does not, and `index.css:42` only resets `font-family`. To keep the pixels identical I added the minimum offsets where the original element had no explicit value: `border: 'none'` on segment tabs; `background: 'transparent'`, `padding: 0`, `fontSize: 13` on `pageButtonStyle`; a new `pageStepStyle` const for Prev/Next (`background: 'transparent'`, `border: 'none'`, plus their original padding/font-size); `background/border/padding` resets on the Report button and the hook toggle (the latter also `width: '100%'` and `textAlign: 'left'` so the row still spans the panel). Tag chips and the sort toggle already set background and border explicitly, so they needed nothing. I have **not** visually confirmed this in a browser — see Notes.

## Build & test

API — run from `D:\Personal\_e3a\api`:

```
dotnet build E3A.slnx
```
> `34 Error(s)` — **all** `MSB3021`/`MSB3027` file-copy locks in `E3A.Api` only: *"The file is locked by: E3A.Api (40768)"*. The API host is currently running from `E3A.Api/bin/Debug/net10.0`, so MSBuild cannot overwrite the copied `Core.*`/`E3A.*` DLLs. These are post-compile copy failures, not compilation errors, and none of them reference a source file. I did not kill the dev's running process.

```
dotnet build E3A.Tests/E3A.Tests.csproj
```
> `Build succeeded.` — `9 Warning(s)`, `0 Error(s)`. This compiles `E3A.Domain`, `E3A.Application` (where item 1 lives) and `E3A.Infrastructure`. All 9 warnings are the pre-existing `core-libraries` set (`Core.Validation` CS8602 ×2, `Core.OTP` CS8618 ×2, `Core.Notifications` CS8618 ×5) — identical to the list in `02-implementation.md`. Zero warnings from any `E3A.*` project; no new warning introduced.

```
dotnet test E3A.Tests/E3A.Tests.csproj --no-build
```
> `Passed!  - Failed:     0, Passed:   166, Skipped:     0, Total:   166, Duration: 1 s - E3A.Tests.dll (net10.0)`

166 green, unchanged from the pre-rework baseline — the paging and sort tests use distinct dates/install counts, so the `Id` tie-break does not perturb them.

Web — run from `D:\Personal\_e3a\web`:

```
npm run build
```
> ```
> > tsc -b && vite build
> vite v8.2.2 building client environment for production...
> ✓ 52 modules transformed.
> dist/index.html                   0.93 kB │ gzip:  0.50 kB
> dist/assets/index-KNzsuVkz.css    4.69 kB │ gzip:  1.41 kB
> dist/assets/index-DOu2dHns.js   309.35 kB │ gzip: 89.92 kB
> ✓ built in 877ms
> ```

TS strict passes (`tsc -b` is part of the build script and produced no diagnostics).

## Notes for review

1. **`E3A.Api` was never recompiled clean** because its output directory is locked by the running API process. Nothing I changed lives in `E3A.Api`, and the solution build reported no `CS####` diagnostics — but a reviewer wanting a fully green `dotnet build E3A.slnx` will need to stop PID 40768 first.
2. **No visual verification.** The style-neutralisation in item 4/5 is reasoned from the UA defaults and `index.css`, not observed in a browser — I have no browser tooling in this session. The chips/tabs/sort toggle are the low-risk ones (they set background and border themselves); the ones worth an eyeball are the **pagination row** (Prev / numbers / Next) and the **hook-warning header**, where I supplied the resets.
3. **`type="button"` is inconsistent with the repo.** No existing `<button>` in `web/` carries `type="button"` (checked all 17). The triage prescribed it explicitly for items 4 and 5, so the new buttons have it and the pre-existing neighbours in the same files (`Retry` now has it since I touched that line; `Clear filters` at `CatalogPage.tsx:119` does not) do not. I did not sweep the untouched ones — out of scope. Flagging in case the dev wants consistency either way.
4. **Item 3 changed Retry's page behaviour.** The old handler called `setPage(1)`; the new one does not, so Retry now re-fetches whatever page failed rather than bouncing to page 1. This follows the triage wording ("increment it … alongside clearing `loadFailed`") and is the more correct behaviour, but it is a behaviour change beyond the literal hang bug.
5. **Item 6 removed the `setEngineers([])` reset** from the catalog catch. On a failure after a prior success the featured grid now keeps the last-known engineers instead of clearing — but the stats row is replaced by the unavailable note, so nothing is presented as a fresh fact. If the dev prefers a hard clear, that is a one-line addition.
6. **Item 7's `"cwd": "web"`** is relative to the repo root. That matches how the launcher resolves other relative paths in this repo as far as I can tell, but I could not execute the launch profile to confirm; if the launcher resolves `cwd` relative to `.claude/` instead, it needs `"../web"`.
7. **No `/docs` change.** Per `.claude/rules/docs-sync.md`, none of the seven items alters what the product does or how it is designed — item 2 fixes the web client to match the already-documented `page` contract; the rest are ordering determinism, accessibility, an error state and a tooling path. RC7 and RC8 (the two docs comments) were rejected by the triage and stay rejected.
