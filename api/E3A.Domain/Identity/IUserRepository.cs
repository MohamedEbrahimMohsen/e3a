using Core.DDD.Repositories;

namespace E3A.Domain.Identity;

public interface IUserRepository : IRepository<User>
{
    Task<bool> IsUserNameExistsAsync(string normalizedUserName, CancellationToken cancellationToken);
}
