using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Local development seeding tool — inserts catalog demo data through the real domain factories.
// Default targets the localdb the API uses; pass a connection string argument to override.
const string DefaultConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=E3A;Trusted_Connection=True;TrustServerCertificate=True;";

var connectionString = args.Length > 0 ? args[0] : DefaultConnectionString;
var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(EngineersOptions).Assembly));
services.Configure<EngineersOptions>(options => { options.SlugMaxLength = 100; options.TagsColumnMaxLength = 400; options.DisplayNameMaxLength = 100; options.DescriptionMaxLength = 500; });

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var owners = new[] { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("33333333-3333-3333-3333-333333333333") };
var manifestJson = """{"Imported":[{"SourcePath":"skills/ddd-slices/SKILL.md","TargetPath":"skills/ddd-slices/SKILL.md","Category":"Skills"},{"SourcePath":"agents/reviewer.md","TargetPath":"agents/reviewer.md","Category":"Agents"},{"SourcePath":"settings.json#hooks","TargetPath":"hooks/hooks.json","Category":"Hooks"}],"Converted":[{"SourcePath":"CLAUDE.md","TargetPath":"skills/house-rules/SKILL.md","Reason":"Always-on instructions travel as a skill."}],"Skipped":[{"SourcePath":"settings.json#permissions","Reason":"No plugin equivalent."}],"StrippedPaths":["settings.local.json"],"HookWarnings":[{"Event":"PreToolUse","Matcher":"Bash","Command":"./hooks/guard.sh"}],"ClaudeMdSnippet":null,"UploadedAt":"2026-08-20T10:00:00+00:00"}""";

var seedRows = new (string Name, string? Description, string[] Tags, int Installs, int AgeDays, int Owner, bool WithManifest)[]
{
    ("Security Reviewer", "Threat-models every diff. OWASP, secrets scanning, dependency audits.", ["security", "review"], 2310, 160, 1, true),
    ("Dive Backend Engineer", "Senior .NET backend engineer — CQRS vertical slices, EF Core, clean error contracts.", ["dotnet", "cqrs", "api"], 1204, 150, 0, true),
    ("React Frontend", "React 18 + TypeScript strict. TanStack Query, RHF + Zod, shadcn/ui.", ["react", "typescript"], 987, 140, 0, false),
    ("Test Author", "Writes the tests you skipped. Unit, integration, property-based.", ["testing", "tdd"], 640, 120, 2, false),
    ("Code Archaeologist", "Explains legacy code and maps hidden coupling before you refactor.", ["legacy", "analysis"], 530, 110, 2, true),
    ("DevOps Runner", "CI/CD pipelines, Docker, Terraform modules, rollback playbooks.", ["devops", "ci"], 412, 100, 1, false),
    ("Payments Engineer", "Integrates payment gateways with idempotent retries and reconciliation.", ["payments", "dotnet"], 156, 90, 0, false),
    ("Prompt Curator", "Curates prompts and personas for coding agents across teams.", ["prompts", "docs"], 93, 75, 1, false),
    ("Docs Writer", "ADRs, READMEs, runbooks. Keeps docs in sync with the code.", ["docs"], 77, 60, 1, false),
    ("API Designer", "REST and gRPC contracts, versioning strategy, OpenAPI-first.", ["api", "openapi"], 38, 45, 2, false),
    ("Data Migrator", "Zero-downtime schema migrations and backfills, with rollback plans.", ["sql", "migrations"], 21, 30, 2, false),
    ("Accessibility Auditor", "WCAG audits and fixes for web frontends.", ["react", "a11y"], 12, 20, 0, false),
    ("Sql Tuning Engineer", "EF Core query analysis, indexes and execution plans.", ["sql", "dotnet"], 7, 10, 0, false),
    ("Fresh Engineer", "Just published — no installs yet.", ["dotnet"], 0, 2, 1, false),
};

var seededCount = 0;

foreach (var row in seedRows)
{
    var slug = row.Name.ToLowerInvariant().Replace(' ', '-');

    if (await context.Engineers.AnyAsync(x => x.Slug == slug))
    {
        continue;
    }

    var engineer = Engineer.Create(owners[row.Owner], slug, row.Name, row.Description, [.. row.Tags]);
    engineer.MarkPublished(Guid.NewGuid());
    engineer.RecordInstallCount(row.Installs);

    if (row.WithManifest)
    {
        engineer.ReplaceDraftManifest(manifestJson);
    }

    engineer.CreationDate = DateTimeOffset.UtcNow.AddDays(-row.AgeDays);
    await context.Engineers.AddAsync(engineer);
    seededCount++;
}

var draftRows = new (string Name, string[] Tags)[] { ("Hidden Draft Engineer", ["dotnet"]), ("Unpublished Experiment", ["testing"]) };

foreach (var row in draftRows)
{
    var slug = row.Name.ToLowerInvariant().Replace(' ', '-');

    if (await context.Engineers.AnyAsync(x => x.Slug == slug))
    {
        continue;
    }

    await context.Engineers.AddAsync(Engineer.Create(owners[0], slug, row.Name, "Draft — must never appear in the public catalog.", [.. row.Tags]));
    seededCount++;
}

await context.SaveChangesAsync(CancellationToken.None);
Console.WriteLine($"Seeded {seededCount} engineers (idempotent by slug). Published: {seedRows.Length}, drafts: {draftRows.Length}.");
