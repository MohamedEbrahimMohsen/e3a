using Core.EntityFrameworkCore.Repositories;
using E3A.Domain.Teams;
using E3A.Infrastructure.Data.Context;

namespace E3A.Infrastructure.Teams;

public class TeamRepository(AppDbContext context) : Repository<Team>(context), ITeamRepository
{
    public async Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        var matchingSlugCount = await CountAsync(cancellationToken, x => x.Slug == slug).ConfigureAwait(false);
        return matchingSlugCount > 0;
    }
}
