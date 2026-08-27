using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;

namespace Core.Azure.Clients;

public interface IMIClient
{
    TokenCredential GetCredential(string managedIdentityClientId);
}

public class MIClient(IHostEnvironment environment) : IMIClient
{
    public TokenCredential GetCredential(string managedIdentityClientId)
    {
        var managedIdentityId = ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId);
        return environment.IsDevelopment() ? new DefaultAzureCredential() : new ManagedIdentityCredential(managedIdentityId);
    }
}
