---
name: e3a-feature
description: Run the full e3a delivery loop for one feature — Fable writes the spec, Opus implements, Fable reviews until clean, then commit, PR, CodeRabbit, and Fable triage of CodeRabbit's comments against the conventions ledger.
argument-hint: "<feature description>"
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Bash(coderabbit *) Read Write Edit Grep Glob Agent
---

# e3a delivery loop

You are the **coordinator and the implementer**. You own the loop, you write the
code, and you are the only participant allowed to ask the human a question. The
Fable subagents specify, review, and triage; they cannot edit files and cannot
reach the human except through you.

Feature request: **$ARGUMENTS**

## Non-negotiables

1. **Every `Agent` call passes `run_in_background: false`.** Subagents default to
   background and return a turn later; this loop is a sequential handoff and
   will stall if you background any step.
2. **Every delegation prompt is self-contained.** Subagents get no conversation
   history. Pass the slug, round, and every file path explicitly, every time.
3. **Write the state file after every phase transition, before doing the next
   thing.** The state file is what lets this loop survive compaction, and what
   the `Stop` hook reads to decide whether the session may end.
4. **Never edit `.e3a/reviews/*` or `.e3a/triage/*` to change a verdict.** Those
   are records. If you disagree with a verdict, escalate; do not overwrite it.
5. **You may not mark `phase: done` while `blocking` items are unresolved.**

## State file

`.e3a/state/<slug>.json`, rewritten in full at each transition:

```json
{
  "slug": "payment-retry",
  "phase": "review",
  "round": 2,
  "cr_round": 0,
  "base": "main",
  "branch": "feat/payment-retry",
  "spec": ".e3a/specs/payment-retry.md",
  "pr": null,
  "halt": false,
  "halt_reason": null,
  "updated": "2026-08-17T14:02:00Z"
}
```

`phase` is one of `spec`, `implement`, `review`, `precheck`, `pr`, `coderabbit`,
`triage`, `done`.

`halt: true` stops the loop and releases the `Stop` hook. Set it only for the
reasons listed below, always with a `halt_reason`, and always tell the human why
in the same turn.

## Autonomy

Run phases 1–8 without stopping. Apply accepted CodeRabbit fixes and re-push
without asking. Interrupt the human **only** for:

- a `spec_conflicts` entry from the reviewer
- an `escalate` decision from triage
- any `halt`

Everything else is yours to decide.

---

## Phase 1 — `spec`

Derive `slug` from the request (kebab-case, ≤ 4 words). Determine `base` from
the repo's default branch. Write the state file with `phase: "spec"`.

Delegate:

> Agent `e3a-spec`, `run_in_background: false`, prompt containing: `slug`,
> the verbatim feature request, `spec_path` = `.e3a/specs/<slug>.md`,
> `ledger_path` = `.e3a/conventions.md`.

Read the returned JSON. If `open_questions` is non-empty **and** any of them
would change what gets built, ask the human with `AskUserQuestion`, then append
the answers to the spec file under a `## Decisions` heading. Cosmetic or
already-answerable questions: resolve them yourself and note the resolution.

Create the branch `feat/<slug>` off `base`. Set `phase: "implement"`, `round: 1`.

## Phase 2 — `implement`

Read `.e3a/specs/<slug>.md` in full, plus the ledger.

Implement every `AC-n`, reusing what the spec's **Reuse** section names. Follow
the ledger. Match the surrounding code's idiom.

On rounds 2+, you also receive the previous round's `blocking` list. Fix every
item. If you believe an item is wrong, do **not** silently skip it — implement
your alternative and say so in the next delegation prompt so the reviewer can
check the claim.

Then run the project's build, tests, and analyzers. Fix failures and re-run
until green. **Never hand a red build to review** — a reviewer spending its
round on a compile error is a wasted round.

Set `phase: "review"`.

## Phase 3 — `review`

Delegate:

> Agent `e3a-reviewer`, `run_in_background: false`, prompt containing: `slug`,
> `round`, `spec_path`, `ledger_path` = `.e3a/conventions.md`, `base`, and on
> rounds 2+ `prior_review` = `.e3a/reviews/<slug>-r<round-1>.json` plus any
> disagreement you noted in phase 2.

Write the verdict verbatim to `.e3a/reviews/<slug>-r<round>.json`.

Then branch:

- `spec_conflicts` non-empty → **ask the human** with `AskUserQuestion`. Spec vs
  code is their call, not yours. Apply the answer to the spec, then return to
  `implement` at the same round.
- `verdict: "changes_requested"` and `round < 3` → `round++`, back to
  `implement` carrying the `blocking` list.
