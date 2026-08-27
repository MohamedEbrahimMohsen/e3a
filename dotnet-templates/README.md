# Templates

```
dotnet pack CoreOrg.Templates.csproj -o ./nupkg
dotnet new install ./nupkg/CoreOrg.Templates.1.0.0.nupkg
```

## Solution

The Core libraries are vendored into the template, so a generated solution
builds immediately with no path argument and no feed:

```
dotnet new company-api -n Acme.Billing
```

Once Core is published, `--coreMode package` drops the vendored copy and uses
the feed instead:

```
dotnet new company-api -n Acme.Billing --coreMode package --coreOrg Company --coreVersion 1.2.0
```

| Option | Default | Effect |
|---|---|---|
| `--coreMode` | `project` | `project` = vendored `core-libraries/` referenced as projects · `package` = PackageReference, vendored copy omitted |
| `--coreOrg` | `Company` | package-id prefix — package mode only |
| `--coreVersion` | `1.0.0` | version pinned in `Directory.Packages.props` — package mode only |
| `--jobs` | `true` | include the background jobs host |
| `--sample` | `false` | include one throwaway slice as a live convention reference |

`sourceName` is `AppTemplate`, so the name you pass rewrites every project,
folder, and namespace. `Core.*` is untouched by that rename — the package ids
are governed by `--coreOrg` instead.

### The copy has to be kept honest

Every generated solution holds its own copy of Core. That is what makes it
self-contained, and it is also how sixteen quiet forks happen. Two things keep
it in check: `sync-core.ps1` refreshes a solution's copy from the canonical one
at the kit root, and the `core-drift` gate reports divergence during the ladder.

Solutions in package mode have no copy and no drift.

### Central package management

`core-libraries/Directory.Packages.props` sets
`ManagePackageVersionsCentrally` to false. Core pins its versions inline, and
without that file the solution's central package management would reach the
Core projects and fail the build with NU1008.

## Items

Run from inside the Application project so `RootNamespace` binds:

```
dotnet new cqrs-command -n AddOrder --feature Orders
dotnet new cqrs-query   -n ListOrders --feature Orders
```
