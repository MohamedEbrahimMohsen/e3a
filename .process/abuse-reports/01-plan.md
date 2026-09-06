# Plan — Abuse Reports (submission path)

## Goal
Today `web/src/app/ReportContext.tsx` `submit()` closes the modal and shows `"Report submitted — thank you"` without making any network call — no `reports` table, no entity, no endpoint exist. After this slice, a visitor on an engineer detail page (signed in or not) picks a reason, optionally writes details, and presses **Submit report**; the browser `POST`s to `POST /api/reports`, the API resolves the target item, persists a `Report` row with `Status = Open` (attributed to the signed-in reporter, or anonymous), and the toast appears **only** after a 2xx. Every failure renders prose in the modal and leaves it open.

## Scope

**In**
- Domain: `Report` aggregate + `ReportReason` / `ReportStatus` enums + `IReportRepository`.
- Application: `Reports/SubmitReport/` slice (command, validator, handler), `Reports/Shared/` result + generator, `ReportsOptions`, 7 error codes.
- Infrastructure: `ReportRepository`, `AppDbContext` DbSet + `ConfigureReports` + global soft-delete filter line, migration `reports006`.
- Api: `ReportsController` (`POST /api/reports`, anonymous-capable), `Requests.cs`, both resx files.
- Tests: entity, handler (happy + every throw branch), validator (every rule).
- Web: `lib/reportsApi.ts` (+ its vitest file), `ReportContext` wired through `requestJson`, `errorMessages.ts` codes, `reportReasons` deleted from mock `lib/catalog.ts`, dead Report affordances removed from `Footer.tsx` and `DetailHeader.tsx`.
- Postman `Reports / Submit Report`.
- Docs sync: `docs/implementation-plan.md`, `docs/security-scan.md`.

**Out**
- Any read endpoint for reports (`GET /api/reports`) — nothing consumes it.
- Admin/moderation UI, status transitions, takedown automation, notification on report.
- Cloudflare rate rules (infra, dev-owned, P6).
- Vote/like endpoints (the neighbouring `likes` row in the data-model doc).

**Deferred**
| Item | Why |
|---|---|
| Admin moderation (list reports, resolve/dismiss, pull item from `marketplace.json`) | Needs an admin surface and a role policy that do not exist in v0.1; modelling `Reviewed`/`Dismissed` now would ship states nothing can reach. |
| Team reporting from the web UI | `TeamDetailPage` renders mock `CatalogItem` data (`lib/catalog.ts`), which carries no team `Id`. There is no team catalog endpoint yet, so the UI cannot supply a real `ItemId`. The **API accepts `ItemType.Team`** already (it is the doc-specified schema and the existing `ItemType` enum), so the path lands the moment the team catalog endpoint exists. |
| Per-reporter duplicate suppression | See Decision 2. |
| Rate limiting by IP | Cloudflare rate rules, P6 Hardening — see Decision 1. |

## Decisions

| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Anonymous write is a spam vector — what protection ships now? | A **per-item cap** enforced in the handler: `ReportsOptions.MaxReportsPerItem` (20, from appsettings). `reportRepository.CountAsync(x => x.ItemType == … && x.ItemId == …)`; at or over the cap → `RateLimitExceededCoreException(ErrorCodes.ReportLimitReached)` → **429**, with `context["limit"]`. No IP throttling in-API. | The cap makes total table growth **bounded by `catalog size × 20`** rather than unbounded, which is the property that matters for an anonymous write. It needs no new abstraction (base `CountAsync` covers it) and no request-scoped state. IP/ASN throttling belongs at the edge — `docs/implementation-plan.md` §Current #4 already assigns it to Cloudflare and P6 owns it. **Accepted residual risk, stated plainly:** an attacker who fills an item's 20 slots blocks genuine reports for that item until moderation exists. It is accepted because with no moderation UI in v0.1 an item at 20 reports is already maximally flagged — the 21st report adds no signal a human would act on differently — and because the alternative (unbounded rows) is strictly worse. |
| 2 | Per-item-per-reporter uniqueness instead of / as well as the cap? | **No.** Deferred. | It only constrains *signed-in* reporters — the population least likely to spam, since they have an attributable account — and does nothing against the anonymous path this endpoint exists to serve. The real case it catches is a double-click, which is handled deterministically client-side by disabling the submit button while `submitting` is true. Adding it would mean a filtered unique index, a `ConflictCoreException` branch and two more tests for no coverage of the actual threat. |
| 3 | Must the reported item exist? | **Yes.** The handler resolves `ItemId` against `IEngineerRepository` / `ITeamRepository` by `ItemType`. Unknown → `BadRequestCoreException(ErrorCodes.ReportItemNotFound)` → **400**. The lookup accepts an item in **any** status (`Draft`, `Published`, `Unlisted`) — only a genuinely absent (or soft-deleted) id fails. | Without the check the table fills with unverifiable rows and is useless as evidence for a takedown — the whole point of the human backstop. The information-disclosure concern is answered by accepting every status: the response is **byte-identical** for a draft, an unlisted and a published item, so the endpoint is *not* an oracle for item status. It does reveal "this Guid is an item at all", which is unexploitable — `ItemId` is a random 128-bit Guid, so there is nothing to enumerate. `400` (not `404`) because the bad value came in a body field, and it keeps the endpoint from reading as a resource lookup. |
| 4 | Reason: free text or fixed set? | **Fixed set**, as a domain enum `E3A.Domain.Reports.ReportReason { Malicious, Spam, Copyright, Other }`, stored `HasConversion<string>()`. Wire shape is the string name, bound by the `JsonStringEnumConverter` already on `AddControllers().AddJsonOptions(...)`. Validated with `.IsInEnum().WithErrorCode(ErrorCodes.ReportReasonInvalid)`, mirroring `PublishEngineerValidator`'s `Increment` rule. `Other` additionally requires `Details`. | Free text cannot be grouped, counted or triaged, and it is a second injection surface. The enum is the canonical server-side list. Frontend sync: `web/src/lib/reportsApi.ts` exports `export type ReportReason = 'Malicious' \| 'Spam' \| 'Copyright' \| 'Other'` plus `REPORT_REASON_OPTIONS` (value → human label), and a vitest asserts the four values — one place to change, and a mismatch is a compile error at every call site. The four labels are exactly today's `reportReasons` strings, so the visible UI text does not change. |
| 5 | Where do the length caps live? | `E3A.Application/Options/ReportsOptions.cs` (`SectionName = "Reports"`), bound in `AddApplication`, consumed by **both** `SubmitReportValidator` and `AppDbContext.ConfigureReports`. Zero constants on the entity. | Standing review rule (SKILL §8.1) — a cap change must be a config change. Mirrors `EngineersOptions`/`CatalogOptions` exactly, including the `AppDbContext` constructor taking `IOptions<ReportsOptions>`. |
| 6 | `Status` — which states, moved by whom? | `public enum ReportStatus { Open }`. `Report.Create` sets `Open`. **No** transition method, because no actor in v0.1 can move a report. | The column is in the doc-specified schema and moderation will need it, but `Reviewed`/`Dismissed`/`Actioned` would be unreachable states — untestable by definition and forbidden by "do not model states nothing can reach". Soft delete still works through `ISoftDeletable` on the base + the global query filter; no `Delete()` domain method is added because nothing calls one (adding one would be the same unreachable-branch defect). When moderation lands, adding members to a one-member enum is additive. |
| 7 | Migration name | `reports006` → `dotnet ef migrations add reports006`, producing `<EF-timestamp>_reports006.cs` + `.Designer.cs` and an updated `AppDbContextModelSnapshot.cs`. | Real convention in `api/E3A.Infrastructure/Data/Migrations/` is `<name><nnn>`: `initial`, `versions002`, `scan003`, `oauth004`, `teams005`. Next is `006`. |
| 8 | Controller attributes for an anonymous-capable write | `[Authorize]` on the **class**, `[AllowAnonymous]` on the **action** — the exact shape of `EngineersController.GetEngineer` and `AuthenticationController`. Not a bare `[AllowAnonymous]` controller like `CatalogController`. | **Load-bearing.** `Program.cs` has no `app.UseAuthentication()`; the principal is populated because `AuthorizationMiddleware` builds a policy from `[Authorize]` metadata and authenticates. On an endpoint whose only metadata is `[AllowAnonymous]`, the middleware short-circuits and `HttpContext.User` is never populated — `ICurrentUserService.UserId` would be `null` for **every** caller and `ReporterUserId` would silently always be null. `CatalogController` gets away with it because nothing there reads the user. This is a wire-level defect that unit tests cannot see (SKILL §8.6 class of bug). |
| 9 | Anonymous reporter and the `UserId` guard | The handler **must not** throw `UnauthorizedCoreException`. It normalises: `var reporterUserId = currentUserService.UserId == Guid.Empty ? null : currentUserService.UserId;` and passes the `Guid?` straight to `Report.Create`, which passes it to `AuditEntity`'s `createdBy`. | This is the one command in the solution where "no authenticated user" is a valid outcome, not an error. `Guid.Empty` is normalised to `null` because the rest of the codebase treats `Guid.Empty` as "no user" (`CreateEngineerHandler`, `GetEngineerQueryHandler`), and an all-zeros `ReporterUserId` would read as a real attribution. |
| 10 | Footer "Report abuse" | **Removed** from `web/src/components/Footer.tsx`. | A report must name an item; the footer has none, so the affordance cannot be honoured by a real endpoint — keeping it would preserve exactly the lie this slice removes. It is also a `<span onClick>`, an outright `conventions/react-feature.md` §6 violation (no focus, no keyboard activation). `docs/design-prompt.md` §Pages 1 mentions "footer" but never enumerates its links, so no design doc diverges. |
| 11 | `DetailHeader` Report link (team detail) | **Removed** from `web/src/features/detail/DetailHeader.tsx` (and its now-unused `useReport` import — `noUnusedLocals` would fail the build otherwise). Reporting remains on `EngineerDetailPage`, which has a real `engineer.id`. | `DetailHeader` is only used by `TeamDetailPage`, which renders mock `CatalogItem` data with no id. It could only call a real endpoint with a fabricated `ItemId`, which Decision 3 rejects at the API. `docs/design-prompt.md` §Pages 4 ("Team detail — same layout") stays unchanged: it describes the target design, and the team catalog simply lags — that is *incompleteness*, not divergence (`.claude/rules/docs-sync.md`). `docs/implementation-plan.md` records the deferral because deferring a feature **is** a scope change. |
| 12 | Response body | `ReportResult(Guid Id, string Status, DateTimeOffset CreatedAt)` via `ReportResultGenerator`, returned with `Ok(result)`. No `CreatedAtAction` (there is no `GET /api/reports/{id}`), no echo of `Details` or `ReporterUserId`. | Client-facing result; the web client ignores the body and keys off the 2xx, but a bare `Ok()` would leave nothing for Postman/curl to confirm. Echoing the reporter or the details back to an anonymous caller has no use and adds a disclosure surface. e3a is EN-only, so plain `string` — no `LocalizedText`, no `.Localized()` anywhere in this slice (SKILL §4.5). |
| 13 | `IAuditableCommand`? | **No.** | No command in `E3A.Application` opts in today; introducing the first usage here would be an unrelated cross-cutting change. The `Report` row, with `CreatedBy`/`CreationDate` from `AuditEntity`, is itself the audit record. |
| 14 | Reason picker control | The cycling `<div onClick>` becomes a `<select>` bound to `ReportReason`. The `✕` `<span onClick>` becomes `<button type="button" aria-label="Close">✕</button>`. | We must bind a canonical enum value rather than an index into a label array, so the control is being rewritten regardless. A `<div onClick>` cycling through four options is unreachable by keyboard — same class of defect as `conventions/react-feature.md` §6's documented "keyboard-only creator stopped dead" bug. `docs/design-prompt.md` §Pages already specifies "the report-item modal (**reason dropdown** + details textarea)", so this moves the code *toward* the doc. |

## Existing code touched

