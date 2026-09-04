# Plan — Stop the API leaking exception detail in error bodies

## Goal

Today every error the E3A API returns — in every environment — carries a `data` field holding
`"{exception.Message} - {exception.StackTrace}"`, and every unhandled 500 also carries the raw
.NET exception message in `message`. A plain `GET /api/catalog/{unknown-slug}` returns absolute
source paths (`D:\Personal\_e3a\api\E3A.Application\Catalog\...:line 17`) to any anonymous caller.
After this ships, a client outside Development receives exactly two fields — `code` and `message` —
where `message` is the localized, client-safe string from the resx files; the exception message and
stack trace never appear in a response body. In Development the current diagnostic `data` field is
preserved unchanged. Nothing is lost operationally: the 5xx branch of `CoreExceptionMiddleware`
already logs the full exception object, and `CoreRequestLoggingMiddleware` already returns
`X-Trace-Id` / `X-Debug-Id` response headers, so a production caller can still quote a correlation
id for the log the trace now lives in.

## Scope

**In:**
- `ErrorResponseHandler` gains an `IHostEnvironment` dependency and populates `Data` only in Development.
- `ErrorResponse<T>.Data` is omitted from the emitted JSON when it is the default, so the body is `{code, message}` and not `{code, message, data: null}`.
- The missing `UNHANDLED_EXCEPTION` resource strings are added to `Messages.en.resx` and `Messages.ar.resx`, closing the second leak path (the localizer falls back to `exception.Message` when a code has no resx key).
- Removal of the provably dead `using Microsoft.Extensions.Hosting;` in `ExceptionMiddleware.cs`.
- Unit tests in the existing `api/E3A.Tests` project, covering Development and non-Development, on both the returned object and the emitted JSON.

**Out:**
- Any endpoint, route, request or documented response-field change. There is none.
- `postman/` — see Decision 13.
- `/docs` — see Decision 14.
- `web/` — see Decision 15.
- Middleware logging behaviour (already correct — see Decision 5).
- `IErrorResponseHandler` signatures, `ExceptionDetails`, `ExceptionErrorCodes`, DI registration.

