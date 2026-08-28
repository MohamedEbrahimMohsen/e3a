TRIAGE: 4 to implement, 8 rejected, 2 dev-decisions

# CodeRabbit Triage — PR #4 (`publish-pipeline`), commit `bf47eff`

Independent verification re-run before triage:

- `dotnet build api/E3a.slnx --no-incremental` → **0 errors, 9 warnings**, all pre-existing in
  `core-libraries` (`Core.Validation` x2, `Core.Notifications` x5, `Core.OTP` x2). Matches baseline.
- `dotnet test api/E3A.Tests/E3A.Tests.csproj` → **351/351 passed, 0 failed, 0 skipped**. Matches baseline.

CodeRabbit raised **no Critical items**. All 13 inline comments are Major or Minor, so no downgrade
veto is required from the dev.

---

# IMPLEMENT

## 1. Compare the zip blob name exactly, not by prefix (from RC2)

**File:** `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:82`

**Change:**

```csharp
// current
if (existingZips.Count == 0)

// required
if (!existingZips.Exists(name => string.Equals(name, zipBlobPath, StringComparison.Ordinal)))
```

**Why.** `ListByPrefixAsync` is a prefix query (`StorageBlobClient.cs:69` passes `prefix` straight to
`GetBlobsAsync`). `existingZips.Count == 0` therefore treats *any* blob whose name merely starts with
`z/{pluginName}/{semanticVersion}.zip` as proof the real zip exists, skips the upload at line 85, and
still runs `version.MarkPublished(...)` at line 88 — publishing a version whose `ZipUrl` 404s.

Plan decision D4 (`01-plan.md:54`) states the check is "on the exact zip blob name"; the code does not
implement that. This aligns the code with its own approved decision, in one line, with no behavioural
risk to the existing paths.

**Tests.** `ProcessPublishJobHandlerTests.Handle_ShouldSkipZipUpload_WhenBlobAlreadyExists`
(`api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerTests.cs:80`) stubs an exact match
and keeps passing unchanged. Add one sibling test:

- `Handle_ShouldUploadZip_WhenOnlyAPrefixMatchingBlobExists` — stub
  `ListByPrefixAsync(..., PublicBlobContainerName, ZipBlobPath, ...)` to return `[ZipBlobPath + ".bak"]`
  and assert `Received(1)` on the 9-arg `UploadAsync` for `ZipBlobPath`. This test must fail against the
  current `Count == 0` code.

---

## 2. Write the pinned marketplace before persisting `Published` (from RC8, first half only)

**File:** `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:88-97`

**Why.** Today the handler saves `Published` at line 92 and only then uploads the pinned marketplace at
line 97. If that upload throws, the exception bubbles to the Function (correct — no `try`/`catch`), the
message is redelivered, and on retry the guard at line 28 sees `Status == Published` and returns at
line 30. The pinned `/m/{pluginName}/{semanticVersion}/marketplace.json` is then **never written, and
nothing will ever write it**. That artefact is a named deliverable of the plan Goal (`01-plan.md:8-9`)
and of acceptance. This is not the recorded "query not a lock" window — it is an unrecoverable hole in
the retry design, and it is cheap to close.

**Change** — reorder only; keep every existing call, keep the save count at two (D16 preserved), add no
`try`/`catch`:

```csharp
version.MarkPublished(zipBlobPath, zipped.Sha256, zipped.SizeBytes);
engineer.MarkPublished(version.Id);

var pinnedJson = MarketplaceDocumentGenerator.Generate([MarketplaceDocumentGenerator.GeneratePlugin(engineer, version, authorName, publishing)], publishing);

using var pinnedStream = new MemoryStream(Encoding.UTF8.GetBytes(pinnedJson));
await storageBlobClient.UploadAsync(pinnedStream, azure.ManagedIdentityClientId, azure.StorageAccountUrl, azure.PublicBlobContainerName, PublishBlobPaths.PinnedMarketplace(pluginName, version.SemanticVersion), PublishBlobPaths.MarketplaceContentType, publishing.MarketplaceCacheControl, overwrite: true, cancellationToken).ConfigureAwait(false);

itemVersionRepository.Update(version);
engineerRepository.Update(engineer);
await itemVersionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
```

`MarkPublished` sets `ZipBlobPath` / `ZipSha256` in memory, which is what `GeneratePlugin` reads, so the
generated document is unchanged. On an upload failure the version stays `Building` in the database, so
the retry re-enters at line 41, re-zips deterministically, skips the zip upload (item 1), and retries
the pinned write. Idempotent.

