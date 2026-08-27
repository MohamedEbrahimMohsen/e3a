using Core.DDD.Repositories;
using Core.Notifications.Entities;

namespace Core.Notifications.Repositories;

public interface INotificationRepository : IRepository<Notification>
{
}
