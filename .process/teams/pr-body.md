## Goal

A creator bundles published engineers at **pinned versions** into one installable Claude Code plugin named `e3a-team-{slug}`. Members are frozen at the version pinned when the team is published, so a member republishing can never mutate an already-published team.

`/plugin install e3a-team-dotnet-product-squad@e3a` installs every member's agents, skills and commands in one go.

## The pinning invariant is structural, not conventional

This is the part worth reviewing carefully, because it is the whole product:

- The roster is frozen into `ItemVersion.FrozenManifestJson` at publish time. The worker reads it from **there**, never from the `TeamMember` rows — so editing membership after publishing cannot alter a published version.
- `TeamPublishBuilder` takes **no `IEngineerRepository`**, so it cannot reach a member engineer's *current* `LatestVersionId` at all. That is supporting evidence, not the whole guarantee — it does take `ITeamRepository` and loads the team, so live `TeamMember` rows are reachable in principle. The guarantee is the data flow above and below: the roster comes from `FrozenManifestJson`, the content from the pinned snapshot prefix.
- Member content comes from each pinned version's own immutable `snapshots/{pinnedVersionId}/` prefix, which is never rewritten.

**Proven by mutation, not by assertion.** Round 1's review caught that the test nominated as proof of this was vacuous — it built a newer member snapshot and never passed it to anything, so both calls took identical input. It was also at the wrong level: the assembler is a pure function over already-pinned snapshots, so no mutation of it could express the defect. The replacement lives at builder level; mutating `TeamPublishBuilder` to resolve by engineer instead of by pin fails exactly one test, and two reviewers independently reproduced byte-identical shas.

## Design decisions worth a look

- **`e3a-team-{slug}`** rather than a shared namespace, per your explicit choice. The prefix alone does **not** make collision impossible — CodeRabbit round 4 showed engineer slug `team-{x}` and team slug `{x}` both produce `e3a-team-{x}`. The namespaces are made disjoint by rejecting the `team-` prefix on the engineer side: `PluginName.IsTeamNamespaced` is enforced by all three engineer slug validators with `ENGINEER_SLUG_RESERVED`.
- **One worker, one pipeline, two builders.** `ProcessPublishJobHandler` switches on `ItemType` and delegates; an inline branch would have pushed it past 150 lines with two entity types to mark published.
- **Idempotent full-replace membership** (`PUT /members`) rather than three endpoints — makes ordering a property of the payload, and removes the member-not-found and reorder-mismatch guards entirely.
- **Collision prefixing is symmetric**: when two members share an agent filename, *both* get prefixed, so output never depends on member order. That is what makes the shuffled-input determinism test possible.
- **Slug logic was moved, not duplicated** — `SharedKernel/SlugGenerator` + `Shared/SlugResolver`. Copying it would have duplicated the suffix-length invariant, which is the exact defect class the skill's §8.3 exists to prevent. Pass 1 was behaviour-neutral: 354/354 with zero assertion edits.
- **A member engineer that is later unlisted or deleted does not break a published team** — the builder never loads the member `Engineer`.

## Verification

| Check | Result |
|---|---|
| `dotnet build api/E3A.slnx --no-incremental` | 0 errors, 9 warnings — all pre-existing, all in `core-libraries` |
| `dotnet test api/E3A.slnx` | **521 / 521** (baseline 354) |

Implemented in 5 checkpointed passes, each with a full build+test before the next began. Migration `teams005` creates only `Teams` and `TeamMembers`. Postman is `+212 / −0`, purely additive.

## ⚠️ Breaking change

`PublishStatusResult.EngineerId` → **`ItemId`**, plus a new `ItemType` field. The old name is wrong for teams. This affects `GET /api/publish/{versionId}/status` and the `202` bodies of both publish endpoints:

```text
{ versionId, itemId, itemType, versionNumber, semanticVersion, status,
  zipUrl, zipSha256, sizeBytes, failureReason, updatedAt }
```

Nothing consumes that endpoint yet — `web/` is still on static fixtures — so this is safe now and would not be later.

## Deliberately deferred — `team-compile-merge`

Hooks concatenation with per-member attribution, `.mcp.json` / `.lsp.json` merge-by-server-name, and the "a member has a newer version, republish to adopt it" prompt. Only `agents/`, `skills/` and `commands/` are merged today.

`docs/plugin-spec.md` promised the richer merge, so it was **marked deferred rather than silently broken** — the promise stands as the target, with today's behaviour stated plainly beside it.

## New configuration

A `Teams` section was added to `appsettings.json` (git-ignored, so not in this diff). Its values drive EF column widths, so it needs mirroring into your other environments and Azure App Configuration before deploying — same standing property as `Engineers` and `Publishing`.

## Pipeline artifacts

- [`00-acceptance.md`](.process/teams/00-acceptance.md) — proxied scope, 11 product decisions, the scope split
- [`01-plan.md`](.process/teams/01-plan.md) — 38 decisions, 50 files, save-count matrix, 147 tests
- [`02-implementation.md`](.process/teams/02-implementation.md) — 5 passes + rework round 1
- [`03-review.md`](.process/teams/03-review.md) · [`03-review-r2.md`](.process/teams/03-review-r2.md) — **APPROVED**
- [`04-metrics.md`](.process/teams/04-metrics.md) — run log

🤖 Generated with [Claude Code](https://claude.com/claude-code)
