# Run Metrics — publish-pipeline

**Base branch:** `main` @ `ba2c824` · **Feature branch:** `feature/publish-pipeline` · **Dev:** away, blanket authority granted (all gates proxied)

| # | Stage | Agent | Model | Started | Finished | Duration | Tokens | Tool uses | Outcome |
|---|-------|-------|-------|---------|----------|----------|--------|-----------|---------|
| 0 | Pre-flight + Acceptance (PROXY) | orchestrator | — | 2026-08-28 14:36 | 2026-08-28 14:40 | ~4m | — | — | clean tree on merged main; scope split (scanner → own slice); 10 product decisions proxied |
| 1 | Plan | feature-planner | OPUS 5 | 2026-08-28 14:41 | 2026-08-28 15:01 | 19m 51s | 183,232 | 57 | plan written; 21 decisions, 1 DEV-DECISION; 47 production + 22 test files, 87 tests |
| 2 | Plan gate (PROXY) | orchestrator | OPUS 5 | 2026-08-28 15:01 | 2026-08-28 15:03 | — | — | — | APPROVED with an execution change: implementation split into 2 sequential passes (see below) |

## Plan-gate verification (orchestrator, before proxy approval)

Checked the claims most likely to break a build, rather than trusting them:

1. **`IRepository<T>.FindPaginatedAsync` exists with the signature the plan calls.** CONFIRMED —
   `Core.DDD/Repositories/IRepository.cs:32`, including the `filter` / `orderBy` / `asNoTracking`
   parameters `RegenerateMarketplaceHandler` depends on.
2. **Every Azure Functions package is already in `api/Directory.Packages.props`.** CONFIRMED —
   lines 32–39 declare `Microsoft.Azure.Functions.Worker`, `.Sdk`, `.Extensions.Abstractions`,
   `.Extensions.Storage.Queues`, `.Extensions.Http.AspNetCore` plus
   `Microsoft.Extensions.Configuration.AzureAppConfiguration` and `Azure.Storage.Blobs`. The
   AppTemplate anticipated a Jobs project, so `E3A.Jobs` can use versionless `PackageReference`
   under central package management with **no edit to that file** — exactly as the plan states.
3. **Solution file is `api/E3A.slnx`** — confirmed, and the plan correctly requires `E3A.Jobs` to be
   added to it.

## Execution change — implementation split into two passes (orchestrator)

The plan is 47 production + 22 test files. The largest prior slice was 13 files / 45 tests and cost
one implementer ~173k tokens; this is roughly four times that. Asking a single agent for it invites
a truncated or degraded second half, and the planner itself identified the cut line.

The slice is NOT split — one branch, one PR, one review. Only the implementer work is split, along
the plan's own build order:

- **Pass 1 — build steps 1–6.** Core.Azure additions, options, error codes, resx, domain, EF +
  migration, `Publishing/Shared` units, `PublishEngineer`, `ProcessPublishJob`,
  `RegenerateMarketplace`, `E3A.Jobs`. **The pipeline is end-to-end functional at the end of this
  pass**, which is what makes it a safe stopping point.
- **Pass 2 — build steps 7–9.** `GetPublishStatus` + `PublishController`, unlist/relist, Postman,
  docs sync.

One reviewer then reviews the whole slice, as usual.
| 3 | Implement pass 1 (steps 1–6) | feature-implementer | OPUS 5 | 2026-08-28 15:03 | 2026-08-28 15:20 | ~17m | n/a | n/a | **agent terminated by a session usage limit immediately after finishing** — work and report both complete on disk; usage not reported, so recorded as n/a rather than estimated |
| 4 | Pass 1 verification | orchestrator | OPUS 5 | 2026-08-28 15:2x | 2026-08-28 15:2x | — | — | — | build 0 errors / 9 pre-existing warnings · **324/324 tests** (236 → +88) · `E3A.Jobs` present in `E3A.slnx` — all re-run independently, not trusted |

## Pass 1 interruption

