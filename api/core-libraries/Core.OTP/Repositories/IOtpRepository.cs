using Core.DDD.Repositories;
using Core.OTP.Entities;

namespace Core.OTP.Repositories;

public interface IOtpRepository : IRepository<Otp>
{
    Task<Otp?> FindAsync(string phoneNumber, string? requestIP, CancellationToken cancellationToken);
    Task<Otp?> FindByVerificationId(Guid verificationId, CancellationToken cancellationToken);
}
