//using Core.Identity.Register;
//using Core.Identity.Requests;
//using MediatR;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Routing;

//namespace Core.Identity;

//public static class CoreAuthEndpoints
//{
//    public static IEndpointRouteBuilder MapCoreAuthEndpoints<TUser>(this IEndpointRouteBuilder endpoints) where TUser : IdentityUser, new()
//    {
//        endpoints.MapPost("/c/auth/register", Register<TUser>);
//        return endpoints;
//    }

//    private static async Task<IResult> Register<TUser>(RegisterRequest request, IMediator mediator, CancellationToken ct) where TUser : IdentityUser, new()
//    {
//        var identityUser = new TUser()
//        {
//            UserName = request.UserName,
//            PhoneNumber = request.PhoneNumber,
//            Email = request.Email
//        };

//        var command = new RegisterCommand<TUser>(identityUser, request.Password);
//        return Results.Ok(await mediator.Send(command, ct));
//    }
//}


////public static class CoreAuthEndpoints2
////{
////    public static IEndpointRouteBuilder MapCoreAuthEndpoints<TUser>(this IEndpointRouteBuilder endpoints) where TUser : IdentityUser, new()
////    {
////        //endpoints.MapPost("/c/auth/login", LoginHandler);
////        //endpoints.MapPost("/c/auth/register", RegisterHandler<TUser>);
////        //endpoints.MapPost("/c/auth/forgot-password", ForgotPasswordHandler
////        return endpoints;
////    }
////}
