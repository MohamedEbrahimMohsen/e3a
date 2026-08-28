# CodeRabbit rework — `publish-pipeline` (triage `06-coderabbit-triage.md`, on `bf47eff`)

## 1. Findings addressed

| # | Triage item | What I changed | Where |
|---|---|---|---|
| 1 | RC2 — prefix query treated as exact-existence proof | `existingZips.Count == 0` → `!existingZips.Exists(name => string.Equals(name, zipBlobPath, StringComparison.Ordinal))`, exactly as decision D4 states | `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:82` |
| 1 | RC2 — test | New `Handle_ShouldUploadZip_WhenOnlyAPrefixMatchingBlobExists`: `ListByPrefixAsync` returns `["z/e3a-dive-backend-engineer/1.0.0.zip.bak"]`, asserts `Received(1)` on the 9-arg `UploadAsync` for the exact zip path | `api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerRetryTests.cs:48` |
| 2 | RC8 — pinned marketplace written after `Published` was persisted | Reordered the tail: `MarkPublished` (in-memory) → generate + upload the pinned `m/{plugin}/{version}/marketplace.json` → `Update` + `SaveChangesAsync`. Two saves at most on every path (D16 intact); no `try`/`catch` added | `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:88-98` |
| 2 | RC8 — ordering tests | `Handle_ShouldWritePinnedMarketplaceBeforeSaving_WhenPublishing` (`Received.InOrder`: pinned upload, then save) and `Handle_ShouldNotPersistPublished_WhenPinnedMarketplaceUploadFails` (pinned upload throws → handler throws, exactly one save: the `Building` checkpoint) | `ProcessPublishJobHandlerRetryTests.cs:59` and `:73` |
| 2 | RC8 — docs-sync of the reordered sequence | Publish-pipeline sequence lines now read `… upload zip → pinned marketplace → persist Published + LatestVersionId → regenerate root`, plus one paragraph in `architecture.md` recording why artefacts precede the status write | `docs/architecture.md:35-42`, `docs/implementation-plan.md:65` |
| 3 | RC11 — `maxDequeueCount` off-by-one | Both occurrences now read ``Poison queue after `maxDequeueCount` (5) total attempts, including the first.`` | `docs/architecture.md:44`, `docs/implementation-plan.md:65` |
| 4 | RC12 — superseded architecture decisions | `### Superseded (kept for history — see the Locked stack paragraph above for the current design)` above decision 1, a one-line `Superseded 2026-08-27:` note under decisions 1 and 2, and `### Current` above decision 3 so the superseded scope closes at 2. Nothing deleted | `docs/implementation-plan.md:15-22` |

## 2. Files created

| Path | Lines | Purpose |
|---|---|---|
| `api/E3A.Tests/Publishing/ProcessPublishJob/ProcessPublishJobHandlerRetryTests.cs` | 90 | The three retry-safety tests for findings 1 and 2 |

## 3. Files modified

| Path | Change |
|---|---|
| `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs` | Exact zip-name check; pinned marketplace uploaded before the `Published` save |
| `docs/architecture.md` | Pipeline sequence reordered + retry-safety paragraph; `maxDequeueCount` wording |
| `docs/implementation-plan.md` | Pipeline sequence reordered; `maxDequeueCount` wording; Superseded/Current headings over decisions 1–2 |

No Postman change: no endpoint or contract was added, altered or removed.

## 4. How the ordering is pinned (would it catch a revert?)

Verified empirically, not by inspection. I reverted both production changes in place, ran
`dotnet test --filter FullyQualifiedName~ProcessPublishJobHandlerRetryTests`, and got
**3 failed, 0 passed**:

- `Handle_ShouldNotPersistPublished_WhenPinnedMarketplaceUploadFails` — with the old order the
  `Published` save happens before the throwing pinned upload, so `SaveChangesAsync` is received
  twice and the `Received(1)` assertion fails (`ReceivedCallsException: Expected to receive exactly
  1 call matching`). This is the one that encodes the actual bug: it proves nothing durable records
  `Published` until the pinned artefact exists.
- `Handle_ShouldWritePinnedMarketplaceBeforeSaving_WhenPublishing` — starts from a `Building`
  version so there is exactly one `SaveChangesAsync` in the run, which makes `Received.InOrder`
  unambiguous; the old order produces `[save, pinned upload]` and fails with
  `SequenceInOrderAssertion: Expected to receive these calls in order`.