**Deferred:**
| Item | Why deferred |
|---|---|
| Auditing all 88 resx keys against `E3A.Application/Exceptions/ErrorCodes.cs` for other missing keys with the same fallback-leak shape | Same defect class but a different, larger unit of work across ~88 keys; not needed to close the reported leak. `UNHANDLED_EXCEPTION` is included here only because it is the *exception message* path the request names explicitly. |
| Propagating the fix to `dotnet-templates/solution/core-libraries/Core.Exceptions/` | That tree is not in `E3a.slnx`, is never built, and precedent commit `7973b36` ("fix(core): camelCase error bodies…") fixed the vendored copy only. |
| Surfacing `X-Trace-Id` inside the error body so a client can quote a correlation id | The correlation path already exists as a response header (`CoreRequestLoggingMiddleware`). Putting it in the body is a deliberate contract addition, i.e. a separate slice. |

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Does the gate live in the handler, the middleware, or the response shape? | **The handler.** `ErrorResponseHandler.GenerateErrorResponse(ExceptionDetails)` returns `Data = null` unless `IHostEnvironment.IsDevelopment()`. | It is the single place that builds the string, so no present or future caller can bypass it. All three `IErrorResponseHandler` signatures stay byte-identical, so no consumer breaks. A middleware gate would leave the leak reachable by any direct call to the handler, and middleware is explicitly out of unit-test scope (`conventions/dotnet-testing.md` §5) while the handler is a pure two-dependency unit. Changing the response shape (e.g. returning `ErrorResponse` instead of `ErrorResponse<string>`) is a breaking interface change and would delete the Development diagnostics the request asks us to keep. |
| 2 | `IHostEnvironment` or `IWebHostEnvironment`? | `IHostEnvironment` (`Microsoft.Extensions.Hosting`). | `Core.Exceptions.csproj` line 11 already references `Microsoft.Extensions.Hosting.Abstractions` and does not reference `Microsoft.AspNetCore.Hosting.Abstractions`. Both neighbouring Core libraries that gate on environment use `IHostEnvironment`: `Core.Azure/Clients/MIClient.cs:12` and `Core.Logging/RequestLoggingMiddleware.cs:11`. Mirror, don't modernize. |
| 3 | Does the DI registration change? | No. `Core.Exceptions/DependencyInjection.cs` stays exactly as it is: `services.AddScoped<IErrorResponseHandler, ErrorResponseHandler>();` | `IHostEnvironment` is registered as a singleton by the generic host in `WebApplication.CreateBuilder`; a scoped service may depend on a singleton, so no lifetime problem and no extra registration. |
| 4 | Does any other consumer of `IErrorResponseHandler` break? | No consumer changes at all. | Verified by solution-wide grep: the only call site of `GenerateErrorResponse(ExceptionDetails)` anywhere is `ExceptionMiddleware.cs:70-71`. The other two overloads have zero call sites. `E3A.Jobs.csproj` does **not** reference `Core.Exceptions` (its `ProjectReference` list is `Core.CQRS`, `E3A.Application`, `E3A.Domain`, `E3A.Infrastructure`), and `E3A.Jobs/Program.cs` never calls `AddCoreExceptions()` nor registers the middleware — so the Jobs host is untouched by this slice. |
| 5 | Should the middleware start logging the stack trace? | **No middleware logging change.** | `ExceptionMiddleware.cs:63` already calls `logger.LogError(exception, …)` on the 5xx branch, passing the exception object, so the full stack trace already reaches the log store. The 4xx branch deliberately omits it per `SKILL.md` §8.7. The information genuinely moves from the body to a log that already has it; adding a second log statement would duplicate it. |
| 6 | Emit `"data": null`, or omit the key? | **Omit.** Annotate `ErrorResponse<T>.Data` with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`. | `JsonSerializerDefaults.Web` (the options `CoreExceptionMiddleware` uses) sets no default ignore condition, so a null `Data` would still serialize as `"data":null` and the body would not be "only `code` and `message`". Two rejected alternatives: (a) `JsonIgnoreCondition.WhenWritingNull` throws `InvalidOperationException` at serialization time if `ErrorResponse<T>` is ever closed over a value type, because the property type `T?` is non-nullable for unconstrained `T`; `WhenWritingDefault` is legal for every `T`. (b) Setting `DefaultIgnoreCondition` on the middleware's `ErrorSerializerOptions` would also drop `code` when it is null, and `BaseException.ErrorCode` is declared `string?`, so that is reachable — the failure contract clients read must stay stable. The attribute is scoped to the one property that is the subject of this slice. |
| 7 | Is the exception *message* leaking anywhere other than `Data`? | **Yes — fix it in this slice.** Add `UNHANDLED_EXCEPTION` to both resx files. | `ExceptionMiddleware.cs:46-47` sets `Code = ExceptionErrorCodes.UnhandledException` and `Message = exception.Message` for any non-`BaseException`. `Localizer.GetMessage` (`Core.Localization/Localizer.cs:16`) returns `fallbackMessage` when `localized.ResourceNotFound`. `UNHANDLED_EXCEPTION` has **no key in either resx file** (verified by grep across all 88 keys), so every unhandled 500 today returns the raw .NET exception message in `message` — exactly the "exception message must not appear in the response body" the goal forbids. Resource-only fix: no code change, no new error-code constant. |
| 8 | Does adding that resx key weaken Development diagnostics? | No. | In Development `Data` still carries `"{Message} - {StackTrace}"`, so the raw exception message remains available to the developer; only the client-facing `message` becomes the generic localized string. |
| 9 | New test project, or the existing `api/E3A.Tests`? | **Existing `api/E3A.Tests`.** Add one line to `E3A.Tests.csproj`: `<ProjectReference Include="../core-libraries/Core.Exceptions/Core.Exceptions.csproj" />`. | `E3A.Tests` today references only `E3A.Application` and `E3A.Domain` and has no core-library system under test — but nothing prevents one: `ErrorResponseHandler` is a pure unit with two substitutable dependencies and no `HttpContext`. A new `Core.Exceptions.Tests` project would add a csproj, an `E3a.slnx` entry and duplicated package wiring to host two test classes; that does not clear the skill's "no new abstractions" bar. `Microsoft.Extensions.Hosting.Abstractions` flows transitively through the new project reference, so `Directory.Packages.props` needs no new entry. |
| 10 | Test folder and namespace name | `api/E3A.Tests/CoreExceptions/`, namespace `E3A.Tests.CoreExceptions`. **Not** `E3A.Tests.Core.Exceptions`. | A `Core` segment inside the test namespace would make any qualified use of `Core.Exceptions.X` or `Core.Localization.X` bind to `E3A.Tests.Core` first and fail to compile. |
| 11 | Can `[InlineData(Environments.Production)]` be used? | **No — use the string literals** `"Production"`, `"Staging"`, `"QualityAssurance"` in `[InlineData]`. | `Microsoft.Extensions.Hosting.Environments` members are `public static readonly string`, not `const`, so they are not valid attribute arguments (CS0182). `Environments.Development` is still used in ordinary statements, where it is legal. |
| 12 | How do we assert "no stack trace in the body" without writing a vacuous test? | Serialize, then parse with `JsonDocument` and assert on **properties**. Never `json.Should().NotContain(stackTrace)`. | A stack trace is escaped when serialized (`\` → `\\`, newlines → `\r\n`), so a substring `NotContain` against the raw trace passes even when the trace *is* in the body — the exact unfalsifiable shape `conventions/dotnet-testing.md` §9 forbids. `TryGetProperty("data", …)` and an object-property count cannot be fooled that way. |
| 13 | Does `postman/e3a.postman_collection.json` need any change? | **No. Do not add, edit or delete any request.** | No endpoint, route, method, request body or documented response field changes. The collection contains zero `pm.test` / `pm.expect` scripts and zero references to `data` (verified by grep over all 694 lines), so nothing in it describes the error body. |
| 14 | Does `/docs` need an edit under `.claude/rules/docs-sync.md`? | **No, and no new doc is created.** | Read in full: `docs/architecture.md` (77 lines) and `docs/constitution.md` (154 lines). Neither asserts anything about the error response body's shape, its fields, or stack-trace exposure — a grep for `stacktrace`/`stack trace`/`ErrorResponse`/`error contract`/`error body`/`error envelope` across all of `/docs` returns nothing. The only adjacent statement is `constitution.md` §4 ("`Core.Exceptions` middleware formats responses"), which remains true after this change. docs-sync requires an edit only when a doc "still says the old thing"; none does, so this is not divergence. |
| 15 | Does `web/` need a change? | **No.** | `web/src/lib/http.ts:31` types the error body as `Promise<{ code?: string; message?: string }>` and line 69 reads only `errorBody.code` and `errorBody.message`. `data` is never read anywhere in `web/src`. Removing it is invisible to the SPA. |
| 16 | The unused `using Microsoft.Extensions.Hosting;` at `ExceptionMiddleware.cs:5` | **Remove it.** No other change to that file. | It is provably unused today and, once the gate lands in the handler, provably will never be used there. Leaving it in place signals a half-finished gate in the first file a reader opens when auditing this behaviour. |
| 17 | Update `dotnet-templates/solution/core-libraries/Core.Exceptions/`? | **No.** | Not listed in `E3a.slnx`, never built, and the directly analogous prior fix (`7973b36`) touched `api/core-libraries` only. Recorded under Deferred. |

## Existing code touched

| File | Change |
|------|--------|
| `api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs` | Add `using Microsoft.Extensions.Hosting;`. Add `IHostEnvironment environment` as the second primary-constructor parameter. Replace the inline `Data = $"…"` expression with a call to a new private method that returns `null` outside Development. Exact contract below. |
| `api/core-libraries/Core.Exceptions/ErrorResponse.cs` | Add `using System.Text.Json.Serialization;` and `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` on `ErrorResponse<T>.Data`. Nothing else. |
| `api/core-libraries/Core.Exceptions/ExceptionMiddleware.cs` | Delete line 5, `using Microsoft.Extensions.Hosting;`. **No other edit to this file** — the logging branches, the serializer options and the middleware body are already correct. |
| `api/E3A.Api/Resources/Messages.en.resx` | Add one `<data name="UNHANDLED_EXCEPTION">` block as the last entry, immediately before `</root>`. |
| `api/E3A.Api/Resources/Messages.ar.resx` | Add the matching `<data name="UNHANDLED_EXCEPTION">` block as the last entry, immediately before `</root>`. |
| `api/E3A.Tests/E3A.Tests.csproj` | Add `<ProjectReference Include="../core-libraries/Core.Exceptions/Core.Exceptions.csproj" />` to the existing `ItemGroup` that holds the `E3A.Application` and `E3A.Domain` references, keeping the list alphabetical by placing it first in that group. |

### Exact expected content — `ErrorResponseHandler.cs`

```csharp
using Core.Localization;
using Microsoft.Extensions.Hosting;

