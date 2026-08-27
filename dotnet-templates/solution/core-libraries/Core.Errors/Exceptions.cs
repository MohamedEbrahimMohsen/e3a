using Core.Errors.Interfaces;
using System.Net;

namespace Core.Errors;


/// <summary>
/// HTTP 400 Bad Request.
/// </summary>
public class BadRequestCoreException(string errorCode = "BAD_REQUEST", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.BadRequest;
}

/// <summary>
/// User is not authenticated (no valid token).
/// HTTP 401 Unauthorized.
/// </summary>
public class UnauthorizedCoreException(string errorCode = "UNAUTHORIZED", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.Unauthorized;
}

/// <summary>
/// User is authenticated but lacks permission.
/// HTTP 403 Forbidden.
/// </summary>
public class ForbiddenCoreException(string errorCode = "FORBIDDEN", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.Forbidden;
}

/// <summary>
/// Entity not found in repository.
/// HTTP 404 Not Found.
/// </summary>
public class NotFoundCoreException(string errorCode = "NOT_FOUND", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.NotFound;
}

/// <summary>
/// Resource conflict such as duplicate names or concurrency violations.
/// HTTP 409 Conflict.
/// </summary>
public class ConflictCoreException(string errorCode = "CONFLICT", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.Conflict;
}

/// <summary>
/// Application-level validation failure (e.g. FluentValidation).
/// HTTP 422 Unprocessable Entity.
/// </summary>
public class ApplicationValidationCoreException(string errorCode = "VALIDATION_ERROR", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.UnprocessableEntity;
}

/// <summary>
/// Rate limit exceeded.
/// HTTP 429 Too Many Requests.
/// </summary>
public class RateLimitExceededCoreException(string errorCode = "RATE_LIMIT_EXCEEDED", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.TooManyRequests;
}

public class InternalServerErrorCoreException(string errorCode = "INTERNAL_SERVER_ERROR", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.InternalServerError;
}

public class BusinessRuleViolationCoreException(string errorCode = "BUSINESS_RULE_VIOLATION", string? message = null, Dictionary<string, object>? context = null, Exception? innerException = null) : BaseException(errorCode, message, context, innerException), IHasHttpStatus
{
    public int StatusCode => (int)HttpStatusCode.BadRequest;
}