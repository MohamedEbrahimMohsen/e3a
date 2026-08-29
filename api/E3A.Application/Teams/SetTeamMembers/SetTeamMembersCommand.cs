using E3A.Application.Teams.Shared;
using MediatR;

namespace E3A.Application.Teams.SetTeamMembers;

public sealed record SetTeamMembersCommand(Guid TeamId, List<TeamMemberSelection> Members) : IRequest<TeamDetailResult>;

public sealed record TeamMemberSelection(Guid EngineerId, Guid? PinnedVersionId);
