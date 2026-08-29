# Autonomous Run Brief — 2026-08-28

**Written before a `/compact`. This file, not conversation memory, is the source of truth.**
If you are an agent resuming this work: read this file first, then `docs/HANDOFF.md`.

## Authorization (dev, verbatim)

> go ahead and do the fearures 1, 2, 3 and 6, regarding the feature 4 & 5 skip it for now.
> but I will go to slepp now, so ask all your questions or your requirments now and when you're
> ready to go, don't ever stop by any mean untill you finished, I grant you all the permissions to
> commit, create PR, merge PR, anything will not block the implementation do it unless you needed
> to create any resource in Azure, that's only my job.

- **Granted:** commit, push, open PR, merge PR, all pipeline gates proxied.
- **FORBIDDEN:** creating any Azure resource. That is the dev's job alone.
  (None of the four features need one — storage account, `publish-jobs` queue, and the
  `drafts` / `snapshots` / `public` containers all already exist.)
- **Do not stop** until all four features are merged.

## Build order — do not reorder

1. **Security scanner** — blocks the first real publish
2. **GitHub OAuth**
3. **Teams**
4. **Frontend auth surfaces**

**Skipped by dev decision:** install counting (feature 4) and reports/abuse (feature 5).

## Dev's answers (asked and answered before he slept)

| # | Question | ANSWER |
|---|----------|--------|
| 1 | Team vs engineer slug collision | **Option (b): `e3a-team-{slug}` prefix.** Dev chose this explicitly over the shared-namespace option the orchestrator recommended. Teams get a `team-` segment; engineers stay `e3a-{slug}`. Collision becomes structurally impossible. |
| 2 | GitHub App callback URL | **Confirmed** as registered: `https://localhost:62935/api/auth/github/callback` — matches `appsettings.json` already. |
| 3 | Token delivery to browser | **URL fragment.** API redirects to `/auth/callback#token=…`. Never sent to a server, never in logs. Not an httpOnly cookie. |
| 4 | Workspace flow | **Confirmed:** create engineer → upload `.claude` zip → review import manifest → publish → poll status. The old compose-from-parts mock design is superseded. |

## Proxied decisions (dev did not object)

- Scanner rule tiers and thresholds follow `docs/security-scan.md`
- A `Rejected` version burns its version number, same as `Failed`
- First OAuth login creates the user record just-in-time
- Seeded engineers keep their existing owner rows; no migration
- Team members pin to an exact `ItemVersion`
- 10 teams per creator (from `docs/implementation-plan.md`)

## Known constraint

**OAuth cannot be verified end to end without the dev** — the flow needs a human at a GitHub
consent screen. Build it, unit-test it, verify everything up to the redirect, and say plainly in the
report that the live round-trip is unverified.

## Process rules in force (unchanged)

- Every feature goes through the full `/feature` pipeline: Stage 0 acceptance (proxied) → plan →
  gate (proxied) → implement → review → PR → CodeRabbit triage/fix/verify → merge.
- Artifacts in `.process/<slug>/`; metrics appended per stage; every proxied call recorded for veto.
- Models: **OPUS 5** for all stages (dev's standing instruction; Opus 4.8 is not selectable).
- House rules bind: `docs/constitution.md`, `.claude/skills/dotnet-feature/SKILL.md` §8 DO/DON'T,
  `.claude/rules/docs-sync.md`, `conventions/dotnet-testing.md`.
- Postman sync is blocking. Docs divergence is blocking; incompleteness is never flagged.
- Build check must use `dotnet build api/E3a.slnx --no-incremental` — a plain incremental build
  misleadingly reports 0 warnings because `core-libraries` is not recompiled. Baseline: 0 errors,
  9 pre-existing `core-libraries` warnings.
- CodeRabbit polling must distinguish *absent* from *not yet* — its walkthrough posts within seconds
  saying "Currently processing". Wait for inline comments, a review object, or "Actionable comments
  posted". This has caused two near-misses already.

## State at time of writing

`main` @ `8f66d7f`, tree clean. Merged: Engineers CRUD · upload + import manifest · public catalog ·
creator-typed slug · publish pipeline. **354 tests passing.**

## Carried debts (not in scope, do not fix silently)

Preserve-by-default normalizer fix (unknown folders dropped today) · `.gitignore:20` `publish/`
breaking fresh clones (spawned as its own task) · `Core.Utilities.IGenerator` trailing separator ·
DD1 marketplace-regeneration concurrency · DD2 · `03-review.md` follow-ups N1, N3, N5–N9 ·
the `AuditBehaviour` landmine documented in `.process/publish-pipeline/04-metrics.md`.

## Still owed by the dev (non-blocking)

Domain confirmation (`e3a.dev` assumed) · regenerate the GitHub client secret before the repo is
public · a manual smoke test of the queue payload round-trip before the first real publish.
