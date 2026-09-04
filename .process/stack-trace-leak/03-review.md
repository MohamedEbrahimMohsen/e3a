VERDICT: APPROVED

# Review — Stop the API leaking exception detail in error bodies

No blocking findings. The gate is closed at the only place that builds the string, the wire format
was verified rather than assumed, and the tests are falsifiable.

## Non-blocking
- `api/E3A.Api/Program.cs:26-31` — `new Uri(endpoint!)` on an empty `Azure:AACAppSettingsEndpoint`
  makes a local `Production` boot impossible (`UriFormatException` before Kestrel binds). Pre-existing,
  untouched by this slice, and correctly declared as the reason the Production wire leg was run as
  `Staging`. Worth a separate slice (guard the endpoint, or fall back to local configuration) so the
  Production configuration path is testable at all; it does not gate this change.
- `api/core-libraries/Core.Exceptions/ErrorResponse.cs:7` — `WhenWritingDefault` also omits `data`
  when a caller passes a deliberate default payload through `GenerateErrorResponse<T>(code, message, data)`
  (`0`, `false`, `Guid.Empty`). Plan Decision 6 weighed this against `WhenWritingNull` (which throws for
  value-type `T`) and the overload has zero production call sites today, so the choice is right for now;
  it is a trap to remember if that overload ever gets a caller with a numeric payload.

## Verified

Claims from `02-implementation.md`, each checked against the tree, not the report:

- **The gate.** `ErrorResponseHandler.cs:40-48` — `GenerateDiagnosticData` returns `null` unless
  `environment.IsDevelopment()`; the comparison direction is correct (`!IsDevelopment()` -> `null`), and
  `IsDevelopment()` is case-insensitive over `EnvironmentName`, so every non-`Development` name is gated,
  not just `Production`. `ErrorResponseHandler.cs:33` is the only place `Data` is populated on the
  `ExceptionDetails` overload.
- **The two other overloads are byte-identical to `main`.** `git diff main -- api/core-libraries/Core.Exceptions/ErrorResponseHandler.cs`
  touches only the `using`, the primary-constructor line, the single `Data =` expression and the new
  private method. No hunk reaches `ErrorResponseHandler.cs:8-24`.
- **Wire format.** `ErrorResponse.cs:7` carries `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`.
  `WhenWritingNull` is absent from `Core.Exceptions`. A solution-wide grep finds exactly one
  `DefaultIgnoreCondition` in `api/` — `E3A.Application/Publishing/Shared/PluginJsonSerializer.cs:12`,
  pre-existing and not in the diff. `CoreExceptionMiddleware.ErrorSerializerOptions`
  (`ExceptionMiddleware.cs:16`) is untouched.
- **`ExceptionMiddleware.cs` differs by exactly one deleted line.** The diff against `main` is a single
  `-using Microsoft.Extensions.Hosting;` at line 5 and zero `+` lines. Logging branches
  (`ExceptionMiddleware.cs:54-63`), the serializer options and the body are unchanged.
- **The tests bite.** `ExceptionDetailsFactory.cs:13-17` genuinely throws and catches, so
  `Exception.StackTrace` is populated at runtime. This is self-enforcing: tests 1 and 5 assert
  `Contain(details.Exception!.StackTrace!)`, and FluentAssertions throws on a null or empty
  `Contain` argument — a vacuous factory would make those tests error, not pass. No test uses
  `NotContain` against raw JSON anywhere; both JSON tests parse with `JsonDocument` and assert on
  `TryGetProperty` plus `EnumerateObject().HaveCount(2)`
  (`ErrorResponseHandlerSerializationTests.cs:37-40`), which escaping cannot fool.
- **Build.** Re-ran `dotnet build` from `api/` myself: `Build succeeded. 9 Warning(s), 0 Error(s)`.
  All nine are pre-existing CS8618/CS8602 in `Core.Notifications`, `Core.OTP`, `Core.Validation` —
  none in `Core.Exceptions`, `E3A.Api` or `E3A.Tests`. No new warnings.
- **Tests.** Re-ran `dotnet test` myself: `Failed: 0, Passed: 777, Skipped: 0, Total: 777`. Matches
  the reported count exactly.
- **Mutation claims are consistent with the code as written.** Mutation 1 (gate removed) must fail
  test 2's three `[InlineData]` cases plus test 4 (`data` present, three properties) and leave tests 1,
  3 and 5 passing -> `Failed: 4, Passed: 3`, exactly as reported. Mutation 2 (`[JsonIgnore]` removed)
  changes only the serialized shape in the non-Development case: test 5 still sees `data`, tests 1-3
  are object-level, so only test 4 can fail -> `Failed: 1`, as reported. Both claimed outcomes are the
  outcomes these tests would produce.
