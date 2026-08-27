namespace Core.Errors.Interfaces;

public interface IHasHttpStatus
{
    int StatusCode { get; }
}
