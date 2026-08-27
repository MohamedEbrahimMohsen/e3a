using AppTemplate.Domain.Samples;
using AppTemplate.Infrastructure.Data.Context;
using Core.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Samples;

public class SampleRepository(AppDbContext context) : Repository<Sample>(context), ISampleRepository
{
    public async Task<Sample?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var sample = await context.Set<Sample>().AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, cancellationToken).ConfigureAwait(false);
        return sample;
    }
}