| File | Change |
|---|---|
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Append a `// Reports` group with the 7 constants below. |
| `api/E3A.Application/DependencyInjection.cs` | `services.Configure<ReportsOptions>(configuration.GetSection(ReportsOptions.SectionName));` after the `GitHubAuthenticationOptions` line. |
| `api/E3A.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IReportRepository, ReportRepository>();` in `AddInfrastructure`, alphabetically after `IItemVersionRepository`. |
| `api/E3A.Infrastructure/Data/Context/AppDbContext.cs` | Add ctor parameter `IOptions<ReportsOptions> reportsOptions`; add `public DbSet<Report> Reports { get; set; }`; call `ConfigureReports(modelBuilder)` in `OnModelCreating`; add the private `ConfigureReports` method (below); add `modelBuilder.Entity<Report>().HasQueryFilter(x => !x.IsDeleted);` to `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. |
| `api/E3A.Infrastructure/Data/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add reports006`. Do not hand-edit. |
| `api/E3A.Api/appsettings.json` | New `"Reports": { "DetailsMaxLength": 1000, "MaxReportsPerItem": 20 }` section, placed after `"Catalog"`. |
| `api/E3A.Api/Resources/Messages.en.resx` | 7 new `<data>` entries (values below), before the `UNHANDLED_EXCEPTION` entry. |
| `api/E3A.Api/Resources/Messages.ar.resx` | The same 7 keys, Arabic values, `{limit}` placeholder preserved. |
| `postman/e3a.postman_collection.json` | New top-level `Reports` folder with one request (below), placed after `Catalog`. |
| `web/src/app/ReportContext.tsx` | Rewritten — see API surface / frontend contract. |
| `web/src/components/Footer.tsx` | Delete the `Report abuse` `<span>` (line 13) and the now-unused `useReport` import + `const { openReport } = useReport();`. |
| `web/src/features/detail/DetailHeader.tsx` | Delete the `Report` `<span>` (line 34), the `useReport` import and the `const { openReport } = useReport();` line. |
| `web/src/features/detail/EngineerDetailPage.tsx` | Change the Report button's handler to `onClick={() => openReport({ itemType: 'Engineer', itemId: engineer.id, label: engineer.slug })}`. Nothing else changes. |
| `web/src/lib/catalog.ts` | Delete `export const reportReasons = [...]` (line 91). It is mock data and its only consumer moves to `reportsApi.ts`. |
| `web/src/lib/errorMessages.ts` | Add the 7 report codes to the `errorMessages` record. |
| `docs/implementation-plan.md` | See Docs sync. |
| `docs/security-scan.md` | See Docs sync. |

## Files to create

### Domain

| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `api/E3A.Domain/Reports/ReportReason.cs` | enum | `namespace E3A.Domain.Reports;` · `public enum ReportReason { Malicious, Spam, Copyright, Other }`. No extensions class — nothing needs one. |
| 2 | `api/E3A.Domain/Reports/ReportStatus.cs` | enum | `namespace E3A.Domain.Reports;` · `public enum ReportStatus { Open }`. See Decision 6. |
| 3 | `api/E3A.Domain/Reports/Report.cs` | entity | `namespace E3A.Domain.Reports;` · `using Core.DDD.Entities;` · `using E3A.Domain.Publishing;` · `public class Report : AuditEntity`. Members below. |
| 4 | `api/E3A.Domain/Reports/IReportRepository.cs` | interface | `namespace E3A.Domain.Reports;` · `using Core.DDD.Repositories;` · `public interface IReportRepository : IRepository<Report> { }` — **empty**; base `CountAsync`/`AddAsync`/`SaveChangesAsync` cover every need. |

`Report` members, in this order:
```
public ItemType ItemType { get; private set; }
public Guid ItemId { get; private set; }
public Guid? ReporterUserId { get; private set; }
public ReportReason Reason { get; private set; }
public string? Details { get; private set; }
public ReportStatus Status { get; private set; }
public bool IsAnonymous => ReporterUserId == null;

private Report(Guid id, Guid? createdBy) : base(id, createdBy) { }

public static Report Create(ItemType itemType, Guid itemId, Guid? reporterUserId, ReportReason reason, string? details)
```
`Create` returns `new Report(Guid.NewGuid(), reporterUserId)` with object-initialiser assignment of `ItemType`, `ItemId`, `ReporterUserId`, `Reason`, `Details`, `Status = ReportStatus.Open`, `CreationDate = DateTimeOffset.UtcNow`, `UpdationDate = DateTimeOffset.UtcNow` — mirroring `Engineer.Create`. No other methods.

### Application

| # | Path | Type | Contract |
|---|---|---|---|
| 5 | `api/E3A.Application/Options/ReportsOptions.cs` | options | `namespace E3A.Application.Options;` · `public sealed class ReportsOptions` · `public const string SectionName = "Reports";` · `public int DetailsMaxLength { get; set; }` · `public int MaxReportsPerItem { get; set; }` |
| 6 | `api/E3A.Application/Reports/Shared/ReportResult.cs` | result | `namespace E3A.Application.Reports.Shared;` · `public sealed record ReportResult(Guid Id, string Status, DateTimeOffset CreatedAt);` — client-facing, no `LocalizedText`. |
| 7 | `api/E3A.Application/Reports/Shared/ReportResultGenerator.cs` | generator | `namespace E3A.Application.Reports.Shared;` · `public static class ReportResultGenerator` · `public static ReportResult Generate(Report report) => new(report.Id, report.Status.ToString(), report.CreationDate);` (statement-bodied `return`, mirroring `CatalogEngineerResultGenerator`). |
| 8 | `api/E3A.Application/Reports/SubmitReport/SubmitReportCommand.cs` | command | `namespace E3A.Application.Reports.SubmitReport;` · `public sealed record SubmitReportCommand(ItemType ItemType, Guid ItemId, ReportReason Reason, string? Details) : IRequest<ReportResult>;` |
| 9 | `api/E3A.Application/Reports/SubmitReport/SubmitReportValidator.cs` | validator | `namespace E3A.Application.Reports.SubmitReport;` · `public sealed class SubmitReportValidator : AbstractValidator<SubmitReportCommand>` · ctor `SubmitReportValidator(IOptions<ReportsOptions> reportsOptions)`. Rules below. |
| 10 | `api/E3A.Application/Reports/SubmitReport/SubmitReportHandler.cs` | handler | `namespace E3A.Application.Reports.SubmitReport;` · `public sealed class SubmitReportHandler(IReportRepository reportRepository, IEngineerRepository engineerRepository, ITeamRepository teamRepository, ICurrentUserService currentUserService, IOptions<ReportsOptions> reportsOptions) : IRequestHandler<SubmitReportCommand, ReportResult>` — one line, no wrapping. Steps below. |

**Validator rules** (in this order):

| Rule | Extension / call | Error code |
|---|---|---|
| `ItemId` present | `RuleFor(x => x.ItemId).ValidateRequired(...)` (Core.Validation `RequiredValidationExtensions`, `Guid` overload) | `ErrorCodes.ReportItemIdRequired` |
| `ItemType` a known enum member | `RuleFor(x => x.ItemType).IsInEnum().WithErrorCode(...)` | `ErrorCodes.ReportItemTypeInvalid` |
| `Reason` a known enum member | `RuleFor(x => x.Reason).IsInEnum().WithErrorCode(...)` | `ErrorCodes.ReportReasonInvalid` |
| `Details` within cap | `RuleFor(x => x.Details).ValidateMaxLength(options.DetailsMaxLength, ...)` | `ErrorCodes.ReportDetailsTooLong` |
| `Details` required when `Reason == Other` | `RuleFor(x => x.Details).ValidateRequired(...).When(x => x.Reason == ReportReason.Other)` | `ErrorCodes.ReportDetailsRequired` |

`var options = reportsOptions.Value;` is the first line of the ctor, mirroring `CreateEngineerValidator`.

**`Handle` — ordered steps** (every `await` carries `.ConfigureAwait(false)`; no `try`/`catch`):

1. `var options = reportsOptions.Value;`
2. Resolve existence with a two-arm switch expression (both arms reachable and tested; no unreachable `_ => false` arm):
   ```
   var itemExists = request.ItemType switch
   {
       ItemType.Engineer => await engineerRepository.GetByIdAsync(request.ItemId, cancellationToken, asNoTracking: true).ConfigureAwait(false) is not null,
       _ => await teamRepository.GetByIdAsync(request.ItemId, cancellationToken, asNoTracking: true).ConfigureAwait(false) is not null,
   };
   ```
3. `if (!itemExists) { throw new BadRequestCoreException(ErrorCodes.ReportItemNotFound); }`
4. `var existingReportCount = await reportRepository.CountAsync(cancellationToken, x => x.ItemType == request.ItemType && x.ItemId == request.ItemId).ConfigureAwait(false);` — note the `CountAsync(cancellationToken, predicate)` parameter order used by `EngineerRepository`.
5. `if (existingReportCount >= options.MaxReportsPerItem) { throw new RateLimitExceededCoreException(ErrorCodes.ReportLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxReportsPerItem }); }`
6. `var reporterUserId = currentUserService.UserId == Guid.Empty ? null : currentUserService.UserId;`
7. `var report = Report.Create(request.ItemType, request.ItemId, reporterUserId, request.Reason, request.Details);`
8. `await reportRepository.AddAsync(report, cancellationToken).ConfigureAwait(false);`
9. `await reportRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);` — the only `SaveChangesAsync` call in the slice.
10. `return ReportResultGenerator.Generate(report);`

### Infrastructure

| # | Path | Type | Contract |
|---|---|---|---|
| 11 | `api/E3A.Infrastructure/Reports/ReportRepository.cs` | repository | `namespace E3A.Infrastructure.Reports;` · `public class ReportRepository(AppDbContext context) : Repository<Report>(context), IReportRepository { }` — empty body, mirrors the simple form in SKILL §6.1. |
| 12 | `api/E3A.Infrastructure/Data/Migrations/<EF-timestamp>_reports006.cs` + `.Designer.cs` | migration | Generated, not hand-written: `dotnet ef migrations add reports006 --project api/E3A.Infrastructure --startup-project api/E3A.Api`. Contents asserted below. |

`ConfigureReports(ModelBuilder modelBuilder)` — private method on `AppDbContext`, placed after `ConfigureItemVersions`:
```
var reportSchema = reportsOptions.Value;

