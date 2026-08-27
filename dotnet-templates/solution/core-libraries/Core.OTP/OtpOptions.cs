namespace Core.OTP;

public class OtpOptions
{
    public const string SectionName = "CoreOtp";

    public string Secret { get; set; } = default!;
    public int OtpLength { get; init; } = 6;
    public List<string> PhoneCodes { get; set; } = ["010", "011", "012"];
    public int PhoneLength { get; set; } = 11;
    public string AllowedCharacters { get; init; } = "0123456789";
    public int ExpirationMinutes { get; init; } = 5;
    public int MaxVerificationAttempts { get; init; } = 3;
    public int ReissueCooldownSeconds { get; init; } = 60;
    public int MaxReissueCount { get; init; } = 5;
    public int ReissueBlockCooldownInHours { get; init; } = 24;
}