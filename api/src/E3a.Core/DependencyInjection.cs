using E3a.Core.Infrastructure.Plugins;
using E3a.Core.Infrastructure.Scanning;
using E3a.Core.Infrastructure.Validation;
using E3a.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace E3a.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddE3aCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PublishingOptions>(configuration.GetSection(PublishingOptions.SectionName));
        services.Configure<MarketplaceOptions>(configuration.GetSection(MarketplaceOptions.SectionName));

        services.AddSingleton<PluginBuilder>();
        services.AddSingleton<PackageComposer>();
        services.AddSingleton<StructureValidator>();
        services.AddSingleton<SecurityScanner>();
        services.AddSingleton<MarketplaceGenerator>();

        return services;
    }
}
