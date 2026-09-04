using Core.Utilities.Generator;
using E3A.Application.Options;
using E3A.Domain.Identity;

namespace E3A.Application.Authentication.Shared;

public static class UserNameResolver
{
    public static async Task<string> ResolveUniqueAsync(string gitHubLogin, IUserRepository userRepository, IGenerator generator, GitHubAuthenticationOptions options, CancellationToken cancellationToken)
    {
        if (!await userRepository.IsUserNameExistsAsync(gitHubLogin.ToUpperInvariant(), cancellationToken).ConfigureAwait(false))
        {
            return gitHubLogin;
        }

        string candidateUserName;

        do
        {
            // Core IGenerator always emits the separator before the empty suffix, leaving a trailing hyphen.
            candidateUserName = generator.Generate(prefix: gitHubLogin, size: options.UserNameSuffixSize).TrimEnd('-');
        } while (await userRepository.IsUserNameExistsAsync(candidateUserName.ToUpperInvariant(), cancellationToken).ConfigureAwait(false));

        return candidateUserName;
    }
}
