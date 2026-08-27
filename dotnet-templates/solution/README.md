# AppTemplate

A four-layer service over the shared `Core.*` libraries.

```
Domain          entities, value objects, enums, domain events, repository interfaces
Application     commands, queries, handlers, validators, results
Infrastructure  repository implementations, DbContext, EF configuration, migrations
Api             controllers, request records, composition root
Jobs            scheduled and queue-triggered functions
Tests           one project mirroring the production structure
```

## First run

```
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:DbConnectionString" "<connection string>" --project AppTemplate.Api
dotnet ef database update --project AppTemplate.Infrastructure --startup-project AppTemplate.Api
dotnet run --project AppTemplate.Api
```

The API reference is served at `/scalar/v1`; health at `/health`.

## Shared libraries

`core-libraries/` is vendored into this solution and referenced as projects, so
the solution builds with no feed and no configuration. `CoreMode` switches how
they are referenced:

```
dotnet build                       # project references (default)
dotnet build -p:CoreMode=package   # once Core is published
```

### The vendored copy is a copy

It must not drift. Editing it to solve a problem specific to this service forks
Core silently, and the fork is only discovered when two services behave
differently for reasons nobody can find.

```powershell
./sync-core.ps1 -Target . -Check    # has it drifted?
./sync-core.ps1 -Target .           # refresh from the canonical copy
```

A locally modified Core file is one of two things: service-specific code that
does not belong in Core, or a genuine Core improvement. If it is the second,
promote it to the canonical copy first, then sync back — never leave it only
here.

The `core-drift` gate runs this check as part of the ladder.

### Package mode

Versions pin centrally in `Directory.Packages.props`. A rebuilt package with an
unchanged version is served from the global cache rather than the feed, so
every local pack must produce a unique version — Core's own
`Directory.Build.props` adds a timestamp suffix for exactly this reason.

## Standards

`.specify/memory/constitution.md` is the law of this repository, and
`.claude/rules/` are always in force. Static analysis runs in-build via
`SonarAnalyzer.CSharp` — there is no server to install.
