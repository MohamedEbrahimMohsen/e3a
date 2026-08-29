TRIAGE: 3 to implement, 3 rejected, 0 dev-decisions

# Stage 4 — CodeRabbit triage round 2, PR #5 (`github-oauth`)

6 new inline comments (RC2-1 … RC2-6), all of them anchored on `.process/` documents rather than on
source. Judged against the worktree code, not against the comment text or the earlier reports.

**Two things shape this triage.**

1. **Five of six anchors are on closed, append-only audit artifacts** (`pr-body.md` is the exception —
   see below). Round 2's standing ruling, applied in round 1's triage to RC1 and RC6, is that an
   approved plan or a signed review is not edited to match later work: doing so erases the gap the
   trail exists to show. Corrections are recorded in the *current* document.
2. **CodeRabbit is reading pre-fix prose.** RC2-1 quotes the residual-risk paragraph written *before*
   browser binding shipped. I verified the code rather than the paragraph.

Baseline re-run in the worktree before triaging, so the rework has a known-good starting point:

```
dotnet test api/E3A.slnx
Passed!  - Failed: 0, Passed: 433, Skipped: 0, Total: 433 - E3A.Tests.dll (net10.0)
```

`dotnet --version` in this worktree: **10.0.400** (TFM `net10.0`). Recorded here because RC2-4 asks for it.

---

# IMPLEMENT

## 1. Correct the PR body — browser binding shipped, the "residual risk" paragraph is now false

**Covers:** RC2-1 (code change **rejected**, prose correction accepted) · plus the RC2-4 correction ·
**Severity: Major (a factual defect in the PR description; zero code impact)**

### Verification — browser binding is genuinely shipped

RC2-1 asks to "add the nonce-cookie comparison before accepting the callback, or gate this
authentication flow". All of that exists, in the very commit CodeRabbit was reviewing (`d44bfc3`):

- `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:24` — the login action appends
  `options.StateCookieName` carrying `result.StateNonce`, with `HttpOnly`, `Secure`, `SameSite=Lax`,
  `IsEssential`, `Path=/api/auth`, `MaxAge = StateExpirationMinutes`
  (`OAuthStateCookieOptionsGenerator.cs:8-18`).
- `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandler.cs:12-15` — one
  `Create()` call, so the cookie nonce and the nonce inside the signed state are the same value.
- `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:34,38` — the callback reads the
  cookie and passes it as `CompleteGitHubLoginCommand(code, state, nonce)`.
- `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:31` (missing nonce → `Invalid`) and
  `:43` (`CryptographicOperations.FixedTimeEquals` of `segments[0]` against the cookie → `Invalid`).
- `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs:21-26` — the
  status maps to `#error=AUTHENTICATION_STATE_INVALID` **above** the code exchange at `:33`.

So the exact attack RC2-1 describes — attacker mints a state, victim's browser completes it — fails at
`OAuthStateProtector.cs:31/43`: browser B holds either no cookie or its own nonce, and the cookie is
`HttpOnly`/`Secure`/origin-scoped, so the attacker cannot plant A's nonce in B. The round-1 verifier
mutation-tested this (`08-coderabbit-verify.md:33-44`) and the guarding test does go red when the
comparison is deleted. **The code demand in RC2-1 is already satisfied; gating the flow would be a
regression.**

What is *not* satisfied is the document CodeRabbit was reading. `.process/github-oauth/pr-body.md` is
**not** a closed audit artifact — it is the live description of what ships, and it currently tells a
reviewer that the merged code has an open login-CSRF hole. That is a divergence between an artifact and
the code, and it is the one thing in RC2-1 worth doing.

### Fix — edit `pr-body.md` only

- `pr-body.md:38-40` — replace the "Known residual risk" paragraph. Browser binding is implemented, not
  deferred. State the shipped mechanism (`Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/api/auth` nonce
  cookie, fixed-time compared against state segment 0, cleared on the callback) and state the *actual*
  residual: the nonce travels verbatim as segment 0 of `state`, so an attacker who obtains a victim's
  **in-flight** state value can still pair it with an attacker-owned `code` — see follow-up F1. Do not
  claim a hole that no longer exists, and do not claim a completeness that would need PKCE.
- `pr-body.md:9` — "Anti-CSRF `state` is **stateless, signed and expiring**" is now half-wrong. The
  signed/expiring half stands; "stateless" must become "browser-bound by a nonce cookie, with no server
  or cache state". Match the wording already in `docs/architecture.md:28`.
