using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace Core.Localization;

public sealed class LocalizationManager(IConfiguration configuration) : ILocalizationManager
{
    private readonly string? _defaultLanguage = configuration["Localization:DefaultLanguage"]?.ToLowerInvariant();

    public T GetLocalizedValue<T>(T valueAr, T valueEn)
    {
        var currentLang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        if (currentLang == "en")
            return valueEn;

        if (currentLang == "ar")
            return valueAr;

        // If the current language is not supported, use the default language, always fallback to English if default language is not set or invalid.
        return _defaultLanguage == "ar" ? valueAr : valueEn;
    }
}