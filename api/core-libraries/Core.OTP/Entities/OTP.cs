using Core.DDD.Entities;
using Core.Errors;
using Core.OTP.Exceptions;

namespace Core.OTP.Entities;

public class Otp : Entity
{
    public Guid VerificationId { get; private set; }
    public string PhoneNumber { get; private set; }
    public string CodeHash { get; private set; }
    
    public string? RequestIP { get; private set; }
    public string? UserAgent { get; private set; }

    public int VerificationAttempts { get; private set; }
    public int MaxVerificationAttempts { get; private set; }

    public int ReissueCount { get; private set; }
    public int MaxReissueCount { get; private set; }
    public int ReissueCooldownSeconds { get; private set; }
    public int ReissueBlockCooldownInHours { get; private set; }
    public DateTimeOffset NextAllowedReissueAt { get; private set; }

    public bool IsVerified { get; private set; }
    public bool IsUsed { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; private set; }
    private Otp(Guid id): base(id) { }

    public static Otp Create(string phoneNumber, string codeHash, int expiresInMinutes, int maxVerificationAttempts, int reissueCooldownSeconds, int maxReissueCount, int reissueBlockCooldownInHours)
    {
        var id = Guid.NewGuid();
        return new Otp(id)
        {
            VerificationId = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            CodeHash = codeHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes),
            NextAllowedReissueAt = DateTimeOffset.UtcNow.AddSeconds(reissueCooldownSeconds),
            ReissueCooldownSeconds = reissueCooldownSeconds,
            VerificationAttempts = 0,
            MaxVerificationAttempts = maxVerificationAttempts,
            ReissueCount = 0,
            MaxReissueCount = maxReissueCount,
            ReissueBlockCooldownInHours = reissueBlockCooldownInHours,
            IsVerified = false
        };
    }

    public void Reissue(string newCodeHash, int expiresInMinutes)
    {
        var now = DateTimeOffset.UtcNow;

        if (CreatedAt.AddDays(1) <= DateTimeOffset.UtcNow)
        {
            ReissueCount = 0;
        }

        if (now < NextAllowedReissueAt)
        {
            var cooldown = NextAllowedReissueAt - now;
            throw new BaseException(ErrorCodes.OTPReissueCooldown, context: new Dictionary<string, object>
            {
                ["days"] = cooldown.Days,
                ["hours"] = cooldown.Hours,
                ["minutes"] = cooldown.Minutes,
                ["seconds"] = cooldown.Seconds
            });
        }

        if (ReissueCount > MaxReissueCount)
        {
            throw new BaseException(ErrorCodes.OTPReachedMaxReissueCount);
        }

        VerificationId = Guid.NewGuid();
        CodeHash = newCodeHash;
        IsVerified = false;
        IsUsed = false;
        VerificationAttempts = 0;
        ReissueCount++;
        CreatedAt = now;
        ExpiresAt = now.AddMinutes(expiresInMinutes);
        NextAllowedReissueAt = ReissueCount == MaxReissueCount? NextAllowedReissueAt.AddHours(ReissueBlockCooldownInHours) : now.AddSeconds(ReissueCooldownSeconds);
    }

    public string? Verify(string codeHash)
    {
        VerificationAttempts++;

        if (IsVerified)
        {
            return ErrorCodes.OTPAlreadyVerified;
        }

        if (ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ErrorCodes.OTPExpired;
        }

        if (VerificationAttempts > MaxVerificationAttempts)
        {
            return ErrorCodes.OTPReachedMaxAttempts;
        }

        if (CodeHash != codeHash)
        {
            return ErrorCodes.OTPNotMatched;
        }

        IsVerified = true;
        VerificationAttempts = 0;
        return null;
    }

    public void MarkUsed()
    {
        if (!IsVerified)
        {
            throw new BaseException(ErrorCodes.OTPNotVerified);
        }

        if (IsUsed)
        {
            throw new BaseException(ErrorCodes.OTPAlreadyUsed);
        }

        if (ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new BaseException(ErrorCodes.OTPExpired);
        }

        IsUsed = true;
    }
}
