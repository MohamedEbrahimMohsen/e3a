using Core.EntityFrameworkCore.Repositories;
using E3A.Domain.Engineers;
using E3A.Infrastructure.Data.Context;

namespace E3A.Infrastructure.Engineers;

public class EngineerRepository(AppDbContext context) : Repository<Engineer>(context), IEngineerRepository
{
    public async Task<bool> IsSlugExistsAsync(string slug, CancellationToken cancellationToken)
    {
        var matchingSlugCount = await CountAsync(cancellationToken, x => x.Slug == slug).ConfigureAwait(false);
        return matchingSlugCount > 0;
    }
}
