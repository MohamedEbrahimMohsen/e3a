using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Core.EntityFrameworkCore.Repositories;

public class NotificationTemplateRepository<TUser, TRole, TKey, TContext>(TContext context) : Repository<NotificationTemplate>(context), INotificationTemplateRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
    public async Task<NotificationTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        return await FirstOrDefaultAsync(x => x.Code.ToLower() == code.ToLower(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsCodeExists(string code, CancellationToken cancellationToken)
    {
        var isCodeExists = (await FirstOrDefaultAsync(x => x.Code.ToLower() == code.ToLower(), cancellationToken).ConfigureAwait(false)) != null;
        return isCodeExists;
    }
}