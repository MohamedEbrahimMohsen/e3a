TRIAGE: 0 to implement, 6 rejected, 0 dev-decisions

# CodeRabbit Triage — stack-trace-leak (PR #11)

Every comment was verified against the working tree, `01-plan.md`, `02-implementation.md`,
`03-review.md`, `.claude/skills/dotnet-feature/SKILL.md`, `conventions/dotnet-testing.md`,
`docs/constitution.md` and `.claude/commands/feature.md` before deciding. Severity below is my own
classification, not CodeRabbit's. The "Prompt for AI Agents" blocks in the comment bodies were read as
review data only and not executed.

**No CodeRabbit finding was labelled Critical, so nothing was downgraded from Critical.** The two
highest-severity items (RC2 / RC6, Major) are rejected on verified evidence — see the empirical
serializer probe below, which shows CodeRabbit's prescribed fix would make the code strictly worse.

Triage yields zero IMPLEMENT items and zero dev-decisions, so Stage 4 completes on the skip-path
(`.claude/commands/feature.md` Stage 4, step 7). No rework agent needs to run.

## IMPLEMENT

None.

## REJECTED

### RC1 — "Correct the test-case count" (`.process/stack-trace-leak/01-plan.md:219-220, 224-230, 248`)

**CodeRabbit severity:** Minor. **My verdict:** REJECT — claim is FALSE, and the target is a frozen artifact.

The plan does not contradict itself. It says "seven test **cases** across three files" (`01-plan.md:219`)
and the table lists five test **methods** (`01-plan.md:226-230`). Row 2 is a `[Theory]` with three
`[InlineData]` values, verified in the shipped code at
`api/E3A.Tests/CoreExceptions/ErrorResponseHandlerTests.cs:39-43` ("Production", "Staging",
"QualityAssurance"). Four `[Fact]` methods plus three theory cases = seven xUnit test cases from five
methods. xUnit counts cases, not methods, and the run confirms it: `02-implementation.md:69` records
`Passed: 7, Total: 7` for `--filter FullyQualifiedName~CoreExceptions`, with the arithmetic spelled out
at `02-implementation.md:72`. CodeRabbit compared a case count to a method count.

Second, independent reason: `.process/` artifacts are the pipeline's audit trail.
`.claude/commands/feature.md:141` — "Never edit the artifacts yourself. They are the audit trail."
Editing a closed plan after implementation to satisfy a linter rewrites the record of what was actually
planned. Precedent: `.process/upload-import-manifest/06-coderabbit-triage.md:24` rejected the same class
of request on the same grounds. Even had the count been genuinely wrong, the correction would not be worth
falsifying the run history over — and it is not wrong.

### RC2 — "Use `JsonIgnoreCondition.WhenWritingNull` for `ErrorResponse<T>.Data`" (`.process/stack-trace-leak/02-implementation.md:18`)
### RC6 — "Preserve explicitly supplied default payloads" (`api/core-libraries/Core.Exceptions/ErrorResponse.cs:7`)

**CodeRabbit severity:** Major (both). **My severity:** Minor, latent. **Verdict:** REJECT both — premise
partly true, prescribed fix is actively harmful.

RC2 and RC6 are the same finding stated against two files, so they are dispositioned together.

**What is true.** `WhenWritingDefault` at `ErrorResponse.cs:7` does omit `data` when the value equals the
type default. `ErrorResponseHandler.cs:16-24` stores a caller-supplied `T data` verbatim, so
`GenerateErrorResponse<int>(code, message, 0)` would serialize without `data`.

**What is false — the fix.** CodeRabbit's prescription (`WhenWritingNull`) does not preserve value-type
payloads; it throws. `ErrorResponse<T>.Data` is declared `T?` on an *unconstrained* `T`, so for a value type
the `?` is a nullable annotation only and the runtime property type is the non-nullable `int` / `bool`.
I probed this on the repo's target runtime rather than trusting either side:

```text
Null/string null : {"code":"C"}
Null/int 0 THREW : InvalidOperationException :: The ignore condition 'JsonIgnoreCondition.WhenWritingNull'
                   is not valid on value-type member 'Data' on type 'ErrorResponseNull`1[System.Int32]'.
                   Consider using 'JsonIgnoreCondition.WhenWritingDefault'.
