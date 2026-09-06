using E3A.Application.Catalog.Shared;
using MediatR;

namespace E3A.Application.Catalog.GetCreatorProfile;

public sealed record GetCreatorProfileQuery(string GitHubLogin) : IRequest<CreatorProfileResult>;
