using Microsoft.Extensions.Options;
using Mofam.Application.Abstractions;
using Mofam.Application.Services;
using Mofam.Application.Helpers;
using Mofam.Domain.Constants;
using Mofam.Domain.Options;

namespace Mofam.Application.Mapping;
public sealed class ContactEmailConfigMapper(
    ISiteRootResolver siteRootResolver,
    IPropertyValueMapper valueMapper,
    ICachePolicy cachePolicy,
    IOptions<CacheOptions> cacheOptions,
    PageCacheToken pageCacheToken) : IContactEmailConfigMapper
{
    private const string CacheKeyPrefix = "mofam:contact-config:";

    public ContactEmailConfig? TryGet(string culture, out string? failure)
    {
        string? reason = null;

        // A failure is not cached (the cache keeps only found values), so fixing the content works at once.
        var config = cachePolicy.GetOrCreate(
            $"{CacheKeyPrefix}{culture}",
            cacheOptions.Value.ContactConfig,
            () => Build(culture, out reason),
            pageCacheToken.Token);

        failure = config is null ? reason ?? "The contact content is not available." : null;
        return config;
    }

    private ContactEmailConfig? Build(string culture, out string? failure)
    {
        var settingsRoot = siteRootResolver.GetRoot(CmsConstants.RootAlias.Settings);
        if (settingsRoot is null)
        {
            failure = $"The '{CmsConstants.RootAlias.Settings}' node was not found or is not published.";
            return null;
        }

        var formSettings = siteRootResolver.FindChild(settingsRoot, CmsConstants.ContentTypes.ContactUsFormSettings);
        if (formSettings is null)
        {
            failure = $"No published '{CmsConstants.ContentTypes.ContactUsFormSettings}' node was found under '{CmsConstants.RootAlias.Settings}'.";
            return null;
        }

        var adminEmail = valueMapper.Text(formSettings, CmsConstants.Fields.AdminEmail, culture)?.Trim();
        if (!CommonHelper.IsValidEmail(adminEmail))
        {
            failure = $"The '{CmsConstants.Fields.AdminEmail}' property of the '{CmsConstants.ContentTypes.ContactUsFormSettings}' node is empty or not a valid email address.";
            return null;
        }

        var templatesRoot = siteRootResolver.GetRoot(CmsConstants.RootAlias.EmailTemplates);
        if (templatesRoot is null)
        {
            failure = $"The '{CmsConstants.RootAlias.EmailTemplates}' node was not found or is not published.";
            return null;
        }

        var template = siteRootResolver.FindChild(templatesRoot, CmsConstants.ContentTypes.ContactEmailTemplate);
        if (template is null)
        {
            failure = $"No published '{CmsConstants.ContentTypes.ContactEmailTemplate}' node was found under '{CmsConstants.RootAlias.EmailTemplates}'.";
            return null;
        }

        var subject = valueMapper.Text(template, CmsConstants.Fields.EmailSubject, culture)?.Trim();
        var body = valueMapper.Text(template, CmsConstants.Fields.AdminEmailHtml, culture)?.Trim();
        if (CommonHelper.IsBlank(subject) || CommonHelper.IsBlank(body))
        {
            failure = $"The '{CmsConstants.Fields.EmailSubject}' or '{CmsConstants.Fields.AdminEmailHtml}' property of the email template is empty for culture '{culture}'.";
            return null;
        }

        failure = null;
        return new ContactEmailConfig(adminEmail!, subject!, body!);
    }
}