namespace Core.Exceptions;

public sealed class ErrorResponseHandler(ILocalizer localizer, IHostEnvironment environment) : IErrorResponseHandler
{
    public ErrorResponse GenerateErrorResponse(string code, string message)
    {
        return new ErrorResponse
        {
            Code = code,
            Message = localizer.GetMessage(code, message)
        };
    }
    public ErrorResponse<T> GenerateErrorResponse<T>(string code, string message, T data)
    {
        return new ErrorResponse<T>
        {
            Code = code,
            Message = localizer.GetMessage(code, message),
            Data = data
        };
    }
    public ErrorResponse<string> GenerateErrorResponse(ExceptionDetails exceptionDetails)
    {
        ArgumentNullException.ThrowIfNull(exceptionDetails);

        return new ErrorResponse<string>
        {
            Code = exceptionDetails.Code,
            Message = localizer.GetMessage(exceptionDetails.Code, exceptionDetails.Message, exceptionDetails.Context),
            Data = GenerateDiagnosticData(exceptionDetails.Exception)
        };
    }

    // Exception message and stack trace expose absolute source paths and internal call structure.
    // Outside Development they belong in the log only - the 5xx branch of CoreExceptionMiddleware
    // already logs the full exception - never in a body an anonymous client can read.
    private string? GenerateDiagnosticData(Exception? exception)
    {
        if (!environment.IsDevelopment())
        {
            return null;
        }

        return $"{exception?.Message} - {exception?.StackTrace}";
    }
}
```

The two ungated overloads are unchanged: an explicitly supplied `data` payload is a caller's
deliberate choice, not exception detail, and must keep flowing in every environment.

### Exact expected content — `ErrorResponse.cs`

```csharp
using System.Text.Json.Serialization;

