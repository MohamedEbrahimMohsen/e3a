using E3A.Application.Authentication.Shared;
using MediatR;

namespace E3A.Application.Authentication.GetGitHubLoginUrl;

public sealed record GetGitHubLoginUrlQuery : IRequest<AuthenticationRedirectResult>;
