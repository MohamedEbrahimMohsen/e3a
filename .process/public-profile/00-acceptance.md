# Stage 0 — Workflow acceptance

**Date:** 2026-09-05 01:33
**Slice:** public-profile (4 of 7 in an unattended queue)

## Feature request

`/u/:login` works only for the signed-in creator's own profile — a stopgap shipped earlier in
this session. Every other creator's profile renders "Public profiles aren't available yet", and
catalog cards link to author profiles, so a visitor clicking an author hits a dead end.

There is no public endpoint that lists a creator's published items: `GET /api/catalog` supports
only search/tags/sort/paging with no author filter, and `CatalogEngineerResult` does not carry
the owner's login. `GET /api/engineers/mine` is `[Authorize]` and self-only.

## Pipeline terms accepted

All three stages on **Opus 5**; PR via the GitHub REST API with CodeRabbit triggered manually
(repo has <10 stars, so no automatic review); merge to `main` on pass; park and continue if blocked.
