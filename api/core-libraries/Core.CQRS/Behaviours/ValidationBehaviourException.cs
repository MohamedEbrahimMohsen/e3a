using Core.Errors;
using Core.Errors.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Core.CQRS.Behaviours;

public class ValidationBehaviourException(string errorCode = "VALIDATION_FAILED", List<string> errorMessages = null!, Dictionary<string, object>? context = null)
    : BaseException(errorCode, string.Join(" , ", errorMessages), context), IHasHttpStatus
{
    public ValidationBehaviourException(List<string> errorCodes = null!, List<string> errorMessages = null!, Dictionary<string, object>? context = null)
        : this(string.Join(',', errorCodes), errorMessages, context) { }
    public int StatusCode => StatusCodes.Status422UnprocessableEntity;
}
