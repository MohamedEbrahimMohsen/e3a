namespace Core.Exceptions;

public class ErrorResponse<T> : ErrorResponse
{
    public T? Data { get; set; }
}

public class ErrorResponse
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}