- `pr-body.md:28` — "**420 / 420**" is stale; the shipped suite is **433**.
- `pr-body.md:44-48` — the artifact list stops at `04-metrics.md`. Add `05-coderabbit-comments.md`,
  `06-coderabbit-triage.md`, `07-coderabbit-rework.md`, `08-coderabbit-verify.md`, and this file.
- **Fold in the RC2-4 correction** (see that rejection below): the deviation note at
  `07-coderabbit-rework.md:84` justified the null `MaxAge` on the delete path with a claim that is false
  on this SDK. State in the PR body that `ResponseCookies.Delete(string, CookieOptions)` copies the
  options and then forces `Expires = UnixEpoch, MaxAge = null` regardless, so the null default is
  belt-and-braces, not load-bearing — probed empirically at `08-coderabbit-verify.md:52-56` on SDK
  **10.0.400**. Nobody should later "simplify" the delete path on the strength of the wrong reason.

No source file changes. No test changes. No `/docs` change: `docs/architecture.md:28` and
`docs/implementation-plan.md:63` already describe the shipped browser binding correctly.

---

## 2. Delete the state cookie only when the nonce actually matched

**Covers:** RC2-2 · **Severity: Minor (denial of an in-progress login; no token, no data, self-healing)**

### Verification — reachable, and lower-impact than CodeRabbit implies

`AuthenticationController.cs:36` deletes the cookie **before** `mediator.Send` at `:38`, unconditionally
and under a fixed cookie name (`GitHubAuthenticationOptions.cs:36`, `e3a_oauth_state`). The deletion is a
`Set-Cookie` on the response, so it happens whether or not the request carried the cookie and whether or
not `state` is even present. Concretely: a victim is mid-flow (cookie live, sitting on GitHub's consent
screen); the attacker gets them to make one top-level navigation to
`https://<api>/api/auth/github/callback` — no parameters needed; the response clears the victim's cookie;
the victim's real callback then lands on `OAuthStateProtector.cs:31` and redirects to
`#error=AUTHENTICATION_STATE_INVALID`.

Impact ceiling, stated plainly: **the victim's in-flight login dies and they log in again.** No token is
issued, no account is touched, nothing persists, and the attack must be re-landed inside each fresh
10-minute window. CodeRabbit files this as Major; it is Minor.

### The trade-off, stated rather than assumed

Round 1's triage deliberately specified unconditional deletion (`06-coderabbit-triage.md:74`) — "that is
the consume" — reasoning that a cookie surviving a failed callback is a replayable cookie. **That
reasoning does not survive inspection.** Under a match-then-delete rule the cookie survives *only* when
the presented nonce did not match segment 0 of the state, i.e. only on callbacks that consumed nothing.
Every callback whose nonce did match still deletes, so "the state is single-use per browser" — the
property item 1 bought — is preserved exactly. There is no security cost to pay here, only a DoS to
remove.

Two further points settle it:

- **The docs already describe the conditional behaviour.** `docs/architecture.md:28`: "the callback
  rejects the state unless the cookie matches, **then clears it**". `docs/implementation-plan.md:63`:
  "a nonce cookie the callback **compares and clears**". Both read compare-then-clear; the code clears
  first. This fix makes code and docs agree and needs **no doc edit**. Leaving it is a small standing
  divergence under `.claude/rules/docs-sync.md`.
- **It is what the framework's own OAuth handler does.** `RemoteAuthenticationHandler.ValidateCorrelationId`
  names the correlation cookie per-state, so a callback carrying an attacker's state cannot clear a
  victim's cookie. Our fixed cookie name is what opens this; gating the delete is the cheap equivalent.

### Fix (minimal shape — the implementer owns the final shape)

1. `AuthenticationRedirectResult` (`api/E3A.Application/Authentication/Shared/AuthenticationRedirectResult.cs:3`)
   gains a second member, e.g. `bool StateNonceConsumed`. It has exactly one producer
   (`CompleteGitHubLoginHandler.cs:70,75`) and one consumer, so the blast radius is two files.
2. `CompleteGitHubLoginHandler`: `false` on the two branches that consume nothing — the code-missing
   guard at `:16-19` and the `Invalid` guard at `:23-26`. `true` from `:28` onward, so `Expired`, a
   failed exchange, a failed profile fetch and success all consume. **The code-missing branch is the
   trap: it returns before `Validate` runs, so a `true` there re-opens the exact hole via a parameterless
   `/callback` hit.** Do not reorder the guards to dodge it — the `#error=` precedence contract and
   `CompleteGitHubLoginHandlerFailureTests.cs:38-44` both depend on the current order.