Do **not** edit `01-plan.md` to match — see the audit-trail note under REJECT.

**Tests.** Existing `Handle_ShouldWritePinnedMarketplace_WhenPublishing` (line 72) and
`Handle_ShouldPublishVersionAndEngineer_WhenDraftIsValid` (line 50) must still pass. Add:

- `Handle_ShouldNotPersistPublished_WhenPinnedMarketplaceUploadFails` — configure the substitute so the
  `UploadAsync` call for `PinnedMarketplacePath` throws, assert the handler throws, and assert
  `_itemVersionRepository.Received(1).SaveChangesAsync(...)` (the `Building` checkpoint only, not two).

---

## 3. `maxDequeueCount` is total attempts, not retries (from RC11)

**Files:** `docs/architecture.md:40` and `docs/implementation-plan.md:58`

Both currently read: ``Poison queue after `maxDequeueCount` (5) retries.``

`maxDequeueCount: 5` in `api/E3A.Jobs/host.json` means five **total** processing attempts (the initial
delivery plus four redeliveries) before the message is poisoned. Replace both occurrences with:

``Poison queue after `maxDequeueCount` (5) total attempts, including the first.``

Docs-only, both sides of the same sentence, no code change.

---

## 4. Mark the superseded architecture decisions as history (from RC12)

**File:** `docs/implementation-plan.md:15-16` (numbered decisions 1 and 2)

This is a real docs-sync **divergence** under `.claude/rules/docs-sync.md` — the doc and the code give
two different answers to the same question, and `docs/implementation-plan.md` is the owning doc for
scope and phases:

- Decision 1 says the API is a "separate .NET 10 Function App at `api.<domain>`". Line 11 of the same
  file, and this PR, say `E3A.Api` on Azure Container Apps with `E3A.Jobs` as the only Functions host.
- Decision 2 says "Backend = vertical slices **without MediatR** — 3 projects `E3a.Functions`,
  `E3a.Core`, `E3a.Core.Tests` ... FluentValidation; EF Core directly (**no repo layer**)". The actual
  solution this PR extends is five projects, MediatR 14, and a repository layer
  (`IItemVersionRepository`, `IUserRepository` added here).

This is not "docs describe unbuilt work" (never a finding) — it is the doc asserting a different
architecture than the one that exists. The section is demonstrably live rather than historical: this
same PR rewrote decision 3 in that list.

**Change — minimal.** Do not rewrite the file. Insert a heading immediately above decision 1:

`### Superseded (kept for history — see the Locked stack paragraph above for the current design)`

covering decisions 1 and 2 only, and add a one-line current-state note under each:

- under 1: `Superseded 2026-08-27: the API is E3A.Api on Azure Container Apps; E3A.Jobs is the only Functions host and carries the queue trigger.`
- under 2: `Superseded 2026-08-27: five projects (E3A.Api / E3A.Application / E3A.Domain / E3A.Infrastructure / E3A.Jobs) with MediatR 14 and a repository layer.`

Leave decisions 3 and 4 in place — decision 3 was already updated by this PR and decision 4 is current.

---

# REJECT

## RC1 — "Add an atomic claim or lease for `ProcessPublishJobHandler`" (Major)

Rejected on two independent grounds.

**Target.** RC1 is filed against `.process/publish-pipeline/01-plan.md:175`. `.process/` artifacts are
this pipeline's immutable audit trail — a frozen record of what was planned at the time. Comparable
comments were rejected on this ground on PR #2 and PR #3; this is consistent with that precedent.

**Substance.** The harm is not reproducible. Two concurrent processings of the same version require the
same message to be redelivered while the first attempt is still running — only one message is ever
enqueued per version, since `ItemVersion.Create` raises exactly one `PublishRequestedDomainEvent`. Even
then: assembly runs from the **in-memory** bytes captured during the freeze (D12,
`ProcessPublishJobHandler.cs:50` and `:68`), not from re-reading the snapshot container, so a competing
instance's `DeleteByPrefixAsync` on the snapshot prefix cannot corrupt the artefact; zipping is
deterministic, so both instances produce byte-identical output; and the zip upload uses
`overwrite: false` (`:85`). Nothing in this slice reads the snapshot container back. The proposed fix is
a persisted lease — a new table, migration and aggregate — i.e. a scope change needing plan-gate
approval, bought against no demonstrable failure.

