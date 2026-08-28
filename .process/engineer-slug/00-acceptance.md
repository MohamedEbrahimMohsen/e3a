# Stage 0 — Workflow Acceptance

**Date:** 2026-08-28
**Feature slug:** `engineer-slug`
**Base branch:** `main` @ `5b8fd6a` (PR #2 merged, tree clean, in sync with origin)
**Pipeline snapshot:** `00-pipeline.svg` (frozen copy of `docs/feature-pipeline.svg` as accepted)

## Feature request

Creator-typed engineer slug. The slug becomes the permanent plugin name `e3a-{slug}` —
e.g. an engineer named "Mohamed Mohsen" with slug `mmohsen` publishes as `e3a-mmohsen`.
This supersedes the earlier `e3a-{githublogin}-{item-slug}` naming and removes GitHub
login from the plugin identity.

## Models — this run only (dev-directed override)

| Stage | Agent | Model | Default in frontmatter |
|-------|-------|-------|------------------------|
| 1 · Plan | `feature-planner` | **OPUS 5** | fable |
| 2 · Implement | `feature-implementer` | **OPUS 5** | opus |
| 3 · Review | `feature-reviewer` | **OPUS 5** | fable |
| 4 · Triage / verify | `feature-reviewer` | **OPUS 5** | fable |
| 4 · Fix | `feature-implementer` | **OPUS 5** | opus |

Dev asked for Fable 5 → Opus 5 and Opus 5 → Opus 4.8 "to save tokens". The first swap is
applied as a per-call override (agent files untouched; next run reverts to defaults).
**Opus 4.8 was NOT applied** — the model selector exposes tiers only (`opus`/`sonnet`/
`haiku`/`fable`) and `opus` resolves to Opus 5 in this session. Surfaced to the dev before
acceptance; dev chose to keep Stage 2 on Opus 5 rather than drop to Sonnet 5.

## Terms accepted

- Two dev gates: plan approval before implementation, and the round-2 stop
- Rework cap: max 2 review rounds, then re-plan
- Stage 4: PR → CodeRabbit → triage (critical must-fix; major/minor reviewer's call) → fix → verify, cap 2 cycles
- Implementation on `feature/engineer-slug`, branched from `main`

## Scope accepted

1. `CreateEngineerCommand` gains `Slug`; handler uses it as the base and keeps the existing
   `IsSlugExistsAsync` + `IGenerator` suffix loop as a race guard — skill §8.3 unchanged.
2. New `GET /api/engineers/slug-availability?slug=` so the dev sees "taken" before submitting.
3. `UpdateEngineerCommand` gains slug editing, guarded by the freeze rule.
4. New error codes + `ar`/`en` resx entries.
5. `EngineersOptions` gains `SlugMinLength` + `ReservedSlugs`.
6. Postman collection updated (blocking pipeline rule).
7. Docs divergence in the same change: `plugin-spec.md` (naming line 11, example lines 87, 94)
   and `implementation-plan.md` (data model line 34, naming line 44).

**Out of scope:** frontend. The composer is still mock pending OAuth; the create form with the
live availability check lands with the OAuth slice.

## Rules locked

| Rule | Value |
|------|-------|
| Format | `^[a-z0-9]+(-[a-z0-9]+)*$` — kebab-case, no leading/trailing/double hyphens |
| Length | 3 – `SlugMaxLength` (100) |
| Reserved | `e3a`, `api`, `admin`, `www`, `docs`, `health`, `install`, `marketplace`, `catalog`, `teams`, `new`, `edit`, `settings`, `z`, `m` — config list, not constants |
| Mutable while | `LatestVersionId == null`; frozen permanently after the first publish |
| Collision | auto-suffix via `IGenerator`; availability endpoint makes it rare |
| `DisplayName` | independent free text |

No data migration — the 16 seeded engineers already hold valid kebab-case slugs.

## Dev acceptance (verbatim)

> Accept, Keep stage 2 Opus 5, Go Ahead, and again ask me questions as much as possible because I will be away for long time.

Preceded by, on ordering and the slug design:

> regarding the slug, there is two options, dev enters slug, and system find or suggest another slug, then sends back and create with this slug or dev enters a username, it's not found add suffix (i believe this is much easier)
>
> but definetly we should has the slug before the OAuth

> propsed order is correct

Agreed slice order: **slug → publish → OAuth**.

## Dev availability

Dev is away for an extended period. Per the established pattern from the `public-catalog`
run, judgment calls that would normally be dev-gated are decided as **proxy** and recorded
here and in `04-metrics.md` as an explicit veto list for the dev's return. The Stage 1 plan
gate is proxied on the same terms.
