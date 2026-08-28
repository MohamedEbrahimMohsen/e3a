using E3A.Application;
using E3A.Domain.Identity;
using E3A.Infrastructure;
using E3A.Infrastructure.Data.Context;
using Azure.Identity;
using Core.Auditing;
using Core.Azure;
using Core.CQRS;
using Core.EntityFrameworkCore;
using Core.Exceptions;
using Core.Identity;
using Core.Localization;
using Core.Logging;
using Core.Notifications;
using Core.Notifications.Endpoints;
using Core.OTP;
using Core.OTP.Endpoints;
using Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

#region EXTERNAL CONFIGURATION
if (builder.Environment.IsProduction())
{
    var endpoint = builder.Configuration["Azure:AACAppSettingsEndpoint"];
    var managedIdentityClientId = builder.Configuration["Azure:ManagedIdentityClientId"];
    var managedIdentityId = string.IsNullOrEmpty(managedIdentityClientId) ? null : ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId);
    builder.Configuration.AddAzureAppConfiguration(new Uri(endpoint!), managedIdentityId == null ? new DefaultAzureCredential() : new ManagedIdentityCredential(managedIdentityId));
}
#endregion

builder.Services.AddControllers();
builder.Services.AddOpenApi();

#region IDENTITY
builder.Services.AddCoreIdentity<User, Guid, Role, AppDbContext>(
    configuration: builder.Configuration,
    dbContextOptions: options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DbConnectionString"));
    },
    identityOptions: options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
        options.Password.RequiredUniqueChars = 0;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    }
);
#endregion

#region CORE SERVICES
builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddCoreLogging(builder.Configuration, builder.Environment);
builder.Services.AddCoreLocalization();
builder.Services.AddCoreAzure();
builder.Services.AddCoreExceptions();
// Register auditing BEFORE CQRS so AuditBehaviour sits outermost in the MediatR pipeline.
builder.Services.AddCoreAuditing(builder.Configuration);
builder.Services.AddCoreCQRS();
builder.Services.AddCoreOtp(builder.Configuration);
builder.Services.AddCoreNotifications(builder.Configuration);
builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>();
builder.Services.AddCoreUtilities();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();
#endregion

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins("http://localhost:5173", "http://localhost:5174").AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

// First, so every downstream middleware and handler resolves in the caller's language.
app.UseCoreLocalization(builder.Configuration);

app.UseMiddleware<CoreRequestLoggingMiddleware>();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

app.UseAuthorization();

app.UseMiddleware<CoreExceptionMiddleware>();

app.MapControllers();

app.MapCoreDevicesNotificationEndpoints();
app.MapCoreFirebaseNotificationEndpoints().RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));
app.MapCoreUserNotificationEndpoints().RequireAuthorization(policy => policy.RequireRole(RoleNames.User));
app.MapCoreNotificationTemplateEndpoints().RequireAuthorization(policy => policy.RequireRole(RoleNames.Admin));

app.MapCoreOTPEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

try
{
    await app.RunAsync();
}
finally
{
    // Flush buffered sinks so the last batch reaches the log store on shutdown.
    await Serilog.Log.CloseAndFlushAsync();
}