- `verdict: "changes_requested"` and `round == 3` → set `halt: true`,
  `halt_reason: "review_rounds_exhausted"`, and show the human the remaining
  blockers. Three rounds without convergence means something is wrong with the
  spec or the approach, and burning a fourth will not fix it.
- `verdict: "pass"` → `phase: "precheck"`.

`nonblocking` items: apply the ones that are quick and clearly right; ignore the
rest. They never gate the loop.

## Phase 4 — `precheck`

Run CodeRabbit against the local diff before the PR exists — it is far cheaper
than a PR round trip:

```bash
coderabbit review --agent --base main
```

Apply findings that are real defects. For findings that look like house-style
disagreements, check the ledger first and drop anything an active rule already
settles. Re-run build and tests.

If the `coderabbit` CLI is missing or unauthenticated, note it and continue —
this phase is an optimisation, not a gate.

Set `phase: "pr"`.

## Phase 5 — `pr`

Commit with a message whose body references the criteria implemented
(`Implements AC-1..AC-6`). Push the branch. Open the PR with `gh pr create`,
body summarising intent, the criteria, and the review rounds it took.

Record `pr` in the state file. Set `phase: "coderabbit"`.

> The `gate-commit` hook blocks `git commit` while the latest review for this
> slug says `changes_requested`. If a commit is denied, that is the gate working
> — go back to `implement`, do not try to route around it.

## Phase 6 — `coderabbit`

Poll for CodeRabbit's review of the PR. Check roughly every 60s, up to ~15
minutes. Do not sleep in the foreground for the whole window — poll, and do
useful work or wait between checks.

```bash
gh api repos/{owner}/{repo}/pulls/{pr}/reviews --paginate --jq '[.[]|select(.user.login=="coderabbitai[bot]")|{id,body,submitted_at}]'
```

```bash
gh api repos/{owner}/{repo}/pulls/{pr}/comments --paginate --jq '[.[]|select(.user.login=="coderabbitai[bot]")|{id,path,line,body,html_url}]'
```

On rounds 2+, keep only comments newer than the previous triage timestamp.

Write the combined payload to `.e3a/triage/pr-<pr>-input-<cr_round>.json`.

- Nothing after the timeout → `halt: true`,
  `halt_reason: "coderabbit_timeout"`, tell the human the PR is open and
  unreviewed.
- No comments at all (CodeRabbit approved) → `phase: "done"`.
- Comments present → `phase: "triage"`.

## Phase 7 — `triage`

Delegate:

> Agent `e3a-triage`, `run_in_background: false`, prompt containing: `pr`,
> `comments_path`, `spec_path`, `ledger_path`, `base`.

Write the result verbatim to `.e3a/triage/pr-<pr>.json`. Then act on each
decision:

**`accept`** — implement the `instruction`. Group them into one pass, re-run
build and tests. Reply on each accepted thread saying what changed:

```bash
gh api repos/{owner}/{repo}/pulls/{pr}/comments/{comment_id}/replies -f body="Applied — <what changed>."
```

**`reject`** — post the reason with the rule cited, then resolve the thread.
Rejections are **posted, never silently dropped**; the PR should record why:

```bash
gh api repos/{owner}/{repo}/pulls/{pr}/comments/{comment_id}/replies -f body="Not applying — <reason> (convention E3A-nnn)."
```

**`escalate`** — collect all escalations and ask the human **once**, with
`AskUserQuestion`, using each decision's `question` and `options`. For every
answer:

1. Apply it (implement, or amend the spec if it changes an `AC-n`).
2. Append a new `E3A-nnn` rule to `.e3a/conventions.md` with `Status: active`
   and `Source: escalation PR #<pr>`, so the same argument never recurs.
3. Mirror the rule into `.coderabbit.yaml` under `reviews.path_instructions`.
4. Reply on the thread with the outcome.

**`new_rules_proposed`** — append each to the ledger with `Status: proposed`.
Do not mirror proposed rules to `.coderabbit.yaml`, and do not cite them.

Then: if anything was implemented, commit and push to the same PR, `cr_round++`,
and return to `coderabbit` for one more pass. **Cap at `cr_round == 2`** — after
two CodeRabbit rounds set `halt: true`,
`halt_reason: "coderabbit_rounds_exhausted"`.

If nothing was accepted (all rejected), skip straight to `done`.

## Phase 8 — `done`

Set `phase: "done"` and report:

- PR link and branch
- review rounds used, and what round 1 blocked on
- CodeRabbit: accepted / rejected / escalated counts
- new ledger rules added, and whether they were mirrored to `.coderabbit.yaml`
- anything left unresolved

Report this faithfully. If tests are failing, say so with the output. If a phase
was skipped, say which and why.
