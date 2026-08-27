namespace Core.OTP.GenerateOTP;

public sealed record GenerateOTPResult(Guid VerificationId, 
                                       DateTimeOffset ExpiresAt, 
                                       DateTimeOffset NextAllowedReissueAt, 
                                       int VerificationAttempts, 
                                       int ReissueCount, 
                                       int MaxVerificationAttempts, 
                                       int MaxReissueCount,
                                       string Code); // CODE HAS TO BE DELETED
