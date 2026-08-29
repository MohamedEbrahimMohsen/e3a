TRIAGE: 3 to implement, 6 rejected, 1 dev-decisions

# Stage 4 — CodeRabbit triage, PR #5 (`github-oauth`)

13 inline comments (RC1–RC13) reviewed against the worktree code, not against the comment text.
They collapse into **3 implement items, 6 rejections, 1 escalation**.

Headline: **the CSRF deferral does not hold.** It was accepted on a premise I verified to be false,
and the cheap fix needs no Azure resource. That is the one Critical item.

## Downgrades — flagged for dev veto

CodeRabbit marked these Major; I am rejecting or splitting them. Surface these four:

| Comment | CodeRabbit | My call | One-line reason |
|---|---|---|---|
| RC3 | Major | **REJECT** | Options-vs-migration cap divergence is a repo-wide, deliberately chosen convention (skill §8.1, plan decision 16), not an OAuth defect. |
| RC5 | Major | **REJECT** | The requested fix is a `try`/`catch` in a handler — banned by `docs/constitution.md:130` and `SKILL.md:476,510,872`. |
| RC8 | Major | **REJECT** | Real but a millisecond-wide race with no data loss; the only fix is the same banned `try`/`catch`. |
| RC12 | Major | **SPLIT** | The 500 it describes is real and confirmed — fixed by IMPLEMENT #2. The *semantics* of a soft-deleted account signing in is a product call, escalated as DEV-DECISION D1. |

---

# IMPLEMENT

## 1. Bind the OAuth `state` to the initiating browser with a nonce cookie
**Covers:** RC7, RC9, RC10, RC13 (one issue, four anchors) · **Severity: Critical**

### Verification — the hole is real

- `api/E3A.Application/Authentication/Shared/OAuthStateProtector.cs:19-27` — `Create()` signs only
  `{nonce}.{expiry}`. Nothing ties it to a user agent.
- `api/E3A.Application/Authentication/GetGitHubLoginUrl/GetGitHubLoginUrlQueryHandler.cs:10-16` —
  returns a redirect URL only. No cookie, no side channel.
- `api/E3A.Api/Controllers/Authentication/AuthenticationController.cs:15-29` — neither action reads or
  writes `Request.Cookies` / `Response.Cookies`.
- `api/E3A.Application/Authentication/CompleteGitHubLogin/CompleteGitHubLoginHandler.cs:20-30` —
  `Validate(request.State)` takes one argument. Any browser presenting a well-signed, unexpired state
  is accepted.

Attack, concretely: the attacker starts `GET /api/auth/github/login`, authorises **their own** GitHub
account, and intercepts their own callback URL before letting it fire. They send that URL to a victim.
The victim's browser hits `/api/auth/github/callback`; the API exchanges the attacker's code, issues a
JWT for the **attacker's** account, and 302s the victim to `WebRedirectUrl#token=...`. The victim's SPA
stores it. Every engineer the victim then creates, uploads and publishes lands in the attacker's
account. Classic OAuth login-CSRF, fully open.

### Why the deferral no longer holds

Acceptance decision 2 (`00-acceptance.md:73`) chose a stateless state because "`Core.Cache` is an
empty placeholder, and a distributed cache would mean an Azure resource, which is forbidden."
**That premise does not apply to a cookie.** A `Secure`, `HttpOnly`, `SameSite=Lax` nonce cookie stores
the value in the browser — no cache, no server store, no Azure resource. `SameSite=Lax` is the correct
mode: the GitHub callback is a top-level GET navigation, so Lax cookies are sent (Strict would break
the flow). The forbidden-resource constraint is fully respected.

Plan decision 7 (`01-plan.md:62`) is also factually wrong where it calls replay "inert in practice: it
is worthless without a matching unused GitHub `code`." Supplying the unused code **is** the attack.
Two internal reviews carried that sentence forward unchallenged. This is the one place CodeRabbit
found something the internal reviews missed.

### Fix (minimal shape)

1. `IOAuthStateProtector.Create()` returns the state **and** its nonce (e.g. `OAuthState(string Value,
   string Nonce)`); `Validate(string? state, string? nonce)` compares the supplied nonce to segment 0
   with `CryptographicOperations.FixedTimeEquals`, returning `OAuthStateStatus.Invalid` on missing or
   mismatched. **Fold it into `Validate`** — no new `ErrorCodes` entry, no new `Messages.en/ar.resx`
   strings, and the failure contract stays `#error=AUTHENTICATION_STATE_INVALID`.
