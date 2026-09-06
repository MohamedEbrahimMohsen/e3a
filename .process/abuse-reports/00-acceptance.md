# Stage 0 — Workflow acceptance

**Date:** 2026-09-05 00:06
**Slice:** abuse-reports (3 of 7 in an unattended queue)

## Feature request

`web/src/app/ReportContext.tsx` `submit()` shows a "Report submitted — thank you" toast and
makes no network call. There is no reports table and no endpoint, so no report ever reaches
anyone. The button lies to users, and reporting is the human backstop for the security scanner
that now gates every publish.

`docs/implementation-plan.md` specifies a `reports` table and "Social: POST report (anon OK)".

## Pipeline terms accepted

All three stages on **Opus 5**; PR via the GitHub REST API with CodeRabbit triggered manually
(the repo has <10 stars so CodeRabbit does not auto-review); merge to `main` on pass; park and
continue if blocked.
