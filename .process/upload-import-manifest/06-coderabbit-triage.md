TRIAGE: 1 to implement, 5 rejected, 0 dev-decisions

# CodeRabbit Triage — upload-import-manifest (PR #1)

Every comment was verified against the working tree, `01-plan.md`, `03-review.md`, `.claude/skills/dotnet-feature/SKILL.md`, and `docs/constitution.md` before deciding. Severity below is my own classification, not CodeRabbit's.

## IMPLEMENT

### 1. RC3 — Non-object `hooks` value in settings.json is silently converted into a malformed `hooks/hooks.json` asset
**Where:** `api/E3A.Application/Engineers/UploadEngineerDraft/SettingsJsonImporter.cs:50-58`
**CodeRabbit severity:** Minor · **My severity:** Minor — claim VERIFIED TRUE.
**Verification:** When `settings.json` contains `"hooks": "x"` (or an array/number), line 57 embeds `property.Value.GetRawText()` with no `ValueKind` check, producing `{"hooks":"x"}` as an asset. `HookWarnings` (line 78) returns `[]` for non-object values, so no warning is emitted and no skipped entry is recorded. `DraftNormalizer.cs:63-67` then stores that malformed file to blob AND lists it as an imported manifest entry (category `Hooks`). This violates plan Decision 11's "nothing silently dropped" and manifest transparency; the plan's Decision 14 contract is "`hooks` object → generate `hooks/hooks.json`" — a non-object value is outside that contract and must surface as skipped.
**Fix (smallest):** In `SettingsJsonImporter.cs`:
1. Add a reason constant next to the existing ones (after line 17): `public const string HooksNotConvertibleReason = "The settings.json hooks section must be a JSON object; it was not converted.";`
2. Extend the guard at lines 50-54 so a hooks section with `property.Value.ValueKind != JsonValueKind.Object` also takes the skip branch, with reason resolution: not hooks section → `ReasonFor(property.Name)`; hooks + already uploaded → `HooksAlreadyUploadedReason` (keeps existing test 42 green, priority unchanged); hooks + not uploaded + non-object → `HooksNotConvertibleReason`. CodeRabbit's committable suggestion (condition `!isHooksSection || hooksFileAlreadyUploaded || property.Value.ValueKind != JsonValueKind.Object` plus the nested ternary) is correct as written.
3. Add one test to `api/E3A.Tests/Engineers/UploadEngineerDraft/SettingsJsonImporterTests.cs`: `Import_ShouldSkipHooksSection_WhenHooksValueIsNotAnObject` — input `{"hooks":"x"}` with `hooksFileAlreadyUploaded: false` → `HooksFile` null, `HookWarnings` empty, exactly one skipped `settings.json#hooks` with `SettingsJsonImporter.HooksNotConvertibleReason` (conventions: every new branch needs a failing-side test).

