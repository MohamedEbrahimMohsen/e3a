TRIAGE: 0 to implement, 2 rejected, 0 dev-decisions

# CodeRabbit Triage Round 2 - Public Catalog (PR #2)

Deciding reviewer: feature-reviewer (Stage 4, round 2 of 2). Both comments verified against the working tree on `feature/public-catalog` before classification. Neither is labelled Critical, so no Critical downgrade requires dev veto; both Major rejections are called out explicitly for dev visibility.

## IMPLEMENT

None.

## REJECT

### RC16 (Major) - "Do not reject the overflow case as harmless" (re-open RC5)
Rejected on two independent grounds.
(a) **Re-litigation of a round-1 triage rejection with no new facts.** The code is unchanged since RC5 was verified and rejected: `api/E3A.Application/Catalog/GetCatalog/GetCatalogQueryHandler.cs:44` computes `(request.PageNumber - 1) * pageSize` over a `List<Engineer>` (`matched`, line 40), so `Enumerable.Skip` with a wrapped-negative count no-ops - no throw, no 500, no data exposure; an adversarial `page=1073741825` gets first-page items mislabeled with the requested page number. Round 1 weighed exactly this outcome (06-coderabbit-triage.md, RC5) and rejected it under gate-approved plan Decision 12 ("only what protects the server"). RC16 adds no new claim - "violates the pagination contract" is the same mislabeling round 1 already accepted for anonymous adversarial input. Round-1 triage rejections outrank CodeRabbit; the rejection stands.
(b) **The comment targets `.process/public-catalog/06-coderabbit-triage.md:56`, an immutable orchestrator run record.** Round 1 already established that doc-style demands against run-record artifacts are out of scope (RC7 pattern); audit artifacts are not revised after the fact.
Explicitly noting this Major was not implemented.

### RC17 (Major, "Heavy lift") - "Limit the rendered page-button range" on CatalogPage
Rejected for v0.1 as unobservable at any reachable scale (same standard as round-1 RC14).
**Verified:** `web/src/features/catalog/CatalogPage.tsx:107-109` does render one button per page from `Array.from({ length: data.totalPages }, ...)` - the claim is technically accurate. But the harm requires `data.totalPages` in the thousands: the page never sends `pageSize` (`getCatalog` call at line 39), so the server uses configured `DefaultPageSize`, and the entire pagination block is gated behind `data.totalPages > 1` (line 104). The seed catalog holds 6 engineers and cannot grow until publishing lands (slice 3, gate-approved deferral) - today the block does not render at all, and even at hundreds of engineers it renders tens of buttons, not thousands. A bounded page window with first/last/ellipsis controls is a real UI redesign (CodeRabbit's own "Heavy lift" tag) with zero observable benefit this slice; Prev/Next (lines 106, 110) already give bounded navigation. Revisit when the publishing/ingestion slice makes catalog growth possible.
Explicitly noting this Major was not implemented.

## Notes for the implementer

No work items this round. No code or doc changes required; round-1 fixes (commit b06fd8a) stand as merged state for this PR.
