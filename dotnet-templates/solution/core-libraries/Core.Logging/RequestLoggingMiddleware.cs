using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Context;
using System.Collections;
using System.Diagnostics;

namespace Core.Logging;

public class CoreRequestLoggingMiddleware(RequestDelegate next, IHostEnvironment env, IConfiguration configuration)
{
    private readonly RequestDelegate _next = next;
    private readonly string _environment = env.EnvironmentName;
    private readonly LoggingOptions _options = configuration
                                                    .GetSection(LoggingOptions.SectionName)
                                                    .Get<LoggingOptions>() ?? new LoggingOptions();

    public async Task Invoke(HttpContext context)
    {
        var stopwatch = Stopwatch.GetTimestamp();
        var start = DateTimeOffset.UtcNow;

        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier; ;
        var debugId = ConstructDebugId(traceId, start);
        context.Response.Headers["X-Version"] = _options.Trace?.Version ?? string.Empty;
        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Response.Headers["X-Debug-Id"] = debugId;

        using (LogContext.PushProperty("TraceId", traceId))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                long duration = (long)Stopwatch.GetElapsedTime(stopwatch).TotalMilliseconds;
                var errorCode = context?.Items.TryGetValue("ErrorCode", out var code) == true ? code : null;

                Log.ForContext("IsRequestLog", true)
                   .ForContext("ErrorCode", errorCode)
                   .ForContext("TraceId", traceId)
                   .ForContext("DebugId", debugId)
                   .ForContext("Method", context?.Request?.Method)
                   .ForContext("Path", context?.Request?.Path)
                   .ForContext("QueryString", context?.Request?.QueryString.ToString())
                   .ForContext("Host", context?.Request?.Host.ToString())
                   .ForContext("ClientIp", GetClientIp(context))
                   .ForContext("ForwardedFor", Header(context, "X-Forwarded-For"))
                   .ForContext("Country", Header(context, "CF-IPCountry"))
                   .ForContext("City", Header(context, "CF-IPCity"))
                   .ForContext("State", Header(context, "CF-Region"))
                   .ForContext("Region", Header(context, "CF-Region-Code"))
                   .ForContext("Timezone", Header(context, "CF-Timezone"))
                   .ForContext("Latitude", Header(context, "CF-IPLatitude"))
                   .ForContext("Longitude", Header(context, "CF-IPLongitude"))
                   .ForContext("UserAgent", (context?.Request?.Headers["User-Agent"])?.FirstOrDefault()?.ToString())
                   .ForContext("StatusCode", context?.Response?.StatusCode)
                   .ForContext("MachineName", Environment.MachineName)
                   .ForContext("Environment", _environment)
                   .ForContext("ServiceName", _options.Trace?.ServiceName)
                   .ForContext("Version", _options.Trace?.Version)
                   .ForContext("Cluster", _options.Trace?.Cluster)
                   .ForContext("DurationMs", duration)
                   .Information("HTTP_REQUEST_COMPLETED");
            }
        }
    }

    private string GetClientIp(HttpContext? context)
    {
        return context?.Request?.Headers["CF-Connecting-IP"].FirstOrDefault()
            ?? context?.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    // Cloudflare / proxy request headers. Null when the header is absent (e.g. local run, or the
    // "Add visitor location headers" managed transform not enabled for the geo ones) — logged as null.
    private static string? Header(HttpContext? context, string name)
        => context?.Request?.Headers[name].FirstOrDefault();

    private string ConstructDebugId(string traceId, DateTimeOffset timestamp)
    {
        return $"{traceId}|" +
            $"{timestamp.ToString("dd-MM-yyyy:HH:mm:ss")}|" +
            $"{(string.IsNullOrEmpty(_options.Trace?.ServiceName) ? string.Empty : $"{_options.Trace.ServiceName}|")}" +
            $"{(string.IsNullOrEmpty(_options.Trace?.Cluster) ? string.Empty : $"{_options.Trace.Cluster}|")}" +
            $"{_environment[..2]?.ToUpperInvariant()}" +
            $"|{Environment.MachineName}";
    }
}