# Implementation — Stop the API leaking exception detail in error bodies

## Files created

| Path | Lines | Purpose |
|------|-------|---------|
| `api/E3A.Tests/CoreExceptions/Shared/ExceptionDetailsFactory.cs` | 28 | Builds an `ExceptionDetails` whose `Exception` has a genuine (non-null) `StackTrace`, by throwing and catching a `NotFoundCoreException(Code)` inside `try`/`catch`. `public const string Code = "TEST_ERROR_CODE";` |
| `api/E3A.Tests/CoreExceptions/ErrorResponseHandlerTests.cs` | 63 | Tests 1–3: Development includes diagnostics; the `[Theory]` over `"Production"`/`"Staging"`/`"QualityAssurance"` proves the gate is "is it Development"; the explicit-`data` overload is untouched by the gate. |
| `api/E3A.Tests/CoreExceptions/ErrorResponseHandlerSerializationTests.cs` | 55 | Tests 4–5: the emitted JSON (serialized with `new JsonSerializerOptions(JsonSerializerDefaults.Web)`, mirroring `CoreExceptionMiddleware.ErrorSerializerOptions`) parsed via `JsonDocument`, asserting on properties and property count. |

No production file was created. No new error code, exception type, interface, options class or DI registration.

## Files modified

| Path | Change |
|------|--------|
| `api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs` | Added `using Microsoft.Extensions.Hosting;`; primary constructor is now `(ILocalizer localizer, IHostEnvironment environment)` on one line; `Data = GenerateDiagnosticData(exceptionDetails.Exception)`; new `private string? GenerateDiagnosticData(Exception? exception)` returning `null` unless `environment.IsDevelopment()`. Verbatim as specified in the plan, including the three-line invariant comment. The two other overloads are byte-identical (confirmed by the diff: no hunk touches them). |
| `api/core-libraries/Core.Exceptions/ErrorResponse.cs` | Added `using System.Text.Json.Serialization;` and `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` on `ErrorResponse<T>.Data`. Nothing else. `WhenWritingNull` was **not** used; no `DefaultIgnoreCondition` was set anywhere. |
| `api/core-libraries/Core.Exceptions/ExceptionMiddleware.cs` | Exactly one deleted line: `using Microsoft.Extensions.Hosting;`. Verified: `git diff` for this file is a single `-` line and zero `+` lines. |
| `api/E3A.Api/Resources/Messages.en.resx` | One `<data name="UNHANDLED_EXCEPTION">` block added as the last entry before `</root>`. File now has 89 `<data name=` entries. |
| `api/E3A.Api/Resources/Messages.ar.resx` | Matching Arabic block, no tashkeel, no placeholders, last entry before `</root>`. File now has 89 `<data name=` entries. |
| `api/E3A.Tests/E3A.Tests.csproj` | One new `ProjectReference` to `../core-libraries/Core.Exceptions/Core.Exceptions.csproj`, placed first in the existing project-reference `ItemGroup`. |

`IErrorResponseHandler.cs`, `Core.Exceptions/DependencyInjection.cs`, `E3A.Api/Program.cs`, `postman/e3a.postman_collection.json`, `/docs`, `web/`, `dotnet-templates/`, `Directory.Packages.props` and `E3a.slnx` are all unmodified. `git status --porcelain` at the end is exactly:

```
 M api/E3A.Api/Resources/Messages.ar.resx
 M api/E3A.Api/Resources/Messages.en.resx
 M api/E3A.Tests/E3A.Tests.csproj
 M api/core-libraries/Core.Exceptions/ErrorResponse.cs
 M api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs
 M api/core-libraries/Core.Exceptions/ExceptionMiddleware.cs
?? .process/stack-trace-leak/
?? api/E3A.Tests/CoreExceptions/
```

`git diff --stat`: 6 files, 27 insertions, 4 deletions.

## Deviations

| Plan said | Reality | What I did |
|---|---|---|
| Definition of Done: "run the API with `ASPNETCORE_ENVIRONMENT=Production`, `GET /api/catalog/{unknown-slug}` returns a body with exactly the keys `code` and `message`". | The API **cannot boot** as `Production` on this machine. `E3A.Api/Program.cs:26-32` runs `builder.Configuration.AddAzureAppConfiguration(new Uri(endpoint!), …)` whenever `IsProduction()`, and `appsettings.json` has `"Azure:AACAppSettingsEndpoint": ""`. Startup dies with `System.UriFormatException: Invalid URI: The URI is empty.` at `Program.cs:line 31`, before Kestrel binds. Making it boot would require either an Azure App Configuration endpoint plus managed-identity credentials, or editing `Program.cs` — which is explicitly out of scope for this slice ("`E3A.Api/Program.cs` is not touched"). | Ran the non-Development leg as `ASPNETCORE_ENVIRONMENT=Staging` instead, which exercises the identical code path (the gate is `IHostEnvironment.IsDevelopment()`, so every non-`Development` name behaves the same, and `"Staging"` is one of the three `[InlineData]` cases in test 2). Literal bodies for both legs are recorded below, along with the verbatim Production startup failure. I did **not** silently claim a Production run. |

