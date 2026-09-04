using Core.Errors;
using Core.Exceptions;
using System.Net;

namespace E3A.Tests.CoreExceptions.Shared;

public static class ExceptionDetailsFactory
{
    public const string Code = "TEST_ERROR_CODE";

    public static ExceptionDetails Thrown()
    {
        try
        {
            throw new NotFoundCoreException(Code);
        }
        catch (NotFoundCoreException exception)
        {
            return new ExceptionDetails
            {
                Code = Code,
                Message = exception.Message,
                StatusCode = (int)HttpStatusCode.NotFound,
                Exception = exception
            };
        }
    }
}
