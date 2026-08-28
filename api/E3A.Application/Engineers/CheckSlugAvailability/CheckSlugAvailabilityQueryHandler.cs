using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.CheckSlugAvailability;

public sealed class CheckSlugAvailabilityQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<EngineersOptions> engineersOptions) : IRequestHandler<CheckSlugAvailabilityQuery, SlugAvailabilityResult>
{
    public async Task<SlugAvailabilityResult> Handle(CheckSlugAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var slug = EngineerSlugGenerator.NormalizeTypedSlug(request.Slug);

        if (!await engineerRepository.IsSlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            return new SlugAvailabilityResult(slug, true, null);
        }

        var suggestedSlug = await EngineerSlugResolver.ResolveUniqueAsync(slug, engineerRepository, generator, engineersOptions.Value, cancellationToken).ConfigureAwait(false);

        return new SlugAvailabilityResult(slug, false, suggestedSlug);
    }
}