3. `AuthenticationController.CompleteGitHubLogin`: move `Response.Cookies.Delete(...)` from `:36` to
   after `mediator.Send`, behind `if (result.StateNonceConsumed)`. Same
   `OAuthStateCookieOptionsGenerator.Generate()` options — `Path` must stay identical or the browser will
   not match the cookie.

No handler `try`/`catch`, no new options key, no migration, no package, no Azure resource, no `/docs`
edit, no Postman change (URL, method, query params and auth mode all unchanged).

### Tests required

The controller is untested by house rule (`conventions/dotnet-testing.md:156`, `SKILL.md:898`), so the
flag must be pinned at the handler layer — which is where the branch logic actually lives:

- `Handle_ShouldNotConsumeTheStateCookie_WhenCodeIsAbsent` — `StateNonceConsumed == false`. This is the
  one that pins the trap in step 2.
- `Handle_ShouldNotConsumeTheStateCookie_WhenTheBrowserNonceDoesNotMatch` — `Validate` → `Invalid`,
  `StateNonceConsumed == false`.
- `Handle_ShouldConsumeTheStateCookie_WhenTheStateMatchesButTheExchangeFails` — `Validate` → `Valid`,
  exchange returns `null`, `StateNonceConsumed == true`. Without this one an "always false"
  implementation passes and the single-use property is silently lost.
- Success path: assert `StateNonceConsumed == true` in `CompleteGitHubLoginHandlerTests`.

Put the three new cases in a **new** file (`CompleteGitHubLoginHandlerCookieTests.cs`), not in
`CompleteGitHubLoginHandlerFailureTests.cs` — that file is already 147 lines against `SKILL.md:902`'s
~100-line rule and was flagged for it in round 1.

Expected count: 433 → ~437, never down.

---

## 3. Remove the assertion that is false against the production protector

**Covers:** RC2-6 · **Severity: Minor, but the clearest defect of the six**

### Verification — the assertion documents a false property

`api/E3A.Tests/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandlerTests.cs:44`:

    result.RedirectUrl.Should().NotContain("state-nonce");

The substitute at `:18` returns the unrelated pair `("signed-state", "state-nonce")`, so nothing named
`state-nonce` can appear in the URL and the assertion is vacuously true. With the real protector the
opposite holds: `OAuthStateProtector.cs:24,26` puts the nonce **verbatim** as segment 0 of the state, and
`OAuthStateProtectorTests.cs:29-32` pins exactly that (`segments[0].Should().Be(Nonce)`); the state then
goes into the query string at `GitHubAuthorizationUrlGenerator.cs:15`. So the nonce **is** a substring of
every real redirect URL, and line 44 asserts the reverse.

Two independent reviewers converged here: our own verifier raised it at `08-coderabbit-verify.md:105`
before CodeRabbit posted RC2-6. A test asserting something false about production is worse than no test —
the next reader will believe the nonce is kept out of the URL, which is exactly the misconception
follow-up F1 exists to correct.

### Fix

Delete line 44. Keep line 43 (`result.StateNonce.Should().Be("state-nonce")`) and the test name — that
line pins the plumbing this test exists for. Do not try to restate the real contract here: at this layer
the protector is a substitute and no true statement about the state's internal layout is available. The
true statement already lives in `OAuthStateProtectorTests.cs:29-32`.

One line, no production change, count stays 433.

---

# REJECT

### RC2-3 — "do not dismiss the concurrent first-login failure"

**Re-raise of round 1's RC8; rejected again, and the one new idea in it does not change the arithmetic.**
The race is real and unchanged: two concurrent callbacks for one brand-new GitHub account both read
`null` at `CompleteGitHubLoginHandler.cs:52`, both `AddAsync` at `:58`, and the loser violates
`IX_AspNetUsers_GitHubId` at `:66`. Round 1 rejected it because the only remedy offered was a handler
`try`/`catch`, banned by `docs/constitution.md:130` and `SKILL.md:476,510,872`.

