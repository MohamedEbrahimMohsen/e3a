using Core.Auditing.Entities;
using Core.Notifications.Entities;
using Core.OTP.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Context;

public interface ICoreDbContext
{
    DbSet<UserDevice> UserDevices { get; set; }
    DbSet<Otp> Otps { get; set; }
    DbSet<Notification> Notifications { get; set; }
    DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    DbSet<AuditLog> AuditLogs { get; set; }
}
