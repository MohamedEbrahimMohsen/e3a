namespace Core.Validation;

public static class ValidationErrors
{
    public const string ValidationRequired = "VALIDATION_REQUIRED";

    public const string ValidationMaxLength = "VALIDATION_MAX_LENGTH";
    public const string ValidationMinLength = "VALIDATION_MIN_LENGTH";
    public const string ValidationListMaxItems = "VALIDATION_LIST_MAX_ITEMS";

    public const string ValidationPasswordUppercase = "VALIDATION_PASSWORD_UPPERCASE";
    public const string ValidationPasswordLowercase = "VALIDATION_PASSWORD_LOWERCASE";
    public const string ValidationPasswordNumber = "VALIDATION_PASSWORD_NUMBER";
    public const string ValidationPasswordSpecialCharacter = "VALIDATION_PASSWORD_SPECIAL_CHARACTER";

    public const string ValidationEmail = "VALIDATION_EMAIL";
    public const string ValidationUrl = "VALIDATION_URL";

    public const string ValidationInvalidCharacters = "VALIDATION_INVALID_CHARACTERS";
    public const string ValidationOnlyDigits = "VALIDATION_ONLY_DIGITS";

    public const string ValidationPositive = "VALIDATION_POSITIVE";
    public const string ValidationNonNegative = "VALIDATION_NON_NEGATIVE";
    public const string ValidationMinValue = "VALIDATION_MIN_VALUE";
    public const string ValidationMaxValue = "VALIDATION_MAX_VALUE";
    public const string ValidationRange = "VALIDATION_RANGE";

    public const string ValidationFileSize = "VALIDATION_FILE_SIZE";
    public const string ValidationAllowedExtensions = "VALIDATION_ALLOWED_EXTENSIONS";

    public const string ValidationPhoneNumberIsRequired = "VALIDATION_PHONE_NUMBER_IS_REQUIRED";
    public const string ValidationPhoneNumberMustBeOnlyDigits = "VALIDATION_PHONE_NUMBER_MUST_BE_ONLY_DIGITS";
    public const string ValidationPhoneNumberMustBeXDigits = "VALIDATION_PHONE_NUMBER_MUST_BE_X_DIGITS";
    public const string ValidationPhoneNumberInvalidCellulerCode = "VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE";
}
