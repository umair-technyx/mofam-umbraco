namespace Mofam.CMS;
public static class StatusPage
{
    private const string ClientName = "Client Name";

    public static string Html(string umbracoVersion) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <style>
                * { box-sizing: border-box; }
                html, body {
                    margin: 0;
                    padding: 0;
                    width: 100%;
                    height: 100%;
                }
                body {
                    min-height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    background:
                        radial-gradient(circle at 15% 10%, rgba(95, 115, 228, 0.4), transparent 45%),
                        radial-gradient(circle at 90% 90%, rgba(11, 129, 82, 0.21), transparent 45%),
                        linear-gradient(160deg, #232963, #313986 55%, #2b3271);
                    font-family: -apple-system, 'Segoe UI', system-ui, sans-serif;
                    -webkit-font-smoothing: antialiased;
                }
                .card {
                    position: relative;
                    background: #ffffff;
                    border-radius: 24px;
                    padding: 60px 72px 50px;
                    text-align: center;
                    min-width: 380px;
                    box-shadow:
                        0 1px 2px rgba(0, 0, 0, 0.06),
                        0 34px 80px -18px rgba(10, 14, 40, 0.55);
                }
                .logo-mark {
                    width: 64px;
                    height: 64px;
                    margin: 0 auto 24px;
                    border-radius: 16px;
                    background: linear-gradient(160deg, #34449e, #283a97);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    box-shadow: 0 12px 26px -6px rgba(40, 58, 151, 0.55);
                }
                .logo-mark svg { width: 32px; height: 32px; }
                .logo { width: 64px; height: 64px; margin: 0 auto 24px; display: block; border-radius: 16px; }
                .client-name {
                    margin: 0;
                    color: #3d4266;
                    font-size: 13px;
                    font-weight: 700;
                    letter-spacing: 0.03em;
                    text-transform: uppercase;
                }
                h1 {
                    margin: 10px 0 0;
                    color: #1b2559;
                    font-size: 26px;
                    font-weight: 700;
                    letter-spacing: -0.01em;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    gap: 12px;
                }
                .dot {
                    position: relative;
                    width: 11px;
                    height: 11px;
                    border-radius: 50%;
                    background: #0b8152;
                    flex-shrink: 0;
                }
                .dot::after {
                    content: "";
                    position: absolute;
                    inset: -6px;
                    border-radius: 50%;
                    background: #0b8152;
                    opacity: 0.35;
                    animation: pulse 2s ease-out infinite;
                }
                @keyframes pulse {
                    0% { transform: scale(0.6); opacity: 0.45; }
                    100% { transform: scale(2.2); opacity: 0; }
                }
                .status-sub {
                    margin: 8px 0 0;
                    color: #0b8152;
                    font-size: 14px;
                    font-weight: 500;
                }
                .meta {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    gap: 10px;
                    margin: 32px 0 0;
                    padding-top: 22px;
                    border-top: 1px solid #eef0f8;
                    color: #3d4266;
                    font-size: 11px;
                    font-weight: 700;
                    letter-spacing: 0.06em;
                    text-transform: uppercase;
                }
                .meta span:not(:last-child)::after {
                    content: "•";
                    margin-left: 10px;
                    color: #c3c7e2;
                }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="logo-mark">
                    <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M6 4v9a6 6 0 0 0 12 0V4" stroke="#ffffff" stroke-width="2.4" stroke-linecap="round" />
                    </svg>
                </div>
                <p class="client-name">{{ClientName}}</p>
                <h1><span class="dot"></span>API is Running</h1>
                <p class="status-sub">All systems operational</p>
                <div class="meta">
                    <span>API Status</span>
                    <span>Umbraco v{{umbracoVersion}}</span>
                </div>
            </div>
        </body>
        </html>
        """;
}
