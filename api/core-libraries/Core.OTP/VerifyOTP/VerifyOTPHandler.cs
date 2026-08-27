using Core.Errors;
using Core.OTP.Exceptions;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using MediatR;

namespace Core.OTP.VerifyOTP;

public sealed class VerifyOTPHandler(IOtpRepository otpRepository, IOtpHasher otpHasher) : IRequestHandler<VerifyOTPCommand, VerifyOTPResult>
{
    public async Task<VerifyOTPResult> Handle(VerifyOTPCommand request,CancellationToken cancellationToken)
    {
        var codeHash = otpHasher.Hash(request.Code);
        var otp = await otpRepository.FindByVerificationId(request.VerificationId, cancellationToken).ConfigureAwait(false);
        
        if (otp == null)
        {
            throw new BaseException(ErrorCodes.OtpInvalid);
        }

        var errorCode = otp.Verify(codeHash);
        await otpRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        if(!string.IsNullOrEmpty(errorCode))
        {
            throw new BaseException(errorCode);
        }

        return new VerifyOTPResult();
    }
}