modelBuilder.Entity<Report>(builder =>
{
    builder.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
    builder.Property(x => x.Details).HasMaxLength(reportSchema.DetailsMaxLength);
    builder.HasIndex(x => new { x.ItemType, x.ItemId });
    builder.HasIndex(x => x.ReporterUserId);
});
```
No FK to `Engineers`/`Teams` — mirrors `team_members`' documented "carries no foreign key" choice, so an item can be deleted without breaking its report history, and one column cannot point at two tables.

**The migration must create**: table `Reports` with `Id uniqueidentifier PK`, `ItemType nvarchar(50) NOT NULL`, `ItemId uniqueidentifier NOT NULL`, `ReporterUserId uniqueidentifier NULL`, `Reason nvarchar(50) NOT NULL`, `Details nvarchar(1000) NULL`, `Status nvarchar(50) NOT NULL`, plus the `AuditEntity`/`Entity` columns already emitted for every table (`IsDeleted bit`, `DeletedAt datetimeoffset NULL`, `CreatedBy uniqueidentifier NULL`, `CreationDate datetimeoffset`, `UpdatedBy uniqueidentifier NULL`, `UpdationDate datetimeoffset`). **Indexes**: non-unique `IX_Reports_ItemType_ItemId` (serves the per-item cap count) and non-unique `IX_Reports_ReporterUserId` (serves future moderation and abuse triage). No unique index — Decision 2.

### Api

| # | Path | Type | Contract |
|---|---|---|---|
| 13 | `api/E3A.Api/Controllers/Reports/Requests.cs` | request record | `namespace E3A.Api.Controllers.Reports;` · `using System.Text.Json.Serialization;` · `using E3A.Domain.Publishing;` · `using E3A.Domain.Reports;` · `public sealed record SubmitReportRequest([property: JsonRequired] ItemType ItemType, Guid ItemId, [property: JsonRequired] ReportReason Reason, string? Details);` — `JsonRequired` on the enums mirrors `PublishEngineerRequest`. |
| 14 | `api/E3A.Api/Controllers/Reports/ReportsController.cs` | controller | See below. |

```
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult> SubmitReport([FromBody] SubmitReportRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SubmitReportCommand(request.ItemType, request.ItemId, request.Reason, request.Details), cancellationToken);
        return Ok(result);
    }
}
```
`[Authorize]` on the class is required — Decision 8. Do not add a policy constant: there is no `DefaultCodes` class in this repo and `Program.cs` registers no named policies; every controller uses bare `[Authorize]`/`[AllowAnonymous]`.

### Web

| # | Path | Type | Contract |
|---|---|---|---|
| 15 | `web/src/lib/reportsApi.ts` | API wrapper + pure helpers | Exports below. Named exports only; `import type` for type-only imports. |
| 16 | `web/src/lib/reportsApi.test.ts` | vitest | Tests enumerated in the test plan. `.ts`, not `.tsx`. |

`reportsApi.ts` exports:
```
export type ReportItemType = 'Engineer' | 'Team';
export type ReportReason = 'Malicious' | 'Spam' | 'Copyright' | 'Other';

export interface ReportTarget { itemType: ReportItemType; itemId: string; label: string }
export interface ReportInput { itemType: ReportItemType; itemId: string; reason: ReportReason; details: string | null }
export interface SubmittedReport { id: string; status: string; createdAt: string }

export const REPORT_REASON_OPTIONS: { value: ReportReason; label: string }[]
  // exactly: Malicious → 'Malicious or unsafe behavior', Spam → 'Spam or misleading listing',
  //          Copyright → 'Copyright or license violation', Other → 'Other'
export const DEFAULT_REPORT_REASON: ReportReason      // 'Malicious'

export function normalizeReportDetails(details: string): string | null   // trims; '' / whitespace → null
export function canSubmitReport(reason: ReportReason, details: string): boolean
  // false only when reason === 'Other' && normalizeReportDetails(details) === null
export function submitReport(input: ReportInput): Promise<SubmittedReport>
  // return requestJson('/reports', { method: 'POST', body: input });
