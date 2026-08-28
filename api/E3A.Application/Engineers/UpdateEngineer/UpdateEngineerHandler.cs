using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Utilities.Generator;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Options;
using E3A.Domain.Engineers;
using MediatR;
using Microsoft.Extensions.Options;

namespace E3A.Application.Engineers.UpdateEngineer;

public sealed class UpdateEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService, IGenerator generator, IOptions<EngineersOptions> engineersOptions) : IRequestHandler<UpdateEngineerCommand, EngineerResult>
{
    public async Task<EngineerResult> Handle(UpdateEngineerCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.OwnerUserId != ownerUserId)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        var resolvedSlug = await ResolveSlugChangeAsync(request, engineer, cancellationToken).ConfigureAwait(false);

        engineer.UpdateMetadata(request.DisplayName, request.Description, request.Tags);

        if (resolvedSlug != null)
        {
            engineer.ChangeSlug(resolvedSlug);
        }

        engineerRepository.Update(engineer);
        await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return EngineerResultGenerator.Generate(engineer);
    }

    private async Task<string?> ResolveSlugChangeAsync(UpdateEngineerCommand request, Engineer engineer, CancellationToken cancellationToken)
    {
        if (request.Slug == null)
        {
            return null;
        }

        var requestedSlug = EngineerSlugGenerator.NormalizeTypedSlug(request.Slug);

        if (requestedSlug == engineer.Slug)
        {
            return null;
        }

        if (!engineer.IsSlugMutable)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.EngineerSlugFrozen);
        }

        return await EngineerSlugResolver.ResolveUniqueAsync(requestedSlug, engineerRepository, generator, engineersOptions.Value, cancellationToken).ConfigureAwait(false);
    }
}