RC2-3 does add something: "use the repository or persistence boundary" — a shape that would **not** break
the constitution's letter. Credit where due. It still does not earn a place in the final cycle: the
window is milliseconds wide and needs two separately-completed GitHub authorization flows for the same
account landing together (GitHub codes are single-use individually, so this is not a double-click); the
index does its job, so no duplicate account and no data loss; the loser gets a 500 and an immediate retry
succeeds because the row now exists. Against that, the fix means putting `DbUpdateException`
interpretation into the shared persistence boundary — new behaviour in code every other slice depends on,
introduced in the last cycle of the run with no reviewer pass left after it. Carried as **F2**.

### RC2-4 — "correct the cookie-deletion rationale" in `07-coderabbit-rework.md:84`

**Substance conceded and already ours; rejected as an anchored edit.** CodeRabbit is right that the
implementer's stated reason is wrong: `ResponseCookies.Delete(string, CookieOptions)` copies the options
and then overrides `Expires = UnixEpoch, MaxAge = null` unconditionally, so a `MaxAge` left on the options
could never have emitted `Max-Age`. Our own verifier found this first, and by experiment rather than by
reading, against a real `DefaultHttpContext` (`08-coderabbit-verify.md:50-58`) — same conclusion, with a
probe behind it. The code is correct either way and `Generate(TimeSpan? maxAge = null)`
(`OAuthStateCookieOptionsGenerator.cs:8`) remains the safer shape.

`07-coderabbit-rework.md` is a closed audit artifact; the append-only ruling that governed RC1 and RC6 in
round 1 governs it. Retroactively rewriting an implementer's stated reasoning to look correct is exactly
the erasure the trail exists to prevent. The correction is recorded **here** and folded into the PR body
by IMPLEMENT #1, which is where a future maintainer will actually read it. The exact SDK version
CodeRabbit asked for is recorded at the top of this document: **10.0.400**.

### RC2-5 — "add PKCE to the GitHub OAuth flow"

**Correct on the mechanism, correct where it corrects us, and still out of scope for this cycle.**

Verified: `GitHubAuthorizationUrlGenerator.cs:10-16` sends no `code_challenge`,
`GitHubOAuthClient.cs:19-28` sends no `code_verifier`, and `CompleteGitHubLoginHandler.cs:33` exchanges
whatever `code` arrived once the state validates. CodeRabbit also lands a correction on our own verifier:
`08-coderabbit-verify.md:104` proposes putting `SHA-256(nonce)` in the state as the hardening for this
exposure, and **that does not fix this attack** — the victim's browser supplies the matching cookie
either way, so hashing reduces what leaks without closing the code-injection path. That note must not be
relied on as a remedy; F1 supersedes it.

Why not now:

- **Not reachable without a prior leak.** The attack needs the victim's *in-flight* `state` value, which
  is never sent to the attacker: it lives in the victim's address bar, the victim's history and GitHub's
  logs. An attacker who triggers `/api/auth/github/login` in the victim's browser (link, iframe) gets an
  opaque cross-origin redirect and learns nothing. This is defence-in-depth against a leaked in-flight
  secret — unlike the login-CSRF round 1 closed, which needed no leak at all.
- **It changes the outbound contract with GitHub, in the one part of this slice nobody here can verify.**
  `pr-body.md:34` already records that the live round trip against real GitHub is unverified and needs a
  human at a consent screen. GitHub's rule is that if the authorize leg carries `code_challenge`, the
  exchange leg **must** carry a matching `code_verifier` or the exchange fails. Shipping that unverified,
  in the final cycle, with no reviewer pass after it, risks breaking a working flow to harden an
  unreachable one.
- **It is a slice, not a fix.** Verifier generation and browser-bound storage (a second cookie or an
  extended state), `S256` derivation, an `IGitHubOAuthClient.ExchangeCodeForAccessTokenAsync` signature
  change, new options, the regression test CodeRabbit rightly asks for, plus `docs/architecture.md` and
  `docs/implementation-plan.md` under `.claude/rules/docs-sync.md`. "Heavy lift" is CodeRabbit's own
  label and it is accurate.

Carried as **F1** — the strongest follow-up, and the one I would schedule first. Dev veto invited: if the
flow should be held unmerged until PKCE lands, say so and it becomes the next slice's opening item.

---

# DEV-DECISION

None new this round.

**D1 from round 1 remains open and unanswered**: what should happen when a soft-deleted GitHub account
signs in again — restore, block, or new account (`06-coderabbit-triage.md:289-310`)? Today the flow mints
a second row for that identity (option (c)), which the filtered index at
`api/E3A.Infrastructure/Data/Context/AppDbContext.cs:47` permits by design. Still unreachable in
production — nothing calls `User.MarkDeleted()` — so it does not gate this merge, but whichever answer is
chosen must land in `docs/implementation-plan.md` with it. Carried as **F3**.

