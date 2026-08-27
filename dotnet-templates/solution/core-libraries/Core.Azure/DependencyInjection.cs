using Core.Azure.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Azure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreAzure(this IServiceCollection services)
    {
        services.AddSingleton<IMIClient, MIClient>();
        services.AddSingleton<IStorageBlobClient, StorageBlobClient>();
        services.AddSingleton<IStorageQueueClient, StorageQueueClient>();
        return services;
    }
}