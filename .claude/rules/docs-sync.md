# Docs–Implementation Sync Rule

Every review of changes in this repo (human, /code-review, or any reviewer agent) MUST
check `/docs` consistency against the change being reviewed — using this distinction:

## NOT a violation — incompleteness

The docs describe the target; the code lags behind. That is the normal state of an
in-progress plan. Examples:

- The plan lists 10 features and only 6 exist.
- `docs/plugin-spec.md` describes the team merge rules but team compilation isn't built yet.
- A phase is half-done.

Never flag missing implementation as a docs problem, and never "fix" docs by deleting
the not-yet-built parts.

## A violation — divergence

The change alters WHAT the product does or HOW it is designed, and the doc that
describes that area still says the old thing. If the implementation and the doc give
two different answers to the same question, the change is incomplete until the doc is
updated in the same change. Examples of divergence triggers:

- Business logic or product behavior changes (e.g. the upload-only pivot: composer flow
  changed → plugin-spec, implementation-plan, security-scan, design-prompt all had to move).
- Scope changes: a feature added, dropped, replaced, or deferred.
- Architecture or data-model changes: new/removed Azure resources, tables, pipeline steps.
- Policy changes: limits, security-scan rules or tiers, naming/format contracts
  (plugin layout, marketplace.json shape, URL schemes).
- Constitution-level rule changes.

## Doc ownership map (what to check per change area)

| Change touches… | Doc that must agree |
|---|---|
| Scope, phases, feature list | `docs/implementation-plan.md` |
| Plugin layout, ingestion/mapping, naming, marketplace format, team merge rules | `docs/plugin-spec.md` |
| Scanner rules, tiers, sanitize behavior, hooks policy | `docs/security-scan.md` |
| Azure resources, pipeline sequence, serving/caching, backend structure | `docs/architecture.md` |
| Engineering rules, style, config policy | `docs/constitution.md` |
| Any UI page's content, flow, or components | `docs/design-prompt.md` |

## Reviewer output

When divergence is found, report it as a blocking finding naming BOTH sides: the code
change and the stale doc section. When only incompleteness is found, say nothing about
docs. All docs live in `/docs` only (plus root README.md) — a doc created anywhere else
is itself a violation.
