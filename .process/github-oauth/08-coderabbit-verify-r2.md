VERDICT: APPROVED

# Stage 4 verification — CodeRabbit round 2, PR #5 (`github-oauth`)

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
is identical to the pre-probe state (5/3, 9/9, 1/1, 8/0, 0/1) and the suite is green at 437.

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
signed, expired state whose nonce is their own; landing the victim on `/callback?state=<that>` returns
`Expired`, flag `true`, and clears the victim's cookie. That is RC2-2 rebuilt by a refactor in a
different file, with nothing to catch it.

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
  succeeds — and any replay still needs a fresh GitHub `code`. Worth one line in F2's description.
- **NB-4** `.process/github-oauth/pr-body.md:59-64` carries F1 to F6; the triage's F7 (process learning,
  a reviewer-prompt change) is absent. Correct call for a PR body — F7 is pipeline debt, not shipping
  debt — noted only so it is not lost.
