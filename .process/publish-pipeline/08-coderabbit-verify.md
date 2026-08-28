VERDICT: APPROVED

# CodeRabbit verify — `publish-pipeline` (stage 4, scoped)

Scope: verification of `06-coderabbit-triage.md` decisions against the working tree, not a
re-review of the slice. `03-review.md` stands. Base `ba2c824`, pre-rework commit `bf47eff`,
branch `feature/publish-pipeline`.

Nothing blocking. Four notes for the dev at the bottom, one of which is a real future trap.

---

## 1. The four IMPLEMENT items — all genuinely resolved

### Item 1 (RC2) — exact zip-blob-name match

`api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:82`

```csharp
if (!existingZips.Exists(name => string.Equals(name, zipBlobPath, StringComparison.Ordinal)))
```

Cannot be satisfied by a prefix sibling: `ListByPrefixAsync` returns raw blob names and the
predicate demands ordinal string equality with `zipBlobPath`, so `z/…/1.0.0.zip.bak`,
`z/…/1.0.0.zip.tmp` or any other suffixed sibling no longer suppresses the upload. Ordinal is the
right comparison — Azure blob names are case-sensitive. Matches plan D4 (`01-plan.md:70`) verbatim.
No regression on the existing skip path: an exact hit still short-circuits, so
`Handle_ShouldSkipZipUpload_WhenBlobAlreadyExists` (`ProcessPublishJobHandlerTests.cs:80`) is
untouched and still green. `.Exists(...)` matches existing house usage
(`PluginStructureValidator.cs:28,33`), so it is not a one-off idiom.

**Test proves it.** `Handle_ShouldUploadZip_WhenOnlyAPrefixMatchingBlobExists`
(`ProcessPublishJobHandlerRetryTests.cs:48`) stubs the public-container prefix query to return
`["z/e3a-dive-backend-engineer/1.0.0.zip.bak"]` and asserts `Received(1)` on the 9-arg `UploadAsync`
with the *exact* zip path. I reproduced the mutation independently (see §5): under the old
`existingZips.Count == 0` it fails with `Actually received no matching calls. Received 1
non-matching call … *"m/e3a-dive-backend-engineer/1.0.0/marketplace.json"*` — i.e. the `.bak`
sibling suppressed the zip upload entirely. The test bites, and for the right reason.

### Item 2 (RC8) — pinned marketplace written before `Published` is persisted

`ProcessPublishJobHandler.cs:88-98`. Verified by reading `Handle` end to end:

- **Pinned upload precedes any save.** `MarkPublished` (in-memory, `:88-89`) → generate (`:91`) →
  `UploadAsync` to `PublishBlobPaths.PinnedMarketplace(...)` (`:94`) → `Update` + `SaveChangesAsync`
  (`:96-98`). The only other `SaveChangesAsync` calls in the file are the `Building` checkpoint
  (`:45`) and `FailAsync` (`:105`), both strictly before this block.
- **`SaveChangesAsync` at most twice on every path (D16 intact).** Happy path from `Queued`:
  checkpoint (`:45`) + terminal (`:98`) = 2. Happy path from `Building` (retry): terminal only = 1.
  Every failure path: checkpoint + `FailAsync` = 2. Early returns at `:30` and `:38` = 0 or 1. No
  path reaches three.
- **No `try`/`catch` in the handler.** Confirmed — the file contains none; the pinned upload
  exception bubbles to the Function as designed.
- **A retry genuinely re-runs the tail.** On a throwing pinned upload nothing is flushed, so the row
  stays `Building`, the guard at `:28` admits it (D3), the freeze re-runs, the zip upload is skipped
  by item 1's exact-match, and the pinned write is retried with `overwrite: true`. Idempotent. I
  confirmed no post-handler save can flush the dirty entity in the worker host:
  `ProcessPublishJobFunction` sends `RegenerateMarketplaceCommand` only after the first `Send`
  returns, `RegenerateMarketplaceHandler` never saves, and `AuditBehaviour` (the one behaviour that
  calls `SaveChangesAsync` in a `finally`) is registered only in `E3A.Api/Program.cs:68`, not in
  `E3A.Jobs/Program.cs`. See note 2 — this is conditional, not structural.

