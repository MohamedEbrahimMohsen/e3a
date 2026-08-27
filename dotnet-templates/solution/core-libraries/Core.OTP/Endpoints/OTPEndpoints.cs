using Core.OTP.GenerateOTP;
using Core.OTP.VerifyOTP;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Core.OTP.Endpoints;

public static class CoreOTPEndpoints
{
    public static RouteGroupBuilder MapCoreOTPEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/c/otp");

        group.MapPost("/send", async (GenerateOTPCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/resend", async (GenerateOTPCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(request, cancellationToken);
            return Results.Ok(result);
        });

        group.MapPost("/verify", async (VerifyOTPCommand request, IMediator mediator, CancellationToken cancellationToken) =>
        {
            var result = await mediator.Send(request, cancellationToken);
            return Results.Ok(result);
        });

        return group;
    }
}