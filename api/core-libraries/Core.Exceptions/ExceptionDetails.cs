using System.Net;

namespace Core.Exceptions;

public class ExceptionDetails
{
    public string? Message { get; set; } = string.Empty;
    public string? Code { get; set; } = string.Empty;
    public int? StatusCode { get; set; } = (int)HttpStatusCode.InternalServerError;
    public Exception? Exception { get; set; } = null!;
    public Dictionary<string, object>? Context { get; set; } = null;
}
