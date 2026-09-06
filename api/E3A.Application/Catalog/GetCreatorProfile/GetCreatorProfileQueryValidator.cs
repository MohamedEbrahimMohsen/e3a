using Core.Validation.Extensions;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace E3A.Application.Catalog.GetCreatorProfile;

public sealed class GetCreatorProfileQueryValidator : AbstractValidator<GetCreatorProfileQuery>
{
    public GetCreatorProfileQueryValidator(IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions)
    {
        var options = gitHubAuthenticationOptions.Value;

        RuleFor(x => x.GitHubLogin)
            .ValidateRequired(ErrorCodes.CatalogCreatorLoginRequired)
            .ValidateMaxLength(options.GitHubLoginMaxLength, ErrorCodes.CatalogCreatorLoginTooLong);
    }
}
