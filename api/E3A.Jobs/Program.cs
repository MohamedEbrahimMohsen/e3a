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
