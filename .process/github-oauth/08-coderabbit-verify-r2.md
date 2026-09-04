VERDICT: APPROVED

# Stage 4 verification — CodeRabbit round 2, PR #5 (`github-oauth`)

> **Artifact placement note.** This file was written to the scratchpad, not to
> `.process/github-oauth/08-coderabbit-verify-r2.md`, because the worktree
> `scratchpad/wt-oauth` was deleted by another process at ~17:03 while this review was being written
> (`.git`, `.claude`, `.process` and most of `api/` are gone; 691 files remain, no git metadata).
> All verification below completed against the intact worktree **before** the deletion. Copy this file
> to `.process/github-oauth/08-coderabbit-verify-r2.md` once a worktree exists again. **The round-2
> changes were uncommitted in that worktree — check whether they survived before merging.**

Fresh reviewer, worktree `wt-oauth`, branch `feature/github-oauth`, HEAD `d44bfc3` plus the uncommitted
round-2 changes. Judged against `06-coderabbit-triage-r2.md`, the `## Round 2` section of
`07-coderabbit-rework.md`, and the code — not the report.

The conditional deletion **did not reopen anything.** Every branch was traced and mutation-probed, and
both degenerate implementations ("always true", "always false") fail the suite. Round 1's browser
binding survives round 2 intact.

## Blocking

None.

## Branch trace — `CompleteGitHubLoginHandler` to cookie

`OAuthStateProtector.Validate` (`api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:29-66`)
compares the nonce at `:43`, **before** the signature check at `:55` and the expiry check at `:60`.
So `Valid` and `Expired` both imply the nonce matched; `Invalid` implies it either did not match or was
never compared. Mapping:

| Handler branch | Flag | Nonce compared | Matched | Cookie cleared | Correct |
|---|---|---|---|---|---|
| `:18` code missing (returns before `Validate`) | `false` | no | — | no | yes — the trap; `true` here re-opens RC2-2 via a bare `GET /callback` |
| `:25` `Invalid` — missing state/nonce, wrong segment count, nonce mismatch | `false` | no, or yes-and-failed | no | no | yes — nothing was consumed |
| `:25` `Invalid` — forged signature or bad expiry parse **after** a nonce match | `false` | yes | yes | no | yes — state rejected, nothing burned; reaching this needs the nonce already, which is F1's precondition |
| `:30` `Expired` | `true` | yes | yes | yes | yes — see ruling 2 |
| `:37` exchange failed | `true` | yes | yes | yes | yes |
| `:44` profile fetch failed | `true` | yes | yes | yes | yes |
| `:49` profile invalid | `true` | yes | yes | yes | yes |
| `:70` success | `true` | yes | yes | yes | yes |

No branch consumes the nonce and fails to clear. No branch clears without having matched. There is
exactly one producer (`CompleteGitHubLoginHandler.cs:70,75`) and one consumer
(`AuthenticationController.cs:37`) — grepped, nothing else constructs the record.

**On the brief's phrasing "cleared on success *and nonce-mismatch*":** clearing on nonce-mismatch is
precisely the RC2-2 bug. An attacker mints their own state (nonce = theirs), lands the victim's browser
on the callback, mismatch, cookie cleared, victim's in-flight login dies. `false` at `:25` is correct
and matches `06-coderabbit-triage-r2.md:136`. A mismatch consumes nothing, so nothing needs burning.

## Mutation probes — run here, not taken on report

All against the full suite. Files restored from backup after each; the final `git diff --numstat -- api`
was identical to the pre-probe state (5/3, 9/9, 1/1, 8/0, 0/1) and the suite green at 437.

| Mutation | Result |
|---|---|
| `:18` code-missing `false` to `true` | **Failed 1 / 437** — `CompleteGitHubLoginHandlerCookieTests.Handle_ShouldNotConsumeTheStateCookie_WhenCodeIsAbsent`, and only that. Matches the claim. |
| `:37` exchange-failure `true` to `false` | **Failed 1 / 437** — `Handle_ShouldConsumeTheStateCookie_WhenTheStateMatchesButTheExchangeFails`, and only that. Matches the claim. |
| **all** flags to `true` | **Failed 2** — `..._WhenCodeIsAbsent`, `..._WhenTheBrowserNonceDoesNotMatch` |
| **all** flags to `false` | **Failed 2** — `..._WhenTheStateMatchesButTheExchangeFails`, `CompleteGitHubLoginHandlerTests.Handle_ShouldConsumeTheStateCookie_WhenLoginSucceeds` |
| `:25` `Invalid` `false` to `true` | **Failed 1** — `..._WhenTheBrowserNonceDoesNotMatch` |
| `:30` `Expired` `true` to `false` | **437 green** — unpinned (NB-1) |
| `:44` profile-fetch `true` to `false` | **437 green** — unpinned (NB-1) |
| `:49` profile-invalid `true` to `false` | **437 green** — unpinned (NB-1) |
| `OAuthStateProtector.Validate`: expiry check moved **above** the nonce comparison | **437 green** — unguarded (NB-2) |

