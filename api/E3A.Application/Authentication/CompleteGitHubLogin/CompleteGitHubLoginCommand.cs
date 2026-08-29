using E3A.Application.Authentication.Shared;
using MediatR;

namespace E3A.Application.Authentication.CompleteGitHubLogin;

public sealed record CompleteGitHubLoginCommand(string? Code, string? State, string? Nonce) : IRequest<AuthenticationRedirectResult>;