namespace Core.Exceptions;

public class ErrorResponse<T> : ErrorResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public T? Data { get; set; }
}

public class ErrorResponse
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}
```

### Wire-format before / after (§8.6 — verify the emitted JSON, not the object)

| Case | Before | After |
|---|---|---|
| 404 in Production | `{"code":"ENGINEER_NOT_FOUND","message":"We couldn't find that engineer.","data":"Exception of type 'Core.Errors.NotFoundCoreException' was thrown. -    at E3A.Application.Catalog…:line 17"}` | `{"code":"ENGINEER_NOT_FOUND","message":"We couldn't find that engineer."}` |
| 500 in Production | `{"code":"UNHANDLED_EXCEPTION","message":"<raw .NET exception message>","data":"<message> - <stack trace>"}` | `{"code":"UNHANDLED_EXCEPTION","message":"Something went wrong on our side. Please try again."}` |
| 404 in Development | unchanged | unchanged — `data` still present with message and stack trace |

## Files to create

| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/E3A.Tests/CoreExceptions/Shared/ExceptionDetailsFactory.cs` | test factory | namespace `E3A.Tests.CoreExceptions.Shared`; `public static class ExceptionDetailsFactory`; `public const string Code = "TEST_ERROR_CODE";`; `public static ExceptionDetails Thrown()`. The method must **throw and catch** a `NotFoundCoreException(Code)` inside a `try`/`catch` so that `Exception.StackTrace` is genuinely non-null, then return `new ExceptionDetails { Code = Code, Message = exception.Message, StatusCode = (int)HttpStatusCode.NotFound, Exception = exception }`. Do not construct the exception without throwing it — an unthrown exception has a null `StackTrace` and every assertion below would be vacuous. |
| 2 | `api/E3A.Tests/CoreExceptions/ErrorResponseHandlerTests.cs` | test class | namespace `E3A.Tests.CoreExceptions`; `public sealed class ErrorResponseHandlerTests`. Fields: `private readonly ILocalizer _localizer = Substitute.For<ILocalizer>();`, `private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();`, `private readonly ErrorResponseHandler _sut;`. Constructor stubs `_localizer.GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>()).Returns(LocalizedMessage);` where `private const string LocalizedMessage = "localized";`, then assigns `_sut = new ErrorResponseHandler(_localizer, _environment);`. Environment name is stubbed per test in the arrange block. |
| 3 | `api/E3A.Tests/CoreExceptions/ErrorResponseHandlerSerializationTests.cs` | test class | namespace `E3A.Tests.CoreExceptions`; `public sealed class ErrorResponseHandlerSerializationTests`. Same three fields and the same constructor wiring as #2, plus `private static readonly JsonSerializerOptions ErrorSerializerOptions = new(JsonSerializerDefaults.Web);` — mirroring `CoreExceptionMiddleware.ErrorSerializerOptions` exactly, so the test asserts the dialect the client actually receives. |