Neither an always-true nor an always-false implementation can pass. Both security directions —
"must not consume when nothing was validated" and "must consume when it was" — are pinned.

## Ruling on the two items flagged for a human

**1. `if (result.StateNonceConsumed)` at `AuthenticationController.cs:37` has no test — acceptable, no
action needed.** `conventions/dotnet-testing.md:156` and `SKILL.md:898` put controllers out of scope,
and the line is a bare `if` over a flag pinned in both directions at the layer where the logic lives.
Testing it would need `WebApplicationFactory` or a hand-built `DefaultHttpContext` — a new dependency
and a new test category, in the final cycle, to guard three lines. The convention is working as
designed. The rework's note 3 is the right disposition.

**2. `Expired` consuming the cookie — correct today, and it wants a test, not a guard.** The reasoning
is sound: `Validate` compares the nonce at `:43` before the expiry check at `:60`, so `Expired` implies
a match and clearing is right. But I probed the coupling: **moving the expiry check above the nonce
comparison leaves all 437 tests green.** After such a reorder, an attacker who calls
`/api/auth/github/login` in their *own* browser and waits out `StateExpirationMinutes` holds a validly
signed, expired state whose nonce is their own; landing the victim on that callback returns `Expired`,
flag `true`, and clears the victim's cookie. That is RC2-2 rebuilt by a refactor in a different file,
with nothing to catch it.

Not blocking: the code as it stands is correct and I cannot write an input that produces a wrong result
against this tree. A guard is the wrong instrument — it would duplicate the protector's logic in the
handler. The right instrument is one protector test, e.g.
`Validate_ShouldReturnInvalid_WhenTheNonceDoesNotMatchAnExpiredState`, about six lines in
`OAuthStateProtectorNonceTests.cs`, building an expired state with `stateExpirationMinutes: -1` and
validating it against a foreign nonce. **Recommended as follow-up F8**, ahead of F4 and F5.

## Non-blocking

- **NB-1** `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs:30,44,49`
  — three of the five consuming branches (`Expired`, profile-fetch-failed, profile-invalid) carry no
  assertion on `StateNonceConsumed`; flipping each to `false` leaves 437 green. The branches themselves
  are covered (`CompleteGitHubLoginHandlerFailureTests.cs:78,100,113`), only the new flag is unasserted.
  Harmless today because single-use is pinned on two other branches, so no always-false refactor can
  survive. A `[Theory]` over the failure modes would close it cheaply.
- **NB-2** `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:43` vs `:60` — the
  nonce-before-expiry ordering that gives `Expired` its meaning is unguarded. See ruling 2. F8.
- **NB-3** `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:35-40` — moving the delete
  below `mediator.Send` means an exception inside the handler (the F2 first-login race at
  `CompleteGitHubLoginHandler.cs:66`) now leaves the cookie in place, where the old code always cleared
  it. Neither the triage nor the rework mentions this. Benign, arguably better — the user's retry then
  succeeds — and any replay still needs a fresh GitHub code. Worth one line in F2's description.
- **NB-4** `.process/github-oauth/pr-body.md:59-64` carries F1 to F6; the triage's F7 (process learning,
  a reviewer-prompt change) is absent. Correct call for a PR body — F7 is pipeline debt, not shipping
  debt — noted only so it is not lost.

## Verified

Every claim in the `## Round 2` section of `07-coderabbit-rework.md` was checked against the tree.

- **Build.** `dotnet build api/E3A.slnx --no-incremental` gives `Build succeeded. 9 Warning(s) 0
  Error(s)`. All nine are in `api/core-libraries` (`Core.Validation` CS8602 x2, `Core.OTP` CS8618 x2,
  `Core.Notifications` CS8618 x5). No `E3A.*` warning. Claim exact.
