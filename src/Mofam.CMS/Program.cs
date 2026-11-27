using System.Text.Json;
using System.Threading.RateLimiting;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Umbraco.StorageProviders.AzureBlob.IO;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Options;
using Mofam.CMS.Pages;
using Mofam.CMS.Security;
using Mofam.Infrastructure.HealthChecks;
using Mofam.Infrastructure.Middleware;
using Mofam.Infrastructure.RateLimiting;
using Mofam.Infrastructure.Services;

const string CorsPolicyName = "ConfiguredOrigins";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The "Server: Kestrel" header only tells an attacker what runs here.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

if (!builder.Environment.IsDevelopment())
{
    var keyVaultSection = builder.Configuration.GetSection("KeyVault");

    if (Uri.TryCreate(keyVaultSection["URL"], UriKind.Absolute, out var keyVaultUri))
    {
        var tenantId = keyVaultSection["TenantId"];
        var clientId = keyVaultSection["ClientId"];
        var clientSecret = keyVaultSection["ClientSecret"];

        TokenCredential credential =
            !string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret)
                ? new ClientSecretCredential(tenantId, clientId, clientSecret)
                : new DefaultAzureCredential();

        builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);
    }
}

builder.AddCentralizedLogging();

var rateLimitSection = builder.Configuration.GetSection(RateLimitOptions.SectionName);
if (!rateLimitSection.Exists())
{
    throw new InvalidOperationException(
        $"Configuration section '{RateLimitOptions.SectionName}' is missing — set it in appsettings.json.");
}

var rateLimitOptions = new RateLimitOptions();
rateLimitSection.Bind(rateLimitOptions);

int[] rateLimitValues =
[
    rateLimitOptions.ApiTokenLimit, rateLimitOptions.ApiTokensPerPeriod, rateLimitOptions.ApiPeriodSeconds,
    rateLimitOptions.SignInPermitLimit, rateLimitOptions.SignInWindowSeconds,
    rateLimitOptions.FormTokenLimit, rateLimitOptions.FormTokensPerPeriod, rateLimitOptions.FormPeriodSeconds,
    rateLimitOptions.FormTotalTokenLimit, rateLimitOptions.FormTotalTokensPerPeriod, rateLimitOptions.FormTotalPeriodSeconds,
];
if (rateLimitValues.Any(value => value <= 0))
{
    throw new InvalidOperationException(
        $"Invalid '{RateLimitOptions.SectionName}' configuration — every token limit, tokens per period and period seconds must be greater than 0.");
}

var entraSettings = builder.Configuration.GetSection(EntraBackOfficeOptions.SectionName).Get<EntraBackOfficeOptions>() ?? new EntraBackOfficeOptions();
string[] signInPaths = [entraSettings.CallbackPath.Trim(), entraSettings.SignedOutCallbackPath.Trim(), GeneralConstants.SignInErrorPath];

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiterOptions.GlobalLimiter = RateLimitPolicy.Create(rateLimitOptions, signInPaths);

    rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        // Written to the log (Information, not Warning: an attack would send one Sentry event for each rejected request).
        LoggingService.AppLogger.Information("Rate limit exceeded. Path={Path}, Client={Client}", context.HttpContext.Request.Path, ClientAddress.Resolve(context.HttpContext).Key);

        var response = ApiResponse<object>.TooManyRequests("Rate limit exceeded. Please try again later.") with { TraceId = context.HttpContext.TraceIdentifier };
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(response, ApiJson.Options), cancellationToken);
    };
});

var corsOptions = new CorsOptions();
builder.Configuration.GetSection(CorsOptions.SectionName).Bind(corsOptions);

builder.Services.AddCors(corsBuilder =>
{
    corsBuilder.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var allowedHosts = (builder.Configuration["AllowedHosts"] ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.Configure<ForwardedHeadersOptions>(forwardedOptions =>
{
    forwardedOptions.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    forwardedOptions.KnownIPNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();

    if (allowedHosts.Length > 0 && !allowedHosts.Contains("*"))
    {
        forwardedOptions.AllowedHosts = allowedHosts;
    }
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SignInErrorToken>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

var umbracoBuilder = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite();

var blobSection = builder.Configuration.GetSection("Umbraco:Storage:AzureBlob:Media");

if (!string.IsNullOrWhiteSpace(blobSection["ConnectionString"]))
{
    umbracoBuilder.AddAzureBlobMediaFileSystem(options =>
        options.TryCreateBlobContainerClientUsingUri(accountUri =>
            new BlobContainerClient(
                new Uri($"{accountUri.GetLeftPart(UriPartial.Authority)}/{options.ContainerName}"),
                new DefaultAzureCredential())));
}

umbracoBuilder
    .AddComposers()
    .Build();

// Umbraco replaced the injected Serilog logger with its own. This registers our logger again (it must stay after the Umbraco builder),
// so the code of the project writes to the "logs" container and to Sentry.
builder.Services.AddSingleton<Serilog.ILogger>(_ => LoggingService.AppLogger);
FunctionTracer.Logger = LoggingService.AppLogger;

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseForwardedHeaders();

// Before everything else, so every response (errors and refusals too) gets the browser security headers.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseResponseCompression();

app.UseMiddleware<GlobalExceptionMiddleware>();

// Browsers remember to use HTTPS only. Not in Development, where the site runs on plain http.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// CORS and the rate limit run before the API checks, so a limited request is refused without its body being read, and every refusal has the CORS headers.
app.UseCors(CorsPolicyName);
app.UseRateLimiter();

app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    // First, so a caller without the key is refused before the body is read or validated.
    branch.UseMiddleware<ApiKeyMiddleware>();
    branch.UseMiddleware<NotFoundNormalizationMiddleware>();
    branch.UseMiddleware<ApiValidationMiddleware>();
    branch.UseMiddleware<XssSanitizationMiddleware>();
});

app.Use(async (http, next) =>
{
    if (HttpMethods.IsGet(http.Request.Method)
        && http.Request.Path.Equals(GeneralConstants.SignInErrorPath, StringComparison.OrdinalIgnoreCase)
        && http.RequestServices.GetRequiredService<SignInErrorToken>()
            .TryRead(http.Request.Query[GeneralConstants.SignInErrorTokenQuery], out var reason, out var reference))
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XFrameOptions = "DENY";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";

        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync(SignInErrorPage.Html(reason, reference));
        return;
    }

    await next();
});

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
        u.EndpointRouteBuilder.MapControllers();
        u.EndpointRouteBuilder.MapHealthChecks("/health");
        u.EndpointRouteBuilder.MapGet("/", () => Results.Content(StatusPage.Html(), "text/html"));

    });

await app.RunAsync();