## RC3 — "Label `03-review.md` as pre-pass-4 or update it" (Minor)

Rejected. The premise is accurate — `03-review.md:28` (N2) and `:46` (N4) do record those findings as
open while `02-implementation.md` records pass 4 fixing them — but that is precisely what a review
record is. `03-review.md` is the reviewer's output at the moment of review; `02-implementation.md`'s
pass-4 section is the record of the response. Editing the earlier artefact to reflect later work
falsifies the audit trail and destroys the sequencing the pipeline exists to capture. Same precedent as
PR #2 / PR #3.

## RC4 — "Make the metrics table complete and valid" (Minor)

Rejected. `04-metrics.md` is a run log written incrementally while stages execute; the ellipses at
stage 6 (`04-metrics.md:59`) are honest in-flight markers, and the prose between rows records the
pass-1 session-limit interruption at the point it happened. Reformatting a frozen run log for Markdown
table rendering is cosmetic churn on an audit artefact. Same precedent.

## RC5 — "Map a 404 from `DownloadContentAsync` to `null`" (Minor)

Rejected — the window is unreachable through the product's own code paths.

The TOCTOU is real in the abstract: `StorageBlobClient.cs:84` calls `ExistsAsync` and `:89` calls
`DownloadContentAsync` as two separate requests. But the only code that deletes draft blobs is
`UploadEngineerDraftHandler.cs:56`, and that handler throws
`ConflictCoreException(PublishAlreadyInProgress)` at `UploadEngineerDraftHandler.cs:39-44` whenever a
version is `Queued` or `Building` — which is exactly the interval during which `DraftSnapshotFreezer` is
downloading. No other caller deletes from the drafts container. If the window were somehow hit, the
result is an exception that bubbles to the Function and drives a queue retry against then-consistent
state: no data loss, no wrong artefact.

Weighed against that: `api/core-libraries/` is vendored, shared code, and this slice's blast radius was
approved as exactly three **additive** members with no behavioural change. Introducing exception-flow
control there to close a window nothing can open is the wrong trade.

## RC6 — "Make the per-engineer publication claim atomic" (Major)

Rejected. Both scenarios were checked against the files.

*Duplicate version allocation.* True but already contained. The worst outcome is that the second save
violates the unique index on `(ItemType, ItemId, VersionNumber)` and the caller gets a 500 instead of a
409. The constraint is the safety net and it holds — no duplicate version is ever persisted. This is the
"in-flight guard is a query not a lock" window already recorded and accepted for every other handler in
this codebase; RC6 adds nothing new to it.

*Upload replacing drafts mid-publish.* CodeRabbit's premise that `UploadEngineerDraftHandler` has a
check is correct (`:39-44`), and the window between that check and the blob replacement at `:56` is
real. But the design already answers it: `PluginTreeAssembler` filters snapshot assets against the
**frozen** manifest's target paths, and `PluginStructureValidator` fails the version with
`PluginManifestAssetMissing` when a frozen target path has no matching asset (D11, `01-plan.md:61`).
Drift produces a loud `Failed` status, not a silently wrong plugin. The residual case — identical paths,
different bytes — is the creator racing their own two actions.

The proposed remedy (a persisted per-engineer publication lease held until terminal state, coordinated
across two handlers) is a new aggregate and migration: a scope change requiring plan-gate approval, not
a review fix.

## RC7 — "Make the active-publication check and unlist transition atomic" (Major)

Rejected as the same recorded window. `UnlistEngineerHandler.cs:35` reads and `:50` saves; a publish can
insert a `Queued` version in between. The consequence is bounded and self-correcting on the next
regeneration, and closing it needs either a lifecycle-row lock or an engineer concurrency token with
retry — again a plan-gate scope change, not a triage fix. Identical in kind to RC6.

## RC9 — "Do not delay newly queued publish jobs" (Major)

**Rejected, and the proposed fix would introduce the bug it claims to prevent.**

`PublishRequestedEventHandler.cs:16` passes `visibilityTimeout` deliberately. Verified in
`api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs`: domain events are published at
line 99 and `base.SaveChangesAsync` runs at line **104** — the queue message is sent before the
`ItemVersions` row is committed. With the send made immediately visible, the worker can dequeue and hit
`ProcessPublishJobHandler.cs:21-26` before the row exists, throw `NotFoundCoreException`, and burn a
dequeue against `maxDequeueCount`. The 10-second `QueueVisibilityTimeoutSeconds`
(`api/E3A.Api/appsettings.json:25`) is the guard for exactly that race, and the plan records it as such
(`01-plan.md:159`).

