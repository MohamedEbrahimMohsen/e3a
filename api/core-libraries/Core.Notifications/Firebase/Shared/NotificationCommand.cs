namespace Core.Notifications.Firebase.Shared;

public sealed record NotificationCommand(string Title, string Body, string? DeepLink = null, string? ImageUrl = null, Dictionary<string, string>? Data = null);
public sealed record MultilingualNotificationCommand(string TitleAr, string TitleEn, string BodyAr, string BodyEn, string? DeepLink = null, string? ImageUrl = null, Dictionary<string, string>? Data = null)
{
    public Dictionary<string, string> ToData()
    {
        var data = new Dictionary<string, string>(Data ?? [])
        {
            ["title_ar"] = TitleAr,
            ["title_en"] = TitleEn,
            ["body_ar"] = BodyAr,
            ["body_en"] = BodyEn
        };

        if (!string.IsNullOrWhiteSpace(ImageUrl))
            data["image_url"] = ImageUrl;

        if (!string.IsNullOrWhiteSpace(DeepLink))
            data["deep_link"] = DeepLink;

        return data;
    }
}