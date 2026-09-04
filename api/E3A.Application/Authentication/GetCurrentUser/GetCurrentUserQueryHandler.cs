using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using E3A.Application.Authentication.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Identity;
using MediatR;

namespace E3A.Application.Authentication.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IUserRepository userRepository, ICurrentUserService currentUserService) : IRequestHandler<GetCurrentUserQuery, CurrentUserResult>
{
    public async Task<CurrentUserResult> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;

        if (userId == null || userId == Guid.Empty)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var user = await userRepository.GetByIdAsync(userId.Value, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (user == null)
        {
            throw new NotFoundCoreException(ErrorCodes.UserNotFound);
        }

        return new CurrentUserResult(user.Id, user.GitHubId, user.GitHubLogin, user.DisplayName, user.AvatarUrl, user.CreationDate);
    }
}
