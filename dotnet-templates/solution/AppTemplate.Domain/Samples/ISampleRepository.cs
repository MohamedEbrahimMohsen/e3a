using Core.DDD.Repositories;

namespace AppTemplate.Domain.Samples;

public interface ISampleRepository : IRepository<Sample>
{
    Task<Sample?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}