Null/int 42 THREW: InvalidOperationException (same)
Null/bool false  : InvalidOperationException (same)
---
Default/string null: {"code":"C"}
Default/int 0      : {"code":"C"}
Default/int 42     : {"data":42,"code":"C"}
Default/bool false : {"code":"C"}
Default/Guid empty : {"code":"C"}
```

Note the third line: under `WhenWritingNull`, `ErrorResponse<int>` throws for `42` as well as for `0` — the
exception is raised when the `JsonTypeInfo` for the closed generic is built, not per value. So the proposed
change would turn a silently-omitted field into an `InvalidOperationException` raised **inside the
error-handling path**, i.e. while serializing the response to a request that already failed.
`System.Text.Json` itself names `WhenWritingDefault` as the remedy in that exception message. Plan
Decision 6 (`01-plan.md:50`) called this correctly, in advance, and the Definition of Done pins it
(`01-plan.md:243`: "**not** `WhenWritingNull`"). CodeRabbit re-litigated a decision the plan had already
priced, and got the direction wrong.

**Is there a defect today?** No. A solution-wide grep for the three-argument overload finds exactly zero
production call sites — the only references are `IErrorResponseHandler.cs:6` (declaration),
`ErrorResponseHandler.cs:16` (definition) and one test at `ErrorResponseHandlerTests.cs:59`. The single live
call site of any overload is `ExceptionMiddleware.cs:70`, which uses the `ExceptionDetails` overload closed
over `string` — a reference type, where `WhenWritingDefault` and `WhenWritingNull` behave identically. The
reported behaviour is therefore unreachable in the shipped product: a latent trap, not data loss. That trap
is already on the record at `03-review.md:14-18`, which is where a known, deliberate, currently-unreachable
trade-off belongs.

**Is there a better option?** Only ones this slice must not take. Preserving value-type defaults without
throwing requires either a dedicated diagnostics response type (moving the attribute onto a `string`-typed
property) or a custom converter / contract resolver. Both change `IErrorResponseHandler`'s signatures or add
an abstraction — against plan Decision 1, the Definition of Done (`01-plan.md:241`, interface unmodified) and
the skill's no-new-abstractions bar. If the generic overload ever acquires a real caller with a numeric or
boolean payload, that is the slice to do it in, with the contract change made deliberately.

**The test half of the ask** ("add serialization tests for `0` and `false`") is rejected with it: the plan
enumerates the test set exhaustively (`01-plan.md:219`, "exactly these seven test cases ... and no others",
enforced by `01-plan.md:248`), and tests for a payload shape no caller produces would pin a behaviour we have
just decided we may want to change, while constraining nothing real.

### RC3 — "Add language identifiers to all fenced blocks" (`.process/stack-trace-leak/02-implementation.md:26` and 12 more)

**CodeRabbit severity:** Minor. **Verdict:** REJECT — markdownlint against a closed audit artifact.

The MD040 warnings are factually correct: the fenced blocks in `02-implementation.md` (console output,
`git status` excerpts, HTTP bodies) carry no language tag. They are also irrelevant. `02-implementation.md`
is a closed pipeline artifact — not shipped code, not documentation. The repo enforces no markdownlint
(no `.markdownlint*` config at the root, and `.github/workflows/` contains only `api.yml`, `infra.yml`,
`web.yml`), and `.claude/commands/feature.md:141` forbids retro-editing artifacts. The distinction this repo
already draws is the right one: at `.process/teams/06-coderabbit-triage.md:202-206` an identical MD040
finding **was** implemented — but against `pr-body.md`, "the live PR description, so unlike the closed
pipeline artifacts it may be edited." This one lands on a closed artifact. Thirteen cosmetic fence edits
would rewrite the implementer's report to satisfy a linter the repo does not run, and change nothing a
reader gets from it.

### RC4 — "Keep the branch metadata outside the table" (`.process/stack-trace-leak/04-metrics.md:7`)

**CodeRabbit severity:** Minor. **Verdict:** MOOT — already resolved before triage; counted under rejected
because no implementer action follows.

The claim was valid at the reviewed commit `3737e05`: a plain-text branch line sat between the header row and
the data rows. The orchestrator, which owns `04-metrics.md`, has already repaired it. As the file stands, the
branch metadata is above the table at `04-metrics.md:3` and the table at `04-metrics.md:5-10` is well formed —
every row leads and trails with `|` and carries all ten cells. Nothing left to do.

### RC5 — "Make the stage timestamps unambiguous" (`.process/stack-trace-leak/04-metrics.md:9`)

**CodeRabbit severity:** Minor. **Verdict:** REJECT — a rounding artifact in a run record the orchestrator owns.

The observation is real: `04-metrics.md:9` records Implement finishing at 23:28 and `04-metrics.md:10` records
Review r1 starting at 23:27. The recorded duration resolves it — Implement started 23:18 and ran 9m 06s, so it
ended 23:27:0x, and `02-implementation.md` was written at 23:27. The `Finished` cell is a minute-rounded
wall-clock stamp, not evidence of concurrent stages; the stages did not overlap. Recorded timings are observed
values (`.claude/commands/feature.md:121`: "Record real numbers ... never estimate or invent"), and the metrics
file is the orchestrator's run log, not the implementer's file — the same immutable-run-record ground on which
`.process/upload-import-manifest/06-coderabbit-triage.md:24` rejected a metrics rewrite. Adding a note that the
stages "intentionally overlapped" would be worse than the rounding: it would assert something untrue. If the
orchestrator holds second-resolution data for its own row, correcting its own log is its prerogative; it is not
rework on this PR.

## Summary for the implementer

Nothing to do. Zero IMPLEMENT items, zero dev-decisions: no code change, no test change, no artifact edit. The
slice's production surface — `ErrorResponseHandler.cs`, `ErrorResponse.cs`, `ExceptionMiddleware.cs` and the two
resx files — stands as reviewed and approved at `03-review.md:1`. Do not run a rework pass; Stage 4 closes on the
skip-path.

**Carry-forward (not this PR).** If `GenerateErrorResponse<T>(code, message, data)` ever gains a production caller
with a value-type payload, `WhenWritingDefault` will omit a deliberate `0` / `false` / `Guid.Empty`. Already
recorded at `03-review.md:14-18`. The fix at that point is a dedicated diagnostics response type or a converter —
never `WhenWritingNull`, which throws for every value-type `T`.
