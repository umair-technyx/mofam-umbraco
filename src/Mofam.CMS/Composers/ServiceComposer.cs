using Umbraco.Cms.Core.Composing;
using Mofam.Application.Abstractions;
using Mofam.Application.Mapping;
using Mofam.Application.Services;
using Mofam.Domain.Options;
using Mofam.Infrastructure.Abstractions;
using Mofam.Infrastructure.Filters;
using Mofam.Infrastructure.Services;

namespace Mofam.CMS.Composers;

public sealed class ServiceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<IPageService, PageService>();
        builder.Services.AddScoped<IComponentMapper, ComponentMapper>();
        builder.Services.AddScoped<ISiteRootResolver, SiteRootResolver>();
        builder.Services.AddScoped<IMediaUrlBuilder, MediaUrlBuilder>();
        builder.Services.AddScoped<ApiKeyAuthFilter>();
        builder.Services.AddScoped<IDatabaseConnectivityService, DatabaseConnectivityService>();

        builder.Services.Configure<SecurityOptions>(
            builder.Config.GetSection(SecurityOptions.SectionName));
    }
}
