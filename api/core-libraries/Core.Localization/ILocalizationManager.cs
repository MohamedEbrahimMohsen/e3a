namespace Core.Localization;

public interface ILocalizationManager
{
    T GetLocalizedValue<T>(T valueAr, T valueEn);
}
