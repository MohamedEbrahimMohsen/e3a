namespace Core.Localization;

public interface ILocalizer
{
    string GetMessage(string? code, string? fallbackMessage = "", Dictionary<string, object>? context = null);
}