using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using Core.OTP.Entities;
using Core.OTP.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Repositories;

public class OtpRepository<TUser, TRole, TKey, TContext>(TContext context) : Repository<Otp>(context), IOtpRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
    public async Task<Otp?> FindAsync(string phoneNumber, string? requestIP, CancellationToken cancellationToken)
    {
        return await _dbSet.FirstOrDefaultAsync(otp => otp.PhoneNumber == phoneNumber && otp.RequestIP == requestIP, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Otp?> FindByVerificationId(Guid verificationId, CancellationToken cancellationToken)
    {
        return await _dbSet.FirstOrDefaultAsync(otp => otp.VerificationId == verificationId, cancellationToken).ConfigureAwait(false);
    }
}

