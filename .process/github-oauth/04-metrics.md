# Metrics — `github-oauth`

**Base branch:** `main` @ `6def01a`
**Feature branch:** `feature/github-oauth`
**Stage 0 acceptance:** PROXIED by the orchestrator under blanket dev authorisation (see `00-acceptance.md`).

## Run log

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 0 | Acceptance | orchestrator (proxied) | — | — | — | — | — | — | accepted |
| 1 | Plan | feature-planner | OPUS 5 | 2026-08-29 | 2026-08-29 | n/a | n/a | n/a | plan written (589 lines, 24 decisions, ~60 tests) |

| 2 | Gate | orchestrator (proxied) | — | — | — | — | — | — | APPROVED — independently verified `Constants.UserIdClaimType == ClaimTypes.NameIdentifier`, the `IGenerator.Generate` overload ambiguity, and that the plan sets `NormalizedUserName` (unique index permits only one NULL) |
| 3 | Implement | feature-implementer | OPUS 5 | 2026-08-29 | 2026-08-29 | 15m 30s | 181,112 | 72 | done — 21 production files, 14 test files, migration `oauth004`, 419 passing; 3 declared deviations |
| 4 | Review r1 | feature-reviewer | OPUS 5 | 2026-08-29 | 2026-08-29 | 12m 06s | 193,866 | 46 | CHANGES_REQUESTED (1 blocking: signature-before-expiry order unguarded by any test) |
| 5 | Rework r1 | feature-implementer (fresh) | OPUS 5 | 2026-08-29 | 2026-08-29 | 6m 01s | 71,329 | 28 | ordering test added and mutation-proven; log-level floor; 420 passing |
| 6 | Review r2 | feature-reviewer (fresh) | OPUS 5 | 2026-08-29 | 2026-08-29 | 9m 25s | 113,993 | 41 | **APPROVED** — mutation experiment independently re-run; scope containment proven from file mtimes |
| 7 | PR + CodeRabbit r1 | external | — | 2026-08-29 | 2026-08-29 | ~5m | — | — | PR #5 opened; 13 inline comments |

| 8 | Triage | feature-reviewer (fresh) | OPUS 5 | 2026-08-29 | 2026-08-29 | 9m 06s | 101,409 | 40 | 3 implement, 6 rejected, 1 dev-decision; **found the CSRF deferral rested on a false premise** |

| 9 | CodeRabbit rework | feature-implementer | OPUS 5 | 2026-08-29 | 2026-08-29 | 14m 36s | 169,911 | 61 | CSRF browser binding + UserName collision; 420 -> 433 |

| 10 | CodeRabbit verify | feature-reviewer (fresh) | OPUS 5 | 2026-08-29 | 2026-08-29 | 10m 37s | 117,987 | 39 | **APPROVED** — mutation-proved the cross-browser test bites; probed the framework to confirm the cookie really clears |
