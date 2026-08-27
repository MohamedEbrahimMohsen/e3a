using Core.Errors;
using Core.Errors.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Core.EntityFrameworkCore.Exceptions;

/// <summary>
/// Represents an infrastructure-level error such as repository misuse,
/// mapping misconfiguration, data store failures, or service unavailability.
/// Not intended to be exposed to clients; always mapped to 500 Internal Server Error.
/// </summary>
public class InfrastructureCoreException(InfrastructureErrorCode errorCode, string message = null!, Dictionary<string, object>? context = null) : BaseException(errorCode.Code, message, context), IHasHttpStatus, IHasMaskedCode
{
    public int StatusCode => StatusCodes.Status500InternalServerError;
    public string MaskedCode { get; set; } = errorCode.MaskedCode;
}