CodeRabbit's substitute — configure the timeout through the worker's dequeue lease instead — does not
address this at all: a dequeue lease governs redelivery after a failed attempt, not initial visibility.
The cost being objected to is a 10-second head start on an asynchronous, status-polled operation whose
blob work runs far longer. Removing the argument turns the happy path into a guaranteed race.

## PC1 (nitpick) — "Add a queue payload round-trip test"

Rejected as already known and recorded, and as out of test scope. The gap is real —
`StorageQueueClient.cs:21` serialises with default (PascalCase) `JsonSerializer` options while the
isolated-worker binder deserialises — and it is already on the record for this slice. But the test
CodeRabbit asks for is an integration test against a live queue and a Functions host: `E3A.Tests`
references only `E3A.Application` and `E3A.Domain`, and `Core.Azure` and `E3A.Jobs` are both out of test
scope by the plan (`01-plan.md:257`, D1). Adding an integration-test harness is a pipeline-level
decision, not a PR fix.

## Audit-trail note covering the RC8 and RC10 siblings

RC8 and RC10 each also requested edits to `.process/publish-pipeline/01-plan.md` (lines 183-186 and 55).
Rejected on the audit-trail ground above. The code change in IMPLEMENT #2 stands on its own; the plan
stays as written.

## RC8's second claim — relist/unlist need a durable retry point

Rejected. If `RegenerateMarketplaceCommand` fails at `UnlistEngineerHandler.cs:51` the database is
already correct and the caller gets a 500 they can retry; the next publish, unlist or relist regenerates
from live state. No outbox or checkpoint is warranted at v0.1.

---

# DEV-DECISIONS

## DD1 — Should `marketplace.json` writes be ETag-guarded? (from RC10)

**The question.** Two different processes now regenerate the root `marketplace.json`: the Functions
worker after a publish (D2) and the API inline during unlist/relist (D21, `UnlistEngineerHandler.cs:51`).
`RegenerateMarketplaceHandler` reads a database snapshot (`:27-45`) and then unconditionally overwrites
the blob (`:56`, `overwrite: true`). If an unlist and a publish overlap, the later PUT can carry the
earlier snapshot — for example re-listing an engineer the creator just unlisted, which contradicts
acceptance decision #3 (unlist must actually stop discovery).

**Why this needs you rather than me.** Decision D5 (`01-plan.md:55`) was approved at the plan gate and
explicitly rejected any lease/ETag scheme as unnecessary, on the strength of `batchSize: 1`. That
reasoning is sound for the worker but was written before D21 added a second regeneration path in a
different process, so D5 does not actually cover this case. I am not overturning an approved decision on
my own authority, and CodeRabbit's fix (conditional write with reload-and-retry) is a heavy lift.

**My recommendation:** defer. The state is self-healing — any subsequent publish, unlist or relist
rewrites the document from live database state — and at v0.1 volume the overlap window is a few hundred
milliseconds against a handful of events per day. If you want it closed, the cheapest form is an
`If-Match` ETag on the root-marketplace upload with one reload-and-retry, which is a fourth additive
`Core.Azure` member and should be scoped into the next slice rather than bolted onto this one.

## DD2 — Do we advertise a minimum Claude Code version? (from RC13)

**The question.** CodeRabbit asserts that the `archive` marketplace source type requires Claude Code
v2.1.224 or later, and that v2.1.120 through v2.1.223 fail to install such plugins. `docs/plugin-spec.md`
(rewritten by this PR) commits e3a to archive sources.

**Why this needs you.** I could not verify the version number — it rests on a web-search result, and it
is an external product fact, not something reproducible in the repo. CodeRabbit's framing is also partly
wrong about the file: it asks to add the note to "the install instructions and release checklist" in
`docs/plugin-spec.md`, and neither section exists there. Publishing an unverified minimum version in a
public-facing spec is worse than publishing nothing.

**My recommendation:** confirm the number against the current Claude Code plugin-marketplace docs. If it
holds, add one line to `docs/plugin-spec.md` under the `marketplace.json` section — archive sources
require Claude Code vX.Y.Z or later — and carry the same statement into the install copy when the
frontend catalog slice builds it. Do not add it on CodeRabbit's word alone.
