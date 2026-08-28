using E3A.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace E3A.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(mediatRConfiguration => mediatRConfiguration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.Configure<EngineersOptions>(configuration.GetSection(EngineersOptions.SectionName));
        services.Configure<UploadsOptions>(configuration.GetSection(UploadsOptions.SectionName));
        services.Configure<AzureOptions>(configuration.GetSection(AzureOptions.SectionName));
        services.Configure<CatalogOptions>(configuration.GetSection(CatalogOptions.SectionName));
        services.Configure<PublishingOptions>(configuration.GetSection(PublishingOptions.SectionName));

        return services;
    }
}
