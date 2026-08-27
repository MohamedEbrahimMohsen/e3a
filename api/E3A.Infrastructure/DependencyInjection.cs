using E3A.Domain.Engineers;
using E3A.Infrastructure.Engineers;
using Microsoft.Extensions.DependencyInjection;

namespace E3A.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEngineerRepository, EngineerRepository>();

        return services;
    }
}
