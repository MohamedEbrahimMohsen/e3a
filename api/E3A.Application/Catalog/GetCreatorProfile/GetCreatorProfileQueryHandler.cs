using Core.Errors;
using E3A.Application.Catalog.Shared;
using E3A.Application.Exceptions;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Teams;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace E3A.Application.Catalog.GetCreatorProfile;

public sealed class GetCreatorProfileQueryHandler(IUserRepository userRepository, IEngineerRepository engineerRepository, ITeamRepository teamRepository) : IRequestHandler<GetCreatorProfileQuery, CreatorProfileResult>
{
    private const string CaseInsensitiveCollation = "SQL_Latin1_General_CP1_CI_AS";

    public async Task<CreatorProfileResult> Handle(GetCreatorProfileQuery request, CancellationToken cancellationToken)
    {
        var login = request.GitHubLogin.Trim();

        // No collation is configured on Users.GitHubLogin, so case-insensitivity would otherwise rest on an
        // unstated database default. Collate forces it in the query itself. ToUpper()/string.Equals cannot be
        // used here: the analyzers reject the first and EF Core cannot translate the second.
        var creator = await userRepository.FirstOrDefaultAsync(x => x.GitHubLogin != null && EF.Functions.Collate(x.GitHubLogin, CaseInsensitiveCollation) == login, cancellationToken, asNoTracking: true).ConfigureAwait(false);

        if (creator == null)
        {
            throw new NotFoundCoreException(ErrorCodes.UserNotFound);
        }

        var ownerUserId = creator.Id;
        var engineers = await engineerRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var teams = await teamRepository.FindAsync(x => x.OwnerUserId == ownerUserId, cancellationToken, include: query => query.Include(x => x.Members), asNoTracking: true).ConfigureAwait(false);

        var publishedEngineers = engineers
            .Where(x => x.Status == EngineerStatus.Published)
            .OrderByDescending(x => x.CreationDate)
            .ThenBy(x => x.Id)
            .ToList();

        var publishedTeams = teams
            .Where(x => x.Status == TeamStatus.Published)
            .OrderByDescending(x => x.CreationDate)
            .ThenBy(x => x.Id)
            .ToList();

        return CreatorProfileResultGenerator.Generate(creator, publishedEngineers, publishedTeams);
    }
}
