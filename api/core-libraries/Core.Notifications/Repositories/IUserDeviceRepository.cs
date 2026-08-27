using Core.DDD.Repositories;
using Core.Notifications.Entities;

namespace Core.Notifications.Repositories;

public interface IUserDeviceRepository : IRepository<UserDevice>
{
    Task<List<string>> GetTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<string>> GetTokensByUserIdsAsync(List<Guid> userIds, CancellationToken cancellationToken);
}
