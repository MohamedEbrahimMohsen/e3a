using Core.DDD.Entities;
using Core.Identity.Tokens;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.CurrentUser;
using Core.Identity.Tokens.RefreshToken;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Core.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreIdentity<TUser, TKey, TRole, TDbContext>(
                this IServiceCollection services,
                IConfiguration configuration,
                Action<DbContextOptionsBuilder>? dbContextOptions,
                Action<IdentityOptions>? identityOptions = null) //,Action<JwtBearerOptions>? jwtOptions = null
                where TUser : IdentityUser<TKey>, IEntity, new()
                where TKey : IEquatable<TKey>, new()
                where TRole: IdentityRole<TKey>, new()
                where TDbContext : DbContext
    {
        //services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        //services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        //services.AddTransient<IRequestHandler<RegisterCommand<TUser>, RegisterResult>, RegisterHandler<TUser>>();

        services.AddScoped<ITokenService, JwtTokenService<TUser, TKey>>();
        services.AddScoped<IRefreshTokenService<TUser, TKey>, RefreshTokenService<TUser, TKey>>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        services.AddAuthentication()
                .AddBearerToken(IdentityConstants.BearerScheme);

        services.AddAuthorizationBuilder();

        services.AddDbContext<TDbContext>(options => dbContextOptions?.Invoke(options));

        services.AddIdentityCore<TUser>(options => identityOptions?.Invoke(options))
                .AddRoles<TRole>()
                .AddApiEndpoints()
                .AddEntityFrameworkStores<TDbContext>()
                .AddDefaultTokenProviders();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                var jwtOptions = jwt.Value;

                if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
                    throw new InvalidOperationException("JWT Issuer is not configured.");

                if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
                    throw new InvalidOperationException("Audience Key is not configured.");

                if (string.IsNullOrWhiteSpace(jwtOptions.Key))
                    throw new InvalidOperationException("JWT Key is not configured.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorization();

        return services;
    }
}