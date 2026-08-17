# e3a — a multi-model delivery loop

Fable 5 specifies. Opus 5 implements. Fable 5 reviews and bounces it back until
clean. Opus commits and opens a PR. CodeRabbit reviews the PR. Fable 5 triages
CodeRabbit's comments against a conventions ledger — forwarding what's worth
doing, rejecting the rest with a cited rule, and asking you about anything that
would change the feature.

One command runs all of it:

```bash
/e3a-feature "add a retry window to failed payment attempts"
```

## Why it's built this way

Subagents and skills are not competing options — in current Claude Code they've
converged, since a skill with `context: fork` + `model:` is a delegated
subagent. So the split here is by role, not by mechanism:

| Primitive | Role | Used for |
|---|---|---|
| **Subagent** (`.claude/agents/`) | **Who** — pinned model, own tool budget, fresh context | The three Fable roles |
| **Skill** (`.claude/skills/`) | **What/how** — procedure and knowledge, loaded on demand | The coordinator loop, the ledger |
| **Hook** (`.claude/hooks/`) | **Enforcement** — runs outside the model's discretion | The two gates |
| **Main session** (Opus 5) | Coordinator + implementer | Owns the loop; the only participant that can ask you a question |

Context isolation is the point of using subagents for the reviewer: a reviewer
that already watched the implementer reason is not an independent reviewer.

**Not agent teams.** They're experimental, off by default, and a teammate's idle
notification doesn't carry its output — the lead is told "teammate stopped", not
what it found. This loop is a sequential handoff, so it would stall. Leave
`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` unset.

## The loop

```
spec  ->  implement  ->  review  ->  precheck  ->  pr  ->  coderabbit  ->  triage  ->  done
 |            ^            |                                                 |
 |            +--- changes_requested (max 3 rounds)                           |
 |            +--- accepted CodeRabbit fixes (max 2 rounds) -------------------+
 |
 Fable                  Fable                                              Fable
```

State lives in `.e3a/state/<slug>.json` so the loop survives compaction.
Every phase transition rewrites it.

## The two gates

The handoffs are enforced by hooks, not by the model agreeing to comply:

- **`gate-commit.ps1`** (`PreToolUse` on Bash) — denies `git commit` while the
  newest review for the active feature says `changes_requested`. This is the
  structural half of "go back to Opus if there are issues".
- **`gate-stop.ps1`** (`Stop`) — exits 2 if any feature is mid-flight, so a
  fully-autonomous run can't end after the spec and call it done. `halt: true`
  in the state file is the escape valve, and is checked first — without it the
  hook would be an infinite loop.

## The conventions ledger

`.e3a/conventions.md` holds numbered rules (`E3A-001`, …). A rule is what lets
review feedback be **rejected with a citation** rather than argued about again
next PR. Rules are added only when a human decision creates one — by resolving
an escalation, or by promoting a `proposed` rule.

Each active rule is mirrored into `.coderabbit.yaml` under
`reviews.path_instructions`, so CodeRabbit stops raising it at all. Filtering
the same comment forever is worse than never receiving it — that mirror is what
makes the loop converge instead of running in place.

## Setup

This repo is the source of truth. Install it into a repo that has code to build:

```bash
powershell -File scripts/install-into-repo.ps1 -TargetRepo D:\Work\YourRepo -IncludeCodeRabbit
```

Add `-Link` to junction instead of copy (edits here take effect everywhere), or
`-Force` to overwrite an existing install. Re-running is idempotent: it replaces
its own hook entries rather than stacking them, and unions the permission
allowlist. Your existing `.claude/settings.json` is merged, not replaced, and
backed up to `settings.json.e3a-backup`.

Then:

1. `gh auth status` — the loop needs the GitHub CLI
2. Install the CodeRabbit GitHub App on the target repo
3. CodeRabbit CLI for the local pre-check:
   `curl -fsSL https://cli.coderabbit.ai/install.sh | sh` then `coderabbit auth login`
   and `/plugin install coderabbit`
4. Add `.e3a/state/` to the target's `.gitignore`
5. Open Claude Code there **on Opus 5** — Fable is reached through the subagents'
   `model: fable`, so the main session must be the implementer

## Commands

| Command | Does |
|---|---|
| `/e3a-feature "<description>"` | The full loop, spec through triage |
| `/e3a-triage-pr <pr#>` | Triage half only, for a PR you opened by hand |

## Autonomy

The loop runs unattended through all eight phases, including applying accepted
CodeRabbit fixes and re-pushing. It interrupts you only for:

- a triage `escalate` — a comment that would change an acceptance criterion, a
  public contract, or auth behaviour
- a reviewer `spec_conflicts` — the spec and the code disagree, which is a
  product call
- a `halt` — review rounds exhausted (3), CodeRabbit rounds exhausted (2), or
  CodeRabbit didn't respond within the timeout

## Files

```
.claude/agents/       e3a-spec, e3a-reviewer, e3a-triage   (model: fable)
.claude/skills/       e3a-feature, e3a-triage-pr, e3a-conventions
.claude/hooks/        gate-stop.ps1, gate-commit.ps1
.claude/settings.json hook wiring + permission allowlist
.e3a/conventions.md   the ledger
.e3a/{specs,reviews,triage}/   audit trail, committed
.e3a/state/           loop state, gitignored
.coderabbit.yaml      mirrors active ledger rules
```

## Editing the hooks

Two constraints, both learned the hard way on PowerShell 5.1:

- **ASCII only.** A BOM-less `.ps1` is read as ANSI, so one em-dash in a comment
  breaks the parse.
- **Never `$obj.PSObject.Properties.Name` under `Set-StrictMode`.** On an object
  with no properties — which is what `ConvertFrom-Json '{}'` gives you — it
  throws. Both hooks use a `Get-Prop` helper instead. A hook that crashes exits
  0 and silently allows the action, so this failure mode looks exactly like a
  passing test.
