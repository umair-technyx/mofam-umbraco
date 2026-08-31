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
        public const string ServiceCategory = "serviceCategory";


        /// <summary>
        /// The channel root a given page type lives under, so callers don't have to pass
        /// the root alias alongside the page type.
        /// </summary>
        public static string RootFor(string pageContentTypeAlias) => pageContentTypeAlias switch
        {
            Page => RootAlias.Site,
            _ => RootAlias.Site,
        };
    }

    public static class Cultures
    {
        public const string English = "en";
        public const string Arabic = "ar";
    }

    public static class Fields
    {
        public const string Components = "components";

        /// <summary>
        /// Editor-controlled URL segment. Note this is a custom property, not Umbraco's
        /// generated <c>UrlSegment</c> — page lookups match on this value.
        /// </summary>
        public const string Slug = "slug";

        public const string Title = "title";
        public const string Categories = "categories";
    }

    /// <summary>
    /// Properties from the metaTags composition. Any content type composed with it
    /// exposes these, so the SEO block is built the same way for every page type.
    /// </summary>
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
    }

    /// <summary>Properties on the site root, grouped by the tab they sit under.</summary>
    public static class SiteFields
    {
        // Header Content tab
        public const string Header = "header";
        public const string HeaderPrimaryLogo = "headerPrimaryLogo";
        public const string HeaderSecondaryLogo = "headerSecondaryLogo";

        // Footer Content tab
        public const string Footer = "footer";
        public const string FooterPrimaryLogo = "footerPrimaryLogo";
        public const string FooterSecondaryLogo = "footerSecondaryLogo";
        public const string SocialLinks = "socialLinks";
        public const string LabelFollowUs = "labelFollowUs";
        public const string BottomText = "bottomText";
    }

    public static class Http
    {
        public const string ApiKeyHeader = "X-Api-Key";
    }
}
