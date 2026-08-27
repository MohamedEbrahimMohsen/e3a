using System;
using System.Collections.Generic;
using System.Text;

namespace Extensions.Core.Logging;

public class RequestLog
{
    public long Id { get; set; }

    // 🔗 Correlation
    public string? TraceId { get; set; }

    // 🌐 Request Base
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? QueryString { get; set; }

    // 🌍 Client / Network
    public string? Host { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }

    // ☁️ Cloudflare (important in your case)
    public string? CfRay { get; set; }
    public string? CfConnectingIp { get; set; }
    public string? CfCountry { get; set; }

    // 🧠 Execution
    public int? StatusCode { get; set; }
    public string? MachineName { get; set; }
    // ⏱ Timing
    public DateTime? StartTime { get; set; }
    public long? DurationMs { get; set; }

    // 👤 Optional (future)
    public string? UserId { get; set; }
    // 📦 Optional (if needed later)
    public string? HeadersJson { get; set; }
}
