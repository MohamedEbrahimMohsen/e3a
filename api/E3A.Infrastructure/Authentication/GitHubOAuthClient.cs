using E3A.Application.Authentication.Shared;
using E3A.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace E3A.Infrastructure.Authentication;

public sealed partial class GitHubOAuthClient(HttpClient httpClient, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions, ILogger<GitHubOAuthClient> logger) : IGitHubOAuthClient
{
    private const string GitHubJsonMediaType = "application/vnd.github+json";
    private const string BearerScheme = "Bearer";

    public async Task<string?> ExchangeCodeForAccessTokenAsync(string code, CancellationToken cancellationToken)
    {
        var options = gitHubAuthenticationOptions.Value;

        using var request = new HttpRequestMessage(HttpMethod.Post, options.AccessTokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = options.CallbackUrl,
            }),
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = await SendAsync<GitHubAccessTokenPayload>(request, cancellationToken).ConfigureAwait(false);

        if (payload == null || !string.IsNullOrWhiteSpace(payload.Error))
        {
            return null;
        }

        return payload.AccessToken;
    }

    public async Task<GitHubProfile?> GetProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, gitHubAuthenticationOptions.Value.UserProfileUrl);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GitHubJsonMediaType));
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);

        var payload = await SendAsync<GitHubProfilePayload>(request, cancellationToken).ConfigureAwait(false);

        if (payload == null)
        {
            return null;
        }

        return new GitHubProfile(payload.Id, payload.Login ?? string.Empty, payload.Name, payload.AvatarUrl);
    }

    private async Task<TPayload?> SendAsync<TPayload>(HttpRequestMessage request, CancellationToken cancellationToken) where TPayload : class
    {
        var requestPath = request.RequestUri?.GetLeftPart(UriPartial.Path);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                LogUnsuccessfulResponse(logger, requestPath, (int)response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return JsonSerializer.Deserialize<TPayload>(content);
        }
        catch (HttpRequestException exception)
        {
            LogFaultedRequest(logger, exception, requestPath);
            return null;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogFaultedRequest(logger, exception, requestPath);
            return null;
        }
        catch (JsonException exception)
        {
            LogFaultedRequest(logger, exception, requestPath);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub request to {RequestPath} returned status {StatusCode}.")]
    private static partial void LogUnsuccessfulResponse(ILogger logger, string? requestPath, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GitHub request to {RequestPath} did not complete.")]
    private static partial void LogFaultedRequest(ILogger logger, Exception exception, string? requestPath);
}
