namespace Core.Notifications.Exceptions;

public static class ErrorCodes
{   
    public const string UserIdRequired = "USER_ID_REQUIRED";
    public const string UserDeviceIdRequired = "USER_DEVICE_ID_REQUIRED";
    public const string PushTokenRequired = "PUSH_TOKEN_REQUIRED";
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string DevicePlatformRequired = "DEVICE_PLATFORM_REQUIRED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string NotificationIdRequired = "NOTIFICATION_ID_REQUIRED";
    public const string NotificationNotFound = "NOTIFICATION_NOT_FOUND";
    public const string OnlyDirectNotificationCanBeMarked = "ONLY_DIRECT_NOTIFICATION_CAN_BE_MARKED";
    public const string NotificationTopicRequired = "NOTIFICATION_TOPIC_REQUIRED";
    public const string NotificationTitleArRequired = "NOTIFICATION_TITLE_AR_REQUIRED";
    public const string NotificationTitleEnRequired = "NOTIFICATION_TITLE_EN_REQUIRED";
    public const string NotificationBodyArRequired = "NOTIFICATION_BODY_AR_REQUIRED";
    public const string NotificationBodyEnRequired = "NOTIFICATION_BODY_EN_REQUIRED";
    public const string NotificationTitleRequired = "NOTIFICATION_TITLE_REQUIRED";
    public const string NotificationBodyRequired = "NOTIFICATION_BODY_REQUIRED";
    public const string NotificationUserIdsRequired = "NOTIFICATION_USER_IDS_REQUIRED";
    public const string NotificationTemplateCodeAlreadyExist = "NOTIFICATION_TEMPLATE_CODE_ALREADY_EXIST";
    public const string NotificationTemplateCodeRequired = "NOTIFICATION_TEMPLATE_CODE_REQUIRED";
    public const string NotificationTemplateIdRequired = "NOTIFICATION_TEMPLATE_ID_REQUIRED";
    public const string NotificationTemplateNotFound = "NOTIFICATION_TEMPLATE_NOT_FOUND";
    public const string NotificationTemplateSystemReservedCannotDeleted = "NOTIFICATION_TEMPLATE_SYSTEM_RESERVED_CANNOT_DELETED";
    public const string NotificationTemplateTitleArabicRequired = "NOTIFICATION_TEMPLATE_TITLE_ARABIC_REQUIRED";
    public const string NotificationTemplateTitleEnglishRequired = "NOTIFICATION_TEMPLATE_TITLE_ENGLISH_REQUIRED";
    public const string NotificationTemplateContentArabicRequired = "NOTIFICATION_TEMPLATE_CONTENT_ARABIC_REQUIRED";
    public const string NotificationTemplateContentEnglishRequired = "NOTIFICATION_TEMPLATE_CONTENT_ENGLISH_REQUIRED";
    public const string NotificationTemplateDeepLinkRequired = "NOTIFICATION_TEMPLATE_DEEP_LINK_REQUIRED";
    public const string NotificationTemplateTitleRequired = "NOTIFICATION_TEMPLATE_TITLE_REQUIRED";
    public const string NotificationTemplateContentRequired = "NOTIFICATION_TEMPLATE_CONTENT_REQUIRED";
    public const string FirebaseServiceAccountJsonNotFound = "FIREBASE_SERVICE_ACCOUNT_JSON_NOT_FOUND";





}
