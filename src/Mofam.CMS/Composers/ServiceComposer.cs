using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.Mapping;
using Mofam.Application.Services;
using Mofam.Domain.Options;
using Mofam.Infrastructure.Abstractions;
using Mofam.Infrastructure.Filters;
using Mofam.Infrastructure.Services;
using Mofam.Application.IServices;
using Mofam.Application.Notifications;
using Umbraco.Cms.Core.Notifications;

namespace Mofam.CMS.Composers;

public sealed class ServiceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<IApiService, ApiService>();
        builder.Services.AddScoped<IStartupService, StartupService>();
        builder.Services.AddScoped<IComponentMapper, ComponentMapper>();
        builder.Services.AddScoped<IPropertyValueMapper, PropertyValueMapper>();
        builder.Services.AddScoped<ICachePolicy, CachePolicy>();

        // Singleton: one shared token for all cached pages (cleared on publish).
        builder.Services.AddSingleton<PageCacheToken>();
        builder.Services.AddScoped<ISeoMapper, SeoMapper>();
        builder.Services.AddScoped<IPageMapper, PageMapper>();
        builder.Services.AddScoped<ISiteRootResolver, SiteRootResolver>();
        builder.Services.AddScoped<IMediaUrlBuilder, MediaUrlBuilder>();
        // Required: SearchController takes ISiteSearchService in its constructor, so it must be registered.
        builder.Services.AddScoped<ISiteSearchService, SiteSearchService>();
        builder.Services.AddScoped<IFilterService, FilterService>();
        builder.Services.AddScoped<ApiKeyAuthFilter>();
        builder.Services.AddScoped<IDatabaseConnectivityService, DatabaseConnectivityService>();

        // Normalises the slug and blocks duplicates before content is saved.
        builder.AddNotificationHandler<ContentSavingNotification, SlugNotificationHandler>();

        // Clears the startup and filter caches when content changes, so the CMS changes show at once.
        builder.AddNotificationHandler<ContentCacheRefresherNotification, ContentCacheNotificationHandler>();

        builder.Services.Configure<SecurityOptions>(builder.Config.GetSection(SecurityOptions.SectionName));
        builder.Services.Configure<CacheOptions>(builder.Config.GetSection(CacheOptions.SectionName));
        builder.Services.Configure<EmailSettings>(builder.Config.GetSection(EmailSettings.SectionName));
        builder.Services.Configure<ReCaptchaOptions>(builder.Config.GetSection(ReCaptchaOptions.SectionName));

        builder.Services.AddScoped<IContactEmailConfigMapper, ContactEmailConfigMapper>();
        builder.Services.AddScoped<IContactService, ContactService>();
        builder.Services.AddSingleton<IRecaptchaService, RecaptchaService>();

        // The send timeout (15 seconds) is set in EmailHelper.
        builder.Services.AddHttpClient<EmailHelper>();
    }
}
