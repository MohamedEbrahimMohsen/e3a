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
