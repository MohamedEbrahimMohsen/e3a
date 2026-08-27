namespace E3A.Application.Exceptions;

/// <summary>
/// Flat error-code registry, grouped by area. Adding a code here is only the
/// first of the places the constitution requires — complete every one.
/// </summary>
public static class ErrorCodes
{
    // Identity
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string UserNotFound = "USER_NOT_FOUND";

    // Engineers
    public const string EngineerNotFound = "ENGINEER_NOT_FOUND";
    public const string EngineerNotOwned = "ENGINEER_NOT_OWNED";
    public const string EngineerLimitReached = "ENGINEER_LIMIT_REACHED";
    public const string EngineerIdRequired = "ENGINEER_ID_REQUIRED";
    public const string EngineerDisplayNameRequired = "ENGINEER_DISPLAY_NAME_REQUIRED";
    public const string EngineerDisplayNameTooLong = "ENGINEER_DISPLAY_NAME_TOO_LONG";
    public const string EngineerDisplayNameInvalid = "ENGINEER_DISPLAY_NAME_INVALID";
    public const string EngineerDescriptionTooLong = "ENGINEER_DESCRIPTION_TOO_LONG";
    public const string EngineerTooManyTags = "ENGINEER_TOO_MANY_TAGS";
    public const string EngineerTagRequired = "ENGINEER_TAG_REQUIRED";
    public const string EngineerTagTooLong = "ENGINEER_TAG_TOO_LONG";
    public const string EngineerDraftNotUploaded = "ENGINEER_DRAFT_NOT_UPLOADED";

    // Uploads
    public const string UploadFileRequired = "UPLOAD_FILE_REQUIRED";
    public const string UploadFileMustBeZip = "UPLOAD_FILE_MUST_BE_ZIP";
    public const string UploadFileTooLarge = "UPLOAD_FILE_TOO_LARGE";
    public const string UploadZipInvalid = "UPLOAD_ZIP_INVALID";
    public const string UploadTooManyFiles = "UPLOAD_TOO_MANY_FILES";
    public const string UploadUncompressedTooLarge = "UPLOAD_UNCOMPRESSED_TOO_LARGE";
    public const string UploadUnsafePath = "UPLOAD_UNSAFE_PATH";
    public const string UploadSymlinkNotAllowed = "UPLOAD_SYMLINK_NOT_ALLOWED";
    public const string UploadFileTypeNotAllowed = "UPLOAD_FILE_TYPE_NOT_ALLOWED";
    public const string UploadDuplicatePath = "UPLOAD_DUPLICATE_PATH";
    public const string UploadEmpty = "UPLOAD_EMPTY";
}
