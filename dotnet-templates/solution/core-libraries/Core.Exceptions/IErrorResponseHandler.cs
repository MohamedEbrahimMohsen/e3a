namespace Core.Exceptions;

public interface IErrorResponseHandler
{
    ErrorResponse GenerateErrorResponse(string code, string message);
    ErrorResponse<T> GenerateErrorResponse<T>(string code, string message, T data);
    ErrorResponse<string> GenerateErrorResponse(ExceptionDetails exceptionDetails);
}

