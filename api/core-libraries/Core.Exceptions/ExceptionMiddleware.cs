using Core.Errors;
using Core.Errors.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Core.Exceptions;

public class CoreExceptionMiddleware(RequestDelegate next, ILogger<CoreExceptionMiddleware> logger)
{
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

        logger.LogError(exception, $"StatusCode: {exceptionDetails.StatusCode}, ErrorCode: {exceptionDetails.Code}, ErrorMessage: {exceptionDetails.Message}");

        context.Items["ErrorCode"] = exceptionDetails.Code;
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var apiResponseHandler = context.RequestServices.GetRequiredService<IErrorResponseHandler>();
        var response = apiResponseHandler.GenerateErrorResponse(exceptionDetails);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
