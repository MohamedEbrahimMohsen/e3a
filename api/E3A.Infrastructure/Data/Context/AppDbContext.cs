using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using Core.Auditing.Entities;
using Core.EntityFrameworkCore.Context;
using Core.Notifications.Entities;
using Core.OTP.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace E3A.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator, IOptions<EngineersOptions> engineersOptions) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    // Enum-as-string columns share one width; not tunable — widening requires a migration anyway.
    private const int EnumColumnMaxLength = 50;

    public DbSet<Engineer> Engineers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureEngineers(modelBuilder);

        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private void ConfigureEngineers(ModelBuilder modelBuilder)
    {
        var engineerSchema = engineersOptions.Value;

        modelBuilder.Entity<Engineer>(builder =>
        {
            builder.Property(x => x.Slug).IsRequired().HasMaxLength(engineerSchema.SlugMaxLength);
            builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.HasIndex(x => x.OwnerUserId);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(engineerSchema.DisplayNameMaxLength);
            builder.Property(x => x.Description).HasMaxLength(engineerSchema.DescriptionMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Tags)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                       v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
                   .HasMaxLength(engineerSchema.TagsColumnMaxLength);
        });
    }

    /// <summary>
    /// Every soft-deletable entity is registered here. Filtering deleted rows
    /// inside a query instead means this registration is missing — fix it here.
    /// </summary>
    private static void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<Engineer>().HasQueryFilter(x => !x.IsDeleted);
    }
}