```
`ReportTarget` lives here (not in `ReportContext.tsx`) so the pure module owns the shape and the context imports it with `import type`.

**`web/src/app/ReportContext.tsx`** (rewritten, still a single named-export provider + `useReport` hook):
- `interface ReportContextValue { openReport: (target: ReportTarget) => void }` — the default context value stays `{ openReport: () => undefined }`.
- State: `target: ReportTarget | null`, `reason: ReportReason` (init `DEFAULT_REPORT_REASON`), `details: string` (init `''`), `submitting: boolean`, `errorMessage: string | null`.
- `close()` — `setTarget(null); setReason(DEFAULT_REPORT_REASON); setDetails(''); setErrorMessage(null);`
- `submit()`:
  ```
  if (!target || submitting) { return; }
  setSubmitting(true);
  setErrorMessage(null);
  submitReport({ itemType: target.itemType, itemId: target.itemId, reason, details: normalizeReportDetails(details) })
    .then(() => { setSubmitting(false); close(); showToast('Report submitted — thank you'); })
    .catch((error: unknown) => { setSubmitting(false); setErrorMessage(messageForApiError(error)); });
  ```
  No `cancelled` guard: `conventions/react-feature.md` §3 governs `useEffect` loads that can outlive their dependency; this is a user-initiated action inside a provider mounted for the app's lifetime, and the modal is only closed by the success path itself.
- Markup changes from today: header title reads `Report {target.label}`; `✕` becomes `<button type="button" aria-label="Close" onClick={close}>`; the reason `<div onClick>` becomes a `<select value={reason} onChange={event => setReason(event.target.value as ReportReason)}>` rendering `REPORT_REASON_OPTIONS` as `<option value={...}>{label}</option>`, keeping the existing `hover-border` class and inline style object; the `<textarea>` becomes controlled (`value={details}` / `onChange`), placeholder unchanged; `errorMessage` renders above the buttons as `<div style={{ fontSize: 12.5, color: 'var(--danger)' }}>{errorMessage}</div>`; both buttons gain `type="button"`; submit becomes `disabled={submitting || !canSubmitReport(reason, details)}` with label `{submitting ? 'Submitting…' : 'Submit report'}`.
- Toast text is unchanged and fires **only** inside `.then`.

## Error codes

Appended to `ErrorCodes.cs` under a new `// Reports` comment group, after `// Publishing`.

| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `ReportItemIdRequired` | `REPORT_ITEM_ID_REQUIRED` | `SubmitReportValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `ReportItemTypeInvalid` | `REPORT_ITEM_TYPE_INVALID` | `SubmitReportValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `ReportReasonInvalid` | `REPORT_REASON_INVALID` | `SubmitReportValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `ReportDetailsRequired` | `REPORT_DETAILS_REQUIRED` | `SubmitReportValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `ReportDetailsTooLong` | `REPORT_DETAILS_TOO_LONG` | `SubmitReportValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `ReportItemNotFound` | `REPORT_ITEM_NOT_FOUND` | `SubmitReportHandler` step 3 | `BadRequestCoreException` | 400 |
| `ReportLimitReached` | `REPORT_LIMIT_REACHED` | `SubmitReportHandler` step 5 | `RateLimitExceededCoreException` | 429 |

Resource strings — the key **is** the code, and every key goes in **both** files:

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|---|---|---|
| `REPORT_ITEM_ID_REQUIRED` | `The reported item is required.` | `العنصر المبلغ عنه مطلوب.` |
| `REPORT_ITEM_TYPE_INVALID` | `The reported item type is not valid.` | `نوع العنصر المبلغ عنه غير صالح.` |
| `REPORT_REASON_INVALID` | `The report reason is not valid.` | `سبب البلاغ غير صالح.` |
| `REPORT_DETAILS_REQUIRED` | `Please describe what you found.` | `من فضلك اشرح ما وجدته.` |
| `REPORT_DETAILS_TOO_LONG` | `The report details are too long.` | `تفاصيل البلاغ طويلة جدا.` |
| `REPORT_ITEM_NOT_FOUND` | `We couldn't find the item you are reporting.` | `لم نتمكن من العثور على العنصر المبلغ عنه.` |
| `REPORT_LIMIT_REACHED` | `This item has already been reported {limit} times and is queued for review.` | `تم الابلاغ عن هذا العنصر {limit} مرة وهو في انتظار المراجعة.` |

`{limit}` appears in **both** languages (it is filled from the exception `context`). Arabic without tashkeel, matching the existing entries.

`web/src/lib/errorMessages.ts` — add all seven so no SCREAMING_SNAKE value can reach the screen even though three are unreachable from the typed UI:
```
REPORT_ITEM_ID_REQUIRED: 'Please choose the item you are reporting.',
REPORT_ITEM_TYPE_INVALID: 'That item cannot be reported.',
REPORT_REASON_INVALID: 'Please choose a reason from the list.',
REPORT_DETAILS_REQUIRED: 'Please describe what you found.',
REPORT_DETAILS_TOO_LONG: 'Those details are too long. Please shorten them.',
REPORT_ITEM_NOT_FOUND: 'We could not find the item you are reporting.',
REPORT_LIMIT_REACHED: 'This item has already been reported enough times for us to review it. Thank you.',
```

## Domain behaviour

`Report` has exactly one piece of behaviour — the factory. There are **no** state transitions, **no** `BusinessRuleViolationException` guards, and therefore **no** `UpdationDate` mutation beyond the initial stamp.

```csharp
public static Report Create(ItemType itemType, Guid itemId, Guid? reporterUserId, ReportReason reason, string? details)
{
    return new Report(Guid.NewGuid(), reporterUserId)
    {
        ItemType = itemType,
        ItemId = itemId,
        ReporterUserId = reporterUserId,
        Reason = reason,
        Details = details,
        Status = ReportStatus.Open,
        CreationDate = DateTimeOffset.UtcNow,
        UpdationDate = DateTimeOffset.UtcNow,
    };
}
```

Invariants and where they live, stated so the reviewer can check nothing leaked into the wrong layer:

| Invariant | Enforced in |
|---|---|
| `Status` starts `Open` and never changes | `Report.Create`; no mutator exists |
| `ReporterUserId` is `null` exactly when the caller is anonymous, and `Guid.Empty` is never stored | `SubmitReportHandler` step 6 (normalisation) — the entity accepts whatever `Guid?` it is given |
| `CreatedBy` mirrors `ReporterUserId` | `Create` passes `reporterUserId` as `AuditEntity.createdBy` |
| The reported item exists | `SubmitReportHandler` step 3 — an application-orchestration concern (it needs two repositories), not an entity invariant |
| Reports per item ≤ `MaxReportsPerItem` | `SubmitReportHandler` step 5 — needs a repository count, so it cannot live in the entity |
| `Details` length, `Reason`/`ItemType` membership | `SubmitReportValidator` |

No `BusinessRuleViolationException` is thrown anywhere in this slice, because every rule needs data the entity does not hold.

## API surface