- **Tests.** `dotnet test api/E3A.slnx` gives `Failed: 0, Passed: 437, Skipped: 0, Total: 437`. Up four
  from the 433 baseline recorded at `06-coderabbit-triage-r2.md:21`, as predicted. Claim exact.
- **RC2-2 shape.** `AuthenticationRedirectResult.cs:3` is `sealed record ... (string RedirectUrl, bool
  StateNonceConsumed)`; `CompleteGitHubLoginHandler.cs:73` is `Failure(string errorCode, bool
  stateNonceConsumed)` with named arguments at all six call sites; `AuthenticationController.cs:39`
  deletes with the same `OAuthStateCookieOptionsGenerator.Generate()`, so the cookie `Path`
  (`OAuthStateCookieOptionsGenerator.cs:6,16`) is identical to the append at `:24` and the browser
  matches the cookie. Guard order in the handler is unchanged; the error precedence contract and
  `CompleteGitHubLoginHandlerFailureTests.cs:38-44` still hold.
- **Tests required by the triage all exist, with the exact names.**
  `CompleteGitHubLoginHandlerCookieTests.cs:32` `Handle_ShouldNotConsumeTheStateCookie_WhenCodeIsAbsent`
  (with `_oAuthStateProtector.DidNotReceive().Validate(...)` at `:37`, which also fails on a guard
  reorder), `:41` `Handle_ShouldNotConsumeTheStateCookie_WhenTheBrowserNonceDoesNotMatch`, `:51`
  `Handle_ShouldConsumeTheStateCookie_WhenTheStateMatchesButTheExchangeFails`, and
  `CompleteGitHubLoginHandlerTests.cs:78` `Handle_ShouldConsumeTheStateCookie_WhenLoginSucceeds`. New
  file, so F4 does not get worse; `CompleteGitHubLoginHandlerFailureTests.cs` is still 147 lines.
- **RC2-6.** In `GetGitHubLoginUrlQueryHandlerTests.cs` the `NotContain` line is gone; `:43`
  `result.StateNonce.Should().Be("state-nonce")` and the test name
  `Handle_ShouldSurfaceTheNonceForTheBrowserCookie_WhenCalled` both remain, and the name is still true of
  what the test asserts. The real contract stays pinned at `OAuthStateProtectorTests.cs:31`
  (`segments[0].Should().Be(Nonce)`). No test removed, so 433 plus 4 equals 437 checks out.
- **Round 1's fix survives round 2.** `OAuthStateProtector.cs:31` (missing nonce gives Invalid), `:43`
  (`CryptographicOperations.FixedTimeEquals` on segment 0), `:55` (signature, fixed-time, before expiry).
  Guarding tests intact and untouched: `OAuthStateProtectorNonceTests.cs:58`
  `Validate_ShouldReturnInvalid_WhenTheStateIsPresentedByAnotherBrowser` (cross-browser, asserting both
  directions), `:28` missing-nonce theory, `:38` nonce mismatch, and
  `OAuthStateProtectorTamperTests.cs:79-88` (expiry moved into the past gives Invalid, explicitly
  `NotBe(Expired)`) — the round-1 blocking finding. `git diff` showed no change to any of these files.
- **Postman.** `postman/e3a.postman_collection.json` parses (json.load clean). The diff is one
  description string and nothing else. All three auth requests present and unchanged: GET
  api/auth/github/login (noauth), GET api/auth/github/callback with the code and state query params
  (noauth), GET api/auth/me. URL, method, query params and auth mode untouched; no request added,
  removed or orphaned; the collection mirrors `AuthenticationController` exactly. The declared deviation
  is correct and the new text is accurate — the old sentence claiming the cookie is cleared on every
  callback would have been false after this change, so correcting it was required by the Postman-sync
  rule, not optional.
- **Docs.** `/docs` untouched (`git status docs/` clean) and no divergence exists.
  `docs/architecture.md:28` — the callback rejects the state unless the cookie matches, **then clears
  it** — and `docs/implementation-plan.md:63` — a nonce cookie the callback **compares and clears** —
  both describe compare-then-clear. The code now does exactly that; round 1's clear-then-compare was the
  divergence and this change closes it. No doc edit was needed or made.
