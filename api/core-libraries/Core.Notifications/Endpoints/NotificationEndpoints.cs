using Core.Notifications.Firebase.MultilingualSendToAll;
using Core.Notifications.Firebase.MultilingualSendToTopic;
using Core.Notifications.Firebase.MultilingualSendToUsers;
using Core.Notifications.Firebase.SendToAll;
using Core.Notifications.Firebase.SendToTopic;
using Core.Notifications.Firebase.SendToUsers;
using Core.Notifications.Firebase.SubscribeToTopic;
using Core.Notifications.Firebase.UnsubscribeFromTopic;
using Core.Notifications.ListNotifications;
using Core.Notifications.MarkNotificationAsRead;
using Core.Notifications.RegisterUserDevice;
using Core.Notifications.Services;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Core.Notifications.Endpoints;

public static class CoreNotificationEndpoints
{
    public static RouteGroupBuilder MapCoreFirebaseNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/c/notification");

        group.MapPost("/send/topic", async (SendToTopicCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/user", async (SendToUserCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(new SendToUsersCommand([request.UserId], request.Notification), cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/users", async (SendToUsersCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/all", async (SendToAllCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/multilingual/topic", async (MultilingualSendToTopicCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/multilingual/user", async (MultilingualSendToUserCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(new MultilingualSendToUsersCommand([request.UserId], request.Notification), cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/multilingual/users", async (MultilingualSendToUsersCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/send/multilingual/all", async (MultilingualSendToAllCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        group.MapPost("/subscribe/topic", async (SubscribeToTopicCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(request, cancellationToken);
            return Results.Ok();
        });

        group.MapPost("/unsubscribe/topic", async (UnsubscribeFromTopicCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(request, cancellationToken);
            return Results.Ok();
        });

        return group;
    }

    public static RouteGroupBuilder MapCoreDevicesNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/c/notification");

        group.MapPost("/devices/register", async (RegisterUserDeviceCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            await mediator.Send(request, cancellationToken);
            return Results.Ok();
        });

        return group;
    }

    public static RouteGroupBuilder MapCoreUserNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/c/notification");

        group.MapPost("/me/{id}", async (Guid id, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var isFound =  await mediator.Send(new MarkNotificationAsReadCommand(id), cancellationToken);
            return isFound? Results.Ok() : Results.NoContent();
        });

        group.MapGet("/me", async ([AsParameters] ListNotificationsQuery request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var results = await mediator.Send(request, cancellationToken);
            return Results.Ok(results);
        });

        return group;
    }
}