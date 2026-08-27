using Core.OTP.Entities;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using Core.Utilities.Generator;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.OTP.GenerateOTP;

public sealed class GenerateOTPHandler(IOtpRepository otpRepository, IGenerator generator, IOtpHasher otpHasher, IOptions<OtpOptions> otpOptions) : IRequestHandler<GenerateOTPCommand, GenerateOTPResult>
{
    private readonly OtpOptions _otpOptions = otpOptions.Value;
    public async Task<GenerateOTPResult> Handle(GenerateOTPCommand request, CancellationToken cancellationToken)
    {
        var code = generator.Generate(size: _otpOptions.OtpLength, allowedCharacters: _otpOptions.AllowedCharacters);
        var otp = await otpRepository.FindAsync(request.PhoneNumber, null!, cancellationToken);
        var codeHash = otpHasher.Hash(code);

        if (otp is not null)
        {
            otp.Reissue(codeHash, _otpOptions.ExpirationMinutes);
        }
        else
        {
            otp = Otp.Create(phoneNumber: request.PhoneNumber,
                             codeHash: codeHash,
                             expiresInMinutes: _otpOptions.ExpirationMinutes,
                             maxVerificationAttempts: _otpOptions.MaxVerificationAttempts,
                             reissueCooldownSeconds: _otpOptions.ReissueCooldownSeconds,
                             maxReissueCount: _otpOptions.MaxReissueCount,
                             reissueBlockCooldownInHours: _otpOptions.ReissueBlockCooldownInHours);
            await otpRepository.AddAsync(otp, cancellationToken).ConfigureAwait(false);
        }

        await otpRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new GenerateOTPResult(VerificationId: otp.VerificationId, 
                                     ExpiresAt: otp.ExpiresAt, 
                                     NextAllowedReissueAt: otp.NextAllowedReissueAt, 
                                     VerificationAttempts: otp.VerificationAttempts,
                                     ReissueCount: otp.ReissueCount,
                                     MaxVerificationAttempts: otp.MaxVerificationAttempts,
                                     MaxReissueCount: otp.MaxReissueCount,
                                     Code: code);
    }
}