| Method | Route | Auth | Request record | Response |
|---|---|---|---|---|
| `POST` | `/api/reports` | `[Authorize]` on the controller, `[AllowAnonymous]` on the action — no policy constant (this repo has no `DefaultCodes`) | `SubmitReportRequest(ItemType ItemType, Guid ItemId, ReportReason Reason, string? Details)` | `200 OK` with `ReportResult(Guid Id, string Status, DateTimeOffset CreatedAt)`; `400` `REPORT_ITEM_NOT_FOUND`; `422` validation; `429` `REPORT_LIMIT_REACHED` |

Wire body (camelCase both ways, per the one-JSON-contract rule already enforced in `Program.cs`):
```json
{ "itemType": "Engineer", "itemId": "5f2f...", "reason": "Malicious", "details": "The postinstall hook posts ~/.aws to a webhook." }
```

**Postman** — add a top-level folder `Reports` after `Catalog`, containing one request:

| Field | Value |
|---|---|
| Name | `Submit Report` |
| Method | `POST` |
| Auth | `"auth": { "type": "noauth" }` on the request — proves the anonymous path; remove it locally to exercise the attributed path |
| Header | `Content-Type: application/json` |
| URL | `{{baseUrl}}/api/reports` — `"host": ["{{baseUrl}}"]`, `"path": ["api", "reports"]` |
| Body | raw / json: `{ "itemType": "Engineer", "itemId": "00000000-0000-0000-0000-000000000000", "reason": "Malicious", "details": "The postinstall hook reads ~/.aws and posts it to a webhook." }` |

## Test plan

Backend tests live in `api/E3A.Tests/Reports/`, mirroring the production tree. Entities are built only through `ReportFactory` → `Report.Create` (no `new`, no reflection). Substitutes are `private readonly` field initialisers; the ctor wires `_sut` only. `CancellationToken.None` in bodies, `Arg.Any<CancellationToken>()` in setup/verification.

Support file (not a test class): `api/E3A.Tests/Reports/Shared/ReportFactory.cs` — `namespace E3A.Tests.Reports.Shared;` · `public static class ReportFactory` with `public static Report Anonymous(Guid itemId, ItemType itemType = ItemType.Engineer, ReportReason reason = ReportReason.Malicious, string? details = "It exfiltrates credentials.")`, `public static Report Attributed(Guid itemId, Guid reporterUserId, ...)` (same defaults), and `public static ReportsOptions CreateReportsOptions(int maxReportsPerItem = 20, int detailsMaxLength = 1000)` — mirroring `EngineerFactory.CreateEngineersOptions`.

| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | `ReportTests` (`api/E3A.Tests/Reports/ReportTests.cs`) | `Create_ShouldStartOpenWithStampedDates_WhenReportIsCreated` | `Status == ReportStatus.Open`; `Id != Guid.Empty`; `ItemType`/`ItemId`/`Reason`/`Details` equal the arguments; `CreationDate` and `UpdationDate` `.Should().BeOnOrAfter(before)` where `before` is captured first |
| 2 | `ReportTests` | `Create_ShouldRecordReporterAndCreatedBy_WhenReporterIsSignedIn` | local `var reporterUserId = Guid.NewGuid();` → `ReporterUserId == reporterUserId`; `CreatedBy == reporterUserId`; `IsAnonymous.Should().BeFalse()` |
| 3 | `ReportTests` | `Create_ShouldLeaveReporterUnset_WhenReportIsAnonymous` | `ReporterUserId.Should().BeNull()`; `CreatedBy.Should().BeNull()`; `IsAnonymous.Should().BeTrue()` |
| 4 | `SubmitReportHandlerTests` (`api/E3A.Tests/Reports/SubmitReport/SubmitReportHandlerTests.cs`) | `Handle_ShouldPersistOpenReport_WhenEngineerExists` | `engineerRepository.GetByIdAsync` stubbed to an `EngineerFactory.Published(...)`; result `Status == nameof(ReportStatus.Open)`, `Id != Guid.Empty`, `CreatedAt` on-or-after a captured `before`; `_reportRepository.Received(1).AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>())`; `_reportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>())` |
| 5 | `SubmitReportHandlerTests` | `Handle_ShouldPersistOpenReport_WhenTeamExists` | command with `ItemType.Team`; `teamRepository.GetByIdAsync` stubbed to a team; captured `Report.ItemType == ItemType.Team`; `_teamRepository.Received(1).GetByIdAsync(...)`; `_engineerRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())`; `SaveChangesAsync` `Received(1)` |
| 6 | `SubmitReportHandlerTests` | `Handle_ShouldAttributeReportToReporter_WhenCallerIsSignedIn` | `_currentUserService.UserId` returns a local `var reporterUserId`; the `Report` captured from `AddAsync` has `ReporterUserId == reporterUserId` and `IsAnonymous == false` |
| 7 | `SubmitReportHandlerTests` | `Handle_ShouldLeaveReporterUnset_WhenCallerIsAnonymous` | `_currentUserService.UserId` returns `(Guid?)null`; captured `Report.ReporterUserId.Should().BeNull()`; `SaveChangesAsync` `Received(1)` — proves anonymous submission persists |
| 8 | `SubmitReportHandlerTests` | `Handle_ShouldLeaveReporterUnset_WhenCurrentUserIdIsEmpty` | `_currentUserService.UserId` returns `Guid.Empty`; captured `Report.ReporterUserId.Should().BeNull()` — locks the `Guid.Empty` → `null` normalisation (step 6) |
| 9 | `SubmitReportHandlerTests` | `Handle_ShouldPersistReport_WhenItemIsOneReportBelowTheCap` | `_reportRepository.CountAsync(...)` returns `MaxReportsPerItem - 1`; no throw; `AddAsync` and `SaveChangesAsync` each `Received(1)` — the lower half of the `>=` boundary |
| 10 | `SubmitReportHandlerGuardTests` (`api/E3A.Tests/Reports/SubmitReport/SubmitReportHandlerGuardTests.cs`) | `Handle_ShouldThrowBadRequest_WhenEngineerDoesNotExist` | `engineerRepository.GetByIdAsync` returns `(Engineer?)null`; `await act.Should().ThrowAsync<BadRequestCoreException>().Where(x => x.ErrorCode == ErrorCodes.ReportItemNotFound)`; `_reportRepository.DidNotReceive().AddAsync(...)`; `_reportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>())` |
| 11 | `SubmitReportHandlerGuardTests` | `Handle_ShouldThrowBadRequest_WhenTeamDoesNotExist` | same, with `ItemType.Team` and `teamRepository.GetByIdAsync` returning `(Team?)null`; `DidNotReceive().SaveChangesAsync` |
| 12 | `SubmitReportHandlerGuardTests` | `Handle_ShouldThrowRateLimitExceeded_WhenItemReachedTheReportCap` | `CountAsync` returns `MaxReportsPerItem`; `ThrowAsync<RateLimitExceededCoreException>().Where(x => x.ErrorCode == ErrorCodes.ReportLimitReached)`; the thrown `Context!["limit"]` equals `MaxReportsPerItem`; `DidNotReceive().AddAsync(...)`; `DidNotReceive().SaveChangesAsync(...)` — the upper half of the `>=` boundary |
| 13 | `SubmitReportValidatorTests` (`api/E3A.Tests/Reports/SubmitReport/SubmitReportValidatorTests.cs`) | `Validate_ShouldPass_WhenCommandIsValid` | `IsValid.Should().BeTrue()` for `(ItemType.Engineer, Guid.NewGuid(), ReportReason.Malicious, "It exfiltrates credentials.")` |
| 14 | `SubmitReportValidatorTests` | `Validate_ShouldPass_WhenDetailsAreOmittedForANonOtherReason` | `Details = null`, `Reason = ReportReason.Spam` → `IsValid` true |
| 15 | `SubmitReportValidatorTests` | `Validate_ShouldFail_WhenItemIdIsEmpty` | `ItemId = Guid.Empty` → `IsValid` false; `Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.ReportItemIdRequired)` |
| 16 | `SubmitReportValidatorTests` | `Validate_ShouldFail_WhenItemTypeIsNotAKnownValue` | `(ItemType)99` → `ErrorCodes.ReportItemTypeInvalid` |
| 17 | `SubmitReportValidatorTests` | `Validate_ShouldFail_WhenReasonIsNotAKnownValue` | `(ReportReason)99` → `ErrorCodes.ReportReasonInvalid` |
| 18 | `SubmitReportValidatorTests` | `Validate_ShouldFail_WhenDetailsExceedTheConfiguredMaximum` | `new string('x', detailsMaxLength + 1)` where `detailsMaxLength` comes from `ReportFactory.CreateReportsOptions()` → `ErrorCodes.ReportDetailsTooLong` |
| 19 | `SubmitReportValidatorTests` | `Validate_ShouldFail_WhenDetailsAreMissingForTheOtherReason` | `[Theory]` `[InlineData(null)] [InlineData("")] [InlineData("   ")]` with `Reason = ReportReason.Other` → `ErrorCodes.ReportDetailsRequired` |

