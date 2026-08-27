# Mofam — Umbraco Headless Boilerplate

A headless API boilerplate built on **Umbraco CMS 17.6.2**. Umbraco's backoffice is used purely as a content editor — no Razor views, no public content rendering. All content is served through secured, versioned REST endpoints.

## Tech Stack

- **.NET 10** / Umbraco CMS 17.6.2 (pinned exact version)
- **Serilog** — structured logging (Console + File sinks; optional Sentry integration)
- **Examine** — Umbraco's built-in search/indexing engine
- SQL Server (or any Umbraco-supported provider) via `umbracoDbDSN`

## Project Structure

Clean-architecture style, split across four projects:

```
Mofam.Domain          Options, constants, DTOs, and other framework-agnostic models
Mofam.Infrastructure   Middleware, filters, health checks, and infra-facing services
Mofam.Application      Business logic / application services
Mofam.CMS              Umbraco host — Program.cs, controllers, composers, config
```

Interfaces live in an `Abstractions/` folder alongside their layer (e.g. `Mofam.Application/Abstractions`, `Mofam.Infrastructure/Abstractions`) — this convention is used project-wide.

## Getting Started

1. Clone the repo and open `Mofam.CMS.sln` (or the relevant `.sln`) in Visual Studio / Rider.
2. Copy `appsettings.json` and fill in your own values (see **Configuration** below) — `appsettings.Development.json` is gitignored for local overrides.
3. Point `ConnectionStrings:umbracoDbDSN` at your database.
4. Run the `Mofam.CMS` project. On first run, Umbraco will bootstrap the database (unattended install is enabled).
5. Visit `/` for a status page confirming the API is running and the Umbraco version in use.
6. Visit `/umbraco` to access the backoffice and start creating content.

## Configuration

Key `appsettings.json` sections:

| Section | Purpose |
|---|---|
| `Security:ApiKey` | Shared API key required on all secured endpoints (`X-Api-Key` header) |
| `RateLimit` | Fixed-window rate limiting (`PermitLimit`, `WindowSeconds`) |
| `Cors:AllowedOrigins` | Allow-listed origins for browser requests (empty = deny all, fail-closed) |
| `Sentry` | Optional error tracking — disabled by default; set `Enabled: true` + a real `Dsn` to turn on |
| `Umbraco:CMS:Global:MainDomLock` | Set to `SqlMainDomLock` for multi-instance/load-balanced deployments |
| `Umbraco:CMS:DeliveryApi:Enabled` | Kept `false` — Umbraco's built-in Delivery API is never exposed; only custom controllers below are public |

## API Endpoints

All endpoints (except `/health`) require an `X-Api-Key` header and are rate-limited.

| Endpoint | Description |
|---|---|
| `GET /api/v1/web/pages/{culture}/{slug}` | Fetch a single web page by slug |
| `GET /api/v1/app/pages/{culture}/{slug}` | Fetch a single app page by slug |
| `GET /health` | Unauthenticated health check (DB connectivity + Umbraco runtime state) — for load balancers / uptime monitors |
| `GET /` | Status page — confirms the API is running and shows the Umbraco version |

Responses follow a consistent shape: `ApiResponse<T>` with `StatusCode`, `Success`, `Data`, `Message`, and `TraceId`.

## Request Pipeline (Middleware)

Requests pass through this order (see `Program.cs`):

1. **Response compression** — Brotli + Gzip.
2. **Global exception middleware** — catches any unhandled exception, maps it to a status code, and returns it as `ApiResponse<object>`. This is the *only* place unexpected errors are turned into HTTP responses — nothing upstream re-wraps or re-logs them.
3. **404 normalization middleware** — buffers the response and rewrites empty 404 bodies into the standard `ApiResponse<object>` shape, so a raw/empty 404 never reaches the client.
4. **Request validation middleware** — for `POST`/`PUT`/`PATCH`: enforces `Content-Type: application/json` and rejects malformed JSON before it reaches model binding.
5. **XSS sanitization middleware** — scans `POST`/`PUT`/`PATCH` JSON bodies for `<script>` tags, inline event handlers, and `javascript:` URIs; rejects with 400 if found.
6. **HTTPS redirection → CORS → rate limiting.**
7. Umbraco's own middleware/endpoints (backoffice, website, health check, controllers, status page).

## Routing

- Route parameters are automatically slugified to kebab-case (`RouteTokenTransformerConvention` + `SlugifyParameterTransformer`).
- Controller routes are explicit (`api/v1/web/...`, `api/v1/app/...`) — there's no automatic `/api` prefix convention layered on top, to avoid double-prefixing.

## Health Check

`GET /health` runs a 3-layer check, cached for 10 seconds (`IMemoryCache`) so frequent monitoring hits don't hammer the DB on every call:

1. Umbraco runtime state is `RuntimeLevel.Run` (fully booted).
2. `IUmbracoDatabaseFactory` reports configured + connectable.
3. A real `SELECT 1` round-trip against the database (not just a factory-level check).

It's intentionally the one unauthenticated, public endpoint — by design, for load balancers and uptime monitors.

## Security Notes

- API key comparison uses constant-time equality (`CryptographicOperations.FixedTimeEquals`) to avoid timing attacks; failed attempts are logged with path + remote IP.
- CORS defaults to fail-closed — no origins are allowed until explicitly configured.
- Request bodies are capped at 5 MB (Kestrel `MaxRequestBodySize`) — deliberately finite, not `long.MaxValue`.
- Password policies are enforced for both backoffice users and members (`Umbraco:CMS:Security:UserPassword` / `MemberPassword`) — min length, digit/case/non-alphanumeric requirements, lockout after repeated failures.
- `SanitizeTinyMce` is enabled — strips unsafe HTML/script from rich-text editor output.

## Deployment

- For any multi-instance (load-balanced) deployment, set `MainDomLock` to `SqlMainDomLock` in config — otherwise multiple instances can each think they're the "main" instance.
- Keep real secrets (API keys, connection strings, Sentry DSN) out of source control — use environment variables or a secrets manager in production.
- Response compression is enabled for HTTPS by default — fine for a pure-JSON API; no extra MIME types needed since `application/json` is already covered.
