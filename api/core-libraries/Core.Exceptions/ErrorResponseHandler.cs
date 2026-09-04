using Core.Localization;
using Microsoft.Extensions.Hosting;

namespace Core.Exceptions;

public sealed class ErrorResponseHandler(ILocalizer localizer, IHostEnvironment environment) : IErrorResponseHandler
{
    public ErrorResponse GenerateErrorResponse(string code, string message)
    {
        return new ErrorResponse
        {
            Code = code,
            Message = localizer.GetMessage(code, message)
        };
    }
    public ErrorResponse<T> GenerateErrorResponse<T>(string code, string message, T data)
    {
        return new ErrorResponse<T>
        {
            Code = code,
            Message = localizer.GetMessage(code, message),
            Data = data
        };
    }
    public ErrorResponse<string> GenerateErrorResponse(ExceptionDetails exceptionDetails)
    {
        ArgumentNullException.ThrowIfNull(exceptionDetails);

        return new ErrorResponse<string>
        {
            Code = exceptionDetails.Code,
            Message = localizer.GetMessage(exceptionDetails.Code, exceptionDetails.Message, exceptionDetails.Context),
            Data = GenerateDiagnosticData(exceptionDetails.Exception)
        };
    }

    // Exception message and stack trace expose absolute source paths and internal call structure.
    // Outside Development they belong in the log only - the 5xx branch of CoreExceptionMiddleware
    // already logs the full exception - never in a body an anonymous client can read.
    private string? GenerateDiagnosticData(Exception? exception)
    {
        if (!environment.IsDevelopment())
        {
            return null;
        }

        return $"{exception?.Message} - {exception?.StackTrace}";
    }
}