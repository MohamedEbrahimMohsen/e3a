using Core.Errors;

namespace Core.DDD.Models;

public record LocalizedText
{
    public string Arabic { get; }
    public string English { get; }

    public LocalizedText(string arabic, string english)
    {
        if (string.IsNullOrEmpty(arabic))
        {
            throw new BaseException("LOCALIZED_TEXT_ARABIC_REQUIRED");
        }

        if (string.IsNullOrEmpty(english))
        {
            throw new BaseException("LOCALIZED_TEXT_ENGLISH_REQUIRED");
        }

        Arabic = arabic;
        English = english;
    }
}
