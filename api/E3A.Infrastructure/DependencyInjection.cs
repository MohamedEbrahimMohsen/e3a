using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using E3A.Infrastructure.Engineers;
using E3A.Infrastructure.Identity;
using E3A.Infrastructure.Publishing;
using E3A.Infrastructure.Teams;
using Microsoft.Extensions.DependencyInjection;

namespace E3A.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEngineerRepository, EngineerRepository>();
        services.AddScoped<IItemVersionRepository, ItemVersionRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
