using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
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

        Log.Logger = loggerConfig.CreateLogger();
        Log.Logger.Information("Logging Initialized");

        builder.Host.UseSerilog(Log.Logger);
        builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);

        return builder;
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
