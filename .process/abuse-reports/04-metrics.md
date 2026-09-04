# Metrics — abuse-reports

**Base branch:** `main` · **Feature branch:** `feature/abuse-reports`

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 0 | Acceptance | orchestrator | — | 2026-09-05 00:06 | 2026-09-05 00:06 | — | — | — | accepted (queue slice 3 of 7) |
| 1a | Plan (attempt 1) | feature-planner | OPUS 5 | 00:06 | 00:17 | ~11m | n/a | n/a | FAILED — agent stalled (watchdog, 600s no progress); no plan written |
| 1b | Plan (retry) | feature-planner | OPUS 5 | 00:24 | 00:59 | 12m 45s | 163,214 | 44 | plan written; gate self-approved by orchestrator per dev grant |
| 2 | Implement | feature-implementer | OPUS 5 | 00:37 | 01:23 | 22m 49s | 257,057 | 184 | done; 3 deviations declared; all 3 mutations bit |
| 3 | Review r1 | feature-reviewer | OPUS 5 | 01:00 | 01:32 | 7m 57s | 149,887 | 37 | APPROVED (0 blocking, 4 non-blocking) |