- `Handle_ShouldUploadZip_WhenOnlyAPrefixMatchingBlobExists` — under `Count == 0` the `.bak` sibling
  suppresses the zip upload, so the expected `UploadAsync` never happens
  (`Received 1 non-matching call` — only the pinned upload).

Restored the fixes; full suite green again.

## 5. Deviations

| Triage said | Reality | What I did |
|---|---|---|
| "Add one sibling test" (item 1) and "Add: `Handle_ShouldNotPersistPublished_…`" (item 2), implying `ProcessPublishJobHandlerTests` | That file is already 102 lines; appending two tests would take it to ~125, against the ≤100-line rule in the skill and `conventions/dotnet-testing.md` §9 | Put both prescribed tests (names verbatim) in a new sibling class `ProcessPublishJobHandlerRetryTests` (90 lines), following the existing `ProcessPublishJobHandlerFailureTests` split precedent in the same folder. `ProcessPublishJobHandlerTests` is untouched |
| Item 2 prescribes one new test | The rework brief additionally required the ordering itself to be asserted ("a test that would fail if someone reorders it back") | Added a third test, `Handle_ShouldWritePinnedMarketplaceBeforeSaving_WhenPublishing`, using `Received.InOrder`. The failure test alone would catch a revert; this one names the invariant explicitly and fails with a sequence diff rather than a call-count diff |
| Item 4 says insert the heading above decision 1, covering decisions 1–2 only | A single `###` heading with nothing after decision 2 would visually pull decisions 3–4 under "Superseded" too | Also added `### Current` immediately above decision 3. Decisions 3 and 4 are otherwise untouched |
| Items 1–2 are described as code-only | Reordering the pipeline changed the sequence that `docs/architecture.md` and `docs/implementation-plan.md` both spell out step by step — a docs-sync divergence under `.claude/rules/docs-sync.md` (architecture owns the pipeline sequence) | Updated both sequence sentences and added one explanatory paragraph to `architecture.md`. Not a scope expansion: the same two sentences were already being edited for item 3 |

Everything in REJECT stayed rejected. No `.process/` artefact was edited (this file is new). No file
under `api/core-libraries/` was touched. No lease/ETag scheme added. `05-coderabbit-comments.md` was
not re-read for additional work.

## 6. Build & test

Run from `D:\Personal\_e3a` against the final tree:

- `dotnet build api/E3a.slnx --no-incremental` → `Build succeeded. 9 Warning(s), 0 Error(s)` — the
  same 9 pre-existing `core-libraries` warnings as the baseline (`Core.Validation` ×2, `Core.OTP` ×2,
  `Core.Notifications` ×5). No new warning.
- `dotnet test api/E3A.Tests/E3A.Tests.csproj` → `Passed! - Failed: 0, Passed: 354, Skipped: 0,
  Total: 354`. Baseline 351 + the 3 new tests.

## 7. Notes for review

- `Handle_ShouldSkipZipUpload_WhenBlobAlreadyExists` still stubs an exact match and still passes, so
  the "same zip, second attempt" retry path is unchanged.
- `MarkPublished` is still called before the pinned document is generated, because `GeneratePlugin`
  reads `ZipBlobPath` / `ZipSha256` off the version. Only the **persistence** moved; the generated
  JSON is byte-identical to before, which is why `Handle_ShouldWritePinnedMarketplace_WhenPublishing`
  needed no change.
- The failure test uses `NSubstitute.ExceptionExtensions.ThrowsAsync`; this is the first use of that
  namespace in `E3A.Tests` (it ships in the existing NSubstitute package, no new reference).
- On the failure path the in-memory `version` is left `Published` while the database keeps `Building`.
  That instance is discarded with the scope when the exception reaches the Function, and the retry
  re-reads from the database, so it is not observable — but it is the reason the failure test asserts
  on the save count rather than on `version.Status`.
- Root `marketplace.json` regeneration still runs in `ProcessPublishJobFunction` after the handler
  returns, unchanged by this rework.
- DD1 and DD2 remain open dev-decisions; nothing here forecloses either.
