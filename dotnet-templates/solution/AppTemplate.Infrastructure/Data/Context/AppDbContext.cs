using AppTemplate.Domain.Identity;
using Core.Auditing.Entities;
using Core.EntityFrameworkCore.Context;
using Core.Notifications.Entities;
using Core.OTP.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    /// <summary>
    /// Every soft-deletable entity is registered here. Filtering deleted rows
    /// inside a query instead means this registration is missing — fix it here.
    /// </summary>
    private static void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
    }
}
