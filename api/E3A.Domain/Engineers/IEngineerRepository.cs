using Core.DDD.Repositories;

namespace E3A.Domain.Engineers;

public interface IEngineerRepository : IRepository<Engineer>
{
    Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken);
}
