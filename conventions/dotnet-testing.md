# Testing Convention — companion to `dotnet-feature`

**Stack:** xUnit · NSubstitute · FluentAssertions · .NET 10

> **Overrides the skill.** `dotnet-feature/SKILL.md` §8 says *"No test projects created."*
> Inside this pipeline that line does not apply — the implementer stage MUST produce tests.
> Every other rule in the skill still holds, including in test code
> (file-scoped namespaces, `sealed`, `DateTimeOffset`, `.ConfigureAwait(false)`, `[]` collections, no comments).

---

## 1. Project layout

One test project per module. Mirrors the module's folder tree.

```
tests/
└── BoardManagement.Modules.Tenant.Tests/
    ├── BoardManagement.Modules.Tenant.Tests.csproj
    ├── Domain/
    │   └── Entities/
    │       └── TenantTests.cs
    ├── Application/
    │   ├── Commands/
    │   │   └── SuspendTenant/
    │   │       ├── SuspendTenantHandlerTests.cs
    │   │       └── SuspendTenantValidatorTests.cs
    │   └── Queries/
    │       └── GetTenants/
    │           └── GetTenantsHandlerTests.cs
    └── Shared/
        └── TenantFactory.cs
```

`.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="FluentAssertions" Version="7.*" />
  </ItemGroup>
</Project>
```

> Pin FluentAssertions to `7.*`. Version 8+ changed to a commercial licence.

---

## 2. Naming

| Thing | Convention | Example |
|-------|-----------|---------|
| Test class | `[Sut]Tests`, `sealed class` | `SuspendTenantHandlerTests` |
| Test method | `Method_Should[Outcome]_When[Condition]` | `Handle_ShouldThrowNotFound_WhenTenantDoesNotExist` |
| System under test field | `_sut` | `private readonly SuspendTenantHandler _sut;` |
| Substitute field | `_[dependency]` camelCase | `_tenantRepository`, `_currentUserService` |
| Builder / factory | `[Entity]Factory` static class | `TenantFactory.Active()` |

No `Test_`, `Should_`, or `Given_When_Then_` prefixes. One underscore-separated triple, nothing else.

---

## 3. Shape — constructor wiring, AAA body

```csharp
namespace BoardManagement.Modules.Tenant.Tests.Application.Commands.SuspendTenant;

public sealed class SuspendTenantHandlerTests
{
    private readonly ITenantRepository _tenantRepository = Substitute.For<ITenantRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SuspendTenantHandler _sut;

    public SuspendTenantHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _sut = new SuspendTenantHandler(_tenantRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_ShouldSuspendTenant_WhenTenantIsActive()
    {
        var tenant = TenantFactory.Active();
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);

        var result = await _sut.Handle(new SuspendTenantCommand(tenant.Id), CancellationToken.None);

        result.Status.Should().Be(nameof(TenantStatus.Suspended));
        tenant.Status.Should().Be(TenantStatus.Suspended);
        await _tenantRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

Rules:

- Substitutes are `private readonly` field initialisers. The constructor wires `_sut` and nothing else.
- Body is three unlabelled blocks separated by one blank line — arrange, act, assert. **No `// Arrange` comments.**
- One behaviour per test. Multiple `.Should()` calls are fine when they describe the same behaviour.
- `CancellationToken.None` in tests; `Arg.Any<CancellationToken>()` in substitute setup and verification.
- No `.ConfigureAwait(false)` on awaits **inside test methods** — xUnit needs no sync context and it adds noise. Everywhere else in the test project it still applies.

---

## 4. Test factories — never `new` an entity in a test

Entities have private constructors, so tests go through `Create(...)` exactly like production code does.

```csharp
namespace BoardManagement.Modules.Tenant.Tests.Shared;

public static class TenantFactory
{
    public static Tenant Active(string slug = "acme")
        => Tenant.Create(
            new LocalizedText(arabic: "شركة", english: "Acme"),
            slug,
            Guid.NewGuid(),
            Guid.NewGuid());

    public static Tenant Suspended(string slug = "acme")
    {
        var tenant = Active(slug);
        tenant.Suspend();
        return tenant;
    }
}
```

Reflection to force entity state is PROHIBITED. If a state is unreachable through domain methods, that is a domain design finding — raise it, do not work around it.

---

## 5. Coverage contract — what every feature must have

For each command/query in the plan:

| Layer | Required tests |
|-------|----------------|
| **Domain method** | happy path mutates state · `UpdationDate` advances · each `BusinessRuleViolationException` branch |
| **Validator** | one `[Theory]` per rule covering the invalid inputs · one `[Fact]` proving a valid command passes |
| **Command handler** | happy path · every `throw` branch · `SaveChangesAsync` received exactly once on success and **never** on a throwing path |
| **Query handler** | happy path shape · each filter branch · empty-result case · localized vs admin field mapping when both exist |
| **Result generator** | only when mapping has branching or `.Localized()` resolution |

Explicitly **out of scope**: controllers, EF Core entity configurations, the `Repository<T>` base, MediatR pipeline behaviours, DI registration. Those are integration concerns and this pipeline does not cover them.

---

## 6. Asserting exceptions

```csharp
[Fact]
public async Task Handle_ShouldThrowNotFound_WhenTenantDoesNotExist()
{
    _tenantRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
        .Returns((Tenant?)null);

    var act = async () => await _sut.Handle(new SuspendTenantCommand(Guid.NewGuid()), CancellationToken.None);

    await act.Should().ThrowAsync<NotFoundCoreException>()
        .Where(x => x.ErrorCode == ErrorCodes.TenantNotFound);
    await _tenantRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
}
```

Assert on the **error code constant**, never on a message string. If the core exception exposes the code under a different member name, use that member — but the assertion must bind to `ErrorCodes.X`, not a literal.

---

## 7. Validators

```csharp
public sealed class SuspendTenantValidatorTests
{
    private readonly SuspendTenantValidator _sut = new();

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
        => _sut.Validate(new SuspendTenantCommand(Guid.NewGuid())).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldFail_WhenTenantIdIsEmpty()
    {
        var result = _sut.Validate(new SuspendTenantCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.ErrorCode == ErrorCodes.TenantIdRequired);
    }
}
```

Validators are pure — no substitutes, no async.

---

## 8. Determinism

- Never assert on `DateTimeOffset.UtcNow` equality. Capture `var before = DateTimeOffset.UtcNow;` and assert `.Should().BeOnOrAfter(before)`.
- Never assert on a generated `Guid` value — assert it is not `Guid.Empty`, or capture it from the entity.
- No `Thread.Sleep`, no wall-clock waits, no ordering dependence between tests.
- Any `Guid` a test needs twice is a local `var`, never a re-call to `Guid.NewGuid()`.

---

## 9. Test-code checklist

- [ ] Test project mirrors module folder structure; one test class per production class
- [ ] `sealed class` test classes; `_sut` and substitute naming followed
- [ ] `Method_Should[Outcome]_When[Condition]` naming, no comment labels in the body
- [ ] Entities built via `[Entity]Factory` → `Create(...)`; no reflection, no `new`
- [ ] Every `throw` branch in the handler has a test
- [ ] `SaveChangesAsync` asserted `Received(1)` on success and `DidNotReceive()` on every throwing path
- [ ] Exception tests assert on `ErrorCodes.*` constants, not messages
- [ ] Validator has a passing case plus one failing case per rule
- [ ] No `DateTime`, no wall-clock assertions, no inter-test ordering
- [ ] No file exceeds ~100 lines — split by behaviour group if it does