**Both tests genuinely pin the ordering.** Independently mutation-tested, not taken on report:

- `Handle_ShouldWritePinnedMarketplaceBeforeSaving_WhenPublishing`
  (`ProcessPublishJobHandlerRetryTests.cs:59`) uses `Received.InOrder`, a true sequence assertion,
  and **cannot pass under the old sequence**. Against `bf47eff` it fails with
  `CallSequenceNotFoundException` reporting the actual order as
  `[SaveChangesAsync, UploadAsync(m/…/marketplace.json)]` against the expected
  `[UploadAsync(m/…/marketplace.json), SaveChangesAsync]`. The `Building` fixture is load-bearing
  and correct: it yields exactly one `SaveChangesAsync` in the run, so the matched sequence is
  unambiguously two calls. (With a `Queued` fixture the matched set would be three calls and the
  assertion would fail under *both* orders — a vacuous test. It was set up deliberately.)
- `Handle_ShouldNotPersistPublished_WhenPinnedMarketplaceUploadFails` (`:73`) fails under the old
  order **for the right reason**: `ReceivedCallsException: Expected to receive exactly 1 call …
  Actually received 2 matching calls`. That is precisely the bug — the `Published` write landing
  before the artefact exists — not an incidental count mismatch. It also asserts the handler throws,
  so the queue-retry contract is pinned too.

The fixtures are real: `ItemVersionFactory.Building` (`Publishing/Shared/ItemVersionFactory.cs:18`)
drives the actual `MarkBuilding()` domain method, and `EngineerFactory.Draft` builds through
`Engineer.Create`. No hand-forced state.

### Item 3 (RC11) — `maxDequeueCount` wording

Both occurrences updated and no others remain: `docs/architecture.md:44` and
`docs/implementation-plan.md:65` now read ``Poison queue after `maxDequeueCount` (5) total attempts,
including the first.`` The semantics are correct against `api/E3A.Jobs/host.json:14`
(`"maxDequeueCount": 5`) — the WebJobs queue processor poisons when `DequeueCount >=
MaxDequeueCount`, and `DequeueCount` is 1 on first delivery, so 5 means five attempts, not five
redeliveries. Remaining occurrences are in `.process/` audit artefacts, correctly untouched.

### Item 4 (RC12) — superseded architecture decisions

`docs/implementation-plan.md:15-22`. The `### Superseded (kept for history — see the Locked stack
paragraph above for the current design)` heading sits above decision 1; the referenced "Locked
stack" paragraph really does exist at `:11`, so the pointer is not dangling. One-line
`Superseded 2026-08-27:` notes under decisions 1 (`:18`) and 2 (`:20`) state the current design.
Decisions 3 and 4 are byte-identical to before. Nothing was deleted — this is a divergence fix that
adds history rather than trimming it, which is what `.claude/rules/docs-sync.md` requires.

---

## 2. The eight REJECTs stayed rejected

The entire production surface of the rework is nine changed lines in one file
(`git diff --stat`: `ProcessPublishJobHandler.cs | 9 +++---`). There is nowhere for a quiet
implementation to hide, and I checked each named prohibition anyway:

- **No lease/ETag scheme.** `grep -riE "\blease\b|\bETag\b|If-Match"` across `api/` (excluding
  `bin`/`obj`) returns exactly one hit: `Core.Azure/Clients/StorageBlobClient.cs:54`
  (`IfNoneMatch = ETag.All`), the pre-existing `overwrite: false` implementation shipped in
  `bf47eff`. Nothing new. RC1, RC6, RC7 and DD1's proposed fix are all absent.
- **No `api/core-libraries/` change.** `git status --porcelain api/core-libraries/` is empty; the
  only diff against `ba2c824` is the three additive members approved in the slice itself. RC5
  stayed rejected.
