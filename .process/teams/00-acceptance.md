# Stage 0 — Workflow Acceptance (PROXIED)

**Date:** 2026-08-29
**Feature slug:** `teams`
**Base branch:** `main` @ `6def01a` (tree clean, in sync with origin)
**Pipeline snapshot:** `00-pipeline.svg`

## Dev authorisation

The dev is asleep and granted blanket authority for four consecutive features:

> go ahead and do the fearures 1, 2, 3 and 6, regarding the feature 4 & 5 skip it for now.
> but I will go to slepp now, so ask all your questions or your requirments now and when you're
> ready to go, don't ever stop by any mean untill you finished, I grant you all the permissions to
> commit, create PR, merge PR, anything will not block the implementation do it unless you needed
> to create any resource in Azure, that's only my job.

Stage 0 acceptance, the Stage 1 plan gate, and every product judgment call are **proxied by the
orchestrator** and recorded here as an explicit veto list.

**Standing prohibition:** no Azure resource may be created. This slice needs none — teams reuse the
existing `publish-jobs` queue and the existing blob containers.

## Models

All stages run on **OPUS 5**, per the dev's standing instruction.

## Feature request

Teams: a creator bundles member engineers at **pinned versions** into a single installable Claude Code
plugin. Members are frozen at the version pinned when the team is published, so a member republishing
never mutates a published team.

## Dev's answer, already given (binding — do not re-decide)

> **Option (b): `e3a-team-{slug}` prefix.**

The dev chose this **explicitly over the shared-namespace option the orchestrator recommended**. Teams
carry a `team-` segment; engineers stay `e3a-{slug}`. Collision between a team slug and an engineer
slug becomes structurally impossible. Do not "simplify" this back to a shared namespace.

Also carried from the run brief: **team members pin to an exact `ItemVersion`**, and the cap is
**10 teams per creator** (from `docs/implementation-plan.md`).

## SCOPE SPLIT — orchestrator decision (veto item)

Full P5 as described in `docs/implementation-plan.md` is team CRUD **plus** the namespaced team
compile from snapshots **plus** the "newer member versions" republish flow. That is comparable in size
to the whole `publish-pipeline` slice, and this is the third of four features in an unattended run.

It is split:

- **This slice — `teams`:** the team aggregate, membership with pinned versions, CRUD, the limits, and
  publishing a team through the **existing** worker so it produces an installable `e3a-team-{slug}`
  plugin.
- **Deferred — `team-compile-merge`:** the richer merge rules in `docs/plugin-spec.md` (hook
  concatenation with per-member attribution, `.mcp.json` / `.lsp.json` merge-by-server-name with
  collision prefixing) and the "a member has a newer version, republish to adopt it" prompt.

Deferring is safe because nothing is deployed and no team can be published to a live domain yet. If
the planner judges the split unsound, it must say so rather than silently building either half.

## In scope

1. `Team` aggregate + `TeamMember` (pinned `ItemVersionId`, sort order) + EF configuration + migration
   (`teams005`).
2. Team CRUD, owner-gated, mirroring the engineer endpoints that already exist.
3. Member management: add, remove, reorder — each member pinned to an exact published `ItemVersion`.
4. Limits from `[Area]Options`: 10 teams per creator, and a cap on members per team.
5. `POST /api/teams/{id}/publish` → `202`, reusing `ItemVersion` with `ItemType.Team` and the existing
   `publish-jobs` queue — **no new queue, no new container**.
6. Team plugin assembly from member **snapshots**, with `skills/{member-slug}--{skill-slug}/`
   double-hyphen namespacing and `{member-slug}--` prefixing on `agents/` and `commands/` collisions,
   per `docs/plugin-spec.md`.
7. Plugin name `e3a-team-{slug}`.
8. Teams appear in `marketplace.json` alongside engineers.
9. Postman collection updated (blocking pipeline rule).
10. Docs sync per `.claude/rules/docs-sync.md`.

## Out of scope

