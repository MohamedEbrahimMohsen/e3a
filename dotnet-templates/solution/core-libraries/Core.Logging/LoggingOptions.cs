namespace Core.Logging;

using Serilog.Events;

public class LoggingOptions
{
    public const string SectionName = "CoreLogging";
    public TraceOptions Trace { get; set; } = new();
    public ConsoleLoggingOptions Console { get; set; } = new();
    public FileLoggingOptions File { get; set; } = new();
    public SqlLoggingOptions Sql { get; set; } = new();
    public AzureTableLoggingOptions AzureTable { get; set; } = new();
    public AppInsightsLoggingOptions AppInsights { get; set; } = new();
    public SeqLoggingOptions Seq { get; set; } = new();
}

public class TraceOptions
{
    public string? ServiceName { get; set; }
    public string? Version { get; set; }
    public string? Cluster { get; set; }
}

public class ConsoleLoggingOptions
{
    public bool Enabled { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Debug;
    public string[] Environments { get; set; } = [];
}

public class FileLoggingOptions
{
    public bool Enabled { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
    public string BasePath { get; set; } = "Logs";
    public int RetentionInDays { get; set; } = 30;
}

public class SqlLoggingOptions
{
    public SqlSinkTarget RequestLogs { get; set; } = new() { TableName = "RequestLogs" };
    public SqlSinkTarget Logs { get; set; } = new() { TableName = "Logs" };
}

public class SqlSinkTarget
{
    public bool Enabled { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Warning;
    public string? ConnectionString { get; set; }
    public string TableName { get; set; } = "Logs";
}

public class AzureTableLoggingOptions
{
    public bool Enabled { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
    /// <summary>Storage account connection string (table endpoint).</summary>
    public string? ConnectionString { get; set; }
    public string LogsTableName { get; set; } = "Logs";
    public string RequestLogsTableName { get; set; } = "RequestLogs";
    /// <summary>When set, overrides the sink default batch size (null = sink default).</summary>
    public int? BatchPostingLimit { get; set; }
    /// <summary>When set, flush period in seconds (null = sink default).</summary>
    public double? PeriodSeconds { get; set; }
}

public class AppInsightsLoggingOptions
{
    public bool Enabled { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Warning;
    public string? ConnectionString { get; set; }
}

public class SeqLoggingOptions
{
    public bool Enabled { get; set; }
    public string? ServerUrl { get; set; }
    public string? ApiKey { get; set; }
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
}