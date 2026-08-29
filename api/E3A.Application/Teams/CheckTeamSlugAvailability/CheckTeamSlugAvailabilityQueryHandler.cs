using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Application.Shared;
using E3A.Domain.SharedKernel;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Teams.CheckTeamSlugAvailability;

public sealed class CheckTeamSlugAvailabilityQueryHandler(ITeamRepository teamRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<TeamsOptions> teamsOptions) : IRequestHandler<CheckTeamSlugAvailabilityQuery, SlugAvailabilityResult>
{
    public async Task<SlugAvailabilityResult> Handle(CheckTeamSlugAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var slug = SlugGenerator.NormalizeTypedSlug(request.Slug);

        if (!await teamRepository.IsSlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            return new SlugAvailabilityResult(slug, true, null);
        }

        var suggestedSlug = await SlugResolver.ResolveUniqueAsync(slug, teamRepository.IsSlugExistsAsync, generator, teamsOptions.Value.SlugMaxLength, teamsOptions.Value.SlugSuffixSize, cancellationToken).ConfigureAwait(false);

        return new SlugAvailabilityResult(slug, false, suggestedSlug);
    }
}