The pass-1 implementer hit a session usage limit and was terminated by the harness with its last
output being "Green. Writing the report." The report had in fact been written
(`02-implementation.md`, complete through its "What pass 2 still owes" section), and the orchestrator
independently re-ran the build and the full test suite rather than accepting "Green" on trust.
Both pass. No work was lost and nothing needed redoing.

Token usage for this stage is unavailable because the terminating notification carried no usage
block; per the metrics rule it is recorded as `n/a` rather than estimated.
| 5 | Implement pass 2 (steps 7–9) | feature-implementer | OPUS 5 | 2026-08-28 15:26 | 2026-08-28 15:37 | 11m 19s | 182,224 | 96 | 10 production + 7 test files; **347/347** tests; Postman + 3 docs synced; 3 declared deviations; **found a real defect in pass 1's endpoint and escalated rather than patching it** |
| 6 | Implement pass 3 (enum fix) | feature-implementer | OPUS 5 | 2026-08-28 15:39 | … | … | … | … | scoped correction, authorised after orchestrator verified the blast radius |

## Pass 2's escalation — verified before authorising a fix

Pass 2 reported that `POST /api/engineers/{id}/publish` rejects `{"increment": "Patch"}` and accepts
only `{"increment": 0}`, contradicting the plan's own API-surface table. It declined to fix it
(`Program.cs` is not in the plan's touched-file list) and declared it instead — the correct call.

Orchestrator verification:

- `Program.cs:35` is a bare `AddControllers()`; `:78` calls `ConfigureHttpJsonOptions`, which
  configures `Microsoft.AspNetCore.Http.Json.JsonOptions` — read by **minimal APIs only**. MVC
  controllers read `Microsoft.AspNetCore.Mvc.JsonOptions`. The finding is correct.
- **Blast radius established before authorising:** `PublishEngineerRequest.Increment` is the only
  enum-typed property in any JSON request or response body in the API. `CatalogSort` is `[FromQuery]`
  (query-string model binding, unaffected — `?sort=Newest` already works), and every result record
  exposes status as a `string`. The change touches exactly one property.

Authorised as **in scope**: it is a defect in the endpoint this slice created, and the endpoint does
not honour its own documented contract. Merging a documented-but-broken endpoint would be worse. The
fix keeps `ConfigureHttpJsonOptions` in place — the minimal-API endpoints still need it; both
registrations are required.

Pass 3 was additionally required to prove the fix **empirically** (string binds, `Major` does not
silently bind as the first member, and an invalid value is rejected rather than defaulting) rather
than asserting that it should now work.
| 7 | Review r1 | feature-reviewer | OPUS 5 | 2026-08-28 15:45 | … | … | … | … | reviewing all three passes as one slice |

## Pass 3 outcome

Fix applied to exactly two files (`Program.cs:35`, one Postman body); `ConfigureHttpJsonOptions`
deliberately retained — controllers read `Mvc.JsonOptions`, minimal APIs read `Http.Json.JsonOptions`,
and deleting either silently breaks one half.

**Proof method matters here.** Pass 3 could not POST to the endpoint (it is behind `[Authorize]`, so a
401 fires before model binding). It instead drove the real `SystemTextJsonInputFormatter` taken from a
built host's `IOptions<MvcOptions>`, against the real `PublishEngineerRequest`, running a **control
with the bare pre-fix line in the same process**:

| Body | Control (pre-fix) | Fixed |
|---|---|---|
| `"Patch"` / `"Minor"` / `"Major"` | REJECTED | bind to 0 / 1 / 2 — not silently to the first member |
| `"Nonsense"` | REJECTED | **REJECTED** — bad input is not swallowed |
| `0` | BOUND | BOUND — backward compatible with the numeric workaround |
| `{}` | REJECTED | REJECTED — pass 1's `[property: JsonRequired]` guard survives |

The control is what makes this evidence rather than assertion: it attributes the delta to the fix
rather than to System.Text.Json web defaults.

Pass 3 also corrected an inaccuracy in pass 2's escalation — the fix does **not** change response
shapes API-wide, because no result record exposes an enum. Pass 2's text was left intact as the
historical record, with the correction noted in pass 3.

**Build-measurement trap worth remembering:** a plain incremental `dotnet build` reports 0 warnings
because `core-libraries` is not recompiled. Only `--no-incremental` yields a figure comparable to the
9-warning baseline. Reviewers must use it or they will "confirm" a warning count that is an artefact.

## Residual risk (no test can catch these — worker and DI wiring are out of test scope)

1. **Queue payload round-trip is unproven.** `StorageQueueClient` serializes with default options
   (PascalCase `{"VersionId":"…"}`); the isolated-worker `QueueTrigger` binder should deserialize
   case-insensitively, but nothing here proves it. **Smoke-test before the first real publish.**
2. **Nothing tests the JSON DI wiring.** If someone later drops the `AddJsonOptions` call, the publish
   endpoint silently regresses to numeric-only.
| 8 | Implement pass 4 (N4 + N2) | feature-implementer | OPUS 5 | 2026-08-28 16:07 | 2026-08-28 16:14 | 7m 25s | 77,459 | 46 | upload/publish race guard + strengthened determinism test; **351/351** tests |
| 9 | Pass 4 verification | orchestrator | OPUS 5 | 2026-08-28 16:15 | 2026-08-28 16:16 | — | — | — | 351/351 re-run; guard ordering read directly — sits after the owner check and before `OpenReadStream()`, so a rejected upload touches no storage |

## Post-approval fixes (orchestrator decision)

Review r1 was APPROVED with 9 non-blocking follow-ups. Two were fixed before the PR rather than
deferred:

**N4 — upload could race an in-flight publish. This was an orchestrator error.** The architecture
described to the dev said "the same guard blocks a new upload while publishing, so drafts can't shift
under a running job", but the acceptance doc's In-scope list omitted it. The planner and implementers
built exactly what was specified; the gap was in the specification. The slice itself created the race
(`Building` did not exist before it): upload during `Building` → `DraftSnapshotFreezer` reads the NEW
draft bytes against the OLD `FrozenManifestJson`, publishing content the creator never reviewed.

**N2 — the determinism test did not defend the invariant it existed for.** Proven, not assumed:
temporarily setting `DeterministicZipper`'s epoch to `DateTimeOffset.UtcNow` produced `Failed: 1,
Passed: 3` — the strengthened test failed while the other three zipper tests, including the reversed-
input test 24, still passed. That confirms the reviewer's diagnosis precisely.

**A suggested assertion was rejected on evidence.** The obvious pinning assertion
(`entry.LastWriteTime == DeterministicTimestamp`) fails on any non-UTC machine: zip entries carry
MS-DOS timestamps with no timezone, so the value reads back at the machine's local offset. It would
have passed in review and flaked in CI. The committed assertion compares offset-independent
wall-clock components across every entry.

**No fresh internal review ran on pass 4** — review r1 was APPROVED, and these were elective
non-blocking fixes, so the rework loop (which exists for CHANGES_REQUESTED) did not apply. The
orchestrator verified them directly and CodeRabbit provides the external check.

## Known, accepted, not fixed here

The in-flight guard is a `FirstOrDefaultAsync`, not a lock, so two genuinely concurrent requests could
both pass it. This is the same window `PublishEngineerHandler`, `UnlistEngineerHandler` and
`RelistEngineerHandler` already carry; closing it needs a unique filtered index plus a caught
constraint violation. The fix narrows the race rather than eliminating it — flagged deliberately.
| 10 | Commit + push + PR | orchestrator | — | 2026-08-28 16:17 | 2026-08-28 16:22 | ~5m | — | — | commit `bf47eff` (109 files, +6,104/−37); **PR #4** opened via GitHub REST API |
| 11 | CodeRabbit wait + fetch | orchestrator | — | 2026-08-28 16:22 | 2026-08-28 16:33 | ~9m poll | — | — | 13 inline (RC1–RC13) + 1 review object saved verbatim to `05-coderabbit-comments.md` |
| 12 | CodeRabbit triage | feature-reviewer | OPUS 5 | 2026-08-28 16:34 | … | … | … | … | fresh reviewer decides |
| 13 | CodeRabbit triage | feature-reviewer | OPUS 5 | 2026-08-28 16:34 | 2026-08-28 16:42 | 8m 00s | 141,056 | 40 | **TRIAGE: 4 implement / 8 rejected / 2 dev-decisions** — no Criticals raised, so no downgrade to veto; build + 351/351 re-run |
| 14 | CodeRabbit rework | feature-implementer | OPUS 5 | 2026-08-28 16:43 | 2026-08-28 16:51 | 8m 02s | 76,865 | 36 | 4/4 done; **354/354**; proved the new tests bite by reverting both production changes (3 failures) then restoring; 4 declared deviations |
| 15 | CodeRabbit verify | feature-reviewer | OPUS 5 | 2026-08-28 16:52 | … | … | … | … | fresh scoped verification |

## The bug CodeRabbit found that the internal review did not

**RC8 was a genuine data-integrity defect, and it originated in the plan, not the implementation.**
The approved step order wrote the pinned per-version marketplace *after* persisting `Published`:

```
MarkPublished → SaveChangesAsync → upload m/{plugin}/{version}/marketplace.json
```

If that upload throws, the queue retries, the guard `if (version.Status is not (Queued or Building))
return;` sees `Published`, and returns. The pinned marketplace is never written **and nothing will
ever write it** — a permanent 404 for anyone pinning that version, on a version reporting success.

Fixed by moving the pinned upload ahead of the save, so a retry finds `Building` and re-runs the
whole tail idempotently (zip upload skipped by the exact-match check, pinned rewritten with
`overwrite: true`). Save count stays at two; no `try`/`catch` added.

## The rejection that would have been dangerous to accept

**RC9 asked for the queue send-side `visibilityTimeout` to be removed as redundant.** The triage read
`CoreDbContext.cs` and found domain events published at line 99 with `base.SaveChangesAsync` at line
104 — the message really is enqueued before its own row commits, and that delay is the only guard
against the worker chasing a row that does not exist yet. A dequeue lease governs redelivery, not
initial visibility. Implementing RC9 would have created intermittent publish failures visible only
under load.

Accepting a finding that contradicted the plan while refusing one that contradicted the code is
exactly what the triage stage exists to do.
| 16 | CodeRabbit verify | feature-reviewer | OPUS 5 | 2026-08-28 16:52 | 2026-08-28 17:00 | 7m 43s | 114,680 | 38 | **APPROVED** — 4/4 resolved, 8 rejects held, scope contained; re-proved the tests bite in a detached worktree at `bf47eff`; build + 354/354 re-run |

## ⚠️ Landmine recorded by the verifier — read before touching E3A.Jobs

On the RC8 failure path the **in-memory** `version` is left `Published` while the database keeps
`Building`. That is safe *today* only because nothing in the `E3A.Jobs` DI scope saves after the throw.

But `Core.Auditing/AuditBehaviour.cs:44-68` calls `SaveChangesAsync` **in a `finally` block, on the
exception path, swallowing its own errors**. `AddCoreAuditing` is currently registered only in
`E3A.Api/Program.cs:68`. If it is ever added to `E3A.Jobs/Program.cs` and `ProcessPublishJobCommand`
becomes `IAuditableCommand`, that flush commits the in-memory `Published` status on the failure path
and **silently re-creates RC8** — the exact permanent-404 bug this cycle just fixed.

If auditing ever reaches the worker: stop mutating `version` before the pinned upload rather than
relying on scope disposal.

Also noted: the ordering test's `Building` fixture is load-bearing. It yields exactly one save, which
makes the `Received.InOrder` sequence unambiguous. Under a `Queued` fixture the matched set would be
three calls and the test would pass under **both** orderings — vacuous. Do not "simplify" it.
