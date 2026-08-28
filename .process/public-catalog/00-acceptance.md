# Workflow Acceptance — public-catalog (slice ⑤ of the v0.1 roadmap)

**Date:** 2026-08-28 02:40
**Pipeline:** FABLE 5 plan → OPUS 5 implement → FABLE 5 review · plan gate PROXIED (below) · 2-round rework cap · Stage 4 (PR + CodeRabbit) DEFERRED to after the dev's morning commit (snapshot: `00-pipeline.svg`)
**Pre-flight:** clean — main at merge commit `1350fb0` (PR #1 merged by dev), branch `feature/public-catalog` from `main`
**Base branch:** `main` (default — no warning needed)

**Feature request (verbatim):**
> Let's implement the public catalog for now, Then after that create a postman collection for all the endpoints, and ensure the process itself updated where the implementer should add/modify/delete the [collection entries] to keep them updated. Also the reviewer should ensure it updated. Then do seed many initial test data but ensure they are seeded from the endpoints to ensure everything is working. Then update the frontend, and also ensure it working properly and there is no crashes in the UI and if there is ability to seed also go ahead and seed from the frontend and also verify the catalog and the filters and tags.

**Dev acceptance & standing authorizations (2026-08-28, before sleeping):**
- Workflow accepted; slice ⑤ runs overnight.
- **Plan-gate proxy**: orchestrator gates the plan as the dev's proxy against locked decisions; every judgment call resolved is listed in the morning report with dev veto.
- Seeding relaxed to **database-direct** ("okay, you can change it and seed from the database if you can do it for now") — dev-only helper endpoints not required.
- Postman collection at repo-root `postman/`; process updated: implementer maintains it, reviewer verifies it.
- Frontend scope: PUBLIC pages only (home/catalog/detail) on the real API; auth pages stay mocked until GitHub OAuth slice.
- Local-run blanket approval: edit machine-local gitignored appsettings.json, create/migrate local SQL DB, Development-gated code fallbacks if startup blocks (declared in report).
- **Nothing gets committed** — the dev commits everything together in the morning. Stage 4 runs after that.
