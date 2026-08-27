using Core.Auditing.Repositories;
using Core.DDD.Entities;
using Core.DDD.Repositories;
using Core.EntityFrameworkCore.Context;
using Core.EntityFrameworkCore.Repositories;
using Core.Notifications.Repositories;
using Core.OTP.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Core.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreEntityFrameworkCore<TUser, TRole, TKey, TContext>(this IServiceCollection services)
        where TUser : IdentityUser<TKey>, IEntity, new()
        where TRole : IdentityRole<TKey>, new()
        where TKey : IEquatable<TKey>, new()
        where TContext : CoreDbContext<TUser, TRole, TKey>
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IOtpRepository, OtpRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<INotificationRepository, NotificationRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository<TUser, TRole, TKey, TContext>>();

        return services;
    }
}