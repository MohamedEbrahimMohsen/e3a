# Stage 0 — Workflow acceptance

**Date:** 2026-09-04 23:35
**Slice:** domain-consistency (2 of 7 in an unattended queue)

## Feature request (from the dev)

> regarding number 6, I don't know what is this, but I need to highlight here that the domain
> is e3a.ai not a3e.dev

The dev confirmed the real domain is **e3a.ai**. An audit then found the repo is split: the
backend runtime config already says `e3a.ai`, while the frontend default, `.env.example`,
the publishing test fixtures and `docs/design-prompt.md` all still say `e3a.dev`. Because
`web/.env.local` does not set `VITE_SITE_URL`, the running site currently renders install
commands pointing at the wrong domain.

## Pipeline terms accepted

Same terms as the stack-trace-leak slice: all three stages on **Opus 5**, PR via the GitHub
REST API with CodeRabbit, merge to `main` on pass, park and continue if blocked.