Nothing else deviates: every file, signature, test name and resx entry is as the plan specifies.

## Build & test

### `dotnet build` (from `api/`)

```
Build succeeded.
    9 Warning(s)
    0 Error(s)
Time Elapsed 00:00:36.73
```

All 9 warnings are pre-existing CS8618/CS8602 in vendored libraries this slice does not touch (`Core.Notifications`, `Core.OTP`, `Core.Validation`). Zero warnings in `Core.Exceptions`, `E3A.Api` or `E3A.Tests`. No new warnings.

### `dotnet test` (from `api/`, full suite)

```
Passed!  - Failed:     0, Passed:   777, Skipped:     0, Total:   777, Duration: 771 ms - E3A.Tests.dll (net10.0)
```

Filtered to the new tests only (`--filter "FullyQualifiedName~CoreExceptions"`):

```
Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 132 ms - E3A.Tests.dll (net10.0)
```

7 = 5 test methods, with test 2 as a 3-case `[Theory]`.

### Mutation check 1 — invert the gate

`ErrorResponseHandler.cs` was copied to the scratchpad first (`md5 b789e49b01b1fa6e1c85c873ef817583`), then `GenerateDiagnosticData` was reduced to `return $"{exception?.Message} - {exception?.StackTrace}";` (the `if (!environment.IsDevelopment()) { return null; }` block deleted), so it returns the diagnostic string unconditionally.

Observed, verbatim:

```
  Failed E3A.Tests.CoreExceptions.ErrorResponseHandlerTests.GenerateErrorResponse_ShouldOmitExceptionDiagnostics_WhenEnvironmentIsNotDevelopment(environmentName: "Staging") [60 ms]
  Failed E3A.Tests.CoreExceptions.ErrorResponseHandlerSerializationTests.Serialize_ShouldEmitOnlyCodeAndMessage_WhenEnvironmentIsNotDevelopment [49 ms]
  Failed E3A.Tests.CoreExceptions.ErrorResponseHandlerTests.GenerateErrorResponse_ShouldOmitExceptionDiagnostics_WhenEnvironmentIsNotDevelopment(environmentName: "Production") [1 ms]
  Failed E3A.Tests.CoreExceptions.ErrorResponseHandlerTests.GenerateErrorResponse_ShouldOmitExceptionDiagnostics_WhenEnvironmentIsNotDevelopment(environmentName: "QualityAssurance") [< 1 ms]
Failed!  - Failed:     4, Passed:     3, Skipped:     0, Total:     7, Duration: 257 ms - E3A.Tests.dll (net10.0)
```

That is exactly the expected outcome: test 2 (all three `[InlineData]` cases) and test 4 failed; tests 1, 3 and 5 passed. Failure message on test 4:

```
  Error Message:
   Expected document.RootElement.TryGetProperty("data", out _) to be False, but found True.
```

Restored from the byte-exact copy:

```
$ cp "$SCRATCH/ErrorResponseHandler.cs.orig" api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs
$ cmp "$SCRATCH/ErrorResponseHandler.cs.orig" api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs && echo "cmp: identical"
cmp: identical
b789e49b01b1fa6e1c85c873ef817583 *api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs
```

### Mutation check 2 — remove `[JsonIgnore]`

`ErrorResponse.cs` was copied first (`md5 d71064f347765abee69fe6fadbfdc9d6`), then the `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` line was removed. The now-unused `using System.Text.Json.Serialization;` was removed with it, because `TreatWarningsAsErrors` would otherwise fail the build before any test could run.

Observed, verbatim:

```
  Failed E3A.Tests.CoreExceptions.ErrorResponseHandlerSerializationTests.Serialize_ShouldEmitOnlyCodeAndMessage_WhenEnvironmentIsNotDevelopment [55 ms]
  Error Message:
   Expected document.RootElement.TryGetProperty("data", out _) to be False, but found True.
Failed!  - Failed:     1, Passed:     6, Skipped:     0, Total:     7, Duration: 249 ms - E3A.Tests.dll (net10.0)
```

Exactly the expected outcome: only test 4 fails, and it fails on `data` being present (the `TryGetProperty("data")` assertion runs before `HaveCount(2)`, so it is the reported failure; both would have failed — `"data":null` makes the object three properties).

Restored from the byte-exact copy:

```
$ cmp "$SCRATCH/ErrorResponse.cs.orig" api/core-libraries/Core.Exceptions/ErrorResponse.cs && echo "cmp: identical"
cmp: identical
d71064f347765abee69fe6fadbfdc9d6 *api/core-libraries/Core.Exceptions/ErrorResponse.cs
```

Full suite re-run after both restores: `Failed: 0, Passed: 777`.

### Manual wire check

The API was run from `api/E3A.Api` as `dotnet bin/Debug/net10.0/E3A.Api.dll` with `ASPNETCORE_URLS=https://localhost:62935`, and `curl -k` used against `https://localhost:62935/api/catalog/some-unknown-slug`. Every instance was killed afterwards; `netstat` shows no listener on 62935 and `tasklist` shows no E3A process.

