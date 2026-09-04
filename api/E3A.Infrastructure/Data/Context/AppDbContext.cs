using E3A.Application.Options;
using E3A.Domain.Engineers;
using E3A.Domain.Identity;
using E3A.Domain.Publishing;
using E3A.Domain.Teams;
using Core.Auditing.Entities;
using Core.EntityFrameworkCore.Context;
using Core.Notifications.Entities;
using Core.OTP.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace E3A.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator, IOptions<EngineersOptions> engineersOptions, IOptions<TeamsOptions> teamsOptions, IOptions<PublishingOptions> publishingOptions, IOptions<GitHubAuthenticationOptions> gitHubAuthenticationOptions) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    // Enum-as-string columns share one width; not tunable — widening requires a migration anyway.
    private const int EnumColumnMaxLength = 50;

    // A SHA-256 hex digest is always exactly 64 characters; this is an invariant of the algorithm, not a cap.
    private const int Sha256HexLength = 64;

    public DbSet<Engineer> Engineers { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<ItemVersion> ItemVersions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureEngineers(modelBuilder);
        ConfigureTeams(modelBuilder);
        ConfigureItemVersions(modelBuilder);

        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var gitHubAuthenticationSchema = gitHubAuthenticationOptions.Value;

        modelBuilder.Entity<User>(builder =>
        {
            builder.Property(x => x.GitHubLogin).HasMaxLength(gitHubAuthenticationSchema.GitHubLoginMaxLength);
            builder.Property(x => x.DisplayName).HasMaxLength(gitHubAuthenticationSchema.DisplayNameMaxLength);
            builder.Property(x => x.AvatarUrl).HasMaxLength(gitHubAuthenticationSchema.AvatarUrlMaxLength);
            builder.HasIndex(x => x.GitHubId).IsUnique().HasFilter("[GitHubId] IS NOT NULL AND [IsDeleted] = 0");
        });
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

    private void ConfigureTeams(ModelBuilder modelBuilder)
    {
        var teamSchema = teamsOptions.Value;
        var publishingSchema = publishingOptions.Value;

        modelBuilder.Entity<Team>(builder =>
        {
            builder.Property(x => x.Slug).IsRequired().HasMaxLength(teamSchema.SlugMaxLength);
            builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.HasIndex(x => x.OwnerUserId);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(teamSchema.DisplayNameMaxLength);
            builder.Property(x => x.Description).HasMaxLength(teamSchema.DescriptionMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Tags)
                   .HasConversion(
                       v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                       v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>())
                   .HasMaxLength(teamSchema.TagsColumnMaxLength);
            builder.HasMany(x => x.Members).WithOne().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamMember>(builder =>
        {
            builder.Property(x => x.EngineerSlug).IsRequired().HasMaxLength(teamSchema.SlugMaxLength);
            builder.Property(x => x.PinnedSemanticVersion).IsRequired().HasMaxLength(publishingSchema.SemanticVersionMaxLength);
            builder.HasIndex(x => new { x.TeamId, x.EngineerId }).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.HasIndex(x => x.TeamId);
        });
    }

    private void ConfigureItemVersions(ModelBuilder modelBuilder)
    {
        var publishingSchema = publishingOptions.Value;

        modelBuilder.Entity<ItemVersion>(builder =>
        {
            builder.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(EnumColumnMaxLength);
            builder.Property(x => x.SemanticVersion).IsRequired().HasMaxLength(publishingSchema.SemanticVersionMaxLength);
            builder.Property(x => x.FrozenManifestJson).IsRequired();
            builder.Property(x => x.ZipBlobPath).HasMaxLength(publishingSchema.BlobPathMaxLength);
            builder.Property(x => x.ZipSha256).HasMaxLength(Sha256HexLength);
            builder.Property(x => x.FailureReason).HasMaxLength(publishingSchema.FailureReasonMaxLength);
            builder.Property(x => x.ScanReportJson);
            builder.HasIndex(x => new { x.ItemType, x.ItemId, x.VersionNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.HasIndex(x => x.ItemId);
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
        modelBuilder.Entity<Team>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<TeamMember>().HasQueryFilter(x => !x.IsDeleted);
        modelBuilder.Entity<ItemVersion>().HasQueryFilter(x => !x.IsDeleted);
    }
}
