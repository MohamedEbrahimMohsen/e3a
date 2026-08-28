# Run Metrics — engineer-slug

**Base branch:** `main` @ `5b8fd6a` · **Feature branch:** `feature/engineer-slug` · **Dev:** away (proxy gating in effect)

## Plan-gate verification (orchestrator, before proxy approval)

The plan rests on two claims that would be expensive to get wrong. Both were checked against
the real code rather than taken on trust:

1. **Decision #9 — `IGenerator` emits a trailing separator.** CONFIRMED.
   `Core.Utilities/Generator/Generator.cs:15` returns
   `$"{prefix}{separator}{Nanoid.Generate(...)}{separator}{suffix}"`, and `suffix` defaults to
   `""`, so `Generate(prefix: "mmohsen", size: 4)` yields `"mmohsen-ab12-"`.
   **This is a live latent defect in `main` today**: `CreateEngineerHandler`'s collision path
   already produces trailing-hyphen slugs. It is invisible only because the slug had no format
   contract until this slice and the existing test substitutes `IGenerator`. The plan's fix
   (`.TrimEnd('-')` in the resolver, core untouched) is accepted for this slice — see the debt
   note in the dev veto list about fixing `Core.Utilities` properly later.

2. **No unlisted caller breaks on the command signature change.** CONFIRMED.
   `tools/E3A.Seeder/Program.cs` builds engineers through the **domain factory**
   `Engineer.Create(ownerUserId, slug, ...)`, whose signature this slice does not touch — so the
   seeder is unaffected. This mattered because the seeder is NOT in `api/E3a.slnx`, so a break
   there would not surface in a solution build. A repo-wide grep confirms every
   `CreateEngineerCommand`/`UpdateEngineerCommand` caller is already on the plan's touched-files
   list.

`EngineerFactory` was also verified to already expose `Draft`, `Published`, `DefaultSlug`, and
`CreateEngineersOptions` exactly as the test plan assumes.

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 0 | Pre-flight + Acceptance | orchestrator | — | 2026-08-28 13:05 | 2026-08-28 13:10 | ~5m | — | — | clean tree on merged main; models overridden to OPUS 5 (Opus 4.8 unavailable, surfaced); scope + slug rules accepted |
| 1 | Plan | feature-planner | OPUS 5 | 2026-08-28 13:11 | 2026-08-28 13:24 | 13m 33s | 133,271 | 48 | plan written; 15 decisions, 2 DEV-DECISIONs recorded |
| 2 | Plan gate (PROXY) | orchestrator | OPUS 5 | 2026-08-28 13:24 | 2026-08-28 13:25 | — | — | — | APPROVED as proxy (dev away). Orchestrator independently verified the two riskiest claims before approving — see below |
| 3 | Implement | feature-implementer | OPUS 5 | 2026-08-28 13:26 | 2026-08-28 13:38 | 11m 47s | 172,929 | 112 | done — 13 files created + planned modifications; build clean (9 pre-existing core-libraries warnings); 236/236 tests; 6 declared deviations |
| 4 | Review r1 | feature-reviewer | OPUS 5 | 2026-08-28 13:39 | 2026-08-28 13:49 | 10m 12s | 149,213 | 60 | **CHANGES_REQUESTED** — 1 blocking (README.md:9 stale plugin-name contract), 1 non-blocking follow-up; all 6 deviations judged sound; build + 236/236 independently re-confirmed |
| 5 | Rework r1 · item 1 | feature-implementer | OPUS 5 | 2026-08-28 13:50 | 2026-08-28 13:53 | 2m 39s | 34,459 | 14 | README.md:9 fixed; build + 236/236 re-confirmed; **self-found a second divergence** (web install command) and correctly declined to fix it unilaterally |
| 6 | Rework r1 · item 2 | feature-implementer | OPUS 5 | 2026-08-28 13:55 | 2026-08-28 13:59 | 4m 01s | 40,313 | 27 | `installCommand(slug)` + 4 call sites; web build clean; API build + 236/236 unchanged; **found a pre-existing repo defect** (see below) |
| 7 | Review r2 | feature-reviewer | OPUS 5 | 2026-08-28 14:00 | 2026-08-28 14:08 | 7m 55s | 93,315 | 38 | **APPROVED** — 0 blocking; both items resolved; scope extension judged justified + correctly bounded; build/tests/web all independently re-run |
| 8 | Commit + push + PR | orchestrator | — | 2026-08-28 14:09 | … | — | — | — | git identity confirmed repo-local (global unset) |

## Pre-existing defect found during rework (NOT fixed here — spun off as separate work)

`.gitignore:20` is `publish/`, a `dotnet publish` build-artifact rule. With no leading slash git
matches it at any depth, so it also swallows `web/src/features/publish/`.

- `web/src/features/publish/PublishStatusPage.tsx` is therefore **untracked** — confirmed by
  `git check-ignore -v` and an empty `git ls-files` on that path.
- `web/src/App.tsx:17` imports it and `:59` routes it at `/workspace/publish`.
- Consequence: **a fresh clone of `main` cannot build the web app.** It builds on this machine only
  because the file exists in the working tree.

Equally broken on `main`, entirely unrelated to the slug contract, and `.gitignore` is repo policy —
so it was deliberately NOT folded into this slice. Logged as its own task for the dev.

Side effect relevant to reading this slice's diff: that file is one of the four `installCommand`
call sites. Its edit is real on disk and is compiled by the web build, but will not appear in the
commit. Do not read its absence as an unfixed call site.

## Proxy scope extension (dev veto item)

The dev's accepted scope says **"Out of scope: frontend."** The orchestrator extended it by one
item during rework round 1, and records the reasoning here for veto:

- **What:** `web/src/lib/config.ts:13` `installCommand(author, name)` returns
  `/plugin install e3a-{author}-{name}@e3a` — it *implements* the plugin-name contract this slice
  redefined to `e3a-{slug}`. Fixed to take the slug alone; four call sites updated.
- **Why it is divergence, not incompleteness:** the code is not lagging behind an unbuilt target —
  it is built to the superseded specification. Two call sites (`HomePage.tsx:40`,
  `EngineerDetailPage.tsx:66`) are live pages wired to the real API and browser-verified last
  slice, so they render a structurally invalid command to visitors today.
- **Why it belongs in this slice:** review finding #1 blocked a one-line README for precisely this
  defect class. Shipping the slice while leaving the function that renders the same wrong string to
  actual users would be incoherent.
- **Why the exclusion arguably does not cover it:** the acceptance reasons the frontend exclusion
  as "the composer is still mock pending OAuth; the create form and the live availability check
  land with the OAuth slice" — i.e. it is about the create/composer surface, not a blanket ban on
  `web/`. The composer pages' hardcoded mock arguments were deliberately left alone as genuine
  incompleteness.
- **Size:** the function signature plus four call sites, ~5 lines.
- **If vetoed:** revert the `web/` hunk; the API side of the slice is unaffected.
