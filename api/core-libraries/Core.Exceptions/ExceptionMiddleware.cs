using Core.Errors;
using Core.Errors.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Core.Exceptions;

public class CoreExceptionMiddleware(RequestDelegate next, ILogger<CoreExceptionMiddleware> logger)
{
    // MVC serialises successful responses with JsonSerializerDefaults.Web (camelCase). Serialising
    // errors with the bare default would emit PascalCase, so a client reading `code` off a failure
    // body would silently get undefined on every error.
    private static readonly JsonSerializerOptions ErrorSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = (exception as IHasHttpStatus)?.StatusCode ?? (int)HttpStatusCode.InternalServerError;

        var exceptionDetails = exception is BaseException ex
            ? new ExceptionDetails
            {
                Message = ex.Message,
                Code = (exception as IHasMaskedCode)?.MaskedCode ?? ex.ErrorCode,
                StatusCode = statusCode,
                Exception = ex,
                Context = ex.Context
            }
            : new ExceptionDetails
            {
                Message = exception.Message,
                Code = ExceptionErrorCodes.UnhandledException,
                StatusCode = statusCode,
                Exception = exception,
            };

        // A 4xx is an expected outcome the caller asked for, not an application fault. Logging it at
        // Error with a stack trace makes normal flow (a draft with no upload yet, an unauthenticated
        // probe) indistinguishable from a real failure, and buries the 5xx that actually need eyes.
        var isClientError = exceptionDetails.StatusCode is >= 400 and < 500;

        if (isClientError)
        {
            logger.LogWarning($"StatusCode: {exceptionDetails.StatusCode}, ErrorCode: {exceptionDetails.Code}, ErrorMessage: {exceptionDetails.Message}");
        }
        else
        {
            logger.LogError(exception, $"StatusCode: {exceptionDetails.StatusCode}, ErrorCode: {exceptionDetails.Code}, ErrorMessage: {exceptionDetails.Message}");
        }

        context.Items["ErrorCode"] = exceptionDetails.Code;
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var apiResponseHandler = context.RequestServices.GetRequiredService<IErrorResponseHandler>();
        var response = apiResponseHandler.GenerateErrorResponse(exceptionDetails);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, ErrorSerializerOptions));
    }
}
