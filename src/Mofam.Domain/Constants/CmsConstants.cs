namespace Mofam.Domain.Constants;

public static class CmsConstants
{
    public static class RootAlias
    {
        public const string Site = "site";
        public const string Components = "components";
        public const string DataSources = "dataSources";
        public const string Settings = "settings";
        public const string EmailTemplates = "emailTemplates";
        public const string Forms = "forms";
    }

    public static class ContentTypes
    {
        public const string Page = "page";
        public const string Service = "service";
        public const string News = "news";
        public const string Resource = "resource";
        public const string Story = "story";
        public const string Leader = "leader";
        public const string Media = "media";
        public const string ServiceCategory = "serviceCategory";
        public const string MediaCategory = "mediaCategory";
        public const string ResourceCategory = "resourceCategory";
        public const string NewsCategory = "newsCategory";
        public const string NewsYearCategory = "newsYearCategory";
        public const string ContactUsFormSettings = "contactUsFormSettings";
        public const string ContactEmailTemplate = "contactEmailTemplate";

        // Content types with their own detail page (reached by slug). Any other type picked inside a page is shown in full.
        public static readonly string[] PageTypes = [Page, Service, ServiceCategory];
    }

    public static class Cultures
    {
        public const string English = "en";
        public const string Arabic = "ar";
        public static readonly string[] All = [English, Arabic];
        public static string? Resolve(string? culture) => All.FirstOrDefault(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));
    }

    public static class Fields
    {
        public const string Components = "components";
        public const string Slug = "slug";
        public const string Title = "title";
        public const string Categories = "categories";
        public const string Year = "year";
        public const string DetailPageComponents = "detailPageComponents";
        public const string EmailSubject = "emailSubject";
        public const string AdminEmailHtml = "adminEmailHtml";
        public const string AdminEmail = "adminEmail";
    }

    public static class SeoFields
    {
        public const string MetaTitle = "metaTitle";
        public const string MetaDescription = "metaDescription";
        public const string MetaKeywords = "metaKeywords";
        public const string MetaCanonicalLink = "metaCanonicalLink";
        public const string MetaSchemaJson = "metaSchemaJson";

        public const string RobotsIndex = "robotsMetaTagsindex";
        public const string RobotsFollow = "robotsMetaTagsFollow";

        public const string OgTitle = "ogTitle";
        public const string OgDescription = "ogDescription";
        public const string OgType = "ogType";
        public const string OgPageUrl = "ogPagUrl";
        public const string OgImage = "ogImage";
        public const string OpenGraphImage = "openGraphImage";
        public const string OpenGraphImageExternal = "openGraphImageExternal";

        public const string TwitterTitle = "ogTwitterTitle";
        public const string TwitterDescription = "ogTwitterDescription";
        public const string TwitterUrl = "ogTwitterUrl";
        public const string TwitterImage = "ogTwitterImage";

        public const string HreflangDefault = "defaultHreflangURL";
        public const string HreflangEnglish = "englishHreflangURL";
        public const string HreflangArabic = "arabicHreflangURL";

        public const string HideFromSitemap = "hideFromSitemap";
        public const string CustomPriority = "customPriority";
        public const string CustomChangeFrequency = "customchangeFrequency";
        public const string LastUpdatedDate = "lastUpdatedDate";

        // Every SEO alias, so listing responses can leave them out in one step (SEO is only for a detail page).
        public static readonly string[] All =
        [
            MetaTitle, MetaDescription, MetaKeywords, MetaCanonicalLink, MetaSchemaJson,
            RobotsIndex, RobotsFollow,
            OgTitle, OgDescription, OgType, OgPageUrl, OgImage, OpenGraphImage, OpenGraphImageExternal,
            TwitterTitle, TwitterDescription, TwitterUrl, TwitterImage,
            HreflangDefault, HreflangEnglish, HreflangArabic,
            HideFromSitemap, CustomPriority, CustomChangeFrequency, LastUpdatedDate,
        ];
    }

    public static class SiteFields
    {
        // Header Content tab
        public const string Header = "header";
        public const string HeaderPrimaryLogo = "headerPrimaryLogo";
        public const string HeaderSecondaryLogo = "headerSecondaryLogo";
        public const string HeaderLogoAltText = "headerLogoAltText";
        public const string TopBarLabel = "topBarLabel";
        public const string SearchBarLabel = "searchBarLabel";
        public const string LearnMoreCta = "learnMoreCta";
        public const string AccessibilityScript = "accessbilitiyScript"; 

        // Footer Content tab
        public const string Footer = "footer";
        public const string FooterPrimaryLogo = "footerPrimaryLogo";
        public const string FooterSecondaryLogo = "footerSecondaryLogo";
        public const string FooterLogoAltText = "footerLogoAltText";
        public const string SocialLinks = "socialLinks";
        public const string FollowUsLabel = "followUsLabel";
        public const string BottomText = "bottomText";
        public const string LastUpdatedToken = "{lastUpdated}";
    }

    public static class Http
    {
        public const string ApiKeyHeader = "X-Api-Key";
    }
}
