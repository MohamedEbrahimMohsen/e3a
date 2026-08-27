using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Repositories;

public class UserDeviceRepository<TUser, TRole, TKey, TContext>(TContext context) : Repository<UserDevice>(context), IUserDeviceRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
    public async Task<List<string>> GetTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await context.UserDevices.Where(ud => ud.UserId == userId && !ud.IsDeleted)
                                        .Select(ud => ud.PushToken)
                                        .ToListAsync(cancellationToken)
                                        .ConfigureAwait(false);
    }
    public async Task<List<string>> GetTokensByUserIdsAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        return await context.UserDevices.Where(ud => ud.UserId != null && userIds.Contains(ud.UserId.Value) && !ud.IsDeleted)
                                        .Select(ud => ud.PushToken)
                                        .ToListAsync(cancellationToken)
                                        .ConfigureAwait(false);
    }
}