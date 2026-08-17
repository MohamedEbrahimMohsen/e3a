# e3a conventions ledger

The numbered rules that decide how this codebase does things. A rule here is
what lets review feedback be **rejected with a citation** instead of argued
about again on the next PR.

## How this file is used

- `Status: active` — decides. Cite the ID when rejecting feedback.
- `Status: proposed` — suggested by triage, not yet confirmed by a human.
  **Cannot be cited to reject anything.**
- `Status: retired` — no longer applies. Never cite. Kept so old PR replies that
  reference the ID still resolve.

Rules are added only when a human decision creates one — by resolving an
escalation, or by promoting a `proposed` rule to `active`. Take the highest
existing number and add one. **Never reuse or renumber an ID**, and never delete
a rule; retire it. PR comments cite these IDs and must stay resolvable.

When a rule becomes `active`, mirror its substance into `.coderabbit.yaml` under
`reviews.path_instructions`. Filtering the same comment forever is worse than
never receiving it.

---

<!-- Seed rules. Replace these with your actual conventions — they are examples
     of the shape, not claims about your codebase. Delete any that do not apply
     rather than leaving them active and wrong. -->

## E3A-001 — Rejections cite a rule; opinions escalate
Status: active
Since: 2026-08-17
Source: seed
Rationale: a rejection without a cited rule is an opinion wearing a uniform. If
no rule decides it, the honest moves are to accept it or to ask the human, and
then write the rule so the question is settled next time.
Rejects: "reject this, it doesn't match our style" (with no rule ID)

## E3A-002 — Review feedback whose premise is factually wrong is rejected, not applied
Status: active
Since: 2026-08-17
Source: seed
Rationale: automated reviewers are confidently wrong often enough that applying
unverified claims introduces bugs. Verify the claim against the code before
deciding, and say "premise is incorrect" when it is.
Rejects: applying a suggested fix without first checking the behaviour it describes

## E3A-003 — Public behaviour changes need a human, never a reviewer's suggestion alone
Status: active
Since: 2026-08-17
Source: seed
Rationale: status codes, wire contracts, schemas, and auth semantics are product
decisions. A review comment is not authorisation to change one, however
reasonable the argument.
Rejects: "change this endpoint to return 422 instead of 409"

## E3A-004 — Tests assert observable behaviour, not implementation shape
Status: active
Since: 2026-08-17
Source: seed
Rationale: tests coupled to internal structure fail on every refactor and stop
being read. Assert what a caller can see.
Rejects: "assert this private method was called", "add a mock verification here"
