namespace E3A.Application.Options;

public sealed class AzureOptions
{
    public const string SectionName = "Azure";

    public string ManagedIdentityClientId { get; set; } = string.Empty;
    public string StorageAccountUrl { get; set; } = string.Empty;
    public string DraftsBlobContainerName { get; set; } = string.Empty;
    public string StorageAccountQueueUrl { get; set; } = string.Empty;
    public string SnapshotsBlobContainerName { get; set; } = string.Empty;
    public string PublicBlobContainerName { get; set; } = string.Empty;
    public string PublishQueueName { get; set; } = string.Empty;
}
