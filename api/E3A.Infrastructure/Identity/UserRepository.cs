using Core.EntityFrameworkCore.Repositories;
using E3A.Domain.Identity;
using E3A.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace E3A.Infrastructure.Identity;

public class UserRepository(AppDbContext context) : Repository<User>(context), IUserRepository
{
    public async Task<bool> IsUserNameExistsAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        // UserNameIndex is unique but not filtered on IsDeleted, so a soft-deleted row still holds the name.
        return await _dbSet.IgnoreQueryFilters().AnyAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken).ConfigureAwait(false);
    }
}
