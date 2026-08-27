using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace Core.OTP.OtpHasher;

public class HmacOtpHasher(IOptions<OtpOptions> options) : IOtpHasher
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Secret);

    public string Hash(string otp)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(otp));
        return Convert.ToBase64String(hash);
    }
}
