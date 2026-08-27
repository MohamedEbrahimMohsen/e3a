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


    public void MarkDeleted()
    {
        IsDeleted = true;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
