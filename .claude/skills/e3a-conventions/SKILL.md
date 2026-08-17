---
name: e3a-conventions
description: The e3a conventions ledger — the numbered rules that decide how this codebase does things, used to accept or reject review feedback. Load before triaging review comments or arguing about house style.
user-invocable: false
allowed-tools: Read Grep Glob
---

# The conventions ledger

The ledger lives at `.e3a/conventions.md` in the repository root. Read it now:

```
Read .e3a/conventions.md
```

If the file does not exist, the ledger is empty. That is a valid state on a new
repo — it means no rule decides anything yet, so every stylistic disagreement is
either accepted or escalated, never rejected.

## How to read a rule

```markdown
## E3A-001 — Handlers return Result<T>; exceptions are for the unexpected only
Status: active
Since: 2026-08-17
Source: seed
Rationale: expected failures are part of the caller's contract and must be typed.
Rejects: "wrap in try/catch and rethrow", "throw a domain exception here"
```

- **`Status: active`** — this rule decides. Cite it to reject feedback.
- **`Status: proposed`** — suggested by triage, not yet confirmed by the human.
  A proposed rule **cannot** be cited to reject anything. It exists so the human
  can confirm or discard it.
- **`Status: retired`** — no longer applies. Never cite. Kept so old PR replies
  that reference the ID still resolve to something.
- **`Rejects`** — paraphrases of feedback this rule turns away. Use it for
  matching, not as an exhaustive list.

## How rules are used

- **Rejecting review feedback** requires an `active` rule ID. Feedback that
  contradicts an active rule is rejected with that ID cited in the reply, so the
  PR records *why* rather than showing a silently ignored comment.
- **Writing a spec** must not contradict an active rule. If the request does,
  that goes in the spec's Open questions with the rule ID.
- **Reviewing code** must not raise anything an active rule already settles.

## How rules are added

A rule is added only when a human decision creates one — either by resolving an
escalation, or by confirming a `proposed` rule. Rules are never invented by an
agent acting alone.

Numbering: take the highest existing `E3A-nnn` and add one. **Never reuse or
renumber an ID** — PR comments cite these IDs and must stay resolvable. Retire,
don't delete.

## Mirroring to CodeRabbit

Filtering the same comment forever is worse than never receiving it. When a rule
becomes `active`, its substance is mirrored into `.coderabbit.yaml` under
`reviews.path_instructions`, so CodeRabbit stops raising it at the source. A
rule that is active in the ledger but absent from `.coderabbit.yaml` is a bug in
the loop.
