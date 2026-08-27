using Core.Utilities.Generator;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Utilities;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreUtilities(this IServiceCollection services)
    {
        services.AddScoped<IGenerator, Generator.Generator>();

        return services;
    }
}