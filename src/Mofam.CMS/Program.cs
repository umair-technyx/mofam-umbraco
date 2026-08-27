using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Mofam.Domain.Models.Common;
using Mofam.Domain.Options;
using Mofam.CMS;
using Mofam.Infrastructure.HealthChecks;
using Mofam.Infrastructure.Middleware;
using Mofam.Infrastructure.Routing;
using Mofam.Infrastructure.Services;

const string CorsPolicyName = "ConfiguredOrigins";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddCentralizedLogging();

var rateLimitSection = builder.Configuration.GetSection(RateLimitOptions.SectionName);
if (!rateLimitSection.Exists())
{
    throw new InvalidOperationException(
        $"Configuration section '{RateLimitOptions.SectionName}' is missing — set it in appsettings.json.");
}

var rateLimitOptions = new RateLimitOptions();
rateLimitSection.Bind(rateLimitOptions);

if (rateLimitOptions.PermitLimit <= 0 || rateLimitOptions.WindowSeconds <= 0)
{
    throw new InvalidOperationException(
        $"Invalid '{RateLimitOptions.SectionName}' configuration — PermitLimit and WindowSeconds must be greater than 0.");
}

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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

builder.Services.AddMemoryCache();
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

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

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
