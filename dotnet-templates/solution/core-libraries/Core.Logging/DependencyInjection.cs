using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.MSSqlServer;
using System.Data;

namespace Core.Logging;

public static class DependencyInjection
{
    // Entry for APIs (cleanest usage)
    public static IServiceCollection AddCoreLogging(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration
            .GetSection(LoggingOptions.SectionName)
            .Get<LoggingOptions>() ?? new LoggingOptions(); // ✅ fallback

        return services.AddCoreLogging(options, environment.EnvironmentName);
    }

    // Core method (reusable internally)
    private static IServiceCollection AddCoreLogging( this IServiceCollection services, LoggingOptions options, string environmentName)
    {
        var loggerConfig = CreateBaseConfiguration(environmentName);

        ConfigureConsole(loggerConfig, options.Console, environmentName);
        ConfigureFile(loggerConfig, options.File);
        ConfigureSql(loggerConfig, options.Sql);
        ConfigureAzureTable(loggerConfig, options.AzureTable);
        ConfigureApplicationInsights(loggerConfig, options.AppInsights);
        ConfigureSeq(loggerConfig, options.Seq);

        var logger = loggerConfig.CreateLogger();
        Log.Logger = logger;

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(logger);
        });

        return services;
    }

    // Base config
    private static LoggerConfiguration CreateBaseConfiguration(string environmentName)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithMachineName()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", environmentName);
    }

    // Console
    private static void ConfigureConsole(LoggerConfiguration config, ConsoleLoggingOptions options, string environmentName)
    {
        if (!options.Enabled) 
            return;

        if (options.Environments?.Length > 0 &&
            !options.Environments.Contains(environmentName))
            return;

        config.WriteTo.Async(a => a.Console(
            restrictedToMinimumLevel: options.MinimumLevel
        ));
    }

    // File
    private static void ConfigureFile(LoggerConfiguration config, FileLoggingOptions options)
    {
        if (!options.Enabled) 
            return;

        var folder = options.BasePath ?? "Logs";
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, "log-.log");

        config.WriteTo.Async(a => a.File(
            path: path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: options.RetentionInDays,
            restrictedToMinimumLevel: options.MinimumLevel,
            outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] [{TraceId}] {Message:lj}{NewLine}{Exception}"
        ));
    }

    // SQL
    private static void ConfigureSql(LoggerConfiguration config, SqlLoggingOptions options)
    {
        if (!options.RequestLogs.Enabled && !options.Logs.Enabled)
            return;

        // 🔹 Common column setup
        var logColumns = new ColumnOptions();
        logColumns.Store.Remove(StandardColumn.Properties);
        logColumns.AdditionalColumns = new List<SqlColumn>
        {
            new SqlColumn("TraceId", SqlDbType.NVarChar, dataLength: 100),
            new SqlColumn("Environment", SqlDbType.NVarChar, dataLength: 50)
        };

        // 🔹 RequestLogs columns
        var requestColumns = new ColumnOptions();
        requestColumns.Store.Remove(StandardColumn.Properties);

        requestColumns.AdditionalColumns = new List<SqlColumn>
        {
            new SqlColumn("TraceId", SqlDbType.NVarChar, dataLength: 100),
            new SqlColumn("ErrorCode", SqlDbType.NVarChar, dataLength: 300),
            new SqlColumn("DebugId", SqlDbType.NVarChar, dataLength: 500),
            new SqlColumn("Method", SqlDbType.NVarChar, dataLength: 10),
            new SqlColumn("Path", SqlDbType.NVarChar, dataLength: 500),
            new SqlColumn("QueryString", SqlDbType.NVarChar, dataLength: -1),
            new SqlColumn("Host", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("ClientIp", SqlDbType.NVarChar, dataLength: 50),
            new SqlColumn("ForwardedFor", SqlDbType.NVarChar, dataLength: 300),
            new SqlColumn("Country", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("City", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("State", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("Region", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("Timezone", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("Latitude", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("Longitude", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("UserAgent", SqlDbType.NVarChar, dataLength: 500),
            new SqlColumn("StatusCode", SqlDbType.Int),
            new SqlColumn("MachineName", SqlDbType.NVarChar, dataLength: 100),
            new SqlColumn("DurationMs", SqlDbType.BigInt),
            new SqlColumn("Environment", SqlDbType.NVarChar, dataLength: 50),
            new SqlColumn("ServiceName", SqlDbType.NVarChar, dataLength: 200),
            new SqlColumn("Version", SqlDbType.NVarChar, dataLength: 50),
            new SqlColumn("Cluster", SqlDbType.NVarChar, dataLength: 100)
        };

        if (options.RequestLogs.Enabled)
        {
            // 🔥 RequestLogs table
            config.WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("IsRequestLog"))
                .WriteTo.MSSqlServer(
                    connectionString: options.RequestLogs.ConnectionString,
                    sinkOptions: new MSSqlServerSinkOptions
                    {
                        TableName = options.RequestLogs.TableName ?? "RequestLogs",
                        AutoCreateSqlTable = true
                    },
                    columnOptions: requestColumns
                )
            );
        }

        if (options.Logs.Enabled)
        {
            // 🔥 Logs table (everything else)
            config.WriteTo.Logger(lc => lc
                .Filter.ByExcluding(e => e.Properties.ContainsKey("IsRequestLog"))
                .WriteTo.MSSqlServer(
                    connectionString: options.Logs.ConnectionString,
                    sinkOptions: new MSSqlServerSinkOptions
                    {
                        TableName = options.Logs.TableName ?? "Logs",
                        AutoCreateSqlTable = true
                    },
                    columnOptions: logColumns
                )
            );
        }
    }

    // Azure Table Storage
    private static void ConfigureAzureTable(LoggerConfiguration config, AzureTableLoggingOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
            return;

        var period = options.PeriodSeconds is { } s ? TimeSpan.FromSeconds(s) : (TimeSpan?)null;
        string[] azureRequestLogPropertyColumns = ["TraceId", "ErrorCode", "DebugId", "Method", "Path", "QueryString", "Host", "ClientIp", "ForwardedFor", "Country", "City", "State", "Region", "Timezone", "Latitude", "Longitude", "UserAgent", "StatusCode", "MachineName", "DurationMs", "Environment", "ServiceName", "Version", "Cluster"];
        string[] azureLogsPropertyColumns = ["TraceId", "Environment"];
        
        config.WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("IsRequestLog"))
            .WriteTo.AzureTableStorage(
                connectionString: options.ConnectionString,
                restrictedToMinimumLevel: options.MinimumLevel,
                storageTableName: options.RequestLogsTableName,
                period: period,
                batchPostingLimit: options.BatchPostingLimit,
                propertyColumns: azureRequestLogPropertyColumns
            )
        );

        config.WriteTo.Logger(lc => lc
            .Filter.ByExcluding(e => e.Properties.ContainsKey("IsRequestLog"))
            .WriteTo.AzureTableStorage(
                connectionString: options.ConnectionString,
                restrictedToMinimumLevel: options.MinimumLevel,
                storageTableName: options.LogsTableName,
                period: period,
                batchPostingLimit: options.BatchPostingLimit,
                propertyColumns: azureLogsPropertyColumns
            )
        );
    }

    // Log Analytics (Azure Monitor)
    private static void ConfigureApplicationInsights(LoggerConfiguration config, AppInsightsLoggingOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
            return;

        var telemetryConfig = TelemetryConfiguration.CreateDefault();
        telemetryConfig.ConnectionString = options.ConnectionString;

        config.WriteTo.ApplicationInsights(
            telemetryConfig,
            TelemetryConverter.Traces,
            restrictedToMinimumLevel: options.MinimumLevel
        );
    }

    // Seq
    private static void ConfigureSeq(LoggerConfiguration config, SeqLoggingOptions options)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ServerUrl))
            return;

        config.WriteTo.Async(a => a.Seq(
            serverUrl: options.ServerUrl,
            apiKey: options.ApiKey,
            restrictedToMinimumLevel: options.MinimumLevel
        ));
    }
}