Note: `SettingsJsonImporter.cs` is already a declared over-limit file (implementation deviation #3); the ~5-line growth stays inside that declared deviation — do not split the file, the plan fixes the production file list.

## REJECTED

### RC1 — "Reconcile the reviewer flags count" (`.process/upload-import-manifest/04-metrics.md:10-11`)
**CodeRabbit severity:** Minor · **Verdict:** REJECT — claim is FALSE (misreading).
The two numbers are different metrics on different stage rows. Row 3 (Implement) reports "2 reviewer flags" = the two items the implementer flagged for reviewer attention (`02-implementation.md` Notes 1 and 2: git-ignored appsettings; EngineerTests length). Row 4 (Review) reports "4 non-blocking" = the reviewer's own findings. No inconsistency exists; furthermore `04-metrics.md` is an immutable pipeline run record, not documentation — rewriting it after the fact would falsify the run history.

### RC2 — "Fail fast when upload configuration is invalid" (`api/E3A.Application/DependencyInjection.cs:15-16`)
**CodeRabbit severity:** Major · **Verdict:** REJECT — contradicts the recorded house policy and pattern.
The factual premise is true: missing config binds `UploadsOptions`/`AzureOptions` to 0/empty, and `new Uri("")` in `StorageBlobClient.cs:27` would throw on first upload. But this exact consequence is a documented, dated dev decision — `docs/constitution.md` §2 (line 99): "Config is deploy-time only (dev decision 2026-08-27) … CI and fresh clones have NO defaults (options bind to 0/empty)". The repo pattern registers options with plain `Configure<>`; `EngineersOptions` (`DependencyInjection.cs:14`) has no validator either. Bolting `ValidateOnStart()` onto only two of three options sections contradicts the skill's "mirror, don't modernize" rule and invents a cross-cutting pattern mid-slice. The failure mode CodeRabbit fears (empty config in a real deployment) surfaces immediately and loudly on first use, in an environment the constitution says must supply full configuration externally. If the dev wants startup-time options validation, it should be adopted repo-wide as its own change and added to skill §8 — not smuggled into this PR.

### RC4 — "Make draft replacement failure-safe and concurrency-safe" (`api/E3A.Application/Engineers/UploadEngineerDraft/UploadEngineerDraftHandler.cs:48-59`)
**CodeRabbit severity:** Critical · **My severity:** Major, not Critical · **Verdict:** REJECT — re-litigates an explicit, dev-approved plan decision.
The factual claim is true: line 48 deletes the blob prefix before the uploads (lines 50-54) and the single `SaveChangesAsync` (line 59), so a mid-flight failure leaves the prior draft's blobs gone with a stale DB manifest, and overlapping uploads are last-writer-wins. But this is not a discovered bug — it is the designed, documented v0.1 behaviour: plan Decision 15 ("If an upload fails midway … the next successful upload heals both. Accepted for v0.1") and the Deferred-table row "Concurrency guard on simultaneous uploads … last-writer-wins is acceptable for v0.1", both approved by the dev at the plan gate on 2026-08-27. CodeRabbit itself cites `01-plan.md:20` — it saw the deferral and asked for the staged-prefix redesign anyway.
Why I downgrade from Critical: in the current product state the blast radius is bounded — draft blobs are read by nothing in this slice (no download path exists, Deferred row 5); the draft is owner-only and trivially reproducible by re-uploading the same zip; the DB manifest itself is never lost (`SaveChangesAsync` only runs after all uploads succeed); and delete-before-upload is itself forced by the vendored `UploadAsync` not overwriting (plan Decision 3). The staged-generation-prefix + swap design CodeRabbit proposes is exactly the "true atomicity" the plan priced and deferred. The right time to build it is slice ③, when the publish pipeline starts consuming draft blobs — carry it on that slice's backlog. If the dev wishes to re-open Decision 15 earlier that is his prerogative, but the decision has already been made once, so this is not a DEV-DECISION item.

### RC5 — "Use one configuration source of truth" (`docs/constitution.md:99`)
**CodeRabbit severity:** Major · **Verdict:** REJECT — the divergence it alleges does not exist.
`docs/constitution.md:99` already IS the single source of truth: it explicitly defines the policy (deploy-time only; no configuration file committed; `appsettings.json` git-ignored), dates it as a dev decision (2026-08-27), and spells out the exact consequence CodeRabbit warns about (fresh clones/CI bind 0/empty; new options sections are announced to the dev to mirror into his environments). The "conflicting" records it cites — `00-acceptance.md:17-19` and `01-plan.md:134-157` — are `.process/` pipeline run records: historical logs of what was asked and planned during the run. Per `.claude/rules/docs-sync.md`, docs live in `/docs` only; process records are not docs and are never retro-edited to match later decisions (that would falsify the run history). Nothing in the branch commits an appsettings file (verified: `api/E3A.Api/appsettings.json` is git-ignored and absent from the diff), so code and constitution agree. Nothing to change.

### PC1 — PR-level walkthrough + failed "Docstring Coverage" check (0% vs 80% threshold)
**Verdict:** REJECT — the only actionable item directly contradicts house rules.
The walkthrough is descriptive; its "Merge Risk" paragraph restates RC4/RC2, dispositioned above. The failed pre-merge check asks for docstrings on the 122 touched functions; skill §1 ("No Comments — zero comments unless the WHY is a hidden invariant") and the constitution prohibit exactly that. Generating XML docstrings across the diff would be a mass style violation. Do not tick the "Generate docstrings" or "Fix all pre-merge checks with AI" checkboxes. Optional housekeeping for the dev, outside this PR: disable the docstring-coverage pre-merge check in `.coderabbit.yaml` so it stops failing every E3A PR.

## Summary for the implementer

Implement item #1 only (RC3): the `ValueKind` guard + `HooksNotConvertibleReason` constant in `SettingsJsonImporter.cs`, plus the one new test in `SettingsJsonImporterTests.cs`. Re-run `dotnet build E3A.slnx` and `dotnet test` (expect 132 passing, 0 failed). Record the change referencing this file's item #1. Everything else: no action.