- **Scope containment.** `git diff` was exactly the three IMPLEMENT items plus the declared Postman
  sentence: five source and test files, one Postman description, three `.process/` files. RC2-3, RC2-4
  and RC2-5 have no code footprint — no try/catch anywhere in the auth slice (the only two catch blocks
  in `E3A.Application` are pre-existing, in `UploadEngineerDraft` helpers, not handlers), no persistence
  boundary change, no code challenge or verifier. No new package, no csproj change, no migration, no
  Azure resource, no new options key.
- **Closed artifacts intact.** `git status` showed `01-plan.md`, `02-implementation.md`, `03-review.md`,
  `03-review-r2.md`, `05-coderabbit-comments.md`, `06-coderabbit-triage.md` and `08-coderabbit-verify.md`
  unmodified. `07-coderabbit-rework.md` was **125 added lines, 0 deleted** — strictly appended, round 1's
  text untouched, as the append-only ruling requires. `04-metrics.md` was 7 added lines, 0 deleted.
  `pr-body.md` was the only rewritten file, which the triage authorises at `:318`.
- **PR body is now true.** `pr-body.md:9` "signed, expiring and browser-bound" matches
  `docs/architecture.md:28` and matches the code. `:28` reports **437 / 437**, verified. `:44-47`
  describes the shipped cookie exactly as `OAuthStateCookieOptionsGenerator.cs:8-19` builds it and states
  the new conditional rule correctly, including the clause that a callback validating nothing leaves the
  cookie alone. `:49` states the residual honestly and explicitly retracts the hash-the-nonce note from
  `08-coderabbit-verify.md:104`. `:53` carries the RC2-4 MaxAge correction with SDK **10.0.400**. F1 to
  F6 are present at `:59-64`. The artifact list at `:68-77` now covers both rounds. No claim in it is
  false.
- **Style absolutes on every touched file.** File-scoped namespaces; `sealed record` on
  `AuthenticationRedirectResult`; `sealed class` on the handler and both touched test classes; no
  comments added; `.ConfigureAwait(false)` on every await in the handler (`:33,40,52,56,58,66`) and none
  in the controller; one `SaveChangesAsync`, on the success path only (`:66`); no `DateTime`. Line
  counts: handler 77, controller 51, new test file 59, `CompleteGitHubLoginHandlerTests.cs` 98 — all
  under the 100-line rule. No section 8 DON'T pattern appears in the diff (8.1 through 8.5 are all
  inapplicable: no entity constant, no hand-rolled random, no slug logic, no lifecycle enum, no
  soft-delete filter).

## Test quality

- **`CompleteGitHubLoginHandlerCookieTests`** (new, 59 lines) — genuinely constrains. All three cases
  were shown to bite by mutation, and the file as a whole rejects both degenerate implementations. `:37`
  `DidNotReceive().Validate(...)` is the strongest assertion in the diff: it pins *why* the flag is
  `false` on the code-missing branch, not just that it is, so a guard reorder that made the flag
  incidentally right would still fail. The opposite of a substitute-echo test.
- **`CompleteGitHubLoginHandlerTests.Handle_ShouldConsumeTheStateCookie_WhenLoginSucceeds`** — carries
  real weight: one of the two tests that kill the always-false implementation.
- **`GetGitHubLoginUrlQueryHandlerTests.Handle_ShouldSurfaceTheNonceForTheBrowserCookie_WhenCalled`** —
  after the deletion this is a thin plumbing assertion (the substitute returns the nonce, the test
  asserts the nonce), but it does pin that the handler surfaces the protector's nonce rather than
  dropping or re-deriving it, and removing the pass-through turns it red. Weak but not vacuous, and
  strictly better than the false assertion it replaced.
- **`CompleteGitHubLoginHandlerFailureTests`** — unchanged, still constrains the error precedence
  order; it does not assert the new flag (NB-1).
- **`OAuthStateProtectorNonceTests` and `OAuthStateProtectorTamperTests`** — unchanged and still the real
  guard for browser binding; `NonceTests:63-64` asserting both directions in one test is what makes the
  cross-browser property non-vacuous. Their one gap is the nonce/expiry ordering (NB-2).

## Recommendation

Merge. Add **F8** — the roughly six-line protector ordering test from ruling 2 — to the follow-up list
before or immediately after merge; it is the cheapest remaining hardening and the only one guarding a
coupling this change introduced.

**Before merging, confirm the round-2 working-tree changes survived the worktree deletion described at
the top of this file.** They were uncommitted when the worktree was removed.