2. `GetGitHubLoginUrlQueryHandler` surfaces the nonce alongside the redirect URL.
3. `AuthenticationController.GetGitHubLoginUrl` appends the cookie:
   `HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, IsEssential = true, Path = "/api/auth"`,
   `MaxAge` = `StateExpirationMinutes`. Cookie name as a `GitHubAuthenticationOptions` property with a
   class default, mirroring `StateNonceSize` (skill §8.1).
4. `AuthenticationController.CompleteGitHubLogin` reads the cookie, **deletes it unconditionally**
   (success and failure alike — that is the "consume"), and passes the value on
   `CompleteGitHubLoginCommand(code, state, nonce)`.

Cookie work lives in the Api layer; handlers stay `HttpContext`-free. No migration. No new package.
Side benefit: deleting the cookie makes the state single-use *per browser*, which retires the replay
window decision 7 tried to argue away.

### Tests required

- `OAuthStateProtectorTests` — add `Validate_ShouldReturnInvalid_WhenNonceIsMissing`,
  `Validate_ShouldReturnInvalid_WhenNonceDoesNotMatch`, `Validate_ShouldReturnValid_WhenNonceMatches`.
- `CompleteGitHubLoginHandlerFailureTests` — a nonce-missing case asserting the `#error=` redirect **and**
  `DidNotReceive().ExchangeCodeForAccessTokenAsync(...)`, so the guard cannot drift below the exchange.
- **Do not** delete `api/E3A.Tests/Authentication/Shared/OAuthStateProtectorTests.cs:77-87`
  (`Validate_ShouldReturnValid_WhenTheSameStateIsValidatedTwice`), contrary to RC13's last sentence. The
  protector remains stateless by design; single-use now lives in the controller's cookie deletion, so
  that test still states the protector's true contract. Keep it.

### Docs sync — mandatory, same change

This alters a documented security policy, so per `.claude/rules/docs-sync.md` the owning doc must move
with it or the change is a blocking divergence:

- `docs/architecture.md:28` currently reads "The anti-CSRF `state` is stateless: a nonce plus an expiry,
  HMAC-signed with the JWT key, so no cache and no extra Azure resource are needed." After the fix the
  state is **browser-bound via a nonce cookie**, still with no server store and no Azure resource.
  Rewrite that clause. The "never a cookie" phrase earlier in the same line is about the **token** and
  stays true — do not delete it; make clear the nonce cookie is not the token.
- `docs/implementation-plan.md:63` — "302 to GitHub with a stateless signed anti-CSRF `state`" needs the
  same one-word correction.

Postman needs no change: `GitHub Callback` in `postman/e3a.postman_collection.json` already cannot be
exercised without a real GitHub `code`.

---

## 2. Make the local `UserName` collision-safe on first GitHub login
**Covers:** RC4, and the crash half of RC12 · **Severity: Major**

### Verification — one defect, two triggers, confirmed against the real snapshot

`api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs:500-503`:

    b.HasIndex("NormalizedUserName")
        .IsUnique()
        .HasDatabaseName("UserNameIndex")
        .HasFilter("[NormalizedUserName] IS NOT NULL");

`UserNameIndex` is unique and **not** filtered on `IsDeleted`. The `GitHubId` index *is*
(`api/E3A.Infrastructure/Data/Context/AppDbContext.cs:47`,
`api/E3A.Infrastructure/Data/Migrations/20260829112516_oauth004.cs:40-45`). That asymmetry is the bug.

`api/E3A.Domain/Identity/User.cs:52-53` copies the GitHub login straight into `UserName` and
`NormalizedUserName`. So:

- **RC12 trigger.** A soft-deleted GitHub user signs in again. The global filter
  (`AppDbContext.cs:95`) hides the row, so `CompleteGitHubLoginHandler.cs:51` gets `null`. The filtered
  `GitHubId` index does **not** block the insert (the old row has `IsDeleted = 1`). `UserNameIndex`
  does. `SaveChangesAsync` (`CompleteGitHubLoginHandler.cs:64`) throws `DbUpdateException`, which
  reaches `api/core-libraries/Core.Exceptions/ExceptionMiddleware.cs:28,50-51` and is written as
  **`application/json`, HTTP 500** — to a browser mid-OAuth-redirect. RC12's causal chain is precise,
  including which index fires.
