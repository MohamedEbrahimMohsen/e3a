TRIAGE: 0 to implement, 3 rejected, 0 dev-decisions

# CodeRabbit Triage — PR #4 (`publish-pipeline`), ROUND 2 (final cycle), commit `cbcf3ef`

**Nothing to implement. The PR is mergeable as it stands.**

Both new inline comments are rejected with evidence. The third item (`PC1-r2`) is the review
object; it restates RC1-r2 and RC2-r2 verbatim and contains no independent finding — it is counted
only so every item in `05-coderabbit-comments-r2.md` is accounted for.

CodeRabbit raised **no Critical items** in round 2 (RC1-r2 is Minor, RC2-r2 is Major). No downgrade
of a CodeRabbit-labelled Critical is being made, so no veto is required from the dev.

## Independent verification re-run before triage

Run from `D:\Personal\_e3a` against the working tree at `cbcf3ef` (tree is clean; the only
untracked file is `05-coderabbit-comments-r2.md`):

- `dotnet build api/E3a.slnx --no-incremental` -> **Build succeeded. 9 Warning(s), 0 Error(s)**.
  All nine pre-existing in `core-libraries`: `Core.Validation` x2 (`RequiredValidationExtensions.cs:52,57`),
  `Core.OTP` x2 (`OTP.cs:30`), `Core.Notifications` x5 (`NotificationTemplate.cs:15` x3,
  `Notification.cs:35` x2). Matches baseline; no new warning.
- `dotnet test api/E3A.Tests/E3A.Tests.csproj` -> **Passed! Failed: 0, Passed: 354, Skipped: 0,
  Total: 354.** Matches baseline.

## Invariants re-checked against the current file, not against the reports

- **`SaveChangesAsync` at most twice on every path (D16).** `ProcessPublishJobHandler.cs` has exactly
  three call sites: the `Building` checkpoint (`:45`), the terminal write (`:98`) and `FailAsync`
  (`:105`). Every `FailAsync` call site returns immediately (`:38`, `:55`, `:63`, `:74`), so no path
  reaches three.
- **No `try`/`catch` in `ProcessPublishJobHandler`.** Confirmed by reading the file end to end
  (`:19-106`).
- **Pinned marketplace strictly before any save.** `:88-89` in-memory marks -> `:91` generate ->
  `:94` upload pinned -> `:96-98` `Update` + `SaveChangesAsync`. Intact.
- **The three retry tests still bite.** `ProcessPublishJobHandlerRetryTests.cs:48,59,73` are present
  and unchanged; `:65-69` is a real `Received.InOrder` sequence assertion and `:81` is
  `Received(1).SaveChangesAsync` on the throwing path, not a substitute echoing its own stub.

---

# REJECT

## RC1-r2 — "Make the `.process/` status statement precise" (Minor)

**Target:** `.process/publish-pipeline/07-coderabbit-rework.md:61-63`.

**The factual premise is correct; the remedy is the violation.** I confirmed it rather than assuming
it: `git show --stat cbcf3ef` lists `.process/publish-pipeline/04-metrics.md | 54 +`, and
`git diff bf47eff cbcf3ef -- .process/publish-pipeline/04-metrics.md` shows 54 appended lines
(orchestrator run-log rows 10-16 plus two narrative sections). So the sentence "No `.process/`
artefact was edited (this file is new)" is inaccurate as written for the commit it ships in.

**Rejected anyway, on the settled ground.** `.process/` artefacts are this pipeline's immutable audit
trail — a frozen record of what each stage said at the time it said it. Comparable comments were
rejected on this ground on PR #2, PR #3, and three times in round 1 of this PR (RC1, RC3, RC4, plus
the RC8/RC10 plan-edit siblings). Retro-editing a stage report so it reads correctly in hindsight is
exactly the falsification the trail exists to prevent.

