using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace Core.Localization;

public static class DependencyInjection
{
    public static IApplicationBuilder UseCoreLocalization(this IApplicationBuilder app, IConfiguration configuration)
    {
        var defaultLang = configuration["CoreLocalization:DefaultLanguage"]?[..2].ToLowerInvariant() ?? "en";

        var supportedCultures = new[]
        {
            new CultureInfo("en"),
            new CultureInfo("en-US"),
            new CultureInfo("ar"),
            new CultureInfo("ar-EG")
        };

        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(defaultLang),
            SupportedCultures = supportedCultures,
            SupportedUICultures = supportedCultures,
            FallBackToParentCultures = true,
            FallBackToParentUICultures = true
        };

        app.UseRequestLocalization(options);

        return app;
    }

    public static IServiceCollection AddCoreLocalization(this IServiceCollection services)
    {
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddScoped<ILocalizationManager, LocalizationManager>();
        services.AddScoped<ILocalizer, Localizer>();

        return services;
    }
}
