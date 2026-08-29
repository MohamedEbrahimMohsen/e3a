using E3A.Application.Authentication.Shared;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Infrastructure.Authentication;
using E3A.Infrastructure.Engineers;
using E3A.Infrastructure.Identity;
using E3A.Infrastructure.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace E3A.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEngineerRepository, EngineerRepository>();
        services.AddScoped<IItemVersionRepository, ItemVersionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitHubAuthenticationOptions>>().Value;
            httpClient.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        });

        return services;
    }
}
