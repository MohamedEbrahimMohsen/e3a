namespace Core.OTP.OtpHasher;

public interface IOtpHasher
{
    string Hash(string otp);
}
