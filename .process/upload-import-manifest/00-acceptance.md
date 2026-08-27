# Workflow Acceptance — upload-import-manifest (slice ② of the v0.1 roadmap)

**Date:** 2026-08-27 14:09
**Pipeline:** FABLE 5 plan → OPUS 5 implement → FABLE 5 review · plan-approval gate · 2-round rework cap (snapshot: `00-pipeline.svg`)
**Pre-flight:** clean at 14:09 after commit `4caa0a9` (pending pipeline edit committed with dev approval)
**Base branch:** `main` (default — no warning needed)

**Feature request (verbatim):**
> Great, let's start with Upload + import manifest and Public catalog
> for the upload, use the same way that Morabh used to upload the files into Azure, and update the appsettings with the Azure account, and I will create them by my own once you finished.

**Dev acceptance (verbatim):**
> "Yes, I accept — run ② then ⑤"

**Scope note:** two slices accepted, run sequentially — this run is slice ② (upload + import
manifest); slice ⑤ (public catalog) gets its own Stage 0 when this one completes. Upload
mechanics must mirror Morabh's Azure Blob pattern (`D:\Personal\Morabh\repos\apis`);
appsettings gains the `Azure` section with placeholder account values — the dev creates the
actual Azure resources himself afterward.