No production file is created. No new error-code constant, exception type, interface, options class or
DI registration is introduced by this slice.

## Error codes

No new constant is added. The one code involved already exists as
`Core.Exceptions.ExceptionErrorCodes.UnhandledException`; only its two missing resource strings are added.

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ExceptionErrorCodes.UnhandledException` | `UNHANDLED_EXCEPTION` | Not thrown — assigned by `CoreExceptionMiddleware.HandleExceptionAsync` (`ExceptionMiddleware.cs:47`) for any exception that is not a `Core.Errors.BaseException` | any non-`BaseException` (e.g. `SqlException`, `InvalidOperationException`) | 500 |

Resource strings to add, as the last `<data>` block before `</root>` in each file:

`api/E3A.Api/Resources/Messages.en.resx`
```xml
  <data name="UNHANDLED_EXCEPTION" xml:space="preserve">
    <value>Something went wrong on our side. Please try again.</value>
  </data>
```

`api/E3A.Api/Resources/Messages.ar.resx`
```xml
  <data name="UNHANDLED_EXCEPTION" xml:space="preserve">
    <value>حدث خطا لدينا. برجاء المحاولة مرة اخرى.</value>
  </data>
```

No runtime placeholders in either string, so nothing to keep in sync. Arabic without tashkeel,
matching the existing entries. After the edit both files must contain 89 `<data name=` entries.

## Domain behaviour

None. This slice has no entity, no aggregate, no domain method, no state transition, no
`BusinessRuleViolationException` guard and no `UpdationDate` stamp — nothing in `E3A.Domain` is
read or written. The only behavioural rule is the environment gate in
`ErrorResponseHandler.GenerateDiagnosticData`, specified verbatim above:

- `environment.IsDevelopment()` is true → return `$"{exception?.Message} - {exception?.StackTrace}"` (unchanged from today).
- otherwise → return `null`, and `ErrorResponse<T>.Data` is then omitted from the serialized body by `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`.

## API surface

**No change.** No controller is added or edited, no route changes, no HTTP method changes, no policy
constant is added, no request record is added, no response type changes. The only observable
difference is that the `data` field of the existing error body stops being emitted outside
Development, and that a 500's `message` becomes the localized string instead of the raw exception
message. `E3A.Api/Program.cs` is not touched — middleware order, DI composition and JSON options all
stay exactly as they are.

## Test plan

Enumerated per `conventions/dotnet-testing.md` §5. The implementer writes exactly these seven test
cases across three files and no others. No `// Arrange` comments; three unlabelled blocks separated
by one blank line; no `.ConfigureAwait(false)` inside test method bodies; all methods are
synchronous (the system under test has no async members).

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `ErrorResponseHandlerTests` | `GenerateErrorResponse_ShouldIncludeExceptionDiagnostics_WhenEnvironmentIsDevelopment` | Arrange `_environment.EnvironmentName.Returns(Environments.Development)` and `var details = ExceptionDetailsFactory.Thrown();`. Act `var result = _sut.GenerateErrorResponse(details);`. Assert `result.Data.Should().NotBeNull();` and `result.Data.Should().Contain(details.Exception!.StackTrace!);` and `result.Code.Should().Be(ExceptionDetailsFactory.Code);`. The `Contain` argument is read off the input object at runtime — never a hard-coded string. |
| 2 | `ErrorResponseHandlerTests` | `GenerateErrorResponse_ShouldOmitExceptionDiagnostics_WhenEnvironmentIsNotDevelopment` | `[Theory]` with `[InlineData("Production")]`, `[InlineData("Staging")]`, `[InlineData("QualityAssurance")]` bound to a `string environmentName` parameter. Arrange `_environment.EnvironmentName.Returns(environmentName)`. Assert `result.Data.Should().BeNull();` and `result.Code.Should().Be(ExceptionDetailsFactory.Code);`. The third case proves the gate is "is it Development", not "is it Production". |
| 3 | `ErrorResponseHandlerTests` | `GenerateErrorResponse_ShouldKeepExplicitData_WhenEnvironmentIsNotDevelopment` | Arrange `_environment.EnvironmentName.Returns("Production")`. Act `var result = _sut.GenerateErrorResponse(ExceptionDetailsFactory.Code, string.Empty, ExpectedPayload);` where `private const int ExpectedPayload = 42;`. Assert `result.Data.Should().Be(ExpectedPayload);`. Locks Decision 1: the gate applies to the `ExceptionDetails` overload only, so a caller-supplied payload is never silently dropped. |
| 4 | `ErrorResponseHandlerSerializationTests` | `Serialize_ShouldEmitOnlyCodeAndMessage_WhenEnvironmentIsNotDevelopment` | Arrange Production + `ExceptionDetailsFactory.Thrown()`. Act `var json = JsonSerializer.Serialize(_sut.GenerateErrorResponse(details), ErrorSerializerOptions); using var document = JsonDocument.Parse(json);`. Assert `document.RootElement.TryGetProperty("data", out _).Should().BeFalse();`, `document.RootElement.TryGetProperty("code", out _).Should().BeTrue();`, `document.RootElement.TryGetProperty("message", out _).Should().BeTrue();`, `document.RootElement.EnumerateObject().Should().HaveCount(2);`. This is the slice's headline invariant. |
| 5 | `ErrorResponseHandlerSerializationTests` | `Serialize_ShouldEmitDiagnosticsInData_WhenEnvironmentIsDevelopment` | Arrange Development + `ExceptionDetailsFactory.Thrown()`. Same serialize-and-parse act. Assert `document.RootElement.TryGetProperty("data", out var data).Should().BeTrue();` and `data.GetString().Should().Contain(details.Exception!.StackTrace!);`. Proves Development diagnostics survive, and proves test 4 is falsifiable — the same code path with a different environment produces the property test 4 asserts is absent. |

