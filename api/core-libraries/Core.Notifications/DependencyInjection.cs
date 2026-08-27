using Core.Errors;
using Core.Notifications.Exceptions;
using Core.Notifications.Services;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using FluentValidation;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Core.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        var firebaseJson = configuration["CoreFirebaseServiceAccountJson"]
            ?? throw new InternalServerErrorCoreException(ErrorCodes.FirebaseServiceAccountJsonNotFound);

        services.AddSingleton(_ =>
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var credential = CredentialFactory.FromJson<ServiceAccountCredential>(firebaseJson)
                                                  .ToGoogleCredential();

                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential
                });
            }

            return FirebaseMessaging.DefaultInstance;
        });

        services.AddSingleton<IFirebaseNotificationService, FirebaseNotificationService>();
        return services;
    }
}