using Core.Auditing.Entities;
using Core.DDD.Repositories;

namespace Core.Auditing.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
}