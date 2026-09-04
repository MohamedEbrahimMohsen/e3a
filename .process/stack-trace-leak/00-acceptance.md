# Stage 0 — Workflow acceptance

**Date:** 2026-09-04 23:08
**Slice:** stack-trace-leak (1 of 7 in an unattended queue)

## Feature request (from the dev)

Run the remaining e3a completion points through the /feature pipeline. This slice: the
error contract leaks a full .NET stack trace and absolute source paths to the client on
every error, in every environment.

## Pipeline terms accepted

- Stage 1 plan / Stage 2 implement / Stage 3 review all on **Opus 5** (the dev asked for
  one model across all three stages; `.claude/agents/*.md` updated in commit on main).
- Stage 4: PR opened via the GitHub REST API, wait for CodeRabbit, full triage/fix/verify.
- Approved slices merge to `main` as they pass.
- A blocked slice is parked with a stop report and the queue continues.

## The dev's replies, verbatim

> do implement the above remaining points, all of the models should be changed into Opus 5,
> I will be away, ask me any questions you want before starting, and don't stop untill finished.

> "Stage 4 needs a PR and CodeRabbit..." = **PR via REST API, wait for CodeRabbit**
> "When a slice passes internal review..." = **Merge to main as each passes**
> "...if a slice gets blocked..." = **Skip it and continue**

> drop number 4
> regarding number 6, I don't know what is this, but I need to highlight here that the domain
> is e3a.ai not a3e.dev
> regarding number 7, I already handleded this in the exception middleware it hides everything
> in the production
> regarding number 8, I will handle it on my own

## Orchestrator note on this slice

The dev believed #7 was already handled. It is not, and the dev was shown the evidence before
this slice was queued: `ExceptionMiddleware.cs` has no `IsDevelopment`/`IsProduction`/
`IHostEnvironment` reference, `ErrorResponseHandler.cs:32` sets `Data` unconditionally,
`git log` shows only the initial commit and PR #10 touching those files, and a live request to
the running API returned a full stack trace with absolute paths. The dev was told it would be
included unless they objected, and did not object.