**Development** — `data` still present (unchanged behaviour), literal body:

```
HTTP/1.1 404 Not Found
Content-Type: application/json
X-Trace-Id: dae6ab6474330abe3b79feb52db76e65
X-Debug-Id: dae6ab6474330abe3b79feb52db76e65|04-09-2026:20:24:15|E3A|Europe|DE|DESKTOP-B2BJ9O2

{"data":"Exception of type \u0027Core.Errors.NotFoundCoreException\u0027 was thrown. -    at E3A.Application.Catalog.GetCatalogEngineer.GetCatalogEngineerQueryHandler.Handle(GetCatalogEngineerQuery request, CancellationToken cancellationToken) in D:\\Personal\\_e3a\\api\\E3A.Application\\Catalog\\GetCatalogEngineer\\GetCatalogEngineerQueryHandler.cs:line 17\r\n   at Core.CQRS.Behaviours.ValidationBehaviour\u00602.Handle(...)\r\n   [... full trace ...]","code":"ENGINEER_NOT_FOUND","message":"We couldn\u0027t find that engineer."}
```

(The trace is truncated here for readability only; the live body carried the complete stack trace, as before this change.)

**Production** — could not boot; verbatim console output:

```
Unhandled exception. System.UriFormatException: Invalid URI: The URI is empty.
   at System.Uri.CreateThis(String uri, Boolean dontEscape, UriKind uriKind, UriCreationOptions& creationOptions)
   at System.Uri..ctor(String uriString)
   at Program.<Main>$(String[] args) in D:\Personal\_e3a\api\E3A.Api\Program.cs:line 31
   at Program.<Main>(String[] args)
```

**Staging** (the substitute non-Development leg) — exactly the keys `code` and `message`, no stack trace, literal body:

```
HTTP/1.1 404 Not Found
Content-Type: application/json
X-Trace-Id: 8fda4b5ac22d71b5c2b2149c33e297df
X-Debug-Id: 8fda4b5ac22d71b5c2b2149c33e297df|04-09-2026:20:25:40|E3A|Europe|ST|DESKTOP-B2BJ9O2

{"code":"ENGINEER_NOT_FOUND","message":"We couldn\u0027t find that engineer."}
```

**Bonus — the 500 / `UNHANDLED_EXCEPTION` leg** (plan line 235 says the resx lookup "is verified … by the manual 500 check"; the unit tests deliberately cannot reach the real resource file). Same Staging instance restarted with `ConnectionStrings__DbConnectionString` pointed at a non-existent database so the SQL failure surfaces as a non-`BaseException` 500. Literal body:

```
HTTP/1.1 500 Internal Server Error
Content-Type: application/json
X-Trace-Id: 8346f997ef8cb41649e642ee8eca8cc3

{"code":"UNHANDLED_EXCEPTION","message":"Something went wrong on our side. Please try again."}
```

Before this change that `message` would have been the raw `SqlException` text. This confirms the new resx key resolves through `Localizer` at runtime and closes the second leak path. No file was edited for this check — the connection string was overridden by environment variable only.

## Notes for review

- **The Production leg of the wire check is Staging.** See Deviations. `Program.cs:26-32` makes a local `Production` boot impossible without Azure App Configuration; I judged editing `Program.cs` (or `appsettings.json`) to be a worse outcome than substituting an equivalent non-Development environment name and saying so. If you want a literal `Production` body, it needs an AAC endpoint or a temporary `Program.cs` change that is not part of this slice.
- **Mutation 2 also removed the now-unused `using System.Text.Json.Serialization;`.** With `TreatWarningsAsErrors` on, leaving it in would have failed the build (CS8019/IDE0005) and produced no test result at all. The restore was still `cp` + `cmp` from the pre-mutation byte copy, not a re-edit.
- **`ErrorResponseHandler.cs` has no trailing newline**, both before and after — that is pre-existing and preserved, which is why `git diff` shows `\ No newline at end of file`.
- **The three-line comment in `GenerateDiagnosticData` is deliberate** and is the plan's verbatim text. It documents a non-obvious security invariant (why the value is dropped and where the information went instead), which is the one case the skill's zero-comments rule allows.
- **Serialization order** puts `data` first in the Development body (`{"data":…,"code":…,"message":…}`) because `System.Text.Json` emits derived-type properties before base-type ones. That is pre-existing, unchanged, and irrelevant to any consumer; `web/src/lib/http.ts` reads by key.
- **Test 3 asserts `result.Data.Should().Be(42)`** on `ErrorResponse<int>`. Note this overload has zero production call sites today (Decision 4); the test exists to lock the boundary of the gate, not to cover live behaviour.
- The `git status` snapshot in my task context showed a modified `.gitignore`, a modified `ProcessPublishJobFunction.cs` and an untracked `api/api-run.log`. None of those were present when I started on `feature/stack-trace-leak`; the tree was clean apart from `.process/stack-trace-leak/`, and every change listed above is mine.
