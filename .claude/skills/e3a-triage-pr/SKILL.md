---
name: e3a-triage-pr
description: Triage CodeRabbit's comments on an existing PR against the e3a conventions ledger, then apply, reject with a cited rule, or escalate. Use on a PR that was not created by the /e3a-feature loop.
argument-hint: "<pr-number>"
disable-model-invocation: true
allowed-tools: Bash(git *) Bash(gh *) Read Write Edit Grep Glob Agent
---

# Triage CodeRabbit on PR #$ARGUMENTS

The triage half of the e3a loop, standalone — for PRs that already exist,
including ones you opened by hand. There is no spec file and no review rounds
here; everything else works the same way.

## Steps

1. **Locate the PR.** `gh pr view $ARGUMENTS --json number,headRefName,baseRefName,url,title`.
   Check out the head branch if you are not already on it.

2. **Fetch CodeRabbit's comments.**

   ```bash
   gh api repos/{owner}/{repo}/pulls/$ARGUMENTS/reviews --paginate --jq '[.[]|select(.user.login=="coderabbitai[bot]")|{id,body,submitted_at}]'
   ```

   ```bash
   gh api repos/{owner}/{repo}/pulls/$ARGUMENTS/comments --paginate --jq '[.[]|select(.user.login=="coderabbitai[bot]")|{id,path,line,body,html_url}]'
   ```

   Write the combined payload to `.e3a/triage/pr-$ARGUMENTS-input.json`. If
   CodeRabbit has not reviewed the PR yet, say so and stop.

3. **Delegate to the triage agent.** Agent `e3a-triage` with
   `run_in_background: false`, passing `pr`, `comments_path`, `ledger_path` =
   `.e3a/conventions.md`, `base` = the PR's base branch, and
   `spec_path: null` — tell it explicitly that there is no spec, so
   "would change an `AC-n`" is not available as an escalation trigger. In its
   place, escalate anything that changes **observable behaviour** of the PR.

4. **Write** the verdict verbatim to `.e3a/triage/pr-$ARGUMENTS.json`.

5. **Act on the decisions** exactly as in `/e3a-feature` phase 7:
   - `accept` → implement the `instruction`, re-run build and tests, reply on
     the thread with what changed.
   - `reject` → reply citing the rule ID, then resolve the thread. Never drop a
     comment silently.
   - `escalate` → ask the human once with `AskUserQuestion`, apply the answers,
     append a new `E3A-nnn` rule with `Status: active` and
     `Source: escalation PR #$ARGUMENTS`, and mirror it into `.coderabbit.yaml`.
   - `new_rules_proposed` → append with `Status: proposed`; do not mirror, do
     not cite.

6. **Push** if anything changed, then report: accepted / rejected / escalated
   counts, new rules, and anything left open.

Do not run a second triage round automatically — one pass, then hand back.
