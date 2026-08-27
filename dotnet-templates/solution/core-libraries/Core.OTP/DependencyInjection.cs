using Core.OTP.GenerateOTP;
using Core.OTP.OtpHasher;
using Core.OTP.VerifyOTP;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Core.OTP;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreOtp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.AddScoped<IOtpHasher, HmacOtpHasher>();
        services.AddValidatorsFromAssemblyContaining<VerifyOTPValidator>();
        services.AddValidatorsFromAssemblyContaining<GenerateOTPValidator>();

        return services;
    }
}