**And the correction already exists, in the right place.** The round-1 verifier caught this
independently and recorded it at `.process/publish-pipeline/08-coderabbit-verify.md:201-208`,
including the reason it is not a triage violation (the appended stage-15 row carries in-flight
placeholders the implementer could not have written, so it is the orchestrator's append, and
appending is that artefact's purpose). CodeRabbit cites that very passage in its own comment.
**RC1-r2 therefore adds nothing beyond what the trail already says** — it asks to move a correction
from the artefact that owns it (the later verification) into the artefact it corrects (the earlier
report), which is the wrong direction for an audit record.

No change.

## RC2-r2 — "Regenerate the root marketplace after publication" (Major)

**Target:** `api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:98`.

**Rejected: both load-bearing claims are false against the tree.** I read this one as a possible
regression from the RC8 fix, which is what it would have to be to matter, and it is not one — the
RC8 reorder touched only lines `88-98` inside the handler and did not move, remove or reorder
anything to do with the root document.

**Claim 1 — "It does not invoke `RegenerateMarketplaceCommand` ... The new version will not appear in
the root `marketplace.json`." False.**

`api/E3A.Jobs/Functions/ProcessPublishJobFunction.cs:19-20`:

    await mediator.Send(new ProcessPublishJobCommand(publishRequested.VersionId), cancellationToken).ConfigureAwait(false);
    await mediator.Send(new RegenerateMarketplaceCommand(), cancellationToken).ConfigureAwait(false);

The root regeneration runs on every dequeue, immediately after the publish handler returns. This is
approved plan decision **D2** (`01-plan.md:52`) verbatim: *"The Function sends
`ProcessPublishJobCommand` then, unconditionally, `RegenerateMarketplaceCommand`. Two `Send` calls,
no branch in the Function."* `RegenerateMarketplaceHandler.cs:27` selects
`Status == EngineerStatus.Published && LatestVersionId != null` and `:43` filters versions to
`ItemVersionStatus.Published`, both from live database state — so once `:98` commits, the very next
statement rebuilds the root document *including* the new version.

CodeRabbit did not see this because `ProcessPublishJobFunction.cs` is not in its round-2 file set
(`05-coderabbit-comments-r2.md:333-343` lists the 9 files changed between `bf47eff` and `cbcf3ef`);
it reviewed the handler without its only caller.

**Claim 2 — "A queue retry after this save returns at Lines 28-31 and cannot repair a failed or
skipped root update." False, and it is the inverse of how the code behaves.**

Lines 28-31 return from the **handler**, not from the Function. If `RegenerateMarketplaceCommand`
throws at `ProcessPublishJobFunction.cs:20`, the exception leaves the Function (no `try`/`catch`
anywhere on the path), the message is redelivered, and on the next attempt line `:19` no-ops through
the terminal-status guard while line `:20` **runs the regeneration again**. The early return is what
makes the retry cheap, not what blocks it. That is precisely the "durable retryable follow-up"
RC2-r2 asks to be added — it already exists, it is stateless, and it re-derives from committed
database state rather than from a persisted action row that could itself go stale. It gets
`maxDequeueCount` (5) total attempts, per `api/E3A.Jobs/host.json`.

**The remedy would break three things the slice is required to hold.**

1. "Persist a retryable regeneration action with the publication state" is a new table, aggregate and
   migration — a plan-gate scope change, identical in kind to RC1/RC6/RC7 which were rejected on that
   ground in round 1.
2. Writing that action alongside the version and engineer, or invoking regeneration inside the
   handler, adds a third write/save to the publish path and puts D16 (`01-plan.md:66`, at most two
   `SaveChangesAsync` on every path) at risk.
3. Making a regeneration failure non-fatal inside the handler would require a `try`/`catch` in
   `ProcessPublishJobHandler` — prohibited outright.

**The residual is real, bounded, and already on the record.** If regeneration fails all five
attempts the message poisons and the root document stays stale until the next publish, unlist or
relist rewrites it from live state. That exact trade was triaged and accepted in round 1 ("RC8's
second claim — relist/unlist need a durable retry point — Rejected ... No outbox or checkpoint is
warranted at v0.1", `06-coderabbit-triage.md:261-265`). RC2-r2 brings no new evidence, so
re-litigating it in the final cycle would be churn, not diligence.

**The requested test.** "Add an integration test that verifies the root document includes the
published version" needs a Functions host and a live blob endpoint. `E3A.Tests` references only
`E3A.Application` and `E3A.Domain` (`E3A.Tests.csproj:17-18`); `E3A.Jobs` and `Core.Azure` are both
out of test scope by the plan (D1, `01-plan.md:51`, and `01-plan.md:256`). Same ground as PC1 in
round 1. The intent is nevertheless already covered at the level the plan allows:
`RegenerateMarketplaceHandlerTests.cs:43` (`Handle_ShouldWriteMarketplaceWithEveryPublishedEngineer_WhenCalled`)
captures the uploaded stream (`:37-39`) and asserts on the actual JSON written to
`PublishBlobPaths.RootMarketplaceBlobName` (`:50`), with siblings at `:54`, `:68`, `:78`, `:91` and
`:102`. Those are real content assertions, not substitute echoes.

**Docs check.** No divergence is created by rejecting this. `docs/architecture.md:31-38` ends the
publish-pipeline sequence with "-> regenerate the root `marketplace.json`", which is what the code
does. Nothing to update.

No change.

## PC1-r2 — review object (COMMENTED)

Carries no finding of its own: its body is the two inline comments above, repeated verbatim
(`05-coderabbit-comments-r2.md:278-293`). Rejected with them.

---

# DEV-DECISIONS

**None new.** DD1 (ETag-guarding `marketplace.json` writes now that D21 added a second regeneration
path in the API process) and DD2 (whether to publish a minimum Claude Code version for `archive`
sources) remain open from round 1. Nothing in round 2 touches, decides or forecloses either, and
nothing in round 2 warrants opening a third — RC2-r2's subject matter was already settled at the
plan gate by D2 and again in round-1 triage.

---

# Conclusion

Round 2 produced **no valid finding**. This is cycle 2 of 2, the cap, so stage 4 is closed.

- RC1-r2 — correct fact, wrong remedy: it asks to rewrite a frozen audit artefact, and the
  correction it wants already exists at `08-coderabbit-verify.md:201-208`.
- RC2-r2 — a false premise caused by reviewing `ProcessPublishJobHandler.cs` without
  `ProcessPublishJobFunction.cs`. The root marketplace **is** regenerated after every publish
  (`ProcessPublishJobFunction.cs:20`), and a queue retry **does** repair a failed regeneration.
- No regression was introduced by the RC8 fix. The reorder is confined to
  `ProcessPublishJobHandler.cs:88-98`; the save count, the absence of `try`/`catch`, the
  artefact-before-save ordering and the three failing-under-the-old-order tests all still hold, and
  the suite is green at 354/354 with 0 errors and the 9 baseline warnings.

**PR #4 is mergeable as it stands. No rework commit is required for round 2.**
