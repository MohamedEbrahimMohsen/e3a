# Plan — Creator-Typed Engineer Slug

## Goal

After this ships, a creator types the slug themselves when creating an engineer instead of
receiving one derived from `DisplayName`, and can check `GET /api/engineers/slug-availability?slug=`
beforehand to see whether it is free (and what the system would fall back to). The slug is
validated as kebab-case `^[a-z0-9]+(-[a-z0-9]+)*$`, 3–100 characters, not on a configured
reserved list, and remains editable through `PUT /api/engineers/{id}` only while the engineer
has never been published (`LatestVersionId == null`); after the first publish it is frozen.
The slug is now the whole plugin identity — `e3a-{slug}` — with GitHub login removed from
the plugin name entirely.

## Scope

**In:**
- `CreateEngineerCommand.Slug` (required), typed slug used as the base for the existing
  `IsSlugExistsAsync` + `IGenerator` suffix race guard (skill §8.3 unchanged).
- `UpdateEngineerCommand.Slug` (optional; `null` = leave unchanged), guarded by the freeze rule.
- New query slice `CheckSlugAvailability` + `GET /api/engineers/slug-availability?slug=` (auth required).
- `Engineer.ChangeSlug(...)` + `Engineer.IsSlugMutable`.
- `EngineerSlugGenerator.NormalizeTypedSlug(...)` + `EngineerSlugGenerator.IsValidFormat(...)`.
- `EngineerSlugResolver` — the unique-slug race guard extracted once, used by three call sites.
- `EngineersOptions.SlugMinLength` + `EngineersOptions.ReservedSlugs` + `appsettings.json` values.
- 6 new `ErrorCodes` constants + matching `Messages.ar.resx` / `Messages.en.resx` entries.
- Postman collection: 1 request added, 2 request bodies updated.
- Docs divergence: `docs/plugin-spec.md`, `docs/implementation-plan.md`, `docs/design-prompt.md`.
- Fix: `IGenerator.Generate(prefix, size)` emits a **trailing separator** (see Decisions #9) —
  the resolver trims it, otherwise every collision-resolved slug is format-invalid.

**Out:**
- Frontend / web. The composer is mock pending OAuth; the create form and the live availability
  check land with the OAuth slice.
- Any change to `EngineerSlugGenerator.Normalize(displayName, maxLength)` or its tests — it is
  still used to truncate the prefix before suffixing. Do not delete it.
- Database migration. Column shape (`SlugMaxLength`, unique filtered index) is unchanged and
  no existing row violates the new rules.
- `DisplayName` semantics. It stays independent free text.
- Auditing (`IAuditableCommand`), teams, publish.

**Deferred:**
- Slug-namespace sharing between engineers and teams once `e3a-{slug}` covers both — teams do
  not exist yet (P5). See DEV-DECISION #1.
- Slug history / redirects for renamed drafts. Nothing is published from a draft slug, so a
  rename before first publish has no external consumer.

## Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Slug required or optional on create? | **Required.** No fallback to `DisplayName` derivation. | Proxied dev decision, and the point of the slice is that the creator owns the identity. Agreed — a silent fallback would leave two slug-origin paths to reason about forever. |
| 2 | Is the availability endpoint anonymous? | **Auth required.** Class-level `[Authorize]` on `EngineersController` already covers it; the handler also guards `ICurrentUserService.UserId` with `UnauthorizedCoreException`, mirroring every other engineer handler. | Proxied dev decision. Agreed — draft slugs are not public data and an anonymous endpoint is a free enumeration oracle for unpublished names. |
| 3 | Reject uppercase, or normalize? | **Normalize.** `Trim()` + `ToLowerInvariant()` applied before every check; the stored slug and the result payload are always the normalized form. | Proxied dev decision. Agreed — standard username behaviour. Note the scope: only case and surrounding whitespace are forgiven. `"my slug!"` still fails `ENGINEER_SLUG_INVALID`; we never silently rewrite punctuation into something the creator did not type. |
| 4 | Update semantics for `Slug`? | **Optional; `null` = leave unchanged.** `""` / whitespace is rejected with `ENGINEER_SLUG_REQUIRED`. Sending the current slug verbatim is a no-op (no freeze check, no existence check). | Proxied dev decision. Agreed. The no-op rule is required, otherwise a published engineer could never have its description edited by a client that echoes back the full object. |
| 5 | Where does the freeze guard live? | **Handler**, not the entity: `Engineer` exposes `public bool IsSlugMutable => LatestVersionId == null;`, and `UpdateEngineerHandler` throws `BusinessRuleViolationCoreException(ErrorCodes.EngineerSlugFrozen)`. | Skill §4.8 wants `BusinessRuleViolationException` in the entity, but that type **does not exist** in this repo (only `BusinessRuleViolationCoreException` in `Core.Errors`), and `E3A.Domain` references only `Core.DDD` — it cannot see `E3A.Application.Exceptions.ErrorCodes`. Creating a domain exception type + a domain error-code registry is a new abstraction and is forbidden. `GetImportManifestQueryHandler` already sets the precedent (checks `engineer.DraftManifestJson == null`, throws from the handler). |
| 6 | Does update auto-suffix on collision, or reject? | **Auto-suffix**, identical to create. | Skill §8.3 is unconditional: never throw Conflict for an auto-resolvable collision. Symmetry with create also means one code path to test. |
| 7 | Where does the unique-slug loop live now that three call sites need it? | **New static `EngineerSlugResolver` in `E3A.Application/Engineers/Shared/`.** `CreateEngineerHandler`'s private `GenerateUniqueSlugAsync` is deleted and both handlers plus the availability handler call it. | Skill §1 explicitly sanctions extracting to helpers/generators; the repo already puts shared per-area code in `{Area}/Shared/`. Triplicating a race-guard loop is worse. It cannot live in `E3A.Domain` — it needs `Core.Utilities.IGenerator` and `EngineersOptions`, neither visible from the domain project. |
| 8 | Does the availability response suggest an alternative? | **Yes** — `SuggestedSlug` is the slug the create handler would actually assign, `null` when the requested slug is free. | The dev's own framing was "system find or suggest another slug". It is one call to the resolver already on the stack, has no side effects, and makes the endpoint answer the question the composer will ask next. |
| 9 | `IGenerator.Generate(prefix: p, size: n)` returns `"{p}-{nanoid}-"` — a **trailing hyphen** (`suffix` defaults to `""` and the separator is emitted unconditionally). | **Trim it in the resolver**: `.TrimEnd('-')`. Do not modify `api/core-libraries/`. | Pre-existing latent bug, invisible today because the slug had no format contract and the existing test mocks `IGenerator`. This slice makes the slug the plugin name and enforces the regex, so a collision-resolved slug would be format-invalid on the wire. Core is vendored/shared; fixing it there is out of this slice's blast radius. |
| 10 | Where does the reserved-slug check fire for the availability endpoint? | **In the validator (422)**, identically to create/update — *not* as `IsAvailable = false`. | One rule set, one meaning: whatever the availability endpoint accepts, create accepts. `IsAvailable` then means exactly "not already taken". |
| 11 | Use `ValidateMinLength`/`ValidateMaxLength` Core extensions for the slug? | **No** — use `RuleFor(x => x.Slug).Must(predicate).WithMessage(...).WithErrorCode(...)`, with each predicate calling `EngineerSlugGenerator.NormalizeTypedSlug` first. | The Core extensions bind to the raw property, but every slug rule must run against the *normalized* value (Decision #3). `.Must(...).WithErrorCode(...)` is already the established repo pattern — see the `ENGINEER_DISPLAY_NAME_INVALID` rule in both existing engineer validators. `ValidateRequired` still fits the raw value and IS used. |
| 12 | Parameter position of `Slug` in the commands/requests? | `CreateEngineerCommand(Slug, DisplayName, Description, Tags)`; `UpdateEngineerCommand(EngineerId, Slug, DisplayName, Description, Tags)`. Request records mirror exactly. | Mirrors `Engineer.Create(ownerUserId, slug, displayName, ...)`, which already puts slug before displayName. |
| 13 | Do the new error codes need a `DefaultCodes` policy? | **No.** `DefaultCodes` does not exist in this repo; `EngineersController` uses a bare class-level `[Authorize]` and no per-action policies. The new action adds none. | Mirror, don't modernize. Introducing a policy registry is a separate slice. |
| 14 | `docs/design-prompt.md:16` is not on the acceptance doc list. | **Update it anyway.** | It prints `/plugin install e3a-mohamed-dive-backend-engineer@e3a` — a naming-contract example that this change invalidates. `.claude/rules/docs-sync.md` classes naming/format-contract changes as blocking divergence regardless of which doc holds them. |
| 15 | Test files that would exceed ~100 lines. | Split by behaviour group into sibling files rather than growing the existing ones. | `conventions/dotnet-testing.md` §9. The repo already does this (`UploadEngineerDraftHandlerGuardTests` / `UploadEngineerDraftHandlerTests`, `DraftNormalizerConversionTests` / `DraftNormalizerTests`). |

### DEV-DECISION (record for the dev's return; not blocking this slice)

1. **Engineer/team slug collision.** `e3a-{slug}` drops the disambiguating login segment, so once teams
   ship, an engineer slug and a team slug can produce the same plugin name from two different tables.
   Options: one shared slug table, a prefix (`e3a-t-{slug}`), or a cross-table uniqueness check at
   create time. Teams are P5 and no table exists yet — no code is written for this now.
2. **Reserved list contains `z` and `m`** (URL path prefixes), both 1 character, which `SlugMinLength = 3`
   already makes unreachable. Kept verbatim as locked, in case `SlugMinLength` is ever lowered.

## Existing code touched

| File | Change |
|------|--------|
| `api/E3A.Domain/Engineers/Engineer.cs` | Add `public bool IsSlugMutable => LatestVersionId == null;` (place directly under the `InstallCount` property). Add `ChangeSlug(string slug)` domain method after `UpdateMetadata`. |
| `api/E3A.Domain/Engineers/EngineerSlugGenerator.cs` | Add `NormalizeTypedSlug(string? slug)`, `IsValidFormat(string slug)`, and the private compiled `SlugFormatRegex`. Leave `Normalize(displayName, maxLength)` untouched. |
| `api/E3A.Application/Options/EngineersOptions.cs` | Add `public int SlugMinLength { get; set; }` (after `SlugSuffixSize`) and `public List<string> ReservedSlugs { get; set; } = [];` (last property). |
| `api/E3A.Api/appsettings.json` | In the `Engineers` section add `"SlugMinLength": 3` and `"ReservedSlugs": [ "e3a", "api", "admin", "www", "docs", "health", "install", "marketplace", "catalog", "teams", "new", "edit", "settings", "z", "m" ]`. |
| `api/E3A.Application/Exceptions/ErrorCodes.cs` | Add the 6 constants below to the end of the `// Engineers` group, after `EngineerDraftNotUploaded`. |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerCommand.cs` | `public sealed record CreateEngineerCommand(string Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<EngineerResult>;` |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerValidator.cs` | Add the 4 slug rules (exact block below), placed above the existing `DisplayName` rules. |
| `api/E3A.Application/Engineers/CreateEngineer/CreateEngineerHandler.cs` | Delete the private `GenerateUniqueSlugAsync`. Replace line 33 with the resolver call on the typed slug. Constructor unchanged. |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerCommand.cs` | `public sealed record UpdateEngineerCommand(Guid EngineerId, string? Slug, string DisplayName, string? Description, List<string> Tags) : IRequest<EngineerResult>;` |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerValidator.cs` | Add the 4 slug rules, each additionally gated `.When(x => x.Slug != null)`, placed above the existing `DisplayName` rules. |
| `api/E3A.Application/Engineers/UpdateEngineer/UpdateEngineerHandler.cs` | Constructor gains `IGenerator generator, IOptions<EngineersOptions> engineersOptions`. Add the slug freeze guard + resolution before any mutation, then `ChangeSlug`. |
| `api/E3A.Api/Controllers/Engineers/Requests.cs` | `CreateEngineerRequest(string Slug, string DisplayName, string? Description, List<string>? Tags)`; `UpdateEngineerRequest(string? Slug, string DisplayName, string? Description, List<string>? Tags)`. |
| `api/E3A.Api/Controllers/Engineers/EngineersController.cs` | Add the `slug-availability` action (after `ListMyEngineers`). Pass `request.Slug` through in `CreateEngineer` and `UpdateEngineer`. |
| `api/E3A.Api/Resources/Messages.en.resx` | 6 new `<data>` entries after `ENGINEER_DRAFT_NOT_UPLOADED`. |
| `api/E3A.Api/Resources/Messages.ar.resx` | The same 6 keys, same order, Arabic values, no tashkeel. |
| `postman/e3a.postman_collection.json` | Add `Check Slug Availability` to the `Engineers` folder (position 2, after `List My Engineers`); add `"slug": "dive-backend-engineer"` as the first field of the `Create Engineer` and `Update Engineer` raw JSON bodies. |
| `docs/plugin-spec.md` | Lines 11, 87, 94 — see Docs section. |
| `docs/implementation-plan.md` | Lines 34, 44, 55 — see Docs section. |
| `docs/design-prompt.md` | Line 16 — see Docs section. |
| `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` | `CreateEngineersOptions` gains `SlugMinLength = 3` and the full `ReservedSlugs` list. Add `public const string DefaultReservedSlug = "admin";`. |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerHandlerTests.cs` | Update all command constructions for the new signature; rename/retarget 2 tests; add 1; **delete** `Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken` (moved to `EngineerSlugResolverTests`). |
| `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerValidatorTests.cs` | Update all 8 command constructions to pass a valid slug (`EngineerFactory.DefaultSlug`) as the first argument. No test added or removed. |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerHandlerTests.cs` | Constructor wires the 2 new substitutes; update all 4 command constructions with `null` as the `Slug` argument. |
| `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerValidatorTests.cs` | Update all 9 command constructions with `null` as the `Slug` argument. No test added or removed. |

## Files to create

### 1. `api/E3A.Application/Engineers/Shared/EngineerSlugResolver.cs`

Namespace `E3A.Application.Engineers.Shared`. `public static class EngineerSlugResolver`.

```csharp
public static async Task<string> ResolveUniqueAsync(string baseSlug, IEngineerRepository engineerRepository, IGenerator generator, EngineersOptions options, CancellationToken cancellationToken)
```

Ordered steps:
1. `if (!await engineerRepository.IsSlugExistsAsync(baseSlug, cancellationToken).ConfigureAwait(false)) { return baseSlug; }`
2. `var prefix = EngineerSlugGenerator.Normalize(baseSlug, options.SlugMaxLength - options.SlugSuffixSize - 1);`
3. `do { candidateSlug = generator.Generate(prefix: prefix, size: options.SlugSuffixSize).TrimEnd('-'); } while (await engineerRepository.IsSlugExistsAsync(candidateSlug, cancellationToken).ConfigureAwait(false));`
4. `return candidateSlug;`

Two WHY comments are permitted and expected here (they are hidden invariants):
- above step 2: `// Re-normalize shorter so "{prefix}-{suffix}" can never exceed SlugMaxLength.` (carried over verbatim from the deleted private method)
- above the `generator.Generate` call: `// Core IGenerator always emits the separator before the empty suffix, leaving a trailing hyphen.`

### 2. `api/E3A.Application/Engineers/Shared/SlugAvailabilityResult.cs`

```csharp
namespace E3A.Application.Engineers.Shared;

public sealed record SlugAvailabilityResult(string Slug, bool IsAvailable, string? SuggestedSlug);
```

`Slug` is the **normalized** requested slug (so the composer can show what will actually be stored).
`SuggestedSlug` is `null` when `IsAvailable` is `true`. All three fields are client-facing; no
`LocalizedText` is involved anywhere in this slice, so no `.Localized()` calls.

### 3. `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQuery.cs`

```csharp
namespace E3A.Application.Engineers.CheckSlugAvailability;

public sealed record CheckSlugAvailabilityQuery(string Slug) : IRequest<SlugAvailabilityResult>;
```

### 4. `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryValidator.cs`

`public sealed class CheckSlugAvailabilityQueryValidator : AbstractValidator<CheckSlugAvailabilityQuery>`,
constructor `(IOptions<EngineersOptions> engineersOptions)`. Body is the **canonical slug rule block**
(below) applied to `x => x.Slug`, with no `.When(x => x.Slug != null)` outer gate.

### 5. `api/E3A.Application/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandler.cs`

```csharp
public sealed class CheckSlugAvailabilityQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<EngineersOptions> engineersOptions) : IRequestHandler<CheckSlugAvailabilityQuery, SlugAvailabilityResult>
```

Ordered steps in `Handle`:
1. `var userId = currentUserService.UserId;` → `if (userId == null || userId == Guid.Empty) { throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated); }`
2. `var slug = EngineerSlugGenerator.NormalizeTypedSlug(request.Slug);`
3. `if (!await engineerRepository.IsSlugExistsAsync(slug, cancellationToken).ConfigureAwait(false)) { return new SlugAvailabilityResult(slug, true, null); }`
4. `var suggestedSlug = await EngineerSlugResolver.ResolveUniqueAsync(slug, engineerRepository, generator, engineersOptions.Value, cancellationToken).ConfigureAwait(false);`
5. `return new SlugAvailabilityResult(slug, false, suggestedSlug);`

No `SaveChangesAsync`. No `try`/`catch`.

### 6–11. Test files — see Test plan.

| # | Path |
|---|------|
| 6 | `api/E3A.Tests/Engineers/EngineerSlugTests.cs` |
| 7 | `api/E3A.Tests/Engineers/EngineerSlugGeneratorTypedInputTests.cs` |
| 8 | `api/E3A.Tests/Engineers/Shared/EngineerSlugResolverTests.cs` |
| 9 | `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerSlugValidatorTests.cs` |
| 10 | `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerSlugValidatorTests.cs` |
| 11 | `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerSlugHandlerTests.cs` |
| 12 | `api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryValidatorTests.cs` |
| 13 | `api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandlerTests.cs` |

**Create exactly these files and no others.**

## Canonical slug rule block

Used verbatim in `CreateEngineerValidator` and `CheckSlugAvailabilityQueryValidator`. In
`UpdateEngineerValidator` every rule below additionally carries `.When(x => x.Slug != null)`
(chained after the existing `.When(...)` where one is already present — i.e. the format and
reserved rules become `.When(x => x.Slug != null && !string.IsNullOrWhiteSpace(x.Slug))`).

`var options = engineersOptions.Value;` is already the first line of both existing validators.

```csharp
RuleFor(x => x.Slug)
    .ValidateRequired(ErrorCodes.EngineerSlugRequired);

RuleFor(x => x.Slug)
    .Must(slug => EngineerSlugGenerator.NormalizeTypedSlug(slug).Length >= options.SlugMinLength)
    .WithMessage($"{{PropertyName}} must be at least {options.SlugMinLength} characters.")
    .WithErrorCode(ErrorCodes.EngineerSlugTooShort)
    .When(x => !string.IsNullOrWhiteSpace(x.Slug));

RuleFor(x => x.Slug)
    .Must(slug => EngineerSlugGenerator.NormalizeTypedSlug(slug).Length <= options.SlugMaxLength)
    .WithMessage($"{{PropertyName}} must not exceed {options.SlugMaxLength} characters.")
    .WithErrorCode(ErrorCodes.EngineerSlugTooLong)
    .When(x => !string.IsNullOrWhiteSpace(x.Slug));

RuleFor(x => x.Slug)
    .Must(slug => EngineerSlugGenerator.IsValidFormat(EngineerSlugGenerator.NormalizeTypedSlug(slug)))
    .WithMessage("{PropertyName} must be lowercase letters, digits and single hyphens.")
    .WithErrorCode(ErrorCodes.EngineerSlugInvalid)
    .When(x => !string.IsNullOrWhiteSpace(x.Slug));

RuleFor(x => x.Slug)
    .Must(slug => !options.ReservedSlugs.Contains(EngineerSlugGenerator.NormalizeTypedSlug(slug), StringComparer.OrdinalIgnoreCase))
    .WithMessage("{PropertyName} is reserved.")
    .WithErrorCode(ErrorCodes.EngineerSlugReserved)
    .When(x => !string.IsNullOrWhiteSpace(x.Slug));
```

Rule-to-error-code map:

| Rule | Extension / predicate | Error code |
|------|----------------------|------------|
| present and non-blank | `ValidateRequired` (Core.Validation) | `EngineerSlugRequired` |
| normalized length ≥ `options.SlugMinLength` | `.Must` | `EngineerSlugTooShort` |
| normalized length ≤ `options.SlugMaxLength` | `.Must` | `EngineerSlugTooLong` |
| normalized matches `^[a-z0-9]+(-[a-z0-9]+)*$` | `.Must` + `EngineerSlugGenerator.IsValidFormat` | `EngineerSlugInvalid` |
| normalized not in `options.ReservedSlugs` | `.Must` (`StringComparer.OrdinalIgnoreCase`) | `EngineerSlugReserved` |

## Error codes

Added to the `// Engineers` group of `api/E3A.Application/Exceptions/ErrorCodes.cs`, in this order,
after `EngineerDraftNotUploaded`:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `EngineerSlugRequired` | `ENGINEER_SLUG_REQUIRED` | `CreateEngineerValidator`, `UpdateEngineerValidator`, `CheckSlugAvailabilityQueryValidator` | `ApplicationValidationCoreException` (pipeline) | 422 |
| `EngineerSlugTooShort` | `ENGINEER_SLUG_TOO_SHORT` | same three validators | `ApplicationValidationCoreException` (pipeline) | 422 |
| `EngineerSlugTooLong` | `ENGINEER_SLUG_TOO_LONG` | same three validators | `ApplicationValidationCoreException` (pipeline) | 422 |
| `EngineerSlugInvalid` | `ENGINEER_SLUG_INVALID` | same three validators | `ApplicationValidationCoreException` (pipeline) | 422 |
| `EngineerSlugReserved` | `ENGINEER_SLUG_RESERVED` | same three validators | `ApplicationValidationCoreException` (pipeline) | 422 |
| `EngineerSlugFrozen` | `ENGINEER_SLUG_FROZEN` | `UpdateEngineerHandler` | `BusinessRuleViolationCoreException` | 400 |

Resource strings — add all six to **both** files, same order, immediately after the
`ENGINEER_DRAFT_NOT_UPLOADED` entry. Numerals are hardcoded, matching the existing
`ENGINEER_DISPLAY_NAME_TOO_LONG` / `ENGINEER_TAG_TOO_LONG` precedent. Arabic without tashkeel.

| Key | `Messages.en.resx` | `Messages.ar.resx` |
|-----|--------------------|--------------------|
| `ENGINEER_SLUG_REQUIRED` | `A slug is required.` | `الاسم المختصر مطلوب.` |
| `ENGINEER_SLUG_TOO_SHORT` | `A slug must be at least 3 characters.` | `يجب ان يكون الاسم المختصر 3 احرف على الاقل.` |
| `ENGINEER_SLUG_TOO_LONG` | `A slug must not exceed 100 characters.` | `يجب الا يتجاوز الاسم المختصر 100 حرف.` |
| `ENGINEER_SLUG_INVALID` | `A slug must use lowercase English letters, digits and single hyphens.` | `يجب ان يتكون الاسم المختصر من حروف انجليزية صغيرة وارقام وشرطات مفردة.` |
| `ENGINEER_SLUG_RESERVED` | `That slug is reserved. Please choose another one.` | `هذا الاسم المختصر محجوز. من فضلك اختر اسما اخر.` |
| `ENGINEER_SLUG_FROZEN` | `A slug cannot be changed after the engineer has been published.` | `لا يمكن تغيير الاسم المختصر بعد نشر المهندس.` |

## Domain behaviour

### `Engineer` (`api/E3A.Domain/Engineers/Engineer.cs`)

Add the computed member directly beneath `public int InstallCount { get; private set; }`:

```csharp
public bool IsSlugMutable => LatestVersionId == null;
```

Add the domain method directly after `UpdateMetadata`:

```csharp
public void ChangeSlug(string slug)
{
    Slug = slug;
    UpdationDate = DateTimeOffset.UtcNow;
}
```

No guard inside `ChangeSlug` — see Decision #5. `Slug` stays `private set`; the handler cannot
mutate it directly. `UpdationDate` is stamped. No `BusinessRuleViolationException` is added
anywhere in the domain project (the type does not exist in this solution).

### `EngineerSlugGenerator` (`api/E3A.Domain/Engineers/EngineerSlugGenerator.cs`)

`Normalize(string displayName, int maxLength)` is unchanged. Add, above `Normalize`:

```csharp
private static readonly Regex SlugFormatRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
```

Add, below `Normalize`:

```csharp
public static string NormalizeTypedSlug(string? slug)
{
    return slug?.Trim().ToLowerInvariant() ?? string.Empty;
}

public static bool IsValidFormat(string slug)
{
    return SlugFormatRegex.IsMatch(slug);
}
```

`IsValidFormat("")` returns `false` (the regex requires at least one character); the validators
never reach it on a blank slug because `ENGINEER_SLUG_REQUIRED` fires first and the format rule
is gated on `!string.IsNullOrWhiteSpace`. Requires `using System.Text.RegularExpressions;`.

### `CreateEngineerHandler` — ordered steps in `Handle`

1. `var userId = currentUserService.UserId;` → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)` when null/empty. **(unchanged)**
2. `var ownerUserId = userId.Value;` / `var options = engineersOptions.Value;` / `CountAsync` limit check → `BusinessRuleViolationCoreException(ErrorCodes.EngineerLimitReached)` with the `["limit"]` context. **(unchanged)**
3. `var slug = await EngineerSlugResolver.ResolveUniqueAsync(EngineerSlugGenerator.NormalizeTypedSlug(request.Slug), engineerRepository, generator, options, cancellationToken).ConfigureAwait(false);` **(replaces the `GenerateUniqueSlugAsync(request.DisplayName, ...)` call)**
4. `var engineer = Engineer.Create(ownerUserId, slug, request.DisplayName, request.Description, request.Tags);` **(unchanged)**
5. `AddAsync` → `SaveChangesAsync` (once) → `return EngineerResultGenerator.Generate(engineer);` **(unchanged)**

Delete the private `GenerateUniqueSlugAsync` method entirely. The constructor signature is unchanged.

### `UpdateEngineerHandler` — ordered steps in `Handle`

Constructor becomes:

```csharp
public sealed class UpdateEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<EngineersOptions> engineersOptions) : IRequestHandler<UpdateEngineerCommand, EngineerResult>
```

1. `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)` when `UserId` null/empty. **(unchanged)**
2. `GetByIdAsync(request.EngineerId, ...)` → `NotFoundCoreException(ErrorCodes.EngineerNotFound)` when null. **(unchanged)**
3. `engineer.OwnerUserId != ownerUserId` → `ForbiddenCoreException(ErrorCodes.EngineerNotOwned)`. **(unchanged)**
4. `var resolvedSlug = await ResolveSlugChangeAsync(request, engineer, cancellationToken).ConfigureAwait(false);` — a private method returning `string?` (`null` = no slug change). All throws happen here, **before any mutation**.
5. `engineer.UpdateMetadata(request.DisplayName, request.Description, request.Tags);`
6. `if (resolvedSlug != null) { engineer.ChangeSlug(resolvedSlug); }`
7. `engineerRepository.Update(engineer);` → `await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);` (once)
8. `return EngineerResultGenerator.Generate(engineer);`

Private method:

```csharp
private async Task<string?> ResolveSlugChangeAsync(UpdateEngineerCommand request, Engineer engineer, CancellationToken cancellationToken)
```

1. `if (request.Slug == null) { return null; }`
2. `var requestedSlug = EngineerSlugGenerator.NormalizeTypedSlug(request.Slug);`
3. `if (requestedSlug == engineer.Slug) { return null; }`
4. `if (!engineer.IsSlugMutable) { throw new BusinessRuleViolationCoreException(ErrorCodes.EngineerSlugFrozen); }`
5. `return await EngineerSlugResolver.ResolveUniqueAsync(requestedSlug, engineerRepository, generator, engineersOptions.Value, cancellationToken).ConfigureAwait(false);`

## API surface

| Method | Route | Auth | Request | Response |
|--------|-------|------|---------|----------|
| GET | `/api/engineers/slug-availability?slug=` | class-level `[Authorize]` (no per-action policy; `DefaultCodes` does not exist in this repo) | `[FromQuery] string slug` | `200 OK` `SlugAvailabilityResult` |
| POST | `/api/engineers` | class-level `[Authorize]` (unchanged) | `CreateEngineerRequest(Slug, DisplayName, Description, Tags)` | `201 Created` `EngineerResult` (unchanged) |
| PUT | `/api/engineers/{engineerId:guid}` | class-level `[Authorize]` (unchanged) | `UpdateEngineerRequest(Slug, DisplayName, Description, Tags)` | `200 OK` `EngineerResult` (unchanged) |

New action, placed immediately after `ListMyEngineers` (before the `{engineerId:guid}` route; the
`:guid` constraint means there is no route ambiguity):

```csharp
[HttpGet("slug-availability")]
public async Task<ActionResult> CheckSlugAvailability([FromQuery] string slug, CancellationToken cancellationToken)
{
    var result = await mediator.Send(new CheckSlugAvailabilityQuery(slug), cancellationToken);
    return Ok(result);
}
```

Modified mappings:

```csharp
var result = await mediator.Send(new CreateEngineerCommand(request.Slug, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
var result = await mediator.Send(new UpdateEngineerCommand(engineerId, request.Slug, request.DisplayName, request.Description, request.Tags ?? []), cancellationToken);
```

## Postman

`postman/e3a.postman_collection.json`, `Engineers` folder:

1. **Create Engineer** — raw body becomes
   `{\n  "slug": "dive-backend-engineer",\n  "displayName": "Dive Backend Engineer",\n  "description": "...",\n  "tags": ["dotnet", "cqrs", "api"]\n}` (keep the existing description text and tags).
2. **Update Engineer** — raw body becomes
   `{\n  "slug": "dive-backend-engineer",\n  "displayName": "Dive Backend Engineer",\n  "description": "Updated description.",\n  "tags": ["dotnet", "ddd"]\n}`.
3. **New request `Check Slug Availability`**, inserted at index 1 (right after `List My Engineers`):
   method `GET`, empty `header` array, no `auth` override (inherits collection auth),
   `url.raw` = `{{baseUrl}}/api/engineers/slug-availability?slug=dive-backend-engineer`,
   `url.host` = `["{{baseUrl}}"]`, `url.path` = `["api","engineers","slug-availability"]`,
   `url.query` = `[{ "key": "slug", "value": "dive-backend-engineer" }]`.

Preserve the file's existing JSON formatting and key ordering conventions.

## Docs

`.claude/rules/docs-sync.md`: these are **divergence**, not incompleteness — the naming contract,
the data-model description of how a slug is produced, and the API surface all change in this commit.

| File | Line | Replace with |
|------|------|--------------|
| `docs/plugin-spec.md` | 11 | ``Plugin name: `e3a-{slug}` — the creator types the slug when creating the engineer; it is globally unique, editable only while the item has never been published, and permanently frozen afterwards. GitHub login is no longer part of the plugin name; attribution lives in the `author` field.`` |
| `docs/plugin-spec.md` | 87 | `  "name": "e3a-mmohsen",` |
| `docs/plugin-spec.md` | 94 | `    "url": "https://<domain>/z/e3a-mmohsen/3.0.0.zip",` |
| `docs/implementation-plan.md` | 34 | Replace only the `Slug (...)` parenthetical with: ``Slug (unique, creator-typed kebab-case `^[a-z0-9]+(-[a-z0-9]+)*$`, 3–`SlugMaxLength` characters, reserved words rejected from a config list, auto-suffixed via IGenerator only as a collision race guard; editable while `LatestVersionId` is null and frozen after the first publish; the slug is the entire plugin name `e3a-{slug}` — GitHub login is not part of the plugin identity)``. Leave the rest of the bullet, including the `[Area]Options` sentence, untouched. |
| `docs/implementation-plan.md` | 44 | ``- **Naming**: `e3a-{slug}` — the creator-typed slug is the plugin name; uniqueness enforced by the DB index, attribution via `author` (GitHub login).`` |
| `docs/implementation-plan.md` | 55 | In the `Engineers:` sentence, add `GET /api/engineers/slug-availability?slug=` to the `[auth/owner]` list alongside `GET /api/engineers/mine`. |
| `docs/design-prompt.md` | 16 | Change `/plugin install e3a-mohamed-dive-backend-engineer@e3a` to `/plugin install e3a-mmohsen@e3a`. |

Do **not** change `docs/plugin-spec.md` line 90 — the `author` block staying `@mohamed-dive` while
the plugin name is `e3a-mmohsen` is exactly the point: slug and GitHub login are now independent.

## Test plan

Follows `conventions/dotnet-testing.md` §5. The implementer writes exactly these tests. Substitutes
are `private readonly` field initialisers; the constructor wires `_sut` only; no `// Arrange` comments;
`CancellationToken.None` in test bodies and `Arg.Any<CancellationToken>()` in setup/verification;
entities only via `EngineerFactory`.

### `api/E3A.Tests/Engineers/EngineerSlugTests.cs` (new)

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `EngineerSlugTests` | `ChangeSlug_ShouldReplaceSlugAndStampUpdationDate_WhenCalled` | `EngineerFactory.Draft(...)`, capture `before`; `ChangeSlug("mmohsen")`; `Slug == "mmohsen"`, `UpdationDate.Should().BeOnOrAfter(before)` |
| 2 | `EngineerSlugTests` | `IsSlugMutable_ShouldBeTrue_WhenEngineerHasNoLatestVersion` | `EngineerFactory.Draft(...)` → `IsSlugMutable.Should().BeTrue()` |
| 3 | `EngineerSlugTests` | `IsSlugMutable_ShouldBeFalse_WhenEngineerIsPublished` | `EngineerFactory.Published(...)` → `IsSlugMutable.Should().BeFalse()` |

### `api/E3A.Tests/Engineers/EngineerSlugGeneratorTypedInputTests.cs` (new)

| # | Test method | Asserts |
|---|-------------|---------|
| 4 | `NormalizeTypedSlug_ShouldTrimAndLowercase_WhenInputHasCaseOrWhitespace` | `[Theory]` rows `("  MMohsen ", "mmohsen")`, `("DIVE-Backend", "dive-backend")`, `("mmohsen", "mmohsen")` |
| 5 | `NormalizeTypedSlug_ShouldReturnEmpty_WhenInputIsNull` | `NormalizeTypedSlug(null).Should().BeEmpty()` |
| 6 | `IsValidFormat_ShouldReturnTrue_WhenSlugIsKebabCase` | `[Theory]` `"abc"`, `"a1"`, `"dive-backend-engineer"`, `"a-1-b"` → `BeTrue()` |
| 7 | `IsValidFormat_ShouldReturnFalse_WhenSlugIsNotKebabCase` | `[Theory]` `""`, `"-abc"`, `"abc-"`, `"a--b"`, `"Abc"`, `"a_b"`, `"a b"`, `"abc!"` → `BeFalse()` |

### `api/E3A.Tests/Engineers/Shared/EngineerSlugResolverTests.cs` (new)

Substitutes: `_engineerRepository`, `_generator`. Options via `EngineerFactory.CreateEngineersOptions()`.
Static SUT — call `EngineerSlugResolver.ResolveUniqueAsync(...)` directly.

| # | Test method | Asserts |
|---|-------------|---------|
| 8 | `ResolveUniqueAsync_ShouldReturnBaseSlug_WhenBaseSlugIsFree` | returns `"mmohsen"`; `_generator.DidNotReceive().Generate(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())` |
| 9 | `ResolveUniqueAsync_ShouldStripTrailingSeparator_WhenGeneratorAppendsOne` | base taken; generator returns `"mmohsen-ab12-"`; result is `"mmohsen-ab12"` (no trailing hyphen), and `EngineerSlugGenerator.IsValidFormat(result).Should().BeTrue()` |
| 10 | `ResolveUniqueAsync_ShouldRetry_WhenFirstCandidateIsAlsoTaken` | generator returns `"mmohsen-ab12-"` then `"mmohsen-cd34-"`; first candidate `IsSlugExistsAsync` true, second false; result `"mmohsen-cd34"`; `_generator.Received(2).Generate(...)` |
| 11 | `ResolveUniqueAsync_ShouldShortenPrefix_WhenBaseSlugIsAtMaxLength` | base = 100 `'a'` chars, taken; `_generator.Received(1).Generate(Arg.Is<string>(prefix => prefix.Length == 95), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())` (`SlugMaxLength 100 - SlugSuffixSize 4 - 1`) |

### `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerHandlerTests.cs` (modified)

| # | Test method | Asserts |
|---|-------------|---------|
| 12 | `Handle_ShouldCreateEngineerWithTypedSlug_WhenSlugIsFree` *(renamed from `...WithBaseSlug_WhenSlugIsFreeAndUnderLimit`)* | command slug `"mmohsen"`, `IsSlugExistsAsync("mmohsen")` false; `result.Slug == "mmohsen"`, `result.Status == nameof(EngineerStatus.Draft)`, `result.InstallCount == 0`; `AddAsync` `Received(1)`; `SaveChangesAsync` `Received(1)` |
| 13 | `Handle_ShouldNormalizeTypedSlug_WhenSlugHasUppercaseAndWhitespace` *(new)* | command slug `"  MMohsen  "`; `IsSlugExistsAsync("mmohsen")` false; `result.Slug == "mmohsen"`; `AddAsync(Arg.Is<Engineer>(x => x.Slug == "mmohsen"), ...)` `Received(1)` |
| 14 | `Handle_ShouldCreateEngineerWithSuffixedSlug_WhenTypedSlugIsTaken` *(renamed from `...WhenBaseSlugIsAlreadyTaken`)* | `IsSlugExistsAsync("mmohsen")` true, `"mmohsen-ab12"` false; generator returns `"mmohsen-ab12-"`; `result.Slug == "mmohsen-ab12"`; `AddAsync(Arg.Is<Engineer>(x => x.Slug == "mmohsen-ab12"), ...)` `Received(1)`; `SaveChangesAsync` `Received(1)` |
| — | `Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken` | **DELETE** — superseded by test #10 |
| 15 | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` *(existing, signature update only)* | `UnauthorizedCoreException` with `ErrorCode == ErrorCodes.UserNotAuthenticated`; `SaveChangesAsync` `DidNotReceive()` |
| 16 | `Handle_ShouldThrowBusinessRuleViolation_WhenCreatorReachedTheLimit` *(existing, signature update only)* | `BusinessRuleViolationCoreException` with `ErrorCode == ErrorCodes.EngineerLimitReached`; `AddAsync` and `SaveChangesAsync` `DidNotReceive()` |

### `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerSlugValidatorTests.cs` (new)

SUT: `new CreateEngineerValidator(Options.Create(EngineerFactory.CreateEngineersOptions()))`.

| # | Test method | Asserts |
|---|-------------|---------|
| 17 | `Validate_ShouldFail_WhenSlugIsMissing` | `[Theory]` `null`, `""`, `"   "` → `IsValid` false, error code `EngineerSlugRequired` |
| 18 | `Validate_ShouldFail_WhenSlugIsShorterThanMinimum` | `"ab"` → `EngineerSlugTooShort` |
| 19 | `Validate_ShouldFail_WhenSlugExceedsMaxLength` | `new string('a', 101)` → `EngineerSlugTooLong` |
| 20 | `Validate_ShouldFail_WhenSlugIsNotKebabCase` | `[Theory]` `"-mmohsen"`, `"mmohsen-"`, `"m--mohsen"`, `"m_mohsen"`, `"m mohsen"`, `"mmohsen!"` → `EngineerSlugInvalid` |
| 21 | `Validate_ShouldFail_WhenSlugIsReserved` | `[Theory]` `"admin"`, `"API"`, `"Marketplace"` → `EngineerSlugReserved` |
| 22 | `Validate_ShouldPass_WhenSlugDiffersOnlyByCaseOrWhitespace` | `"  MMohsen  "` → `IsValid` true |

### `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerSlugValidatorTests.cs` (new)

SUT: `new UpdateEngineerValidator(Options.Create(EngineerFactory.CreateEngineersOptions()))`.

| # | Test method | Asserts |
|---|-------------|---------|
| 23 | `Validate_ShouldPass_WhenSlugIsNull` | `Slug = null` with otherwise valid command → `IsValid` true |
| 24 | `Validate_ShouldFail_WhenSlugIsBlank` | `[Theory]` `""`, `"   "` → `EngineerSlugRequired` |
| 25 | `Validate_ShouldFail_WhenSlugIsShorterThanMinimum` | `"ab"` → `EngineerSlugTooShort` |
| 26 | `Validate_ShouldFail_WhenSlugExceedsMaxLength` | `new string('a', 101)` → `EngineerSlugTooLong` |
| 27 | `Validate_ShouldFail_WhenSlugIsNotKebabCase` | `[Theory]` `"-mmohsen"`, `"mmohsen-"`, `"m--mohsen"`, `"m mohsen"` → `EngineerSlugInvalid` |
| 28 | `Validate_ShouldFail_WhenSlugIsReserved` | `"admin"` → `EngineerSlugReserved` |
| 29 | `Validate_ShouldPass_WhenSlugDiffersOnlyByCaseOrWhitespace` | `"  MMohsen  "` → `IsValid` true |

### `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerSlugHandlerTests.cs` (new)

Substitutes: `_engineerRepository`, `_currentUserService`, `_generator`; `_ownerUserId` field;
`_sut = new UpdateEngineerHandler(_engineerRepository, _currentUserService, _generator, Options.Create(EngineerFactory.CreateEngineersOptions()))`.

| # | Test method | Asserts |
|---|-------------|---------|
| 30 | `Handle_ShouldChangeSlug_WhenEngineerIsDraftAndSlugIsFree` | draft engineer; command slug `"mmohsen"`; `IsSlugExistsAsync("mmohsen")` false; `engineer.Slug == "mmohsen"`, `result.Slug == "mmohsen"`; `Update` `Received(1)`; `SaveChangesAsync` `Received(1)` |
| 31 | `Handle_ShouldNormalizeSlug_WhenSlugHasUppercaseAndWhitespace` | command slug `"  MMohsen  "` → `engineer.Slug == "mmohsen"`; `IsSlugExistsAsync("mmohsen", ...)` `Received(1)` |
| 32 | `Handle_ShouldChangeToSuffixedSlug_WhenRequestedSlugIsTaken` | `IsSlugExistsAsync("mmohsen")` true, `"mmohsen-ab12"` false; generator returns `"mmohsen-ab12-"`; `engineer.Slug == "mmohsen-ab12"`; `SaveChangesAsync` `Received(1)` |
| 33 | `Handle_ShouldLeaveSlugUnchanged_WhenSlugIsNull` | draft engineer, command slug `null`; `engineer.Slug == EngineerFactory.DefaultSlug`; `IsSlugExistsAsync` `DidNotReceive()`; `SaveChangesAsync` `Received(1)` |
| 34 | `Handle_ShouldLeaveSlugUnchanged_WhenRequestedSlugEqualsCurrentSlug` | **published** engineer, command slug `EngineerFactory.DefaultSlug`; no throw; `engineer.Slug` unchanged; `IsSlugExistsAsync` `DidNotReceive()`; `SaveChangesAsync` `Received(1)` |
| 35 | `Handle_ShouldThrowBusinessRuleViolation_WhenEngineerIsAlreadyPublished` | published engineer, command slug `"mmohsen"`; `BusinessRuleViolationCoreException` with `ErrorCode == ErrorCodes.EngineerSlugFrozen`; `Update` and `SaveChangesAsync` `DidNotReceive()`; `engineer.DisplayName` still the original (proves no mutation happened before the throw) |

### `api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryValidatorTests.cs` (new)

| # | Test method | Asserts |
|---|-------------|---------|
| 36 | `Validate_ShouldPass_WhenSlugIsValid` | `"mmohsen"` → `IsValid` true |
| 37 | `Validate_ShouldFail_WhenSlugIsMissing` | `[Theory]` `null`, `""`, `"   "` → `EngineerSlugRequired` |
| 38 | `Validate_ShouldFail_WhenSlugIsShorterThanMinimum` | `"ab"` → `EngineerSlugTooShort` |
| 39 | `Validate_ShouldFail_WhenSlugExceedsMaxLength` | `new string('a', 101)` → `EngineerSlugTooLong` |
| 40 | `Validate_ShouldFail_WhenSlugIsNotKebabCase` | `[Theory]` `"-mmohsen"`, `"mmohsen-"`, `"m--mohsen"`, `"m mohsen"` → `EngineerSlugInvalid` |
| 41 | `Validate_ShouldFail_WhenSlugIsReserved` | `"admin"` → `EngineerSlugReserved` |

### `api/E3A.Tests/Engineers/CheckSlugAvailability/CheckSlugAvailabilityQueryHandlerTests.cs` (new)

| # | Test method | Asserts |
|---|-------------|---------|
| 42 | `Handle_ShouldReturnAvailable_WhenSlugIsFree` | `IsSlugExistsAsync("mmohsen")` false → `Slug == "mmohsen"`, `IsAvailable` true, `SuggestedSlug` null; `_generator.DidNotReceive().Generate(...)` |
| 43 | `Handle_ShouldReturnUnavailableWithSuggestion_WhenSlugIsTaken` | `IsSlugExistsAsync("mmohsen")` true, `"mmohsen-ab12"` false, generator returns `"mmohsen-ab12-"` → `IsAvailable` false, `SuggestedSlug == "mmohsen-ab12"` |
| 44 | `Handle_ShouldReturnNormalizedSlug_WhenSlugHasUppercaseAndWhitespace` | query slug `"  MMohsen  "` → `result.Slug == "mmohsen"`; `IsSlugExistsAsync("mmohsen", ...)` `Received(1)` |
| 45 | `Handle_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated` | `UserId` null → `UnauthorizedCoreException` with `ErrorCode == ErrorCodes.UserNotAuthenticated`; `IsSlugExistsAsync` `DidNotReceive()` |

### Signature-only test updates (no behaviour change, no test added or removed)

- `api/E3A.Tests/Engineers/CreateEngineer/CreateEngineerValidatorTests.cs` — 8 call sites gain
  `EngineerFactory.DefaultSlug` as the first constructor argument.
- `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerHandlerTests.cs` — constructor wires
  `_generator` + options; 4 call sites gain `null` as the second constructor argument.
- `api/E3A.Tests/Engineers/UpdateEngineer/UpdateEngineerValidatorTests.cs` — 9 call sites gain
  `null` as the second constructor argument.
- `api/E3A.Tests/Engineers/Shared/EngineerFactory.cs` — `CreateEngineersOptions` gains
  `SlugMinLength = 3` and `ReservedSlugs = ["e3a", "api", "admin", "www", "docs", "health", "install", "marketplace", "catalog", "teams", "new", "edit", "settings", "z", "m"]`.

Explicitly not tested (out of scope per `conventions/dotnet-testing.md` §5): `EngineersController`,
`AppDbContext` configuration, `EngineerRepository`, the MediatR validation pipeline, DI registration.

## Definition of done

- [ ] `CreateEngineerCommand` is `(string Slug, string DisplayName, string? Description, List<string> Tags)`; `Slug` is required and no code path derives a slug from `DisplayName` any more.
- [ ] `UpdateEngineerCommand` is `(Guid EngineerId, string? Slug, string DisplayName, string? Description, List<string> Tags)`; `null` leaves the slug unchanged.
- [ ] `CreateEngineerHandler.GenerateUniqueSlugAsync` is deleted; both handlers and the availability handler call `EngineerSlugResolver.ResolveUniqueAsync`.
- [ ] `EngineerSlugResolver` trims the trailing separator emitted by `Core.Utilities.IGenerator`, and no file under `api/core-libraries/` is modified.
- [ ] The `IsSlugExistsAsync` + `IGenerator` suffix loop is intact — no `ConflictCoreException` is thrown anywhere in this slice (skill §8.3).
- [ ] `Engineer.ChangeSlug` sets `Slug` and stamps `UpdationDate = DateTimeOffset.UtcNow`; `Slug` remains `private set`; no handler mutates a property directly.
- [ ] `Engineer.IsSlugMutable => LatestVersionId == null;` exists; `UpdateEngineerHandler` throws `BusinessRuleViolationCoreException(ErrorCodes.EngineerSlugFrozen)` and does so **before** `UpdateMetadata` is called.
- [ ] Sending the engineer's current slug on update is a no-op even when the engineer is published.
- [ ] `EngineerSlugGenerator.Normalize(displayName, maxLength)` and `EngineerSlugGeneratorTests` are unchanged.
- [ ] `EngineersOptions` has `SlugMinLength` and `ReservedSlugs`; both are read from `appsettings.json`; no slug cap or reserved word is a constant in an entity, validator, or handler.
- [ ] All 5 slug validation rules run against the **normalized** value; `"  MMohsen  "` is accepted and stored as `"mmohsen"`; `"my slug!"` is rejected with `ENGINEER_SLUG_INVALID`.
- [ ] `GET /api/engineers/slug-availability?slug=` exists, is not `[AllowAnonymous]`, and returns `SlugAvailabilityResult(Slug, IsAvailable, SuggestedSlug)` with the normalized slug echoed back.
- [ ] All 6 new `ErrorCodes` constants exist and each has an identical key in **both** `Messages.ar.resx` and `Messages.en.resx`; the two files have the same key set and the same key order.
- [ ] `SaveChangesAsync` is called exactly once per mutating handler and never on a throwing path.
- [ ] `.ConfigureAwait(false)` on every `await` outside controllers and outside test method bodies.
- [ ] `postman/e3a.postman_collection.json` has the new `Check Slug Availability` request and both modified bodies.
- [ ] `docs/plugin-spec.md` (11, 87, 94), `docs/implementation-plan.md` (34, 44, 55) and `docs/design-prompt.md` (16) contain no remaining reference to `e3a-{githublogin}-{item-slug}` or `e3a-mohamed-dive-backend-engineer`; `grep -rn "githublogin" docs/` returns nothing.
- [ ] `docs/plugin-spec.md` line 90 (`author`) is unchanged.
- [ ] Exactly the 13 listed files are created; no others.
- [ ] All 45 enumerated tests exist with exactly the listed names; `Handle_ShouldRetrySuffixedSlug_WhenFirstCandidateIsAlsoTaken` is deleted from `CreateEngineerHandlerTests`.
- [ ] No test uses reflection or `new` on an entity; every entity comes from `EngineerFactory`.
- [ ] No new file exceeds ~100 lines; file-scoped namespaces everywhere; `sealed` on every new command, query, validator, handler, result, and test class.
- [ ] No new exception type, no new repository method, no service layer, no EF migration.
- [ ] `dotnet build` produces zero new warnings; `dotnet test` is green.
