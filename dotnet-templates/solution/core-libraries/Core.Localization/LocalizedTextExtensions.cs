using Core.DDD.Models;
using System.Globalization;

namespace Core.Localization;

public static class LocalizedTextExtensions
{
    private static readonly string? _defaultLang = CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName;

    public static string Localized(this LocalizedText text)
    {
        var lang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        return lang switch
        {
            "en" => text?.English,
            "ar" => text?.Arabic,
            _ => _defaultLang == "ar" ? text.Arabic : text.English // always fallback to English if default language is not set or invalid.
        } ?? string.Empty;
    }
}
