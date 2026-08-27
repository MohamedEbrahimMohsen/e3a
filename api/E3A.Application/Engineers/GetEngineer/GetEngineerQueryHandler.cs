using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Engineers.GetEngineer;

public sealed class GetEngineerQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<GetEngineerQuery, EngineerResult>
{
    public async Task<EngineerResult> Handle(GetEngineerQuery request, CancellationToken cancellationToken)
    {
        var engineer = await engineerRepository.GetByIdAsync(request.EngineerId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (engineer == null)
        {
            throw new NotFoundCoreException(ErrorCodes.EngineerNotFound);
        }

        if (engineer.Status == EngineerStatus.Published)
        {
            return EngineerResultGenerator.Generate(engineer);
        }

        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        if (engineer.OwnerUserId != userId.Value)
        {
            throw new ForbiddenCoreException(ErrorCodes.EngineerNotOwned);
        }

        return EngineerResultGenerator.Generate(engineer);
    }
}
