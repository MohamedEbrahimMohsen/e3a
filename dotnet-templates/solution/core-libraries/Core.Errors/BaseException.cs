namespace Core.Errors;

/// <summary>
/// Base exception for all domain-level business rule violations.
/// Thrown when an operation violates a domain invariant or rule.
/// </summary>
public class BaseException(string? errorCode = null, string? message = null, Dictionary<string, object>? context = null, Exception ? innerException = null) : Exception(message, innerException)
{
    public string? ErrorCode { get; private set; } = errorCode;
    public Dictionary<string, object>? Context { get; private set; } = context;
}