- **The declared deviation is sound.** `Program.cs:26-31` does run `AddAzureAppConfiguration(new Uri(endpoint!), ...)`
  under `IsProduction()`, and `appsettings.json` carries an empty `Azure:AACAppSettingsEndpoint`, so the
  reported `UriFormatException` is real and unavoidable without editing an out-of-scope file. Because the
  gate is `IHostEnvironment.IsDevelopment()` and nothing in the path branches on `Production` specifically,
  `Staging` exercises the identical code path — the substitution proves the same property. It was declared,
  not hidden. The Production-boot problem itself is pre-existing and out of scope; recorded above as
  non-blocking.
- **Plan contract.** Exactly three new source files exist, at the three specified paths
  (`git status --porcelain -uall` shows nothing else beyond `.process/stack-trace-leak/`). No production
  file created, no new error code, exception type, interface, options class or DI registration.
- **Scope discipline held.** `git diff main` is empty for `postman/`, `docs/`, `web/`, `dotnet-templates/`,
  `Directory.Packages.props` and `E3a.slnx`. `IErrorResponseHandler.cs` and
  `Core.Exceptions/DependencyInjection.cs` are unmodified — the interface still declares the same three
  signatures, and the registration is still `AddScoped<IErrorResponseHandler, ErrorResponseHandler>()`
  (valid: `IHostEnvironment` is a host singleton, and `Core.Azure/Clients/MIClient.cs:12` and
  `Core.Logging/RequestLoggingMiddleware.cs:11` are existing precedent for injecting it into `Core.*`).
- **Resx.** Both `Messages.en.resx` and `Messages.ar.resx` contain 89 `<data name=` entries. Byte
  inspection of the Arabic value at `Messages.ar.resx:273-275` shows no code point in the tashkeel range
  U+064B-U+0652 and no `{placeholder}`. The key matches `ExceptionErrorCodes.UnhandledException` verbatim,
  and `Localizer.cs:16` returns `localized.Value` once `ResourceNotFound` is false, so the raw exception
  message no longer reaches `message` on a 500.
- **Postman (review order #7).** No endpoint, route, method, request body or documented response field
  changed, so no request is added, stale or orphaned. `postman/e3a.postman_collection.json` contains zero
  `pm.test`/`pm.expect` scripts and zero references to `data`, so nothing in the collection describes the
  error body. Unmodified is correct.
- **Docs (review order #8).** No divergence. Nothing in `/docs` or `README.md` describes the error body's
  shape, its `data` field or stack-trace exposure (grep for `stack trace`/`stacktrace`/`ErrorResponse`/
  `error body`/`error envelope`/`error contract` returns nothing). The one adjacent statement,
  `docs/constitution.md:130` ("`Core.Exceptions` middleware formats responses"), is still true after the
  change. No doc edit was owed.
- **Skill §8/§9.** §8.6 honoured — the changed serialization path was verified as emitted JSON
  (`ErrorResponseHandlerSerializationTests`) and again on the wire, not just as an object. §8.7's closing
  rule ("Never leak `Exception.StackTrace` into a response body outside Development") is precisely what
  this slice implements. No §8 DON'T pattern appears in the diff. File-scoped namespaces, `sealed` test
  classes, no `DateTime`, no `try`/`catch` in a handler, no file over 100 lines. The three-line comment at
  `ErrorResponseHandler.cs:37-39` is the WHY-of-a-hidden-invariant exception the skill's zero-comments rule
  allows, and it is the plan's verbatim text.

## Test quality

- `ErrorResponseHandlerTests` — constrains the implementation. Test 1 reads the expected substring off
  the input object at runtime rather than hard-coding it, so it cannot drift; test 2's third case
  (`"QualityAssurance"`) is what proves the gate is "is it Development", not "is it Production" — delete
  that case and an `IsProduction()` implementation would slip through; test 3 pins the boundary so a
  future over-broad gate that also swallows a caller-supplied payload fails here.
- `ErrorResponseHandlerSerializationTests` — this is the pair that carries the slice. Test 4 is the
  headline invariant and is independently falsifiable in two directions: the `TryGetProperty("data")`
  assertion catches a broken gate, and `HaveCount(2)` catches a missing `[JsonIgnore]` even when the gate
  is right. Test 5 is what makes test 4 non-vacuous — the same code path with a different environment
  produces the exact property test 4 asserts is absent, so test 4 cannot be passing merely because the
  serializer never emits `data`.
- No test in this slice asserts on a message string literal, and none claims to constrain a repository
  query predicate. The `ILocalizer` substitute returns a fixed string that no assertion depends on — it is
  wiring, not a claimed proof, which is the correct use of a substitute here.
- Genuinely not covered, and correctly so: the real resx lookup (`Localizer` resolves from
  `Assembly.GetEntryAssembly()`, the xUnit runner under test) and `CoreExceptionMiddleware` itself
  (integration, out of scope by `conventions/dotnet-testing.md` §5). Both were covered instead by the
  recorded manual wire check, including the 500 / `UNHANDLED_EXCEPTION` leg.
