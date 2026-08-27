namespace Mofam.Domain.Constants;

public static class CmsConstants
{
    public static class RootAlias
    {
        public const string Root = "root";
        public const string Web = "web";
        public const string App = "app";
        public const string Components = "components";
        public const string WebComponents = "webComponents";
        public const string AppComponents = "appComponents";
        public const string SharedComponents = "sharedComponents";
        public const string DataSources = "dataSources";
        public const string Settings = "settings";
        public const string EmailTemplates = "emailTemplates";
        public const string Forms = "forms";
    }

    public static class ContentTypes
    {
        public const string WebPage = "webPage";
        public const string AppPage = "appPage";

        /// <summary>
        /// The channel root a given page type lives under, so callers don't have to pass
        /// the root alias alongside the page type.
        /// </summary>
        public static string RootFor(string pageContentTypeAlias) => pageContentTypeAlias switch
        {
            WebPage => RootAlias.Web,
            AppPage => RootAlias.App,
            _ => RootAlias.Root,
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
    }

    public static class Http
    {
        public const string ApiKeyHeader = "X-Api-Key";
    }
}
