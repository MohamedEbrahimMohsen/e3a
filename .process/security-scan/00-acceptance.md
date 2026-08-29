# Stage 0 — Workflow Acceptance (PROXIED)

**Date:** 2026-08-29
**Feature slug:** `security-scan`
**Base branch:** `main` @ `6def01a` (tree clean, in sync with origin)
**Pipeline snapshot:** `00-pipeline.svg`

## Dev authorisation

The dev is asleep and granted blanket authority for four consecutive features:

> go ahead and do the fearures 1, 2, 3 and 6, regarding the feature 4 & 5 skip it for now.
> but I will go to slepp now, so ask all your questions or your requirments now and when you're
> ready to go, don't ever stop by any mean untill you finished, I grant you all the permissions to
> commit, create PR, merge PR, anything will not block the implementation do it unless you needed
> to create any resource in Azure, that's only my job.

and, at the start of this run:

> don't stop untill finished, if there is any feature blocked for any reason skip/ignore it and
> continue with the others, beucase I will be sleep and will not be able to monitor.

Stage 0 acceptance, the Stage 1 plan gate, and every product judgment call in this slice are
therefore **proxied by the orchestrator** and recorded here as an explicit veto list.

**Standing prohibition:** no Azure resource may be created. This slice needs none — it adds a pure
in-process scanning step to an existing worker.

## Models

All stages run on **OPUS 5**, per the dev's standing instruction (Opus 4.8 is not selectable — the
model override exposes tiers only). `.claude/agents/*.md` frontmatter defaults are untouched.

## Feature request

Build the security scanner deferred out of the `publish-pipeline` slice. Every publish runs a
rule-based scan over the composed plugin **before** anything becomes downloadable. A blocking
finding rejects the version with per-file, per-line reasons the creator can act on.

This is the gate that must land **before the first real publish** — `publish-pipeline`'s scope
split was declared safe only because nothing is deployed yet.

## In scope

1. **Scanner engine** in `E3A.Application/Publishing/Security/` — pure, deterministic, no I/O:
   rule definitions, per-file line-oriented matching, severity aggregation, report model.
2. **Rule categories** exactly as `docs/security-scan.md` specifies: credential exfiltration,
   encoded payloads, dangerous commands, instruction injection, hygiene blocks.
3. **Script tier** — files with `.sh` / `.ps1` / `.js` / `.py` extensions carry the markdown rules
   plus script-only rules and a lower Block threshold, per the doc's post-pivot note.
4. **Outcomes** — `Block` → version `Rejected`; `Warn` → published but flagged.
5. **`ItemVersion.MarkRejected(...)` + `ScanReportJson`** column and migration (`scan-003`).
6. **Wiring** into `ProcessPublishJobHandler` between structure validation and zipping — nothing
   reaches the public container when the scan blocks.
7. **Scan report surfaced** on `GET /api/publish/{versionId}/status` so the composer can render
   per-file reasons.
8. **Hook-count warning data** — the scan result carries the number of auto-running hook scripts so
   the catalog detail page can show the mandatory notice.
9. **Corpus fixtures** — every rule gets both a positive (malicious) and a negative (benign)
   fixture, per the doc: "Every rule has corpus fixtures".
10. Postman collection updated if any contract changes (blocking pipeline rule).
11. Docs sync per `.claude/rules/docs-sync.md`.

## Out of scope

- The **sanitize step** (stripping `settings.local.json`, `.env*`, memory/session files). That runs
  at *upload* time and already has an owner in the import-manifest slice; adding it here would edit
  a different slice's handler. Recorded as a carried debt, not silently dropped.
- The catalog **detail-page rendering** of the hook warning — this slice produces the data; the
  frontend slice (feature 4 of this run) consumes it.
- The **report button** / abuse flow — explicitly skipped by the dev (feature 5).
- Re-scanning already-published versions.

## Proxied product decisions (dev veto list)

| # | Decision | Call | Rationale |
|---|----------|------|-----------|
| 1 | Where the scan runs | **In the worker, between validate and zip** | Declared in the `publish-pipeline` acceptance ("the worker gains one step between validate and zip"). Scanning at upload time would miss the generated plugin tree. |
| 2 | Rejected version numbering | **Burns the version number**, same as `Failed` | Consistent with decision 7 of the publish slice; version numbers are an audit trail. |
| 3 | Warn tier behaviour | **Publishes, and the report is persisted and returned** | The doc says "published, flagged for review". With reports/abuse skipped, persisting the report is the whole of "flagged" available today. |
| 4 | Rule severity thresholds | **Block on any single Block-severity rule; Warn on any Warn-severity rule; script tier promotes selected Warn rules to Block** | The doc specifies a "lower Block threshold" for scripts without a number. A per-rule severity table is more auditable than a score, and reviewable line by line. |
| 5 | Binary / non-text files | **Not pattern-scanned; handled by the hygiene rules only** (executable extension, oversize) | Regex over binary is noise. The hygiene tier already blocks executables outright. |
| 6 | Scan report size | **Capped from `PublishingOptions`, with an explicit "truncated" flag** | An unbounded report is an unbounded `nvarchar(max)` write driven by untrusted input. |
| 7 | Rule catalogue location | **C# constants in the Application layer, not configuration** | House rule: caps live in Options, but the rules themselves are behaviour under test. Config-driven regex is an unreviewed code path and a ReDoS vector. |
| 8 | ReDoS safety | **Every rule compiled with a match timeout; no nested unbounded quantifiers** | Untrusted input drives the regex engine. Non-negotiable. |
| 9 | Where the failure reason goes | **`FailureReason` gets the error code; `ScanReportJson` gets the detail** | `FailureReason` is capped at 500 chars and already carries codes. |
| 10 | False-positive escape hatch | **None in this slice** | A creator-facing suppression mechanism is a product decision the dev has not made. Blocked publishes are visible and the dev can override in the database until then. |

## Known debts NOT to be fixed here

Upload-time sanitize step · preserve-by-default normalizer · `.gitignore:20` `publish/` ·
`Core.Utilities.IGenerator` trailing separator · DD1 marketplace-regeneration concurrency · DD2 ·
`03-review.md` follow-ups N1, N3, N5–N9 · the `AuditBehaviour` landmine in
`.process/publish-pipeline/04-metrics.md`.