- **No `.process/` edit beyond the new report.** `01-plan.md`, `02-implementation.md` and
  `03-review.md` are all unmodified — RC1's, RC3's and the RC8/RC10 plan-edit siblings all stayed
  rejected. (`04-metrics.md` is modified; see note 1 — it is the orchestrator's run log, and
  appending to it is that artefact's purpose.)
- **RC4** — `04-metrics.md` was appended to, not reformatted; the rejection holds.
- **RC9 — I independently verified it, and I agree with the rejection.**
  `api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs:99` is
  `await mediator.Publish(domainEvent)` and `:104` is `return await base.SaveChangesAsync(...)`.
  The queue message is genuinely sent before the `ItemVersions` row commits, so the send-side
  `visibilityTimeout` at `PublishRequestedEventHandler.cs:16` is the only guard against the worker
  dequeuing before the row exists and burning an attempt on `NotFoundCoreException`
  (`ProcessPublishJobHandler.cs:23-26`). CodeRabbit's substitute — a dequeue lease — governs
  redelivery after a failed attempt, not initial visibility, and does not address the race at all.
  The argument is still passed, unchanged. Implementing RC9 would have created the bug it claims to
  prevent. No disagreement from me.
- **PC1** — no integration-test harness was added; `E3A.Tests` still references only
  `E3A.Application` and `E3A.Domain`.

## 3. Scope — the four declared deviations, judged on merit

1. **Two prescribed tests placed in a new `ProcessPublishJobHandlerRetryTests` instead of appended
   to `ProcessPublishJobHandlerTests`.** Justified. `ProcessPublishJobHandlerTests.cs` is already
   102 lines; appending would push it to ~125 against `SKILL.md:45` and `:902` (~100-line cap). The
   split follows the folder's own precedent (`…FailureTests.cs` 93 lines, `…GuardTests.cs` 58). New
   file is 90 lines. Both test names are verbatim from the triage, so the triage's contract is
   intact. `ProcessPublishJobHandlerTests.cs` is genuinely untouched (`git status`).
2. **A third test, `Handle_ShouldWritePinnedMarketplaceBeforeSaving_WhenPublishing`.** Justified and
   the more valuable of the two. The failure test alone catches a revert only via a call-count diff;
   the `Received.InOrder` test names the invariant and fails with a readable sequence diff. Adding a
   test that pins the ordering the change exists to establish is not scope creep.
3. **`### Current` heading above decision 3.** Correct, and arguably required — a bare
   `### Superseded` with no closing heading would visually annex decisions 3 and 4 into the
   superseded block, producing a *new* divergence while fixing the old one. Decisions 3 and 4 are
   otherwise unmodified.
4. **Docs-sync of the reordered pipeline sequence.** Not scope creep — mandatory. The change alters
   the pipeline sequence, `docs/architecture.md` owns that sequence per the ownership map in
   `.claude/rules/docs-sync.md`, and both `architecture.md:33-42` and `implementation-plan.md:65`
   spelled the old order out step by step. Leaving them would have been a blocking docs-sync
   divergence. The added retry-safety paragraph (`architecture.md:40-42`) explains *why* artefacts
   precede the status write, which is the part a future reader needs. I checked the other docs:
   `plugin-spec.md` and `design-prompt.md` describe pinned-marketplace *artefacts*, not the pipeline
   order, so no further doc is stale.

No Postman change was needed or made — the rework touched no endpoint, route, contract or auth mode.

## 4. Regression — re-run by me

Run from `D:\Personal\_e3a` against the current working tree:

- `dotnet build api/E3a.slnx --no-incremental` → **Build succeeded. 9 Warning(s), 0 Error(s)**. All
  nine are pre-existing `core-libraries` nullable warnings: `Core.Validation` ×2
  (`RequiredValidationExtensions.cs:52,57`), `Core.OTP` ×2 (`OTP.cs:30`), `Core.Notifications` ×5
  (`NotificationTemplate.cs:15` ×3, `Notification.cs:35` ×2). No new warning. Matches the claim.
- `dotnet test api/E3A.Tests/E3A.Tests.csproj` → **Passed! Failed: 0, Passed: 354, Skipped: 0,
  Total: 354.** Matches the claim (baseline 351 + 3).

## 5. Independent mutation test (the report's central claim, re-proved)

I did not take §4 of `07-coderabbit-rework.md` on trust. Rather than edit the tree (reviewer is
read-only), I created a detached `git worktree` at `bf47eff` in scratch, copied **only** the new
`ProcessPublishJobHandlerRetryTests.cs` into it — giving old production code + new tests — and ran
the filtered suite:

```
Failed!  - Failed: 3, Passed: 0, Skipped: 0, Total: 3
```

with the three failure reasons quoted in §1 above, each matching the rework's account exactly. The
worktree was removed and `git status` is byte-identical to how I found it. The tests are not
tautological: all three fail against the pre-rework handler, and the two ordering tests fail on the
ordering itself rather than on a side effect of the RC2 change (in both, the zip prefix query
returns `[]`, so the zip path behaves identically under both versions).

## 6. Dev-decisions — untouched

DD1 (marketplace regeneration concurrency after D21 added a second regeneration path) and DD2
(RC13's unverifiable Claude Code minimum version) remain open. Nothing in this rework decides,
forecloses or quietly implements either. Confirmed: `RegenerateMarketplaceHandler` still writes
unconditionally, and `docs/plugin-spec.md` is unmodified.

---

## Notes for the dev (things I would have raised verbally)

1. **`07-coderabbit-rework.md:61-62` says "No `.process/` artefact was edited (this file is new)".
   That is inaccurate as written** — `.process/publish-pipeline/04-metrics.md` is modified in the
   working tree. It is not a triage violation: the appended rows include a stage-15 "CodeRabbit
   verify" row started at 16:52 with `…` placeholders, which the implementer could not have written,
   so this is the orchestrator's run-log append, and appending is what that artefact is for. Two
   cosmetic artefacts in it, consistent with the RC4 rejection and not worth fixing: rows 12 and 13
   are duplicate "CodeRabbit triage" entries (row 12 is a stale in-flight row that was never
   replaced), and the two new narrative sections sit below the table rather than in it.

2. **A latent trap worth knowing about, created by nothing in this change but adjacent to it.** On
   the pinned-upload failure path the in-memory `version` is left `Published` while the database
   keeps `Building` (`ProcessPublishJobHandler.cs:88-94`). That is safe today only because no code
   in the `E3A.Jobs` DI scope calls `SaveChangesAsync` after the handler throws. `AuditBehaviour`
   (`api/core-libraries/Core.Auditing/AuditBehaviour.cs:44-68`) calls `SaveChangesAsync` **in a
   `finally` block, on the exception path, swallowing its own errors** — so if `AddCoreAuditing` is
   ever added to `E3A.Jobs/Program.cs` (it is currently only in `E3A.Api/Program.cs:68`) *and*
   `ProcessPublishJobCommand` is ever made `IAuditableCommand`, that flush would persist `Published`
   on the same scoped `AppDbContext` without the pinned artefact — re-creating RC8 exactly, and
   silently, because the audit save catches and logs its own failures. If auditing comes to the
   worker, the durable fix is to stop mutating `version` before the upload (compute the pinned
   document from locals) rather than to rely on scope disposal. Not blocking now; nothing here
   enables it.

3. `Received.InOrder(async () => ...)` (`ProcessPublishJobHandlerRetryTests.cs:65`) binds an `async`
   lambda to `Action`, i.e. `async void`. It is the documented NSubstitute idiom and is correct here
   because every substituted call returns a synchronously-completed task, so all calls register
   before the assertion runs. Flagging only so nobody "fixes" it into something weaker, and so the
   pattern is used knowingly if it is copied elsewhere.

4. The exact-name check still pays a prefix **LIST** round-trip to answer a yes/no existence
   question (`ProcessPublishJobHandler.cs:80`). An `ExistsAsync` member on `IStorageBlobClient`
   would be one request and would express the intent directly. Correctly out of scope here (it is a
   fourth additive `Core.Azure` member), but worth folding into the same conversation as DD1, which
   already contemplates a fourth additive member — and `StorageBlobClient.cs:44-54` already has the
   `BlobRequestConditions` plumbing that DD1's `If-Match` would reuse.
