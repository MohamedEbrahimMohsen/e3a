using Core.Azure;
using Core.CQRS;
using Core.EntityFrameworkCore;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Core.Utilities;
using E3A.Application;
using E3A.Domain.Identity;
using E3A.Infrastructure;
using E3A.Infrastructure.Data.Context;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

// This job-only host shares the E3A.Application assembly with the API, so MediatR's assembly scan
// also registers auth handlers (e.g. CompleteGitHubLoginHandler) that depend on ITokenService —
// authentication infrastructure the worker deliberately does not wire up. Those handlers are never
// resolved on the queue path, so eager container validation must not fail on their unused dependencies.
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
{
    ValidateOnBuild = false,
    ValidateScopes = false,
}));

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DbConnectionString")));
builder.Services.AddIdentityCore<User>().AddRoles<Role>().AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddCoreLocalization();
builder.Services.AddCoreAzure();
builder.Services.AddCoreCQRS();
builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>();
builder.Services.AddCoreUtilities();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

await builder.Build().RunAsync();