No `ReportResultGeneratorTests`: the mapping has no branching and no `.Localized()` resolution, so `conventions/dotnet-testing.md` §5 does not require one. `ReportRepository`, `ConfigureReports`, the migration and `ReportsController` are out of scope by §5.

**Mutation checks required** (§9 — record both observed outcomes in the implementation report):
- Test 12 is the stated proof of the cap. Change `>=` to `>` in step 5, run the suite, confirm test 12 fails and test 9 still passes; restore from a byte-exact copy verified with `cmp`.
- Test 8 is the stated proof of the `Guid.Empty` normalisation. Delete the ternary in step 6 so `currentUserService.UserId` is passed straight through, confirm **only** test 8 fails, restore.
- Test 10 is the stated proof of the existence guard. Delete step 3's `throw`, confirm tests 10 and 11 fail, restore.
- **Do not claim any test constrains a repository predicate.** `_reportRepository.CountAsync(...)` and `GetByIdAsync(...)` are NSubstitute stubs that never invoke the expression, so the `x.ItemType == … && x.ItemId == …` filter in step 4 is **unproven by unit tests** — say so in the report rather than implying coverage.

### Frontend tests — `web/src/lib/reportsApi.test.ts`

vitest, `environment: 'node'`. `stubFetch` and `stubLocalStorage` helpers are declared locally in the file (there is no shared test-util module; `http.test.ts` does the same). `afterEach(() => vi.unstubAllGlobals())`.

| # | `describe` | `it` | Asserts |
|---|---|---|---|
| 20 | `submitReport` | `should POST to the reports endpoint` | `fetchMock.mock.calls[0][0] === \`${config.apiBaseUrl}/reports\``; `init.method === 'POST'` |
| 21 | `submitReport` | `should send the item identity and reason in the request body` | `JSON.parse(init.body)` deep-equals `{ itemType: 'Engineer', itemId: '…', reason: 'Malicious', details: '…' }`; `headers['Content-Type'] === 'application/json'` |
| 22 | `submitReport` | `should send no authorization header when the reporter is signed out` | `'Authorization' in headers` is `false` |
| 23 | `submitReport` | `should attach the bearer token when the reporter is signed in` | `headers.Authorization === 'Bearer jwt'` |
| 24 | `submitReport` | `should reject with an ApiError when the API refuses the report` | stub `{ ok: false, status: 429, json: async () => ({ code: 'REPORT_LIMIT_REACHED', message: '…' }) }`; `await expect(...).rejects.toBeInstanceOf(ApiError)`; the rejected error's `code === 'REPORT_LIMIT_REACHED'` |
| 25 | `normalizeReportDetails` | `should return null when the details are only whitespace` | `normalizeReportDetails('   ')` and `normalizeReportDetails('')` both `null` |
| 26 | `normalizeReportDetails` | `should trim the surrounding whitespace when details are present` | `normalizeReportDetails('  hook posts my keys  ') === 'hook posts my keys'` |
| 27 | `canSubmitReport` | `should allow submission when the reason is not Other and details are empty` | `canSubmitReport('Malicious', '')` is `true` |
| 28 | `canSubmitReport` | `should block submission when the reason is Other and details are empty` | `canSubmitReport('Other', '   ')` is `false` |
| 29 | `canSubmitReport` | `should allow submission when the reason is Other and details are provided` | `canSubmitReport('Other', 'It ships a fork bomb')` is `true` |
| 30 | `REPORT_REASON_OPTIONS` | `should list every reason the API accepts` | `REPORT_REASON_OPTIONS.map(option => option.value)` deep-equals `['Malicious', 'Spam', 'Copyright', 'Other']` — the frontend-server sync check |

**No component tests are planned.** There is no DOM runner and jsdom/testing-library is not authorised (`conventions/react-feature.md` §7). How the component changes are verified instead, to be stated in the implementation report:
- Changing `openReport`'s parameter from `string` to `ReportTarget` makes every un-updated call site a **compile error**, so `npm run build` (`tsc -b`) is the mechanical proof that `Footer.tsx`, `DetailHeader.tsx` and `EngineerDetailPage.tsx` were all handled.
- `noUnusedLocals` turns the leftover `useReport` imports in `Footer.tsx` / `DetailHeader.tsx` into build failures, so their removal is proven by the same command.
- Deleting `reportReasons` from `catalog.ts` is proven by `npm run build` — a surviving import would not resolve.
- The `<select>` / `<button type="button">` a11y change is verified by reading the emitted markup; state that plainly rather than implying coverage.

