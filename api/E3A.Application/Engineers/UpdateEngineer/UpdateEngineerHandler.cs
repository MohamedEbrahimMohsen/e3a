using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Engineers.UpdateEngineer;

public sealed class UpdateEngineerHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<UpdateEngineerCommand, EngineerResult>
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

        engineer.UpdateMetadata(request.DisplayName, request.Description, request.Tags);

        engineerRepository.Update(engineer);
        await engineerRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return EngineerResultGenerator.Generate(engineer);
    }
}
