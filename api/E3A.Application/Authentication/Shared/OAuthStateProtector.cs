using Core.Identity.Tokens;
using Core.Utilities.Generator;
using E3A.Application.Options;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace E3A.Application.Authentication.Shared;

public sealed class OAuthStateProtector(IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions, IOptions<JwtOptions> jwtOptions, IGenerator generator) : IOAuthStateProtector
{
    // Neither the nanoid alphabet nor Base64Url contains '.', so a segment can never swallow the separator.
    private const char PayloadSeparator = '.';

    private const int ExpectedSegmentCount = 3;

    public OAuthState Create()
    {
        var options = gitHubAuthenticationOptions.Value;
        var nonce = generator.Generate(options.StateNonceSize);
        var expiresAtUnixSeconds = DateTimeOffset.UtcNow.AddMinutes(options.StateExpirationMinutes).ToUnixTimeSeconds();
        var payload = $"{nonce}{PayloadSeparator}{expiresAtUnixSeconds}";

        return new OAuthState($"{payload}{PayloadSeparator}{Sign(payload)}", nonce);
    }

    public OAuthStateStatus Validate(string? state, string? nonce)
    {
        if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(nonce))
        {
            return OAuthStateStatus.Invalid;
        }

        var segments = state.Split(PayloadSeparator);

        if (segments.Length != ExpectedSegmentCount)
        {
            return OAuthStateStatus.Invalid;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(segments[0]), Encoding.UTF8.GetBytes(nonce)))
        {
            return OAuthStateStatus.Invalid;
        }

        if (!long.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresAtUnixSeconds))
        {
            return OAuthStateStatus.Invalid;
        }

        var expectedSignature = Sign($"{segments[0]}{PayloadSeparator}{segments[1]}");

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(segments[2])))
        {
            return OAuthStateStatus.Invalid;
        }

        if (DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds) < DateTimeOffset.UtcNow)
        {
            return OAuthStateStatus.Expired;
        }

        return OAuthStateStatus.Valid;
    }

    private string Sign(string payload)
    {
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwtOptions.Value.Key), Encoding.UTF8.GetBytes(payload));

        return Base64Url.EncodeToString(signature);
    }
}
