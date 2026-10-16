using System.Net;
using System.Text.RegularExpressions;
using Mofam.Domain.Constants;

namespace Mofam.CMS.Pages;

// The page users see when the Microsoft sign in does not work.
// The message depends on the reason code. The real cause is never shown, it is only in the log,
// with the same reference code that the page shows.
public static partial class SignInErrorPage
{
    // A reference code: letters, digits, colon, dash, underscore, up to 64 characters.
    [GeneratedRegex("^[A-Za-z0-9:_-]{1,64}$")]
    private static partial Regex ReferencePattern();

    public static string Html(string? reason, string? reference)
    {
        var (title, message) = GeneralConstants.TextFor(reason);

        // Only a safe reference is shown, and it is encoded.
        var referenceHtml = reference is not null && ReferencePattern().IsMatch(reference)
            ? $"<p class=\"ref\">Reference ID: <b id=\"refcode\">{WebUtility.HtmlEncode(reference)}</b><button type=\"button\" class=\"copy\" id=\"copy\" title=\"Copy reference\" aria-label=\"Copy reference\"><svg viewBox=\"0 0 24 24\" width=\"14\" height=\"14\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"9\" y=\"9\" width=\"11\" height=\"11\" rx=\"2\"/><path d=\"M5 15V5a2 2 0 0 1 2-2h10\"/></svg></button></p>"
            : string.Empty;

        return $$"""
            <!DOCTYPE html><html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex"><title>{{GeneralConstants.ClientName}} · {{title}}</title><style>*{box-sizing:border-box}html,body{margin:0;min-height:100%}body{display:flex;justify-content:center;align-items:flex-start;padding:10vh 16px 16px;background:#fdf1ef;color:#fff;font:14px/1.5 -apple-system,'Segoe UI',system-ui,sans-serif;-webkit-font-smoothing:antialiased}.card{position:relative;width:100%;max-width:620px;padding:24px 32px;background:#1b264f;border-radius:20px;overflow:hidden;box-shadow:0 24px 48px -24px rgba(27,38,79,.6)}.bg{position:absolute;right:-70px;top:-70px;width:240px;height:240px;opacity:.12;pointer-events:none}.top{position:relative;display:flex;align-items:center;gap:12px}.top svg{width:38px;height:38px;flex-shrink:0}.top strong{font-size:15px;letter-spacing:.08em}.tag{margin-left:auto;display:inline-flex;align-items:center;gap:7px;padding:5px 12px;border-radius:999px;background:rgba(245,193,188,.16);color:#f5c1bc;font-size:12px;font-weight:600}.dot{width:7px;height:7px;border-radius:50%;background:#f5c1bc}h1{position:relative;margin:22px 0 0;font-size:26px;line-height:1.2;font-weight:700;letter-spacing:-.01em}.note{position:relative;margin-top:14px;padding:14px 16px;border-radius:12px;background:rgba(255,255,255,.06);border:1px solid rgba(255,255,255,.08);color:#d5d9ee;font-size:14px}.note p{margin:0}.actions{position:relative;margin-top:18px}.btn{display:inline-block;padding:11px 22px;border-radius:10px;background:#f5c1bc;color:#1b264f;font-size:14px;font-weight:700;text-decoration:none}.btn:hover,.btn:focus{background:#fbd5d1;outline:none}.ref{position:relative;margin:14px 0 0;color:#a9b0d1;font-size:12px;letter-spacing:.02em;word-break:break-all}.ref b{color:#d5d9ee;font-weight:600}.copy{display:inline-flex;vertical-align:middle;margin-left:8px;padding:4px;border:0;border-radius:6px;background:transparent;color:#a9b0d1;cursor:pointer}.copy:hover,.copy:focus{background:rgba(255,255,255,.1);color:#fff;outline:none}.copy.ok{color:#5fe0a3}</style></head><body><main class="card"><svg class="bg" viewBox="0 0 32 32" aria-hidden="true"><circle cx="16" cy="16" r="15" fill="none" stroke="#f5c1bc" stroke-width="1"/><path d="M10 10v7a6 6 0 0 0 12 0v-7" fill="none" stroke="#f5c1bc" stroke-width="2" stroke-linecap="round"/></svg><div class="top"><svg viewBox="0 0 32 32" aria-hidden="true"><circle cx="16" cy="16" r="16" fill="#f5c1bc"/><path d="M10 10v7a6 6 0 0 0 12 0v-7" fill="none" stroke="#1b264f" stroke-width="2.6" stroke-linecap="round"/></svg><strong>{{GeneralConstants.ClientName}}</strong><span class="tag"><span class="dot"></span>Notice</span></div><h1>{{title}}</h1><div class="note"><p>{{message}}</p></div><div class="actions"><a class="btn" href="/umbraco">Back to sign in</a></div>{{referenceHtml}}</main><script>var c=document.getElementById('copy');if(c){c.addEventListener('click',function(){var t=document.getElementById('refcode').textContent;var d=function(){c.classList.add('ok');c.title='Copied';setTimeout(function(){c.classList.remove('ok');c.title='Copy reference'},1500)};if(navigator.clipboard){navigator.clipboard.writeText(t).then(d)}else{var a=document.createElement('textarea');a.value=t;document.body.appendChild(a);a.select();document.execCommand('copy');a.remove();d()};})}</script></body></html>
            """;
    }
}
