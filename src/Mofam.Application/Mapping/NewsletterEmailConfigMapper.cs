using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Application.Services;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;

namespace Mofam.Application.Mapping;
public sealed class NewsletterEmailConfigMapper(
    ISiteRootResolver siteRootResolver,
    IPropertyValueMapper valueMapper,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    PageCacheToken pageCacheToken) : INewsletterEmailConfigMapper
{
    private const string CacheKeyPrefix = "mofam:newsletter-config:";

    public bool IsEmailEnabled(string culture)
    {
        var settingsRoot = siteRootResolver.GetRoot(CmsConstants.RootAlias.Settings);
        var settings = settingsRoot is null ? null : siteRootResolver.FindChild(settingsRoot, CmsConstants.ContentTypes.WebsiteSettings);

        return settings is not null && valueMapper.Raw(settings, CmsConstants.Fields.NewsletterSendEmail, culture) is true;
    }

    public NewsletterEmailConfig? TryGet(string culture, out string? failure)
    {
        string? reason = null;

        // A failure is not cached (the cache keeps only found values), so fixing the content works at once.
        var config = cachePolicy.GetOrCreate(
            $"{CacheKeyPrefix}{culture}",
            cacheOptions.Value.ContactConfig,
            () => Build(culture, out reason),
            pageCacheToken.Token);

        failure = config is null ? reason ?? "The newsletter content is not available." : null;
        return config;
    }

    private NewsletterEmailConfig? Build(string culture, out string? failure)
    {
        var settingsRoot = siteRootResolver.GetRoot(CmsConstants.RootAlias.Settings);
        if (settingsRoot is null)
        {
            failure = $"The '{CmsConstants.RootAlias.Settings}' node was not found or is not published.";
            return null;
        }

        var settings = siteRootResolver.FindChild(settingsRoot, CmsConstants.ContentTypes.WebsiteSettings);
        if (settings is null)
        {
            failure = $"No published '{CmsConstants.ContentTypes.WebsiteSettings}' node was found under '{CmsConstants.RootAlias.Settings}'.";
            return null;
        }

        var adminEmail = valueMapper.Text(settings, CmsConstants.Fields.AdminEmail, culture)?.Trim();
        if (!CommonHelper.IsValidEmail(adminEmail))
        {
            failure = $"The '{CmsConstants.Fields.AdminEmail}' property of the '{CmsConstants.ContentTypes.WebsiteSettings}' node is empty or not a valid email address.";
            return null;
        }

        var templatesRoot = siteRootResolver.GetRoot(CmsConstants.RootAlias.EmailTemplates);
        if (templatesRoot is null)
        {
            failure = $"The '{CmsConstants.RootAlias.EmailTemplates}' node was not found or is not published.";
            return null;
        }

        var template = siteRootResolver.FindChild(templatesRoot, CmsConstants.ContentTypes.NewsletterEmailTemplate);
        if (template is null)
        {
            failure = $"No published '{CmsConstants.ContentTypes.NewsletterEmailTemplate}' node was found under '{CmsConstants.RootAlias.EmailTemplates}'.";
            return null;
        }

        var subject = valueMapper.Text(template, CmsConstants.Fields.EmailSubject, culture)?.Trim();
        var adminBody = valueMapper.Text(template, CmsConstants.Fields.AdminEmailHtml, culture)?.Trim();
        var userBody = valueMapper.Text(template, CmsConstants.Fields.UserEmailHtml, culture)?.Trim();
        if (CommonHelper.IsBlank(subject) || CommonHelper.IsBlank(adminBody) || CommonHelper.IsBlank(userBody))
        {
            failure = $"The '{CmsConstants.Fields.EmailSubject}', '{CmsConstants.Fields.AdminEmailHtml}' or '{CmsConstants.Fields.UserEmailHtml}' property of the newsletter email template is empty for culture '{culture}'.";
            return null;
        }

        failure = null;
        return new NewsletterEmailConfig(adminEmail!, subject!, adminBody!, userBody!);
    }
}
