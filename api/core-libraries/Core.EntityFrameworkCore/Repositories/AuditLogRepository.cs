using Core.Auditing.Entities;
using Core.Auditing.Repositories;
using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using Microsoft.AspNetCore.Identity;

namespace Core.EntityFrameworkCore.Repositories;

public class AuditLogRepository<TUser, TRole, TKey, TContext>(TContext context) : Repository<AuditLog>(context), IAuditLogRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
}