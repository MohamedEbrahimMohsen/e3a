using Core.DDD.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace E3A.Domain.Identity;

public class User : IdentityUser<Guid>, IAuditEntity
{
    public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdationDate { get; set; } = DateTimeOffset.Now;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public long? GitHubId { get; private set; }
    public string? GitHubLogin { get; private set; }
    public string? DisplayName { get; private set; }
    public string? AvatarUrl { get; private set; }

    public User() { }
    private User(Guid id) : base()
    {
        Id = id;
        CreationDate = DateTimeOffset.UtcNow;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public static User Create(Guid? createdBy = null)
    {
        var id = Guid.NewGuid();

        return new User(id)
        {
            Id = id,
            CreatedBy = createdBy,
        };
    }


    public static User CreateFromGitHub(long gitHubId, string gitHubLogin, string userName, string? displayName, string? avatarUrl)
    {
        var id = Guid.NewGuid();

        return new User(id)
        {
            Id = id,
            GitHubId = gitHubId,
            GitHubLogin = gitHubLogin,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
    }

    public void UpdateGitHubProfile(string? displayName, string? avatarUrl)
    {
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