## Docs sync

Per `.claude/rules/docs-sync.md`, this change adds an endpoint, a table and a rate policy — all divergence triggers.

| Doc | Section | Required edit |
|---|---|---|
| `docs/implementation-plan.md` | §Data model (SQL, EF Core), the `reports` bullet | Replace `- \`reports\`: Id, ItemType, ItemId, ReporterUserId?, Reason, Details, Status` with the real shape: `Reason(Malicious\|Spam\|Copyright\|Other)`, `Status(Open)` with a note that moderation states are deferred, `Details` capped by `ReportsOptions.DetailsMaxLength`, non-unique indexes on `(ItemType, ItemId)` and `ReporterUserId`, and no foreign key to `engineers`/`teams` (same rationale as `team_members`). |
| `docs/implementation-plan.md` | §Key architecture decisions → Current, item 4 | Amend "reports allowed anonymous behind Cloudflare rate limiting" to also name the in-API per-item cap (`ReportsOptions.MaxReportsPerItem`, 429 `REPORT_LIMIT_REACHED`) as the protection that ships in the API, with Cloudflare rate rules still owned by P6. This is a **policy change** — the doc currently gives a different answer to "what bounds anonymous reports". |
| `docs/implementation-plan.md` | §API surface (`/api/*`) | Replace `Social: \`POST report\` (anon OK)` with `Social: \`POST /api/reports\` (anon OK; signed-in callers are attributed) — 400 on an unknown item, 429 past the per-item cap; the web UI submits from engineer detail only, team reporting deferred until a team catalog endpoint exists`. Covers both the endpoint shape and the deferred scope (Decision 11). |
| `docs/security-scan.md` | §Outcomes, the bullet "The report button on every catalog item is the human backstop; reported items can be pulled from `marketplace.json` immediately." | Amend to say where the report now goes: persisted to the `reports` table via `POST /api/reports` with `Status = Open`, available on engineer detail pages (team reporting deferred), and that pulling an item is still a **manual** operator action — there is no moderation UI in v0.1. The current text implies a working end-to-end backstop that this slice only half delivers; leaving it would leave two answers to "what happens when I press Report". |
| `docs/architecture.md` | — | **No change.** It describes resources, the publish pipeline and backend layering; it enumerates no tables and no endpoints, and this slice adds no Azure resource or pipeline step. |
| `docs/design-prompt.md` | — | **No change.** §Pages 3 (Engineer detail "Report link") and the "report-item modal (reason dropdown + details textarea)" line still describe what ships — the `<select>` moves the code toward the doc. §Pages 4 (Team detail) describes the target design; the team catalog lagging is incompleteness, not divergence. §Pages 1 mentions the footer but never lists its links. |
| `docs/plugin-spec.md` / `docs/constitution.md` | — | **No change.** No plugin layout, marketplace format, merge rule or engineering rule moves. |

## Definition of done

- [ ] `Report` extends `AuditEntity`, has a private ctor and a single `static Create(...)`; no public setters; no `Delete()`/transition method.
- [ ] `ReportStatus` has exactly one member, `Open`; no state is modelled that nothing can reach.
- [ ] `IReportRepository` is empty (`: IRepository<Report> { }`) — no custom method was added that the base already covers.
- [ ] No new exception type, no service class, no repository method, no abstraction outside `Core.*` and the skill's vocabulary was introduced.
- [ ] All caps live in `ReportsOptions` bound from `appsettings.json`; `ErrorCodes.cs`, the entity and the validator contain **zero** numeric literals for lengths or counts.
- [ ] `SubmitReportHandler` guards existence before the cap, calls `SaveChangesAsync` exactly once and only on the success path, has no `try`/`catch`, and does **not** throw `UnauthorizedCoreException`.
- [ ] `currentUserService.UserId == Guid.Empty` is normalised to `null` before it reaches `Report.Create`.
- [ ] The item lookup accepts `Draft`, `Published` and `Unlisted` alike, so the response cannot be used to distinguish item status.
- [ ] `ReportsController` carries class-level `[Authorize]` **and** action-level `[AllowAnonymous]`; an anonymous `POST /api/reports` returns 200 and a bearer-carrying `POST` stores `ReporterUserId` (verify by inspecting the row, not by unit test).
- [ ] `AppDbContext` has `DbSet<Report> Reports { get; set; }`, a `ConfigureReports` private method, and a `Report` line in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`. No `.Where(!IsDeleted)` appears anywhere in the slice.
- [ ] Migration is named `reports006`, creates the `Reports` table with the three enum columns as `nvarchar(50)`, `Details` as `nvarchar(1000)`, and both non-unique indexes; `AppDbContextModelSnapshot.cs` is regenerated by the tool, not hand-edited.
- [ ] All 7 error codes exist in `ErrorCodes.cs`, `Messages.en.resx` **and** `Messages.ar.resx`; `{limit}` is present in both language values of `REPORT_LIMIT_REACHED`.
- [ ] `postman/e3a.postman_collection.json` has the `Reports / Submit Report` request with `"auth": { "type": "noauth" }` and the example body above.
- [ ] Tests 1–19 exist with exactly these class and method names; every `throw` branch has a test; `SaveChangesAsync` is `Received(1)` on tests 4, 5, 7, 9 and `DidNotReceive()` on tests 10, 11, 12.
- [ ] The three mutation checks were performed, both outcomes recorded, and the source restored byte-exactly (verified with `cmp`/`md5sum`).
- [ ] The report states plainly that the step-4 `CountAsync` predicate is not constrained by any test.
- [ ] `ReportContext.submit()` calls `submitReport` (never `fetch`), shows the toast only inside `.then`, and renders `messageForApiError(error)` in the modal on failure without closing it.
- [ ] The submit button is disabled while `submitting` and when `canSubmitReport` is false; both buttons carry `type="button"`; the reason control is a `<select>` and the close control is a `<button>`.
- [ ] `reportReasons` no longer exists in `web/src/lib/catalog.ts`; the four labels are unchanged from today's strings.
- [ ] All 7 report codes are in `web/src/lib/errorMessages.ts`; no SCREAMING_SNAKE value can reach the screen.
- [ ] Tests 20–30 exist in `web/src/lib/reportsApi.test.ts`; no component test was added and no DOM library was introduced.
- [ ] `docs/implementation-plan.md` (3 sections) and `docs/security-scan.md` (1 bullet) are updated in this same change; no doc was created outside `/docs`.
- [ ] `dotnet build` zero new warnings · `dotnet test` green · `npm run build` zero TypeScript errors · `npm run test` green · `npx oxlint` no new warnings against a baseline measured from `git archive HEAD web`, with zero `oxlint-disable` / `@ts-ignore`.
