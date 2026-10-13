using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Mofam.Infrastructure.Services;

public static class LoggingService
{
    public static WebApplicationBuilder AddCentralizedLogging(this WebApplicationBuilder builder)
    {
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.FromLogContext();

        AddSentrySinkIfEnabled(loggerConfig, builder.Configuration, builder.Environment.EnvironmentName);

        // The logs of our own code (the injected Serilog logger) and the logs of Umbraco go to different places.
        //  - Development: our logs go to Logs/logs-DD-MM-YYYY.txt. Umbraco keeps writing its own local file.
        //  - Other environments: our logs go to the "logs" container (logs-DD-MM-YYYY.txt) and the logs of
        //    Umbraco go to the "umbraco-logs" container (umbracoLogs-DD-MM-YYYY.txt).
        // Umbraco builds its own logger later and reads ILogEventSink services, so the Umbraco sink is
        // registered as one.
        if (builder.Environment.IsDevelopment())
        {
            loggerConfig.WriteTo.Sink(BuildLocalFileLogger());
        }
        else
        {
            var codeSink = BuildBlobLogger(builder.Configuration, "ContainerName", "logs", "logs-{dd}-{MM}-{yyyy}.txt");
            if (codeSink is not null)
            {
                loggerConfig.WriteTo.Sink(codeSink);
            }

            var umbracoSink = BuildBlobLogger(
                builder.Configuration, "UmbracoContainerName", "umbraco-logs", "umbracoLogs-{dd}-{MM}-{yyyy}.txt");
            if (umbracoSink is not null)
            {
                builder.Services.AddSingleton<ILogEventSink>(umbracoSink);
            }
        }

        Log.Logger = loggerConfig.CreateLogger();
        Log.Logger.Information("Logging Initialized");

        builder.Host.UseSerilog(Log.Logger);
        builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);

        return builder;
    }

    // A logger that writes one log file per day in the Logs folder, for example Logs/logs-31-12-2026.txt.
    // "shared" lets more than one process write to the same file, so there are no _001, _002 files.
    private static Logger BuildLocalFileLogger() =>
        new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Map(
                logEvent => logEvent.Timestamp.ToString("dd-MM-yyyy"),
                (date, sink) => sink.File($"Logs/logs-{date}.txt", shared: true),
                sinkMapCountLimit: 2)
            .CreateLogger();

    // A logger that writes to Azure Blob Storage, one file per day, for example logs-31-12-2026.txt.
    // It uses "LogStorage:ConnectionString" (it can come from Key Vault). A connection string uses the
    // account key. An account address (https://<account>.blob.core.windows.net) uses the Managed Identity.
    // The container name is read from the given key in the "LogStorage" section. Container names must be
    // lower case. Returns null if the connection string is empty, so nothing is written.
    private static Logger? BuildBlobLogger(
        IConfiguration configuration, string containerNameKey, string defaultContainerName, string fileName)
    {
        var section = configuration.GetSection("LogStorage");
        var connection = section["ConnectionString"];

        if (string.IsNullOrWhiteSpace(connection)) return null;

        var containerName = string.IsNullOrWhiteSpace(section[containerNameKey])
            ? defaultContainerName
            : section[containerNameKey]!;

        var blobServiceClient = connection.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? new BlobServiceClient(new Uri(connection), new DefaultAzureCredential())
            : new BlobServiceClient(connection);

        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.AzureBlobStorage(
                blobServiceClient: blobServiceClient,
                restrictedToMinimumLevel: LogEventLevel.Information,
                storageContainerName: containerName,
                storageFileName: fileName,
                period: TimeSpan.FromSeconds(15),
                batchPostingLimit: 100)
            .CreateLogger();
    }

    private static void AddSentrySinkIfEnabled(LoggerConfiguration loggerConfig, IConfiguration configuration, string environmentName)
    {
        var sentrySection = configuration.GetSection("Sentry");
        var useSentry = sentrySection.GetValue<bool>("Enabled");
        var sentryDsn = sentrySection.GetValue<string>("Dsn");
        var appName = sentrySection.GetValue<string>("AppName");
        var minimumEventLevel = sentrySection.GetValue<string>("MinimumEventLevel");

        if (!useSentry || string.IsNullOrWhiteSpace(sentryDsn)) return;

        loggerConfig.WriteTo.Sentry(options =>
        {
            options.Dsn = sentryDsn;
            options.Environment = environmentName;
            options.AttachStacktrace = true;
            options.MinimumEventLevel = Enum.TryParse(minimumEventLevel, true, out LogEventLevel level)
                ? level
                : LogEventLevel.Warning;
            options.MinimumBreadcrumbLevel = LogEventLevel.Information;
            options.DefaultTags.Add("ApplicationName", appName);
        });
    }
}