- **RC4 trigger.** A pre-existing user row (acceptance decision 4 keeps seeded owner rows) whose
  `NormalizedUserName` equals an arriving GitHub login. Same insert, same index, same JSON 500.

Both break acceptance decision 10 (`00-acceptance.md:81`) and the callback contract the PR body and
`docs/implementation-plan.md:63` promise ("every failure returning `#error=<ERROR_CODE>`"). Round 1's
internal review already named this a "latent plan-sanctioned 500"; CodeRabbit has now given it two
concrete triggers.

Reachability today: no production code calls `User.MarkDeleted()` (`api/E3A.Domain/Identity/User.cs:65`
— the only other mention is `api/E3A.Tests/Engineers/EngineerTests.cs:140`), and there is no seeder in
the repo. So this is latent, not live. That is why it is Major, not Critical.

### Why this fix and not RC12's or RC5's

This is not a CodeRabbit preference — the skill prescribes the fix by name. **`SKILL.md` §8.3:**
"Unique-slug pattern: repository `Is...ExistsAsync` + suffix loop — never throw Conflict for an
auto-resolvable collision." A username collision on a GitHub login is exactly an auto-resolvable
collision, and `api/E3A.Application/Engineers/Shared/EngineerSlugResolver.cs:11-24` is the working
in-repo precedent.

### Fix

- Add `Task<bool> IsUserNameExistsAsync(string normalizedUserName, CancellationToken)` to
  `api/E3A.Domain/Identity/IUserRepository.cs`, implemented in
  `api/E3A.Infrastructure/Identity/UserRepository.cs` **with `IgnoreQueryFilters()`** — the soft-deleted
  row still holds the index, so a filtered check would miss it and the collision would come straight
  back. This does not violate §8.5: that rule bans ad-hoc `!x.IsDeleted` predicates in queries, and this
  is the deliberate inverse, confined to one named repository method that must see what the DB index sees.
- Add a `UserNameResolver` in `api/E3A.Application/Authentication/Shared/` mirroring
  `EngineerSlugResolver`: return the login if free, else loop `generator.Generate(prefix:, size:)`
  (§8.2 — `IGenerator`, never `Random`) until free. Suffix size as a `GitHubAuthenticationOptions`
  property with a class default, mirroring `EngineersOptions.SlugSuffixSize` (§8.1).
- `User.CreateFromGitHub` takes the resolved `userName` as a parameter. `GitHubLogin` keeps the raw
  GitHub login — the two fields are now allowed to differ, which is correct.

No migration, no schema change, no new package.

### Tests required

- `UserTests` — `CreateFromGitHub` sets `UserName`/`NormalizedUserName` from the **resolved** name, not
  from the login, when the two differ.
- New `UserNameResolverTests` — returns the login when free; returns a suffixed name when
  `IsUserNameExistsAsync` returns `true` then `false`; the loop terminates.
- `CompleteGitHubLoginHandlerTests` — a substitute returning `true` for the login and `false` for the
  suffixed candidate must produce an `AddAsync` carrying the suffixed `UserName`, with `Received(1)` on
  `SaveChangesAsync`.

---

## 3. Escape the `||` operators in the two broken plan tables
**Covers:** RC2 · **Severity: Minor**

Verified accurate. `.process/github-oauth/01-plan.md:129` (`if (userId == null || userId ==
Guid.Empty)`) and `:344` (`if (profile.Id <= 0 || string.IsNullOrWhiteSpace(profile.Login))`). GFM
splits table cells on the pipe **even inside backticks**; markdownlint MD056 reports line 129 as 6 cells
against an expected 4, with "extra data will be missing". Two approved decision rows render wrong today.

Round 2 ruled that `01-plan.md` must not be edited to match later work — that ruling stands and I apply
it to RC1 below. It does not reach here: escaping a pipe changes zero words of the approved content,
and the current state *hides* part of it. This makes the record legible, not different.

**Fix:** escape the two pipes on those two lines. Nothing else in the file.

---

# REJECT