- Hook concatenation and `.mcp.json` / `.lsp.json` merging (deferred slice above).
- The "newer member versions available" republish prompt.
- Team unlist/relist beyond whatever falls out of reusing the engineer pattern for free.
- Frontend team surfaces — feature 4 of this run, and the dev's confirmed workspace flow does not yet
  include a team composer.
- Install counting and reports — explicitly skipped by the dev.

## Proxied product decisions (dev veto list)

| # | Decision | Call | Rationale |
|---|----------|------|-----------|
| 1 | Plugin naming | **`e3a-team-{slug}`** | The dev's explicit answer, chosen over the orchestrator's recommendation. Locked. |
| 2 | Versioning | **Reuse `ItemVersion` with `ItemType.Team`** | The column already exists precisely so teams slot in without a schema change — stated in the `publish-pipeline` acceptance. |
| 3 | Worker | **Reuse `ProcessPublishJobHandler`, branching on `ItemType`** | A second worker would duplicate freeze/validate/zip/upload/marketplace. If the branch makes the handler unwieldy, the planner should propose a shared pipeline with two assemblers rather than two workers. |
| 4 | Member pinning | **An exact `ItemVersionId`, captured when the member is added** | The run brief. A member republishing must never mutate a published team. |
| 5 | Adding an unpublished engineer | **Rejected.** A member must have at least one `Published` version | Pinning to a version that was never built would produce a team that cannot assemble. |
| 6 | A member engineer later unlisted or deleted | **The published team keeps working**; the pinned zip is immutable | Consistent with "unlist ≠ takedown" from the publish slice. Existing installs must not break. |
| 7 | Cross-owner membership | **Allowed.** A team may include another creator's published engineer | Teams are curation; the content is already public and immutably versioned. Attribution stays with the member's own author metadata. |
| 8 | Team slug rules | **Identical to engineer slugs** — creator-typed, kebab-case, reserved words rejected, frozen after first publish | The `engineer-slug` slice settled this; a second slug policy would be a second thing to get wrong. |
| 9 | Members-per-team cap | **From `[Area]Options`**, not a const | House rule. The planner picks the value and states its basis. |
| 10 | Empty team publish | **Rejected** | An empty team produces a plugin with no installable content, which `PluginStructureValidator` already blocks — failing early gives a better message. |
| 11 | Security scan on team publish | **Not wired here.** The scanner is parked on `feature/security-scan` and unmerged | Member content was already scanned at member-publish time once that slice lands. Recorded so its absence is not read as an oversight. |

## Known constraint

`feature/security-scan` is parked with an open defect (`.process/security-scan/09-stopped.md`) and
`feature/github-oauth` is in review as PR #5. This slice branches from **`main`**, so it has neither.
Any interaction between teams and the scanner is the merge-order problem of whoever lands second.

## Known debts NOT to be fixed here

Everything carried from earlier slices.

## Correction — 2026-08-29 (appended; the decision above is unchanged)

Decision 1's stated rationale at lines 39-40 — "collision between a team slug and an engineer slug
becomes structurally impossible" — **was false as implemented**, and CodeRabbit RC3/RC4 on PR #7
falsified it. The `e3a-team-` prefix on its own does not separate the namespaces: engineer slug
`team-alpha` and team slug `alpha` both produce the plugin name `e3a-team-alpha`, and nothing rejected
an engineer slug beginning `team-`. The consequence was silent artifact adoption in
`ProcessPublishJobHandler` — the second publisher's zip upload is skipped because the blob path
already exists, and the version is still marked `Published` with a sha256 that does not match the
served bytes — plus an overwritten pinned marketplace and duplicate root `marketplace.json` entries.

The decision itself stands: `e3a-team-{slug}` remains the naming scheme. What has changed is that the
invariant is now **enforced** rather than assumed. `PluginName.IsTeamNamespaced(slug)` rejects the
`team-` prefix in `CreateEngineerValidator`, `UpdateEngineerValidator` and
`CheckSlugAvailabilityQueryValidator` with `ENGINEER_SLUG_RESERVED`. Because a team can only ever
produce `e3a-team-{x}`, closing the engineer side alone makes the two namespaces disjoint in both
directions, with no cross-repository lookup.
