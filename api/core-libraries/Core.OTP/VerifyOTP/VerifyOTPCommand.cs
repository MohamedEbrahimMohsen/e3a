using MediatR;

namespace Core.OTP.VerifyOTP;

public sealed record VerifyOTPCommand(string Code, Guid VerificationId) : IRequest<VerifyOTPResult>;