---

# Carried follow-ups — this is the list that leaves with the PR

This was the final CodeRabbit cycle, so everything below ships as documented debt rather than being
fixed here.

| # | Item | From | Why it is being left |
|---|---|---|---|
| F1 | **PKCE** (`S256` challenge + browser-bound verifier) on the GitHub flow, with a regression test proving a `code` from another flow cannot complete a login. Supersedes the "hash the nonce in state" note at `08-coderabbit-verify.md:104`, which does **not** close the code-injection path. | RC2-5 | Not reachable without a leaked in-flight `state`; changes the unverifiable outbound GitHub contract; a slice in its own right. |
| F2 | Atomic first-login user creation — resolve the `IX_AspNetUsers_GitHubId` conflict at the **repository/persistence boundary**, never with a handler `try`/`catch`, so the loser gets `#error=` instead of a JSON 500. | RC2-3 / RC8 | Millisecond race, no data loss, retry succeeds; the fix changes shared persistence behaviour with no review pass left after it. |
| F3 | D1 — soft-deleted account re-login semantics (restore / block / new account), plus the `docs/implementation-plan.md` entry that must record the answer. | Round 1 | Product policy, dev's call, currently unreachable in production. |
| F4 | Split `CompleteGitHubLoginHandlerFailureTests.cs` (147 lines) and `OAuthStateProtectorTamperTests.cs` (124) against `SKILL.md:902`'s ~100-line rule. | Round 1 | Pure refactor; IMPLEMENT #2 must not make it worse — new file, not additions. |
| F5 | `api/E3A.Infrastructure/Identity/UserRepository.cs:13` (`IgnoreQueryFilters()`) is executed by no test; pinning it needs an EF InMemory package, which `conventions/dotnet-testing.md:156` and `SKILL.md:898` put out of scope. Check by hand during the smoke test. | Round 1 | House rule, not a shortfall. |
| F6 | The live round trip against real GitHub is still unverified (`pr-body.md:34`). Smoke-test before relying on the flow. | Round 1 | Needs a human at a consent screen. |
| F7 | Process learning, no code: scope containment is established from `git diff`; mtimes are supporting evidence only (`03-review-r2.md:58` overclaims and stays on the record, per append-only). | RC6 | Reviewer-prompt change for a future pipeline pass. |

---

# Constraints the rework must hold

- **Build stays 0 errors / 9 warnings**, all in `api/core-libraries`. No `E3A.*` warning may appear —
  `TreatWarningsAsErrors` is on; watch nullability if `AuthenticationRedirectResult` gains a member.
- **433 tests stay green and the count only goes up.** IMPLEMENT #2 adds ~4; IMPLEMENT #3 removes an
  assertion, not a test; IMPLEMENT #1 touches no code. Baseline re-confirmed at the top of this file.
- **No Azure resource, no new package, no migration, no new options key.** Confirmed against
  `00-acceptance.md:21`.
- **No handler `try`/`catch`** (`docs/constitution.md:130`, `SKILL.md:476,510,872`) — RC2-3 stays
  rejected, and reintroducing one would be self-defeating.
- **No closed audit artifact is edited.** `01-plan.md`, `02-implementation.md`, `03-review*.md`, `05-*`,
  `06-coderabbit-triage.md`, `07-coderabbit-rework.md` and `08-coderabbit-verify.md` are append-only.
  `pr-body.md` is the live PR description and **is** edited, by IMPLEMENT #1.
- **No `/docs` change is required this round.** `docs/architecture.md:28` and
  `docs/implementation-plan.md:63` already describe browser binding, and IMPLEMENT #2 moves the code
  *toward* their compare-then-clear wording. If the implementer's shape ends up clearing the cookie on a
  non-matching callback anyway, `docs/architecture.md:28` must change in the same commit — that would be
  a docs-sync divergence.
- **Postman needs no change.** `postman/e3a.postman_collection.json` — three auth requests, unchanged
  URLs, methods, query params and auth modes. Nothing added, removed or stale.
- Skill absolutes on every touched file: `sealed`, file-scoped namespaces, `DateTimeOffset`,
  `.ConfigureAwait(false)` outside controllers, no comments, exactly one `SaveChangesAsync` on the
  success path only.
