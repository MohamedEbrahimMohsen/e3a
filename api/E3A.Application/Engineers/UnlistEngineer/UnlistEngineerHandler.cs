using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Application.Publishing.RegenerateMarketplace;
using E3A.Domain.Engineers;
using E3A.Domain.Publishing;
using MediatR;

namespace E3A.Application.Engineers.UnlistEngineer;

public sealed class UnlistEngineerHandler(IEngineerRepository engineerRepository, IItemVersionRepository itemVersionRepository, ICurrentUserService currentUserService, ISender sender) : IRequestHandler<UnlistEngineerCommand, EngineerResult>
{
    public async Task<EngineerResult> Handle(UnlistEngineerCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        var inProgress = await itemVersionRepository.FirstOrDefaultAsync(x => x.ItemType == ItemType.Engineer && x.ItemId == engineer.Id && (x.Status == ItemVersionStatus.Queued || x.Status == ItemVersionStatus.Building), cancellationToken).ConfigureAwait(false);

        if (inProgress != null)
        {
            throw new ConflictCoreException(ErrorCodes.PublishAlreadyInProgress);
        }

        if (engineer.Status != EngineerStatus.Published)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.EngineerNotPublished);
        }

        engineer.Unlist();

        engineerRepository.Update(engineer);
        await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await sender.Send(new RegenerateMarketplaceCommand(), cancellationToken).ConfigureAwait(false);

        return EngineerResultGenerator.Generate(engineer);
    }
}
