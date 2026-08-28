using Umbraco.Cms.Core.Composing;
using Mofam.Application.Abstractions;
using Mofam.Application.Mapping;
using Mofam.Application.Services;
using Mofam.Domain.Options;
using Mofam.Infrastructure.Abstractions;
using Mofam.Infrastructure.Filters;
using Mofam.Infrastructure.Services;
using Mofam.Application.IServices;

namespace Mofam.CMS.Composers;

public sealed class ServiceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<IApiService, ApiServcie>();
        builder.Services.AddScoped<IStartupService, StartupService>();
        builder.Services.AddScoped<IComponentMapper, ComponentMapper>();
        builder.Services.AddScoped<IPropertyValueMapper, PropertyValueMapper>();
        builder.Services.AddScoped<ISeoMapper, SeoMapper>();
        builder.Services.AddScoped<ISiteRootResolver, SiteRootResolver>();
        builder.Services.AddScoped<IMediaUrlBuilder, MediaUrlBuilder>();
        //builder.Services.AddScoped<IContentSearchService, ContentSearchService>();
        builder.Services.AddScoped<ApiKeyAuthFilter>();
        builder.Services.AddScoped<IDatabaseConnectivityService, DatabaseConnectivityService>();

        builder.Services.Configure<SecurityOptions>(
            builder.Config.GetSection(SecurityOptions.SectionName));

        //builder.Services.Configure<SearchOptions>(
        //    builder.Config.GetSection(SearchOptions.SectionName));
    }
}
