using System.Text.Json;
using System.Text.Json.Serialization;
using E3A.Application.Options;

namespace E3A.Application.Publishing.Security;

public static class ScanReportSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(ScanReport report, PublishingOptions options)
    {
        var capped = report;
        var json = JsonSerializer.Serialize(capped, Options);

        while (json.Length > options.ScanReportJsonMaxLength && capped.Findings.Count > 0)
        {
            capped = capped with { Findings = [.. capped.Findings.Take(capped.Findings.Count - 1)], IsTruncated = true };
            json = JsonSerializer.Serialize(capped, Options);
        }

        return json;
    }

    public static ScanReport? Deserialize(string? scanReportJson)
    {
        return string.IsNullOrWhiteSpace(scanReportJson) ? null : JsonSerializer.Deserialize<ScanReport>(scanReportJson, Options);
    }
}