### RC1 — rewrite plan decision 7's "state replay is inert" wording

**Substance conceded, anchor rejected.** CodeRabbit is right that `01-plan.md:62` is factually wrong
(see IMPLEMENT #1). But `01-plan.md` is the **approved, signed-off artifact**; round 2 already ruled
that editing it to match later knowledge erases the gap the audit trail exists to show. The trail is
append-only. The correction is recorded here and belongs in `02-implementation.md` and the PR body —
and IMPLEMENT #1 removes the risk outright, so the residual-risk paragraph CodeRabbit wants would be
describing a hole that no longer exists.

### RC3 — pin column lengths to migration-owned constants

**Rejected on repo convention.** The claim is technically true: `AppDbContext.cs:44-46` reads mutable
`GitHubAuthenticationOptions` while `20260829112516_oauth004.cs:13-38` writes fixed
`nvarchar(100/200/500)`. But it is neither new nor OAuth-specific — `ConfigureEngineers`
(`AppDbContext.cs:57-67`) and `ConfigureItemVersions` (`AppDbContext.cs:79-83`) do exactly the same with
`EngineersOptions` and `PublishingOptions` in already-merged slices. It is the house pattern, stated
three times: `SKILL.md` §8.1 ("caps live in Options, never as entity constants"),
`docs/implementation-plan.md:41`, and plan decision 16 (`01-plan.md:73`). "Changing a cap requires a
migration" is true of every EF `HasMaxLength` and is normal EF workflow. CodeRabbit's alternative —
startup validation across every options class — is a repo-wide change that does not belong in an OAuth
PR. If the dev wants it, it is its own slice.

### RC5 — map `SaveChangesAsync` failures to the `#error=` redirect

**Rejected on a constitution-level rule.** The only way to do this is a `try`/`catch` around
`SaveChangesAsync` in `CompleteGitHubLoginHandler.cs:64`. That is banned outright:
`docs/constitution.md:130` ("no `try`/`catch` in handlers"), `SKILL.md:476`, `:510`, and the §9
checklist at `:872`. Plan decision 11 (`01-plan.md:66`) rejected it on exactly this ground and the
reasoning still holds. Note also that decision 19 already puts the `try`/`catch` where it belongs — in
the Infrastructure adapter (`api/E3A.Infrastructure/Authentication/GitHubOAuthClient.cs`) — so every
*outbound* failure already redirects.

After IMPLEMENT #2 the only remaining `SaveChangesAsync` failure is genuine infrastructure outage, where
a 500 is the honest answer and matches every other endpoint in the solution. If the dev still wants full
fragment coverage, the correct mechanism is content negotiation in `CoreExceptionMiddleware`, not a
handler `try`/`catch` — a `core-libraries` change, out of scope for this run.

### RC6 — do not present mtimes as proof of scope containment

**Substantively correct; rejected as a doc edit.** `03-review-r2.md:58` does overclaim: "This is
stronger evidence than a diff, because it also rules out a change-and-revert that a diff would hide."
It is not — mtimes do not survive a fresh clone or a restore, and what actually ruled out the
change-and-revert was the `md5sum`/`cmp` content check the same reviewer ran and reported at
`03-review-r2.md:38-40`. The conclusion is sound; the evidentiary claim attached to it is not.

But `03-review-r2.md` is a closed audit-trail artifact, and the same append-only principle that governs
RC1 governs it. Retroactively editing a review to look more rigorous is worse than leaving the
overclaim on the record next to this correction. Recorded here instead, as a process learning: **scope
containment is established from `git diff`; mtimes are supporting evidence only.** Worth folding into
the reviewer prompt in a future pipeline pass — no code defect either way.

### RC8 — make first-time user creation atomic

**Real, but rejected on cost and on the same banned pattern.** Two concurrent callbacks for the same
brand-new GitHub account both read `null` at `CompleteGitHubLoginHandler.cs:51`, both `AddAsync`, and
the loser violates `IX_AspNetUsers_GitHubId` (`20260829112516_oauth004.cs:40-45`). Confirmed possible.
But the window is milliseconds wide and requires two *separate completed* GitHub authorization flows
for one account landing simultaneously — GitHub codes are single-use, so it is not a double-click. The
loser gets a 500 and a retry succeeds, because the row now exists. No data loss, no security
consequence, no duplicate account (the index does its job). The remedy CodeRabbit names — "catch the
unique-constraint conflict" — is the handler `try`/`catch` banned by `docs/constitution.md:130`. Not
worth breaking a constitution rule for.

### RC11 — synchronise `GitHubLogin` for returning users

**Rejected: contradicts a binding dev answer.** Acceptance decision 5 (`00-acceptance.md:76`) is
explicit and proxied from the dev: "Update display name and avatar on every login." It enumerates two
fields; login is not one of them. `User.UpdateGitHubProfile` (`api/E3A.Domain/Identity/User.cs:58-63`)
implements it literally, per plan decision 14 (`01-plan.md:69`), whose reasoning is sound: `UserName`
carries a unique Identity index and drives publish attribution
(`api/E3A.Application/Publishing/ProcessPublishJob/ProcessPublishJobHandler.cs:67`,
`api/E3A.Application/Publishing/RegenerateMarketplace/RegenerateMarketplaceHandler.cs:62`), so rewriting
identity fields on every login adds a collision failure mode for no in-scope benefit.

CodeRabbit is right that GitHub usernames are mutable and that `/api/auth/me` can therefore return a
stale `GitHubLogin`. Minor: nothing keys off it, and matching is by numeric id (decision 4), so the
account is never lost. **One honest caveat for the next slice:** IMPLEMENT #2 lets `UserName` and
`GitHubLogin` diverge, which retires decision 14's "GitHubLogin therefore stays equal to UserName"
rationale. That makes RC11 marginally stronger next time — but it does not override a binding dev
answer today.

---

# DEV-DECISION

### D1. What should happen when a soft-deleted GitHub account signs in again?

**From RC12.** IMPLEMENT #2 stops the 500 — but it then makes the flow silently mint a **second** user
row for the same GitHub identity, which the filtered index at `AppDbContext.cs:47` permits by design.
Whether that is right depends on what soft-delete *means* for a user, and the repo has not decided:
nothing in production calls `User.MarkDeleted()` (`api/E3A.Domain/Identity/User.cs:65`), and no doc in
`/docs` defines the semantics.

Three answers, all buildable without an Azure resource:

- **(a) Restore.** Look up by `GitHubId` with `IgnoreQueryFilters()`, clear `IsDeleted`, refresh the
  profile. Treats deletion as "deactivated". Keeps history and prior engineer ownership.
- **(b) Block.** Return `#error=ACCOUNT_DISABLED` (new `ErrorCodes` entry plus `Messages.en.resx` and
  `Messages.ar.resx` strings). Treats deletion as a ban. Necessary if soft-delete will ever mean
  "banned" — otherwise a banned creator re-signs in and is back.
- **(c) New account.** The behaviour after IMPLEMENT #2. Treats deletion as "the user left"; a returning
  user starts clean and loses their old engineers.

I have not picked one because (b)-vs-(c) is a ban-evasion policy question, not an engineering one, and
picking wrong is worse than asking. **Whichever is chosen, `docs/implementation-plan.md` must record it**
— it is a scope/policy statement and `.claude/rules/docs-sync.md` makes divergence blocking. Nothing
here is urgent: the state is currently unreachable in production, so it can ship as a follow-up slice.

---

# Constraints the rework must hold

- **No Azure resource.** Neither implement item needs one. The nonce cookie lives in the browser; the
  username resolver is a DB read. Confirmed against the standing prohibition at `00-acceptance.md:21`.
- **Build stays 0 errors / 9 warnings**, all in `api/core-libraries`. No `E3A.*` warning may appear —
  `TreatWarningsAsErrors` is on, so watch nullability on the new nonce parameter.
- **420 tests stay green** and the count only goes up. IMPLEMENT #1 adds ~4, IMPLEMENT #2 adds ~5.
- **No migration.** Neither fix changes the schema.
- Skill absolutes hold on every touched file: `sealed`, file-scoped namespaces, `DateTimeOffset`,
  `.ConfigureAwait(false)`, no comments, no `try`/`catch` in handlers, exactly one `SaveChangesAsync` on
  the success path only.
- `postman/e3a.postman_collection.json` needs no change — the API surface (three requests, correct
  methods, `noauth`/`inherit` modes) is unchanged by both fixes.