Not tested, deliberately, and the implementer must not add tests for these:

- `CoreExceptionMiddleware` — middleware is an integration concern, out of scope by `conventions/dotnet-testing.md` §5.
- The resx lookup for `UNHANDLED_EXCEPTION` — `Core.Localization/Localizer.cs:8` resolves resources from `Assembly.GetEntryAssembly()`, which under a test host is the xUnit runner, not `E3A.Api`. There is no way to exercise the real resource file from a unit test; it is verified by inspection (key present in both files, counts equal) and by the manual 500 check in the Definition of Done.
- The pre-existing `ArgumentNullException.ThrowIfNull(exceptionDetails)` guard — unchanged behaviour, not part of this slice.

## Definition of done

- [ ] `ErrorResponseHandler` takes `(ILocalizer localizer, IHostEnvironment environment)` on one line, and `GenerateErrorResponse(ExceptionDetails)` returns `Data == null` for every environment name that is not `Development` (case-insensitive, via `IsDevelopment()`).
- [ ] The two other `IErrorResponseHandler` overloads are byte-identical to before; the interface file `IErrorResponseHandler.cs` is not modified.
- [ ] `Core.Exceptions/DependencyInjection.cs` is not modified.
- [ ] `ErrorResponse<T>.Data` carries `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` — **not** `WhenWritingNull`, and `DefaultIgnoreCondition` is not set on any `JsonSerializerOptions`.
- [ ] `ExceptionMiddleware.cs` differs from `HEAD` by exactly one deleted line (`using Microsoft.Extensions.Hosting;`). The logging branches, `ErrorSerializerOptions` and the middleware body are untouched.
- [ ] `UNHANDLED_EXCEPTION` exists in both `Messages.en.resx` and `Messages.ar.resx`; both files contain 89 `<data name=` entries; the Arabic value carries no tashkeel and no placeholders.
- [ ] `api/E3A.Tests/E3A.Tests.csproj` has exactly one new `ProjectReference`, to `Core.Exceptions`. `Directory.Packages.props` is not modified. `E3a.slnx` is not modified. No new test project exists.
- [ ] Exactly three new files exist, at the three paths in "Files to create". No other file is created anywhere in the repo.
- [ ] All seven test cases from the Test plan exist with exactly those class and method names; no additional tests were added.
- [ ] `ExceptionDetailsFactory.Thrown()` throws and catches, and `details.Exception!.StackTrace` is non-null when the tests run (otherwise tests 1 and 5 are vacuous).
- [ ] No test asserts on a message string literal, and no test uses `Should().NotContain(<stack trace>)` against raw JSON text.
- [ ] **Mutation check (`conventions/dotnet-testing.md` §9):** invert the gate in `GenerateDiagnosticData` (make it return the diagnostic string unconditionally), run `dotnet test`, and confirm tests 2 and 4 fail and tests 1, 3 and 5 still pass. Then restore from a byte-exact copy and verify with `md5sum`/`cmp` — not by re-editing from memory. Record both observed outcomes in the implementation report.
- [ ] **Second mutation check:** remove the `[JsonIgnore]` attribute, run `dotnet test`, and confirm test 4 fails on `HaveCount(2)` / `TryGetProperty("data")`. Restore and verify byte-exact.
- [ ] `dotnet build` from `api/` succeeds with zero new warnings (`TreatWarningsAsErrors` is on, so any unused using or analyzer finding is a build failure).
- [ ] `dotnet test` green — the full existing suite plus the seven new cases.
- [ ] Manual wire check, recorded in the report with the literal response bodies: run the API with `ASPNETCORE_ENVIRONMENT=Production`, `GET /api/catalog/{unknown-slug}` returns a body with exactly the keys `code` and `message` and no stack trace; then run with `ASPNETCORE_ENVIRONMENT=Development` and confirm `data` is still present.
- [ ] `postman/e3a.postman_collection.json` is unmodified.
- [ ] `/docs` is unmodified and no new `.md` file was created outside `.process/stack-trace-leak/`.
- [ ] `web/` is unmodified.
- [ ] `dotnet-templates/` is unmodified.
