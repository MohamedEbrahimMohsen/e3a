using System.Text.Json.Serialization;

namespace Core.Exceptions;

public class ErrorResponse<T> : ErrorResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public T? Data { get; set; }
}

public class ErrorResponse
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}