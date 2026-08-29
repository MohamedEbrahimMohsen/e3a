# Metrics — `frontend-auth`

**Base branch:** `main` (OAuth merged; teams still in review as PR #7)
**Feature branch:** `feature/frontend-auth`
**Stage 0 acceptance:** PROXIED by the orchestrator under blanket dev authorisation (see `00-acceptance.md`).

## Run log

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 0 | Acceptance | orchestrator (proxied) | — | — | — | — | — | — | accepted |
| 1 | Plan | feature-planner | OPUS 5 | 2026-08-29 | 2026-08-29 | 17m 09s | 174,594 | 42 | plan written (22 decisions, 26 new files, 48 tests, 2 docs edits) |

| 2 | Gate | orchestrator (proxied) | — | — | — | — | — | — | APPROVED — verified CORS has no `AllowCredentials` (which is what makes decision 1's anchor reasoning correct), the stale `.env.example` port, and that `PublishStatusResult` still declares `EngineerId` on this branch |
| 3 | Implement | feature-implementer | OPUS 5 | 2026-08-29 | 2026-08-29 | 13m 49s | 168,992 | 66 | 26 files created, 17 modified, 54 test cases green; 5 declared deviations; **found `web/src/features/publish/` was git-ignored and never committed** |
| 4 | Review r1 | feature-reviewer | OPUS 5 | 2026-08-29 | 2026-08-29 | 11m 54s | 163,780 | 80 | CHANGES_REQUESTED (1 blocking: publish-failure panel rendered raw error codes — a plan defect) |

| 5 | Rework r1 | feature-implementer | OPUS 5 | 2026-08-29 | 2026-08-29 | 5m 21s | 78,388 | 40 | `failureText()` + 9 mapped codes; merged `origin/main`; 54 -> 58 |

| 6 | Review r2 | feature-reviewer (fresh) | OPUS 5 | 2026-08-29 | 2026-08-29 | 6m 47s | 113,891 | 35 | CHANGES_REQUESTED (1 blocking: the teams merge added a 7th validator code the map did not know) |

| 7 | Rework r2 | feature-implementer | OPUS 5 | 2026-08-29 | 2026-08-29 | 4m 12s | 53,608 | 29 | `PLUGIN_DUPLICATE_PATH` mapped + completeness sweep; 58 passing |
