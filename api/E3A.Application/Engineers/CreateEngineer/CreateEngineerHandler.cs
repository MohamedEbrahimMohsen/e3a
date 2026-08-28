using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.CreateEngineer;

public sealed class CreateEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<EngineersOptions> engineersOptions) : IRequestHandler<CreateEngineerCommand, EngineerResult>
{
    public async Task<EngineerResult> Handle(CreateEngineerCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var options = engineersOptions.Value;
        var ownedEngineerCount = await engineerRepository.CountAsync(cancellationToken, x => x.OwnerUserId == ownerUserId).ConfigureAwait(false);

        if (ownedEngineerCount >= options.MaxEngineersPerCreator)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.EngineerLimitReached, context: new Dictionary<string, object> { ["limit"] = options.MaxEngineersPerCreator });
        }

        var slug = await EngineerSlugResolver.ResolveUniqueAsync(EngineerSlugGenerator.NormalizeTypedSlug(request.Slug), engineerRepository, generator, options, cancellationToken).ConfigureAwait(false);
        var engineer = Engineer.Create(ownerUserId, slug, request.DisplayName, request.Description, request.Tags);

        await engineerRepository.AddAsync(engineer, cancellationToken).ConfigureAwait(false);
        await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return EngineerResultGenerator.Generate(engineer);
    }
}
