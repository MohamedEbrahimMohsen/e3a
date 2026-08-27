using Core.DDD.Repositories;
using Core.Notifications.Entities;

namespace Core.Notifications.Repositories;

public interface INotificationTemplateRepository : IRepository<NotificationTemplate>
{
    Task<bool> IsCodeExists(string code, CancellationToken cancellationToken);
    Task<NotificationTemplate?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}
