using Microsoft.Extensions.Localization;
using System.Reflection;

namespace Core.Localization;

public class Localizer(IStringLocalizerFactory factory) : ILocalizer
{
    private readonly IStringLocalizer _localizer = factory.Create("Messages", Assembly.GetEntryAssembly()!.GetName().Name!);

    public string GetMessage(string? code, string? fallbackMessage = "", Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return fallbackMessage ?? string.Empty;

        var localized = _localizer[code];
        var message = (localized.ResourceNotFound ? fallbackMessage : localized.Value) ?? string.Empty;

        if (context != null)
        {
            foreach (var kvp in context)
            {
                message = message.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString());
            }
        }
        return message;
    }
}