using System.Text.Json;
using System.Threading.RateLimiting;
using Azure.Core;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Storage.Blobs;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.StorageProviders.AzureBlob.IO;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Mofam.Domain.Constants;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Options;
using Mofam.CMS.Pages;
using Mofam.CMS.Security;
using Mofam.Infrastructure.HealthChecks;
using Mofam.Infrastructure.Middleware;
using Mofam.Infrastructure.Routing;
using Mofam.Infrastructure.Services;

const string CorsPolicyName = "ConfiguredOrigins";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

if (rateLimitOptions.PermitLimit <= 0 || rateLimitOptions.WindowSeconds <= 0
    || rateLimitOptions.SignInPermitLimit <= 0 || rateLimitOptions.SignInWindowSeconds <= 0)
{
    throw new InvalidOperationException(
        $"Invalid '{RateLimitOptions.SectionName}' configuration — PermitLimit, WindowSeconds, SignInPermitLimit and SignInWindowSeconds must be greater than 0.");
}

// The sign in pages are open to everyone, so they have their own limit per client address.
var entraSettings = builder.Configuration.GetSection(EntraBackOfficeOptions.SectionName).Get<EntraBackOfficeOptions>()
                    ?? new EntraBackOfficeOptions();
string[] signInPaths = [entraSettings.CallbackPath, entraSettings.SignedOutCallbackPath, GeneralConstants.SignInErrorPath];

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        signInPaths.Any(path => http.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            ? RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.SignInPermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.SignInWindowSeconds),
                    QueueLimit = 0,
                })
            : RateLimitPartition.GetNoLimiter("other"));

    rateLimiterOptions.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = rateLimitOptions.PermitLimit;
        limiterOptions.Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds);
        limiterOptions.QueueLimit = 0;
    });

    rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";

        var response = ApiResponse<object>.TooManyRequests("Rate limit exceeded. Please try again later.");
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(response), cancellationToken);
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

builder.WebHost.ConfigureKestrel(kestrelOptions =>
{
    kestrelOptions.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5 MB
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

builder.Services.Configure<MvcOptions>(options =>
{
    options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
});

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

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

// First, so everything after it sees the real scheme and host.
app.UseForwardedHeaders();

app.UseResponseCompression();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseMiddleware<NotFoundNormalizationMiddleware>();
    branch.UseMiddleware<ApiValidationMiddleware>();
    branch.UseMiddleware<XssSanitizationMiddleware>();
});

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);
app.UseRateLimiter();

app.Use(async (http, next) =>
{
    if (HttpMethods.IsGet(http.Request.Method)
        && http.Request.Path.Equals(GeneralConstants.SignInErrorPath, StringComparison.OrdinalIgnoreCase)
        && http.RequestServices.GetRequiredService<SignInErrorToken>()
            .TryRead(http.Request.Query[GeneralConstants.SignInErrorTokenQuery], out var reason, out var reference))
    {
        // The page must not be cached, shown in another site, or sent as a referrer (the address has a token).
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
        u.EndpointRouteBuilder.MapGet("/", (Umbraco.Cms.Core.Configuration.IUmbracoVersion umbracoVersion) =>
            Results.Content(StatusPage.Html(umbracoVersion.SemanticVersion.ToString().Split('+')[0]), "text/html"));

    });

await app.RunAsync();
