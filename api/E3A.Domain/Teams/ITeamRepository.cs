using Core.DDD.Repositories;

namespace E3A.Domain.Teams;

public interface ITeamRepository : IRepository<Team>
{
    Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken);
}
