using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace E3a.Functions.Functions;

public sealed class PingFunction
{
    // Health-probe identity, not configuration: monitoring dashboards key on this literal.
    private const string ServiceName = "e3a-api";

    [Function("Ping")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ping")] HttpRequest request)
    {
        return new OkObjectResult(new { status = "ok", service = ServiceName, utc = DateTimeOffset.UtcNow });
    }
}
