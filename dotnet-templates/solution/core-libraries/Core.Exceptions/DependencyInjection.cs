using Microsoft.Extensions.DependencyInjection;

namespace Core.Exceptions;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreExceptions(this IServiceCollection services)
    {
        services.AddScoped<IErrorResponseHandler, ErrorResponseHandler>();
        return services;
    }
}
