using E3A.Application.Authentication.Shared;
using MediatR;

namespace E3A.Application.Authentication.GetCurrentUser;

public sealed record GetCurrentUserQuery : IRequest<CurrentUserResult>;
