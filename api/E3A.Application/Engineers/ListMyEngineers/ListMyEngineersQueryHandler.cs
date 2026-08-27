using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Engineers.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using MediatR;

namespace E3A.Application.Engineers.ListMyEngineers;

public sealed class ListMyEngineersQueryHandler(IEngineerRepository engineerRepository, ICurrentUserService currentUserService) : IRequestHandler<ListMyEngineersQuery, List<EngineerResult>>
{
    public async Task<List<EngineerResult>> Handle(ListMyEngineersQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var ownerUserId = userId.Value;
        var engineers = await engineerRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        return engineers
            .OrderByDescending(x => x.CreationDate)
            .Select(EngineerResultGenerator.Generate)
            .ToList();
    }
}
