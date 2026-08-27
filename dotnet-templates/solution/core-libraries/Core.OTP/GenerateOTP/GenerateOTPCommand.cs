using MediatR;

namespace Core.OTP.GenerateOTP;

public sealed record GenerateOTPCommand(string PhoneNumber) : IRequest<GenerateOTPResult>;