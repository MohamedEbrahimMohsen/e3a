# Stage 0 — Workflow Acceptance (PROXIED)

**Date:** 2026-08-28
**Feature slug:** `publish-pipeline`
**Base branch:** `main` @ `ba2c824` (PR #3 merged, tree clean, in sync with origin)
**Pipeline snapshot:** `00-pipeline.svg`

## Dev authorisation

The dev is away for an extended period and granted blanket authority:

> great, now without my interuption, complete everything in the flow, I grant you permission to merge the PRs

Stage 0 acceptance, the Stage 1 plan gate, and every product judgment call in this slice are
therefore **proxied by the orchestrator** and recorded here as an explicit veto list. PR #3 was
merged under the same authorisation.

## Models — carried forward from the dev's last explicit instruction

> replace Fabel 5 with Opus 5, and Opus 5 with Opus 4.8, to save tokens

All stages run on **OPUS 5**. Opus 4.8 remains unselectable (the model override exposes tiers only:
`opus` / `sonnet` / `haiku` / `fable`), which was surfaced to the dev before the slug slice and he
chose to keep Stage 2 on Opus 5. The `.claude/agents/*.md` frontmatter defaults are untouched.

## Feature request

Build the publish pipeline: a creator publishes an engineer, and it becomes an installable Claude
Code plugin at an immutable versioned URL, listed in a `marketplace.json` that Claude Code consumes
natively.

## SCOPE SPLIT — orchestrator decision (veto item)

The full P3 pipeline is 3–4× the size of any slice run so far. Shipping it as one unit risks a weak
plan and burning both rework rounds. It is split:

- **This slice — `publish-pipeline`:** everything needed for an engineer to become installable.
- **Next slice — `security-scan`:** the scanner engine, its rule tiers, corpus fixtures, and the
  `Rejected` path wired into the worker as one additional step.

The scanner is cleanly separable (the worker gains one step between validate and zip) and carries
its own substantial test corpus. Deferring it is safe **only because nothing is deployed or public
yet** — no domain is live, so no untrusted content can reach a consumer in the interim. The scanner
MUST land before the first real publish. Recorded in `docs/implementation-plan.md`.

## In scope

1. `ItemVersion` aggregate + EF configuration + migration (`versions-002`).
2. `POST /api/engineers/{id}/publish` → `202`, taking `VersionIncrement { Patch, Minor, Major }`.
3. Domain event → `IStorageQueueClient` → queue `publish-jobs`, mirroring Morabh's
   `OrderWorkflowNotificationEventHandler` producer pattern.
4. **New project `E3A.Jobs`** — isolated Azure Functions worker (.NET 10, v4), mirroring
   `Morabh.Jobs`: `[QueueTrigger("publish-jobs", Connection = "StorageAccountConnection")]`.
5. Worker sequence: load version (ignore if not `Queued`) → `Building` → freeze
   `drafts/{userId}/{engineerId}/**` into `snapshots/{versionId}/**` → assemble plugin tree from the
   snapshot + `FrozenManifestJson` → validate structure → deterministic zip + sha256 → upload to
   `public/z/{pluginName}/{semanticVersion}.zip` → `Published` + `engineer.MarkPublished(...)` →
   write pinned `public/m/{pluginName}/{semanticVersion}/marketplace.json` → regenerate root
   `marketplace.json` from the database.
6. `GET /api/publish/{versionId}/status` for the composer to poll.
7. Unlist / relist: `EngineerStatus.Unlisted`, blocked while a version is `Queued`/`Building`.
8. `Core.Azure.IStorageBlobClient.UploadAsync` overload taking content type, cache control and an
   explicit overwrite flag (announced core change, same class as the approved `DeleteByPrefixAsync`).
9. Postman collection updated (blocking pipeline rule).
10. Docs sync: `architecture.md` (Functions worker replaces the BackgroundService; Cloudflare purge
    removed), `implementation-plan.md` (stack line, P3 scope, the scanner split).

## Out of scope

- Security scanner (next slice).
- Teams (P5) — `ItemVersion` carries `ItemType` so teams slot in without a schema change.
- Frontend publish UI — the composer is still mock pending OAuth.
- Cloudflare Worker / CDN configuration — no domain is live.

## Proxied product decisions (dev veto list)

| # | Decision | Call | Rationale |
|---|----------|------|-----------|
| 1 | Worker host | **Azure Functions (`E3A.Jobs`)** | Dev confirmed explicitly. Container Apps scale-to-zero means an in-API `BackgroundService` is not running when no HTTP traffic exists, so queued jobs would stall. |
| 2 | Version numbering | Creator picks `Patch`/`Minor`/`Major`; first publish is always `1.0.0` | Dev confirmed. UI labels Fix / Update / Rewrite. |
| 3 | Unlist | In scope, `EngineerStatus.Unlisted`, blocked while publishing | Dev described exactly this behaviour. Marketplace regeneration is being built here anyway, so deferring would mean reopening the pipeline. **Unlist ≠ takedown**: zips stay live at immutable URLs so existing installs keep working; only discovery stops. |
| 4 | Cloudflare purge | **Dropped.** Cache headers set at blob write time instead: `marketplace.json` `max-age=60`, zips `max-age=31536000, immutable` | Dev asked "why do you need purge?" — at this traffic a short TTL costs nothing and removes an API token, a secret, and a moving part. |
| 5 | Domain | Config value, defaulting to **`e3a.dev`** | PROXY. `web/src/lib/config.ts:2` already assumes `https://e3a.dev`. The dev never confirmed a domain, but this does **not** block implementation — the hostname is only baked in permanently when a real publish runs against a live domain, and nothing is deployed. It must be confirmed before the first production publish. |
| 6 | Attribution before OAuth | `author.name` = creator's DisplayName; `homepage` = the engineer's e3a catalog page | PROXY. No GitHub login exists yet; the GitHub URL is added by the OAuth slice. |
| 7 | Failed publish | **Burns the version number** | PROXY. Version numbers are an audit trail; reuse makes "which 1.2.0 was that?" unanswerable. Costs only gaps in the sequence. |
| 8 | Version cap | 50 per item, from `[Area]Options` | PROXY, carried from the implementation plan. |
| 9 | Enqueue race | Queue message sent with a short `visibilityTimeout`; worker treats "version not found" as retryable | PROXY. `CoreDbContext.SaveChangesAsync` publishes domain events **before** `base.SaveChangesAsync`, so the message can otherwise beat its own row to the queue. Morabh has the same latent race. |
| 10 | Zip immutability | Uploading over an existing version blob is a hard failure | PROXY. `Core.Azure.UploadAsync` already defaults to no-overwrite, so this is free; only `marketplace.json` needs overwrite. |

## Deferred items this slice must absorb (from earlier slices)

- **RC4 (PR #2):** staged-prefix atomic replace when rewriting `marketplace.json`.
- **RC17 (PR #2):** bounded pagination when the generator enumerates engineers.
- Manifest source switches from the engineer's live draft to the version's `FrozenManifestJson`.

## Known debts NOT to be fixed here

`Core.Utilities.IGenerator` trailing separator · `.gitignore:20` `publish/` swallowing
`web/src/features/publish/` (spun off as its own task) · resx overriding interpolated validator
messages (D1) · options failing open when config keys are absent (D2).
