using Microsoft.AspNetCore.Http;
using Mofam.Domain.Constants;

namespace Mofam.Infrastructure.Middleware;

// Adds the browser security headers. The public site is built by the front end, so these cover what this app
// serves itself: the backoffice, the API, the media files and the small status and sign-in error pages.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        // Added just before the response starts, so responses written later (errors, 401, 429) get them too.
        context.Response.OnStarting(() =>
        {
            Apply(context.Request.Path, context.Response.Headers);
            return Task.CompletedTask;
        });

        return next(context);
    }

    private static void Apply(PathString path, IHeaderDictionary headers)
    {
        Add(headers, "X-Content-Type-Options", "nosniff");
        Add(headers, "Referrer-Policy", GeneralConstants.ReferrerPolicy);
        Add(headers, "Permissions-Policy", GeneralConstants.PermissionsPolicy);

        if (path.StartsWithSegments("/umbraco", StringComparison.OrdinalIgnoreCase))
        {
            // The backoffice uses an inline import map and inline styles, so a full CSP would break it.
            // What matters here is that the login and the editors cannot be shown inside another site's frame.
            Add(headers, "X-Frame-Options", "SAMEORIGIN");
            Add(headers, "Content-Security-Policy", GeneralConstants.BackOfficeCsp);
            Add(headers, "Cross-Origin-Opener-Policy", "same-origin-allow-popups");
            Add(headers, "Cross-Origin-Resource-Policy", "same-origin");
            return;
        }

        if (path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase))
        {
            // The front end shows these files from another origin, so they must stay loadable cross-origin.
            Add(headers, "X-Frame-Options", "DENY");
            Add(headers, "Cross-Origin-Resource-Policy", "cross-origin");

            // Only an SVG can carry a script. Its CSP stops that script when the file is opened directly.
            // Other files get no CSP: the sandbox would stop Chrome from showing a PDF the site links to.
            if (path.Value?.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) == true)
            {
                Add(headers, "Content-Security-Policy", GeneralConstants.MediaCsp);
            }

            return;
        }

        Add(headers, "X-Frame-Options", "DENY");
        Add(headers, "Cross-Origin-Opener-Policy", "same-origin");
        Add(headers, "Cross-Origin-Embedder-Policy", "require-corp");

        // The API is read by the front end from another origin.
        Add(headers, "Cross-Origin-Resource-Policy",
            path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) ? "cross-origin" : "same-origin");

        // The sign-in error page has a small inline script (copy the reference) and sets its own headers, so it gets no CSP.
        if (!path.Equals(GeneralConstants.SignInErrorPath, StringComparison.OrdinalIgnoreCase))
        {
            Add(headers, "Content-Security-Policy", GeneralConstants.DefaultCsp);
        }
    }

    // A header the endpoint has already set is kept.
    private static void Add(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name)) headers[name] = value;
    }
}
